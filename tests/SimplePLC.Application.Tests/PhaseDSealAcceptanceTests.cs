using System.IO;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Phase D Seal Acceptance Tests (E2E):
/// Kiểm tra sự khớp nối hoàn chỉnh của toàn bộ chuỗi A + B + C + D:
/// - Phase A: McuReferenceSimulator
/// - Phase B: RuntimeStateStore + Tag Quality Lifecycle (Good -> Unknown -> Good)
/// - Phase C: Dynamic rule table & canvas data integrity
/// - Phase D: DeviceLifecycleManager (Fast Recovery, Port Migration, Expected Reboot, Single Monitor Invariant)
/// </summary>
public sealed class PhaseDSealAcceptanceTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    private static UsbCdcEndpoint Endpoint(string port) => new(port);

    private static async Task<DeviceConnectionResult> CreateSessionResultAsync(
        DeviceEndpoint endpoint,
        McuReferenceSimulator sim)
    {
        // Mỗi session mới tương ứng một phiên mở cổng transport / ModbusClient mới tới MCU
        var transport = new FakeUsbCdcTransport { IsOpen = true };
        var client = new FakeModbusClient(sim, isConnected: true);

        var validator = new StandardDeviceCompatibilityValidator();
        var reader = new DeviceDescriptorReader(client);
        var descriptor = await reader.ReadDescriptorAsync(1).ConfigureAwait(false);
        var comp = validator.Validate(descriptor);
        if (!comp.IsCompatible)
        {
            await transport.CloseAsync(CancellationToken.None).ConfigureAwait(false);
            return DeviceConnectionResult.Incompatible(comp);
        }

        var session = new DeviceSession(
            endpoint,
            descriptor,
            1,
            transport,
            client,
            new RuleTableGateway(client),
            new RuntimeTagReader(client),
            new DeviceHealthReader(client),
            new SystemCommandClient(client));

        return DeviceConnectionResult.Success(session);
    }

    // =========================================================================
    // E2E-1: Cable Unplug -> UNKNOWN -> Fast Recovery -> Reconnect -> GOOD
    // =========================================================================
    [Fact]
    [Trait("Category", "E2E")]
    public async Task E2E_UnplugRecover_RestoresGoodState()
    {
        // Arrange: Simulator + Store + Monitor + LifecycleManager
        var sim = new McuReferenceSimulator();
        var sessionManager = new SessionManager();
        var store = new RuntimeStateStore();
        store.InitializeProduct(_product);

        var monitor = new RuntimeMonitorService(
            new RuntimeTagReader(new FakeModbusClient(sim, isConnected: true)),
            new DeviceHealthReader(new FakeModbusClient(sim, isConnected: true)),
            stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(15)
        };

        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (sim.Control.Faults.CableDisconnected)
            {
                return DeviceConnectionResult.Failed("Cable disconnected");
            }
            return await CreateSessionResultAsync(ep, sim).ConfigureAwait(false);
        });

        var detector = new DynamicDetector(() => new[] { Endpoint("COM3") });

        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingFast,
            deviceDetector: detector);

        // Wire: SessionChanged -> Attach/Detach Monitor
        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
                await monitor.AttachSessionAsync(session, _product).ConfigureAwait(false);
            else
                await monitor.DetachSessionAsync().ConfigureAwait(false);
        };

        // Wire: Definitive Transport Error -> Lifecycle recovery
        monitor.OnTransportLost = ex => lifecycle.NotifyUnexpectedDisconnectAsync(ex);

        // Step 1: Connect ban đầu
        bool connected = await lifecycle.ConnectAsync(Endpoint("COM3"));
        Assert.True(connected);
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);

        // Đợi ít nhất 1 chu kỳ polling thành công để tags có chất lượng Good
        await WaitForConditionAsync(
            () => store.GetAllTags().Count > 0 && store.GetAllTags().All(t => t.Quality == TagQuality.Good),
            timeoutMs: 2000,
            stepName: "Initial Good Quality");

        var snapshotBefore = store.CurrentSnapshot;
        Assert.Equal(ConnectionStatus.Connected, snapshotBefore.ConnectionStatus);
        Assert.Equal(TagQuality.Good, store.GetTag(0)!.Quality);

        // Step 2: Cắt cáp USB (Simulate Cable Unplug)
        sim.Control.Faults.CableDisconnected = true;

        // Đợi monitor phát hiện lỗi, chuyển store sang Unknown và kích hoạt lifecycle Reconnecting
        await WaitForConditionAsync(
            () => lifecycle.CurrentState == ConnectionLifecycleState.Reconnecting &&
                  store.CurrentSnapshot.ConnectionStatus == ConnectionStatus.Disconnected &&
                  store.GetAllTags().All(t => t.Quality == TagQuality.Unknown),
            timeoutMs: 2500,
            stepName: "Unplug -> Reconnecting & Tag Quality Degraded to Unknown");

        Assert.Equal(ConnectionLifecycleState.Reconnecting, lifecycle.CurrentState);

        // Step 3: Cắm lại cáp USB (Simulate Cable Re-plug)
        sim.Control.Faults.CableDisconnected = false;

        // Đợi LifecycleManager hoàn tất Fast Recovery, thiết lập session mới và phục hồi trạng thái Good
        await WaitForConditionAsync(
            () => lifecycle.CurrentState == ConnectionLifecycleState.Connected &&
                  store.CurrentSnapshot.ConnectionStatus == ConnectionStatus.Connected &&
                  store.GetAllTags().All(t => t.Quality == TagQuality.Good),
            timeoutMs: 3500,
            stepName: "Re-plug -> Recovery to Connected & Tag Quality Restored to Good");

        // Assert cuối cùng
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, lifecycle.FailureReason);
        Assert.True(monitor.IsRunning);
        Assert.Equal(TagQuality.Good, store.GetTag(0)!.Quality);

        await lifecycle.DisposeAsync();
        await monitor.StopAsync();
    }

    // =========================================================================
    // E2E-2: Expected Reboot -> Restarting -> Preserves Rules -> Reconnected Good
    // =========================================================================
    [Fact]
    [Trait("Category", "E2E")]
    public async Task E2E_RebootReconnect_PreservesRulesAndRuntime()
    {
        // Arrange
        var sim = new McuReferenceSimulator();
        var sessionManager = new SessionManager();
        var store = new RuntimeStateStore();
        store.InitializeProduct(_product);

        var monitor = new RuntimeMonitorService(
            new RuntimeTagReader(new FakeModbusClient(sim, isConnected: true)),
            new DeviceHealthReader(new FakeModbusClient(sim, isConnected: true)),
            stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(15)
        };

        var factory = new ConfigurableConnectionFactory((ep, ct) =>
            CreateSessionResultAsync(ep, sim));

        var detector = new DynamicDetector(() => new[] { Endpoint("COM3") });

        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingFast,
            deviceDetector: detector);

        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
                await monitor.AttachSessionAsync(session, _product).ConfigureAwait(false);
            else
                await monitor.DetachSessionAsync().ConfigureAwait(false);
        };

        monitor.OnTransportLost = ex => lifecycle.NotifyUnexpectedDisconnectAsync(ex);

        // Giả lập bảng quy tắc của Host (trong RAM / Editor của người dùng)
        var hostRuleTable = new List<string>
        {
            "Rule #1: IF DI0 == 1 THEN DO0 = 1",
            "Rule #2: IF AI0 > 500 THEN DO1 = 1"
        };

        // Step 1: Connect thành công
        await lifecycle.ConnectAsync(Endpoint("COM3"));
        await WaitForConditionAsync(
            () => store.GetAllTags().All(t => t.Quality == TagQuality.Good),
            timeoutMs: 2000,
            stepName: "Initial connection Good Quality");

        // Step 2: Gửi lệnh REBOOT có chủ đích
        // Lấy commandClient từ session hiện tại
        var currentSession = sessionManager.CurrentSession!;
        var coordinator = new DeviceOperationCoordinator();
        var commandUseCase = new SimplePLC.Application.UseCases.ExecuteSystemCommandUseCase(
            currentSession.Commands,
            coordinator,
            sessionManager,
            lifecycle);

        await commandUseCase.ExecuteAsync(SimplePLC.Protocol.Enums.SPLC_SystemCommand.REBOOT);

        // Step 3: Kiểm tra trạng thái chuyển sang Restarting ngay lập tức
        Assert.Equal(ConnectionLifecycleState.Restarting, lifecycle.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, lifecycle.FailureReason);

        // Bảng quy tắc trong bộ nhớ Host vẫn được bảo toàn nguyên vẹn, không bị mất mát
        Assert.Equal(2, hostRuleTable.Count);
        Assert.Equal("Rule #1: IF DI0 == 1 THEN DO0 = 1", hostRuleTable[0]);
        Assert.Equal("Rule #2: IF AI0 > 500 THEN DO1 = 1", hostRuleTable[1]);

        // Step 4: Đợi quá trình Expected Restart hoàn tất và tự động kết nối lại
        await WaitForConditionAsync(
            () => lifecycle.CurrentState == ConnectionLifecycleState.Connected &&
                  store.CurrentSnapshot.ConnectionStatus == ConnectionStatus.Connected &&
                  store.GetAllTags().All(t => t.Quality == TagQuality.Good),
            timeoutMs: 3500,
            stepName: "Reboot recovery to Connected and Good Quality");

        // Assert: Thiết bị đã kết nối lại, rules Host vẫn nguyên vẹn, monitor tiếp tục
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);
        Assert.True(monitor.IsRunning);
        Assert.Equal(2, hostRuleTable.Count);

        await lifecycle.DisposeAsync();
        await monitor.StopAsync();
    }

    // =========================================================================
    // E2E-3: COM3 Disappears -> COM4 Appears -> Port Migration -> Single Monitor Loop
    // =========================================================================
    [Fact]
    [Trait("Category", "E2E")]
    public async Task E2E_PortMigration_ReconnectsToNewEndpoint_WithSingleMonitorLoop()
    {
        // Arrange: Simulator 1 cho COM3, Simulator 2 cho COM4
        var sim3 = new McuReferenceSimulator();
        var sim4 = new McuReferenceSimulator();

        var sessionManager = new SessionManager();
        var store = new RuntimeStateStore();
        store.InitializeProduct(_product);

        var monitor = new RuntimeMonitorService(
            new RuntimeTagReader(new FakeModbusClient(sim3, isConnected: true)),
            new DeviceHealthReader(new FakeModbusClient(sim3, isConnected: true)),
            stateStore: store)
        {
            PollingInterval = TimeSpan.FromMilliseconds(15)
        };

        // Detector động: Lúc đầu thấy COM3, sau khi rút chỉ thấy COM4
        bool com3Available = true;
        var detector = new DynamicDetector(() =>
        {
            return com3Available
                ? new[] { Endpoint("COM3") }
                : new[] { Endpoint("COM4") };
        });

        // Factory kết nối theo endpoint
        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (ep.Equals(Endpoint("COM3")))
            {
                if (!com3Available || sim3.Control.Faults.CableDisconnected)
                    return DeviceConnectionResult.Failed("COM3 unavailable");

                return await CreateSessionResultAsync(ep, sim3).ConfigureAwait(false);
            }

            if (ep.Equals(Endpoint("COM4")))
            {
                return await CreateSessionResultAsync(ep, sim4).ConfigureAwait(false);
            }

            return DeviceConnectionResult.Failed($"Unknown endpoint {ep}");
        });

        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingFast,
            deviceDetector: detector);

        int monitorAttachCount = 0;
        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
            {
                Interlocked.Increment(ref monitorAttachCount);
                await monitor.AttachSessionAsync(session, _product).ConfigureAwait(false);
            }
            else
            {
                await monitor.DetachSessionAsync().ConfigureAwait(false);
            }
        };

        monitor.OnTransportLost = ex => lifecycle.NotifyUnexpectedDisconnectAsync(ex);

        // Step 1: Kết nối ban đầu với COM3
        bool connected = await lifecycle.ConnectAsync(Endpoint("COM3"));
        Assert.True(connected);
        Assert.Equal(Endpoint("COM3"), lifecycle.CurrentEndpoint);

        await WaitForConditionAsync(
            () => store.GetAllTags().All(t => t.Quality == TagQuality.Good),
            timeoutMs: 2000,
            stepName: "Initial COM3 Good Quality");

        Assert.Equal(1, monitorAttachCount);

        // Step 2: Cổng COM3 biến mất, thiết bị cắm lại vào COM4
        com3Available = false;
        sim3.Control.Faults.CableDisconnected = true; // Ngắt luồng COM3 hiện tại

        // Đợi LifecycleManager phát hiện mất kết nối, probe và tự động di trú sang COM4
        await WaitForConditionAsync(
            () => lifecycle.CurrentState == ConnectionLifecycleState.Connected &&
                  lifecycle.CurrentEndpoint != null &&
                  lifecycle.CurrentEndpoint.Equals(Endpoint("COM4")) &&
                  store.CurrentSnapshot.ConnectionStatus == ConnectionStatus.Connected &&
                  store.GetAllTags().All(t => t.Quality == TagQuality.Good),
            timeoutMs: 4000,
            stepName: "Port Migration to COM4 & Good Quality Restored");

        // Assert: Di trú hoàn tất, endpoint hiện tại là COM4
        Assert.Equal(Endpoint("COM4"), lifecycle.CurrentEndpoint);
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);

        // Kiểm tra bất biến quan trọng: Duy nhất 1 vòng lặp polling đang chạy
        Assert.True(monitor.IsRunning);
        Assert.Equal(2, monitorAttachCount); // Lần 1 cho COM3, lần 2 cho COM4

        // Session COM3 cũ đã được giải phóng
        Assert.Equal(Endpoint("COM4"), sessionManager.CurrentSession!.Endpoint);

        await lifecycle.DisposeAsync();
        await monitor.StopAsync();
    }

    // =========================================================================
    // Helpers
    // =========================================================================
    private static async Task WaitForConditionAsync(
        Func<bool> condition,
        int timeoutMs,
        string stepName)
    {
        var start = DateTime.UtcNow;
        while (!condition())
        {
            if ((DateTime.UtcNow - start).TotalMilliseconds > timeoutMs)
            {
                throw new TimeoutException($"Timeout after {timeoutMs}ms waiting for step: {stepName}");
            }
            await Task.Delay(20).ConfigureAwait(false);
        }
    }

    private sealed class DynamicDetector : IDeviceDetector
    {
        private readonly Func<IReadOnlyList<DeviceEndpoint>> _provider;
        public DynamicDetector(Func<IReadOnlyList<DeviceEndpoint>> provider) => _provider = provider;
        public Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default)
            => Task.FromResult(_provider());
    }

    private sealed class ConfigurableConnectionFactory : IDeviceConnectionFactory
    {
        private readonly Func<DeviceEndpoint, CancellationToken, Task<DeviceConnectionResult>> _handler;
        public ConfigurableConnectionFactory(Func<DeviceEndpoint, CancellationToken, Task<DeviceConnectionResult>> handler)
            => _handler = handler;

        public Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
            => _handler(endpoint, cancellationToken);
    }
}
