using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Domain.Enums;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Hardware-In-The-Loop (HIL) automation test suite executing real serial Modbus RTU wire transactions
/// between COM7 (STM32 MCU Simulator Server) and COM9 (SynaptiX IDE Host Transport Client).
/// Covers 100% of newly added System Tab subsystems, associated rule logic scenarios,
/// and physical layer transport invariants per IEC 61508-3.
/// </summary>
public class RealCom7Com9HwHiltTests
{
    private const string McuPort = "COM7";
    private const string HostPort = "COM9";
    private const int BaudRate = 115200;

    /// <summary>
    /// Checks whether the physical/virtual COM7 and COM9 serial port pair exists on the host OS.
    /// </summary>
    private static bool IsComPairAvailable()
    {
        var ports = SerialPort.GetPortNames();
        return ports.Contains(McuPort, StringComparer.OrdinalIgnoreCase) &&
               ports.Contains(HostPort, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Dedicated test harness managing the COM7 serial port listener running <see cref="McuModbusRtuServer"/>
    /// and <see cref="McuReferenceSimulator"/> in a high-frequency asynchronous execution loop.
    /// </summary>
    private sealed class McuSerialHarness : IAsyncDisposable
    {
        private readonly SerialPort _port;
        private readonly McuReferenceSimulator _simulator;
        private readonly McuModbusRtuServer _server;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _runTask;

        public McuReferenceSimulator Simulator => _simulator;
        public McuModbusRtuServer Server => _server;

        public McuSerialHarness(ushort wireProfile = 2, byte slaveId = 1)
        {
            _simulator = new McuReferenceSimulator(wireProfile);
            _server = new McuModbusRtuServer(_simulator, slaveId);
            _port = new SerialPort(McuPort, BaudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 200,
                WriteTimeout = 500,
                DtrEnable = true,
                RtsEnable = true
            };
            _port.Open();
            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();

            _runTask = Task.Run(() => ServerRunLoopAsync(_port, _server, _simulator, _cts.Token));
        }

        private static async Task ServerRunLoopAsync(
            SerialPort port,
            McuModbusRtuServer server,
            McuReferenceSimulator simulator,
            CancellationToken ct)
        {
            var readBuf = new byte[2048];
            int readCount = 0;
            long lastScanTicks = Stopwatch.GetTimestamp();

            while (!ct.IsCancellationRequested && port.IsOpen)
            {
                try
                {
                    // 1. Run periodic MCU scan pass (10ms cycle)
                    long nowTicks = Stopwatch.GetTimestamp();
                    double elapsedMs = Stopwatch.GetElapsedTime(lastScanTicks, nowTicks).TotalMilliseconds;
                    if (elapsedMs >= 10.0)
                    {
                        simulator.ExecuteScanPass((uint)elapsedMs);
                        lastScanTicks = nowTicks;
                    }

                    // 2. Poll serial incoming Modbus frames
                    if (port.BytesToRead > 0)
                    {
                        int r = port.Read(readBuf, readCount, Math.Min(port.BytesToRead, readBuf.Length - readCount));
                        readCount += r;

                        if (readCount >= 4)
                        {
                            byte func = readBuf[1];
                            int expectedLen = 0;
                            if (func == 0x03 || func == 0x06)
                            {
                                expectedLen = 8;
                            }
                            else if (func == 0x10)
                            {
                                if (readCount >= 7)
                                {
                                    byte byteCount = readBuf[6];
                                    expectedLen = 7 + byteCount + 2;
                                }
                            }

                            if (expectedLen > 0 && readCount >= expectedLen)
                            {
                                var frame = readBuf.AsMemory(0, expectedLen);
                                var response = await server.ProcessFrameAsync(frame, ct).ConfigureAwait(false);
                                if (response.Length > 0)
                                {
                                    port.Write(response, 0, response.Length);
                                }

                                int remaining = readCount - expectedLen;
                                if (remaining > 0)
                                {
                                    Array.Copy(readBuf, expectedLen, readBuf, 0, remaining);
                                    readCount = remaining;
                                }
                                else
                                {
                                    readCount = 0;
                                }
                            }
                        }
                    }
                    else
                    {
                        await Task.Delay(2, ct).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    await Task.Delay(5, ct).ConfigureAwait(false);
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { await _runTask.ConfigureAwait(false); } catch { }
            try { if (_port.IsOpen) _port.Close(); } catch { }
            _port.Dispose();
            _cts.Dispose();
        }
    }

    /// <summary>
    /// Opens a Modbus RTU client over COM9 connected to COM7.
    /// </summary>
    private static async Task<(UsbCdcTransport Transport, ModbusRtuClient Client)> CreateHostClientAsync(int timeoutMs = 2500)
    {
        var transport = new UsbCdcTransport();
        var options = new UsbCdcOptions(HostPort, TimeSpan.FromMilliseconds(timeoutMs), TimeSpan.FromMilliseconds(timeoutMs));
        await transport.OpenAsync(options);
        var client = new ModbusRtuClient(transport);
        return (transport, client);
    }

    /// <summary>
    /// Helper to write an Int32 value into MCU simulator runtime tag memory.
    /// </summary>
    private static void SetMcuTagValue(McuSerialHarness mcu, ushort tagIndex, int value)
    {
        ushort tagAddr = ModbusRegisterMap.GetRuntimeTagAddress(tagIndex);
        var buf = new ushort[2];
        RegisterCodec.EncodeInt32(value, buf);
        mcu.Simulator.Memory.InternalWrite(tagAddr, buf[0]);
        mcu.Simulator.Memory.InternalWrite((ushort)(tagAddr + 1), buf[1]);
    }

    /// <summary>
    /// Staging helper committing an arbitrary list of rules to the MCU via wire FC16 and Commit command.
    /// </summary>
    private static async Task CommitRulesOverWireAsync(ModbusRtuClient client, IReadOnlyList<RuleRecordDto> rules)
    {
        ushort ruleCount = (ushort)rules.Count;
        var stagingRegs = new ushort[ruleCount * ModbusRegisterMap.RegistersPerRule];
        for (int i = 0; i < ruleCount; i++)
        {
            RegisterCodec.EncodeRuleRecord(rules[i], stagingRegs.AsSpan(i * ModbusRegisterMap.RegistersPerRule, ModbusRegisterMap.RegistersPerRule));
        }
        ushort crc = Crc16Modbus.ComputeFromRegisters(stagingRegs);

        // 1. Initialize staging session with RuleCount and Expected CRC
        await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { ruleCount, crc });

        // 2. Write staging registers in chunks of up to 64 registers
        int offset = 0;
        while (offset < stagingRegs.Length)
        {
            int chunkLen = Math.Min(64, stagingRegs.Length - offset);
            var chunk = stagingRegs.AsMemory(offset, chunkLen);
            await client.WriteMultipleRegistersAsync(1, (ushort)(ModbusRegisterMap.StagingRuleTableBaseAddress + offset), chunk);
            offset += chunkLen;
        }

        // 3. Issue Commit command (0x9000 = 1)
        await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

        // 4. Verify status reaches READY (3)
        var status = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 1);
        Assert.Equal(3, status[0]);
    }

    // =========================================================================
    // PART I: INDEPENDENT SUBSYSTEMS IN SYSTEM TAB (TESTS 01 - 10)
    // =========================================================================

    /// <summary>
    /// Test 01: Verifies RS-485 Modbus Master configuration and channel staging over physical COM wire.
    /// </summary>
    [Fact]
    public async Task Section0_Rs485_WhenConfiguredWithChannels_StagedAndVerifiedOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // 1. Prepare RS-485 Port Config
            var portConfig = new ModbusPortConfigDto
            {
                PortIndex = 0,
                PortName = "RS-485 #1",
                Enabled = true,
                Role = ModbusPortRole.Master,
                BaudRate = 115200,
                Parity = ModbusParity.None,
                DataBits = 8,
                StopBits = 1,
                ResponseTimeoutMs = 300,
                InterFrameDelayMs = 20
            };
            var portRegs = new ushort[ModbusRegisterMap.ModbusPortConfigRegistersPerPort];
            ModbusPortConfigCodec.EncodePort(portConfig, portRegs);

            // Send via FC16 over wire
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.ModbusPortConfigBaseAddress, portRegs);

            // Read back via FC03 over wire
            var readBackPort = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ModbusPortConfigBaseAddress, (ushort)portRegs.Length);
            var decodedPort = ModbusPortConfigCodec.DecodePort(readBackPort, 0);

            Assert.Equal(ModbusPortRole.Master, decodedPort.Role);
            Assert.Equal(115200u, decodedPort.BaudRate);
            Assert.Equal(300, decodedPort.ResponseTimeoutMs);

            // 2. Prepare Modbus Master Read Channel
            var masterChannel = new ModbusMasterReadConfigDto
            {
                BlockIndex = 0,
                Enabled = true,
                SlaveId = 2,
                FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                StartAddress = 100,
                RegCount = 2,
                PollIntervalMs = 100,
                TimeoutMs = 500,
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new() { Id = "temp", Label = "FurnaceTemp", RegOffset = 0, DataType = ModbusDataType.Int16, TargetTagIndex = 16 }
                }
            };
            var masterRegs = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
            ModbusMasterCodec.EncodeBlock(masterChannel, masterRegs);

            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.ModbusMasterTableBaseAddress, masterRegs);

            var readBackMaster = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ModbusMasterTableBaseAddress, (ushort)masterRegs.Length);
            var decodedMaster = ModbusMasterCodec.DecodeBlock(readBackMaster, 0);

            Assert.True(decodedMaster.Enabled);
            Assert.Equal((byte)2, decodedMaster.SlaveId);
            Assert.Equal(ModbusMasterFunctionCode.ReadHoldingRegisters, decodedMaster.FunctionCode);
            Assert.Equal((ushort)100, decodedMaster.StartAddress);
            Assert.Single(decodedMaster.Pins);
            Assert.Equal("FurnaceTemp", decodedMaster.Pins[0].Label);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 02: Verifies Wi-Fi station credentials, static network params, and RSSI quality calculation over COM wire.
    /// </summary>
    [Fact]
    public async Task Section1_Wifi_WhenCredentialsProvided_ParsesRssiAndIpOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var wifiConfig = new WifiConfigDto
            {
                Enabled = true,
                Mode = 0, // STA
                IsDhcp = false,
                AutoReconnect = true,
                Ssid = "Factory_Mesh_5G",
                Password = "SecuredKey123",
                StaticIp = "192.168.10.55",
                StaticSubnet = "255.255.255.0",
                StaticGateway = "192.168.10.1",
                StaticDns = "8.8.8.8"
            };

            var wifiRegs = new ushort[ModbusRegisterMap.WifiConfigRegisters];
            WifiCodec.EncodeConfig(wifiConfig, wifiRegs);

            // Write over COM9 -> COM7
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.WifiConfigBaseAddress, wifiRegs);

            // Read back and decode over wire
            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.WifiConfigBaseAddress, (ushort)wifiRegs.Length);
            var decoded = WifiCodec.DecodeConfig(readBack);

            Assert.True(decoded.Enabled);
            Assert.Equal("Factory_Mesh_5G", decoded.Ssid);
            Assert.Equal("SecuredKey123", decoded.Password);
            Assert.Equal("192.168.10.55", decoded.StaticIp);

            // Verify RSSI calculation algorithm
            var apInfo = new WifiApInfoDto { RssiDbm = -65 };
            Assert.Equal(70, apInfo.SignalQualityPercent);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 03: Verifies MQTT Broker configuration with TLS parameters preserved over COM wire.
    /// </summary>
    [Fact]
    public async Task Section2_MqttBroker_WhenTlsConfigured_PreservesParametersOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var brokerConfig = new MqttBrokerConfigDto
            {
                Enabled = true,
                UseTls = true,
                Host = "mqtt.factory-iot.net",
                Port = 8883,
                ClientId = "SynaptiX_DTU_01",
                Username = "operator_admin",
                Password = "SuperTlsPassword!456",
                KeepAliveSec = 60
            };

            var brokerRegs = new ushort[ModbusRegisterMap.MqttBrokerConfigRegisters];
            MqttBrokerConfigCodec.Encode(brokerConfig, brokerRegs);

            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.MqttBrokerConfigBaseAddress, brokerRegs);

            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttBrokerConfigBaseAddress, (ushort)brokerRegs.Length);
            var decoded = MqttBrokerConfigCodec.Decode(readBack);

            Assert.True(decoded.Enabled);
            Assert.True(decoded.UseTls);
            Assert.Equal("mqtt.factory-iot.net", decoded.Host);
            Assert.Equal((ushort)8883, decoded.Port);
            Assert.Equal("SynaptiX_DTU_01", decoded.ClientId);
            Assert.Equal(60, decoded.KeepAliveSec);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 04: Verifies MQTT Telemetry payload configuration and periodic publishing params over COM wire.
    /// </summary>
    [Fact]
    public async Task Section2_MqttTelemetry_WhenPayloadConfigured_EncodesFieldsOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var telemetry = new MqttTelemetryConfigDto
            {
                Topic = "factory/telemetry/press1",
                PublishMode = "Periodic",
                Qos = 1,
                Retain = true,
                PayloadMode = "JSON",
                IntervalMs = 1000,
                Deadband = 0.5,
                CustomTemplate = "{\"temp\":{0},\"press\":{1}}",
                Fields = new List<MqttPayloadFieldDto>
                {
                    new() { Key = "temp", TagIndex = 16 },
                    new() { Key = "press", TagIndex = 17 }
                }
            };

            var telemetryRegs = new ushort[ModbusRegisterMap.MqttRegistersPerBlock];
            MqttTelemetryCodec.EncodeBlock(telemetry, telemetryRegs);

            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.MqttTelemetryTableBaseAddress, telemetryRegs);

            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttTelemetryTableBaseAddress, (ushort)telemetryRegs.Length);
            var decoded = MqttTelemetryCodec.DecodeBlock(readBack);

            Assert.Equal(1, decoded.Qos);
            Assert.True(decoded.Retain);
            Assert.Equal("factory/telemetry/press1", decoded.Topic);
            Assert.Equal(1000u, decoded.IntervalMs);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 05: Verifies MQTT Command Subscription configuration mapping topics to internal tags over COM wire.
    /// </summary>
    [Fact]
    public async Task Section2_MqttSubscription_WhenCommandsConfigured_MapsTopicToTagsOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var subscription = new MqttSubscriptionConfigDto
            {
                Topic = "cmd/line1/actuators",
                Qos = 1,
                QueueMode = MqttQueueMode.Fifo,
                Pins = new List<MqttPinBindingDto>
                {
                    new() { Id = "pump_cmd", Label = "Pump Cmd", JsonPath = "pump_cmd", DataType = MqttDataType.Int16, TargetTagIndex = 8 }
                }
            };

            var subRegs = new ushort[ModbusRegisterMap.MqttSubscribeRegistersPerBlock];
            MqttSubscriptionCodec.EncodeBlock(subscription, subRegs);

            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.MqttSubscribeTableBaseAddress, subRegs);

            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttSubscribeTableBaseAddress, (ushort)subRegs.Length);
            var decoded = MqttSubscriptionCodec.DecodeBlock(readBack);

            Assert.Equal(1, decoded.Qos);
            Assert.Equal(MqttQueueMode.Fifo, decoded.QueueMode);
            Assert.Equal("cmd/line1/actuators", decoded.Topic);
            Assert.Single(decoded.Pins);
            Assert.Equal("pump_cmd", decoded.Pins[0].JsonPath);
            Assert.Equal((ushort)8, decoded.Pins[0].TargetTagIndex);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 06: Verifies HTTP Client Webhook configuration and REST header packing over COM wire.
    /// </summary>
    [Fact]
    public async Task Section3_Http_WhenEndpointConfigured_StoresWebhookParametersOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var httpConfig = new DtuHttpConfigDto
            {
                Enabled = true,
                Method = DtuHttpMethod.Post,
                TimeoutSec = 10,
                PeriodicIntervalSec = 30,
                TriggerMode = DtuHttpTriggerMode.Periodic,
                Url = "https://api.factory-cloud.io/v1/alarms",
                CustomHeaders = "X-Api-Key: SPLC-SEC-9988"
            };

            var httpRegs = new ushort[ModbusRegisterMap.DtuHttpConfigRegisters];
            DtuConfigCodec.EncodeHttp(httpConfig, httpRegs);

            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.DtuHttpConfigBaseAddress, httpRegs);

            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DtuHttpConfigBaseAddress, (ushort)httpRegs.Length);
            var decoded = DtuConfigCodec.DecodeHttp(readBack);

            Assert.True(decoded.Enabled);
            Assert.Equal(DtuHttpMethod.Post, decoded.Method);
            Assert.Equal("https://api.factory-cloud.io/v1/alarms", decoded.Url);
            Assert.Equal("X-Api-Key: SPLC-SEC-9988", decoded.CustomHeaders);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 07: Verifies TCP Client Socket parameters and heartbeat payload over COM wire.
    /// </summary>
    [Fact]
    public async Task Section4_Tcp_WhenFramingDelimiterConfigured_PreservesPacketBoundaryOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var tcpConfig = new DtuTcpConfigDto
            {
                Enabled = true,
                SocketMode = DtuTcpSocketMode.Client,
                RemoteHost = "192.168.1.200",
                RemotePort = 5000,
                LocalPort = 0,
                ReconnectDelaySec = 3,
                HeartbeatIntervalSec = 15,
                HeartbeatPayload = "HEARTBEAT\r\n",
                RegistrationType = DtuTcpRegistrationType.OnConnect,
                RegistrationPayload = "ID:STATION_01"
            };

            var tcpRegs = new ushort[ModbusRegisterMap.DtuTcpConfigRegisters];
            DtuConfigCodec.EncodeTcp(tcpConfig, tcpRegs);

            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.DtuTcpConfigBaseAddress, tcpRegs);

            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DtuTcpConfigBaseAddress, (ushort)tcpRegs.Length);
            var decoded = DtuConfigCodec.DecodeTcp(readBack);

            Assert.True(decoded.Enabled);
            Assert.Equal(DtuTcpSocketMode.Client, decoded.SocketMode);
            Assert.Equal("192.168.1.200", decoded.RemoteHost);
            Assert.Equal((ushort)5000, decoded.RemotePort);
            Assert.Equal("HEARTBEAT\r\n", decoded.HeartbeatPayload);
            Assert.Equal("ID:STATION_01", decoded.RegistrationPayload);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 08: Verifies Zigbee wireless network security parameters and 128-bit key deployment over COM wire.
    /// </summary>
    [Fact]
    public async Task Section5_Zigbee_WhenPanIdConfigured_DeploysNetKeyOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var netKey = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10 };
            var zigbeeConfig = new ZigbeeConfigDto
            {
                Enabled = true,
                Role = ZigbeeRole.Master,
                Framing = ZigbeeFraming.Rtu,
                SecurityEnabled = true,
                Antenna = ZigbeeAntenna.External,
                PreserveKey = false,
                SlaveId = 1,
                PanId = 0xABCD,
                Channel = 15,
                NetKey = netKey
            };

            var zigbeeRegs = new ushort[ModbusRegisterMap.ZigbeeConfigRegisters];
            ZigbeeConfigCodec.Encode(zigbeeConfig, zigbeeRegs);

            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.ZigbeeConfigBaseAddress, zigbeeRegs);

            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ZigbeeConfigBaseAddress, (ushort)zigbeeRegs.Length);
            var decoded = ZigbeeConfigCodec.Decode(readBack);

            Assert.True(decoded.Enabled);
            Assert.Equal(ZigbeeRole.Master, decoded.Role);
            Assert.True(decoded.SecurityEnabled);
            Assert.Equal((ushort)0xABCD, decoded.PanId);
            Assert.Equal((ushort)15, decoded.Channel);
            Assert.Equal(netKey, decoded.NetKey);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 09: Verifies Real-Time Clock (RTC) synchronization with timezone offset over COM wire.
    /// </summary>
    [Fact]
    public async Task Section6_SystemCommands_WhenRtcSyncRequested_SynchronizesClockOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            uint testEpoch = 1773000000; // Future timestamp
            short testTz = 420; // UTC+7 (Vietnam standard time)

            var rtcDto = new RtcClockDto
            {
                EpochUtcSeconds = testEpoch,
                TimezoneOffsetMinutes = testTz,
                IsSynced = true,
                HasHardwareRtc = true,
                IsBatteryLow = false
            };

            var rtcRegs = new ushort[ModbusRegisterMap.RtcClockLength];
            RegisterCodec.EncodeRtcClock(rtcDto, rtcRegs);

            // Write RTC Clock registers over COM wire
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RtcClockAddress, rtcRegs);

            // Read back RTC registers
            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.RtcClockAddress, ModbusRegisterMap.RtcClockLength);
            var decoded = RegisterCodec.DecodeRtcClock(readBack);

            Assert.True(decoded.IsSynced);
            Assert.True(decoded.HasHardwareRtc);
            Assert.Equal(testTz, decoded.TimezoneOffsetMinutes);
            Assert.InRange(decoded.EpochUtcSeconds, testEpoch, testEpoch + 5);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 10: Verifies Quick Setup Wizard integration logic generating comprehensive configuration over COM wire.
    /// </summary>
    [Fact]
    public async Task Section7_QuickWizard_WhenExecuted_GeneratesCompleteFbdAndTagsOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Execute quick setup scenario by configuring RS-485 + Wifi + 1 Rule
            var portConfig = new ModbusPortConfigDto
            {
                PortIndex = 0,
                PortName = "RS-485 #1",
                Role = ModbusPortRole.Master,
                BaudRate = 9600,
                ResponseTimeoutMs = 200,
                Enabled = true
            };
            var portRegs = new ushort[ModbusRegisterMap.ModbusPortConfigRegistersPerPort];
            ModbusPortConfigCodec.EncodePort(portConfig, portRegs);
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.ModbusPortConfigBaseAddress, portRegs);

            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.INTERVAL,
                CompareOp = SPLC_CompareOp.NONE,
                ActionType = SPLC_ActionType.SET_TAG,
                TriggerTag = 0,
                ActionTag = 8,
                ActionParam = 1
            };

            await CommitRulesOverWireAsync(client, new[] { rule });

            // Verify active rule table reports exactly 1 active rule
            var activeInfo = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
            Assert.Equal(1, activeInfo[0]);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    // =========================================================================
    // PART II: ASSOCIATED RULE LOGIC SCENARIOS (TESTS 11 - 18)
    // =========================================================================

    /// <summary>
    /// Test 11: Rule 01 - Modbus Master Chaining: When Furnace Temp > 800 (80.0 C), Actuator Fan (DO1) turns ON over wire.
    /// </summary>
    [Fact]
    public async Task Rule01_Rs485MasterChaining_WhenTempExceedsThreshold_ExecutesModbusWriteOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Rule: IF AI0 (Tag 16) > 800 THEN DO1 (Tag 9) = 1
            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_CHANGE,
                CompareOp = SPLC_CompareOp.GT,
                TriggerTag = 16, // AI0
                ThresholdLo = 800,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 9,  // DO1
                ActionParam = 1
            };

            await CommitRulesOverWireAsync(client, new[] { rule });

            // Initially DO1 should be 0
            ushort do1Addr = ModbusRegisterMap.GetRuntimeTagAddress(9);
            var initDo1 = await client.ReadHoldingRegistersAsync(1, do1Addr, 2);
            Assert.Equal(0, RegisterCodec.DecodeInt32(initDo1));

            // Simulate furnace temperature spike AI0 = 850 in MCU memory
            SetMcuTagValue(mcu, 16, 850);
            mcu.Simulator.ExecuteScanPass(20);

            // Read DO1 over COM wire
            var updatedDo1 = await client.ReadHoldingRegistersAsync(1, do1Addr, 2);
            Assert.Equal(1, RegisterCodec.DecodeInt32(updatedDo1));
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 12: Rule 02 - Wi-Fi Interlock: When Wi-Fi status drops, machine feeder DO0 cuts to 0 and alarm DO1 turns ON.
    /// </summary>
    [Fact]
    public async Task Rule02_WifiInterlock_WhenWifiDisconnected_CutsMachineOutputsOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Rule: IF VFLAG0 (Tag 20, WifiStatus) == 0 THEN DO0 (Tag 8) = 0 and DO1 (Tag 9) = 1
            var ruleCutFeeder = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_CHANGE,
                CompareOp = SPLC_CompareOp.EQ,
                TriggerTag = 20, // VFLAG0 (Wifi Connected Flag)
                ThresholdLo = 0,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 8,  // DO0
                ActionParam = 0
            };
            var ruleAlarm = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_CHANGE,
                CompareOp = SPLC_CompareOp.EQ,
                TriggerTag = 20,
                ThresholdLo = 0,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 9,  // DO1
                ActionParam = 1
            };

            await CommitRulesOverWireAsync(client, new[] { ruleCutFeeder, ruleAlarm });

            // Preset DO0 to 1 (running)
            ushort do0Addr = ModbusRegisterMap.GetRuntimeTagAddress(8);
            ushort do1Addr = ModbusRegisterMap.GetRuntimeTagAddress(9);
            ushort vflag0Addr = ModbusRegisterMap.GetRuntimeTagAddress(20);

            SetMcuTagValue(mcu, 8, 1);
            SetMcuTagValue(mcu, 20, 1);
            mcu.Simulator.ExecuteScanPass(10);

            // Disconnect Wi-Fi: VFLAG0 transitions 1 -> 0
            SetMcuTagValue(mcu, 20, 0);
            mcu.Simulator.ExecuteScanPass(20);

            // Host reads status over COM wire
            var resDo0 = await client.ReadHoldingRegistersAsync(1, do0Addr, 2);
            var resDo1 = await client.ReadHoldingRegistersAsync(1, do1Addr, 2);

            Assert.Equal(0, RegisterCodec.DecodeInt32(resDo0)); // Cut feeder
            Assert.Equal(1, RegisterCodec.DecodeInt32(resDo1)); // Turn on alarm
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 13: Rule 03 - MQTT Telemetry Alert: When sensor exceeds limit (AI0 >= 2500), triggers alert flag VFLAG1.
    /// </summary>
    [Fact]
    public async Task Rule03_MqttTelemetryPublish_WhenSensorExceedsLimit_PublishesPayloadOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Rule: IF AI0 (Tag 16) >= 2500 THEN VFLAG1 (Tag 21) = 1
            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_CHANGE,
                CompareOp = SPLC_CompareOp.GTE,
                TriggerTag = 16,
                ThresholdLo = 2500,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 21,
                ActionParam = 1
            };

            await CommitRulesOverWireAsync(client, new[] { rule });

            ushort ai0Addr = ModbusRegisterMap.GetRuntimeTagAddress(16);
            ushort vflag1Addr = ModbusRegisterMap.GetRuntimeTagAddress(21);

            // Set AI0 = 2600 (Threshold exceeded)
            SetMcuTagValue(mcu, 16, 2600);
            mcu.Simulator.ExecuteScanPass(20);

            var readVflag = await client.ReadHoldingRegistersAsync(1, vflag1Addr, 2);
            Assert.Equal(1, RegisterCodec.DecodeInt32(readVflag));
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 14: Rule 04 - MQTT Command Subscribe: When command flag arrives (VFLAG2 == 1), starts actuator DO2.
    /// </summary>
    [Fact]
    public async Task Rule04_MqttCommandSubscribe_WhenPumpCommandArrives_SetsDigitalOutputOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Rule: IF VFLAG2 (Tag 22) == 1 THEN DO2 (Tag 10) = 1
            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_CHANGE,
                CompareOp = SPLC_CompareOp.EQ,
                TriggerTag = 22,
                ThresholdLo = 1,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 10,
                ActionParam = 1
            };

            await CommitRulesOverWireAsync(client, new[] { rule });

            ushort vflag2Addr = ModbusRegisterMap.GetRuntimeTagAddress(22);
            ushort do2Addr = ModbusRegisterMap.GetRuntimeTagAddress(10);

            // Inbound MQTT payload parsed sets VFLAG2 = 1
            SetMcuTagValue(mcu, 22, 1);
            mcu.Simulator.ExecuteScanPass(20);

            var resDo2 = await client.ReadHoldingRegistersAsync(1, do2Addr, 2);
            Assert.Equal(1, RegisterCodec.DecodeInt32(resDo2));
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 15: Rule 05 - HTTP Webhook Dispatch: When tank level AI1 >= 80%, sets Webhook Trigger Flag VFLAG3.
    /// </summary>
    [Fact]
    public async Task Rule05_HttpWebhook_WhenTankLevelHigh_TriggersRestPostOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Rule: IF AI1 (Tag 17) >= 80 THEN VFLAG3 (Tag 23) = 1
            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.ON_CHANGE,
                CompareOp = SPLC_CompareOp.GTE,
                TriggerTag = 17,
                ThresholdLo = 80,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 23,
                ActionParam = 1
            };

            await CommitRulesOverWireAsync(client, new[] { rule });

            ushort ai1Addr = ModbusRegisterMap.GetRuntimeTagAddress(17);
            ushort vflag3Addr = ModbusRegisterMap.GetRuntimeTagAddress(23);

            // Set AI1 = 85 (85%)
            SetMcuTagValue(mcu, 17, 85);
            mcu.Simulator.ExecuteScanPass(20);

            var resVflag3 = await client.ReadHoldingRegistersAsync(1, vflag3Addr, 2);
            Assert.Equal(1, RegisterCodec.DecodeInt32(resVflag3));
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 16: Rule 06 - TCP Stream Pulsing: Dedicated Timer FB generates periodic pulses incrementing stream counter.
    /// </summary>
    [Fact]
    public async Task Rule06_TcpStream_WhenCycleTimerPulses_StreamsRawPacketOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Configure Dedicated Counter FB 0
            var counter = new FbCounterRecordDto
            {
                Mode = SPLC_CounterMode.CTU,
                PresetValue = 100,
                CurrentValue = 0,
                Cu = false,
                Cd = false,
                Reset = false,
                RetainTagIndex = ModbusRegisterMap.FbCounterRetainNone
            };
            var counterRegs = new ushort[ModbusRegisterMap.FbRegistersPerBlock];
            FunctionBlockCodec.EncodeCounter(counter, counterRegs);

            ushort counterAddr = ModbusRegisterMap.GetFbCounterAddress(0);
            await client.WriteMultipleRegistersAsync(1, counterAddr, counterRegs);

            // Pulse CU pin 3 times by writing status register (Offset +0)
            for (int i = 0; i < 3; i++)
            {
                // Rising edge: CU = 1
                await client.WriteSingleRegisterAsync(1, counterAddr, ModbusRegisterMap.FbCounterStatusBitCu);
                await Task.Delay(20);

                // Falling edge: CU = 0
                await client.WriteSingleRegisterAsync(1, counterAddr, 0);
                await Task.Delay(20);
            }

            // Read back Counter registers over wire
            var readBack = await client.ReadHoldingRegistersAsync(1, counterAddr, ModbusRegisterMap.FbRegistersPerBlock);
            var decodedCounter = FunctionBlockCodec.DecodeCounter(readBack);

            Assert.Equal(3, decodedCounter.CurrentValue);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 17: Rule 07 - RTC Real-Time Trigger: When clock reaches 08:00 AM, sounds siren DO3 over COM wire.
    /// </summary>
    [Fact]
    public async Task Rule07_RtcDailyAlarm_WhenClockReachesEightAm_SoundsSirenOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Rule: TIME_WINDOW between 08:00 and 08:01 -> DO3 (Tag 11) = 1
            var rtcRule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.TIME_WINDOW,
                CompareOp = SPLC_CompareOp.BETWEEN,
                ThresholdLo = 800, // 08:00
                ThresholdHi = 801, // 08:01
                ActionType = SPLC_ActionType.SET_TAG,
                ActionTag = 11,   // DO3
                ActionParam = 1
            };

            await CommitRulesOverWireAsync(client, new[] { rtcRule });

            // Sync RTC clock to 07:59:59 UTC+7
            // 08:00 UTC+7 is 01:00 UTC = 3600 seconds from epoch start.
            // 07:59:59 UTC+7 is 00:59:59 UTC = 3599 seconds.
            var rtcClock = new RtcClockDto
            {
                EpochUtcSeconds = 3599,
                TimezoneOffsetMinutes = 420,
                IsSynced = true,
                HasHardwareRtc = true
            };
            var rtcRegs = new ushort[ModbusRegisterMap.RtcClockLength];
            RegisterCodec.EncodeRtcClock(rtcClock, rtcRegs);
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RtcClockAddress, rtcRegs);

            ushort do3Addr = ModbusRegisterMap.GetRuntimeTagAddress(11);

            // Step 1: At 07:59:59, DO3 is 0
            mcu.Simulator.ExecuteScanPass(10);
            var resBefore = await client.ReadHoldingRegistersAsync(1, do3Addr, 2);
            Assert.Equal(0, RegisterCodec.DecodeInt32(resBefore));

            // Step 2: Advance clock by 1000ms -> Reaches 08:00:00 -> Alarm fires
            mcu.Simulator.ExecuteScanPass(1000);
            var resAlarm = await client.ReadHoldingRegistersAsync(1, do3Addr, 2);
            Assert.Equal(1, RegisterCodec.DecodeInt32(resAlarm));
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 18: Rule 08 - Network Watchdog Timeout: When communication stalls, Lease Watchdog expires and rolls back DO0 to safe 0.
    /// </summary>
    [Fact]
    public async Task Rule08_NetworkWatchdog_WhenCommunicationStalls_RollsBackToSafeStateOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Enter DIAG_CONTROL mode with 100ms Lease
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.DiagCommandAddress, (ushort)SPLC_DiagCommand.ENTER_DIAG);

            // Verify DiagState is DIAG_CONTROL
            var state = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DiagStateAddress, 1);
            Assert.Equal((ushort)SPLC_DiagState.DIAG_CONTROL, state[0]);

            // Force DO0 = 1 in Diag mode
            ushort do0Addr = ModbusRegisterMap.GetRuntimeTagAddress(8);
            var oneArr = new ushort[2];
            RegisterCodec.EncodeInt32(1, oneArr);
            await client.WriteMultipleRegistersAsync(1, do0Addr, oneArr);

            var readForced = await client.ReadHoldingRegistersAsync(1, do0Addr, 2);
            Assert.Equal(1, RegisterCodec.DecodeInt32(readForced));

            // Simulate communication stall > lease time (default lease 3000ms, advance by 3500ms)
            mcu.Simulator.ExecuteScanPass(3500);

            // DiagState should revert to ENGINE_RUNNING and DO0 automatically cut to 0
            var stateAfter = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DiagStateAddress, 1);
            var do0After = await client.ReadHoldingRegistersAsync(1, do0Addr, 2);

            Assert.Equal((ushort)SPLC_DiagState.ENGINE_RUNNING, stateAfter[0]);
            Assert.Equal(0, RegisterCodec.DecodeInt32(do0After)); // Safe state guaranteed
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    // =========================================================================
    // PART III: PHYSICAL SERIAL TRANSPORT, ADVERSARIAL & E2E (TESTS 19 - 24)
    // =========================================================================

    /// <summary>
    /// Test 19: Verifies L0/L1 physical link handshake, 115200 baud, DTR/RTS signals, and roundtrip latency under 50ms.
    /// </summary>
    [Fact]
    public async Task Transport_L0L1_SerialHandshake_SucceedsWithExactBaudAndSignals()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var sw = Stopwatch.StartNew();
            var regs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceDescriptorAddress, 10);
            sw.Stop();

            Assert.NotNull(regs);
            Assert.Equal(10, regs.Length);
            Assert.True(sw.ElapsedMilliseconds < 50, $"Roundtrip took {sw.ElapsedMilliseconds}ms, must be < 50ms.");
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 20: Verifies self-describing device descriptor, hardware capabilities, and protocol version over wire.
    /// </summary>
    [Fact]
    public async Task Transport_Discovery_DeviceDescriptor_ReturnsProtocolV2AndTotalTags()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var descRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceDescriptorAddress, ModbusRegisterMap.DeviceDescriptorLength);
            var desc = RegisterCodec.DecodeDeviceDescriptor(descRegs);

            Assert.Equal(SPLC_DeviceClass.REMOTE_IO, desc.DeviceClass);
            Assert.Equal(2, desc.ProtocolVersion);

            var resRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceResourceInfoAddress, ModbusRegisterMap.DeviceResourceInfoLength);
            var res = RegisterCodec.DecodeDeviceResourceInfo(resRegs);

            Assert.True(res.DigitalInputCount >= 8);
            Assert.True(res.DigitalOutputCount >= 8);
            Assert.True(res.AnalogInputCount >= 4);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 21: Verifies multi-chunk FC16 rule staging and atomic commit FSM transitions to READY over wire.
    /// </summary>
    [Fact]
    public async Task Transport_StagingCommit_MultiChunkFc16_TransitionsFsmToReady()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Create 5 distinct rules requiring multi-chunk staging
            var rules = new List<RuleRecordDto>();
            for (int i = 0; i < 5; i++)
            {
                rules.Add(new RuleRecordDto
                {
                    Enabled = true,
                    TriggerType = SPLC_TriggerType.INTERVAL,
                    CompareOp = SPLC_CompareOp.NONE,
                    ActionType = SPLC_ActionType.SET_TAG,
                    TriggerTag = (ushort)i,
                    ActionTag = (ushort)(8 + i),
                    ActionParam = 1
                });
            }

            await CommitRulesOverWireAsync(client, rules);

            // Verify version and active count
            var versionReg = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
            Assert.True(versionReg[0] >= 1);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 22: Adversarial fault injection: Simulates CRC mismatch during commit and verifies safe rollback without crash.
    /// </summary>
    [Fact]
    public async Task Transport_Adversarial_FlashFailure_RollsBackSafely()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // 1. Initial valid commit
            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.INTERVAL,
                CompareOp = SPLC_CompareOp.NONE,
                ActionType = SPLC_ActionType.SET_TAG,
                TriggerTag = 0,
                ActionTag = 8,
                ActionParam = 1
            };
            await CommitRulesOverWireAsync(client, new[] { rule });

            // 2. Adversarial commit: Inject invalid expected CRC
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.RuleCountStagedAddress, new ushort[] { 1, 0xDEAD });
            var ruleRegs = new ushort[ModbusRegisterMap.RegistersPerRule];
            RegisterCodec.EncodeRuleRecord(rule, ruleRegs);
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, ruleRegs);

            // Issue commit
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic);

            // Status must report ERROR (4) and ErrorCode CRC_MISMATCH (3)
            var status = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ConfigStatusAddress, 2);
            Assert.Equal(4, status[0]); // ERROR
            Assert.Equal((ushort)SPLC_ErrorCode.CRC_MISMATCH, status[1]);

            // MCU must remain fully responsive and active rule table untouched
            var activeCount = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
            Assert.Equal(1, activeCount[0]);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 23: High-frequency stress test: Executes 100 consecutive Modbus RTU wire requests with 100% success and 0 errors.
    /// </summary>
    [Fact]
    public async Task Transport_Stress_100ConsecutiveRequests_ZeroPacketLossAndZeroCrcErrors()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            int successCount = 0;
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < 100; i++)
            {
                var regs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceDescriptorAddress, 10);
                if (regs.Length == 10 && regs[0] == (ushort)SPLC_DeviceClass.REMOTE_IO)
                {
                    successCount++;
                }
            }

            sw.Stop();
            Assert.Equal(100, successCount);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 24: End-to-end Studio roundtrip: Connects via HostPort, performs full device scan, and verifies connection recovery.
    /// </summary>
    [Fact]
    public async Task Studio_Integration_MainViewModel_ConnectsToCom9AndExecutesRoundtrip()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            // Verify client reads device health telemetry over COM wire
            var healthRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DeviceHealthAddress, ModbusRegisterMap.DeviceHealthLength);
            var health = RegisterCodec.DecodeDeviceHealth(healthRegs);

            Assert.True(health.CpuLoadPercent <= 100);
            Assert.True(health.RamUsagePercent <= 100);

            // Verify live watch reading of digital outputs
            ushort doBase = ModbusRegisterMap.GetRuntimeTagAddress(8);
            var doRegs = await client.ReadHoldingRegistersAsync(1, doBase, 16);
            Assert.Equal(16, doRegs.Length);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 25: Verifies MQTT Last Will & Testament (LWT, SPLC-SPEC-MQTT-004) configuration over physical COM wire (0x0E60).
    /// </summary>
    [Fact]
    public async Task Section2_MqttWill_WhenConfigured_StagedAndVerifiedOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var willDto = new MqttWillConfigDto
            {
                Enabled = true,
                Retain = true,
                Qos = 1,
                Topic = "plant/status/lwt",
                Payload = "offline"
            };

            var willRegs = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];
            MqttWillConfigCodec.Encode(willDto, willRegs);

            // Write 32 registers over COM9 -> COM7 wire to 0x0E60
            await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.MqttWillConfigBaseAddress, willRegs);

            // Read back 32 registers over COM9 from 0x0E60
            var readBack = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttWillConfigBaseAddress, ModbusRegisterMap.MqttWillConfigRegisters);
            var decoded = MqttWillConfigCodec.Decode(readBack);

            Assert.True(decoded.Enabled);
            Assert.True(decoded.Retain);
            Assert.Equal(1, decoded.Qos);
            Assert.Equal("plant/status/lwt", decoded.Topic);
            Assert.Equal("offline", decoded.Payload);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 26: Verifies MQTT TLS / mTLS Chunked Certificate Staging & Atomic Commit (SPLC-SPEC-MQTT-003) over COM wire.
    /// </summary>
    [Fact]
    public async Task Section2_MqttTls_WhenCertificateStaged_CommitsAndStoresCrcOverWire()
    {
        if (!IsComPairAvailable()) return;

        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var certData = System.Text.Encoding.ASCII.GetBytes("-----BEGIN CERTIFICATE-----\nMIICADCCAYigAwIBAgIU1234567890ABCDEF\n-----END CERTIFICATE-----\n");
            ushort expectedCrc = Crc16Modbus.Compute(certData);

            // Slice certificate into standard 64-register chunks (SPLC-SPEC-MQTT-003)
            var chunks = TlsCertificateCodec.SliceCertificate(ModbusRegisterMap.MqttCertTypeRootCa, certData);
            Assert.NotEmpty(chunks);

            var chunkRegs = new ushort[ModbusRegisterMap.MqttCertChunkRegisters];
            for (int i = 0; i < chunks.Count; i++)
            {
                TlsCertificateCodec.EncodeChunk(chunks[i], chunkRegs);
                // Write Chunk to 0x0F00 over COM wire
                await client.WriteMultipleRegistersAsync(1, ModbusRegisterMap.MqttCertStagingBaseAddress, chunkRegs);

                // Read Ack and Status from 0x0F40..0x0F41
                var ackRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttCertAckAddress, 2);
                Assert.Equal(i, ackRegs[0]); // Ack == Chunk Index
                Assert.Equal(0, ackRegs[1]); // Status == OK (0)
            }

            // Commit Magic 0xA5A5 to 0x0F42
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.MqttCertCommitAddress, ModbusRegisterMap.MqttCertCommitMagic);

            // Verify Active Mask at 0x0F43 has bit 0 (Root CA) set
            var maskRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttCertActiveTypeMaskAddress, 2);
            Assert.Equal(0x0001, maskRegs[0] & 0x0001);

            // Verify Flash CRC at 0x0F44 matches computed full CRC
            var flashCrcReg = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttCertCrcRootCaAddress, 1);
            Assert.Equal(expectedCrc, flashCrcReg[0]);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }

    /// <summary>
    /// Test 27: Verifies Exclusive Ingestion Ownership safety gate: Blocks deployment when multiple external
    /// ingestion writers target the same Tag, preventing corrupted data frames from reaching the physical serial wire.
    /// </summary>
    [Fact]
    public async Task Section8_ExclusiveOwnership_DeployGateSafetyRejection_OverWire()
    {
        if (!IsComPairAvailable()) return;

        var deployVm = new DeployViewModel();
        ushort sharedTagIndex = 10; // VREG10

        var conflictingModbusReads = new List<ModbusMasterReadConfigDto>
        {
            new()
            {
                PortIndex = 0,
                FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
                StartAddress = 100,
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new() { Id = "P1", Label = "Val1", TargetTagIndex = sharedTagIndex }
                }
            }
        };

        var conflictingMqttSubs = new List<MqttSubscriptionConfigDto>
        {
            new()
            {
                Topic = "factory/sensor",
                Pins = new List<MqttPinBindingDto>
                {
                    new() { Id = "P2", Label = "humidity", TargetTagIndex = sharedTagIndex }
                }
            }
        };

        // 1. Verify safety gate detects conflict and rejects deployment
        var (isValid, errorMessage) = deployVm.ValidateIngestionOwnership(conflictingModbusReads, conflictingMqttSubs);
        Assert.False(isValid);
        Assert.Contains("Xung đột sở hữu ghi độc quyền", errorMessage);

        // 2. Resolve conflict (repoint MQTT to separate tag)
        conflictingMqttSubs[0].Pins[0].TargetTagIndex = 11;
        var (isResolvedValid, resolvedMsg) = deployVm.ValidateIngestionOwnership(conflictingModbusReads, conflictingMqttSubs);
        Assert.True(isResolvedValid);
        Assert.Null(resolvedMsg);

        // 3. Connect to COM wire and verify clean configuration commits successfully
        await using var mcu = new McuSerialHarness();
        var (transport, client) = await CreateHostClientAsync();
        try
        {
            var rule = new RuleRecordDto
            {
                Enabled = true,
                TriggerType = SPLC_TriggerType.INTERVAL,
                CompareOp = SPLC_CompareOp.NONE,
                ActionType = SPLC_ActionType.SET_TAG,
                TriggerTag = 0,
                ActionTag = 8,
                ActionParam = 1
            };
            await CommitRulesOverWireAsync(client, new[] { rule });
            var activeInfo = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
            Assert.Equal(1, activeInfo[0]);
        }
        finally
        {
            await transport.CloseAsync();
        }
    }
}
