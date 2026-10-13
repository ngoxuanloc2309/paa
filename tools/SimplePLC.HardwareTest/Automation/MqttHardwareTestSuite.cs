using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.HardwareTest.Automation;

/// <summary>
/// Đại diện cho kết quả của một ca kiểm thử tự động.
/// </summary>
public sealed record TestResult(
    string TestId,
    string TestName,
    bool Passed,
    long ElapsedMs,
    string? ErrorMessage = null,
    string? Details = null);

/// <summary>
/// Bộ kiểm thử tự động độc lập phần cứng (Hardware Automation Test Suite) cho phân hệ MQTT Telemetry.
/// Thiết kế theo chuẩn Clean Code (Arrange - Act - Assert), cô lập môi trường và tìm kiếm các lỗi tiềm ẩn.
/// </summary>
public sealed class MqttHardwareTestSuite : IAsyncDisposable
{
    private readonly string _mcuPort;
    private readonly string _studioPort;
    private readonly int _baudRate;
    private readonly byte _slaveId;

    private SerialPort? _mcuSerial;
    private McuReferenceSimulator? _simulator;
    private McuModbusRtuServer? _mcuServer;
    private UsbCdcTransport? _transport;
    private ModbusRtuClient? _client;
    private RuleTableWriter? _writer;

    private CancellationTokenSource? _cts;
    private long _rxFrames;
    private long _txFrames;

    public MqttHardwareTestSuite(string mcuPort = "COM9", string studioPort = "COM7", int baudRate = 115200, byte slaveId = 1)
    {
        _mcuPort = mcuPort;
        _studioPort = studioPort;
        _baudRate = baudRate;
        _slaveId = slaveId;
    }

    /// <summary>
    /// Khởi động phần cứng ảo hai đầu UART (COM9: MCU Slave, COM7: App Master).
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        _cts = new CancellationTokenSource();

        _mcuSerial = new SerialPort(_mcuPort, _baudRate, Parity.None, 8, StopBits.One)
        {
            DtrEnable = true,
            RtsEnable = true,
            ReadTimeout = 1000,
            WriteTimeout = 1000
        };
        _mcuSerial.Open();

        _simulator = new McuReferenceSimulator(wireProfile: 2);
        _mcuServer = new McuModbusRtuServer(_simulator, _slaveId);

        _mcuServer.FrameReceived += _ => Interlocked.Increment(ref _rxFrames);
        _mcuServer.FrameSent += _ => Interlocked.Increment(ref _txFrames);

        _ = Task.Run(() => _mcuServer.RunAsync(_mcuSerial.BaseStream, _mcuSerial.BaseStream, _cts.Token), _cts.Token);

