using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;

namespace SimplePLC.Application.Services;

/// <summary>
/// Hiện thực IDeviceOperationCoordinator bằng Async Lease Pattern.
/// Quản lý phân quyền giữa luồng Runtime Monitor (Polling) và tác vụ nạp (Deploy/Command).
/// </summary>
public sealed class DeviceOperationCoordinator : IDeviceOperationCoordinator
{
    private readonly SemaphoreSlim _exclusiveGate = new(1, 1);
    private readonly object _stateLock = new();

    private int _activePollingCount;
    private int _exclusivePendingCount;
    private bool _isExclusiveActive;
    private TaskCompletionSource<bool>? _pollingDrainedTcs;

    public bool IsExclusiveOperationActive
    {
        get
        {
            lock (_stateLock)
            {
                return _isExclusiveActive || _exclusivePendingCount > 0;
            }
        }
    }

    public ValueTask<IAsyncDisposable?> TryAcquirePollingLeaseAsync(CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ValueTask.FromCanceled<IAsyncDisposable?>(ct);

        lock (_stateLock)
        {
            if (_isExclusiveActive || _exclusivePendingCount > 0)
            {
                // Có tác vụ độc quyền -> Bỏ qua chu kỳ poll ngay lập tức
                return ValueTask.FromResult<IAsyncDisposable?>(null);
            }

            _activePollingCount++;
            return ValueTask.FromResult<IAsyncDisposable?>(new PollingLease(this));
        }
    }

    public async ValueTask<IAsyncDisposable> AcquireExclusiveAsync(DeviceOperation operation, CancellationToken ct = default)
    {
        // 1. Chỉ cho phép 1 tác vụ độc quyền tại một thời điểm
        await _exclusiveGate.WaitAsync(ct).ConfigureAwait(false);

        Task drainTask;
        lock (_stateLock)
        {
            _exclusivePendingCount++;

            if (_activePollingCount == 0)
            {
                _isExclusiveActive = true;
                _exclusivePendingCount--;
                return new ExclusiveLease(this, operation);
            }

            _pollingDrainedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            drainTask = _pollingDrainedTcs.Task;
        }

        // 2. Chờ cho vòng poll đang dở dang kết thúc
        try
        {
            using var reg = ct.Register(() => _pollingDrainedTcs?.TrySetCanceled(ct));
            await drainTask.ConfigureAwait(false);
        }
        catch
        {
            lock (_stateLock)
            {
                _exclusivePendingCount--;
            }
            _exclusiveGate.Release();
            throw;
        }

        lock (_stateLock)
        {
            _isExclusiveActive = true;
            _exclusivePendingCount--;
        }

        return new ExclusiveLease(this, operation);
    }

    private void ReleasePollingLease()
    {
        lock (_stateLock)
        {
            _activePollingCount--;
            if (_activePollingCount == 0 && _exclusivePendingCount > 0)
            {
                _pollingDrainedTcs?.TrySetResult(true);
            }
        }
    }

    private void ReleaseExclusiveLease()
    {
        lock (_stateLock)
        {
            _isExclusiveActive = false;
        }
        _exclusiveGate.Release();
    }

    private sealed class PollingLease : IAsyncDisposable
    {
        private DeviceOperationCoordinator? _coordinator;

        public PollingLease(DeviceOperationCoordinator coordinator)
        {
            _coordinator = coordinator;
        }

        public ValueTask DisposeAsync()
        {
            var coord = Interlocked.Exchange(ref _coordinator, null);
            coord?.ReleasePollingLease();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ExclusiveLease : IAsyncDisposable
    {
        private DeviceOperationCoordinator? _coordinator;
        public DeviceOperation Operation { get; }

        public ExclusiveLease(DeviceOperationCoordinator coordinator, DeviceOperation operation)
        {
            _coordinator = coordinator;
            Operation = operation;
        }

        public ValueTask DisposeAsync()
        {
            var coord = Interlocked.Exchange(ref _coordinator, null);
            coord?.ReleaseExclusiveLease();
            return ValueTask.CompletedTask;
        }
    }
}
