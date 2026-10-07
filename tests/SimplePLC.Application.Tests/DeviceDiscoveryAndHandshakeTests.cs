using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Sessions;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Application.Tests;

public sealed class DeviceDiscoveryAndHandshakeTests
{
    private static DeviceEndpoint Endpoint(string port) => new UsbCdcEndpoint(port);

    private static (IModbusClient Client, IUsbCdcTransport Transport, McuReferenceSimulator Simulator) CreateSimulatorPair(bool compatible = true)
    {
        var sim = new McuReferenceSimulator();
        if (!compatible)
        {
            sim.Control.Faults.OverrideDeviceClass = 0x9999;
        }
        var transport = new FakeUsbCdcTransport { IsOpen = true };
        var client = new FakeModbusClient(sim, isConnected: true);
        return (client, transport, sim);
    }

    private static async Task<DeviceConnectionResult> CreateSuccessfulConnectionAsync(DeviceEndpoint endpoint, bool compatible = true)
    {
        var (client, transport, _) = CreateSimulatorPair(compatible);
        var validator = new StandardDeviceCompatibilityValidator();
        var reader = new DeviceDescriptorReader(client);
        var descriptor = await reader.ReadDescriptorAsync(1);
        var comp = validator.Validate(descriptor);
        if (!comp.IsCompatible)
        {
            await transport.CloseAsync();
            await transport.DisposeAsync();
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

    // 1. PreviousEndpoint_IsProbedFirst
    [Fact]
    public async Task PreviousEndpoint_IsProbedFirst()
    {
        var probedEndpoints = new List<DeviceEndpoint>();
        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            lock (probedEndpoints) probedEndpoints.Add(ep);
            return await CreateSuccessfulConnectionAsync(ep);
        });

        var sessionManager = new SessionManager();
        var detector = new FakeDetector(new[] { Endpoint("COM4"), Endpoint("COM3") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        // Ban đầu kết nối với COM3
        await manager.ConnectAsync(Endpoint("COM3"));
        lock (probedEndpoints) probedEndpoints.Clear();

        // Kích hoạt mất kết nối đột ngột
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(150);

        // Assert: COM3 (previous) phải được probe trước tiên dù detector trả về COM4 trước
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(Endpoint("COM3"), manager.CurrentEndpoint);
        lock (probedEndpoints)
        {
            Assert.NotEmpty(probedEndpoints);
            Assert.Equal(Endpoint("COM3"), probedEndpoints[0]);
            // Vì COM3 thành công ngay lập tức nên COM4 không cần probe
            Assert.DoesNotContain(Endpoint("COM4"), probedEndpoints);
        }

        await manager.DisconnectAsync();
    }

    // 2. PreviousEndpointFails_NextCompatibleCandidateIsAccepted
    [Fact]
    public async Task PreviousEndpointFails_NextCompatibleCandidateIsAccepted()
    {
        var probedEndpoints = new List<DeviceEndpoint>();
        bool isInitialConnect = true;
        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (isInitialConnect && ep.Equals(Endpoint("COM3")))
            {
                isInitialConnect = false;
                return await CreateSuccessfulConnectionAsync(ep);
            }

            lock (probedEndpoints) probedEndpoints.Add(ep);
            if (ep.Equals(Endpoint("COM3")))
            {
                return DeviceConnectionResult.Failed("COM3 missing or busy");
            }
            return await CreateSuccessfulConnectionAsync(ep);
        });

        var sessionManager = new SessionManager();
        var detector = new FakeDetector(new[] { Endpoint("COM3"), Endpoint("COM4") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        lock (probedEndpoints) probedEndpoints.Clear();

        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(150);

        // Assert: COM3 được probe trước nhưng fail, sau đó COM4 được probe và chấp nhận
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(Endpoint("COM4"), manager.CurrentEndpoint);
        lock (probedEndpoints)
        {
            Assert.Equal(Endpoint("COM3"), probedEndpoints[0]);
            Assert.Contains(Endpoint("COM4"), probedEndpoints);
        }

        await manager.DisconnectAsync();
    }

    // 3. IncompatibleCandidate_IsDisposedAndSkipped
    [Fact]
    public async Task IncompatibleCandidate_IsDisposedAndSkipped()
    {
        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (ep.Equals(Endpoint("COM4")))
            {
                // Incompatible device
                return await CreateSuccessfulConnectionAsync(ep, compatible: false);
            }
            return await CreateSuccessfulConnectionAsync(ep, compatible: true);
        });

        var sessionManager = new SessionManager();
        // Previous endpoint COM3 đã biến mất, chỉ còn COM4 (incompatible) và COM5 (compatible)
        var detector = new FakeDetector(new[] { Endpoint("COM4"), Endpoint("COM5") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(150);

        // COM4 bị skip, COM5 được chấp nhận
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(Endpoint("COM5"), manager.CurrentEndpoint);

        await manager.DisconnectAsync();
    }

    // 4. ProbeTimeout_DisposesCandidateTransport
    [Fact]
    public async Task ProbeTimeout_DisposesCandidateTransport()
    {
        bool transportDisposed = false;
        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (ep.Equals(Endpoint("COM4")))
            {
                // Mô phỏng port lạ bị treo, vượt quá ProbeTimeout
                try
                {
                    await Task.Delay(500, ct);
                }
                catch (OperationCanceledException)
                {
                    transportDisposed = true;
                    throw;
                }
                return DeviceConnectionResult.Failed("Should have timed out");
            }

            return await CreateSuccessfulConnectionAsync(ep);
        });

        var sessionManager = new SessionManager();
        var policy = new ReconnectionPolicy
        {
            FastRetryDelays = new[] { TimeSpan.FromMilliseconds(5) },
            ProbeTimeout = TimeSpan.FromMilliseconds(30),
            PassiveWaitInterval = TimeSpan.FromMilliseconds(20),
            AutoReconnectOnDisconnect = true
        };

        var detector = new FakeDetector(new[] { Endpoint("COM4"), Endpoint("COM5") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, policy, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(200);

        // Assert: COM4 bị timeout, transport bị hủy/dispose, và COM5 được kết nối
        Assert.True(transportDisposed);
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(Endpoint("COM5"), manager.CurrentEndpoint);

        await manager.DisconnectAsync();
    }

    // 5. InvalidDescriptor_IsSkipped
    [Fact]
    public async Task InvalidDescriptor_IsSkipped()
    {
        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (ep.Equals(Endpoint("COM4")))
            {
                return DeviceConnectionResult.Failed("Modbus CRC Error or Invalid Packet");
            }
            return await CreateSuccessfulConnectionAsync(ep);
        });

        var sessionManager = new SessionManager();
        var detector = new FakeDetector(new[] { Endpoint("COM4"), Endpoint("COM5") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(150);

        // COM4 lỗi CRC/descriptor bị skip an toàn, COM5 được chấp nhận
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(Endpoint("COM5"), manager.CurrentEndpoint);

        await manager.DisconnectAsync();
    }

    // 6. NoCandidate_RemainsInReconnect
    [Fact]
    public async Task NoCandidate_RemainsInReconnect()
    {
        var factory = new ConfigurableConnectionFactory((ep, ct) => CreateSuccessfulConnectionAsync(ep));
        var sessionManager = new SessionManager();
        // Detector không tìm thấy candidate nào
        var detector = new FakeDetector(Array.Empty<DeviceEndpoint>());
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(50);

        // Assert: Không có candidate nào -> Vẫn ở Reconnecting
        Assert.Equal(ConnectionLifecycleState.Reconnecting, manager.CurrentState);
        Assert.True(
            manager.FailureReason == ConnectionFailureReason.DeviceNotFound ||
            manager.FailureReason == ConnectionFailureReason.ReconnectExhausted,
            $"Expected DeviceNotFound or ReconnectExhausted, but got {manager.FailureReason}");

        await manager.DisconnectAsync();
    }

    // 7. SingleCompatibleCandidate_AfterPortMigrationIsAccepted
    [Fact]
    public async Task SingleCompatibleCandidate_AfterPortMigrationIsAccepted()
    {
        var factory = new ConfigurableConnectionFactory((ep, ct) => CreateSuccessfulConnectionAsync(ep));
        var sessionManager = new SessionManager();

        // Cáp USB đổi từ COM3 sang COM4, danh sách candidate chỉ có duy nhất COM4
        var detector = new FakeDetector(new[] { Endpoint("COM4") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(150);

        // Assert: Đúng 1 candidate tương thích duy nhất -> Chấp nhận Port Migration
        Assert.Equal(ConnectionLifecycleState.Connected, manager.CurrentState);
        Assert.Equal(Endpoint("COM4"), manager.CurrentEndpoint);
        Assert.Equal(ConnectionFailureReason.None, manager.FailureReason);

        await manager.DisconnectAsync();
    }

    // 8. MultipleCompatibleCandidates_DoesNotAutoSelect
    [Fact]
    public async Task MultipleCompatibleCandidates_DoesNotAutoSelect()
    {
        var activeTransports = new System.Collections.Concurrent.ConcurrentBag<FakeUsbCdcTransport>();
        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            var (client, transport, _) = CreateSimulatorPair(true);
            var fakeTransport = (FakeUsbCdcTransport)transport;
            activeTransports.Add(fakeTransport);

            var validator = new StandardDeviceCompatibilityValidator();
            var reader = new DeviceDescriptorReader(client);
            var descriptor = await reader.ReadDescriptorAsync(1);

            var session = new DeviceSession(
                ep,
                descriptor,
                1,
                fakeTransport,
                client,
                new RuleTableGateway(client),
                new RuntimeTagReader(client),
                new DeviceHealthReader(client),
                new SystemCommandClient(client));

            return DeviceConnectionResult.Success(session);
        });

        var sessionManager = new SessionManager();

        // Cũ là COM3 (biến mất). Xuất hiện 2 thiết bị tương thích: COM4 và COM7!
        var detector = new FakeDetector(new[] { Endpoint("COM4"), Endpoint("COM7") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));
        await Task.Delay(150);

        Assert.True(
            manager.FailureReason == ConnectionFailureReason.MultipleCompatibleDevices ||
            manager.FailureReason == ConnectionFailureReason.ReconnectExhausted,
            $"Expected MultipleCompatibleDevices or ReconnectExhausted, got {manager.FailureReason}");
        Assert.False(sessionManager.HasActiveSession);
        Assert.True(activeTransports.Count >= 2);

        await manager.DisconnectAsync();
        await Task.Delay(50);
        Assert.All(activeTransports.ToArray(), t => Assert.False(t.IsOpen));
    }

    // 9. Cancellation_StopsCandidateProbeAndDisposesCurrentTransport
    [Fact]
    public async Task Cancellation_StopsCandidateProbeAndDisposesCurrentTransport()
    {
        var probeStarted = new TaskCompletionSource<bool>();
        bool transportCancelled = false;
        bool isInitialConnect = true;

        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (isInitialConnect && ep.Equals(Endpoint("COM3")))
            {
                isInitialConnect = false;
                return await CreateSuccessfulConnectionAsync(ep);
            }

            probeStarted.TrySetResult(true);
            try
            {
                await Task.Delay(2000, ct);
            }
            catch (OperationCanceledException)
            {
                transportCancelled = true;
                throw;
            }
            return DeviceConnectionResult.Failed("Timeout");
        });

        var sessionManager = new SessionManager();
        var detector = new FakeDetector(new[] { Endpoint("COM4") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));

        // Chờ probe candidate bắt đầu
        await probeStarted.Task;

        // Act: Hủy / shutdown lifecycle manager
        await manager.DisposeAsync();

        // Assert: Probe bị hủy lập tức và transport được cleanup
        Assert.True(transportCancelled);
    }

