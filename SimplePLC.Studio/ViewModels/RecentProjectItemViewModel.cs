using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel quản lý từng dòng dự án gần đây hiển thị trong Startup Hub Window.
/// </summary>
public partial class RecentProjectItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _projectName = string.Empty;

    [ObservableProperty]
    private string _hardwareTarget = string.Empty;

    [ObservableProperty]
    private string _hardwareBadge = "[PLC]";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RelativeTimeText))]
    private DateTime _lastOpened = DateTime.UtcNow;

    [ObservableProperty]
    private bool _isPinned;

    /// <summary>
    /// Chuỗi hiển thị thời gian mở tương đối (ví dụ: "Vừa xong", "Hôm nay, 14:32", "2 ngày trước").
    /// </summary>
    public string RelativeTimeText => FormatRelativeTime(LastOpened);

    /// <summary>
    /// Callback kích hoạt khi người dùng ghim hoặc bỏ ghim dự án.
    /// </summary>
    public Action<RecentProjectItemViewModel>? OnPinToggled { get; set; }

    /// <summary>
    /// Callback kích hoạt khi người dùng yêu cầu xóa dự án khỏi danh sách gần đây.
    /// </summary>
    public Action<RecentProjectItemViewModel>? OnRemoveRequested { get; set; }

    /// <summary>
    /// Callback kích hoạt khi người dùng chọn mở dự án này.
    /// </summary>
    public Action<RecentProjectItemViewModel>? OnOpenRequested { get; set; }

    /// <summary>
    /// Khởi tạo ViewModel từ thông tin mục dự án RecentProjectEntry.
    /// </summary>
    public RecentProjectItemViewModel(RecentProjectEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _filePath = entry.FilePath;
        _projectName = string.IsNullOrWhiteSpace(entry.ProjectName)
            ? System.IO.Path.GetFileNameWithoutExtension(entry.FilePath)
            : entry.ProjectName;
        _hardwareTarget = entry.HardwareTarget;
        _hardwareBadge = string.IsNullOrWhiteSpace(entry.HardwareBadge) ? "[PLC]" : entry.HardwareBadge;
        _lastOpened = entry.LastOpened;
        _isPinned = entry.IsPinned;
    }

    /// <summary>
    /// Lệnh đảo trạng thái ghim dự án lên đầu danh sách.
    /// </summary>
    [RelayCommand]
    public void TogglePin()
    {
        IsPinned = !IsPinned;
        OnPinToggled?.Invoke(this);
    }

    /// <summary>
    /// Lệnh xóa dự án khỏi danh sách mở gần đây.
    /// </summary>
    [RelayCommand]
    public void Remove()
    {
        OnRemoveRequested?.Invoke(this);
    }

    /// <summary>
    /// Lệnh mở dự án.
    /// </summary>
    [RelayCommand]
    public void Open()
    {
        OnOpenRequested?.Invoke(this);
    }

    /// <summary>
    /// Định dạng chuỗi thời gian tương đối hỗ trợ song ngữ Anh/Việt.
    /// </summary>
    public static string FormatRelativeTime(DateTime dt)
    {
        var local = dt.ToLocalTime();
        var span = DateTime.Now - local;
        bool isVi = LocalizationService.Instance.IsVietnamese;

        if (span.TotalMinutes < 1)
            return isVi ? "Vừa xong" : "Just now";
        if (span.TotalHours < 1)
            return isVi ? $"{(int)span.TotalMinutes} phút trước" : $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24 && local.Date == DateTime.Today)
            return isVi ? $"Hôm nay, {local:HH:mm}" : $"Today, {local:HH:mm}";
        if (span.TotalHours < 48 && local.Date == DateTime.Today.AddDays(-1))
            return isVi ? $"Hôm qua, {local:HH:mm}" : $"Yesterday, {local:HH:mm}";
        if (span.TotalDays < 7)
            return isVi ? $"{(int)span.TotalDays} ngày trước" : $"{(int)span.TotalDays}d ago";

        return local.ToString("yyyy-MM-dd HH:mm");
    }
}
