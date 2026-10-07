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
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Phase F3 — Lifecycle / Recovery Stress:
/// Kiểm tra độ bền bỉ của máy trạng thái kết nối, điều phối phiên và vòng lặp giám sát:
/// - F3_01: Lặp chu kỳ Connect -> Unplug -> Reconnect x 100 lần (không rò rỉ session, không deadlock).
/// - F3_02: Rút cáp khi đang Polling -> Monitor dừng sạch -> Re-plug -> Tự phục hồi, duy trì đúng 1 monitor.
/// - F3_03: Rút cáp khi đang Deploy -> Coordinator giải phóng Exclusive lease, không gây deadlock.
/// - F3_04: Người dùng Disconnect khi đang Handshake -> Hủy tác vụ an toàn, không có session mồ côi.
/// - F3_05: Port Migration liên tiếp COM3 -> COM4 -> COM5 -> Duy trì đúng 1 session và 1 monitor.
/// - F3_06: Manual Connect cạnh tranh với Auto-Reconnect -> Không đua lệnh, chỉ sinh 1 session duy nhất.
/// - F3_07: Xuất hiện nhiều thiết bị tương thích cùng lúc -> Chặn tự động kết nối bừa bãi.
/// </summary>
public sealed class LifecycleRecoveryStressTests
{
    private static DeviceEndpoint Endpoint(string port = "COM3") => new UsbCdcEndpoint(port);

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

    private sealed class StressDeviceFactory : IDeviceConnectionFactory
    {
        private readonly McuReferenceSimulator _simulator;
        public bool ShouldSucceed { get; set; } = true;
        public int ConnectDelayMs { get; set; } = 0;
        public int SessionsCreatedCount { get; private set; }

        public StressDeviceFactory(McuReferenceSimulator simulator)
        {
            _simulator = simulator;
        }

        public async Task<DeviceConnectionResult> ConnectAsync(
            DeviceEndpoint endpoint,
            byte slaveId = 1,
            CancellationToken cancellationToken = default)
        {
            if (ConnectDelayMs > 0)
            {
                await Task.Delay(ConnectDelayMs, cancellationToken).ConfigureAwait(false);
            }

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

            SessionsCreatedCount++;
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
    public async Task F3_01_Connect_Unplug_Reconnect_Loop_100Cycles()
    {
        var sim = new McuReferenceSimulator();
        var factory = new StressDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        var endpoint = Endpoint("COM3");

        // Act: Thực hiện 100 chu kỳ Connect -> Unplug -> Reconnect
        for (int cycle = 1; cycle <= 100; cycle++)
        {
            // 1. Kết nối
            factory.ShouldSucceed = true;
            bool connected = await lifecycle.ConnectAsync(endpoint);
            Assert.True(connected, $"Cycle {cycle}: Connect must succeed");
            Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);
            Assert.True(sessionManager.HasActiveSession);

            // 2. Mô phỏng rút cáp / ngắt kết nối
            await lifecycle.NotifyUnexpectedDisconnectAsync(new IOException("Simulated cable pull"));

            // Đợi reconnect hoàn tất qua TestingImmediate
            bool reconnected = await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));
            Assert.True(reconnected, $"Cycle {cycle}: Fast reconnect must succeed");
            Assert.True(sessionManager.HasActiveSession);
        }

        // Bất biến F3: Sau 100 chu kỳ, luôn chỉ có đúng 1 session active, không rò rỉ session
        Assert.True(sessionManager.HasActiveSession);
        Assert.NotNull(sessionManager.CurrentSession);

