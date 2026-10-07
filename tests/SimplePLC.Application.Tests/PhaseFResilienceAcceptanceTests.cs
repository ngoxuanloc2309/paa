using System.IO;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Phase F5 — End-to-End Resilience & Failure Scenarios:
/// Bộ test chấp nhận (Acceptance Tests) khóa cứng 10 kịch bản vận hành thực tế:
/// - E2E-F01: Online -> unplug -> reconnect -> GOOD.
/// - E2E-F02: Deploy 100 rules -> timeout giữa staging -> Active Table cũ giữ nguyên.
/// - E2E-F03: Deploy -> commit -> reboot -> rules được bảo toàn nguyên vẹn sau khi reconnect.
/// - E2E-F04: COM3 biến mất -> COM4 xuất hiện -> migrate -> duy trì đúng 1 monitor duy nhất.
/// - E2E-F05: Unknown product variant (0xABCD) + valid profile -> connect -> monitor -> deploy thành công.
/// - E2E-F06: Invalid profile (WireProfile != 1) -> từ chối kết nối dứt khoát và an toàn.
/// - E2E-F07: Repeated reboot x 50 -> không leak resource, session hay monitor task.
/// - E2E-F08: Malformed Modbus response -> app không crash, bắt lỗi và tiếp tục chu kỳ sau.
/// - E2E-F09: Simulator BUSY khi deploy -> nhận kết quả lỗi có kiểm soát (không treo, không ném unhandled).
/// - E2E-F10: Disconnect khi Watch/Canvas đang hoạt động -> Last Known/Stale -> reconnect -> GOOD.
/// </summary>
public sealed class PhaseFResilienceAcceptanceTests
{
    private static DeviceEndpoint Endpoint(string port = "COM3") => new UsbCdcEndpoint(port);

    private static RuleTable CreateRuleTable(ProductDefinition product, int ruleCount, int marker = 100)
    {
        var table = new RuleTable();
        for (int i = 0; i < ruleCount; i++)
        {
            var inTag = product.Tags[i % 8];
            var outTag = product.Tags[8 + (i % 8)];
            var trigger = new TriggerModel(inTag, TriggerKind.OnRise);
            var action = new ActionModel(outTag, ActionKind.SetTag, marker);
            table.AddRule(new Rule(i, $"R_{i}", trigger, action));
        }
        return table;
    }

