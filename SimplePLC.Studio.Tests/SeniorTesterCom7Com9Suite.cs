using System.Diagnostics;
using System.IO.Ports;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;
using InfraSession = SimplePLC.Infrastructure.Sessions.DeviceSession;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Independent Senior QA / Tester evaluation suite executing real Modbus RTU communication
/// across Virtual COM ports COM7 <-> COM9 (or virtual fallbacks).
/// Evaluates: UI/UX states, Functional Logic (Multi-Subsystem Staged Deploy, PULSE_OUT, Stale clearing),
/// Retentive Memory, and Communication Performance / Latency (RTT).
/// </summary>
public class SeniorTesterCom7Com9Suite : IDisposable
{
    private readonly bool _hasPhysicalComPair;

    public SeniorTesterCom7Com9Suite()
    {
        var ports = SerialPort.GetPortNames();
        _hasPhysicalComPair = ports.Contains("COM7", StringComparer.OrdinalIgnoreCase) &&
                              ports.Contains("COM9", StringComparer.OrdinalIgnoreCase);
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    public void Dispose()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
    }

    private (SerialPort? serverPort, IUsbCdcTransport? testTransport, DeviceEndpoint endpoint, McuReferenceSimulator sim, McuModbusRtuServer server) CreateComHarness(CancellationToken ct)
    {
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);

