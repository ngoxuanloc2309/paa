namespace SimplePLC.Application.Models;

/// <summary>
/// Trạng thái hoạt động của kết nối thiết bị (App đang làm gì).
/// </summary>
public enum ConnectionLifecycleState
{
    /// <summary>
    /// Chưa kết nối hoặc đã ngắt kết nối chủ động bởi người dùng.
    /// </summary>
    Disconnected = 0,

    /// <summary>
    /// Đang trong quá trình bắt tay kết nối ban đầu.
    /// </summary>
    Connecting = 1,

    /// <summary>
    /// Đã kết nối và vận hành bình thường (phiên làm việc hợp lệ).
    /// </summary>
    Connected = 2,

    /// <summary>
    /// Đang trong quá trình tự động kết nối lại (sau sự cố mất kết nối hoặc sau khi MCU khởi động lại).
    /// </summary>
    Reconnecting = 3,

    /// <summary>
    /// Thiết bị đang trong quá trình khởi động lại có chủ đích (Expected Reboot).
    /// </summary>
    Restarting = 4
}

/// <summary>
/// Lý do gây ra lỗi hoặc ngắt kết nối (Tại sao thất bại).
/// </summary>
public enum ConnectionFailureReason
{
    /// <summary>
    /// Không có lỗi (kết nối bình thường hoặc ngắt kết nối chủ động).
    /// </summary>
    None = 0,

    /// <summary>
    /// Không tìm thấy cổng/thiết bị chỉ định.
    /// </summary>
    DeviceNotFound = 1,

    /// <summary>
    /// Mất kết nối truyền thông vật lý (rút cáp USB, cổng COM đóng đột ngột).
    /// </summary>
    CommunicationLost = 2,

    /// <summary>
    /// Hết thời gian chờ phản hồi Modbus từ thiết bị.
    /// </summary>
    Timeout = 3,

    /// <summary>
    /// Thiết bị không tương thích về DeviceClass, ProductVariant hoặc phiên bản giao thức.
    /// </summary>
    IncompatibleDevice = 4,

    /// <summary>
    /// Đã hết lượt thử nhanh và chuyển sang chế độ thăm dò thụ động hoặc dừng lại.
    /// </summary>
    ReconnectExhausted = 5,

    /// <summary>
    /// Phát hiện nhiều thiết bị tương thích nhưng không thể tự động chọn (cần người dùng chỉ định).
    /// </summary>
    MultipleCompatibleDevices = 6
}

/// <summary>
/// Dữ liệu sự kiện khi trạng thái vòng đời kết nối thay đổi.
/// </summary>
public sealed class LifecycleStateChangedEventArgs : EventArgs
{
    public ConnectionLifecycleState PreviousState { get; }
    public ConnectionLifecycleState NewState { get; }
    public ConnectionFailureReason FailureReason { get; }
    public DeviceEndpoint? Endpoint { get; }
    public int RetryAttempt { get; }
    public bool IsPassiveWaiting { get; }
    public string? Message { get; }

    public LifecycleStateChangedEventArgs(
        ConnectionLifecycleState previousState,
        ConnectionLifecycleState newState,
        ConnectionFailureReason failureReason = ConnectionFailureReason.None,
        DeviceEndpoint? endpoint = null,
        int retryAttempt = 0,
        bool isPassiveWaiting = false,
        string? message = null)
    {
        PreviousState = previousState;
        NewState = newState;
        FailureReason = failureReason;
        Endpoint = endpoint;
        RetryAttempt = retryAttempt;
        IsPassiveWaiting = isPassiveWaiting;
        Message = message;
    }
}