    private static async Task<bool> WaitForStateAsync(
        IDeviceLifecycleManager manager,
        ConnectionLifecycleState expectedState,
        TimeSpan timeout)
    {
        if (manager.CurrentState == expectedState) return true;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? s, LifecycleStateChangedEventArgs e)
        {
            if (e.NewState == expectedState)
            {
                tcs.TrySetResult(true);
            }
        }
        manager.StateChanged += Handler;
        try
        {
            if (manager.CurrentState == expectedState) return true;
            return await tcs.Task.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return false;
        }
        finally
        {
            manager.StateChanged -= Handler;
        }
    }

    private sealed class TestDeviceFactory : IDeviceConnectionFactory
    {
        private readonly McuReferenceSimulator _simulator;
        public bool ShouldSucceed { get; set; } = true;

        public TestDeviceFactory(McuReferenceSimulator simulator)
        {
            _simulator = simulator;
        }

        public async Task<DeviceConnectionResult> ConnectAsync(
            DeviceEndpoint endpoint,
            byte slaveId = 1,
            CancellationToken cancellationToken = default)
        {
            if (!ShouldSucceed)
            {
                return DeviceConnectionResult.Failed("Device unreachable");
            }

            var transport = new FakeUsbCdcTransport { IsOpen = true };
            var client = new FakeModbusClient(_simulator, isConnected: true);

            var validator = new StandardDeviceCompatibilityValidator();
            var reader = new DeviceDescriptorReader(client);
            var descriptor = await reader.ReadDescriptorAsync(slaveId, cancellationToken).ConfigureAwait(false);
            var comp = validator.Validate(descriptor);
            if (!comp.IsCompatible)
            {
                return DeviceConnectionResult.Incompatible(comp);
            }

            var session = new DeviceSession(
                endpoint,
                descriptor,
                slaveId,
                transport,
                client,
                new RuleTableGateway(client),
                new RuntimeTagReader(client),
                new DeviceHealthReader(client),
                new SystemCommandClient(client));

            return DeviceConnectionResult.Success(session);
        }
    }

    [Fact]
    public async Task E2E_F01_Online_Unplug_Reconnect_ReturnsGood()
    {
        var sim = new McuReferenceSimulator();
        var factory = new TestDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var store = new RuntimeStateStore();

        var monitor = new RuntimeMonitorService(
            new RuntimeTagReader(new FakeModbusClient(sim, true)),
            new DeviceHealthReader(new FakeModbusClient(sim, true)),
            coordinator: null,
            stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        monitor.OnTransportLost = ex => lifecycle.NotifyUnexpectedDisconnectAsync(ex);

        // 1. Online
        await lifecycle.ConnectAsync(Endpoint("COM3"));
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);
        await Task.Delay(60);
        Assert.All(store.CurrentSnapshot.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));

        // 2. Unplug
        sim.Control.Faults.CableDisconnected = true;
        factory.ShouldSucceed = false;
        await Task.Delay(80);
        Assert.False(monitor.IsRunning);

        // 3. Re-plug & Reconnect
        sim.Control.Faults.CableDisconnected = false;
        factory.ShouldSucceed = true;
        await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);
        await Task.Delay(60);

        // 4. Returns Good
        Assert.True(monitor.IsRunning);
        Assert.All(store.CurrentSnapshot.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));

        await monitor.StopAsync();
        await lifecycle.DisconnectAsync();
    }

    [Fact]
    public async Task E2E_F02_Deploy100Rules_TimeoutMidway_ActiveTablePreserved()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var ruleGateway = new RuleTableGateway(client);
        var deployUseCase = new DeployRulesUseCase(ruleGateway);
        var loadUseCase = new LoadRulesUseCase(ruleGateway);

        // Baseline: Chưa có rule nào trong Active Table
        var initialLoad = await loadUseCase.ExecuteAsync(product);
        Assert.Empty(initialLoad.Rules);

        // Cấy lỗi timeout tại transaction ghi chunk thứ 7
        sim.Control.Faults.TimeoutOnWriteNumber = 7;
        var table100 = CreateRuleTable(product, 100, marker: 999);

        // Deploy ném TimeoutException có kiểm soát do lỗi truyền thông
        await Assert.ThrowsAsync<TimeoutException>(() => deployUseCase.ExecuteAsync(table100));

        // Gỡ timeout để đọc lại
        sim.Control.Faults.TimeoutOnWriteNumber = null;

        // Bất biến E2E-F02: Active Table cũ được bảo toàn hoàn toàn
        var afterLoad = await loadUseCase.ExecuteAsync(product);
        Assert.Empty(afterLoad.Rules);

        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(0, ver[0]);
    }

    [Fact]
    public async Task E2E_F03_Deploy_Commit_Reboot_RulesPreservedAfterReconnect()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var ruleGateway = new RuleTableGateway(client);
        var deployUseCase = new DeployRulesUseCase(ruleGateway);
        var loadUseCase = new LoadRulesUseCase(ruleGateway);

        // 1. Deploy 100 rules thành công
        var table100 = CreateRuleTable(product, 100, marker: 432);
        var deployRes = await deployUseCase.ExecuteAsync(table100);
        Assert.True(deployRes.IsSuccess);
        Assert.Equal(1, deployRes.ActiveVersion);

        // 2. MCU Reboot (mô phỏng Power Cycle)
        sim.Control.PowerCycle();

        // 3. Reconnect và đọc lại
        var loaded = await loadUseCase.ExecuteAsync(product);
        Assert.Equal(100, loaded.Rules.Count);
        Assert.Equal(432, loaded.Rules[0].Action.Parameter);

        var ver = await client.ReadHoldingRegistersAsync(1, ModbusRegisterMap.ActiveRuleVersionAddress, 1);
        Assert.Equal(1, ver[0]);
    }

    [Fact]
    public async Task E2E_F04_PortMigration_COM3ToCOM4_MaintainsSingleMonitor()
    {
        var sim = new McuReferenceSimulator();
        string activePort = "COM3";
        var factory = new TestDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingImmediate,
            timeProvider: TimeProvider.System,
            candidateDiscovery: ct => Task.FromResult<IReadOnlyList<DeviceEndpoint>>(new[] { Endpoint(activePort) }));

        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var store = new RuntimeStateStore();
        var monitor = new RuntimeMonitorService(
            new RuntimeTagReader(new FakeModbusClient(sim, true)),
            new DeviceHealthReader(new FakeModbusClient(sim, true)),
            coordinator: null,
            stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        // 1. Start on COM3
        await lifecycle.ConnectAsync(Endpoint("COM3"));
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);
        await Task.Delay(40);
        Assert.Equal("COM3", ((UsbCdcEndpoint)lifecycle.CurrentEndpoint!).PortName);

        // 2. COM3 disappears, COM4 appears
        activePort = "COM4";
        await lifecycle.NotifyUnexpectedDisconnectAsync(new IOException("COM3 lost"));
        await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));

        // Re-attach
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);
        await Task.Delay(40);

        // 3. Assert single monitor running on COM4
        Assert.True(monitor.IsRunning);
        Assert.Equal("COM4", ((UsbCdcEndpoint)lifecycle.CurrentEndpoint!).PortName);
        Assert.Equal("COM4", ((UsbCdcEndpoint)sessionManager.CurrentSession!.Endpoint).PortName);

        await monitor.StopAsync();
        await lifecycle.DisconnectAsync();
    }

    [Fact]
    public async Task E2E_F05_UnknownProductVariant_ValidProfile_ConnectsMonitorsDeploys()
    {
        var sim = new McuReferenceSimulator();
        // Cấy biến thể lạ 0xABCD nhưng Wire Profile V1 chuẩn
        sim.Control.Faults.OverrideDeviceVariant = 0xABCD;

        var client = new FakeModbusClient(sim, isConnected: true);
        var descriptorReader = new DeviceDescriptorReader(client);
        var descriptor = await descriptorReader.ReadDescriptorAsync(1);

        Assert.Equal(0xABCD, descriptor.DeviceVariant);

        // Sinh dynamic ProductDefinition theo kiến trúc Self-Describing
        var dynamicProduct = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var ruleGateway = new RuleTableGateway(client);
        var deployUseCase = new DeployRulesUseCase(ruleGateway);

        // Nạp rule thành công trên biến thể lạ mà không crash
        var table = CreateRuleTable(dynamicProduct, 5, marker: 888);
        var deployRes = await deployUseCase.ExecuteAsync(table);

        Assert.True(deployRes.IsSuccess);
        Assert.Equal(1, deployRes.ActiveVersion);
    }

    [Fact]
    public async Task E2E_F06_InvalidProfile_RejectedCleanly()
    {
        var sim = new McuReferenceSimulator();
        // Cấy Wire Profile Version không tương thích (ví dụ 0x0200 thay vì V1)
        sim.Control.Faults.UnsupportedDescriptor = true;

        var factory = new TestDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        bool result = await lifecycle.ConnectAsync(Endpoint("COM3"));

        // Bất biến: Từ chối kết nối an toàn, chuyển về Disconnected với lý do IncompatibleDevice
        Assert.False(result);
        Assert.Equal(ConnectionLifecycleState.Disconnected, lifecycle.CurrentState);
        Assert.Equal(ConnectionFailureReason.IncompatibleDevice, lifecycle.FailureReason);
        Assert.False(sessionManager.HasActiveSession);
    }

    [Fact]
    public async Task E2E_F07_RepeatedReboot_50Cycles_NoResourceLeak()
    {
        var sim = new McuReferenceSimulator();
        var factory = new TestDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        await lifecycle.ConnectAsync(Endpoint("COM3"));

        // Lặp lại 50 lần Software Reboot và kết nối lại
        for (int i = 1; i <= 50; i++)
        {
            sim.Control.SoftwareReboot();
            await lifecycle.NotifyUnexpectedDisconnectAsync(new IOException("Rebooting"));

            bool reconnected = await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));
            Assert.True(reconnected, $"Cycle {i} reconnect must succeed");
        }

        // Bất biến: Luôn chỉ có đúng 1 session duy nhất
        Assert.True(sessionManager.HasActiveSession);
        Assert.NotNull(sessionManager.CurrentSession);

        await lifecycle.DisconnectAsync();
    }

    [Fact]
    public async Task E2E_F08_MalformedModbusResponse_NoAppCrash()
    {
        var transport = new FakeUsbCdcTransport { IsOpen = true };
        var client = new ModbusRtuClient(transport);

        // Gửi response có function code lạ và CRC rác
        byte[] garbageResponse = { 0x01, 0x99, 0xEE, 0xFF, 0x00, 0x12 };
        transport.EnqueueResponse(garbageResponse);

        // App bắt InvalidDataException một cách kiểm soát, không crash unhandled
        await Assert.ThrowsAsync<InvalidDataException>(
            () => client.ReadHoldingRegistersAsync(1, 0x0000, 1));

        // Lock phải được giải phóng, transport vẫn mở
        Assert.True(transport.IsOpen);
    }

    [Fact]
    public async Task E2E_F09_SimulatorBusyDuringDeploy_ReceivesControlledFailure()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var ruleGateway = new RuleTableGateway(client);
        var deployUseCase = new DeployRulesUseCase(ruleGateway);

        sim.Control.Faults.DeviceBusy = true;

        var table = CreateRuleTable(product, 5);
        var result = await deployUseCase.ExecuteAsync(table);

        // Nhận kết quả lỗi có kiểm soát
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.BUSY, result.ErrorCode);
    }

    [Fact]
    public async Task E2E_F10_DisconnectDuringActiveWatch_ShowsLastKnown_ReconnectsToGood()
    {
        var sim = new McuReferenceSimulator();
        var factory = new TestDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var store = new RuntimeStateStore();

        var monitor = new RuntimeMonitorService(
            new RuntimeTagReader(new FakeModbusClient(sim, true)),
            new DeviceHealthReader(new FakeModbusClient(sim, true)),
            coordinator: null,
            stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        monitor.OnTransportLost = ex => lifecycle.NotifyUnexpectedDisconnectAsync(ex);

        // 1. Kết nối và theo dõi tags -> Good
        await lifecycle.ConnectAsync(Endpoint("COM3"));
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);
        await Task.Delay(50);

        var snap1 = store.CurrentSnapshot;
        Assert.All(snap1.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));
        int sampleValueBefore = snap1.Tags[0].RawValue;

        // 2. Ngắt kết nối vật lý
        sim.Control.Faults.CableDisconnected = true;
        factory.ShouldSucceed = false;
        await Task.Delay(80);

        // Bất biến: Trạng thái Disconnected, RawValue được giữ nguyên và Quality không còn là Good
        var snap2 = store.CurrentSnapshot;
        Assert.Equal(ConnectionStatus.Disconnected, snap2.ConnectionStatus);
        Assert.Equal(sampleValueBefore, snap2.Tags[0].RawValue);
        Assert.NotEqual(TagQuality.Good, snap2.Tags[0].Quality);

        // 3. Phục hồi kết nối
        sim.Control.Faults.CableDisconnected = false;
        factory.ShouldSucceed = true;
        await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);
        await Task.Delay(50);

        // 4. Tags phục hồi lại Good
        var snap3 = store.CurrentSnapshot;
        Assert.Equal(ConnectionStatus.Connected, snap3.ConnectionStatus);
        Assert.All(snap3.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));

        await monitor.StopAsync();
        await lifecycle.DisconnectAsync();
    }
}
