using CommunityToolkit.Mvvm.ComponentModel;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Model chiếu dữ liệu của một Tag trong danh sách chọn Dropdown (ComboBox)
/// phục vụ cơ chế kiểm soát độc quyền luồng tiếp nhận truyền thông (Exclusive Ingestion Ownership).
/// Kế thừa ObservableObject với các thuộc tính có thể cập nhật tại chỗ (in-place) mà không gây reset bộ sưu tập ItemsSource của WPF.
/// </summary>
public sealed partial class TagOptionItem : ObservableObject
{
    /// <summary>
    /// Đối tượng Tag thực thể gốc trong TagCatalog.
    /// </summary>
    public TagModel Tag { get; }

    /// <summary>
    /// Cho biết Tag này có khả dụng để chọn hay không.
    /// False nếu Tag đã bị một kênh đọc khác (Modbus Read hoặc MQTT Sub) chiếm giữ độc quyền.
    /// </summary>
    [ObservableProperty]
    private bool _isSelectable;

    /// <summary>
    /// Chuỗi văn bản hiển thị tên Tag và bí danh.
    /// </summary>
    [ObservableProperty]
    private string _displayText = string.Empty;

    /// <summary>
    /// Nhãn thông tin xung đột khi Tag bị chiếm dụng (ví dụ: "[⛔ Đang ghi: Kênh 1]").
    /// Rỗng nếu Tag còn khả dụng.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasConflict))]
    private string _conflictBadge = string.Empty;

    /// <summary>
    /// Chi tiết chú giải công cụ giải thích vì sao Tag không thể chọn.
    /// </summary>
    [ObservableProperty]
    private string _conflictTooltip = string.Empty;

    /// <summary>
    /// True nếu Tag này đang xảy ra xung đột chiếm dụng.
    /// </summary>
    public bool HasConflict => !string.IsNullOrEmpty(ConflictBadge);

    /// <summary>
    /// Khởi tạo một đối tượng lựa chọn Tag trong Dropdown.
    /// </summary>
    /// <param name="tag">Đối tượng TagModel thực thể.</param>
    /// <param name="isSelectable">Khả năng bấm chọn trong danh sách.</param>
    /// <param name="displayText">Tên hiển thị của Tag.</param>
    /// <param name="conflictBadge">Nhãn cảnh báo xung đột (nếu có).</param>
    /// <param name="conflictTooltip">Thông tin tooltip chi tiết.</param>
    public TagOptionItem(
        TagModel tag,
        bool isSelectable,
        string displayText,
        string conflictBadge = "",
        string conflictTooltip = "")
    {
        Tag = tag;
        _isSelectable = isSelectable;
        _displayText = displayText;
        _conflictBadge = conflictBadge;
        _conflictTooltip = conflictTooltip;
    }
}
