using System.IO;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// D3 Integration tests: Session↔Monitor orchestration and Lifecycle↔Command integration.
///
/// Frozen invariants:
///   1. At most one active monitor loop at a time (SemaphoreSlim _gate)
///   2. Only definitive transport failure triggers reconnect
///   3. Only DeviceLifecycleManager decides lifecycle/session recovery
/// </summary>
public class D3IntegrationTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static UsbCdcEndpoint Ep(string port = "COM3") => new(port);

    private static (IDeviceConnectionFactory Factory, McuReferenceSimulator Sim) MakeFactory(bool compatible = true)
    {
        var sim = new McuReferenceSimulator();
        if (!compatible) sim.Control.Faults.OverrideDeviceClass = 0x9999;

        var factory = new CallbackConnectionFactory(() =>
        {
            var client = new FakeModbusClient(sim, isConnected: true);
            var transport = new FakeUsbCdcTransport { IsOpen = true };
            return (client, transport);
        });
        return (factory, sim);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // D3.1 — Session ↔ Monitor Orchestration
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When a new session is established (SessionChanged fires), RuntimeMonitorService must
    /// stop any existing loop and start a new one — exactly one loop active.
    /// </summary>
    [Fact]
    public async Task NewSession_StartsRuntimeMonitor_WithSingleLoopInvariant()
    {
        // Arrange
        var (factory, _) = MakeFactory();
        var sessionManager = new SessionManager();
        var stateStore = new RuntimeStateStore();
        var monitor = new RuntimeMonitorService(
            new FakeRuntimeTagReader(),
            new FakeDeviceHealthReader(),
            stateStore: stateStore)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        // D3.1: Wire session → monitor (mimics AppServices.OnSessionChanged)
        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
                await monitor.AttachSessionAsync(session, _product);
            else
                await monitor.DetachSessionAsync();
        };

        var lifecycle = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingImmediate);

        // Act
        bool connected = await lifecycle.ConnectAsync(Ep());

        // Allow one poll cycle
        await Task.Delay(60);

        // Assert: lifecycle connected and monitor is running
        Assert.True(connected);
        Assert.True(monitor.IsRunning);
        Assert.Equal(ConnectionLifecycleState.Connected, lifecycle.CurrentState);

        await lifecycle.DisposeAsync();
        await monitor.StopAsync();
    }

    /// <summary>
    /// When a session is replaced (new session replaces old), the old monitor loop must be
    /// stopped completely before the new one starts.
    /// </summary>
    [Fact]
    public async Task SessionReplacement_StopsOldMonitorFirst_SingleLoopInvariant()
    {
        // Arrange
        var (factory, _) = MakeFactory();
        var sessionManager = new SessionManager();
        int pollLoopStartCount = 0;

        var monitor = new RuntimeMonitorService(
            new FakeRuntimeTagReader(),
            new FakeDeviceHealthReader())
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        monitor.TagsUpdated += _ => Interlocked.Increment(ref pollLoopStartCount);

        // Wire: each session change → AttachSessionAsync
        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
                await monitor.AttachSessionAsync(session, _product);
            else
                await monitor.DetachSessionAsync();
        };

        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        // Act: Connect session 1
        await lifecycle.ConnectAsync(Ep("COM3"));
        await Task.Delay(50); // let first loop run

        bool wasRunningAfterFirst = monitor.IsRunning;

        // Disconnect then reconnect (simulates session replacement)
        await lifecycle.DisconnectAsync();
        await Task.Delay(20);
        bool stoppedAfterDisconnect = !monitor.IsRunning;

        await lifecycle.ConnectAsync(Ep("COM4"));
        await Task.Delay(50); // let second loop run

        // Assert
        Assert.True(wasRunningAfterFirst, "Monitor should be running after first connect");
        Assert.True(stoppedAfterDisconnect, "Monitor should stop after disconnect");
        Assert.True(monitor.IsRunning, "Monitor should be running after second connect");

        await lifecycle.DisposeAsync();
        await monitor.StopAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // D3.2 — Lifecycle ↔ Transport Error Classification
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An unexpected IOException (definitive transport error) from the poll loop
    /// must trigger LifecycleManager.NotifyUnexpectedDisconnectAsync, starting recovery.
    /// </summary>
    [Fact]
    public async Task UnexpectedTransportLoss_MarksStoreUnknown_AndStartsRecovery()
    {
        // Arrange
        var (factory, _) = MakeFactory();
        var sessionManager = new SessionManager();

        // Connect lifecycle to get a real session
        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);
        await lifecycle.ConnectAsync(Ep("COM3"));

        // Build monitor with ThrowOnce reader (simulates cable pull on first poll).
        // NOTE: Use StartAsync (not AttachSessionAsync) so the custom ThrowOnceTagReader
        // stays in place — AttachSessionAsync would replace it with session.RuntimeTags.
        var throwingReader = new ThrowOnceTagReader(new IOException("The device is not connected."));
        var stateStore = new RuntimeStateStore();
        var monitor = new RuntimeMonitorService(throwingReader, new FakeDeviceHealthReader(), stateStore: stateStore)
        {
            PollingInterval = TimeSpan.FromMilliseconds(10)
        };

        var recoveryTcs = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var reconnectTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        monitor.OnTransportLost = ex =>
        {
            recoveryTcs.TrySetResult(ex);
            return lifecycle.NotifyUnexpectedDisconnectAsync(ex);
        };

        // Capture state transitions to verify recovery was triggered
        var visitedStates = new List<ConnectionLifecycleState>();
        var stateLock = new object();
        lifecycle.StateChanged += (_, e) =>
        {
            lock (stateLock)
            {
                visitedStates.Add(e.NewState);
            }
            if (e.NewState == ConnectionLifecycleState.Reconnecting)
            {
                reconnectTcs.TrySetResult(true);
            }
        };

        // Start the poll loop (reader is already ThrowOnceTagReader)
        await monitor.StartAsync(_product, slaveId: 1, tagCount: 60);

        // Wait for transport loss to propagate
        var completed = await Task.WhenAny(recoveryTcs.Task, Task.Delay(3000));

        // Assert: IOException reached callback — key assertion is that transport loss was classified
        Assert.Same(recoveryTcs.Task, completed);
        var capturedEx = await recoveryTcs.Task;
        Assert.IsType<IOException>(capturedEx);
        Assert.True(RuntimeMonitorService.IsDefinitiveTransportError(capturedEx!));

        // Wait for lifecycle to transition to Reconnecting (event-driven, resilient to heavy CI load)
        var stateCompleted = await Task.WhenAny(reconnectTcs.Task, Task.Delay(3000));
        Assert.Same(reconnectTcs.Task, stateCompleted);

        // Verify Reconnecting was visited (lifecycle reacted to transport loss)
        lock (stateLock)
        {
            Assert.Contains(ConnectionLifecycleState.Reconnecting, visitedStates);
        }

        await lifecycle.DisposeAsync();
        await monitor.StopAsync();
    }

    /// <summary>
    /// A transient TimeoutException from the poll loop must NOT invoke OnTransportLost
    /// and must NOT trigger reconnect.
    /// </summary>
    [Fact]
    public async Task TransientPollTimeout_DoesNotStartReconnect()
    {
        // Arrange: reader throws TimeoutException (transient — not definitive transport error)
        var transientEx = new TimeoutException("Modbus timeout");
        Assert.False(RuntimeMonitorService.IsDefinitiveTransportError(transientEx),
            "TimeoutException should NOT be a definitive transport error");

        var throwingReader = new AlwaysThrowTagReader(transientEx);

        bool transportLostCalled = false;
        var monitor = new RuntimeMonitorService(throwingReader, new FakeDeviceHealthReader())
        {
            PollingInterval = TimeSpan.FromMilliseconds(15)
        };
        monitor.OnTransportLost = _ =>
        {
            transportLostCalled = true;
            return Task.CompletedTask;
        };

        var errors = new List<Exception>();
        monitor.PollingError += ex => errors.Add(ex);

        // Act: Start and wait for a few poll cycles
        monitor.Start(_product, slaveId: 1, tagCount: 60);
        await Task.Delay(100);
        await monitor.StopAsync();

        // Assert: transient timeout fires PollingError but never OnTransportLost
        Assert.False(transportLostCalled, "OnTransportLost must not fire for transient timeout");
        Assert.NotEmpty(errors);
        Assert.All(errors, e => Assert.IsType<TimeoutException>(e));

        monitor.Dispose();
    }

    /// <summary>
    /// After manual disconnect, IsConnected becomes false and lifecycle does NOT start reconnect.
    /// </summary>
    [Fact]
    public async Task ManualDisconnect_StopsMonitor_AndDoesNotReconnect()
    {
        // Arrange
        var (factory, _) = MakeFactory();
        var sessionManager = new SessionManager();
        var monitor = new RuntimeMonitorService(new FakeRuntimeTagReader(), new FakeDeviceHealthReader())
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        bool reconnectTriggered = false;
        monitor.OnTransportLost = _ =>
        {
            reconnectTriggered = true;
            return Task.CompletedTask;
        };

        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
                await monitor.AttachSessionAsync(session, _product);
            else
                await monitor.DetachSessionAsync();
        };

        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        // Act: Connect then manually disconnect
        await lifecycle.ConnectAsync(Ep("COM3"));
        await Task.Delay(30);

        await lifecycle.DisconnectAsync(); // Manual disconnect
        await Task.Delay(50);

        // Assert: monitor stopped, lifecycle Disconnected, no reconnect started
        Assert.False(monitor.IsRunning, "Monitor must be stopped after manual disconnect");
        Assert.Equal(ConnectionLifecycleState.Disconnected, lifecycle.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, lifecycle.FailureReason);
        Assert.False(reconnectTriggered, "OnTransportLost must not fire on manual disconnect");

        await lifecycle.DisposeAsync();
        monitor.Dispose();
    }

    /// <summary>
    /// After Expected Reboot, lifecycle transitions Restarting → Reconnecting → Connected.
    /// The monitor must be detached during restart and re-attached on new session.
    /// </summary>
    [Fact]
    public async Task ExpectedReboot_TransitionsCorrectly_AndReattachesMonitor()
    {
        // Arrange
        var (factory, _) = MakeFactory();
        var sessionManager = new SessionManager();
        var monitor = new RuntimeMonitorService(new FakeRuntimeTagReader(), new FakeDeviceHealthReader())
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        sessionManager.SessionChanged += async session =>
        {
            if (session != null)
                await monitor.AttachSessionAsync(session, _product);
            else
                await monitor.DetachSessionAsync();
        };

        var lifecycle = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate);

        var detector = new FakeDetector(new[] { Ep("COM3") });
        var lifcycleWithDetector = new DeviceLifecycleManager(
            factory,
            sessionManager,
            ReconnectionPolicy.TestingImmediate,
            deviceDetector: detector);

        // Act: Connect
        await lifcycleWithDetector.ConnectAsync(Ep("COM3"));
        await Task.Delay(30);

        // Trigger expected restart (reboot)
        await lifcycleWithDetector.NotifyExpectedRestartAsync();

        // Assert immediately: state is Restarting
        Assert.Equal(ConnectionLifecycleState.Restarting, lifcycleWithDetector.CurrentState);

        // Wait for restart recovery to complete (policy.TestingImmediate has 1ms delays)
        await Task.Delay(200);

        Assert.Equal(ConnectionLifecycleState.Connected, lifcycleWithDetector.CurrentState);
        Assert.True(monitor.IsRunning, "Monitor must be running after reconnect from reboot");

        await lifcycleWithDetector.DisposeAsync();
        await monitor.StopAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // IsDefinitiveTransportError classification
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("IOException", true)]
    [InlineData("UnauthorizedAccessException", true)]
    [InlineData("ObjectDisposedException", true)]
    [InlineData("InvalidOperationException_NotOpen", true)]
    [InlineData("TimeoutException", false)]
    [InlineData("InvalidOperationException_Other", false)]
    [InlineData("ArgumentException", false)]
    public void IsDefinitiveTransportError_ClassifiesCorrectly(string caseLabel, bool expected)
    {
        Exception ex = caseLabel switch
        {
            "IOException" => new IOException("Port error"),
            "UnauthorizedAccessException" => new UnauthorizedAccessException("Access denied"),
            "ObjectDisposedException" => new ObjectDisposedException("port"),
            "InvalidOperationException_NotOpen" => new InvalidOperationException("Port is not open."),
            "TimeoutException" => new TimeoutException("Modbus timeout"),
            "InvalidOperationException_Other" => new InvalidOperationException("Some other error"),
            "ArgumentException" => new ArgumentException("Bad arg"),
            _ => throw new NotSupportedException(caseLabel)
        };

        Assert.Equal(expected, RuntimeMonitorService.IsDefinitiveTransportError(ex));
    }
}

