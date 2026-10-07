using SimplePLC.Application.Abstractions;

namespace SimplePLC.Application.Services;

/// <summary>
/// Quản lý phiên làm việc tập trung, an toàn luồng và giải phóng session cũ an toàn.
/// </summary>
public sealed class SessionManager : ISessionManager
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IDeviceSession? _currentSession;
    private bool _isDisposed;

    public IDeviceSession? CurrentSession => _currentSession;
    public bool HasActiveSession => !_isDisposed && _currentSession != null && _currentSession.IsActive;

    public event Action<IDeviceSession?>? SessionChanged;

    public async Task SetCurrentSessionAsync(IDeviceSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentSession != null)
            {
                await _currentSession.DisposeAsync().ConfigureAwait(false);
            }

            _currentSession = session;
            SessionChanged?.Invoke(_currentSession);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task CloseCurrentSessionAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_currentSession != null)
            {
                await _currentSession.DisposeAsync().ConfigureAwait(false);
                _currentSession = null;
                SessionChanged?.Invoke(null);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        await CloseCurrentSessionAsync().ConfigureAwait(false);
        _lock.Dispose();
    }
}
