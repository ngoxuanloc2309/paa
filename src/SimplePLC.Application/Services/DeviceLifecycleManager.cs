using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;

namespace SimplePLC.Application.Services;

/// <summary>
/// Service trung tâm điều phối toàn bộ vòng đời kết nối thiết bị trong ứng dụng.
/// Thực hiện state machine, chính sách thử lại 2 tầng (Fast Recovery + Passive Wait),
/// và xử lý dứt khoát ranh giới giữa Manual Disconnect, Unexpected Disconnect và Expected Restart.
/// </summary>
public sealed class DeviceLifecycleManager : IDeviceLifecycleManager
{
    private readonly IDeviceConnectionFactory _connectionFactory;
    private readonly ISessionManager _sessionManager;
    private readonly ReconnectionPolicy _policy;
    private readonly TimeProvider _timeProvider;
    private readonly IDeviceDetector _deviceDetector;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateLock = new();

    private ConnectionLifecycleState _currentState = ConnectionLifecycleState.Disconnected;
    private ConnectionFailureReason _failureReason = ConnectionFailureReason.None;
    private DeviceEndpoint? _currentEndpoint;
    private int _retryAttempt;
    private bool _isPassiveWaiting;
    private byte _slaveId = 1;

    private CancellationTokenSource? _recoveryCts;
    private CancellationTokenSource? _activeConnectCts;
    private string? _lastMessage;
    private Task? _recoveryTask;
    private bool _isDisposed;

    public ConnectionLifecycleState CurrentState
    {
        get { lock (_stateLock) return _currentState; }
    }

    public ConnectionFailureReason FailureReason
    {
        get { lock (_stateLock) return _failureReason; }
    }

    public string? LastMessage
    {
        get { lock (_stateLock) return _lastMessage; }
    }

    public DeviceEndpoint? CurrentEndpoint
    {
        get { lock (_stateLock) return _currentEndpoint; }
    }

    public int RetryAttempt
    {
        get { lock (_stateLock) return _retryAttempt; }
    }

    public bool IsPassiveWaiting
    {
        get { lock (_stateLock) return _isPassiveWaiting; }
    }

    public event EventHandler<LifecycleStateChangedEventArgs>? StateChanged;

    public DeviceLifecycleManager(
        IDeviceConnectionFactory connectionFactory,
        ISessionManager sessionManager,
        ReconnectionPolicy? policy = null,
        TimeProvider? timeProvider = null,
        IDeviceDetector? deviceDetector = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _policy = policy ?? ReconnectionPolicy.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _deviceDetector = deviceDetector ?? new DefaultCandidateDetector(() => _currentEndpoint);
    }

    public DeviceLifecycleManager(
        IDeviceConnectionFactory connectionFactory,
        ISessionManager sessionManager,
        ReconnectionPolicy? policy,
        TimeProvider? timeProvider,
        Func<CancellationToken, Task<IReadOnlyList<DeviceEndpoint>>>? candidateDiscovery)
        : this(
            connectionFactory,
            sessionManager,
            policy,
            timeProvider,
            candidateDiscovery != null ? new DelegateDeviceDetector(candidateDiscovery) : null)
    {
    }

    public async Task<bool> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        lock (_stateLock)
        {
            _activeConnectCts = linkedCts;
        }

