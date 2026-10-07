namespace SimplePLC.Studio.Models;

/// <summary>
/// Trạng thái sai khác đồ thị của phần tử FBD Canvas (Visual Diff State).
/// Dùng để hiển thị lớp Ghost Preview do AI đề xuất hoặc kiểm tra phiên bản.
/// </summary>
public enum DiffState
{
    /// <summary>
    /// Phần tử bình thường đang hoạt động.
    /// </summary>
    None = 0,

    /// <summary>
    /// Phần tử do AI đề xuất thêm mới (Ghost preview nét đứt xanh ngọc).
    /// </summary>
    Added = 1,

    /// <summary>
    /// Phần tử bị thay đổi tham số/nối dây (Ghost preview viền vàng cam).
    /// </summary>
    Modified = 2,

    /// <summary>
    /// Phần tử đề xuất xóa bỏ (Ghost preview viền đỏ mờ).
    /// </summary>
    Deleted = 3
}