        await lifecycle.DisconnectAsync();
        Assert.False(sessionManager.HasActiveSession);
    }

    [Fact]
    public async Task F3_02_Unplug_During_ActivePolling_TerminatesLoop_RecoversOnReplug()
    {
        var sim = new McuReferenceSimulator();
        var factory = new StressDeviceFactory(sim);
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

        // Khi transport lost -> báo cho lifecycle manager
        monitor.OnTransportLost = async ex =>
        {
            await lifecycle.NotifyUnexpectedDisconnectAsync(ex);
        };

        // Kết nối và gắn session vào monitor
        bool connected = await lifecycle.ConnectAsync(Endpoint("COM3"));
        Assert.True(connected);
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);

        // Chờ 2 chu kỳ poll đầu
        await Task.Delay(60);
        Assert.True(monitor.IsRunning);
        Assert.Equal(ConnectionStatus.Connected, store.CurrentSnapshot.ConnectionStatus);

        // Mô phỏng cáp bị rút vật lý
        sim.Control.Faults.CableDisconnected = true;
        factory.ShouldSucceed = false;

        // Chờ monitor nhận diện lỗi và dừng vòng lặp (cho phép tối đa 300ms nếu CPU đang tải nặng)
        for (int retry = 0; retry < 15 && monitor.IsRunning; retry++)
        {
            await Task.Delay(20);
        }
        Assert.False(monitor.IsRunning, "Monitor loop must have terminated upon cable disconnect");

        // Cắm lại cáp và phục hồi
        sim.Control.Faults.CableDisconnected = false;
        factory.ShouldSucceed = true;

        bool reconnected = await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));
        Assert.True(reconnected);

        // Gắn lại session mới phục hồi vào monitor
        await monitor.AttachSessionAsync(sessionManager.CurrentSession!, product);
        await Task.Delay(60);

        // Bất biến: Duy trì đúng 1 monitor loop đang chạy, trạng thái Connected, Tags phục hồi Good
        Assert.True(monitor.IsRunning);
        var recoveredSnapshot = store.CurrentSnapshot;
        Assert.Equal(ConnectionStatus.Connected, recoveredSnapshot.ConnectionStatus);
        Assert.All(recoveredSnapshot.Tags, t => Assert.Equal(TagQuality.Good, t.Quality));

        await monitor.StopAsync();
        await lifecycle.DisconnectAsync();
    }

    [Fact]
    public async Task F3_03_Unplug_During_Deploy_ReleasesCoordinatorLease_NoDeadlock()
    {
        var sim = new McuReferenceSimulator();
        var client = new FakeModbusClient(sim, isConnected: true);
        var coordinator = new DeviceOperationCoordinator();
        var writer = new RuleTableWriter(client);

        var rules = new List<RuleRecordDto>
        {
            new() { ThresholdLo = 1, Enabled = true, TriggerType = SPLC_TriggerType.ON_RISE, ActionType = SPLC_ActionType.SET_TAG }
        };

        // Bắt đầu deploy với exclusive lease, nhưng giả lập ngắt kết nối
        sim.Control.Faults.CableDisconnected = true;

        var deployTask = Task.Run(async () =>
        {
            await using var lease = await coordinator.AcquireExclusiveAsync(DeviceOperation.DeployRules);
            return await writer.DeployRulesAsync(1, rules);
        });

        // Deploy phải thất bại vì cáp bị rút
        await Assert.ThrowsAsync<IOException>(() => deployTask);

        // Bất biến F3: Coordinator lease phải được giải phóng hoàn toàn, không deadlock
        sim.Control.Faults.CableDisconnected = false;
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var nextLease = await coordinator.AcquireExclusiveAsync(DeviceOperation.SystemCommand, cts.Token);
        Assert.NotNull(nextLease);
        await nextLease.DisposeAsync();
    }

    [Fact]
    public async Task F3_04_ManualDisconnect_During_InFlight_Connect_CancelsCleanly()
    {
        var sim = new McuReferenceSimulator();
        var factory = new StressDeviceFactory(sim) { ConnectDelayMs = 200 };
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        using var cts = new CancellationTokenSource();
        var connectTask = lifecycle.ConnectAsync(Endpoint("COM3"), slaveId: 1, ct: cts.Token);

        // Chờ tác vụ bắt đầu chạy trong background
        await Task.Delay(30);

        // Người dùng hủy kết nối ngay khi đang handshake
        cts.Cancel();

        try
        {
            await connectTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Bất biến: Trạng thái Disconnected hoặc None, không có session active
        Assert.False(sessionManager.HasActiveSession);
    }

    [Fact]
    public async Task F3_05_PortMigration_Sequential_MaintainsSingleMonitor()
    {
        var sim = new McuReferenceSimulator();
        string currentDetectedPort = "COM3";

        var factory = new StressDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingImmediate,
            timeProvider: TimeProvider.System,
            candidateDiscovery: ct => Task.FromResult<IReadOnlyList<DeviceEndpoint>>(new[] { Endpoint(currentDetectedPort) }));

        // 1. Kết nối COM3
        bool ok = await lifecycle.ConnectAsync(Endpoint("COM3"));
        Assert.True(ok);
        Assert.Equal("COM3", ((UsbCdcEndpoint)lifecycle.CurrentEndpoint!).PortName);

        // 2. Thiết bị đổi sang COM4
        currentDetectedPort = "COM4";
        await lifecycle.NotifyUnexpectedDisconnectAsync(new IOException("COM3 closed"));

        bool reconnected4 = await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));
        Assert.True(reconnected4);
        Assert.Equal("COM4", ((UsbCdcEndpoint)lifecycle.CurrentEndpoint!).PortName);

        // 3. Thiết bị tiếp tục đổi sang COM5
        currentDetectedPort = "COM5";
        await lifecycle.NotifyUnexpectedDisconnectAsync(new IOException("COM4 closed"));

        bool reconnected5 = await WaitForStateAsync(lifecycle, ConnectionLifecycleState.Connected, TimeSpan.FromSeconds(2));
        Assert.True(reconnected5);
        Assert.Equal("COM5", ((UsbCdcEndpoint)lifecycle.CurrentEndpoint!).PortName);

        // Bất biến: SessionManager chỉ giữ đúng 1 active session duy nhất cho COM5
        Assert.True(sessionManager.HasActiveSession);
        Assert.Equal("COM5", ((UsbCdcEndpoint)sessionManager.CurrentSession!.Endpoint).PortName);

        await lifecycle.DisconnectAsync();
    }

    [Fact]
    public async Task F3_06_ManualConnect_DoesNotRaceWith_AutoReconnect()
    {
        var sim = new McuReferenceSimulator();
        var factory = new StressDeviceFactory(sim);
        var sessionManager = new SessionManager();
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.Default);

        // Kết nối COM3 ban đầu
        await lifecycle.ConnectAsync(Endpoint("COM3"));

        // Rút cáp để đưa lifecycle vào trạng thái đang chờ auto-reconnect
        factory.ShouldSucceed = false;
        await lifecycle.NotifyUnexpectedDisconnectAsync(new IOException("Cable pulled"));

        // Khi đang chờ auto-reconnect, người dùng bấm nút Connect thủ công tới COM8
        factory.ShouldSucceed = true;
        bool manualResult = await lifecycle.ConnectAsync(Endpoint("COM8"));

        Assert.True(manualResult);
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);
        Assert.Equal("COM8", ((UsbCdcEndpoint)lifecycle.CurrentEndpoint!).PortName);

        // Bất biến: Chỉ có 1 active session duy nhất thuộc COM8
        Assert.True(sessionManager.HasActiveSession);
        Assert.Equal("COM8", ((UsbCdcEndpoint)sessionManager.CurrentSession!.Endpoint).PortName);

        await lifecycle.DisconnectAsync();
    }

    [Fact]
    public async Task F3_07_AmbiguousDevices_MultipleCandidates_HaltsAutoReconnect()
    {
        var sim = new McuReferenceSimulator();
        var factory = new StressDeviceFactory(sim);
        var sessionManager = new SessionManager();

        // 2 ứng viên cùng xuất hiện: COM7 và COM8
        var candidates = new List<DeviceEndpoint> { Endpoint("COM7"), Endpoint("COM8") };

        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingImmediate,
            timeProvider: TimeProvider.System,
            candidateDiscovery: ct => Task.FromResult<IReadOnlyList<DeviceEndpoint>>(candidates));

        // Kết nối COM3 ban đầu
        await lifecycle.ConnectAsync(Endpoint("COM3"));

        // COM3 mất kết nối
        await lifecycle.NotifyUnexpectedDisconnectAsync(new IOException("COM3 disconnected"));

        // Chờ auto-reconnect quét candidate
        await Task.Delay(100);

        // Bất biến: Không được tự tiện kết nối bừa bãi khi có >= 2 thiết bị tương thích
        // Phải dừng ở trạng thái Disconnected / DeviceNotFound hoặc chờ người dùng chọn
        Assert.False(sessionManager.HasActiveSession);
        Assert.True(lifecycle.CurrentState == ConnectionLifecycleState.Disconnected ||
                    lifecycle.CurrentState == ConnectionLifecycleState.Reconnecting);
    }
}