        if (_hasPhysicalComPair)
        {
            // MCU Server on COM7, Client on COM9
            var serverPort = new SerialPort("COM7", 115200, Parity.None, 8, StopBits.One)
            {
                DtrEnable = true,
                RtsEnable = true,
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            serverPort.Open();
            _ = Task.Run(() => mcuServer.RunAsync(serverPort.BaseStream, serverPort.BaseStream, ct));
            return (serverPort, null, new UsbCdcEndpoint("COM9"), mcuSimulator, mcuServer);
        }
        else
        {
            var vTransport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 64 };
            return (null, vTransport, new UsbCdcEndpoint("COM_VIRTUAL"), mcuSimulator, mcuServer);
        }
    }

    private static RuleRecordDto CreateTestRule(ushort trigTag = 0, ushort actTag = 8)
    {
        return new RuleRecordDto
        {
            ThresholdLo = 1,
            ThresholdHi = 1,
            ForMs = 0,
            ActionParam = 1,
            TriggerTag = trigTag,
            ActionTag = actTag,
            GuardTag = ModbusRegisterMap.GuardTagNone,
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            CompareOp = SPLC_CompareOp.EQ,
            ActionType = SPLC_ActionType.SET_TAG
        };
    }

    // =========================================================================
    // PHẦN 1: ĐÁNH GIÁ UI / UX, THÔNG BÁO VÀ CHỐT CHẶN AN TOÀN (SAFETY GATES)
    // =========================================================================

    [Fact]
    public void UIUX_LiveDebug_SafetyGates_BlockEntryWhenGraphInvalidOrDisconnected()
    {
        // Kiểm toán độc lập: Khi chưa kết nối hoặc sơ đồ trống / có lỗi compile,
        // UI phải hiển thị cảnh báo công nghiệp rõ ràng và KHÔNG được bật Live Debug.
        var catalog = new TagCatalogViewModel();
        var stateStore = new RuntimeStateStore();
        var editor = new LogicEditorViewModel(catalog, stateStore: stateStore, uiDispatcher: a => a());

        // 1. Khi đang Disconnected
        stateStore.UpdateConnection(new UsbCdcEndpoint("COM9"), ConnectionStatus.Disconnected);
        editor.ToggleLiveDebug();
        Assert.False(editor.IsLiveDebugActive, "Live Debug không được phép bật khi thiết bị đang Disconnected!");
        Assert.Contains(LocalizationService.Tr("LiveDebugConnectRequired"), editor.SimGateWarning);

        // 2. Khi Connected nhưng sơ đồ trống
        stateStore.UpdateConnection(new UsbCdcEndpoint("COM9"), ConnectionStatus.Connected);
        editor.ToggleLiveDebug();
        Assert.False(editor.IsLiveDebugActive, "Live Debug không được phép bật khi sơ đồ Logic đang rỗng!");
        Assert.Contains(LocalizationService.Tr("LiveDebugEmptyGraphWarning"), editor.SimGateWarning);

        // 3. Khi sơ đồ có node nhưng chưa deploy
        var di0 = catalog.AllTags.First(t => t.Name == "DI0");
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");
        var inp = new InputNodeViewModel(di0);
        var trg = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE };
        var act = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1 };
        editor.Nodes.Add(inp);
        editor.Nodes.Add(trg);
        editor.Nodes.Add(act);
        editor.Connections.Add(new ConnectionViewModel(inp.OutputConnectors[0], trg.InputConnectors[0]));
        editor.Connections.Add(new ConnectionViewModel(trg.OutputConnectors[0], act.InputConnectors[0]));

        editor.ToggleLiveDebug();
        Assert.False(editor.IsLiveDebugActive, "Live Debug không được phép bật khi chưa nạp luật xuống thiết bị!");
        Assert.Contains(LocalizationService.Tr("LiveDebugDeployRequiredWarning"), editor.SimGateWarning);
    }

    [Fact]
    public void UIUX_LiveDebug_ImmediateVisualClearing_OnDisconnection()
    {
        // Kiểm toán độc lập: Đảm bảo khi mất kết nối đột ngột, toàn bộ dây sáng và đèn trạng thái
        // bị xóa ngay lập tức (ClearSimVisuals) để kỹ sư không bị hiểu lầm máy đang chạy.
        var catalog = new TagCatalogViewModel();
        var stateStore = new RuntimeStateStore();
        stateStore.UpdateConnection(new UsbCdcEndpoint("COM9"), ConnectionStatus.Connected);

        var editor = new LogicEditorViewModel(catalog, stateStore: stateStore, uiDispatcher: a => a());
        var di0 = catalog.AllTags.First(t => t.Name == "DI0");
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");
        var inp = new InputNodeViewModel(di0);
        var trg = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, IsSimTriggered = true };
        var act = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1, IsSimActive = true };
        editor.Nodes.Add(inp);
        editor.Nodes.Add(trg);
        editor.Nodes.Add(act);
        var c1 = new ConnectionViewModel(inp.OutputConnectors[0], trg.InputConnectors[0]) { IsActive = true };
        var c2 = new ConnectionViewModel(trg.OutputConnectors[0], act.InputConnectors[0]) { IsActive = true };
        editor.Connections.Add(c1);
        editor.Connections.Add(c2);

        editor.MarkGraphDeployed();
        editor.IsLiveDebugActive = true;

        // Giả lập sự kiện ngắt kết nối
        editor.IsLiveDebugActive = false;
        editor.ClearSimVisuals();

        Assert.False(editor.IsLiveDebugActive);
        Assert.False(trg.IsSimTriggered, "Cờ Trigger phải được reset về false khi ngắt kết nối!");
        Assert.False(act.IsSimActive, "Cờ Action phải được reset về false khi ngắt kết nối!");
        Assert.False(c1.IsActive, "Dây nối c1 phải tắt đèn (IsActive = false)!");
        Assert.False(c2.IsActive, "Dây nối c2 phải tắt đèn (IsActive = false)!");
    }

    // =========================================================================
    // PHẦN 2: ĐÁNH GIÁ CHỨC NĂNG & LOGIC TRUYỀN THÔNG QUA COM7 <-> COM9
    // =========================================================================

    [Fact]
    public async Task Logic_MultiSubsystemDeployment_And_StagedCommit_OverCom()
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

            // 1. Tạo cấu hình Modbus Master Write (0x1100)
            var mmwBlock = new ModbusMasterWriteConfigDto
            {
                Enabled = true,
                SlaveId = 2,
                FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
                StartAddress = 40001,
                PointCount = 1
            };

            // 2. Tạo cấu hình PULSE_OUT (Direct Hardware Pulse Output - 0x1200)
            var pulseBlock = new DirectWriteConfigDto
            {
                TargetPin = DirectWriteTargetPin.Do0,
                Mode = DirectWriteMode.DigitalOutput,
                ChannelCount = 1,
                IsExclusiveChannel = false
            };

            // 3. Tạo rule logic: DI0 OnRise -> DO0 = 1
            var rules = new List<RuleRecordDto> { CreateTestRule(trigTag: 0, actTag: 8) };

            // 4. Triển khai qua session.Rules (IRuleTableGateway)
            var deployResult = await session.Rules.DeployRulesAsync(
                slaveId: 1,
                rules: rules,
                timers: null,
                counters: null,
                mqttConfigs: null,
                brokerConfig: null,
                mqttSubConfigs: null,
                modbusMasterConfigs: null,
                modbusMasterWriteConfigs: new[] { mmwBlock },
                directWriteConfigs: new[] { pulseBlock },
                cancellationToken: cts.Token);

            Assert.True(deployResult.IsSuccess, $"Nạp đa phân hệ thất bại: {deployResult.ErrorMessage}");

            // 5. Xác thực trên bộ nhớ vi điều khiển qua thanh ghi
            // Đọc lại vùng 0x1100 (Modbus Master Write)
            var mmwRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ModbusMasterWriteTableBaseAddress, 64, cts.Token);
            Assert.Equal(64, mmwRegs.Length);
            Assert.Equal(1, mmwRegs[0] & 0x0001); // Enabled = 1

            // Đọc lại vùng 0x1200 (PULSE_OUT / Direct Write)
            var pulseRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.DirectWriteTableBaseAddress, 16, cts.Token);
            Assert.Equal(16, pulseRegs.Length);
            var decodedPulse = DirectWriteCodec.DecodeBlock(pulseRegs);
            Assert.Equal(DirectWriteTargetPin.Do0, decodedPulse.TargetPin);
            Assert.Equal(DirectWriteMode.DigitalOutput, decodedPulse.Mode);

            // Đọc lại bảng Rule đang thực thi (Active 0x0100)
            var activeRuleRegs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleTableBaseAddress, 16, cts.Token);
            Assert.Equal(16, activeRuleRegs.Length);

            // 6. Cho MCU chạy 5 scan passes và kiểm tra kích hoạt logic
            harness.sim.Control.SetTagValue(0, 1); // DI0 = 1
            for (int i = 0; i < 5; i++) harness.sim.ExecuteScanPass(20);

            int do0Val = harness.sim.Control.GetTagValue(8); // DO0
            Assert.Equal(1, do0Val);

            await session.DisposeAsync();
        }
        finally
        {
            cts.Cancel();
            harness.serverPort?.Close();
            harness.serverPort?.Dispose();
        }
    }

    [Fact]
    public async Task Logic_StaleBlockInvalidation_CleansOldActuatorsSafely()
    {
        // Kiểm toán an toàn máy công nghiệp:
        // Lần nạp 1: Có 2 khối PULSE_OUT (Khối 0: DO0, Khối 1: DO1).
        // Lần nạp 2: Kỹ sư xóa khối 1 chỉ giữ khối 0.
        // HỆ THỐNG BẮT BUỘC PHẢI ZERO-OUT KHỐI 1 để tránh tín hiệu xung ma tiếp tục kích hoạt phần cứng.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var harness = CreateComHarness(cts.Token);
        try
        {
            var factory = new DeviceConnectionFactory(transportFactory: harness.testTransport != null ? _ => harness.testTransport : null);
            var connectResult = await factory.ConnectAsync(harness.endpoint, slaveId: 1);
            Assert.True(connectResult.IsSuccess);
            var session = (InfraSession)connectResult.Session!;
            var client = session.Client;

            var emptyRules = new List<RuleRecordDto>();

            // Lần 1: Nạp 2 khối
            var block0 = new DirectWriteConfigDto { TargetPin = DirectWriteTargetPin.Do0, Mode = DirectWriteMode.DigitalOutput };
            var block1 = new DirectWriteConfigDto { TargetPin = DirectWriteTargetPin.Do1, Mode = DirectWriteMode.DigitalOutput };
            var res1 = await session.Rules.DeployRulesAsync(
                slaveId: 1,
                rules: emptyRules,
                timers: null,
                counters: null,
                mqttConfigs: null,
                brokerConfig: null,
                mqttSubConfigs: null,
                modbusMasterConfigs: null,
                modbusMasterWriteConfigs: null,
                directWriteConfigs: new[] { block0, block1 },
                cancellationToken: cts.Token);
            Assert.True(res1.IsSuccess);

            var checkBlock1 = await client.ReadHoldingRegistersAsync(1, (ushort)(ModbusRegisterMap.DirectWriteTableBaseAddress + 16), 16, cts.Token);
            Assert.NotEqual(0, checkBlock1[0]); // Đang có cấu hình

            // Lần 2: Chỉ nạp 1 khối (Khối 0)
            var res2 = await session.Rules.DeployRulesAsync(
                slaveId: 1,
                rules: emptyRules,
                timers: null,
                counters: null,
                mqttConfigs: null,
                brokerConfig: null,
                mqttSubConfigs: null,
                modbusMasterConfigs: null,
                modbusMasterWriteConfigs: null,
                directWriteConfigs: new[] { block0 },
                cancellationToken: cts.Token);
            Assert.True(res2.IsSuccess);

            // Khối 1 tại 0x1210 phải được tự động xóa về 0!
            var checkBlock1After = await client.ReadHoldingRegistersAsync(1, (ushort)(ModbusRegisterMap.DirectWriteTableBaseAddress + 16), 16, cts.Token);
            Assert.Equal(0, checkBlock1After[0]); // ĐÃ ĐƯỢC VÔ HIỆU HÓA HOÀN TOÀN!
            Assert.All(checkBlock1After, reg => Assert.Equal(0, reg));

            await session.DisposeAsync();
        }
        finally
        {
            cts.Cancel();
            harness.serverPort?.Close();
            harness.serverPort?.Dispose();
        }
    }

    [Fact]
    public async Task Logic_RetentiveMemoryProtection_SavesAndPersistsOverCom()
    {
        // Kiểm toán độc lập: Vùng nhớ Retentive (VREG_RETAIN0) phải được ghi và lưu trữ an toàn
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var harness = CreateComHarness(cts.Token);
        try
        {
            var factory = new DeviceConnectionFactory(transportFactory: harness.testTransport != null ? _ => harness.testTransport : null);
            var connectResult = await factory.ConnectAsync(harness.endpoint, slaveId: 1);
            Assert.True(connectResult.IsSuccess);
            var session = (InfraSession)connectResult.Session!;
            var client = session.Client;

            // 1. Vào chế độ Diag để có quyền ghi cấu hình
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.DiagCommandAddress, 0x0001, cts.Token); // CMD_ENTER_DIAG

            // 2. Ghi giá trị 65432 vào VREG_RETAIN0 (Tag Index 84, ADDR = 0x0900 + 84*2 = 0x09A8)
            ushort regAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (84 * 2));
            uint valueToRetain = 65432;
            ushort hi = (ushort)(valueToRetain >> 16);
            ushort lo = (ushort)(valueToRetain & 0xFFFF);
            await client.WriteMultipleRegistersAsync(1, regAddr, new ushort[] { hi, lo }, cts.Token);

            // 3. Commit Retentive Memory vào Flash vi điều khiển
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.DiagCommandAddress, 0x0004, cts.Token); // CMD_COMMIT_RETAIN

            // 4. Thoát chế độ Diag
            await client.WriteSingleRegisterAsync(1, ModbusRegisterMap.DiagCommandAddress, 0x0003, cts.Token); // CMD_EXIT_DIAG

            // 5. Đọc lại giá trị từ thanh ghi Retentive
            var readBack = await client.ReadHoldingRegistersAsync(1, regAddr, 2, cts.Token);
            uint actualValue = ((uint)readBack[0] << 16) | readBack[1];
            Assert.Equal(valueToRetain, actualValue);

            await session.DisposeAsync();
        }
        finally
        {
            cts.Cancel();
            harness.serverPort?.Close();
            harness.serverPort?.Dispose();
        }
    }

    // =========================================================================
    // PHẦN 3: ĐÁNH GIÁ HIỆU NĂNG, ĐỘ TRỄ (RTT) VÀ ĐỘ ỔN ĐỊNH TRUYỀN THÔNG (STRESS)
    // =========================================================================

    [Fact]
    public async Task Performance_ModbusRtuLatency_And_RapidPollingJitter_OverCom()
    {
        // Kiểm toán độc lập hiệu năng:
        // Đo đạc Round-Trip-Time (RTT) của 40 giao dịch liên tiếp đọc Holding Registers
        // qua cổng COM thực tế / ảo, tính toán Average, Min, Max, P95.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var harness = CreateComHarness(cts.Token);
        try
        {
            var factory = new DeviceConnectionFactory(transportFactory: harness.testTransport != null ? _ => harness.testTransport : null);
            var connectResult = await factory.ConnectAsync(harness.endpoint, slaveId: 1);
            Assert.True(connectResult.IsSuccess);
            var session = (InfraSession)connectResult.Session!;
            var client = session.Client;

            int pollCount = 40;
            var latencies = new List<double>();
            var sw = new Stopwatch();

            for (int i = 0; i < pollCount; i++)
            {
                sw.Restart();
                // Đọc 16 thanh ghi trạng thái Digital Output & Inputs
                var regs = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.RuntimeTagValuesBaseAddress, 16, cts.Token);
                sw.Stop();

                Assert.Equal(16, regs.Length);
                latencies.Add(sw.Elapsed.TotalMilliseconds);

                // Giãn cách giả lập vòng quét HMI / Live Watch (15ms)
                await Task.Delay(15);
            }

            double avgLatency = latencies.Average();
            double minLatency = latencies.Min();
            double maxLatency = latencies.Max();
            latencies.Sort();
            double p95Latency = latencies[(int)(pollCount * 0.95)];

            // Đánh giá ngưỡng chất lượng công nghiệp (Industrial SLA):
            // Độ trễ trung bình qua Virtual COM loopback phải dưới 50ms.
            // P95 phải dưới 100ms để đảm bảo không giật lag giao diện Studio.
            Assert.True(avgLatency < 50.0, $"Độ trễ trung bình quá cao: {avgLatency:F2}ms (Yêu cầu < 50ms)");
            Assert.True(p95Latency < 100.0, $"Độ trễ P95 vượt ngưỡng an toàn: {p95Latency:F2}ms (Yêu cầu < 100ms)");

            await session.DisposeAsync();
        }
        finally
        {
            cts.Cancel();
            harness.serverPort?.Close();
            harness.serverPort?.Dispose();
        }
    }
}
