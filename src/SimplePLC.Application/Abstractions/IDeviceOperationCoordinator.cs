using SimplePLC.Application.Models;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Bộ điều phối chính sách truy cập thiết bị giữa luồng giám sát (Monitor) và các tác vụ nạp / điều khiển.
/// Thực hiện mô hình Lease Pattern: Polling lease xin quyền thăm dò, Exclusive lease chiếm quyền độc quyền.
/// </summary>
public interface IDeviceOperationCoordinator
{
    /// <summary>
    /// Thử xin quyền thăm dò chu kỳ cho Monitor.
    /// Nếu có tác vụ độc quyền đang chạy hoặc đang chờ, trả về null ngay lập tức (Skip cycle).
    /// </summary>
    ValueTask<IAsyncDisposable?> TryAcquirePollingLeaseAsync(CancellationToken ct = default);

    /// <summary>
    /// Chiếm quyền truy cập độc quyền cho các tác vụ quan trọng (Deploy, System Command).
    /// Đợi vòng thăm dò hiện tại hoàn tất, chặn các chu kỳ tiếp theo và trả về Lease độc quyền.
    /// </summary>
    ValueTask<IAsyncDisposable> AcquireExclusiveAsync(DeviceOperation operation, CancellationToken ct = default);

    /// <summary>
    /// Cho biết hiện tại có tác vụ độc quyền nào đang chiếm quyền hay không.
    /// </summary>
    bool IsExclusiveOperationActive { get; }
}