        try
        {
            await _gate.WaitAsync(linkedCts.Token).ConfigureAwait(false);
            try
            {
                // Nếu đã kết nối tới chính endpoint này thì không tạo session trùng lặp
                if (CurrentState == ConnectionLifecycleState.Connected &&
                    _currentEndpoint != null &&
                    _currentEndpoint.Equals(endpoint) &&
                    _sessionManager.HasActiveSession)
                {
                    return true;
                }

                // Hủy bỏ bất kỳ vòng lặp hồi phục ngầm nào đang chạy
                await CancelRecoveryLoopAsync().ConfigureAwait(false);

                // Đóng session cũ nếu có trước khi kết nối endpoint mới
                if (_sessionManager.HasActiveSession)
                {
                    await _sessionManager.CloseCurrentSessionAsync(linkedCts.Token).ConfigureAwait(false);
                }

                _currentEndpoint = endpoint;
                _slaveId = slaveId;

                TransitionTo(ConnectionLifecycleState.Connecting, ConnectionFailureReason.None, endpoint: endpoint);

                var connResult = await _connectionFactory.ConnectAsync(endpoint, slaveId, linkedCts.Token).ConfigureAwait(false);

                if (connResult.IsSuccess && connResult.Session != null)
                {
                    await _sessionManager.SetCurrentSessionAsync(connResult.Session, linkedCts.Token).ConfigureAwait(false);
                    TransitionTo(ConnectionLifecycleState.Connected, ConnectionFailureReason.None, endpoint: endpoint);
                    return true;
                }

                if (!connResult.Compatibility.IsCompatible &&
                    connResult.Compatibility.Status != CompatibilityStatus.Unknown)
                {
                    TransitionTo(
                        ConnectionLifecycleState.Disconnected,
                        ConnectionFailureReason.IncompatibleDevice,
                        endpoint: endpoint,
                        message: connResult.FailureReason ?? connResult.Compatibility.Reason);
                    return false;
                }

                var failReason = connResult.FailureReason?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true
                    ? ConnectionFailureReason.Timeout
                    : ConnectionFailureReason.CommunicationLost;

                TransitionTo(
                    ConnectionLifecycleState.Disconnected,
                    failReason,
                    endpoint: endpoint,
                    message: connResult.FailureReason ?? "Failed to connect to device.");
                return false;
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
                TransitionTo(ConnectionLifecycleState.Disconnected, ConnectionFailureReason.None, endpoint: endpoint);
                throw;
            }
            catch (Exception ex)
            {
                TransitionTo(
                    ConnectionLifecycleState.Disconnected,
                    ConnectionFailureReason.CommunicationLost,
                    endpoint: endpoint,
                    message: ex.Message);
                return false;
            }
            finally
            {
                _gate.Release();
            }
        }
        finally
        {
            lock (_stateLock)
            {
                if (_activeConnectCts == linkedCts)
                    _activeConnectCts = null;
            }
        }
    }

    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        // Hủy bất kỳ tác vụ ConnectAsync đang chờ phản hồi ngay lập tức để giải phóng gate
        lock (_stateLock)
        {
            try { _activeConnectCts?.Cancel(); } catch { }
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Ngắt kết nối chủ động: Hủy vòng lặp reconnect và không tự động thử lại
            await CancelRecoveryLoopAsync().ConfigureAwait(false);

            await _sessionManager.CloseCurrentSessionAsync(ct).ConfigureAwait(false);

            TransitionTo(ConnectionLifecycleState.Disconnected, ConnectionFailureReason.None, endpoint: _currentEndpoint);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task NotifyUnexpectedDisconnectAsync(Exception? exception = null, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Chỉ phản ứng nếu trước đó đang Connected hoặc Connecting
            if (CurrentState != ConnectionLifecycleState.Connected &&
                CurrentState != ConnectionLifecycleState.Connecting)
            {
                return;
            }

            // Giải phóng session cũ
            await _sessionManager.CloseCurrentSessionAsync(CancellationToken.None).ConfigureAwait(false);

            await CancelRecoveryLoopAsync().ConfigureAwait(false);

            var reason = exception is TimeoutException
                ? ConnectionFailureReason.Timeout
                : ConnectionFailureReason.CommunicationLost;

            if (!_policy.AutoReconnectOnDisconnect)
            {
                // Chuẩn công nghiệp (Siemens TIA Portal / Rockwell): Khi mất kết nối, dừng polling và đưa thẳng về Disconnected
                TransitionTo(
                    ConnectionLifecycleState.Disconnected,
                    reason,
                    endpoint: _currentEndpoint,
                    message: exception?.Message ?? "Communication lost");
                return;
            }

            TransitionTo(
                ConnectionLifecycleState.Reconnecting,
                reason,
                endpoint: _currentEndpoint,
                retryAttempt: 1,
                isPassiveWaiting: false,
                message: exception?.Message);

            _recoveryCts = new CancellationTokenSource();
            _recoveryTask = RunRecoveryLoopAsync(_recoveryCts.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task NotifyExpectedRestartAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Giải phóng session cũ ngay khi thiết bị chuẩn bị reset
            await _sessionManager.CloseCurrentSessionAsync(CancellationToken.None).ConfigureAwait(false);

            await CancelRecoveryLoopAsync().ConfigureAwait(false);

            if (!_policy.AutoReconnectOnDisconnect)
            {
                // Chuẩn công nghiệp: Thiết bị khởi động lại, chuyển thẳng về Disconnected để người dùng kết nối lại khi sẵn sàng
                TransitionTo(
                    ConnectionLifecycleState.Disconnected,
                    ConnectionFailureReason.None,
                    endpoint: _currentEndpoint,
                    message: "Device restarted");
                return;
            }

            TransitionTo(
                ConnectionLifecycleState.Restarting,
                ConnectionFailureReason.None,
                endpoint: _currentEndpoint,
                message: "Device is restarting");

            _recoveryCts = new CancellationTokenSource();
            _recoveryTask = RunRestartRecoveryLoopAsync(_recoveryCts.Token);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task RunRestartRecoveryLoopAsync(CancellationToken ct)
    {
        try
        {
            // 1. Chờ thời gian thiết bị reset và ngắt kết nối USB vật lý
            await Task.Delay(_policy.ExpectedRestartDelay, _timeProvider, ct).ConfigureAwait(false);

            // 2. Chuyển sang trạng thái Reconnecting
            TransitionTo(
                ConnectionLifecycleState.Reconnecting,
                ConnectionFailureReason.None,
                endpoint: _currentEndpoint,
                retryAttempt: 1,
                isPassiveWaiting: false);

            // 3. Tiến hành tìm kiếm và thiết lập New Session
            await RunRecoveryCoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Bị hủy khi người dùng ngắt kết nối thủ công hoặc đóng ứng dụng
        }
    }

    private async Task RunRecoveryLoopAsync(CancellationToken ct)
    {
        try
        {
            await RunRecoveryCoreAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Bị hủy khi người dùng ngắt kết nối thủ công hoặc đóng ứng dụng
        }
    }

    private async Task RunRecoveryCoreAsync(CancellationToken ct)
    {
        // ================= TẦNG 1: FAST RECOVERY =================
        for (int i = 0; i < _policy.FastRetryDelays.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            int attempt = i + 1;
            TransitionTo(
                ConnectionLifecycleState.Reconnecting,
                FailureReason,
                endpoint: _currentEndpoint,
                retryAttempt: attempt,
                isPassiveWaiting: false);

            // Chờ theo backoff delay
            await Task.Delay(_policy.FastRetryDelays[i], _timeProvider, ct).ConfigureAwait(false);

            bool success = await TryProbeAndConnectCandidatesAsync(ct).ConfigureAwait(false);
            if (success)
            {
                return;
            }
        }

        // ================= TẦNG 2: PASSIVE WAIT =================
        if (!_policy.EnablePassiveWait)
        {
            TransitionTo(
                ConnectionLifecycleState.Disconnected,
                ConnectionFailureReason.ReconnectExhausted,
                endpoint: _currentEndpoint,
                message: "Reconnection attempts exhausted");
            return;
        }

        TransitionTo(
            ConnectionLifecycleState.Reconnecting,
            ConnectionFailureReason.ReconnectExhausted,
            endpoint: _currentEndpoint,
            retryAttempt: _policy.FastRetryDelays.Count,
            isPassiveWaiting: true,
            message: "Entering passive wait for device");

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_policy.PassiveWaitInterval, _timeProvider, ct).ConfigureAwait(false);

            bool success = await TryProbeAndConnectCandidatesAsync(ct).ConfigureAwait(false);
            if (success)
            {
                return;
            }
        }
    }

    private async Task<bool> TryProbeAndConnectCandidatesAsync(CancellationToken ct)
    {
        IReadOnlyList<DeviceEndpoint> candidates;
        try
        {
            candidates = await _deviceDetector.FindCandidatesAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            candidates = Array.Empty<DeviceEndpoint>();
        }

        ct.ThrowIfCancellationRequested();

        var previousEndpoint = _currentEndpoint;

        // BƯỚC 1: Ưu tiên previous endpoint trước (nếu còn tồn tại trong danh sách candidates)
        if (previousEndpoint != null && candidates.Any(c => c.Equals(previousEndpoint)))
        {
            var probeResult = await ProbeCandidateAsync(previousEndpoint, ct).ConfigureAwait(false);
            if (probeResult.IsSuccess && probeResult.Session != null)
            {
                // Khớp chính xác thiết bị cũ, chấp nhận ngay lập tức mà không cần probe thêm
                _currentEndpoint = previousEndpoint;
                await _sessionManager.SetCurrentSessionAsync(probeResult.Session, ct).ConfigureAwait(false);

                TransitionTo(
                    ConnectionLifecycleState.Connected,
                    ConnectionFailureReason.None,
                    endpoint: previousEndpoint,
                    message: "Reconnected successfully to previous endpoint");
                return true;
            }
        }

        // BƯỚC 2: Nếu previous endpoint không có hoặc probe thất bại, tuần tự probe các candidate khác
        var otherCandidates = candidates
            .Where(c => !c.Equals(previousEndpoint))
            .ToList();

        if (otherCandidates.Count == 0)
        {
            var reason = candidates.Count == 0
                ? ConnectionFailureReason.DeviceNotFound
                : ConnectionFailureReason.CommunicationLost;

            TransitionTo(
                ConnectionLifecycleState.Reconnecting,
                reason,
                endpoint: _currentEndpoint,
                message: "No candidates available to reconnect");
            return false;
        }

        var compatibleMatches = new List<DeviceConnectionResult>();
        try
        {
            foreach (var candidate in otherCandidates)
            {
                ct.ThrowIfCancellationRequested();

                var probeResult = await ProbeCandidateAsync(candidate, ct).ConfigureAwait(false);
                if (probeResult.IsSuccess && probeResult.Session != null)
                {
                    compatibleMatches.Add(probeResult);

                    // Nếu đã phát hiện nhiều hơn 1 candidate tương thích -> Không tự ý chọn bừa!
                    // Dừng probe ngay và giải phóng tất cả các session đã mở
                    if (compatibleMatches.Count > 1)
                    {
                        break;
                    }
                }
            }

            // ĐÁNH GIÁ KẾT QUẢ PROBE CÁC CANDIDATES KHÁC:
            if (compatibleMatches.Count == 1)
            {
                // Đúng 1 candidate tương thích duy nhất: Chấp nhận Port Migration!
                var match = compatibleMatches[0];
                var newEndpoint = match.Session!.Endpoint;
                _currentEndpoint = newEndpoint;
                await _sessionManager.SetCurrentSessionAsync(match.Session, ct).ConfigureAwait(false);
                compatibleMatches.Clear(); // Đã chuyển giao quyền sở hữu cho SessionManager

                TransitionTo(
                    ConnectionLifecycleState.Connected,
                    ConnectionFailureReason.None,
                    endpoint: newEndpoint,
                    message: $"Reconnected successfully via port migration to {newEndpoint}");
                return true;
            }

            if (compatibleMatches.Count > 1)
            {
                TransitionTo(
                    ConnectionLifecycleState.Reconnecting,
                    ConnectionFailureReason.MultipleCompatibleDevices,
                    endpoint: _currentEndpoint,
                    message: "Multiple compatible devices found; manual selection required");
                return false;
            }

            // 0 match tương thích
            TransitionTo(
                ConnectionLifecycleState.Reconnecting,
                ConnectionFailureReason.CommunicationLost,
                endpoint: _currentEndpoint,
                message: "No compatible device found among candidates");
            return false;
        }
        finally
        {
            // Bất biến dọn dẹp: Bất kỳ session probe nào không được chuyển giao cho SessionManager
            // (kể cả khi bị hủy bởi CancellationToken trong quá trình probe) đều được giải phóng triệt để.
            foreach (var match in compatibleMatches)
            {
                if (match.Session != null)
                {
                    try { await match.Session.DisposeAsync().ConfigureAwait(false); } catch { }
                }
            }
        }
    }

    private async Task<DeviceConnectionResult> ProbeCandidateAsync(DeviceEndpoint candidate, CancellationToken ct)
    {
        using var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        probeCts.CancelAfter(_policy.ProbeTimeout);

        try
        {
            return await _connectionFactory.ConnectAsync(candidate, _slaveId, probeCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && probeCts.IsCancellationRequested)
        {
            // Bị hủy do probe timeout cục bộ của candidate này
            return DeviceConnectionResult.Failed($"Probe timed out after {_policy.ProbeTimeout.TotalMilliseconds}ms");
        }
        catch (OperationCanceledException)
        {
            throw; // ct gốc bị hủy (do Disconnect hoặc ứng dụng đóng)
        }
        catch (Exception ex)
        {
            return DeviceConnectionResult.Failed(ex.Message);
        }
    }

    private async Task CancelRecoveryLoopAsync()
    {
        if (_recoveryCts != null)
        {
            try
            {
                _recoveryCts.Cancel();
            }
            catch { }
        }

        if (_recoveryTask != null)
        {
            try
            {
                await _recoveryTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch { }
            _recoveryTask = null;
        }

        if (_recoveryCts != null)
        {
            try
            {
                _recoveryCts.Dispose();
            }
            catch { }
            _recoveryCts = null;
        }
    }

    private void TransitionTo(
        ConnectionLifecycleState newState,
        ConnectionFailureReason failureReason,
        DeviceEndpoint? endpoint = null,
        int retryAttempt = 0,
        bool isPassiveWaiting = false,
        string? message = null)
    {
        ConnectionLifecycleState prevState;
        LifecycleStateChangedEventArgs args;

        lock (_stateLock)
        {
            prevState = _currentState;
            _currentState = newState;
            _failureReason = failureReason;
            _lastMessage = message;
            if (endpoint != null) _currentEndpoint = endpoint;
            _retryAttempt = retryAttempt;
            _isPassiveWaiting = isPassiveWaiting;

            args = new LifecycleStateChangedEventArgs(
                prevState,
                newState,
                failureReason,
                _currentEndpoint,
                retryAttempt,
                isPassiveWaiting,
                message);
        }

        // Bắn sự kiện NGOÀI lock để tránh deadlock với subscriber
        StateChanged?.Invoke(this, args);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (_recoveryCts != null)
        {
            try { _recoveryCts.Cancel(); } catch { }
            try { _recoveryCts.Dispose(); } catch { }
            _recoveryCts = null;
        }
        _gate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await CancelRecoveryLoopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private sealed class DelegateDeviceDetector : IDeviceDetector
    {
        private readonly Func<CancellationToken, Task<IReadOnlyList<DeviceEndpoint>>> _func;
        public DelegateDeviceDetector(Func<CancellationToken, Task<IReadOnlyList<DeviceEndpoint>>> func)
        {
            _func = func ?? throw new ArgumentNullException(nameof(func));
        }

        public Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default)
            => _func(ct);
    }

    private sealed class DefaultCandidateDetector : IDeviceDetector
    {
        private readonly Func<DeviceEndpoint?> _currentEndpointProvider;
        public DefaultCandidateDetector(Func<DeviceEndpoint?> currentEndpointProvider)
        {
            _currentEndpointProvider = currentEndpointProvider;
        }

        public Task<IReadOnlyList<DeviceEndpoint>> FindCandidatesAsync(CancellationToken ct = default)
        {
            var ep = _currentEndpointProvider();
            IReadOnlyList<DeviceEndpoint> result = ep != null ? new[] { ep } : Array.Empty<DeviceEndpoint>();
            return Task.FromResult(result);
        }
    }
}
