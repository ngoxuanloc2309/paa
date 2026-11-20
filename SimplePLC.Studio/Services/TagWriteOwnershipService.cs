using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private sealed class WeakAction
    {
        private readonly WeakReference? _targetRef;
        private readonly MethodInfo _method;

        public WeakAction(Action action)
        {
            if (action.Target != null)
            {
                _targetRef = new WeakReference(action.Target);
            }
            _method = action.Method;
        }

        public bool IsAlive => _targetRef == null || _targetRef.IsAlive;

        public bool Matches(Action action)
        {
            if (_method != action.Method) return false;
            if (_targetRef == null) return action.Target == null;
            return ReferenceEquals(_targetRef.Target, action.Target);
        }

        public bool TryInvoke()
        {
            if (_targetRef == null)
            {
                _method.Invoke(null, null);
                return true;
            }

            var target = _targetRef.Target;
            if (target != null)
            {
                _method.Invoke(target, null);
                return true;
            }

            return false;
        }
    }

    private sealed class WeakFunc
    {
        private readonly WeakReference? _targetRef;
        private readonly MethodInfo _method;

        public WeakFunc(Func<IEnumerable<TagClaimEntry>> func)
        {
            if (func.Target != null)
            {
                _targetRef = new WeakReference(func.Target);
            }
            _method = func.Method;
        }

        public bool IsAlive => _targetRef == null || _targetRef.IsAlive;

        public bool Matches(Func<IEnumerable<TagClaimEntry>> func)
        {
            if (_method != func.Method) return false;
            if (_targetRef == null) return func.Target == null;
            return ReferenceEquals(_targetRef.Target, func.Target);
        }

        public IEnumerable<TagClaimEntry>? TryInvoke()
        {
            if (_targetRef == null)
            {
                return (IEnumerable<TagClaimEntry>?)_method.Invoke(null, null);
            }

            var target = _targetRef.Target;
            if (target != null)
            {
                return (IEnumerable<TagClaimEntry>?)_method.Invoke(target, null);
            }

            return null;
        }
    }

    private static readonly Lazy<TagWriteOwnershipService> _instance = new(() => new TagWriteOwnershipService());

    /// <summary>
    /// Singleton instance của dịch vụ quản lý sở hữu Tag.
    /// </summary>
    public static TagWriteOwnershipService Instance => _instance.Value;

    private readonly List<WeakFunc> _providers = new();
    private readonly Dictionary<string, WeakFunc> _keyedProviders = new();
    private readonly List<WeakAction> _weakSubscribers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Sự kiện phát ra khi có bất kỳ kênh truyền thông nào thay đổi việc chọn Tag đích,
    /// kích hoạt các ViewModel Dropdown cập nhật lại danh sách TagOptions.
    /// Sử dụng mô hình Weak Event để tránh rò rỉ bộ nhớ.
    /// </summary>
    public event Action OwnershipChanged
    {
        add
        {
            if (value == null) return;
            lock (_lock)
            {
                _weakSubscribers.Add(new WeakAction(value));
            }
        }
        remove
        {
            if (value == null) return;
            lock (_lock)
            {
                for (int i = _weakSubscribers.Count - 1; i >= 0; i--)
                {
                    if (_weakSubscribers[i].Matches(value))
                    {
                        _weakSubscribers.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Đăng ký một nhà cung cấp danh sách chiếm dụng Tag (Modbus Port, MQTT Node,...).
    /// </summary>
    public void RegisterProvider(Func<IEnumerable<TagClaimEntry>> provider)
    {
        if (provider == null) return;
        lock (_lock)
        {
            if (!_providers.Any(p => p.Matches(provider)))
            {
                _providers.Add(new WeakFunc(provider));
            }
        }
    }

    /// <summary>
    /// Đăng ký nhà cung cấp chiếm dụng Tag theo khóa duy nhất (ví dụ: "ModbusPort_0").
    /// Nếu đã có nhà cung cấp trước đó cùng khóa, nhà cung cấp mới sẽ ghi đè ngay lập tức
    /// để ngăn chặn xung đột ma (phantom conflict) từ các ViewModel cũ chưa được GC thu hồi.
    /// </summary>
    /// <param name="key">Khóa định danh nguồn duy nhất (ví dụ "ModbusPort_0").</param>
    /// <param name="provider">Hàm ủy quyền cung cấp danh sách chiếm dụng.</param>
    public void RegisterKeyedProvider(string key, Func<IEnumerable<TagClaimEntry>> provider)
    {
        if (string.IsNullOrEmpty(key) || provider == null) return;
        lock (_lock)
        {
            _keyedProviders[key] = new WeakFunc(provider);
        }
    }

    /// <summary>
    /// Hủy đăng ký nhà cung cấp theo khóa duy nhất.
    /// </summary>
    /// <param name="key">Khóa định danh nguồn cần hủy.</param>
    public void UnregisterKeyedProvider(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        lock (_lock)
        {
            _keyedProviders.Remove(key);
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
            for (int i = _providers.Count - 1; i >= 0; i--)
            {
                if (_providers[i].Matches(provider))
                {
                    _providers.RemoveAt(i);
                    break;
                }
            }
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
            _keyedProviders.Clear();
            _weakSubscribers.Clear();
        }
    }

    /// <summary>
    /// Thông báo toàn hệ thống rằng quyền sở hữu Tag vừa có sự biến đổi.
    /// </summary>
    public void NotifyOwnershipChanged()
    {
        List<WeakAction> snapshot;
        lock (_lock)
        {
            _weakSubscribers.RemoveAll(s => !s.IsAlive);
            snapshot = new List<WeakAction>(_weakSubscribers);
        }

        foreach (var action in snapshot)
        {
            try
            {
                action.TryInvoke();
            }
            catch (Exception)
            {
                // Isolation defense: Ngoại lệ từ một ViewModel/Subscriber không được làm gián đoạn thông báo quyền sở hữu
            }
        }
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

        List<WeakFunc> snapshot;
        lock (_lock)
        {
            _providers.RemoveAll(p => !p.IsAlive);

            var deadKeys = _keyedProviders.Where(kvp => !kvp.Value.IsAlive).Select(kvp => kvp.Key).ToList();
            foreach (var deadKey in deadKeys)
            {
                _keyedProviders.Remove(deadKey);
            }

            snapshot = new List<WeakFunc>(_providers.Concat(_keyedProviders.Values));
        }

        foreach (var provider in snapshot)
        {
            IEnumerable<TagClaimEntry>? entries = null;
            try
            {
                entries = provider.TryInvoke();
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
