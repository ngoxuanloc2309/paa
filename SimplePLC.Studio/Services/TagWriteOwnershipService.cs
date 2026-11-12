using System;
using System.Collections.Generic;
using System.Linq;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Đại diện cho thông tin định danh của nguồn đang chiếm giữ độc quyền quyền ghi của một Tag.
/// </summary>
/// <param name="SourceType">Loại nguồn dữ liệu ("Modbus Read" hoặc "MQTT Subscribe").</param>
/// <param name="DisplayBadge">Nhãn ngắn gọn hiển thị trong danh sách chọn (ví dụ: "[⛔ Modbus Ch.1]").</param>
/// <param name="TooltipDetail">Thông tin chi tiết nguyên nhân không thể lựa chọn Tag.</param>
public sealed record TagClaimantInfo(
    string SourceType,
    string DisplayBadge,
    string TooltipDetail
);

/// <summary>
/// Bản ghi ánh xạ một Tag cụ thể với đối tượng chủ sở hữu (Owner) và thông tin hiển thị.
/// </summary>
public sealed record TagClaimEntry(
    TagModel Tag,
    object Owner,
    string SourceType,
    string DisplayBadge,
    string TooltipDetail
);

/// <summary>
/// Dịch vụ quản lý quyền sở hữu ghi độc quyền của Tag đối với các luồng dữ liệu truyền thông ngoại vi
/// (Modbus Master Read FC01..04 và MQTT Subscribe Dynamic Pins).
/// Tuân thủ Học thuyết First Principles: Ngăn chặn triệt để xung đột Race Condition và Data Thrashing
/// khi nhiều luồng ngoại vi cùng ghi vào một địa chỉ nhớ PLC.
/// </summary>
public sealed class TagWriteOwnershipService
{
    private static readonly Lazy<TagWriteOwnershipService> _instance = new(() => new TagWriteOwnershipService());

    /// <summary>
    /// Singleton instance của dịch vụ quản lý sở hữu Tag.
    /// </summary>
    public static TagWriteOwnershipService Instance => _instance.Value;

    private readonly List<Func<IEnumerable<TagClaimEntry>>> _providers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Sự kiện phát ra khi có bất kỳ kênh truyền thông nào thay đổi việc chọn Tag đích,
    /// kích hoạt các ViewModel Dropdown cập nhật lại danh sách TagOptions.
    /// </summary>
    public event Action? OwnershipChanged;

    /// <summary>
    /// Đăng ký một nhà cung cấp danh sách chiếm dụng Tag (Modbus Port, MQTT Node,...).
    /// </summary>
    public void RegisterProvider(Func<IEnumerable<TagClaimEntry>> provider)
    {
        if (provider == null) return;
        lock (_lock)
        {
            if (!_providers.Contains(provider))
            {
                _providers.Add(provider);
            }
        }
    }

    /// <summary>
    /// Hủy đăng ký nhà cung cấp.
    /// </summary>
    public void UnregisterProvider(Func<IEnumerable<TagClaimEntry>> provider)
    {
        if (provider == null) return;
        lock (_lock)
        {
            _providers.Remove(provider);
        }
    }

    /// <summary>
    /// Xóa sạch toàn bộ các nhà cung cấp đã đăng ký (phục vụ cách ly hoàn toàn trạng thái giữa các Unit Tests).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _providers.Clear();
        }
    }

    /// <summary>
    /// Thông báo toàn hệ thống rằng quyền sở hữu Tag vừa có sự biến đổi.
    /// </summary>
    public void NotifyOwnershipChanged()
    {
        OwnershipChanged?.Invoke();
    }

    /// <summary>
    /// Kiểm tra xem một Tag có đang bị chiếm dụng độc quyền bởi kênh đọc/nhận nào khác hay không.
    /// </summary>
    /// <param name="tag">Tag cần kiểm tra trạng thái chiếm giữ.</param>
    /// <param name="currentCaller">Đối tượng đang gọi kiểm tra (để áp dụng quy tắc tự miễn trừ Self-Exemption).</param>
    /// <returns>Thông tin người chiếm giữ nếu có xung đột, ngược lại trả về null.</returns>
    public TagClaimantInfo? GetClaimant(TagModel? tag, object? currentCaller)
    {
        if (tag == null || tag.Kind == Domain.Enums.TagKind.None || string.Equals(tag.Name, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        List<Func<IEnumerable<TagClaimEntry>>> snapshot;
        lock (_lock)
        {
            snapshot = new List<Func<IEnumerable<TagClaimEntry>>>(_providers);
        }

        foreach (var provider in snapshot)
        {
            IEnumerable<TagClaimEntry>? entries = null;
            try
            {
                entries = provider();
            }
            catch
            {
                // Bảo vệ không để lỗi từ một provider làm sập giao diện
            }

            if (entries == null) continue;

            foreach (var entry in entries)
            {
                if (entry.Owner == currentCaller)
                {
                    // Tự miễn trừ: chính kênh này đang giữ Tag này thì vẫn hợp lệ đối với nó
                    continue;
                }

                if (IsSameTag(entry.Tag, tag))
                {
                    return new TagClaimantInfo(entry.SourceType, entry.DisplayBadge, entry.TooltipDetail);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// So khớp 2 đối tượng TagModel xem có trỏ tới cùng một vùng nhớ PLC hay không.
    /// </summary>
    public static bool IsSameTag(TagModel? t1, TagModel? t2)
    {
        if (t1 == null || t2 == null) return false;
        if (t1.Kind == Domain.Enums.TagKind.None || t2.Kind == Domain.Enums.TagKind.None) return false;
        if (ReferenceEquals(t1, t2)) return true;
        if (t1.Kind == t2.Kind && t1.Index == t2.Index) return true;
        if (!string.IsNullOrEmpty(t1.Name) && string.Equals(t1.Name, t2.Name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
