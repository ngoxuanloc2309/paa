using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using DomainTagKind = SimplePLC.Domain.Enums.TagKind;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel quản lý màn hình Live Watch Table và Tag Inspector & Waveform Scope.
/// Nhận dữ liệu từ IRuntimeStateStore và cập nhật mượt mà lên giao diện theo chuẩn công nghiệp (TIA Portal / Codesys).
/// </summary>
public partial class LiveWatchViewModel : ObservableObject, IDisposable
{
    private readonly IRuntimeStateStore _stateStore;
    private readonly Dictionary<ushort, WatchTagItemModel> _tagLookup = new();
    private bool _isDisposed;

    public ObservableCollection<WatchTagItemModel> AllTags { get; } = new();
    public ObservableCollection<WatchTagItemModel> FilteredTags { get; } = new();

    [ObservableProperty]
    private WatchTagItemModel? _selectedTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchText))]
    private string _searchText = string.Empty;

    public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

    [RelayCommand]
    public void ClearSearch()
    {
        SearchText = string.Empty;
    }

    [ObservableProperty]
    private string _selectedKindFilter = "ALL";

    public int WatchlistCount => AllTags.Count(t => t.IsPinned);

    public string WatchlistButtonText
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            string prefix = isVi ? "⭐ Theo dõi" : "⭐ Watchlist";
            int count = WatchlistCount;
            return count > 0 ? $"{prefix} ({count})" : prefix;
        }
    }

    [RelayCommand]
    public void TogglePin(WatchTagItemModel? item)
    {
        if (item == null) return;
        item.IsPinned = !item.IsPinned;
        OnPropertyChanged(nameof(WatchlistCount));
        OnPropertyChanged(nameof(WatchlistButtonText));
        if (SelectedKindFilter == "WATCHLIST")
        {
            ApplyFilter();
        }
    }

    [RelayCommand]
    public void ClearWatchlist()
    {
        foreach (var t in AllTags)
        {
            t.IsPinned = false;
        }
        OnPropertyChanged(nameof(WatchlistCount));
        OnPropertyChanged(nameof(WatchlistButtonText));
        if (SelectedKindFilter == "WATCHLIST")
        {
            ApplyFilter();
        }
    }

    public void PinTagByIndex(ushort tagIndex)
    {
        if (_tagLookup.TryGetValue(tagIndex, out var item))
        {
            item.IsPinned = true;
            OnPropertyChanged(nameof(WatchlistCount));
            OnPropertyChanged(nameof(WatchlistButtonText));
            if (SelectedKindFilter == "WATCHLIST")
            {
                ApplyFilter();
            }
        }
    }

    public void UnpinTagByIndex(ushort tagIndex)
    {
        if (_tagLookup.TryGetValue(tagIndex, out var item))
        {
            item.IsPinned = false;
            OnPropertyChanged(nameof(WatchlistCount));
            OnPropertyChanged(nameof(WatchlistButtonText));
            if (SelectedKindFilter == "WATCHLIST")
            {
                ApplyFilter();
            }
        }
    }

    public List<int> GetWatchlistIndices()
    {
        return AllTags.Where(t => t.IsPinned).Select(t => (int)t.Index).ToList();
    }

    public void SetWatchlist(IEnumerable<int>? indices)
    {
        var set = indices != null ? new HashSet<int>(indices) : new HashSet<int>();
        foreach (var tag in AllTags)
        {
            tag.IsPinned = set.Contains(tag.Index);
        }
        OnPropertyChanged(nameof(WatchlistCount));
        OnPropertyChanged(nameof(WatchlistButtonText));
        if (SelectedKindFilter == "WATCHLIST")
        {
            ApplyFilter();
        }
    }

    // Diagnostics Overview
    [ObservableProperty]
    private string _uptimeText = "--";

    [ObservableProperty]
    private ushort _cpuLoad;

    [ObservableProperty]
    private ushort _ramUsage;

    [ObservableProperty]
    private uint _scanTime;

    [ObservableProperty]
    private uint _maxScanTime;

    [ObservableProperty]
    private string _resetReasonText = "--";

    [ObservableProperty]
    private string _healthFlagsText = "--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConnected))]
    private ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;

    public bool IsConnected => ConnectionStatus == ConnectionStatus.Connected;

    [ObservableProperty]
    private string _connectionStatusText = "Chưa kết nối";

    [ObservableProperty]
    private string _lastSuccessfulPollText = "--";

    [ObservableProperty]
    private string _footerSummaryText = string.Empty;

    // Diagnostic & Manual Commissioning (Wire Profile V2)
    private IDiagnosticController? _diagnosticController;
    private readonly bool _hasExplicitController;

    [ObservableProperty]
    private bool _isManualModeActive;

    [ObservableProperty]
    private ushort _leaseRemainingMs;

    [ObservableProperty]
    private string _leaseCountdownText = "--";

    [ObservableProperty]
    private bool _isRetainDirty;

    [ObservableProperty]
    private string _statusNotice = string.Empty;

    public LiveWatchViewModel(IRuntimeStateStore stateStore, IDiagnosticController? diagnosticController = null)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _hasExplicitController = diagnosticController != null;

        if (diagnosticController != null)
        {
            AttachDiagnosticController(diagnosticController);
        }
        else if (AppServices.Instance != null)
        {
            if (AppServices.Instance.SessionManager.HasActiveSession)
            {
                AttachDiagnosticController(AppServices.Instance.DiagnosticController);
            }
            AppServices.Instance.SessionManager.SessionChanged += OnSessionChanged;
        }

        _stateStore.SnapshotUpdated += OnSnapshotUpdated;
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        // Khởi tạo trạng thái ban đầu nếu store đã có snapshot
        SyncFromSnapshot(_stateStore.CurrentSnapshot);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedKindFilterChanged(string value) => ApplyFilter();

    [RelayCommand]
    public void SetKindFilter(string kind)
    {
        SelectedKindFilter = kind;
    }

    private void OnLanguageChanged()
    {
        RunOnUi(() =>
        {
            foreach (var item in AllTags)
            {
                item.NotifyLanguageChanged();
            }
            OnPropertyChanged(nameof(WatchlistButtonText));
            SyncFromSnapshot(_stateStore.CurrentSnapshot);
        });
    }

    private RuntimeDeviceSnapshot? _pendingSnapshot;
    private int _isUiScheduled; // 0 = idle, 1 = scheduled

    private void OnSnapshotUpdated(RuntimeDeviceSnapshot snapshot)
    {
        _pendingSnapshot = snapshot;
        if (Interlocked.Exchange(ref _isUiScheduled, 1) == 0)
        {
            RunOnUi(() =>
            {
                Interlocked.Exchange(ref _isUiScheduled, 0);
                var latest = _pendingSnapshot;
                if (latest != null)
                {
                    SyncFromSnapshot(latest);
                }
            });
        }
    }

    private void SyncFromSnapshot(RuntimeDeviceSnapshot snapshot)
    {
        // 1. Đồng bộ cấu trúc thẻ nếu số lượng hoặc thành phần thay đổi
        if (_tagLookup.Count != snapshot.Tags.Count)
        {
            var previouslyPinned = new HashSet<ushort>(AllTags.Where(t => t.IsPinned).Select(t => t.Index));
            AllTags.Clear();
            _tagLookup.Clear();

            foreach (var tag in snapshot.Tags)
            {
                var item = new WatchTagItemModel
                {
                    Index = tag.TagIndex,
                    Name = tag.Name,
                    Alias = tag.Alias,
                    Kind = tag.Kind,
                    DataType = tag.DataType,
                    RawValue = tag.RawValue,
                    Quality = tag.Quality,
                    LastUpdated = tag.LastSuccessfulUpdateAt,
                    IsPinned = previouslyPinned.Contains(tag.TagIndex)
                };
                AllTags.Add(item);
                _tagLookup[tag.TagIndex] = item;
            }

            OnPropertyChanged(nameof(WatchlistCount));
            OnPropertyChanged(nameof(WatchlistButtonText));
            ApplyFilter();
        }
        else
        {
            // Cập nhật giá trị nhanh cho từng tag
            foreach (var tag in snapshot.Tags)
            {
                if (_tagLookup.TryGetValue(tag.TagIndex, out var item))
                {
                    item.UpdateFromSnapshot(tag);
                }
            }
        }

        // 2. Đồng bộ Connection & Diagnostics theo đúng ngôn ngữ chuẩn 100%
        bool isVi = LocalizationService.Instance.IsVietnamese;
        ConnectionStatus = snapshot.ConnectionStatus;
        if (ConnectionStatus != ConnectionStatus.Connected && IsManualModeActive)
        {
            IsManualModeActive = false;
            foreach (var tag in AllTags)
            {
                tag.IsForced = false;
                tag.ForcedValue = null;
            }
        }

        ConnectionStatusText = snapshot.ConnectionStatus switch
        {
            ConnectionStatus.Connected => isVi ? "Đang trực tuyến" : "Online",
            ConnectionStatus.Connecting => isVi ? "Đang kết nối..." : "Connecting...",
            ConnectionStatus.Incompatible => isVi ? "Không tương thích" : "Incompatible",
            ConnectionStatus.Faulted => isVi ? "Lỗi truyền thông" : "Faulted",
            _ => isVi ? "Chưa kết nối" : "Offline"
        };

        LastSuccessfulPollText = snapshot.LastSuccessfulPollAt.HasValue
            ? snapshot.LastSuccessfulPollAt.Value.ToLocalTime().ToString("HH:mm:ss.fff")
            : "--";

        if (snapshot.Health != null)
        {
            var h = snapshot.Health;
            TimeSpan uptime = TimeSpan.FromSeconds(h.UptimeSeconds);
            UptimeText = uptime.TotalDays >= 1
                ? $"{(int)uptime.TotalDays}d {uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s"
                : $"{uptime.Hours:D2}h {uptime.Minutes:D2}m {uptime.Seconds:D2}s";

            CpuLoad = h.CpuLoadPercent;
            RamUsage = h.RamUsagePercent;
            ScanTime = h.ScanTimeMs;
            MaxScanTime = h.MaxScanTimeMs;
            ResetReasonText = TranslateResetReason(h.ResetReason, isVi);
            HealthFlagsText = h.HealthFlags == Protocol.Enums.SPLC_HealthFlags.NONE
                ? (isVi ? "Bình thường" : "Normal")
                : h.HealthFlags.ToString();
        }
        else
        {
            UptimeText = "--";
            CpuLoad = 0;
            RamUsage = 0;
            ScanTime = 0;
            MaxScanTime = 0;
            ResetReasonText = "--";
            HealthFlagsText = "--";
        }

        UpdateFooterSummary();
    }

    private static string TranslateResetReason(Protocol.Enums.SPLC_ResetReason reason, bool isVi) => reason switch
    {
        Protocol.Enums.SPLC_ResetReason.POWER_ON => isVi ? "Khởi động cấp nguồn" : "Power-On Reset",
        Protocol.Enums.SPLC_ResetReason.EXTERNAL => isVi ? "Nút Reset phần cứng" : "Hardware Pin Reset",
        Protocol.Enums.SPLC_ResetReason.SOFTWARE => isVi ? "Lệnh phần mềm" : "Software Command",
        Protocol.Enums.SPLC_ResetReason.WATCHDOG => isVi ? "Chó canh cổng Watchdog" : "Watchdog Timer",
        Protocol.Enums.SPLC_ResetReason.BROWNOUT => isVi ? "Bảo vệ sụt áp nguồn" : "Brownout Detector",
        _ => isVi ? "Không xác định" : "Unknown"
    };

    public void ApplyFilter()
    {
        var currentSelected = SelectedTag;
        FilteredTags.Clear();

        string query = SearchText.Trim();
        foreach (var tag in AllTags)
        {
            // Filter by Kind / Watchlist
            bool kindMatch = SelectedKindFilter switch
            {
                "WATCHLIST" => tag.IsPinned,
                "DI" => tag.Kind == DomainTagKind.DiscreteInput,
                "DO" => tag.Kind == DomainTagKind.DiscreteOutput,
                "AI" => tag.Kind == DomainTagKind.AnalogInput,
                "VFLAG" => tag.Kind == DomainTagKind.VirtualFlag,
                "VREG" => tag.Kind == DomainTagKind.VirtualRegister,
                "RETAIN" => tag.Kind == DomainTagKind.VirtualRegisterRetain,
                "COUNTER" => tag.Kind == DomainTagKind.Counter,
                "INTERNAL" => tag.Kind is DomainTagKind.VirtualFlag or DomainTagKind.VirtualRegister or DomainTagKind.VirtualRegisterRetain or DomainTagKind.Counter,
                _ => true
            };

            if (!kindMatch) continue;

            // Filter by Search text (hỗ trợ tìm theo Name, Alias, LocalizedAlias, Modbus Hex, DataType, Index)
            if (!string.IsNullOrEmpty(query))
            {
                bool searchMatch = tag.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   (tag.Alias != null && tag.Alias.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                                   tag.LocalizedAlias.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   tag.ModbusAddressHex.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   tag.DataTypeText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                   tag.Index.ToString().Contains(query);

                if (!searchMatch) continue;
            }

            FilteredTags.Add(tag);
        }

        if (currentSelected != null && FilteredTags.Contains(currentSelected))
        {
            SelectedTag = currentSelected;
        }
        else if (FilteredTags.Count > 0)
        {
            SelectedTag = FilteredTags[0];
        }
        else
        {
            SelectedTag = null;
        }

        UpdateFooterSummary();
    }

    public void UpdateFooterSummary()
    {
        bool isVi = LocalizationService.Instance.IsVietnamese;
        int total = AllTags.Count;
        int count = FilteredTags.Count;
        int onCount = 0;
        int offCount = 0;
        int goodCount = 0;

        foreach (var t in FilteredTags)
        {
            if (t.IsOn) onCount++;
            else if (t.IsBoolean) offCount++;

            if (t.Quality == TagQuality.Good) goodCount++;
        }

        if (isVi)
        {
            FooterSummaryText = $"Đang hiển thị: {count} / {total} Tags   •   Trạng thái: {onCount} BẬT  ·  {offCount} TẮT   •   Tín hiệu: {goodCount}/{count} TỐT   •   Làm mới bảng: 200 ms (5 Hz)";
        }
        else
        {
            FooterSummaryText = $"Displaying: {count} / {total} Tags   •   State: {onCount} ON  ·  {offCount} OFF   •   Signal: {goodCount}/{count} GOOD   •   UI Refresh: 200 ms (5 Hz)";
        }
    }

    private static void RunOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app != null && app.Dispatcher != null && !app.Dispatcher.HasShutdownStarted && !app.Dispatcher.CheckAccess())
        {
            try
            {
                app.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, action);
                return;
            }
            catch
            {
                // Fallback to direct execution
            }
        }
        action();
    }

    [RelayCommand]
    public async Task ToggleManualModeAsync()
    {
        if (!IsConnected)
        {
            System.Windows.MessageBox.Show(
                LocalizationService.Instance["UploadNoConnection"],
                LocalizationService.Instance["UploadFailedTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            return;
        }

        if (_diagnosticController == null) return;

        try
        {
            if (IsManualModeActive)
            {
                if (IsRetainDirty)
                {
                    var promptResult = System.Windows.MessageBox.Show(
                        LocalizationService.Instance["LiveWatchRetainDirtyNotice"],
                        LocalizationService.Instance["LiveWatchRetainDirtyBadge"],
                        System.Windows.MessageBoxButton.YesNoCancel,
                        System.Windows.MessageBoxImage.Warning);

                    if (promptResult == System.Windows.MessageBoxResult.Cancel)
                        return;

                    if (promptResult == System.Windows.MessageBoxResult.Yes)
                    {
                        await _diagnosticController.CommitRetainAsync();
                    }
                    else if (promptResult == System.Windows.MessageBoxResult.No)
                    {
                        await _diagnosticController.DiscardRetainAsync();
                    }
                }

                await _diagnosticController.ReleaseAllAsync();
                foreach (var tag in AllTags)
                {
                    tag.IsForced = false;
                    tag.ForcedValue = null;
                }
            }
            else
            {
                var confirmResult = System.Windows.MessageBox.Show(
                    LocalizationService.Instance["LiveWatchConfirmEnterManual"],
                    LocalizationService.Instance["LiveWatchConfirmEnterManualTitle"],
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (confirmResult == System.Windows.MessageBoxResult.Yes)
                {
                    await _diagnosticController.EnterManualModeAsync();
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                LocalizationService.Instance["LiveWatchErrorTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ForceTagBooleanAsync(object? parameter)
    {
        if (_diagnosticController == null || !IsManualModeActive) return;

        WatchTagItemModel? tag = null;
        int targetVal = 1;

        if (parameter is WatchTagItemModel m)
        {
            tag = m;
            targetVal = tag.IsOn ? 0 : 1;
        }
        else if (parameter is object[] arr && arr.Length >= 2 && arr[0] is WatchTagItemModel item)
        {
            tag = item;
            targetVal = arr[1]?.ToString() == "1" ? 1 : 0;
        }

        if (tag != null)
        {
            try
            {
                await _diagnosticController.ForceTagAsync(tag.Index, targetVal);
                tag.ApplyManualOverride(targetVal);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    LocalizationService.Instance["LiveWatchErrorTitle"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public async Task ForceTagValueAsync(WatchTagItemModel? tag)
    {
        if (_diagnosticController == null || !IsManualModeActive || tag == null) return;

        if (int.TryParse(tag.ManualInputValue, out int val))
        {
            try
            {
                await _diagnosticController.ForceTagAsync(tag.Index, val);
                tag.ApplyManualOverride(val);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    ex.Message,
                    LocalizationService.Instance["LiveWatchErrorTitle"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public async Task ReleaseTagAsync(WatchTagItemModel? tag)
    {
        if (_diagnosticController == null || !IsManualModeActive || tag == null) return;

        try
        {
            await _diagnosticController.ReleaseTagAsync(tag.Index);
            tag.ClearManualOverride();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                LocalizationService.Instance["LiveWatchErrorTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task ReleaseAllAsync()
    {
        if (_diagnosticController == null) return;

        try
        {
            await _diagnosticController.ReleaseAllAsync();
            foreach (var tag in AllTags)
            {
                tag.IsForced = false;
                tag.ForcedValue = null;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                LocalizationService.Instance["LiveWatchErrorTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task CommitRetainAsync()
    {
        if (_diagnosticController == null || !IsManualModeActive) return;
        try
        {
            await _diagnosticController.CommitRetainAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                LocalizationService.Instance["LiveWatchErrorTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task DiscardRetainAsync()
    {
        if (_diagnosticController == null || !IsManualModeActive) return;
        try
        {
            await _diagnosticController.DiscardRetainAsync();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                ex.Message,
                LocalizationService.Instance["LiveWatchErrorTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void OnSessionChanged(SimplePLC.Application.Abstractions.IDeviceSession? session)
    {
        if (_hasExplicitController) return;

        RunOnUi(() =>
        {
            if (session != null)
            {
                AttachDiagnosticController(AppServices.Instance?.DiagnosticController);
            }
            else
            {
                DetachDiagnosticController();
                IsManualModeActive = false;
                LeaseRemainingMs = 0;
                LeaseCountdownText = "--";
                IsRetainDirty = false;
                foreach (var tag in AllTags)
                {
                    tag.IsForced = false;
                    tag.ForcedValue = null;
                }
            }
        });
    }

    private void AttachDiagnosticController(IDiagnosticController? controller)
    {
        if (ReferenceEquals(_diagnosticController, controller)) return;

        DetachDiagnosticController();

        _diagnosticController = controller;
        if (_diagnosticController != null)
        {
            _diagnosticController.StatusChanged += OnDiagStatusChanged;
            _diagnosticController.TagForced += OnDiagTagForced;
            _diagnosticController.TagReleased += OnDiagTagReleased;
            _diagnosticController.AllTagsReleased += OnDiagAllTagsReleased;
        }
    }

    private void DetachDiagnosticController()
    {
        if (_diagnosticController != null)
        {
            _diagnosticController.StatusChanged -= OnDiagStatusChanged;
            _diagnosticController.TagForced -= OnDiagTagForced;
            _diagnosticController.TagReleased -= OnDiagTagReleased;
            _diagnosticController.AllTagsReleased -= OnDiagAllTagsReleased;
            _diagnosticController = null;
        }
    }

    private void OnDiagStatusChanged(SimplePLC.Protocol.Dto.DiagnosticStatusDto status)
    {
        RunOnUi(() =>
        {
            IsManualModeActive = status.IsDiagControl;
            LeaseRemainingMs = status.LeaseRemainingMs;
            LeaseCountdownText = $"{status.LeaseRemainingMs / 1000.0:F1}s";
            IsRetainDirty = status.IsRetainDirty;

            if (!status.IsDiagControl)
            {
                foreach (var tag in AllTags)
                {
                    tag.IsForced = false;
                    tag.ForcedValue = null;
                }
            }
        });
    }

    private void OnDiagTagForced(ushort tagIndex, int value)
    {
        RunOnUi(() =>
        {
            if (_tagLookup.TryGetValue(tagIndex, out var tag))
            {
                tag.ApplyManualOverride(value);
            }
        });
    }

    private void OnDiagTagReleased(ushort tagIndex)
    {
        RunOnUi(() =>
        {
            if (_tagLookup.TryGetValue(tagIndex, out var tag))
            {
                tag.ClearManualOverride();
            }
        });
    }

    private void OnDiagAllTagsReleased()
    {
        RunOnUi(() =>
        {
            foreach (var tag in AllTags)
            {
                tag.IsForced = false;
                tag.ForcedValue = null;
            }
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (!_hasExplicitController && AppServices.Instance?.SessionManager != null)
        {
            AppServices.Instance.SessionManager.SessionChanged -= OnSessionChanged;
        }

        DetachDiagnosticController();

        _stateStore.SnapshotUpdated -= OnSnapshotUpdated;
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }
}
