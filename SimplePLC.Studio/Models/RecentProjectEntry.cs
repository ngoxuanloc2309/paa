namespace SimplePLC.Studio.Models;

/// <summary>
/// Đại diện cho một mục dự án mở gần đây (Recent Project Item) lưu trong cấu hình IDE.
/// </summary>
public sealed class RecentProjectEntry
{
    /// <summary>
    /// Đường dẫn tuyệt đối tới tệp dự án (.synx hoặc .json).
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Tên dự án do kỹ sư đặt.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Thiết bị mục tiêu hoặc dòng phần cứng (ví dụ: SynaptiX SYNX-RIO-884, SYNX-DTU-485).
    /// </summary>
    public string HardwareTarget { get; set; } = string.Empty;

    /// <summary>
    /// Huy hiệu mã phần cứng ngắn gọn (ví dụ: [SYNX-RIO-884], [SYNX-DTU-485], [XLINK-ZB-44]).
    /// </summary>
    public string HardwareBadge { get; set; } = string.Empty;

    /// <summary>
    /// Thời điểm mở dự án gần nhất (UTC).
    /// </summary>
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Trạng thái ghim dự án lên đầu danh sách.
    /// </summary>
    public bool IsPinned { get; set; } = false;
}
