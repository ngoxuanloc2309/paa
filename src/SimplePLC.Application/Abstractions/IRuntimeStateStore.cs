namespace SimplePLC.Application.Abstractions;

using SimplePLC.Application.Models;

/// <summary>
/// Giao diện kho lưu trữ trạng thái thời gian thực (In-Memory Runtime State Store) dành cho consumer (UI/Studio).
/// Cung cấp API chỉ đọc (read-only queries) và sự kiện đồng bộ snapshot.
/// Các phương thức cập nhật nội bộ thuộc về concrete class RuntimeStateStore.
/// </summary>
public interface IRuntimeStateStore
{
    /// <summary>
    /// Ảnh chụp trạng thái thiết bị thời gian thực hiện tại.
    /// </summary>
    RuntimeDeviceSnapshot CurrentSnapshot { get; }

    /// <summary>
    /// Lấy danh sách tất cả các RuntimeTagSnapshot hiện tại.
    /// </summary>
    IReadOnlyList<RuntimeTagSnapshot> GetAllTags();

    /// <summary>
    /// Lấy snapshot của một Tag cụ thể theo chỉ số tag.
    /// </summary>
    RuntimeTagSnapshot? GetTag(ushort tagIndex);

    /// <summary>
    /// Sự kiện phát ra khi toàn bộ RuntimeDeviceSnapshot có sự thay đổi (được gọi ngoài store lock).
    /// </summary>
    event Action<RuntimeDeviceSnapshot>? SnapshotUpdated;
}
