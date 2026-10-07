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
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Bộ kiểm thử tự động hóa đầu-cuối (End-to-End Backend Automation Tests)
/// mô phỏng vi điều khiển MCU STM32 kết nối với App SimplePLC qua Virtual COM.
/// Toàn bộ giao tiếp sử dụng khung byte Modbus RTU thật, tính toán CRC-16 hai chiều,
/// xử lý xé gói USB CDC (64 bytes), máy trạng thái Staging Flash commit và vòng lặp Monitor.
/// </summary>
public class McuVirtualComAutomationTests
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

    private sealed class DynamicDetector : IDeviceDetector
    {
        private readonly Func<IEnumerable<DeviceEndpoint>> _detector;
        public DynamicDetector(Func<IEnumerable<DeviceEndpoint>> detector) => _detector = detector;
        public Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<DeviceEndpoint>>(_detector().ToList());
    }

    [Fact]
    public async Task Test_01_VirtualCom_Handshake_And_SelfDescribing_Discovery()
    {
        // Arrange: MCU Virtual COM Server
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);
        var transport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 64 };

        var connectionFactory = new DeviceConnectionFactory(transportFactory: _ => transport);
        var endpoint = new UsbCdcEndpoint("COM_VIRTUAL_MCU");

        // Act: App kết nối với MCU qua Virtual COM
        var connectResult = await connectionFactory.ConnectAsync(endpoint, slaveId: 1);

        // Assert
        Assert.True(connectResult.IsSuccess, $"Connection failed: {connectResult.FailureReason}");
        Assert.NotNull(connectResult.Session);

        var session = connectResult.Session;
        Assert.Equal(1, session.SlaveId);
        Assert.Equal("USB CDC (COM_VIRTUAL_MCU)", session.Endpoint.DisplayName);

        // Kiểm tra khung byte thật đã truyền qua lại trên đường truyền nối tiếp
        Assert.True(transport.TotalBytesSent > 0, "Host must have sent raw bytes to MCU.");
        Assert.True(transport.TotalBytesReceived > 0, "MCU must have returned raw bytes to Host.");

        // Kiểm tra Descriptor và ProductDefinition động
        Assert.Equal(SPLC_DeviceClass.REMOTE_IO, session.Descriptor.DeviceClass);
        Assert.Equal((ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI, session.Descriptor.DeviceVariant);
        Assert.Equal(8, session.Product.Tags.Count(t => t.Kind == TagKind.DiscreteInput));
        Assert.Equal(8, session.Product.Tags.Count(t => t.Kind == TagKind.DiscreteOutput));
        Assert.Equal(4, session.Product.Tags.Count(t => t.Kind == TagKind.AnalogInput));

        // Kiểm tra đọc Health telemetry qua Modbus RTU
        var health = await session.Health.ReadHealthAsync(1);
        Assert.Equal(SPLC_ResetReason.POWER_ON, health.ResetReason);
        Assert.True(health.CpuLoadPercent <= 100);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task Test_02_VirtualCom_Rule_Compilation_And_Staged_Deployment()
    {
        // Arrange
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);
        var transport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 64 };

        var connectionFactory = new DeviceConnectionFactory(transportFactory: _ => transport);
        var connectResult = await connectionFactory.ConnectAsync(new UsbCdcEndpoint("COM_VIRTUAL_MCU"), slaveId: 1);
        Assert.True(connectResult.IsSuccess);
        var session = connectResult.Session!;

        var coordinator = new DeviceOperationCoordinator();
        var deployUseCase = new DeployRulesUseCase(session.Rules, coordinator);
        var loadUseCase = new LoadRulesUseCase(session.Rules);

        // Tạo 2 rule logic trong Domain
        var ruleTable = new RuleTable();
        var di0 = session.Product.FindTagByName("DI0")!;
        var do0 = session.Product.FindTagByName("DO0")!;
        var di1 = session.Product.FindTagByName("DI1")!;
        var do1 = session.Product.FindTagByName("DO1")!;
        var di2 = session.Product.FindTagByName("DI2")!;

        // Rule 1: DI0 OnRise -> Set DO0 = 1
        ruleTable.AddRule(new Rule(
            0,
            "R1_TurnOnLamp",
            new TriggerModel(di0, TriggerKind.OnRise),
            new ActionModel(do0, ActionKind.SetTag, 1)));

        // Rule 2: DI1 OnRise + Guard DI2 -> Set DO1 = 1
        ruleTable.AddRule(new Rule(
            1,
            "R2_SafetyInterlock",
            new TriggerModel(di1, TriggerKind.OnRise),
            new ActionModel(do1, ActionKind.SetTag, 1),
            new GuardModel(di2, negated: false)));

        // Act: Triển khai nạp rule xuống MCU qua Virtual COM
        var deployResult = await deployUseCase.ExecuteAsync(ruleTable);

        // Assert: Nạp thành công
        Assert.True(deployResult.IsSuccess, $"Deploy failed: {deployResult.ErrorMessage}");
        Assert.Equal(2, deployResult.DeployedRuleCount);
        Assert.Equal(SPLC_ErrorCode.NONE, deployResult.ErrorCode);

        // Kiểm tra trực tiếp trên Flash của McuReferenceSimulator:
        // Đã được Commit vĩnh viễn, Active table = 2 rules, version = 1
        Assert.True(mcuSimulator.Control.Flash.HasCommittedData);
        Assert.Equal(2, mcuSimulator.Control.Flash.StoredRuleCount);
        Assert.Equal(1, mcuSimulator.Control.Flash.StoredVersion);

        // Act 2: Đọc ngược lại từ MCU lên để thẩm định tính toàn vẹn (Round-trip integrity)
        var loadedTable = await loadUseCase.ExecuteAsync(session.Product);
        Assert.Equal(2, loadedTable.Rules.Count);
        Assert.Equal(TriggerKind.OnRise, loadedTable.Rules[0].Trigger.Type);
        Assert.Equal(ActionKind.SetTag, loadedTable.Rules[0].Action.Type);
        Assert.Equal(1, loadedTable.Rules[0].Action.Parameter);
        Assert.Equal(TriggerKind.OnRise, loadedTable.Rules[1].Trigger.Type);
        Assert.Equal(ActionKind.SetTag, loadedTable.Rules[1].Action.Type);
        Assert.True(loadedTable.Rules[1].Guard.HasGuard);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task Test_03_VirtualCom_Runtime_Tag_Polling_And_StateStore_Sync()
    {
        // Arrange
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);
        var transport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 64 };

        var connectionFactory = new DeviceConnectionFactory(transportFactory: _ => transport);
        var connectResult = await connectionFactory.ConnectAsync(new UsbCdcEndpoint("COM_VIRTUAL_MCU"), slaveId: 1);
        var session = connectResult.Session!;

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

        await monitor.AttachSessionAsync(session, session.Product);

        // Đợi kết nối ban đầu chuyển sang Good Quality
        await WaitForConditionAsync(
            () => stateStore.GetAllTags().All(t => t.Quality == TagQuality.Good),
            timeoutMs: 2000,
            stepName: "Initial tags Good Quality");

        // Act: Mô phỏng tín hiệu phần cứng trên vi điều khiển MCU
        var di0 = session.Product.FindTagByName("DI0")!;
        var ai0 = session.Product.FindTagByName("AI0")!;
        mcuSimulator.Control.SetTagValue(di0.TagIndex, 1);
        mcuSimulator.Control.SetTagValue(ai0.TagIndex, 2500);

        // Chờ App tự động Polling qua Modbus RTU và cập nhật vào RuntimeStateStore
        await WaitForConditionAsync(
            () =>
            {
                var tagDi0 = stateStore.GetAllTags().FirstOrDefault(t => t.Name == "DI0");
                var tagAi0 = stateStore.GetAllTags().FirstOrDefault(t => t.Name == "AI0");
                return tagDi0?.RawValue == 1 && tagAi0?.RawValue == 2500;
            },
            timeoutMs: 2000,
            stepName: "StateStore synchronized with MCU hardware changes");

        // Assert
        var syncedDi0 = stateStore.GetAllTags().FirstOrDefault(t => t.Name == "DI0");
        var syncedAi0 = stateStore.GetAllTags().FirstOrDefault(t => t.Name == "AI0");
        Assert.NotNull(syncedDi0);
        Assert.NotNull(syncedAi0);
        Assert.Equal(1, syncedDi0.RawValue);
        Assert.Equal(2500, syncedAi0.RawValue);
        Assert.Equal(TagQuality.Good, syncedDi0.Quality);
        Assert.Equal(TagQuality.Good, syncedAi0.Quality);

        await monitor.StopAsync();
        await session.DisposeAsync();
    }

    [Fact]
    public async Task Test_04_VirtualCom_Packet_Fragmentation_Resilience()
    {
        // Kiểm tra tính kiên cường (Resilience) khi USB CDC xé nhỏ gói chỉ còn 16 bytes
        // (nhỏ hơn nhiều so với chuẩn USB FS 64 bytes)
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);
        var transport = new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 16 };

        var connectionFactory = new DeviceConnectionFactory(transportFactory: _ => transport);
        var connectResult = await connectionFactory.ConnectAsync(new UsbCdcEndpoint("COM_VIRTUAL_MCU"), slaveId: 1);
        Assert.True(connectResult.IsSuccess);
        var session = connectResult.Session!;

        // Đọc Descriptor qua gói bị phân mảnh cực nhỏ
        Assert.Equal(SPLC_DeviceClass.REMOTE_IO, session.Descriptor.DeviceClass);

        // Tạo 4 rule (64 registers) để kiểm tra xé gói FC16 Staging Table
        var ruleTable = new RuleTable();
        for (int i = 0; i < 4; i++)
        {
            var di = session.Product.FindTagByName($"DI{i}")!;
            var @do = session.Product.FindTagByName($"DO{i}")!;
            ruleTable.AddRule(new Rule(
                i,
                $"Rule_{i}",
                new TriggerModel(di, TriggerKind.OnRise),
                new ActionModel(@do, ActionKind.SetTag, 1)));
        }

        var deployUseCase = new DeployRulesUseCase(session.Rules);
        var deployResult = await deployUseCase.ExecuteAsync(ruleTable);

        Assert.True(deployResult.IsSuccess);
        Assert.Equal(4, deployResult.DeployedRuleCount);

        await session.DisposeAsync();
    }

    [Fact]
    public async Task Test_05_VirtualCom_Soft_Reboot_And_Lifecycle_Recovery()
    {
        // Kiểm tra vòng đời: Gửi lệnh Soft Reboot từ App -> MCU khởi động lại -> Tự phục hồi kết nối
        var mcuSimulator = new McuReferenceSimulator();
        var mcuServer = new McuModbusRtuServer(mcuSimulator, slaveId: 1);

        // Cung cấp Transport mới mỗi khi Factory được yêu cầu mở kết nối
        var factory = new DeviceConnectionFactory(
            transportFactory: _ => new VirtualComMcuTransport(mcuServer) { MaxPacketSize = 64 });

        var sessionManager = new SessionManager();
        var stateStore = new RuntimeStateStore();
        var coordinator = new DeviceOperationCoordinator();
        var endpoint = new UsbCdcEndpoint("COM_VIRTUAL_MCU");
        var detector = new DynamicDetector(() => new[] { endpoint });

        var monitor = new RuntimeMonitorService(
            new FakeRuntimeTagReader(),
            new FakeDeviceHealthReader(),
            coordinator,
            stateStore)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingFast,
            deviceDetector: detector);

        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
                await monitor.AttachSessionAsync(session, _product);
            else
                await monitor.DetachSessionAsync();
        };

        monitor.OnTransportLost = ex => lifecycle.NotifyUnexpectedDisconnectAsync(ex);

        // Step 1: Kết nối ban đầu
        await lifecycle.ConnectAsync(endpoint);
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);

        // Step 2: Gửi lệnh Reboot có chủ đích
        var commandUseCase = new ExecuteSystemCommandUseCase(
            sessionManager.CurrentSession!.Commands,
            coordinator,
            sessionManager,
            lifecycle);

        await commandUseCase.ExecuteAsync(SPLC_SystemCommand.REBOOT);

        // Step 3: Kiểm tra trạng thái chuyển sang Restarting
        Assert.Equal(ConnectionLifecycleState.Restarting, lifecycle.CurrentState);

        // Step 4: Chờ App tự động kết nối lại sau reboot
        await WaitForConditionAsync(
            () => lifecycle.CurrentState == ConnectionLifecycleState.Connected,
            timeoutMs: 3000,
            stepName: "Reboot recovery to Connected");

        // Assert: Session đã phục hồi, MCU đang online với ResetReason = SOFTWARE
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);
        Assert.NotNull(sessionManager.CurrentSession);

        var healthAfterReboot = await sessionManager.CurrentSession.Health.ReadHealthAsync(1);
        Assert.Equal(SPLC_ResetReason.SOFTWARE, healthAfterReboot.ResetReason);

        await lifecycle.DisposeAsync();
        await monitor.StopAsync();
    }
}
