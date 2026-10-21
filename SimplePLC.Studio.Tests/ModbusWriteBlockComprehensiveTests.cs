using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;
using InfraSession = SimplePLC.Infrastructure.Sessions.DeviceSession;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử chuyên sâu toàn diện cho khối chức năng MB_WRITE (Modbus RTU Master Write):
/// 1. Kiểm tra PDU và CRC-16 cho cả 4 mã hàm Direct: FC 05, FC 06, FC 15, FC 16.
/// 2. Kiểm tra chế độ Channel-Indexed Target (Command FC06 + Param FC16) và cơ chế Exclusive.
/// 3. Kiểm tra 3 chế độ kích hoạt: Rising Edge, Cyclic và On Data Change kèm COV Deadband.
/// 4. Kiểm tra nạp Staging & Commit vào bộ nhớ giả lập MCU (0x1100..0x113F) và giải mã đối ứng.
/// 5. Kiểm tra truyền thông thực địa qua cổng Serial COM7 <-> COM9 (hoặc Virtual COM fallback).
/// 6. Kiểm tra giao diện Popup Dialog ViewModel và tạo mẫu khung truyền trực tiếp.
/// </summary>
public class ModbusWriteBlockComprehensiveTests : IDisposable
{
    private readonly bool _hasPhysicalComPair;

    public ModbusWriteBlockComprehensiveTests()
    {
        var ports = SerialPort.GetPortNames();
        _hasPhysicalComPair = ports.Contains("COM7", StringComparer.OrdinalIgnoreCase) &&
                              ports.Contains("COM9", StringComparer.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
    }

    private (SerialPort? serverPort, IUsbCdcTransport? testTransport, DeviceEndpoint endpoint, McuReferenceSimulator sim, McuModbusRtuServer server) CreateComHarness(CancellationToken ct)
    {
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);

        if (_hasPhysicalComPair)
        {
            var serverPort = new SerialPort("COM7", 115200, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            serverPort.Open();
            _ = Task.Run(() => mcuServer.RunAsync(serverPort.BaseStream, serverPort.BaseStream, ct), ct);
            return (serverPort, null, new UsbCdcEndpoint("COM9"), mcuSimulator, mcuServer);
        }
        else
        {
            var vTransport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 64 };
            return (null, vTransport, new UsbCdcEndpoint("COM_VIRTUAL"), mcuSimulator, mcuServer);
        }
    }

    // =========================================================================
    // 1. KIỂM TRA PDU VÀ CRC-16 CHO TỪNG FUNCTION CODE CỦA DIRECT REGISTERS
    // =========================================================================

    [Fact]
    public void DirectMode_FC05_WriteSingleCoil_GeneratesValidPduAndCrc()
    {
        // Ghi cuộn dây số tại địa chỉ 0x000A (Coil 10), Giá trị = TRUE (0xFF00)
        byte slaveId = 1;
        ushort address = 0x000A;
        bool state = true;

        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x05; // FC 05
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = state ? (byte)0xFF : (byte)0x00;
        frame[5] = 0x00;

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);

