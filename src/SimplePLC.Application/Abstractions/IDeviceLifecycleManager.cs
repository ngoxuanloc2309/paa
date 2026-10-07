using SimplePLC.Application.Models;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Nguồn chân lý duy nhất (Single Source of Truth) quản lý toàn bộ vòng đời kết nối thiết bị.
/// Quản lý state machine: Disconnected, Connecting, Connected, Reconnecting, Restarting.
/// </summary>
public interface IDeviceLifecycleManager : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Trạng thái vòng đời kết nối hiện tại.
    /// </summary>
    ConnectionLifecycleState CurrentState { get; }

    /// <summary>
    /// Nguyên nhân gây ra lỗi hoặc ngắt kết nối hiện tại.
    /// </summary>
    ConnectionFailureReason FailureReason { get; }

    /// <summary>
    /// Thông điệp chi tiết về trạng thái hoặc lỗi kết nối cuối cùng.
    /// </summary>
    string? LastMessage { get; }

    /// <summary>
    /// Endpoint hiện tại đang kết nối hoặc endpoint cuối cùng được cấu hình.
    /// </summary>
    DeviceEndpoint? CurrentEndpoint { get; }

    /// <summary>
    /// Số lần thử kết nối lại hiện tại (trong tầng Fast Recovery).
    /// </summary>
    int RetryAttempt { get; }

    /// <summary>
    /// Cho biết hệ thống có đang ở tầng Passive Wait (thăm dò định kỳ) hay không.
    /// </summary>
    bool IsPassiveWaiting { get; }

    /// <summary>
    /// Sự kiện thông báo khi trạng thái kết nối hoặc lý do lỗi thay đổi.
    /// </summary>
    event EventHandler<LifecycleStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Bắt đầu kết nối chủ động tới một endpoint chỉ định.
    /// Trả về true nếu kết nối thành công và phiên làm việc đã sẵn sàng; ngược lại trả về false.
    /// </summary>
    Task<bool> ConnectAsync(DeviceEndpoint endpoint, byte slaveId = 1, CancellationToken ct = default);

    /// <summary>
    /// Ngắt kết nối chủ động bởi người dùng (Manual Disconnect).
    /// Hủy hoàn toàn mọi vòng lặp reconnect và chuyển trạng thái về Disconnected (FailureReason = None).
    /// </summary>
    Task DisconnectAsync(CancellationToken ct = default);

    /// <summary>
    /// Thông báo về một sự cố mất kết nối đột ngột (rút cáp, cổng đóng, timeout liên tiếp).
    /// Tự động đóng session cũ, chuyển trạng thái sang Reconnecting và kích hoạt vòng lặp hồi phục.
    /// </summary>
    Task NotifyUnexpectedDisconnectAsync(Exception? exception = null, CancellationToken ct = default);

    /// <summary>
    /// Thông báo thiết bị đang khởi động lại có chủ đích (lệnh REBOOT vừa được gửi).
    /// Chuyển trạng thái sang Restarting, đợi thời gian reset định trước và bắt đầu tìm kiếm để tạo New Session.
    /// </summary>
    Task NotifyExpectedRestartAsync(CancellationToken ct = default);
}