    // 10. ManualDisconnect_DuringDiscovery_StopsFurtherProbes
    [Fact]
    public async Task ManualDisconnect_DuringDiscovery_StopsFurtherProbes()
    {
        var probeStarted = new TaskCompletionSource<bool>();
        int probeCount = 0;
        bool isInitialConnect = true;

        var factory = new ConfigurableConnectionFactory(async (ep, ct) =>
        {
            if (isInitialConnect && ep.Equals(Endpoint("COM3")))
            {
                isInitialConnect = false;
                return await CreateSuccessfulConnectionAsync(ep);
            }

            Interlocked.Increment(ref probeCount);
            probeStarted.TrySetResult(true);
            await Task.Delay(500, ct);
            return await CreateSuccessfulConnectionAsync(ep);
        });

        var sessionManager = new SessionManager();
        var detector = new FakeDetector(new[] { Endpoint("COM4"), Endpoint("COM5"), Endpoint("COM6") });
        var manager = new DeviceLifecycleManager(factory, sessionManager, ReconnectionPolicy.TestingImmediate, deviceDetector: detector);

        await manager.ConnectAsync(Endpoint("COM3"));
        await manager.NotifyUnexpectedDisconnectAsync(new System.IO.IOException("Unplugged"));

        // Chờ probe bắt đầu
        await probeStarted.Task;

        // Act: Người dùng bấm Disconnect thủ công
        await manager.DisconnectAsync();

        int probesAtDisconnect = Volatile.Read(ref probeCount);
        await Task.Delay(100);
        int probesAfterWait = Volatile.Read(ref probeCount);

        // Assert: Trạng thái Disconnected, không có probe nào tiếp tục chạy
        Assert.Equal(ConnectionLifecycleState.Disconnected, manager.CurrentState);
        Assert.Equal(ConnectionFailureReason.None, manager.FailureReason);
        Assert.Equal(probesAtDisconnect, probesAfterWait);
    }

    // Test Helpers
    private sealed class FakeDetector : IDeviceDetector
    {
        private readonly IReadOnlyList<DeviceEndpoint> _candidates;

        public FakeDetector(IReadOnlyList<DeviceEndpoint> candidates)
        {
            _candidates = candidates;
        }

        public Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_candidates);
        }
    }

    private sealed class ConfigurableConnectionFactory : IDeviceConnectionFactory
    {
        private readonly Func<DeviceEndpoint, CancellationToken, Task<DeviceConnectionResult>> _handler;

        public ConfigurableConnectionFactory(Func<DeviceEndpoint, CancellationToken, Task<DeviceConnectionResult>> handler)
        {
            _handler = handler;
        }

        public Task<DeviceConnectionResult> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            return _handler(endpoint, cancellationToken);
        }
    }
}