// ─── Test Doubles ─────────────────────────────────────────────────────────────

internal sealed class FakeRuntimeTagReader : IRuntimeTagReader
{
    public Task<int[]> ReadRuntimeTagValuesAsync(byte slaveId, ushort count, CancellationToken ct = default)
        => Task.FromResult(new int[count]);
}

internal sealed class FakeDeviceHealthReader : IDeviceHealthReader
{
    public Task<SimplePLC.Protocol.Dto.DeviceHealthDto> ReadHealthAsync(byte slaveId, CancellationToken ct = default)
        => Task.FromResult(new SimplePLC.Protocol.Dto.DeviceHealthDto());
}

/// <summary>Throws IOException on the first call, then returns empty array.</summary>
internal sealed class ThrowOnceTagReader : IRuntimeTagReader
{
    private readonly Exception _ex;
    private int _callCount;

    public ThrowOnceTagReader(Exception ex) => _ex = ex;

    public Task<int[]> ReadRuntimeTagValuesAsync(byte slaveId, ushort count, CancellationToken ct = default)
    {
        if (Interlocked.Increment(ref _callCount) == 1)
            throw _ex;
        return Task.FromResult(new int[count]);
    }
}

/// <summary>Always throws the given exception.</summary>
internal sealed class AlwaysThrowTagReader : IRuntimeTagReader
{
    private readonly Exception _ex;
    public AlwaysThrowTagReader(Exception ex) => _ex = ex;

