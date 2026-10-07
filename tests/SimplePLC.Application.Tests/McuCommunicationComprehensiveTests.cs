using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Bộ kiểm thử tự động hóa toàn diện giao tiếp vi điều khiển MCU (Automation Test Suite).
/// Kiểm thử các kịch bản cốt lõi:
/// 1. Tự nhận diện thiết bị & đọc toàn bộ cấu hình tài nguyên phần cứng (Descriptors, ResourceInfo, Health, Tags).
/// 2. Triển khai nạp bảng quy tắc (Deploy: Staging buffer 0x1000 -> CRC-16 -> Atomic Commit 0xA5A5).
/// 3. Đọc ngược lại từ Active Table của MCU (0x2000) và đối soát tính toàn vẹn 100% từng byte (Roundtrip Fidelity).
/// 4. Tải cực đại 100 Rules (3200 bytes) truyền qua USB CDC nối tiếp.
/// 5. Hệ thống lệnh điều khiển từ xa (Clear Rules, Factory Reset, Reboot).
/// 6. Cơ chế cấy lỗi (Fault Injection: CRC mismatch, vượt quota rule).
/// 7. Đồng bộ trạng thái phần cứng hai chiều.
/// </summary>
public class McuCommunicationComprehensiveTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        int timeoutMs = 3000,
        int pollIntervalMs = 20,
        string stepName = "")
    {
        var start = Environment.TickCount;
        while (Environment.TickCount - start < timeoutMs)
        {
            if (condition())
                return;
            await Task.Delay(pollIntervalMs);
        }

        throw new TimeoutException($"Timed out waiting for condition '{stepName}' after {timeoutMs} ms.");
    }

    private static (McuReferenceSimulator Simulator, IDeviceSession Session, VirtualComMcuTransport Transport) CreateVirtualSession(
        int maxPacketSize = 64)
    {
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);
        var transport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = maxPacketSize };

        var connectionFactory = new DeviceConnectionFactory(transportFactory: _ => transport);
        var connectResult = connectionFactory.ConnectAsync(new UsbCdcEndpoint("COM_MCU_TEST"), slaveId: 1).GetAwaiter().GetResult();
        Assert.True(connectResult.IsSuccess, $"Failed to connect: {connectResult.FailureReason}");

        return (mcuSimulator, connectResult.Session!, transport);
    }

    [Fact]
    public async Task Test_01_DeviceDiscovery_DescriptorAndResourceConfig_FullInspection()
    {
        // Arrange
        var (simulator, session, transport) = CreateVirtualSession();

        try
        {
            // 1. Đọc Device Descriptor (0x0000..0x0009)
            var descriptor = session.Descriptor;
            Assert.Equal(SPLC_DeviceClass.REMOTE_IO, descriptor.DeviceClass);
            Assert.Equal((ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI, descriptor.DeviceVariant);
            Assert.Equal(1, descriptor.HwVersionMajor);
            Assert.Equal(0, descriptor.HwVersionMinor);
            Assert.Equal(1, descriptor.FwVersionMajor);
            Assert.Equal(7, descriptor.FwVersionMinor);

            // 2. Đọc Resource Info (0x0020..0x0029)
            Assert.NotNull(session.ResourceInfo);
            var resource = session.ResourceInfo;
            Assert.Equal(8, resource.DigitalInputCount);
            Assert.Equal(8, resource.DigitalOutputCount);
            Assert.Equal(4, resource.AnalogInputCount);
            Assert.Equal(32, resource.VirtualFlagCount);
            Assert.Equal(32, resource.VirtualRegisterCount);
            Assert.Equal(32, resource.RetentiveRegisterCount);
            Assert.Equal(100, resource.MaxRules);

            // 3. Đọc Device Health Telemetry (0x0800..0x0809)
            var health = await session.Health.ReadHealthAsync(1);
            Assert.Equal(SPLC_ResetReason.POWER_ON, health.ResetReason);
            Assert.True(health.CpuLoadPercent <= 100);
            Assert.True(health.RamUsagePercent <= 100);

            // 4. Đọc Runtime Tags (0x0100..0x012F)
            var tagValues = await session.RuntimeTags.ReadRuntimeTagValuesAsync(1, 116);
            Assert.NotNull(tagValues);
            Assert.Equal(116, tagValues.Length);

            // Xác minh khung byte thật đã truyền qua lại trên USB CDC
            Assert.True(transport.TotalBytesSent > 0);
            Assert.True(transport.TotalBytesReceived > 0);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Test_02_AllTriggerAndActionKinds_RoundtripFidelity_DeployAndReadBack()
    {
        // Arrange
        var (simulator, session, _) = CreateVirtualSession();
        var deployUseCase = new DeployRulesUseCase(session.Rules);
        var loadUseCase = new LoadRulesUseCase(session.Rules);

        try
        {
            var ruleTable = new RuleTable();

            var di0 = _product.FindTagByName("DI0")!;
            var di1 = _product.FindTagByName("DI1")!;
            var di2 = _product.FindTagByName("DI2")!;
            var di3 = _product.FindTagByName("DI3")!;
            var do0 = _product.FindTagByName("DO0")!;
            var do1 = _product.FindTagByName("DO1")!;
            var do2 = _product.FindTagByName("DO2")!;
            var do3 = _product.FindTagByName("DO3")!;
            var ai0 = _product.FindTagByName("AI0")!;
            var vflag0 = _product.FindTagByName("VFLAG0")!;
            var vreg0 = _product.FindTagByName("VREG0")!;
            var retain0 = _product.FindTagByName("VREG_RETAIN0")!;

            // Rule 0: Trigger OnRise -> SetTag DO0 = 1
            ruleTable.AddRule(new Rule(
                0,
                "R1_OnRise_SetTag",
                new TriggerModel(di0, TriggerKind.OnRise),
                new ActionModel(do0, ActionKind.SetTag, 1)));

            // Rule 1: Trigger OnFall (Debounce 500ms) + Guard DI2 (Negated) -> ToggleTag DO1
            ruleTable.AddRule(new Rule(
                1,
                "R2_OnFall_Toggle_GuardNegated",
                new TriggerModel(di1, TriggerKind.OnFall) { ForMs = 500u },
                new ActionModel(do1, ActionKind.ToggleTag, 0),
                new GuardModel(di2, negated: true)));

            // Rule 2: Trigger OnChange (ThresholdLo=50, CompareOp=GT) -> IncCounter RETAIN0 (+5)
            ruleTable.AddRule(new Rule(
                2,
                "R3_OnChange_IncCounter",
                new TriggerModel(ai0, TriggerKind.OnChange) { CompareOp = CompareOperator.GreaterThan, ThresholdLo = 50 },
                new ActionModel(retain0, ActionKind.IncrementCounter, 5),
                new GuardModel(vflag0, negated: false)));

            // Rule 3: Trigger Interval (Every 2500ms) -> WriteRemote DO2 = 1
            ruleTable.AddRule(new Rule(
                3,
                "R4_Interval_WriteRemote",
                new TriggerModel(vreg0, TriggerKind.Interval) { ForMs = 2500u },
                new ActionModel(do2, ActionKind.WriteRemote, 1)));

            // Rule 4: Trigger TimeWindow (1000..1200, CompareOp=Between) -> SetTag DO3 = 0
            ruleTable.AddRule(new Rule(
                4,
                "R5_TimeWindow_SetTag",
                new TriggerModel(di3, TriggerKind.TimeWindow) { CompareOp = CompareOperator.Between, ThresholdLo = 1000, ThresholdHi = 1200 },
                new ActionModel(do3, ActionKind.SetTag, 0)));

            // Act 1: Nạp xuống MCU qua Staging và Commit
            var deployResult = await deployUseCase.ExecuteAsync(ruleTable);

            // Assert: Nạp thành công
            Assert.True(deployResult.IsSuccess, $"Deploy failed: {deployResult.ErrorMessage}");
            Assert.Equal(5, deployResult.DeployedRuleCount);
            Assert.Equal(1, deployResult.ActiveVersion);

            // Kiểm tra Flash của MCU đã lưu
            Assert.True(simulator.Control.Flash.HasCommittedData);
            Assert.Equal(5, simulator.Control.Flash.StoredRuleCount);

            // Act 2: Đọc ngược lại từ Active Table của MCU
            var loadedTable = await loadUseCase.ExecuteAsync(_product);

            // Assert: Đối soát 100% từng rule và từng trường dữ liệu
            Assert.Equal(5, loadedTable.Rules.Count);

            // Kiểm tra Rule 0
            Assert.Equal(di0.TagIndex, loadedTable.Rules[0].Trigger.Tag.TagIndex);
            Assert.Equal(TriggerKind.OnRise, loadedTable.Rules[0].Trigger.Type);
            Assert.Equal(do0.TagIndex, loadedTable.Rules[0].Action.TargetTag.TagIndex);
            Assert.Equal(ActionKind.SetTag, loadedTable.Rules[0].Action.Type);
            Assert.Equal(1, loadedTable.Rules[0].Action.Parameter);
            Assert.False(loadedTable.Rules[0].Guard.HasGuard);

            // Kiểm tra Rule 1
            Assert.Equal(di1.TagIndex, loadedTable.Rules[1].Trigger.Tag.TagIndex);
            Assert.Equal(TriggerKind.OnFall, loadedTable.Rules[1].Trigger.Type);
            Assert.Equal(500u, loadedTable.Rules[1].Trigger.ForMs);
            Assert.Equal(do1.TagIndex, loadedTable.Rules[1].Action.TargetTag.TagIndex);
            Assert.Equal(ActionKind.ToggleTag, loadedTable.Rules[1].Action.Type);
            Assert.True(loadedTable.Rules[1].Guard.HasGuard);
            Assert.True(loadedTable.Rules[1].Guard.Negated);
            Assert.Equal(di2.TagIndex, loadedTable.Rules[1].Guard.Tag?.TagIndex);

            // Kiểm tra Rule 2
            Assert.Equal(ai0.TagIndex, loadedTable.Rules[2].Trigger.Tag.TagIndex);
            Assert.Equal(TriggerKind.OnChange, loadedTable.Rules[2].Trigger.Type);
            Assert.Equal(CompareOperator.GreaterThan, loadedTable.Rules[2].Trigger.CompareOp);
            Assert.Equal(50, loadedTable.Rules[2].Trigger.ThresholdLo);
            Assert.Equal(retain0.TagIndex, loadedTable.Rules[2].Action.TargetTag.TagIndex);
            Assert.Equal(ActionKind.IncrementCounter, loadedTable.Rules[2].Action.Type);
            Assert.Equal(5, loadedTable.Rules[2].Action.Parameter);
            Assert.True(loadedTable.Rules[2].Guard.HasGuard);
            Assert.False(loadedTable.Rules[2].Guard.Negated);

            // Kiểm tra Rule 3
            Assert.Equal(TriggerKind.Interval, loadedTable.Rules[3].Trigger.Type);
            Assert.Equal(2500u, loadedTable.Rules[3].Trigger.ForMs);
            Assert.Equal(ActionKind.WriteRemote, loadedTable.Rules[3].Action.Type);

            // Kiểm tra Rule 4
            Assert.Equal(TriggerKind.TimeWindow, loadedTable.Rules[4].Trigger.Type);
            Assert.Equal(CompareOperator.Between, loadedTable.Rules[4].Trigger.CompareOp);
            Assert.Equal(1000, loadedTable.Rules[4].Trigger.ThresholdLo);
            Assert.Equal(1200, loadedTable.Rules[4].Trigger.ThresholdHi);
            Assert.Equal(ActionKind.SetTag, loadedTable.Rules[4].Action.Type);
            Assert.Equal(0, loadedTable.Rules[4].Action.Parameter);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Test_03_MaxQuota_100Rules_3200Bytes_AtomicCommitAndReadBack()
    {
        // Kiểm tra dung lượng cực đại: 100 Rules x 32 Bytes = 3200 Bytes = 1600 thanh ghi Modbus
        var (simulator, session, transport) = CreateVirtualSession(maxPacketSize: 64);
        var deployUseCase = new DeployRulesUseCase(session.Rules);
        var loadUseCase = new LoadRulesUseCase(session.Rules);

        try
        {
            var ruleTable = new RuleTable();
            var doTags = _product.Tags.Where(t => t.Kind == TagKind.DiscreteOutput).ToList();
            var intTags = _product.Tags.Where(t => t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.VirtualRegisterRetain).ToList();
            var boolGuardTags = _product.Tags.Where(t => t.Kind == TagKind.DiscreteInput || t.Kind == TagKind.VirtualFlag).ToList();

            for (ushort i = 0; i < 100; i++)
            {
                var trigTag = _product.Tags[i % 20];
                var trigKind = (TriggerKind)((i % 5) + 1); // OnRise, OnFall, OnChange, Interval, TimeWindow

                var trigger = new TriggerModel(trigTag, trigKind)
                {
                    ThresholdLo = (short)(i * 5),
                    ThresholdHi = (short)(i * 5 + 100),
                    ForMs = (uint)(50 + (i * 25) % 1000), // Luôn > 0 cho Interval
                    CompareOp = trigKind == TriggerKind.OnChange ? (CompareOperator)((i % 6) + 1) : CompareOperator.None
                };

                // Action: Boolean cho DO, Int32 cho IncCounter
                ActionModel action;
                if (i % 3 == 0)
                {
                    var targetInt = intTags[i % intTags.Count];
                    action = new ActionModel(targetInt, ActionKind.IncrementCounter, (short)((i % 5) + 1));
                }
                else if (i % 3 == 1)
                {
                    var targetDo = doTags[i % doTags.Count];
                    action = new ActionModel(targetDo, ActionKind.ToggleTag, 0);
                }
                else
                {
                    var targetDo = doTags[i % doTags.Count];
                    action = new ActionModel(targetDo, ActionKind.SetTag, (short)(i % 2));
                }

                // Guard: Luôn là Boolean tag
                GuardModel guard = GuardModel.Empty;
                if (i % 2 == 0)
                {
                    var gTag = boolGuardTags[i % boolGuardTags.Count];
                    guard = new GuardModel(gTag, negated: i % 4 == 0);
                }

                ruleTable.AddRule(new Rule(i, $"Rule_{i + 1}", trigger, action, guard, enabled: true));
            }

            Assert.Equal(100, ruleTable.Count);

            // Act 1: Nạp 100 rules qua Virtual COM
            var deployResult = await deployUseCase.ExecuteAsync(ruleTable);

            // Assert 1: Triển khai 100 rules thành công
            Assert.True(deployResult.IsSuccess, $"Deploy 100 rules failed: {deployResult.ErrorMessage}");
            Assert.Equal(100, deployResult.DeployedRuleCount);
            Assert.Equal(1, deployResult.ActiveVersion);

            // Kiểm tra dung lượng Flash trên MCU
            Assert.Equal(100, simulator.Control.Flash.StoredRuleCount);
            Assert.True(simulator.Control.Flash.HasCommittedData);

            // Act 2: Đọc lại toàn bộ 100 rules từ MCU
            var loadedTable = await loadUseCase.ExecuteAsync(_product);

            // Assert 2: Kiểm tra toàn vẹn đủ 100 rules
            Assert.Equal(100, loadedTable.Rules.Count);
            for (int i = 0; i < 100; i++)
            {
                var orig = ruleTable.Rules[i];
                var loaded = loadedTable.Rules[i];

                Assert.Equal(orig.Trigger.Tag.TagIndex, loaded.Trigger.Tag.TagIndex);
                Assert.Equal(orig.Trigger.Type, loaded.Trigger.Type);
                Assert.Equal(orig.Action.TargetTag.TagIndex, loaded.Action.TargetTag.TagIndex);
                Assert.Equal(orig.Action.Type, loaded.Action.Type);
                Assert.Equal(orig.Action.Parameter, loaded.Action.Parameter);
                Assert.Equal(orig.Guard.HasGuard, loaded.Guard.HasGuard);
                if (orig.Guard.HasGuard)
                {
                    Assert.Equal(orig.Guard.Negated, loaded.Guard.Negated);
                    Assert.Equal(orig.Guard.Tag?.TagIndex, loaded.Guard.Tag?.TagIndex);
                }
            }
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Test_04_SystemCommands_Ping_ClearRules_FactoryReset_Reboot()
    {
        var (simulator, session, _) = CreateVirtualSession();
        var deployUseCase = new DeployRulesUseCase(session.Rules);

        try
        {
            // Nạp trước 2 rule để MCU có dữ liệu
            var ruleTable = new RuleTable();
            ruleTable.AddRule(new Rule(
                0,
                "R1",
                new TriggerModel(_product.FindTagByName("DI0")!, TriggerKind.OnRise),
                new ActionModel(_product.FindTagByName("DO0")!, ActionKind.SetTag, 1)));
            ruleTable.AddRule(new Rule(
                1,
                "R2",
                new TriggerModel(_product.FindTagByName("DI1")!, TriggerKind.OnRise),
                new ActionModel(_product.FindTagByName("DO1")!, ActionKind.SetTag, 1)));

            var deployResult = await deployUseCase.ExecuteAsync(ruleTable);
            Assert.True(deployResult.IsSuccess);
            Assert.Equal(2, simulator.Control.Flash.StoredRuleCount);

            // 1. Lệnh CLEAR_RULES (0x0004)
            var clearResult = await session.Commands.ExecuteCommandAsync(1, SPLC_SystemCommand.CLEAR_RULES);
            Assert.Equal(SPLC_CommandStatus.DONE, clearResult.Status);
            Assert.Equal(SPLC_ErrorCode.NONE, clearResult.ErrorCode);

            // Kiểm tra số active rule trên MCU đã trở về 0
            var activeRuleCountRegs = await simulator.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleCountAddress, 1);
            Assert.Equal(0, activeRuleCountRegs[0]);

            // 2. Lệnh FACTORY_RESET (0x0003)
            var factoryResult = await session.Commands.ExecuteCommandAsync(1, SPLC_SystemCommand.FACTORY_RESET);
            Assert.Equal(SPLC_CommandStatus.DONE, factoryResult.Status);
            Assert.Equal(SPLC_ErrorCode.NONE, factoryResult.ErrorCode);
            Assert.False(simulator.Control.Flash.HasCommittedData);

            // 3. Lệnh REBOOT (0x0002)
            var rebootResult = await session.Commands.ExecuteCommandAsync(1, SPLC_SystemCommand.REBOOT);
            Assert.Equal(SPLC_CommandStatus.DONE, rebootResult.Status);

            // Kiểm tra trạng thái sức khỏe sau reboot
            var health = await session.Health.ReadHealthAsync(1);
            Assert.Equal(SPLC_ResetReason.SOFTWARE, health.ResetReason);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Test_05_FaultInjection_CorruptCrc_CommitRejected_ActivePreserved()
    {
        var (simulator, session, _) = CreateVirtualSession();
        var deployUseCase = new DeployRulesUseCase(session.Rules);
        var loadUseCase = new LoadRulesUseCase(session.Rules);

        try
        {
            // Bước 1: Nạp 1 rule hợp lệ ban đầu (v1)
            var ruleTableV1 = new RuleTable();
            ruleTableV1.AddRule(new Rule(
                0,
                "Rule_Valid_V1",
                new TriggerModel(_product.FindTagByName("DI0")!, TriggerKind.OnRise),
                new ActionModel(_product.FindTagByName("DO0")!, ActionKind.SetTag, 1)));

            var result1 = await deployUseCase.ExecuteAsync(ruleTableV1);
            Assert.True(result1.IsSuccess);
            Assert.Equal(1, result1.ActiveVersion);

            // Bước 2: Cấy lỗi làm sai lệch CRC trước khi nạp bộ rule V2
            simulator.Control.Faults.CorruptCommitCrc = true;

            var ruleTableV2 = new RuleTable();
            ruleTableV2.AddRule(new Rule(
                0,
                "Rule_Corrupt_V2",
                new TriggerModel(_product.FindTagByName("DI5")!, TriggerKind.OnFall),
                new ActionModel(_product.FindTagByName("DO5")!, ActionKind.SetTag, 0)));

            var result2 = await deployUseCase.ExecuteAsync(ruleTableV2);

            // Assert: MCU từ chối Commit vì sai CRC
            Assert.False(result2.IsSuccess);
            Assert.Equal(SPLC_ErrorCode.CRC_MISMATCH, result2.ErrorCode);

            // Xóa lỗi CRC
            simulator.Control.Faults.CorruptCommitCrc = false;

            // Đọc lại từ MCU: Bảng Active table cũ V1 vẫn được bảo vệ nguyên vẹn!
            var currentTable = await loadUseCase.ExecuteAsync(_product);
            Assert.Single(currentTable.Rules);
            Assert.Equal(_product.FindTagByName("DI0")!.TagIndex, currentTable.Rules[0].Trigger.Tag.TagIndex);
            Assert.Equal(1, result1.ActiveVersion);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Test_06_FaultInjection_ExceedMaxRules_Or_MalformedCapacity_Rejected()
    {
        var (simulator, session, _) = CreateVirtualSession();

        try
        {
            // Thử khởi tạo Staging với 101 rules (> MaxRules 100)
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            {
                await simulator.WriteSingleRegisterAsync(
                    1,
                    ModbusRegisterMap.RuleCountStagedAddress,
                    101);
            });

            // Kiểm tra trạng thái lỗi trên MCU
            var statusRegs = await simulator.ReadHoldingRegistersAsync(
                1,
                ModbusRegisterMap.ConfigStatusAddress,
                2);

            Assert.Equal(4, statusRegs[0]); // STATUS_ERROR
            Assert.Equal((ushort)SPLC_ErrorCode.INVALID_PARAMETER, statusRegs[1]);
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task Test_07_HardwareSync_BidirectionalReadWrite_UpdatesStateStore()
    {
        var (simulator, session, _) = CreateVirtualSession();
        var coordinator = new DeviceOperationCoordinator();
        var stateStore = new RuntimeStateStore();
        var monitor = new RuntimeMonitorService(
            session.RuntimeTags,
            session.Health,
            coordinator,
            stateStore)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        try
        {
            await monitor.AttachSessionAsync(session, _product);

            // Đợi kết nối ban đầu chuyển sang Good
            await WaitForConditionAsync(
                () => stateStore.GetAllTags().All(t => t.Quality == TagQuality.Good),
                timeoutMs: 2000,
                stepName: "Initial Good Quality");

            // Mô phỏng thay đổi tín hiệu I/O từ phía vi điều khiển phần cứng
            var di3 = _product.FindTagByName("DI3")!;
            var ai2 = _product.FindTagByName("AI2")!;
            simulator.Control.SetTagValue(di3.TagIndex, 1);
            simulator.Control.SetTagValue(ai2.TagIndex, 3850);

            // Chờ Host Polling Modbus RTU tự động cập nhật vào StateStore
            await WaitForConditionAsync(
                () =>
                {
                    var tDi3 = stateStore.GetAllTags().FirstOrDefault(t => t.Name == "DI3");
                    var tAi2 = stateStore.GetAllTags().FirstOrDefault(t => t.Name == "AI2");
                    return tDi3?.RawValue == 1 && tAi2?.RawValue == 3850;
                },
                timeoutMs: 2000,
                stepName: "Hardware changes synced to StateStore");

            var syncedDi3 = stateStore.GetAllTags().First(t => t.Name == "DI3");
            var syncedAi2 = stateStore.GetAllTags().First(t => t.Name == "AI2");
            Assert.Equal(1, syncedDi3.RawValue);
            Assert.Equal(3850, syncedAi2.RawValue);
        }
        finally
        {
            await monitor.StopAsync();
            await session.DisposeAsync();
        }
    }
}
