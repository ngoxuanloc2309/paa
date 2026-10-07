namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Quản lý vòng đời và sở hữu của phiên kết nối thiết bị hiện tại trong ứng dụng.
/// Bảo đảm giải phóng hoàn toàn tài nguyên COM port của phiên cũ khi thiết lập phiên mới.
/// </summary>
public interface ISessionManager : IAsyncDisposable
{
    IDeviceSession? CurrentSession { get; }
    bool HasActiveSession { get; }

    event Action<IDeviceSession?>? SessionChanged;

    Task SetCurrentSessionAsync(IDeviceSession session, CancellationToken cancellationToken = default);
    Task CloseCurrentSessionAsync(CancellationToken cancellationToken = default);
}