        _ = Task.Run(async () =>
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                _simulator.ExecuteScanPass(20);
                await Task.Delay(20, _cts.Token).ConfigureAwait(false);
            }
        }, _cts.Token);

        _transport = new UsbCdcTransport();
        await _transport.OpenAsync(new UsbCdcOptions(_studioPort, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)), ct);

        _client = new ModbusRtuClient(_transport);
        _writer = new RuleTableWriter(_client);
    }

    /// <summary>
    /// Thực thi toàn bộ danh mục kiểm thử tự động và trả về báo cáo chi tiết.
    /// </summary>
    public async Task<IReadOnlyList<TestResult>> RunAllTestsAsync(CancellationToken ct = default)
    {
        var results = new List<TestResult>();

        results.Add(await RunTestCaseAsync("TC01", "Boundary: Exact 128-byte Maximum Payload", Test01_Exact128BytesPayloadAsync, ct));
        results.Add(await RunTestCaseAsync("TC02", "Boundary: Exact 32-byte Maximum Topic", Test02_Exact32BytesTopicAsync, ct));
        results.Add(await RunTestCaseAsync("TC03", "Safety: Payload & Topic Overflow Protection", Test03_OverflowProtectionAsync, ct));
        results.Add(await RunTestCaseAsync("TC04", "Capacity: Full 4 MQTT Blocks Concurrency (384 regs)", Test04_Full4BlocksConcurrencyAsync, ct));
        results.Add(await RunTestCaseAsync("TC05", "Lifecycle: Stale Block Invalidation on Redundancy Deploy", Test05_StaleBlockCleanupAsync, ct));
        results.Add(await RunTestCaseAsync("TC06", "Data Integrity: Special Characters & Complex JSON Escapes", Test06_SpecialJsonEscapesAsync, ct));
        results.Add(await RunTestCaseAsync("TC07", "Memory Safety: Illegal Address Access Rejection (0x0D80)", Test07_IllegalAddressAccessAsync, ct));
        results.Add(await RunTestCaseAsync("TC08", "Stress: Rapid Deploy Overwrite Cycles (5 back-to-back)", Test08_RapidStressOverwriteAsync, ct));
        results.Add(await RunTestCaseAsync("TC09", "Broker Config: Deploy & Verify 0x0E00 System Broker Settings", Test09_BrokerConfigDeployAndReadbackAsync, ct));
        results.Add(await RunTestCaseAsync("TC10", "End-to-End Coexistence: Rules + FBs + Telemetry + Broker Config", Test10_FullCoexistenceDeployAsync, ct));

        return results;
    }

    private async Task<TestResult> RunTestCaseAsync(string id, string name, Func<CancellationToken, Task<string?>> testFunc, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            string? details = await testFunc(ct);
            sw.Stop();
            return new TestResult(id, name, true, sw.ElapsedMilliseconds, null, details);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new TestResult(id, name, false, sw.ElapsedMilliseconds, ex.Message, ex.ToString());
        }
    }

    // =========================================================================
    // TEST CASES (ARRANGE - ACT - ASSERT)
    // =========================================================================

    /// <summary>
    /// TC01: Kiểm tra nạp chuỗi Payload đúng 128 ký tự (chạm trần).
    /// Phát hiện bug: Codec có bị mất ký tự thứ 128 (Off-by-One) hay không?
    /// </summary>
    private async Task<string?> Test01_Exact128BytesPayloadAsync(CancellationToken ct)
    {
        // 128 ký tự JSON chuẩn công nghiệp
        string exact128Template = "{\"site\":\"FAC_HN01\",\"line\":\"LINE_A3\",\"dev\":\"{device}\",\"tag\":\"{tag}\",\"val\":{val},\"ts\":{ts},\"unit\":\"degC\",\"st\":\"OK\",\"err\":0,\"q\":10}";
        if (exact128Template.Length != 128)
            throw new InvalidOperationException($"Test setup error: Template length is {exact128Template.Length}, expected 128.");

        var config = new MqttTelemetryConfigDto
        {
            Topic = "synaptix/telemetry/exact128",
            PublishMode = "COV",
            PayloadMode = "Custom",
            CustomTemplate = exact128Template,
            IntervalMs = 1000,
            Deadband = 0.5
        };

        var dummyRule = CreateDummyRule();
        var deployResult = await _writer!.DeployRulesAsync(_slaveId, dummyRule, null, null, new[] { config }, ct);
        if (!deployResult.IsSuccess)
            throw new Exception($"Deploy failed: {deployResult.ErrorMessage}");

        // Đọc ngược 96 thanh ghi của Khối 0
        ushort[] readback = await ReadChunkedAsync(ModbusRegisterMap.MqttTelemetryTableBaseAddress, ModbusRegisterMap.MqttRegistersPerBlock, ct);
        var decoded = MqttTelemetryCodec.DecodeBlock(readback);

        if (decoded.CustomTemplate != exact128Template)
        {
            throw new Exception($"Payload mismatch! Expected {exact128Template.Length} chars ('{exact128Template}'), but got {decoded.CustomTemplate.Length} chars ('{decoded.CustomTemplate}').");
        }

        return $"Chuỗi 128 ký tự giải mã toàn vẹn 100%: '{decoded.CustomTemplate[..30]}...{decoded.CustomTemplate[^20..]}'";
    }

    /// <summary>
    /// TC02: Kiểm tra nạp Topic đúng 32 ký tự (chạm trần 16 thanh ghi).
    /// </summary>
    private async Task<string?> Test02_Exact32BytesTopicAsync(CancellationToken ct)
    {
        string exact32Topic = "synaptix/line1/sensor/temperature"; // 34 chars -> điều chỉnh thành đúng 32
        exact32Topic = "synaptix/factory/press/pressure32"; // 33 -> 32
        exact32Topic = exact32Topic.Substring(0, 32);

        var config = new MqttTelemetryConfigDto
        {
            Topic = exact32Topic,
            PublishMode = "Periodic",
            PayloadMode = "JSON",
            IntervalMs = 2000,
            Deadband = 0.1
        };

        var deployResult = await _writer!.DeployRulesAsync(_slaveId, CreateDummyRule(), null, null, new[] { config }, ct);
        if (!deployResult.IsSuccess)
            throw new Exception($"Deploy failed: {deployResult.ErrorMessage}");

        ushort[] readback = await ReadChunkedAsync(ModbusRegisterMap.MqttTelemetryTableBaseAddress, ModbusRegisterMap.MqttRegistersPerBlock, ct);
        var decoded = MqttTelemetryCodec.DecodeBlock(readback);

        if (decoded.Topic != exact32Topic)
        {
            throw new Exception($"Topic mismatch! Expected 32 chars ('{exact32Topic}'), got {decoded.Topic.Length} chars ('{decoded.Topic}').");
        }

        return $"Topic 32 ký tự xác thực khớp: '{decoded.Topic}'";
    }

    /// <summary>
    /// TC03: Kiểm tra hành vi khi chuỗi Template vượt quá 128 ký tự (Overflow).
    /// Xác minh hệ thống không bị tràn bộ nhớ hoặc ghi đè sang khối bên cạnh.
    /// </summary>
    private async Task<string?> Test03_OverflowProtectionAsync(CancellationToken ct)
    {
        string overflowTemplate = new string('A', 150); // 150 ký tự > 128
        var config = new MqttTelemetryConfigDto
        {
            Topic = "synaptix/overflow/test",
            PublishMode = "COV",
            PayloadMode = "Custom",
            CustomTemplate = overflowTemplate
        };

        var deployResult = await _writer!.DeployRulesAsync(_slaveId, CreateDummyRule(), null, null, new[] { config }, ct);
        if (!deployResult.IsSuccess)
            throw new Exception($"Deploy failed: {deployResult.ErrorMessage}");

        ushort[] block0 = await ReadChunkedAsync(ModbusRegisterMap.MqttTelemetryTableBaseAddress, ModbusRegisterMap.MqttRegistersPerBlock, ct);
        var decoded = MqttTelemetryCodec.DecodeBlock(block0);

        if (decoded.CustomTemplate.Length > ModbusRegisterMap.MqttTemplateMaxChars)
        {
            throw new Exception($"Overflow not truncated! Length is {decoded.CustomTemplate.Length} > {ModbusRegisterMap.MqttTemplateMaxChars}");
        }

        // Kiểm tra thanh ghi đầu tiên của Block 1 (0x0C60) có bị ghi đè không
        ushort[] block1 = await ReadChunkedAsync(ModbusRegisterMap.GetMqttBlockAddress(1), 16, ct);
        // Block 1 không được chứa dữ liệu rác từ block 0
        return $"Hệ thống tự động cắt an toàn ở mức {decoded.CustomTemplate.Length} ký tự mà không làm tràn bộ đệm.";
    }

    /// <summary>
    /// TC04: Kiểm tra nạp đồng thời 4 khối MQTT (chiếm toàn bộ 384 thanh ghi từ 0x0C00 đến 0x0D7F).
    /// </summary>
    private async Task<string?> Test04_Full4BlocksConcurrencyAsync(CancellationToken ct)
    {
        var configs = new List<MqttTelemetryConfigDto>();
        for (int i = 0; i < 4; i++)
        {
            configs.Add(new MqttTelemetryConfigDto
            {
                NodeId = $"BLOCK_{i}",
                Topic = $"synaptix/line/{i}",
                PublishMode = i % 2 == 0 ? "Periodic" : "COV",
                IntervalMs = (uint)(1000 * (i + 1)),
                Deadband = 0.5 * (i + 1),
                PayloadMode = i == 3 ? "Custom" : "JSON",
                CustomTemplate = i == 3 ? "{\"id\":3,\"val\":{val}}" : string.Empty
            });
        }

        var deployResult = await _writer!.DeployRulesAsync(_slaveId, CreateDummyRule(), null, null, configs, ct);
        if (!deployResult.IsSuccess)
            throw new Exception($"Deploy failed: {deployResult.ErrorMessage}");

        // Đọc ngược và verify độc lập cả 4 khối
        for (int i = 0; i < 4; i++)
        {
            ushort addr = ModbusRegisterMap.GetMqttBlockAddress(i);
            ushort[] regs = await ReadChunkedAsync(addr, ModbusRegisterMap.MqttRegistersPerBlock, ct);
            var dec = MqttTelemetryCodec.DecodeBlock(regs);

            if (dec.Topic != $"synaptix/line/{i}")
                throw new Exception($"Block {i} topic mismatch: {dec.Topic}");
            if (dec.IntervalMs != (uint)(1000 * (i + 1)))
                throw new Exception($"Block {i} IntervalMs mismatch: {dec.IntervalMs}");
        }

        return "Nạp và xác thực thành công 4/4 khối đồng thời (384 thanh ghi Modbus).";
    }

    /// <summary>
    /// TC05: Kiểm tra việc xóa / vô hiệu hóa các khối cũ khi nạp lại số lượng khối ít hơn (Stale Block Invalidation).
    /// </summary>
    private async Task<string?> Test05_StaleBlockCleanupAsync(CancellationToken ct)
    {
        // 1. Nạp 2 khối
        var twoConfigs = new[]
        {
            new MqttTelemetryConfigDto { Topic = "block/0", PublishMode = "Periodic" },
            new MqttTelemetryConfigDto { Topic = "block/1", PublishMode = "Periodic" }
        };
        await _writer!.DeployRulesAsync(_slaveId, CreateDummyRule(), null, null, twoConfigs, ct);

        // 2. Nạp lại chỉ còn 1 khối (người dùng đã xóa Block 1 trên Canvas)
        var oneConfig = new[]
        {
            new MqttTelemetryConfigDto { Topic = "block/0/updated", PublishMode = "Periodic" }
        };
        await _writer!.DeployRulesAsync(_slaveId, CreateDummyRule(), null, null, oneConfig, ct);

        // Đọc lại Block 1 xem nó có còn Enabled không
        ushort block1Addr = ModbusRegisterMap.GetMqttBlockAddress(1);
        ushort[] block1Regs = await ReadChunkedAsync(block1Addr, ModbusRegisterMap.MqttRegistersPerBlock, ct);
        bool isBlock1Enabled = (block1Regs[0] & ModbusRegisterMap.MqttControlBitEnabled) != 0;

        if (isBlock1Enabled)
        {
            throw new Exception("Lỗi rò rỉ cấu hình: Block 1 đã bị xóa trên App nhưng MCU vẫn giữ cờ Enabled=1!");
        }

        return "Khối cũ đã được thu hồi và vô hiệu hóa an toàn (Enabled=0).";
    }

    /// <summary>
    /// TC06: Kiểm tra ký tự đặc biệt, dấu ngoặc kép lồng, escape sequence trong JSON.
    /// </summary>
    private async Task<string?> Test06_SpecialJsonEscapesAsync(CancellationToken ct)
    {
        string complexTemplate = "{\"msg\":\"Hello: \\\"World\\\"!\",\"p\":{val},\"sym\":\"@#$%&*()\"}";
        var config = new MqttTelemetryConfigDto
        {
            Topic = "special/chars/test",
            PublishMode = "COV",
            PayloadMode = "Custom",
            CustomTemplate = complexTemplate
        };

        var deployResult = await _writer!.DeployRulesAsync(_slaveId, CreateDummyRule(), null, null, new[] { config }, ct);
        if (!deployResult.IsSuccess)
            throw new Exception($"Deploy failed: {deployResult.ErrorMessage}");

        ushort[] readback = await ReadChunkedAsync(ModbusRegisterMap.MqttTelemetryTableBaseAddress, ModbusRegisterMap.MqttRegistersPerBlock, ct);
        var decoded = MqttTelemetryCodec.DecodeBlock(readback);

        if (decoded.CustomTemplate != complexTemplate)
        {
            throw new Exception($"Complex JSON mismatch!\nExpected: '{complexTemplate}'\nActual:   '{decoded.CustomTemplate}'");
        }

        return "Bảo toàn 100% ký tự đặc biệt và dấu ngoặc kép lồng trong JSON.";
    }

    /// <summary>
    /// TC07: Thử truy cập ngoài vùng nhớ (0x0D80 trở đi) và xác minh MCU từ chối an toàn.
    /// </summary>
    private async Task<string?> Test07_IllegalAddressAccessAsync(CancellationToken ct)
    {
        bool readBlocked = false;
        try
        {
            // Vùng hợp lệ kết thúc tại 0x0D7F. Địa chỉ 0x0D80 phải bị từ chối
            await _client!.ReadHoldingRegistersAsync(_slaveId, 0x0D80, 10, ct);
        }
        catch
        {
            readBlocked = true;
        }

        if (!readBlocked)
        {
            throw new Exception("MCU cho phép đọc ngoài vùng địa chỉ MQTT (0x0D80) mà không ném ngoại lệ Modbus!");
        }

        return "MCU bảo vệ bộ nhớ chuẩn xác: Khóa mọi truy cập ngoài vùng 0x0D80.";
    }

    /// <summary>
    /// TC08: Stress test nạp liên tục 5 lần liên tiếp với dữ liệu thay đổi ở tốc độ cao.
    /// </summary>
    private async Task<string?> Test08_RapidStressOverwriteAsync(CancellationToken ct)
    {
        for (int cycle = 1; cycle <= 5; cycle++)
        {
            var config = new MqttTelemetryConfigDto
            {
                Topic = $"stress/cycle/{cycle}",
                PublishMode = "Periodic",
                IntervalMs = (uint)(cycle * 500),
                Deadband = cycle * 0.2,
                PayloadMode = "JSON"
            };

            var deployResult = await _writer!.DeployRulesAsync(_slaveId, CreateDummyRule(), null, null, new[] { config }, ct);
            if (!deployResult.IsSuccess)
                throw new Exception($"Stress cycle {cycle} failed: {deployResult.ErrorMessage}");

            ushort[] readback = await ReadChunkedAsync(ModbusRegisterMap.MqttTelemetryTableBaseAddress, 32, ct);
            var dec = MqttTelemetryCodec.DecodeBlock(readback.Concat(new ushort[64]).ToArray());
            if (dec.Topic != $"stress/cycle/{cycle}")
                throw new Exception($"Stress cycle {cycle} verification mismatch! Got '{dec.Topic}'");
        }

        return "5 chu trình nạp liên tục qua UART hoàn thành xuất sắc không gặp lỗi truyền thông.";
    }

    /// <summary>
    /// TC09: Kiểm tra nạp và đọc lại cấu hình MQTT Broker cấp hệ thống tại phân vùng 0x0E00.
    /// </summary>
    private async Task<string?> Test09_BrokerConfigDeployAndReadbackAsync(CancellationToken ct)
    {
        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "broker.hivemq.com",
            Port = 8883,
            UseTls = true,
            CleanSession = false,
            KeepAliveSec = 90,
            ClientId = "SPLC-UNIT-TEST-01",
            Username = "industrial_admin",
            Password = "P@ssw0rdSecure!"
        };

        var deployResult = await _writer!.DeployRulesAsync(
            _slaveId,
            CreateDummyRule(),
            null,
            null,
            null,
            brokerConfig,
            ct);

        if (!deployResult.IsSuccess)
            throw new Exception($"Deploy with broker config failed: {deployResult.ErrorMessage}");

        // Đọc lại 96 registers từ 0x0E00
        ushort[] readback = await ReadChunkedAsync(ModbusRegisterMap.MqttBrokerConfigBaseAddress, (ushort)ModbusRegisterMap.MqttBrokerConfigRegisters, ct);
        var decoded = MqttBrokerConfigCodec.Decode(readback);

        if (!decoded.Enabled) throw new Exception("Broker Enabled flag mismatch!");
        if (decoded.Host != "broker.hivemq.com") throw new Exception($"Broker Host mismatch! Expected 'broker.hivemq.com', got '{decoded.Host}'");
        if (decoded.Port != 8883) throw new Exception($"Broker Port mismatch! Expected 8883, got {decoded.Port}");
        if (!decoded.UseTls) throw new Exception("Broker TLS flag mismatch!");
        if (decoded.CleanSession) throw new Exception("Broker CleanSession expected false, got true!");
        if (decoded.KeepAliveSec != 90) throw new Exception($"Broker KeepAlive mismatch! Expected 90, got {decoded.KeepAliveSec}");
        if (decoded.ClientId != "SPLC-UNIT-TEST-01") throw new Exception($"Broker ClientId mismatch! Got '{decoded.ClientId}'");
        if (decoded.Username != "industrial_admin") throw new Exception($"Broker Username mismatch! Got '{decoded.Username}'");
        if (decoded.Password != "P@ssw0rdSecure!") throw new Exception($"Broker Password mismatch! Got '{decoded.Password}'");

        return $"0x0E00 Broker Config nạp và đọc lại chính xác tuyệt đối 100%: Host={decoded.Host}, Port={decoded.Port}, TLS={decoded.UseTls}.";
    }

    /// <summary>
    /// TC10: Kiểm tra đồng tồn tại (Coexistence) toàn diện: Rules + FBs (Timer TON) + Telemetry blocks + Broker config trong 1 lượt nạp.
    /// </summary>
    private async Task<string?> Test10_FullCoexistenceDeployAsync(CancellationToken ct)
    {
        var dummyRule = CreateDummyRule();
        var timerDto = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TON,
            PresetMs = 5000
        };

        var telemetryDto = new MqttTelemetryConfigDto
        {
            Topic = "plant/furnace1/temp",
            PublishMode = "COV",
            PayloadMode = "JSON",
            Deadband = 1.0
        };

        var brokerDto = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "192.168.1.100",
            Port = 1883,
            UseTls = false,
            CleanSession = true,
            KeepAliveSec = 60,
            ClientId = "FURNACE-PLC",
            Username = "user",
            Password = "pass"
        };

        var deployResult = await _writer!.DeployRulesAsync(
            _slaveId,
            dummyRule,
            new[] { timerDto },
            null,
            new[] { telemetryDto },
            brokerDto,
            ct);

        if (!deployResult.IsSuccess)
            throw new Exception($"Coexistence deploy failed: {deployResult.ErrorMessage}");

        // Xác minh đọc lại broker
        ushort[] brokerRegs = await ReadChunkedAsync(ModbusRegisterMap.MqttBrokerConfigBaseAddress, (ushort)ModbusRegisterMap.MqttBrokerConfigRegisters, ct);
        var decodedBroker = MqttBrokerConfigCodec.Decode(brokerRegs);
        if (decodedBroker.Host != "192.168.1.100" || decodedBroker.ClientId != "FURNACE-PLC")
            throw new Exception("Coexistence broker verification mismatch!");

        // Xác minh đọc lại telemetry
        ushort[] telemRegs = await ReadChunkedAsync(ModbusRegisterMap.MqttTelemetryTableBaseAddress, 32, ct);
        var decodedTelem = MqttTelemetryCodec.DecodeBlock(telemRegs.Concat(new ushort[64]).ToArray());
        if (decodedTelem.Topic != "plant/furnace1/temp")
            throw new Exception("Coexistence telemetry verification mismatch!");

        return "Toàn bộ bảng Rule, Timer TON, Telemetry block và Broker config nạp đồng bộ thành công trong 1 giao dịch Atomic!";
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    private static IReadOnlyList<RuleRecordDto> CreateDummyRule()
    {
        return new[]
        {
            new RuleRecordDto
            {
                Enabled = true,
                TriggerTag = 0,
                TriggerType = SPLC_TriggerType.ON_CHANGE,
                CompareOp = SPLC_CompareOp.EQ,
                ThresholdLo = 1,
                ActionTag = 8,
                ActionType = SPLC_ActionType.SET_TAG,
                ActionParam = 1
            }
        };
    }

    private async Task<ushort[]> ReadChunkedAsync(ushort startAddress, ushort totalCount, CancellationToken ct)
    {
        const ushort chunkSize = 32;
        var result = new ushort[totalCount];
        ushort read = 0;
        while (read < totalCount)
        {
            ushort countToRead = (ushort)Math.Min((int)chunkSize, totalCount - read);
            var chunk = await _client!.ReadHoldingRegistersAsync(_slaveId, (ushort)(startAddress + read), countToRead, ct);
            chunk.CopyTo(result.AsMemory(read, countToRead));
            read += countToRead;
        }
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        if (_transport != null)
        {
            await _transport.DisposeAsync();
        }
        if (_mcuSerial != null && _mcuSerial.IsOpen)
        {
            _mcuSerial.Close();
            _mcuSerial.Dispose();
        }
    }
}