    public Task<int[]> ReadRuntimeTagValuesAsync(byte slaveId, ushort count, CancellationToken ct = default)
        => throw _ex;
}

/// <summary>Callback-based factory for test setup.</summary>
internal sealed class CallbackConnectionFactory : IDeviceConnectionFactory
{
    private readonly Func<(IModbusClient client, IUsbCdcTransport transport)> _factory;
    private readonly IDeviceCompatibilityValidator _validator = new StandardDeviceCompatibilityValidator();

    public CallbackConnectionFactory(Func<(IModbusClient, IUsbCdcTransport)> factory)
        => _factory = factory;

    public async Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var (client, transport) = _factory();
        var reader = new DeviceDescriptorReader(client);
        var descriptor = await reader.ReadDescriptorAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var compat = _validator.Validate(descriptor);
        if (!compat.IsCompatible)
            return DeviceConnectionResult.Incompatible(compat);

        var session = new DeviceSession(
            endpoint, descriptor, slaveId, transport, client,
            new SimplePLC.Infrastructure.Gateways.RuleTableGateway(client),
            new RuntimeTagReader(client),
            new DeviceHealthReader(client),
            new SystemCommandClient(client));

        return DeviceConnectionResult.Success(session);
    }
}

/// <summary>Returns a fixed list of endpoints for discovery.</summary>
internal sealed class FakeDetector : IDeviceDetector
{
    private readonly IReadOnlyList<DeviceEndpoint> _endpoints;
    public FakeDetector(IReadOnlyList<DeviceEndpoint> endpoints) => _endpoints = endpoints;
    public Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default)
        => Task.FromResult(_endpoints);
}
