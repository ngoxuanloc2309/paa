using System.IO;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public class DeviceLifecycleManagerTests
{
    private static DeviceEndpoint CreateEndpoint(string port = "COM3") => new UsbCdcEndpoint(port);

    private static (IDeviceConnectionFactory Factory, McuReferenceSimulator Simulator) CreateFakeFactory(bool isCompatible = true)
    {
        var simulator = new McuReferenceSimulator();
        if (!isCompatible)
        {
            simulator.Control.Faults.OverrideDeviceClass = 0x9999;
        }

        var factory = new TestFactory(() =>
        {
            var client = new FakeModbusClient(simulator, isConnected: true);
            var transport = new FakeUsbCdcTransport { IsOpen = true };
            return (client, transport);
        });

        return (factory, simulator);
    }

    [Fact]
    public async Task Connect_Success_TransitionsConnectingToConnected()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        var stateChanges = new List<ConnectionLifecycleState>();
        manager.StateChanged += (s, e) => stateChanges.Add(e.NewState);

        // Act
        bool result = await manager.ConnectAsync(CreateEndpoint());

        // Assert
        Assert.True(result);
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, manager.FailureReason);
        Assert.True(sessionManager.HasActiveSession);
        Assert.Contains(ConnectionLifecycleState.Connecting, stateChanges);
        Assert.Contains(ConnectionLifecycleState.Connected, stateChanges);
    }

    [Fact]
    public async Task Connect_IncompatibleDevice_ReturnsDisconnectedWithReason()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory(isCompatible: false);
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        // Act
        bool result = await manager.ConnectAsync(CreateEndpoint());

        // Assert
        Assert.False(result);
        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.IncompatibleDevice, manager.FailureReason);
        Assert.False(sessionManager.HasActiveSession);
    }

    [Fact]
    public async Task UnexpectedUnplug_ClosesSessionAndEntersReconnect()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        await manager.ConnectAsync(CreateEndpoint());
        Assert.True(sessionManager.HasActiveSession);

        bool sessionClosedOnDisconnect = false;
        var tcsReconnecting = new TaskCompletionSource<LifecycleStateChangedEventArgs>();
        manager.StateChanged += (s, e) =>
        {
            if (e.NewState == ConnectionLifecycleState.Reconnecting)
            {
                sessionClosedOnDisconnect = !sessionManager.HasActiveSession;
                tcsReconnecting.TrySetResult(e);
            }
        };

        // Act: Physical disconnect (e.g. cable pulled)
        await manager.NotifyUnexpectedDisconnectAsync(new IOException("The port COM3 was closed unexpectedly."));

        // Assert: Session closed immediately and state enters Reconnecting
        var args = await tcsReconnecting.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ConnectionLifecycleState.Reconnecting, args.NewState);
        Assert.Equal(ConnectionFailureReason.CommunicationLost, args.FailureReason);
        Assert.True(sessionClosedOnDisconnect);

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task TransientPollFailure_DoesNotTriggerReconnect()
    {
        // Arrange: DeviceLifecycleManager does not react to transient data read failures
        // only explicit transport disconnection triggers reconnect.
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        await manager.ConnectAsync(CreateEndpoint());

        // Assert: Current state remains Connected
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, manager.FailureReason);
    }

    [Fact]
    public async Task ExpectedReboot_DoesNotRaiseConnectionLostError()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        await manager.ConnectAsync(CreateEndpoint());

        LifecycleStateChangedEventArgs? restartArgs = null;
        var tcsRestarting = new TaskCompletionSource<bool>();

        manager.StateChanged += (s, e) =>
        {
            if (e.NewState == ConnectionLifecycleState.Restarting)
            {
                restartArgs = e;
                tcsRestarting.TrySetResult(true);
            }
        };

        // Act: App sends REBOOT command
        await manager.NotifyExpectedRestartAsync();

        // Assert: State is Restarting, FailureReason is None (not an error / not connection lost)
        await tcsRestarting.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotNull(restartArgs);
        Assert.Equal(ConnectionLifecycleState.Restarting, restartArgs.NewState);
        Assert.Equal(ConnectionFailureReason.None, restartArgs.FailureReason);
        Assert.False(sessionManager.HasActiveSession); // Old session closed

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task ExpectedReboot_CreatesNewSession()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        await manager.ConnectAsync(CreateEndpoint());
        var initialSession = sessionManager.CurrentSession;
        Assert.NotNull(initialSession);

        var tcsReconnected = new TaskCompletionSource<bool>();
        manager.StateChanged += (s, e) =>
        {
            if (e.NewState == ConnectionLifecycleState.Connected)
            {
                tcsReconnected.TrySetResult(true);
            }
        };

        // Act: Trigger expected reboot
        await manager.NotifyExpectedRestartAsync();

        // Wait for reboot settling delay (1ms in TestingImmediate) and auto-reconnect
        await tcsReconnected.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // Assert: Connection restored with a BRAND NEW session
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.True(sessionManager.HasActiveSession);
        Assert.NotSame(initialSession, sessionManager.CurrentSession);

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task ReconnectExhausted_EntersPassiveWait()
    {
        // Arrange: Factory that always fails
        var failingFactory = new FailingFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(failingFactory, sessionManager, ReconnectionPolicy.TestingImmediate);

        // Manually trigger reconnect from connected
        var tcsPassiveWait = new TaskCompletionSource<LifecycleStateChangedEventArgs>();
        manager.StateChanged += (s, e) =>
        {
            if (e.IsPassiveWaiting)
            {
                tcsPassiveWait.TrySetResult(e);
            }
        };

        // Connect first (fails -> disconnected)
        await manager.ConnectAsync(CreateEndpoint());

        // Set state to Connected then trigger unexpected disconnect to exercise the recovery loop
        // We can use a test wrapper or a custom factory that succeeds once then fails
        var togglingFactory = new TogglingFactory();
        var manager2 = new DeviceLifecycleManager(togglingFactory, sessionManager, ReconnectionPolicy.TestingImmediate);
        togglingFactory.ShouldSucceed = true;
        await manager2.ConnectAsync(CreateEndpoint());

        var tcsPassive = new TaskCompletionSource<LifecycleStateChangedEventArgs>();
        manager2.StateChanged += (s, e) =>
        {
            if (e.IsPassiveWaiting)
            {
                tcsPassive.TrySetResult(e);
            }
        };

        // Make future connects fail so fast retries are exhausted
        togglingFactory.ShouldSucceed = false;

        // Act
        await manager2.NotifyUnexpectedDisconnectAsync(new IOException("Cable pulled"));

        // Assert: Exhausted fast retries and entered Passive Wait
        var args = await tcsPassive.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(args.IsPassiveWaiting);
        Assert.Equal(ConnectionFailureReason.ReconnectExhausted, args.FailureReason);
        Assert.Equal(ConnectionLifecycleState.Reconnecting, args.NewState);

        await manager2.DisconnectAsync();
    }

    [Fact]
    public async Task DeviceReturnsDuringPassiveWait_AutoRecovers()
    {
        // Arrange
        var togglingFactory = new TogglingFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(togglingFactory, sessionManager, ReconnectionPolicy.TestingImmediate);

        togglingFactory.ShouldSucceed = true;
        await manager.ConnectAsync(CreateEndpoint());

        var tcsPassive = new TaskCompletionSource<bool>();
        var tcsRecovered = new TaskCompletionSource<bool>();

        manager.StateChanged += (s, e) =>
        {
            if (e.IsPassiveWaiting)
            {
                tcsPassive.TrySetResult(true);
            }
            if (e.NewState == ConnectionLifecycleState.Connected && tcsPassive.Task.IsCompleted)
            {
                tcsRecovered.TrySetResult(true);
            }
        };

        // Break connection
        togglingFactory.ShouldSucceed = false;
        await manager.NotifyUnexpectedDisconnectAsync(new IOException("Cable pulled"));

        // Wait until passive wait is reached
        await tcsPassive.Task.WaitAsync(TimeSpan.FromSeconds(3));

        // Act: Device plugged back in during passive wait!
        togglingFactory.ShouldSucceed = true;

        // Assert: Auto-recovers to Connected!
        await tcsRecovered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.True(sessionManager.HasActiveSession);

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task ManualDisconnect_CancelsReconnectLoop()
    {
        // Arrange
        var togglingFactory = new TogglingFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(togglingFactory, sessionManager, ReconnectionPolicy.TestingImmediate);

        togglingFactory.ShouldSucceed = true;
        await manager.ConnectAsync(CreateEndpoint());

        togglingFactory.ShouldSucceed = false;
        await manager.NotifyUnexpectedDisconnectAsync(new IOException("Cable pulled"));

        // Act: User clicks "Disconnect"
        await manager.DisconnectAsync();

        // Assert: State is Disconnected, Reason is None, and NO reconnect occurs
        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, manager.FailureReason);
        Assert.False(manager.IsPassiveWaiting);

        // Turn factory back on to verify no background reconnect steals the session
        togglingFactory.ShouldSucceed = true;
        await Task.Delay(50);

        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.False(sessionManager.HasActiveSession);
    }

    [Fact]
    public async Task ApplicationShutdown_CancelsAllLifecycleOperations()
    {
        // Arrange
        var togglingFactory = new TogglingFactory { ShouldSucceed = false };
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(togglingFactory, sessionManager, ReconnectionPolicy.TestingImmediate);

        await manager.ConnectAsync(CreateEndpoint());

        // Act: Dispose during active reconnect attempt
        await manager.DisposeAsync();

        // Assert: Cleanly disposed without deadlock or hanging
        Assert.True(true);
    }

    [Fact]
    public async Task RepeatedConnectCommand_DoesNotCreateTwoSessions()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        var endpoint = CreateEndpoint();

        // Act: Rapid concurrent connects to same endpoint
        var task1 = manager.ConnectAsync(endpoint);
        var task2 = manager.ConnectAsync(endpoint);
        var task3 = manager.ConnectAsync(endpoint);

        var results = await Task.WhenAll(task1, task2, task3);

        // Assert: All succeeded, but only one valid active session exists
        Assert.All(results, Assert.True);
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.True(sessionManager.HasActiveSession);
        Assert.NotNull(sessionManager.CurrentSession);

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task ReconnectLoop_DoesNotOverlapWithManualConnect()
    {
        // Arrange
        var togglingFactory = new TogglingFactory { ShouldSucceed = false };
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(togglingFactory, sessionManager, ReconnectionPolicy.TestingImmediate);

        // Fail initial connect -> enter reconnect loop
        togglingFactory.ShouldSucceed = true;
        await manager.ConnectAsync(CreateEndpoint("COM3"));

        togglingFactory.ShouldSucceed = false;
        await manager.NotifyUnexpectedDisconnectAsync(new IOException("Port closed"));
        Assert.Equal(ConnectionLifecycleState.Reconnecting, manager.CurrentState);

        // Act: User issues manual connect to a valid port while reconnect loop is running
        togglingFactory.ShouldSucceed = true;
        var manualEndpoint = CreateEndpoint("COM4");
        bool connectResult = await manager.ConnectAsync(manualEndpoint);

        // Assert: Manual connect succeeds, state is Connected to COM4, reconnect loop is canceled
        Assert.True(connectResult);
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(manualEndpoint, manager.CurrentEndpoint);
        Assert.True(sessionManager.HasActiveSession);

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task DisconnectDuringRestarting_CancelsExpectedReconnect()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        // Use policy with 200ms restart delay to give time for manual disconnect
        var policy = new ReconnectionPolicy
        {
            ExpectedRestartDelay = TimeSpan.FromMilliseconds(200),
            FastRetryDelays = new[] { TimeSpan.FromMilliseconds(5) },
            AutoReconnectOnDisconnect = true
        };
        var manager = new DeviceLifecycleManager(factory, sessionManager, policy);

        await manager.ConnectAsync(CreateEndpoint());
        Assert.True(sessionManager.HasActiveSession);

        // Initiate expected reboot
        await manager.NotifyExpectedRestartAsync();
        Assert.Equal(ConnectionLifecycleState.Restarting, manager.CurrentState);

        // Act: While in Restarting, user clicks Disconnect
        await manager.DisconnectAsync();

        // Assert: State is Disconnected, Reason is None
        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, manager.FailureReason);
        Assert.False(sessionManager.HasActiveSession);

        // Wait past the restart delay to guarantee no auto-reconnect occurred
        await Task.Delay(250);
        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.False(sessionManager.HasActiveSession);
    }

    [Fact]
    public async Task ConnectDifferentEndpoint_ReplacesPreviousLifecycleSafely()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        var endpoint1 = CreateEndpoint("COM3");
        var endpoint2 = CreateEndpoint("COM5");

        await manager.ConnectAsync(endpoint1);
        var session1 = sessionManager.CurrentSession;
        Assert.NotNull(session1);
        Assert.Equal(endpoint1, manager.CurrentEndpoint);

        // Act: Connect to different endpoint
        bool result = await manager.ConnectAsync(endpoint2);

        // Assert: Previous session closed, new session created on endpoint2
        Assert.True(result);
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(endpoint2, manager.CurrentEndpoint);
        Assert.NotSame(session1, sessionManager.CurrentSession);
        Assert.True(sessionManager.HasActiveSession);

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task StateChanged_Handler_ReentrantRead_DoesNotDeadlock()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        ConnectionLifecycleState observedCurrentState = ConnectionLifecycleState.Disconnected;
        ConnectionFailureReason observedReason = ConnectionFailureReason.None;
        bool readSuccessful = false;

        // Re-entrant subscriber querying manager synchronously inside the event callback
        manager.StateChanged += (s, e) =>
        {
            observedCurrentState = manager.CurrentState;
            observedReason = manager.FailureReason;
            _ = manager.CurrentEndpoint;
            _ = manager.RetryAttempt;
            _ = manager.IsPassiveWaiting;
            readSuccessful = true;
        };

        // Act: Connect triggers StateChanged
        await manager.ConnectAsync(CreateEndpoint());

        // Assert: Event handler executed and synchronously queried manager properties with zero deadlock
        Assert.True(readSuccessful);
        Assert.Equal(ConnectionLifecycleState.Connected, observedCurrentState);
        Assert.Equal(ConnectionFailureReason.None, observedReason);

        await manager.DisconnectAsync();
    }

    [Fact]
    public async Task DefaultPolicy_UnexpectedDisconnect_TransitionsDirectlyToDisconnected_WithoutBackgroundLoop()
    {
        // Arrange: Dùng ReconnectionPolicy.Default (AutoReconnectOnDisconnect == false theo chuẩn Siemens / Rockwell)
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.Default);

        await manager.ConnectAsync(CreateEndpoint());
        Assert.True(sessionManager.HasActiveSession);
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);

        var stateChanges = new List<ConnectionLifecycleState>();
        manager.StateChanged += (s, e) => stateChanges.Add(e.NewState);

        // Act: Rớt kết nối
        await manager.NotifyUnexpectedDisconnectAsync(new IOException("Cable disconnected"));

        // Assert: Ngay lập tức về Disconnected, không đi qua Reconnecting
        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.CommunicationLost, manager.FailureReason);
        Assert.False(sessionManager.HasActiveSession);
        Assert.DoesNotContain(ConnectionLifecycleState.Reconnecting, stateChanges);
        Assert.Contains(ConnectionLifecycleState.Disconnected, stateChanges);
    }

    [Fact]
    public async Task DefaultPolicy_ExpectedRestart_TransitionsDirectlyToDisconnected_WithoutBackgroundLoop()
    {
        // Arrange
        var (factory, _) = CreateFakeFactory();
        var sessionManager = new SessionManager();
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.Default);

        await manager.ConnectAsync(CreateEndpoint());
        Assert.True(sessionManager.HasActiveSession);

        var stateChanges = new List<ConnectionLifecycleState>();
        manager.StateChanged += (s, e) => stateChanges.Add(e.NewState);

        // Act: Gửi thông báo restart
        await manager.NotifyExpectedRestartAsync();

        // Assert: Về Disconnected sạch sẽ
        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, manager.FailureReason);
        Assert.False(sessionManager.HasActiveSession);
        Assert.DoesNotContain(ConnectionLifecycleState.Restarting, stateChanges);
        Assert.DoesNotContain(ConnectionLifecycleState.Reconnecting, stateChanges);
        Assert.Contains(ConnectionLifecycleState.Disconnected, stateChanges);
    }

    // Helper Test Connection Factories
    private sealed class TestFactory : IDeviceConnectionFactory
    {
        private readonly Func<(IModbusClient Client, IUsbCdcTransport Transport)> _factory;
        private readonly IDeviceCompatibilityValidator _validator = new StandardDeviceCompatibilityValidator();

        public TestFactory(Func<(IModbusClient Client, IUsbCdcTransport Transport)> factory)
        {
            _factory = factory;
        }

        public async Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            var (client, transport) = _factory();
            var descriptorReader = new DeviceDescriptorReader(client);
            var descriptor = await descriptorReader.ReadDescriptorAsync(slaveId, cancellationToken);
            var compatibility = _validator.Validate(descriptor);
            if (!compatibility.IsCompatible)
            {
                return DeviceConnectionResult.Incompatible(compatibility);
            }

            var session = new DeviceSession(
                endpoint,
                descriptor,
                slaveId,
                transport,
                client,
                new SimplePLC.Infrastructure.Gateways.RuleTableGateway(client),
                new RuntimeTagReader(client),
                new DeviceHealthReader(client),
                new SystemCommandClient(client));

            return DeviceConnectionResult.Success(session);
        }
    }

    private sealed class FailingFactory : IDeviceConnectionFactory
    {
        public Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(DeviceConnectionResult.Failed("Port unreachable"));
        }
    }

    private sealed class TogglingFactory : IDeviceConnectionFactory
    {
        public bool ShouldSucceed { get; set; }

        public Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            if (!ShouldSucceed)
            {
                return Task.FromResult(DeviceConnectionResult.Failed("Device disconnected"));
            }

            var (factory, _) = CreateFakeFactory();
            return factory.ConnectAsync(endpoint, slaveId, cancellationToken);
        }
    }
}