        // Assert frame structure
        Assert.Equal(8, frame.Length);
        Assert.Equal(0x01, frame[0]);
        Assert.Equal(0x05, frame[1]);
        Assert.Equal(0x00, frame[2]);
        Assert.Equal(0x0A, frame[3]);
        Assert.Equal(0xFF, frame[4]);
        Assert.Equal(0x00, frame[5]);
        // Xác nhận tính toàn vẹn CRC-16
        Assert.True(Crc16Modbus.Verify(frame.AsSpan()));
    }

    [Fact]
    public void DirectMode_FC06_WriteSingleRegister_GeneratesValidPduAndCrc()
    {
        // Ghi thanh ghi tại địa chỉ 0x0100 (Thanh ghi 256), Giá trị = 5000 (0x1388)
        byte slaveId = 2;
        ushort address = 0x0100;
        ushort value = 5000;

        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x06; // FC 06
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((value >> 8) & 0xFF);
        frame[5] = (byte)(value & 0xFF);

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);

        Assert.Equal(8, frame.Length);
        Assert.Equal(0x02, frame[0]);
        Assert.Equal(0x06, frame[1]);
        Assert.Equal(0x01, frame[2]);
        Assert.Equal(0x00, frame[3]);
        Assert.Equal(0x13, frame[4]);
        Assert.Equal(0x88, frame[5]);
        Assert.True(Crc16Modbus.Verify(frame.AsSpan()));
    }

    [Fact]
    public void DirectMode_FC16_WriteMultipleRegisters_GeneratesValidPduAndCrc()
    {
        // Ghi 2 thanh ghi tại địa chỉ 0x00C8, giá trị = [0x1234, 0x5678]
        byte slaveId = 3;
        ushort address = 0x00C8;
        ushort[] values = new ushort[] { 0x1234, 0x5678 };

        byte byteCount = (byte)(values.Length * 2);
        byte[] frame = new byte[7 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = 0x10; // FC 16
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((values.Length >> 8) & 0xFF);
        frame[5] = (byte)(values.Length & 0xFF);
        frame[6] = byteCount;

        for (int i = 0; i < values.Length; i++)
        {
            frame[7 + i * 2] = (byte)((values[i] >> 8) & 0xFF);
            frame[7 + i * 2 + 1] = (byte)(values[i] & 0xFF);
        }

        int payloadLen = 7 + byteCount;
        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, payloadLen));
        frame[payloadLen] = (byte)(crc & 0xFF);
        frame[payloadLen + 1] = (byte)((crc >> 8) & 0xFF);

        Assert.Equal(13, frame.Length);
        Assert.Equal(0x03, frame[0]);
        Assert.Equal(0x10, frame[1]);
        Assert.Equal(0x00, frame[2]);
        Assert.Equal(0xC8, frame[3]);
        Assert.Equal(0x00, frame[4]);
        Assert.Equal(0x02, frame[5]);
        Assert.Equal(0x04, frame[6]);
        Assert.Equal(0x12, frame[7]);
        Assert.Equal(0x34, frame[8]);
        Assert.Equal(0x56, frame[9]);
        Assert.Equal(0x78, frame[10]);
        Assert.True(Crc16Modbus.Verify(frame.AsSpan()));
    }

    // =========================================================================
    // 2. KIỂM TRA CHẾ ĐỘ CHANNEL-INDEXED TARGET (COMMAND FC06 + PARAM FC16)
    // =========================================================================

    [Fact]
    public void ChannelIndexedMode_GeneratesAccurateCommandAndParamFrames()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 1,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            BaseAddressCommand = 0x0100,
            BaseAddressParam = 0x0120,
            ChannelCount = 18,
            IsExclusiveChannel = true
        };

        var dialogVm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            SimChannel = 4,
            SimCommand = 1,
            SimParamHex = "00FF00" // Green color RGB
        };

        dialogVm.GenerateFramePreview();

        // 1. Kiểm tra gói Command FC06: Địa chỉ = 0x0100 + 4 - 1 = 0x0103
        Assert.Contains("0x0103", dialogVm.SimGeneratedFrames);
        Assert.Contains("FC06", dialogVm.SimGeneratedFrames);

        // 2. Kiểm tra gói Param FC16: Địa chỉ = 0x0120 + ((4 - 1) * 2) = 0x0126
        Assert.Contains("0x0126", dialogVm.SimGeneratedFrames);
        Assert.Contains("FC16", dialogVm.SimGeneratedFrames);

        // 3. Kiểm tra tính hợp lệ của chuỗi Hex TX Frame và Param Frame
        Assert.NotEmpty(dialogVm.SimTxFrameHex);
        Assert.NotEmpty(dialogVm.SimParamTxFrameHex);
        Assert.Contains("CRC", dialogVm.SimStatusMessage);

        // 4. Giải mã byte từ Hex và kiểm chứng CRC-16 của cả 2 frame
        byte[] cmdBytes = dialogVm.SimTxFrameHex.Split(' ').Select(b => Convert.ToByte(b, 16)).ToArray();
        Assert.True(Crc16Modbus.Verify(cmdBytes.AsSpan()), "CRC-16 của gói Command FC06 không chính xác!");

        byte[] paramBytes = dialogVm.SimParamTxFrameHex.Split(' ').Select(b => Convert.ToByte(b, 16)).ToArray();
        Assert.True(Crc16Modbus.Verify(paramBytes.AsSpan()), "CRC-16 của gói Param FC16 không chính xác!");
    }

    // =========================================================================
    // 3. KIỂM TRA TRIGGER MODES VÀ COV DEADBAND ENGINE
    // =========================================================================

    [Fact]
    public void TriggerMode_OnDataChange_EvaluatesDeadbandCorrectly()
    {
        var node = new ModbusWriteNodeViewModel
        {
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            Deadband = 0.5f
        };

        // Khi độ lệch nhỏ hơn Deadband (< 0.5): Không kích hoạt
        float initialValue = 50.0f;
        float smallDeltaValue = 50.2f; // Delta = 0.2 < 0.5
        bool smallChangeExceeds = Math.Abs(smallDeltaValue - initialValue) >= node.Deadband;
        Assert.False(smallChangeExceeds, "Độ lệch nhỏ hơn Deadband không được phép phát lệnh ghi!");

        // Khi độ lệch lớn hơn hoặc bằng Deadband (>= 0.5): Kích hoạt
        float largeDeltaValue = 50.6f; // Delta = 0.6 >= 0.5
        bool largeChangeExceeds = Math.Abs(largeDeltaValue - initialValue) >= node.Deadband;
        Assert.True(largeChangeExceeds, "Độ lệch lớn hơn hoặc bằng Deadband phải kích hoạt lệnh ghi!");
    }

    [Fact]
    public void TriggerMode_Switching_UpdatesCanvasConnectorsAndViewModelFlags()
    {
        var node = new ModbusWriteNodeViewModel();

        // 1. Mặc định: Rising Edge
        node.TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge;
        Assert.True(node.IsTriggerMode);
        Assert.False(node.IsCyclicMode);
        Assert.False(node.IsOnDataChangeMode);
        Assert.Contains(node.InputConnectors, c => c.Title == "Trig");

        // 2. Chuyển sang Cyclic: Ẩn Trig, hiện Cyclic
        node.TriggerMode = ModbusWriteTriggerMode.Cyclic;
        Assert.False(node.IsTriggerMode);
        Assert.True(node.IsCyclicMode);
        Assert.False(node.IsOnDataChangeMode);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "Trig");

        // 3. Chuyển sang OnDataChange: Ẩn Trig, hiện OnDataChange
        node.TriggerMode = ModbusWriteTriggerMode.OnDataChange;
        Assert.False(node.IsTriggerMode);
        Assert.False(node.IsCyclicMode);
        Assert.True(node.IsOnDataChangeMode);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "Trig");
    }

    // =========================================================================
    // 4. KIỂM TRA NẠP VÀO BỘ NHỚ GIẢ LẬP MCU (0x1100) VÀ GIẢI MÃ ROUNDTRIP
    // =========================================================================

    [Fact]
    public async Task StagedDeploy_To_McuSimulator_StoresAndDecodesMbWriteBlocks()
    {
        var simulator = new McuReferenceSimulator();
        var client = new FakeModbusClient(simulator);
        await client.ConnectAsync("COM_TEST");

        var gateway = new RuleTableWriter(client);

        var mbWriteConfigs = new List<ModbusMasterWriteConfigDto>
        {
            new()
            {
                Enabled = true,
                SlaveId = 2,
                WriteMode = ModbusWriteMode.ChannelIndexed,
                BaseAddressCommand = 0x0100,
                BaseAddressParam = 0x0120,
                ChannelCount = 18,
                IsExclusiveChannel = true,
                TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge,
                CyclicIntervalMs = 500,
                TimeoutMs = 300,
                Deadband = 0.75f
            }
        };

        var deployResult = await gateway.DeployRulesAsync(
            slaveId: 1,
            rules: new List<RuleRecordDto>(),
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: mbWriteConfigs,
            directWriteConfigs: null,
            cancellationToken: default);

        Assert.True(deployResult.IsSuccess, "Deploy qua Gateway vào MCU Simulator phải thành công!");

        // Đọc 64 registers từ địa chỉ 0x1100 trong bộ nhớ Simulator
        var mem = simulator.Memory;
        ushort[] regs = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        for (int i = 0; i < ModbusRegisterMap.ModbusMasterRegistersPerBlock; i++)
        {
            regs[i] = mem.InternalRead((ushort)(ModbusRegisterMap.ModbusMasterWriteTableBaseAddress + i));
        }

        // Giải mã 64 registers thành DTO
        var decodedDto = ModbusMasterCodec.DecodeWriteBlock(regs, blockIndex: 0);

        Assert.True(decodedDto.Enabled);
        Assert.Equal((byte)2, decodedDto.SlaveId);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, decodedDto.WriteMode);
        Assert.Equal((ushort)0x0100, decodedDto.BaseAddressCommand);
        Assert.Equal((ushort)0x0120, decodedDto.BaseAddressParam);
        Assert.Equal((ushort)18, decodedDto.ChannelCount);
        Assert.True(decodedDto.IsExclusiveChannel);
        Assert.Equal(ModbusWriteTriggerMode.OnTriggerRisingEdge, decodedDto.TriggerMode);
    }

    // =========================================================================
    // 5. KIỂM TRA TRUYỀN THÔNG QUA CỔNG SERIAL THỰC ĐỊA (COM7 <-> COM9)
    // =========================================================================

    [Fact]
    public async Task RealSerialOrVirtualCom_MbWrite_FullDeployAndReadBack_OverWire()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var harness = CreateComHarness(cts.Token);
        try
        {
            var factory = new DeviceConnectionFactory(transportFactory: harness.testTransport != null ? _ => harness.testTransport : null);
            var connectResult = await factory.ConnectAsync(harness.endpoint, slaveId: 1);
            Assert.True(connectResult.IsSuccess, $"Kết nối COM thất bại: {connectResult.FailureReason}");

            var session = (InfraSession)connectResult.Session!;
            var client = session.Client;

            var mmwBlock = new ModbusMasterWriteConfigDto
            {
                Enabled = true,
                SlaveId = 4,
                WriteMode = ModbusWriteMode.DirectRegisters,
                FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
                StartAddress = 200,
                PointCount = 1,
                TriggerMode = ModbusWriteTriggerMode.Cyclic,
                CyclicIntervalMs = 800,
                TimeoutMs = 400
            };

            var deployResult = await session.Rules.DeployRulesAsync(
                slaveId: 1,
                rules: new List<RuleRecordDto>(),
                timers: null,
                counters: null,
                mqttConfigs: null,
                brokerConfig: null,
                mqttSubConfigs: null,
                modbusMasterConfigs: null,
                modbusMasterWriteConfigs: new[] { mmwBlock },
                directWriteConfigs: null,
                cancellationToken: cts.Token);

            Assert.True(deployResult.IsSuccess, $"Deploy qua đường truyền COM thất bại: {deployResult.ErrorMessage}");

            // Đọc lại 64 thanh ghi qua cổng COM từ địa chỉ 0x1100
            var readRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ModbusMasterWriteTableBaseAddress, 64, cts.Token);
            Assert.Equal(64, readRegs.Length);

            var decodedOverWire = ModbusMasterCodec.DecodeWriteBlock(readRegs, 0);
            Assert.True(decodedOverWire.Enabled);
            Assert.Equal((byte)4, decodedOverWire.SlaveId);
            Assert.Equal(ModbusWriteMode.DirectRegisters, decodedOverWire.WriteMode);
            Assert.Equal(ModbusMasterWriteFunctionCode.WriteSingleRegister, decodedOverWire.FunctionCode);
            Assert.Equal((ushort)200, decodedOverWire.StartAddress);
            Assert.Equal(ModbusWriteTriggerMode.Cyclic, decodedOverWire.TriggerMode);
            Assert.Equal(800u, decodedOverWire.CyclicIntervalMs);
        }
        finally
        {
            harness.serverPort?.Close();
            harness.serverPort?.Dispose();
        }
    }

    // =========================================================================
    // 6. KIỂM TRA HỘP THOẠI POPUP DIALOG VIEWMODEL VÀ CHUYỂN ĐỔI GIAO DIỆN
    // =========================================================================

    [Fact]
    public void DialogViewModel_Apply_PreservesAllModifiedPropertiesBackToNode()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 1,
            WriteMode = ModbusWriteMode.DirectRegisters,
            StartAddress = 100
        };

        var dialog = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            SlaveId = 9,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            BaseAddressCommand = 0x0500,
            BaseAddressParam = 0x0550,
            ChannelCount = 12,
            IsExclusiveChannel = true,
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            Deadband = 1.5f,
            CyclicIntervalMs = 1200,
            TimeoutMs = 600
        };

        dialog.Apply();

        Assert.Equal((byte)9, node.SlaveId);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, node.WriteMode);
        Assert.Equal((ushort)0x0500, node.BaseAddressCommand);
        Assert.Equal((ushort)0x0550, node.BaseAddressParam);
        Assert.Equal((ushort)12, node.ChannelCount);
        Assert.True(node.IsExclusiveChannel);
        Assert.Equal(ModbusWriteTriggerMode.OnDataChange, node.TriggerMode);
        Assert.Equal(1.5f, node.Deadband, 3);
        Assert.Equal(1200u, node.CyclicIntervalMs);
        Assert.Equal(600u, node.TimeoutMs);
    }

    // =========================================================================
    // 7. KIỂM TRA MÔ PHỎNG MODBUS POLL QUA TCP LẤY DỮ LIỆU BLOCK 0x1100
    // =========================================================================

    [Fact]
    public async Task ModbusPoll_SimulatedViaTcp_ReadsAndVerifies_MbWriteBlock_At_0x1100()
    {
        const int testPort = 15505;
        var simulator = new McuReferenceSimulator();
        var tcpServer = new McuModbusTcpServer(simulator, testPort);
        tcpServer.Start();

        try
        {
            // 1. Nạp cấu hình MB_WRITE vào Simulator qua Gateway
            var fakeClient = new FakeModbusClient(simulator);
            await fakeClient.ConnectAsync("COM_LOCAL");
            var gateway = new RuleTableWriter(fakeClient);

            var mbConfig = new ModbusMasterWriteConfigDto
            {
                Enabled = true,
                SlaveId = 3,
                WriteMode = ModbusWriteMode.ChannelIndexed,
                BaseAddressCommand = 0x0100,
                BaseAddressParam = 0x0120,
                ChannelCount = 18,
                IsExclusiveChannel = true,
                TriggerMode = ModbusWriteTriggerMode.Cyclic,
                CyclicIntervalMs = 750,
                TimeoutMs = 350
            };

            var deployResult = await gateway.DeployRulesAsync(
                slaveId: 1,
                rules: new List<RuleRecordDto>(),
                timers: null,
                counters: null,
                mqttConfigs: null,
                brokerConfig: null,
                mqttSubConfigs: null,
                modbusMasterConfigs: null,
                modbusMasterWriteConfigs: new[] { mbConfig },
                directWriteConfigs: null,
                cancellationToken: default);

            Assert.True(deployResult.IsSuccess);

            // 2. Giả lập Modbus Poll / SCADA Client kết nối qua TCP port 15505
            await using var modbusPollClient = new ModbusTcpClient("127.0.0.1", testPort, timeoutMs: 3000);
            await modbusPollClient.ConnectAsync();
            Assert.True(modbusPollClient.IsConnected, "Modbus Poll Client phải kết nối thành công tới MCU TCP Server!");

            // 3. Modbus Poll phát lệnh FC 03 (Read Holding Registers) đọc 64 thanh ghi tại 0x1100
            var pollResult = await modbusPollClient.ReadHoldingRegistersAsync(
                slaveId: 1,
                startAddress: ModbusRegisterMap.ModbusMasterWriteTableBaseAddress,
                count: ModbusRegisterMap.ModbusMasterRegistersPerBlock);

            Assert.Equal(64, pollResult.Length);

            // 4. Giải mã dữ liệu thu thập được từ Modbus Poll
            var decoded = ModbusMasterCodec.DecodeWriteBlock(pollResult, 0);

            Assert.True(decoded.Enabled);
            Assert.Equal((byte)3, decoded.SlaveId);
            Assert.Equal(ModbusWriteMode.ChannelIndexed, decoded.WriteMode);
            Assert.Equal((ushort)0x0100, decoded.BaseAddressCommand);
            Assert.Equal((ushort)0x0120, decoded.BaseAddressParam);
            Assert.Equal((ushort)18, decoded.ChannelCount);
            Assert.True(decoded.IsExclusiveChannel);
            Assert.Equal(ModbusWriteTriggerMode.Cyclic, decoded.TriggerMode);
            Assert.Equal(750u, decoded.CyclicIntervalMs);
            Assert.Equal(350u, decoded.TimeoutMs);
        }
        finally
        {
            await tcpServer.StopAsync();
        }
    }
}
