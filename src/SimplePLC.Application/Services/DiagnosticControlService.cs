using System.Collections.Concurrent;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Application.Services;

/// <summary>
/// Dịch vụ quản lý phiên chạy thử & cưỡng bức I/O (Manual Commissioning) theo Wire Profile V2.
/// Duy trì nhịp tim (Heartbeat 1000ms), bảo vệ thời gian thuê (Lease 3000ms) và đồng bộ độc quyền qua Coordinator.
/// </summary>
public sealed class DiagnosticControlService : IDiagnosticController
{
    private readonly IDiagnosticGateway _diagnosticGateway;
    private readonly IDeviceOperationCoordinator? _coordinator;
    private readonly ConcurrentDictionary<ushort, int> _forcedTags = new();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private bool _isDisposed;

    public bool IsManualModeActive { get; private set; }
    public DiagnosticStatusDto? CurrentStatus { get; private set; }
    public ushort LeaseRemainingMs => CurrentStatus?.LeaseRemainingMs ?? 0;
    public bool IsRetainDirty => CurrentStatus?.IsRetainDirty ?? false;
    public IReadOnlyDictionary<ushort, int> ForcedTags => _forcedTags;

    public event Action<DiagnosticStatusDto>? StatusChanged;
    public event Action<ushort, int>? TagForced;
    public event Action<ushort>? TagReleased;
    public event Action? AllTagsReleased;
    public event Action<Exception>? ErrorOccurred;

    public DiagnosticControlService(
        IDiagnosticGateway diagnosticGateway,
        IDeviceOperationCoordinator? coordinator = null)
    {
        _diagnosticGateway = diagnosticGateway ?? throw new ArgumentNullException(nameof(diagnosticGateway));
        _coordinator = coordinator;
    }

