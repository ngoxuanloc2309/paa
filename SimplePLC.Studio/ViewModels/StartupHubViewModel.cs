using System.Collections.ObjectModel;
using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Các hành động khả dĩ được lựa chọn từ màn hình Startup Hub.
/// </summary>
public enum StartupHubAction
{
    None,
    NewProject,
    OpenLocalProject,
    OpenRecentProject,
    UploadFromDevice,
    ContinueBlank
}

/// <summary>
/// ViewModel chính cho màn hình khởi động tiêu chuẩn quốc tế (Startup Hub / Welcome Window).
/// </summary>
public partial class StartupHubViewModel : ObservableObject
{
    /// <summary>
    /// Số lượng dự án gần đây tối đa được hiển thị trên giao diện khởi động (tránh rối mắt).
    /// </summary>
    public const int MaxRecentProjectsDisplay = 5;

    public LocalizationService Loc => LocalizationService.Instance;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public ObservableCollection<RecentProjectItemViewModel> RecentProjects { get; } = new();

    public ObservableCollection<RecentProjectItemViewModel> FilteredRecentProjects { get; } = new();

    public ObservableCollection<string> AvailablePorts { get; } = new();

    [ObservableProperty]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    private bool _isUploading;

    [ObservableProperty]
    private string _uploadStatusMessage = string.Empty;

    [ObservableProperty]
    private StartupHubAction _selectedAction = StartupHubAction.None;

    [ObservableProperty]
    private string? _selectedRecentProjectPath;

    public bool HasRecentProjects => RecentProjects.Count > 0;

    public bool HasFilteredResults => FilteredRecentProjects.Count > 0;

    /// <summary>
    /// Sự kiện yêu cầu đóng cửa sổ Startup Hub và tiếp tục luồng công việc tương ứng.
    /// </summary>
    public event Action? RequestClose;

    /// <summary>
    /// Khởi tạo ViewModel của Startup Hub và nạp danh sách dự án gần đây cùng các cổng COM có sẵn.
    /// </summary>
    public StartupHubViewModel()
    {
        LoadRecentProjects();
        RefreshPorts();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Tải danh sách dự án gần đây từ ProjectFileService.
    /// </summary>
    public void LoadRecentProjects()
    {
        RecentProjects.Clear();
        var entries = ProjectFileService.GetRecentProjectEntries();
        foreach (var entry in entries)
        {
            var item = new RecentProjectItemViewModel(entry)
            {
                OnPinToggled = OnItemPinToggled,
                OnRemoveRequested = OnItemRemoveRequested,
                OnOpenRequested = OnItemOpenRequested
            };
            RecentProjects.Add(item);
        }

        ApplyFilter();
        OnPropertyChanged(nameof(HasRecentProjects));
    }

    /// <summary>
    /// Lọc danh sách dự án gần đây theo từ khóa tìm kiếm (tên dự án, đường dẫn, huy hiệu).
    /// </summary>
    public void ApplyFilter()
    {
        FilteredRecentProjects.Clear();
        var query = SearchText?.Trim() ?? string.Empty;

        var matches = (string.IsNullOrWhiteSpace(query)
            ? RecentProjects
            : RecentProjects.Where(p =>
                p.ProjectName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.FilePath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.HardwareBadge.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.HardwareTarget.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(MaxRecentProjectsDisplay);

        foreach (var m in matches)
        {
            FilteredRecentProjects.Add(m);
        }

        OnPropertyChanged(nameof(HasFilteredResults));
    }

    /// <summary>
    /// Quét các cổng COM vật lý và ảo sẵn có trên máy tính.
    /// </summary>
    [RelayCommand]
    public void RefreshPorts()
    {
        AvailablePorts.Clear();
        try
        {
            var ports = SerialPort.GetPortNames().OrderBy(p => p).ToList();
            foreach (var p in ports)
            {
                AvailablePorts.Add(p);
            }
        }
        catch
        {
            // Best effort
        }

        if (AvailablePorts.Count > 0)
        {
            if (string.IsNullOrEmpty(SelectedPort) || !AvailablePorts.Contains(SelectedPort))
            {
                SelectedPort = AvailablePorts[0];
            }
        }
        else
        {
            SelectedPort = string.Empty;
        }
    }

    private void OnItemPinToggled(RecentProjectItemViewModel item)
    {
        ProjectFileService.TogglePinRecentProject(item.FilePath);
        LoadRecentProjects();
    }

    private void OnItemRemoveRequested(RecentProjectItemViewModel item)
    {
        ProjectFileService.RemoveRecentProject(item.FilePath);
        LoadRecentProjects();
    }

    private void OnItemOpenRequested(RecentProjectItemViewModel item)
    {
        SelectedAction = StartupHubAction.OpenRecentProject;
        SelectedRecentProjectPath = item.FilePath;
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Khởi tạo dự án mới (mở trình hướng dẫn New Project).
    /// </summary>
    [RelayCommand]
    public void CreateNewProject()
    {
        SelectedAction = StartupHubAction.NewProject;
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Mở dự án có sẵn từ máy tính thông qua hộp thoại duyệt file.
    /// </summary>
    [RelayCommand]
    public void OpenLocalProject()
    {
        SelectedAction = StartupHubAction.OpenLocalProject;
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Mở một dự án cụ thể từ danh sách gần đây.
    /// </summary>
    [RelayCommand]
    public void OpenRecentProject(RecentProjectItemViewModel? item)
    {
        if (item == null) return;
        SelectedAction = StartupHubAction.OpenRecentProject;
        SelectedRecentProjectPath = item.FilePath;
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Kích hoạt đảo ngược logic và nạp toàn bộ cấu hình từ thiết bị phần cứng đang kết nối.
    /// </summary>
    [RelayCommand]
    public void UploadFromDevice()
    {
        SelectedAction = StartupHubAction.UploadFromDevice;
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Bỏ qua và vào thẳng không gian làm việc trống.
    /// </summary>
    [RelayCommand]
    public void ContinueBlank()
    {
        SelectedAction = StartupHubAction.ContinueBlank;
        RequestClose?.Invoke();
    }

    /// <summary>
    /// Đóng màn hình khởi động.
    /// </summary>
    [RelayCommand]
    public void Close()
    {
        SelectedAction = StartupHubAction.None;
        RequestClose?.Invoke();
    }
}