    public async Task<bool> EnterManualModeAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsManualModeActive)
                return true;

            IAsyncDisposable? exclusiveLease = null;
            if (_coordinator != null)
            {
                exclusiveLease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DiagnosticControl, cancellationToken).ConfigureAwait(false);
            }

            DiagnosticStatusDto status;
            try
            {
                status = await _diagnosticGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.ENTER_DIAG, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (exclusiveLease != null)
                {
                    await exclusiveLease.DisposeAsync().ConfigureAwait(false);
                }
            }

            CurrentStatus = status;
            StatusChanged?.Invoke(status);

            if (status.State == SPLC_DiagState.DIAG_CONTROL)
            {
                IsManualModeActive = true;
                StartHeartbeatLoop(slaveId);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> ExitManualModeAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await StopHeartbeatLoopAsync().ConfigureAwait(false);

            IAsyncDisposable? exclusiveLease = null;
            if (_coordinator != null)
            {
                exclusiveLease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DiagnosticControl, cancellationToken).ConfigureAwait(false);
            }

            DiagnosticStatusDto status;
            try
            {
                status = await _diagnosticGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.EXIT_DIAG, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                if (exclusiveLease != null)
                {
                    await exclusiveLease.DisposeAsync().ConfigureAwait(false);
                }
            }

            CurrentStatus = status;
            StatusChanged?.Invoke(status);

            if (status.State == SPLC_DiagState.ENGINE_RUNNING)
            {
                IsManualModeActive = false;
                _forcedTags.Clear();
                AllTagsReleased?.Invoke();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ForceTagAsync(ushort tagIndex, int rawValue, byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        IAsyncDisposable? exclusiveLease = null;
        if (_coordinator != null)
        {
            exclusiveLease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DiagnosticControl, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await _diagnosticGateway.WriteTagValueAsync(slaveId, tagIndex, rawValue, cancellationToken).ConfigureAwait(false);
            _forcedTags[tagIndex] = rawValue;
            TagForced?.Invoke(tagIndex, rawValue);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
        finally
        {
            if (exclusiveLease != null)
            {
                await exclusiveLease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task ReleaseTagAsync(ushort tagIndex, byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        if (_forcedTags.TryRemove(tagIndex, out _))
        {
            // Nếu là DO (ngõ ra vật lý 8..15), ghi về 0 để an toàn
            if (tagIndex >= 8 && tagIndex <= 15)
            {
                IAsyncDisposable? exclusiveLease = null;
                if (_coordinator != null)
                {
                    exclusiveLease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DiagnosticControl, cancellationToken).ConfigureAwait(false);
                }
                try
                {
                    await _diagnosticGateway.WriteTagValueAsync(slaveId, tagIndex, 0, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    if (exclusiveLease != null)
                    {
                        await exclusiveLease.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }

            TagReleased?.Invoke(tagIndex);
        }
    }

    public async Task ReleaseAllAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var keys = _forcedTags.Keys.ToList();
        foreach (var tagIndex in keys)
        {
            if (tagIndex >= 8 && tagIndex <= 15)
            {
                try
                {
                    await ForceTagAsync(tagIndex, 0, slaveId, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // Tiếp tục nhả các tag còn lại
                }
            }
            _forcedTags.TryRemove(tagIndex, out _);
        }

        AllTagsReleased?.Invoke();

        if (IsRetainDirty)
        {
            try
            {
                await DiscardRetainAsync(slaveId, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Bỏ qua nếu discard không hoàn tất
            }
        }

        await ExitManualModeAsync(slaveId, cancellationToken).ConfigureAwait(false);
    }

    public async Task CommitRetainAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        IAsyncDisposable? exclusiveLease = null;
        if (_coordinator != null)
        {
            exclusiveLease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DiagnosticControl, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var status = await _diagnosticGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.COMMIT_RETAIN, cancellationToken).ConfigureAwait(false);
            CurrentStatus = status;
            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
        finally
        {
            if (exclusiveLease != null)
            {
                await exclusiveLease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task DiscardRetainAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        IAsyncDisposable? exclusiveLease = null;
        if (_coordinator != null)
        {
            exclusiveLease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DiagnosticControl, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            var status = await _diagnosticGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.DISCARD_RETAIN, cancellationToken).ConfigureAwait(false);
            CurrentStatus = status;
            StatusChanged?.Invoke(status);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            throw;
        }
        finally
        {
            if (exclusiveLease != null)
            {
                await exclusiveLease.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private void StartHeartbeatLoop(byte slaveId)
    {
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = RunHeartbeatLoopAsync(slaveId, _heartbeatCts.Token);
    }

    private async Task StopHeartbeatLoopAsync()
    {
        if (_heartbeatCts != null)
        {
            _heartbeatCts.Cancel();
            if (_heartbeatTask != null)
            {
                try
                {
                    await _heartbeatTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // Chấm dứt bình thường
                }
                catch
                {
                    // Bỏ qua lỗi khi stop
                }
            }
            _heartbeatCts.Dispose();
            _heartbeatCts = null;
            _heartbeatTask = null;
        }
    }

    private async Task RunHeartbeatLoopAsync(byte slaveId, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(1000));
        while (!ct.IsCancellationRequested && await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            try
            {
                IAsyncDisposable? lease = null;
                if (_coordinator != null)
                {
                    lease = await _coordinator.AcquireExclusiveAsync(DeviceOperation.DiagnosticHeartbeat, ct).ConfigureAwait(false);
                }

                DiagnosticStatusDto status;
                try
                {
                    status = await _diagnosticGateway.SendDiagnosticCommandAsync(slaveId, SPLC_DiagCommand.HEARTBEAT, ct).ConfigureAwait(false);
                }
                finally
                {
                    if (lease != null)
                    {
                        await lease.DisposeAsync().ConfigureAwait(false);
                    }
                }

                CurrentStatus = status;
                StatusChanged?.Invoke(status);

                if (status.State != SPLC_DiagState.DIAG_CONTROL)
                {
                    // MCU đã thoát DIAG_CONTROL do timeout hoặc lỗi phần cứng
                    IsManualModeActive = false;
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        await StopHeartbeatLoopAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
