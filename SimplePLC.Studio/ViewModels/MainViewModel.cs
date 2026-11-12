using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Models;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.Ports;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Bảng chỉ mục Tab tập trung của SynaptiX IDE (Single Source of Truth).
/// </summary>
public static class AppTabIndices
{
    public const int Device = 0;
    public const int LogicGraph = 1;
    public const int TagCatalog = 2;
    public const int RuleTable = 3;
    public const int Blueprints = 4;
    public const int Deploy = 5;
    public const int LiveWatch = 6;
    public const int Simulator = 7;
}

public partial class MainViewModel : ObservableObject
{
    public LocalizationService Loc => LocalizationService.Instance;

    public TagCatalogViewModel TagCatalogVM { get; }
    public LogicEditorViewModel LogicEditorVM { get; }
    public RuleTableViewModel RuleTableVM { get; }
    public BlueprintsViewModel BlueprintsVM { get; }
    public DeployViewModel DeployVM { get; }
    public SimulatorViewModel SimulatorVM { get; }
    public LiveWatchViewModel LiveWatchVM { get; }
    public AiChatViewModel AiChatVM { get; }
    public StartupHubViewModel StartupHubVM { get; }

    [ObservableProperty]
    private DtuGatewayStudioViewModel _dtuGatewayStudioVM;

    /// <summary>
    /// Cho biết màn hình hiện tại có phải là Dashboard Khởi động (Start Page) hay không.
    /// </summary>
    public bool IsStartPageActive => CurrentView is StartupHubViewModel;

    public bool IsDtuProject => CurrentProjectMetadata?.DeviceClass == (ushort)SimplePLC.Domain.Enums.SPLC_DeviceClass.DTU;


    public bool HasSystemTab => DtuGatewayStudioVM?.HasSystemCapability ?? false;

    public bool IsMqttSupported => (AppServices.Instance.CurrentProduct?.SupportsMqttPublish ?? false) || 
                                   (AppServices.Instance.CurrentProduct?.SupportsMqttSubscribe ?? false) ||
                                   (DtuGatewayStudioVM?.HasMqttCapability ?? false);

    public bool IsWifiSupported => (AppServices.Instance.CurrentProduct?.SupportsWifi ?? false) || 
                                   (DtuGatewayStudioVM?.HasWifiCapability ?? false);

    public bool IsRs485Supported => (AppServices.Instance.CurrentProduct?.PhysicalRs485PortCount ?? 0) > 0 || 
                                    (AppServices.Instance.CurrentProduct?.SupportsModbusMaster ?? false) || 
                                    (AppServices.Instance.CurrentProduct?.SupportsModbusSlave ?? false) ||
                                    (DtuGatewayStudioVM?.HasRs485Capability ?? false);

    public bool IsRtcClockSupported => (AppServices.Instance.CurrentProduct?.SupportsRtcClock ?? false) || 
                                       (DtuGatewayStudioVM?.CurrentCapabilities.HasFlag(SimplePLC.Domain.Enums.SPLC_HardwareCapabilities.RTC) ?? false);

    public void NotifyHardwareCapabilities()
    {
        OnPropertyChanged(nameof(HasSystemTab));
        OnPropertyChanged(nameof(IsMqttSupported));
        OnPropertyChanged(nameof(IsWifiSupported));
        OnPropertyChanged(nameof(IsRs485Supported));
        OnPropertyChanged(nameof(IsRtcClockSupported));
        BlueprintsVM?.RefreshHardwareCompatibility(AppServices.Instance.CurrentProduct);
    }

    public ObservableCollection<string> AvailablePorts { get; } = new();
    public ObservableCollection<PortOption> PortOptions { get; } = new();

    [ObservableProperty]
    private string _portMenuHeader = "Cổng kết nối";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _selectedPort = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnModeText))]
    [NotifyPropertyChangedFor(nameof(ConnModeIcon))]
    [NotifyPropertyChangedFor(nameof(IsUsbMode))]
    private bool _isTcpMode;

    public bool IsUsbMode
    {
        get => !IsTcpMode;
        set
        {
            if (IsConnected) return;
            if (value && IsTcpMode)
            {
                IsTcpMode = false;
                UpdateConnectionStatus();
            }
        }
    }

    [ObservableProperty]
    private string _tcpHost = "127.0.0.1";

    [ObservableProperty]
    private int _tcpPort = 502;

    public string ConnModeText => IsTcpMode ? "TCP" : "RTU/USB";
    public string ConnModeIcon => IsTcpMode ? "🌐" : "🔌";

    [RelayCommand]
    public void SelectUsbMode()
    {
        if (IsConnected) return;
        if (IsTcpMode)
        {
            IsTcpMode = false;
            UpdateConnectionStatus();
        }
    }

    [RelayCommand]
    public void SelectTcpMode()
    {
        if (IsConnected) return;
        if (!IsTcpMode)
        {
            IsTcpMode = true;
            UpdateConnectionStatus();
        }
    }

    [RelayCommand]
    public void ToggleTcpMode()
    {
        if (IsConnected) return;
        IsTcpMode = !IsTcpMode;
        UpdateConnectionStatus();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(CanSelectPort))]
    [NotifyPropertyChangedFor(nameof(ConnectionBadgeText))]
    [NotifyPropertyChangedFor(nameof(CanDoDeviceAction))]
    [NotifyCanExecuteChangedFor(nameof(UploadFromDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(RebootDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(FactoryResetDeviceCommand))]
    [NotifyCanExecuteChangedFor(nameof(ToggleConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(SyncMcuClockCommand))]
    private bool _isConnected;

    public bool CanSelectPort => !IsConnected;
    public bool CanDoDeviceAction => IsConnected || AppServices.Instance.SessionManager.HasActiveSession || AppServices.Instance.LifecycleManager.CurrentState == ConnectionLifecycleState.Connected;

    [ObservableProperty]
    private ConnectionState _connectionState = ConnectionState.Disconnected;

    [ObservableProperty]
    private CompileState _compileState = CompileState.NotCompiled;

    [ObservableProperty]
    private string _runtimeStatusText = "Simulator idle";

    [ObservableProperty]
    private string _connectionStatusText = string.Empty;

    [ObservableProperty]
    private string _connectActionText = "⚡ Connect Device";

    [ObservableProperty]
    private string _connectButtonBackground = "#107C41";

    [ObservableProperty]
    private string _connectButtonBorderBrush = "#0E6B37";

    private object? _currentView;

    /// <summary>
    /// View đang hoạt động trên màn hình chính của ứng dụng.
    /// </summary>
    public object? CurrentView
    {
        get => _currentView;
        set
        {
            if (!ReferenceEquals(_currentView, value))
            {
                _currentView = value;
                OnPropertyChanged(nameof(CurrentView));
                OnPropertyChanged(nameof(IsStartPageActive));
                UpdateTagPollingState();
            }
        }
    }

    [ObservableProperty]
    private int _selectedTabIndex = 0;

    [ObservableProperty]
    private string _connectionInfo = string.Empty;

    [ObservableProperty]
    private string _deviceStatusText = string.Empty;

    [ObservableProperty]
    private string _deviceStatusColor = "#107C41";

    [ObservableProperty]
    private string _lifecycleStatusText = string.Empty;

    [ObservableProperty]
    private string _firmwareInfo = "No device";

    [ObservableProperty]
    private string _diagnosticsSummary = string.Empty;

    [ObservableProperty]
    private string _deviceProductBadgeText = "Remote I/O";

    [ObservableProperty]
    private string _deviceProductToolTip = string.Empty;

    [ObservableProperty]
    private string _mcuClockDisplayText = "--:--";

    [ObservableProperty]
    private bool _isMcuClockSynced;

    [ObservableProperty]
    private bool _hasHardwareRtc;

    [ObservableProperty]
    private bool _isRtcBatteryLow;

    [ObservableProperty]
    private string _rtcStatusTooltip = string.Empty;

    private DeviceDescriptorDto? _lastDescriptor;
    private DeviceResourceInfoDto? _lastResourceInfo;

    private SimplePLC.Application.Models.DeviceHealthInfo? _lastHealthInfo;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string? _currentProjectPath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private string _currentProjectName = "Untitled";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isProjectDirty;

    [ObservableProperty]
    private ProjectMetadata _currentProjectMetadata = new();

    public Func<NewProjectViewModel, bool?>? ShowNewProjectDialogHandler { get; set; }

    /// <summary>
    /// Delegate hook to intercept Device Info dialog in headless tests.
    /// </summary>
    public Action<DeviceInfoViewModel>? ShowDeviceInfoDialogHandler { get; set; }

    /// <summary>
    /// Delegate hook to intercept Startup Hub dialog in headless tests.
    /// </summary>
    public Func<StartupHubViewModel, bool?>? ShowStartupHubDialogHandler { get; set; }

    public string WindowTitle
    {
        get
        {
            string dirty = IsProjectDirty ? "*" : "";
            string port = IsConnected ? SelectedPort : (Loc.IsVietnamese ? "Chưa kết nối" : "Offline");
            return $"SynaptiX IDE - {CurrentProjectName}{dirty} [{port}]";
        }
    }

    public ObservableCollection<string> RecentProjects { get; } = new();

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _updateAvailableText = string.Empty;

    public AppUpdateService UpdateService { get; } = new();

    public void RefreshRecentProjects()
    {
        RecentProjects.Clear();
        foreach (var p in ProjectFileService.GetRecentProjects())
        {
            RecentProjects.Add(p);
        }
    }

    public MainViewModel(Action<Action>? uiDispatcher = null)
    {
        TagCatalogVM = new TagCatalogViewModel();
        RuleTableVM = new RuleTableViewModel(TagCatalogVM, (tabIndex) => SelectTab(tabIndex));
        LogicEditorVM = new LogicEditorViewModel(TagCatalogVM, RuleTableVM, (tabIndex) => SelectTab(tabIndex), uiDispatcher: uiDispatcher)
        {
            GetProjectMetadata = () => CurrentProjectMetadata,
            OpenDeviceModbusPortConfig = () => OpenModbusPortConfig(),
            OpenDeviceMqttBrokerConfig = () => OpenMqttBrokerConfig()
        };
        RuleTableVM.OnEditRuleRequested = (rule) => LogicEditorVM.LoadRuleToCanvas(rule);
        RuleTableVM.OnNewRuleRequested = () => LogicEditorVM.NewRuleCanvas();
        RuleTableVM.OnUploadFromDeviceRequested = () => UploadFromDeviceAsync();
        RuleTableVM.OnOpenModbusPortSettingsRequested = () => OpenModbusPortConfig();
        RuleTableVM.OnOpenMqttBrokerSettingsRequested = () => OpenMqttBrokerConfig();
        BlueprintsVM = new BlueprintsViewModel(LogicEditorVM, () => SelectTab(AppTabIndices.LogicGraph));
        DeployVM = new DeployViewModel(deployUseCase: null, RuleTableVM, LogicEditorVM, (tabIndex) => SelectTab(tabIndex), () => CurrentProjectMetadata);
        SimulatorVM = new SimulatorViewModel(TagCatalogVM, RuleTableVM, LogicEditorVM);
        LiveWatchVM = new LiveWatchViewModel(AppServices.Instance.StateStore)
        {
            ProjectAliasProvider = tagIndex =>
            {
                var tag = TagCatalogVM.AllTags.FirstOrDefault(t => t.Index == tagIndex);
                return (tag != null && tag.Kind != TagKind.None && !string.IsNullOrWhiteSpace(tag.Alias) && tag.Alias != "Không chọn" && tag.Alias != "NONE" && tag.Alias != "— Không chọn —") ? tag.Alias : null;
            },
            ProjectDataTypeProvider = tagIndex =>
            {
                var tag = TagCatalogVM.AllTags.FirstOrDefault(t => t.Index == tagIndex);
                return tag?.DataType;
            }
        };
        AiChatVM = new AiChatViewModel(
            () => TagCatalogVM?.AllTags,
            () => RuleTableVM?.Rules,
            () => LogicEditorVM);
        DtuGatewayStudioVM = new DtuGatewayStudioViewModel(CurrentProjectMetadata, TagCatalogVM, LogicEditorVM);
        WireDtuGatewayStudioVM(DtuGatewayStudioVM);
        AiChatVM.OnApplyRulesRequested = (specs, msg) => ApplyAiRulesToTable(specs, msg);
        AiChatVM.OnNavigateToTab = (tabIndex) => SelectTab(tabIndex);

        StartupHubVM = new StartupHubViewModel();
        StartupHubVM.RequestClose += () => ExecuteStartupHubAction(StartupHubVM);

        _currentView = StartupHubVM;

        RefreshPorts();
        RefreshRecentProjects();
        UpdateLanguageTexts();

        LogicEditorVM.PropertyChanged += OnLogicEditorPropertyChanged;
        LogicEditorVM.Nodes.CollectionChanged += (s, e) => IsProjectDirty = true;
        LogicEditorVM.Connections.CollectionChanged += (s, e) => IsProjectDirty = true;
        TagCatalogVM.AllTags.CollectionChanged += (s, e) => IsProjectDirty = true;

        // D3.3: Subscribe lifecycle state changes → update TopBar UX reactively
        AppServices.Instance.LifecycleManager.StateChanged += OnLifecycleStateChanged;
        AppServices.Instance.SessionManager.SessionChanged += OnSessionChanged;
        AppServices.Instance.MonitorService.TagsUpdated += OnRuntimeTagsUpdated;
        AppServices.Instance.MonitorService.RtcUpdated += OnRuntimeRtcUpdated;

        Loc.LanguageChanged += () =>
        {
            UpdateLanguageTexts();
            OnPropertyChanged(nameof(WindowTitle));
        };

        InitializeProjectOnStartup();

        // Kiểm tra bản cập nhật ngầm sau khi khởi động và tự động mở hộp thoại nếu có phiên bản mới
        _ = Task.Run(async () =>
        {
            await Task.Delay(2500);
            try
            {
                var info = await UpdateService.CheckForUpdateAsync();
                if (info.HasUpdate)
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        IsUpdateAvailable = true;
                        UpdateAvailableText = $"⚡ v{info.LatestVersion}";

                        // Tự động mở hộp thoại thông báo cập nhật công nghiệp (UpdateDialog) cho kỹ sư
                        var mainWindow = System.Windows.Application.Current?.MainWindow;
                        if (mainWindow != null && mainWindow.IsVisible)
                        {
                            var dialog = new Views.UpdateDialogView(info, UpdateService);
                            dialog.ShowDialog();
                        }
                    });
                }
            }
            catch { }
        });
    }

    private void InitializeProjectOnStartup()
    {
        // 1. Kiểm tra tham số dòng lệnh xem ứng dụng có được mở bằng cách nhấp đúp vào file (.synx) từ Windows Explorer không
        try
        {
            var args = Environment.GetCommandLineArgs();
            if (args != null && args.Length > 1)
            {
                for (int i = 1; i < args.Length; i++)
                {
                    string candidate = args[i].Trim('"');
                    if (System.IO.File.Exists(candidate) &&
                        (candidate.EndsWith(".synx", StringComparison.OrdinalIgnoreCase) ||
                         candidate.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
                    {
                        LoadProjectFile(candidate);
                        return;
                    }
                }
            }
        }
        catch
        {
            // Fallback an toàn nếu có ngoại lệ khi đọc tham số dòng lệnh
        }

        // 2. Mặc định: Ứng dụng khởi động trong trạng thái dự án mới tinh nếu không truyền file
        CurrentProjectPath = null;
        CurrentProjectName = Loc.IsVietnamese ? "Dự án mới" : "Untitled";
        CurrentProjectMetadata = new ProjectMetadata();
        LogicEditorVM.ClearCanvas();
        RuleTableVM.Rules.Clear();
        RuleTableVM.ApplyFilter();
        IsProjectDirty = false;
        UpdateDeviceProductInfo();
        NotifyHardwareCapabilities();

        CurrentView = StartupHubVM;
    }


    private void OnLogicEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogicEditorViewModel.CompileState)
            or nameof(LogicEditorViewModel.ValidRuleCount)
            or nameof(LogicEditorViewModel.Diagnostics))
            UpdateLanguageTexts();

        if (e.PropertyName == nameof(LogicEditorViewModel.IsLiveDebugActive))
            UpdateTagPollingState();
    }

    public void UpdateTagPollingState()
    {
        bool needPolling = (CurrentView == LiveWatchVM) || (CurrentView == LogicEditorVM && LogicEditorVM.IsLiveDebugActive);
        if (AppServices.Instance.MonitorService != null)
        {
            AppServices.Instance.MonitorService.IsTagPollingEnabled = needPolling;
        }
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        UpdateTagPollingState();
    }

    [RelayCommand]
    public void ToggleAiCopilot()
    {
        AiChatVM.TogglePanel();
    }

    private readonly IComPortDiscoveryService _portDiscoveryService = new SystemComPortDiscoveryService();

    [RelayCommand]
    public void RefreshPorts()
    {
        var existing = SelectedPort;
        AvailablePorts.Clear();

        try
        {
            var ports = _portDiscoveryService.GetAvailablePorts();
            foreach (var p in ports)
            {
                AvailablePorts.Add(p);
            }
        }
        catch
        {
            // Ignore port read errors
        }


        // Always provide Simulator option for developer/desktop simulation
        const string simPort = "SIMULATOR (VIRTUAL)";
        if (!AvailablePorts.Contains(simPort))
        {
            AvailablePorts.Insert(0, simPort);
        }

        if (!string.IsNullOrEmpty(existing) && AvailablePorts.Contains(existing))
        {
            SelectedPort = existing;
        }
        else
        {
            SelectedPort = AvailablePorts.FirstOrDefault() ?? string.Empty;
        }

        SyncPortOptions();
        UpdateConnectionStatus();
    }

    private void SyncPortOptions()
    {
        PortOptions.Clear();
        if (AvailablePorts.Count == 0)
        {
            PortOptions.Add(new PortOption
            {
                Name = Loc.IsVietnamese ? "(Không tìm thấy cổng COM)" : "(No COM ports found)",
                IsSelected = false,
                IsEnabled = false
            });
        }
        else
        {
            foreach (var p in AvailablePorts)
            {
                PortOptions.Add(new PortOption
                {
                    Name = p,
                    IsSelected = (p == SelectedPort),
                    IsEnabled = true
                });
            }
        }
    }

    [RelayCommand]
    public void SelectPort(string? port)
    {
        if (!string.IsNullOrEmpty(port) && AvailablePorts.Contains(port))
        {
            SelectedPort = port;
        }
    }

    [RelayCommand]
    public async Task ToggleConnect()
    {
        var lifecycle = AppServices.Instance.LifecycleManager;

        if (IsConnected || lifecycle.CurrentState == ConnectionLifecycleState.Reconnecting || lifecycle.CurrentState == ConnectionLifecycleState.Connecting)
        {
            // D3.3: Manual disconnect or cancel pending reconnection → LifecycleManager
            using var disconnectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await lifecycle.DisconnectAsync(disconnectCts.Token);
            }
            catch { }
            IsConnected = false;
            ConnectionState = ConnectionState.Disconnected;
            FirmwareInfo = Loc.IsVietnamese ? "Chưa có thiết bị" : "No device";
            if (LogicEditorVM.IsLiveDebugActive)
            {
                LogicEditorVM.IsLiveDebugActive = false;
            }
            LogicEditorVM.ClearSimVisuals();
            UpdateConnectionStatus();
            NotifyAllDeviceCommands();
        DtuGatewayStudioVM?.UpdateHardwareConnectionState(IsConnected, SelectedPort, DeviceProductBadgeText);
            // Khôi phục profile tự do (Sandbox Design Mode) để người dùng mở khóa toàn bộ các khối khi ngoại tuyến
            TagCatalogVM.SyncWithProductDefinition(TagCatalogVM.BuildProductFromCurrentTags());
            return;
        }

        DeviceEndpoint endpoint;
        if (IsTcpMode)
        {
            if (string.IsNullOrWhiteSpace(TcpHost))
            {
                ShowNotification(
                    Loc.IsVietnamese ? "Vui lòng nhập địa chỉ IP hoặc Host Modbus TCP." : "Please enter a valid Modbus TCP IP or Host.",
                    Loc.IsVietnamese ? "Chưa nhập địa chỉ IP" : "Host Required",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }
            int port = TcpPort > 0 && TcpPort <= 65535 ? TcpPort : 502;
            endpoint = new TcpEndpoint(TcpHost.Trim(), port);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(SelectedPort))
            {
                ShowNotification(
                    Loc.IsVietnamese ? "Vui lòng chọn cổng COM trước khi kết nối." : "Please select a COM port before connecting.",
                    Loc.IsVietnamese ? "Chưa chọn cổng" : "Port Required",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            AppServices.Instance.ConfigureForPort(SelectedPort);

            // D3.3: Connect delegate toàn bộ cho LifecycleManager
            endpoint = SelectedPort == "SIMULATOR (VIRTUAL)"
                ? new SimulatorEndpoint()
                : new UsbCdcEndpoint(SelectedPort);
        }

        using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            bool success = await lifecycle.ConnectAsync(endpoint, ct: connectCts.Token);

            if (success)
            {
                // SelectedPort / TcpHost được cập nhật từ CurrentEndpoint của lifecycle (confirmed by handshake)
                var confirmedEndpoint = lifecycle.CurrentEndpoint;
                if (confirmedEndpoint is UsbCdcEndpoint usbEp)
                {
                    SelectedPort = usbEp.PortName;
                }
                else if (confirmedEndpoint is SimulatorEndpoint)
                {
                    SelectedPort = "SIMULATOR (VIRTUAL)";
                }
                else if (confirmedEndpoint is TcpEndpoint tcpEp)
                {
                    TcpHost = tcpEp.Host;
                    TcpPort = tcpEp.Port;
                }

                // Cập nhật firmware info từ session descriptor nếu có
                var session = AppServices.Instance.SessionManager.CurrentSession;
                if (session?.Descriptor != null)
                {
                    var d = session.Descriptor;
                    FirmwareInfo = $"HW {d.HwVersionMajor}.{d.HwVersionMinor}.{d.HwVersionPatch} · FW {d.FwVersionMajor}.{d.FwVersionMinor}.{d.FwVersionPatch}";
                    UpdateDeviceProductInfo(d, session.ResourceInfo);
                }
                else
                {
                    UpdateDeviceProductInfo(null);
                }

                if (session?.Product != null)
                {
                    AppServices.Instance.CurrentProduct = session.Product;
                    TagCatalogVM.SyncWithProductDefinition(session.Product);
                    DtuGatewayStudioVM?.SyncWithHardwareProduct(session.Product);
                }
                DtuGatewayStudioVM?.UpdateHardwareConnectionState(true, SelectedPort, DeviceProductBadgeText);
                NotifyHardwareCapabilities();

                // Subscribe health updates từ monitor
                AppServices.Instance.MonitorService.HealthUpdated += OnRuntimeHealthUpdated;
            }
            else
            {
                var reason = lifecycle.FailureReason;
                var failureDetail = lifecycle.LastMessage;
                string msg;

                if (Loc.IsVietnamese)
                {
                    string cleanDetail;
                    if (string.IsNullOrWhiteSpace(failureDetail))
                    {
                        cleanDetail = "Không thể thiết lập kết nối tới thiết bị.";
                    }
                    else if (failureDetail.Contains("calling thread", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanDetail = "Lỗi xung đột luồng giao diện hệ thống.";
                    }
                    else if (failureDetail.Contains("denied", StringComparison.OrdinalIgnoreCase) || failureDetail.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanDetail = "Cổng COM đang bị một chương trình khác chiếm dụng hoặc bị từ chối truy cập.";
                    }
                    else if (failureDetail.Contains("timeout", StringComparison.OrdinalIgnoreCase) || failureDetail.Contains("timed out", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanDetail = "Hết thời gian chờ phản hồi từ thiết bị.";
                    }
                    else if (failureDetail.Contains("not found", StringComparison.OrdinalIgnoreCase) || failureDetail.Contains("does not exist", StringComparison.OrdinalIgnoreCase))
                    {
                        cleanDetail = "Không tìm thấy thiết bị tại cổng đã chọn.";
                    }
                    else
                    {
                        cleanDetail = failureDetail;
                    }

                    bool isTimeout = reason == ConnectionFailureReason.Timeout ||
                        (!string.IsNullOrWhiteSpace(failureDetail) && (failureDetail.Contains("timeout", StringComparison.OrdinalIgnoreCase) || failureDetail.Contains("timed out", StringComparison.OrdinalIgnoreCase)));

                    if (isTimeout)
                    {
                        msg = $"Hết thời gian chờ phản hồi từ thiết bị tại {SelectedPort} (Timeout).\n\n" +
                              "💡 Mẹo:\n" +
                              "• Nếu bạn muốn chạy mô phỏng không cần thiết bị: Hãy chọn cổng \"SIMULATOR (VIRTUAL)\" trên thanh công cụ rồi bấm Kết nối, hoặc nhấn phím F5 để mô phỏng trực tiếp trên sơ đồ Logic.\n" +
                              "• Nếu bạn đang kết nối thiết bị thật: Vui lòng kiểm tra lại cáp kết nối và đảm bảo thiết bị đã được nạp firmware.";
                    }
                    else
                    {
                        msg = reason switch
                        {
                            ConnectionFailureReason.IncompatibleDevice =>
                                "Thiết bị không tương thích: " + cleanDetail,
                            ConnectionFailureReason.DeviceNotFound =>
                                $"Không tìm thấy thiết bị tại {SelectedPort}.",
                            _ =>
                                $"Lỗi kết nối tại {SelectedPort}: {cleanDetail}"
                        };
                    }
                }
                else
                {
                    bool isTimeout = reason == ConnectionFailureReason.Timeout ||
                        (!string.IsNullOrWhiteSpace(failureDetail) && (failureDetail.Contains("timeout", StringComparison.OrdinalIgnoreCase) || failureDetail.Contains("timed out", StringComparison.OrdinalIgnoreCase)));

                    if (isTimeout)
                    {
                        msg = $"Timeout waiting for response from {SelectedPort}.\n\n" +
                              "💡 Tip:\n" +
                              "• To simulate without hardware: Select \"SIMULATOR (VIRTUAL)\" from the port dropdown, or press F5 for direct logic simulation.\n" +
                              "• If connecting a physical device: Check the connection cable, verify the COM port, and ensure the device is running firmware.";
                    }
                    else
                    {
                        msg = reason switch
                        {
                            ConnectionFailureReason.IncompatibleDevice =>
                                "Incompatible device: " + (failureDetail ?? "Unsupported version."),
                            ConnectionFailureReason.DeviceNotFound =>
                                $"Device not found at {SelectedPort}.",
                            _ =>
                                string.IsNullOrWhiteSpace(failureDetail)
                                    ? $"Cannot connect to {SelectedPort}."
                                    : $"Connection error at {SelectedPort}: {failureDetail}"
                        };
                    }
                }

                ShowNotification(
                    msg,
                    Loc.IsVietnamese ? "Lỗi kết nối" : "Connection Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (OperationCanceledException)
        {
            if (lifecycle.CurrentState != ConnectionLifecycleState.Connected)
            {
                ShowNotification(
                    Loc.IsVietnamese
                        ? $"Hết thời gian chờ phản hồi từ thiết bị tại {SelectedPort} (Timeout).\n\n" +
                          "💡 Mẹo:\n" +
                          "• Nếu bạn muốn chạy mô phỏng không cần thiết bị: Hãy chọn cổng \"SIMULATOR (VIRTUAL)\" trên thanh công cụ rồi bấm Kết nối, hoặc nhấn phím F5 để mô phỏng trực tiếp trên sơ đồ Logic.\n" +
                          "• Nếu bạn đang kết nối thiết bị thật: Vui lòng kiểm tra lại cáp kết nối và đảm bảo thiết bị đã được nạp firmware."
                        : $"Timeout waiting for response from {SelectedPort}.\n\n" +
                          "💡 Tip:\n" +
                          "• To simulate without hardware: Select \"SIMULATOR (VIRTUAL)\" from the port dropdown, or press F5 for direct logic simulation.\n" +
                          "• If connecting a physical device: Check the connection cable, verify the COM port, and ensure the device is running firmware.",
                    Loc.IsVietnamese ? "Lỗi kết nối" : "Connection Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            string cleanEx = Loc.IsVietnamese
                ? (ex.Message.Contains("calling thread", StringComparison.OrdinalIgnoreCase)
                    ? "Lỗi xung đột luồng giao diện hệ thống."
                    : ex.Message)
                : ex.Message;

            ShowNotification(
                (Loc.IsVietnamese ? "Lỗi kết nối: " : "Connection error: ") + cleanEx,
                Loc.IsVietnamese ? "Lỗi kết nối" : "Connection Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
        finally
        {
            UpdateConnectionStatus();
            NotifyAllDeviceCommands();
        }
    }

    public void NotifyAllDeviceCommands()
    {
        UploadFromDeviceCommand.NotifyCanExecuteChanged();
        RebootDeviceCommand.NotifyCanExecuteChanged();
        FactoryResetDeviceCommand.NotifyCanExecuteChanged();
        ToggleConnectCommand.NotifyCanExecuteChanged();
        SyncMcuClockCommand.NotifyCanExecuteChanged();

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
        {
            if (dispatcher.CheckAccess())
            {
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
            }
            else
            {
                dispatcher.BeginInvoke(new Action(() => System.Windows.Input.CommandManager.InvalidateRequerySuggested()));
            }
        }
    }

    private void OnSessionChanged(SimplePLC.Application.Abstractions.IDeviceSession? session)
    {
        if (session?.Product != null)
        {
            void Sync()
            {
                AppServices.Instance.CurrentProduct = session.Product;
                TagCatalogVM.SyncWithProductDefinition(session.Product);
                CurrentProjectMetadata.PhysicalRs485PortCount = session.Product.PhysicalRs485PortCount;
            }

            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(Sync);
            }
            else
            {
                Sync();
            }
        }
    }

    private void OnRuntimeTagsUpdated(IReadOnlyList<SimplePLC.Application.Models.RuntimeTagValue> tagValues)
    {
        // Khi đang ở chế độ Mô phỏng Canvas (Offline Simulation), không để dữ liệu polling phần cứng ghi đè lên các Tag đang mô phỏng
        if (LogicEditorVM.IsSimulationMode)
        {
            return;
        }

        TagCatalogVM.UpdateTagValues(tagValues);
    }

    private void OnRuntimeHealthUpdated(SimplePLC.Application.Models.DeviceHealthInfo health)
    {
        _lastHealthInfo = health;
        bool isVi = Loc.IsVietnamese;
        DiagnosticsSummary = isVi
            ? $"CPU: {health.CpuLoadPercent}% · RAM: {health.RamUsagePercent}% · Chu kỳ: {health.ScanTimeMs} ms"
            : $"CPU: {health.CpuLoadPercent}% · RAM: {health.RamUsagePercent}% · Cycle: {health.ScanTimeMs} ms";
    }

    private void OnRuntimeRtcUpdated(SimplePLC.Protocol.Dto.RtcClockDto rtc)
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => UpdateRtcState(rtc));
        }
        else
        {
            UpdateRtcState(rtc);
        }
    }

    /// <summary>
    /// D3.3: Phản chiếu LifecycleState thay đổi → cập nhật IsConnected, SelectedPort, LifecycleStatusText.
    /// MainViewModel không điều phối — chỉ phản chiếu.
    /// </summary>
    private void OnLifecycleStateChanged(object? sender, SimplePLC.Application.Models.LifecycleStateChangedEventArgs e)
    {
        void ApplyUpdate()
        {
            bool isVi = Loc.IsVietnamese;

            switch (e.NewState)
            {
                case ConnectionLifecycleState.Connected:
                    IsConnected = true;
                    ConnectionState = ConnectionState.Connected;
                    // D3.3: SelectedPort chỉ cập nhật khi Connected confirmed by lifecycle
                    if (e.Endpoint is UsbCdcEndpoint usbEp)
                    {
                        SelectedPort = usbEp.PortName;
                    }
                    else if (e.Endpoint is SimulatorEndpoint)
                    {
                        SelectedPort = "SIMULATOR (VIRTUAL)";
                    }
                    else if (e.Endpoint is TcpEndpoint tcpEp)
                    {
                        TcpHost = tcpEp.Host;
                        TcpPort = tcpEp.Port;
                    }
                    var connPortDisplay = e.Endpoint is UsbCdcEndpoint ue ? ue.PortName
                        : e.Endpoint is SimulatorEndpoint ? "SIMULATOR"
                        : e.Endpoint is TcpEndpoint te ? $"TCP {te.Host}:{te.Port}"
                        : SelectedPort;
                    LifecycleStatusText = isVi
                        ? $"● Đã kết nối · {connPortDisplay}"
                        : $"● Connected · {connPortDisplay}";
                    DeviceStatusText = isVi ? "● ĐÃ KẾT NỐI" : "● CONNECTED";
                    DeviceStatusColor = "#107C41";
                    var activeSession = AppServices.Instance.SessionManager.CurrentSession;
                    if (activeSession?.Descriptor != null)
                    {
                        UpdateDeviceProductInfo(activeSession.Descriptor, activeSession.ResourceInfo);
                    }
                    if (activeSession?.Product != null)
                    {
                        AppServices.Instance.CurrentProduct = activeSession.Product;
                        TagCatalogVM.SyncWithProductDefinition(activeSession.Product);
                    }
                    _ = AutoSyncMcuClockAsync();
                    break;

                case ConnectionLifecycleState.Restarting:
                    IsConnected = false;
                    ConnectionState = ConnectionState.Disconnected;
                    LifecycleStatusText = isVi
                        ? "◌ Thiết bị đang khởi động lại..."
                        : "◌ Device restarting...";
                    DeviceStatusText = isVi ? "◌ ĐANG KHỞI ĐỘNG LẠI" : "◌ RESTARTING";
                    DeviceStatusColor = "#C05621";
                    break;

                case ConnectionLifecycleState.Reconnecting:
                    IsConnected = false;
                    ConnectionState = ConnectionState.Disconnected;
                    if (e.FailureReason == ConnectionFailureReason.MultipleCompatibleDevices)
                    {
                        // Ambiguous: hiển thị cảnh báo, không tự chọn
                        LifecycleStatusText = isVi
                            ? "⚠ Phát hiện nhiều thiết bị tương thích · Vui lòng chọn cổng"
                            : "⚠ Multiple compatible devices · Please select a port";
                        DeviceStatusColor = "#B91C1C";
                    }
                    else if (e.IsPassiveWaiting)
                    {
                        LifecycleStatusText = isVi
                            ? "◌ Đang chờ thiết bị cắm lại..."
                            : "◌ Waiting for device to reconnect...";
                        DeviceStatusColor = "#C05621";
                    }
                    else
                    {
                        int maxRetry = SimplePLC.Application.Models.ReconnectionPolicy.Default.FastRetryDelays.Count;
                        LifecycleStatusText = isVi
                            ? $"◌ Mất kết nối · Đang thử lại {e.RetryAttempt}/{maxRetry}"
                            : $"◌ Lost connection · Retrying {e.RetryAttempt}/{maxRetry}";
                        DeviceStatusColor = "#C05621";
                    }
                    DeviceStatusText = isVi ? "○ ĐANG KẾT NỐI LẠI" : "○ RECONNECTING";
                    break;

                case ConnectionLifecycleState.Connecting:
                    LifecycleStatusText = isVi ? "◌ Đang kết nối..." : "◌ Connecting...";
                    DeviceStatusText = isVi ? "◌ ĐANG KẾT NỐI" : "◌ CONNECTING";
                    DeviceStatusColor = "#006487";
                    break;

                case ConnectionLifecycleState.Disconnected:
                default:
                    IsConnected = false;
                    ConnectionState = ConnectionState.Disconnected;
                    if (LogicEditorVM.IsLiveDebugActive)
                    {
                        LogicEditorVM.IsLiveDebugActive = false;
                    }
                    LogicEditorVM.ClearSimVisuals();
                    if (e.FailureReason == ConnectionFailureReason.CommunicationLost)
                    {
                        LifecycleStatusText = isVi 
                            ? "○ Mất kết nối thiết bị · Vui lòng kiểm tra cáp và bấm Kết nối lại" 
                            : "○ Device communication lost · Check cable and click Connect";
                        DeviceStatusText = isVi ? "○ MẤT KẾT NỐI" : "○ COMM LOST";
                        DeviceStatusColor = "#B91C1C";
                    }
                    else if (e.FailureReason == ConnectionFailureReason.Timeout)
                    {
                        LifecycleStatusText = isVi 
                            ? "○ Hết thời gian chờ phản hồi · Vui lòng bấm Kết nối lại" 
                            : "○ Response timeout · Please click Connect";
                        DeviceStatusText = isVi ? "○ TIMEOUT" : "○ TIMEOUT";
                        DeviceStatusColor = "#C05621";
                    }
                    else
                    {
                        LifecycleStatusText = isVi ? "○ Chưa kết nối" : "○ Disconnected";
                        DeviceStatusText = isVi ? "○ CHƯA KẾT NỐI" : "○ DISCONNECTED";
                        DeviceStatusColor = "#64748B";
                    }
                    break;
            }

            UpdateConnectionStatus();
            NotifyAllDeviceCommands();
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ApplyUpdate);
        }
        else
        {
            ApplyUpdate();
        }
    }

    partial void OnSelectedPortChanged(string value)
    {
        foreach (var opt in PortOptions)
        {
            opt.IsSelected = (opt.Name == value);
        }
        UpdateConnectionStatus();
    }

    partial void OnIsConnectedChanged(bool value)
    {
        UpdateConnectionStatus();
        if (!value)
        {
            UpdateDeviceProductInfo(null);
            McuClockDisplayText = "--:--";
            IsMcuClockSynced = false;
            HasHardwareRtc = false;
            IsRtcBatteryLow = false;
            RtcStatusTooltip = string.Empty;
        }
        NotifyAllDeviceCommands();
        UpdateTagPollingState();
    }

    private void UpdateConnectionStatus()
    {
        bool isVi = Loc.IsVietnamese;
        ConnectionStatusText = IsConnected 
            ? (isVi ? "Đã kết nối" : "Connected") 
            : (isVi ? "Chưa kết nối" : "Disconnected");

        bool isSim = SelectedPort == "SIMULATOR (VIRTUAL)";
        var lifecycleState = AppServices.Instance.LifecycleManager.CurrentState;

        if (IsConnected)
        {
            ConnectActionText = isVi ? "🔌 Ngắt kết nối" : "🔌 Disconnect";
            // IEC 60073 / IEC 60204-1 Clause 10.2: Dual-function toggle buttons must have neutral body, not alarm red
            ConnectButtonBackground = "#334155"; // Neutral Industrial Slate
            ConnectButtonBorderBrush = "#1E293B";
        }
        else
        {
            switch (lifecycleState)
            {
                case ConnectionLifecycleState.Restarting:
                    ConnectActionText = isVi ? "🔄 Đang khởi động lại..." : "🔄 Restarting...";
                    ConnectButtonBackground = "#C05621"; // Amber/Rust
                    ConnectButtonBorderBrush = "#9C4221";
                    break;

                case ConnectionLifecycleState.Reconnecting:
                    ConnectActionText = isVi ? "🔄 Đang kết nối lại..." : "🔄 Reconnecting...";
                    ConnectButtonBackground = "#C05621"; // Amber/Rust
                    ConnectButtonBorderBrush = "#9C4221";
                    break;

                case ConnectionLifecycleState.Connecting:
                    ConnectActionText = isVi ? "◌ Đang kết nối..." : "◌ Connecting...";
                    ConnectButtonBackground = "#475569"; // Neutral Slate Blue
                    ConnectButtonBorderBrush = "#334155";
                    break;

                case ConnectionLifecycleState.Disconnected:
                default:
                    ConnectActionText = isVi ? "⚡ Kết nối" : "⚡ Connect";
                    ConnectButtonBackground = "#107C41"; // Forest Green (Safe Initiate)
                    ConnectButtonBorderBrush = "#0E6B37";
                    break;
            }
        }

        PortMenuHeader = !string.IsNullOrEmpty(SelectedPort)
            ? (isVi ? $"Cổng kết nối ({SelectedPort})" : $"Port ({SelectedPort})")
            : (isVi ? "Cổng kết nối (Chưa chọn)" : "Port (None)");

        var activeEp = AppServices.Instance.LifecycleManager.CurrentEndpoint;
        ConnectionInfo = IsConnected
            ? (activeEp is TcpEndpoint tcpEp
                ? $"TCP {tcpEp.Host}:{tcpEp.Port} · Modbus TCP ({(isVi ? "Đã kết nối" : "Connected")})"
                : (isSim
                    ? (isVi ? "SIMULATOR (VIRTUAL) · Thiết bị ảo (Đã kết nối)" : "SIMULATOR (VIRTUAL) · Virtual Device (Connected)")
                    : $"{SelectedPort} · USB CDC ({(isVi ? "Đã kết nối" : "Connected")})"))
            : (IsTcpMode
                ? $"TCP {TcpHost}:{TcpPort} ({(isVi ? "Chưa kết nối" : "Disconnected")})"
                : (string.IsNullOrWhiteSpace(SelectedPort)
                    ? (isVi ? "Chưa chọn cổng · Chưa kết nối" : "No port selected · Disconnected")
                    : (isSim
                        ? (isVi ? "SIMULATOR (VIRTUAL) · Thiết bị ảo (Chưa kết nối)" : "SIMULATOR (VIRTUAL) · Virtual Device (Disconnected)")
                        : $"{SelectedPort} · USB CDC ({(isVi ? "Chưa kết nối" : "Disconnected")})")));

        OnPropertyChanged(nameof(ConnectionInfo));
        OnPropertyChanged(nameof(ConnectionBadgeText));
    }

    public string ConnectionBadgeText => IsConnected
        ? (AppServices.Instance.LifecycleManager.CurrentEndpoint is TcpEndpoint tcp
            ? $"● TCP {tcp.Host}:{tcp.Port}"
            : (SelectedPort == "SIMULATOR (VIRTUAL)" ? "● SIMULATOR" : $"● {SelectedPort}"))
        : (Loc.IsVietnamese ? "○ Chưa kết nối" : "○ Disconnected");

    public void UpdateDeviceProductInfo(DeviceDescriptorDto? descriptor = null, DeviceResourceInfoDto? resourceInfo = null)
    {
        if (descriptor != null)
        {
            _lastDescriptor = descriptor;
        }
        else if (!IsConnected)
        {
            _lastDescriptor = null;
        }

        if (resourceInfo != null)
        {
            _lastResourceInfo = resourceInfo;
        }
        else if (!IsConnected)
        {
            _lastResourceInfo = null;
        }

        bool isVi = Loc.IsVietnamese;

        if (_lastDescriptor != null && IsConnected)
        {
            var d = _lastDescriptor;
            string className = d.DeviceClass switch
            {
                SPLC_DeviceClass.REMOTE_IO => "Remote I/O",
                SPLC_DeviceClass.GATEWAY => "Gateway",
                SPLC_DeviceClass.DATALOGGER => "Datalogger",
                SPLC_DeviceClass.CONTROLLER => isVi ? "Bộ điều khiển" : "Controller",
                SPLC_DeviceClass.DTU => "DTU Gateway",
                _ => isVi ? "Thiết bị" : "Device"
            };

            string ioSummary;
            if (d.DeviceClass == SPLC_DeviceClass.DTU)
            {
                ioSummary = "RS-485 / Net";
            }
            else if (_lastResourceInfo != null)
            {
                var r = _lastResourceInfo;
                var parts = new List<string>(3);
                if (r.DigitalInputCount > 0) parts.Add($"{r.DigitalInputCount}DI");
                if (r.DigitalOutputCount > 0) parts.Add($"{r.DigitalOutputCount}DO");
                if (r.AnalogInputCount > 0) parts.Add($"{r.AnalogInputCount}AI");
                ioSummary = parts.Count > 0 ? string.Join(" / ", parts) : "0 I/O";
            }
            else
            {
                ioSummary = d.DeviceClass switch
                {
                    SPLC_DeviceClass.REMOTE_IO => d.DeviceVariant == 2 ? "16DI / 16DO" : "8DI / 8DO / 4AI",
                    SPLC_DeviceClass.GATEWAY => "RS485 / Ethernet",
                    SPLC_DeviceClass.DATALOGGER => "4DI / 2DO / 4AI",
                    SPLC_DeviceClass.DTU => "RS-485 / Net",
                    _ => $"Variant #{d.DeviceVariant}"
                };
            }

            DeviceProductBadgeText = $"{className} ({ioSummary})";
            DeviceProductToolTip = isVi
                ? $"Thiết bị: SynaptiX {className} ({ioSummary})\nPhần cứng: HW v{d.HwVersionString} · Phần mềm: FW v{d.FwVersionString}\nGiao thức: Modbus RTU Protocol v{d.ProtocolVersion} · Chuẩn Rule v{d.RuleFormatVersion}\n\n👉 Bấm để xem chi tiết thông số thiết bị"
                : $"Device: SynaptiX {className} ({ioSummary})\nHardware: HW v{d.HwVersionString} · Firmware: FW v{d.FwVersionString}\nProtocol: Modbus RTU Protocol v{d.ProtocolVersion} · Rule Format v{d.RuleFormatVersion}\n\n👉 Click to view detailed device properties";
        }
        else
        {
            var meta = CurrentProjectMetadata;
            string className = (SPLC_DeviceClass)meta.DeviceClass switch
            {
                SPLC_DeviceClass.REMOTE_IO => "Remote I/O",
                SPLC_DeviceClass.GATEWAY => "Gateway",
                SPLC_DeviceClass.DATALOGGER => "Datalogger",
                SPLC_DeviceClass.CONTROLLER => isVi ? "Bộ điều khiển" : "Controller",
                SPLC_DeviceClass.DTU => "DTU Gateway",
                _ => isVi ? "Thiết bị" : "Device"
            };

            string ioSummary;
            if ((SPLC_DeviceClass)meta.DeviceClass == SPLC_DeviceClass.DTU)
            {
                ioSummary = meta.PhysicalRs485PortCount > 0 ? $"{meta.PhysicalRs485PortCount}x RS-485" : "RS-485 / Net";
            }
            else
            {
                var ioParts = new List<string>(3);
                if (meta.DigitalInputs > 0) ioParts.Add($"{meta.DigitalInputs}DI");
                if (meta.DigitalOutputs > 0) ioParts.Add($"{meta.DigitalOutputs}DO");
                if (meta.AnalogInputs > 0) ioParts.Add($"{meta.AnalogInputs}AI");
                ioSummary = ioParts.Count > 0 ? string.Join(" / ", ioParts) : "0 I/O";
            }

            DeviceProductBadgeText = $"{className} ({ioSummary})";
            DeviceProductToolTip = isVi
                ? $"Cấu hình dự án: SynaptiX {className} ({ioSummary})\nPhần cứng mục tiêu: {meta.TargetDevice} (Chưa kết nối)\nSố biến I/O: {meta.DigitalInputs + meta.DigitalOutputs + meta.AnalogInputs} tags · Giới hạn Rule: {meta.MaxRules}\n\n👉 Bấm để xem chi tiết thông số thiết bị"
                : $"Project Configuration: SynaptiX {className} ({ioSummary})\nTarget Hardware: {meta.TargetDevice} (Offline)\nI/O Tags: {meta.DigitalInputs + meta.DigitalOutputs + meta.AnalogInputs} tags · Max Rules: {meta.MaxRules}\n\n👉 Click to view detailed device properties";
        }
    }

    [RelayCommand]
    public void ShowDeviceInfo()
    {
        var session = AppServices.Instance.SessionManager.CurrentSession;
        var descriptor = _lastDescriptor ?? session?.Descriptor;
        var resource = _lastResourceInfo ?? session?.ResourceInfo;
        bool isVi = Loc.IsVietnamese;

        DeviceInfoViewModel vm;

        if (IsConnected && descriptor != null)
        {
            var d = descriptor;
            var r = resource ?? DeviceResourceInfoDto.CreateRemoteIo8Di8Do4Ai();

            string className = d.DeviceClass switch
            {
                SPLC_DeviceClass.REMOTE_IO => "Remote I/O",
                SPLC_DeviceClass.GATEWAY => "Gateway",
                SPLC_DeviceClass.DATALOGGER => "Datalogger",
                SPLC_DeviceClass.CONTROLLER => isVi ? "Bộ điều khiển" : "Controller",
                SPLC_DeviceClass.DTU => "DTU Gateway",
                _ => isVi ? "Thiết bị" : "Device"
            };

            string variantText;
            if (d.DeviceClass == SPLC_DeviceClass.DTU)
            {
                variantText = "RS-485 / Net";
            }
            else
            {
                var ioParts = new List<string>(3);
                if (r.DigitalInputCount > 0) ioParts.Add($"{r.DigitalInputCount}DI");
                if (r.DigitalOutputCount > 0) ioParts.Add($"{r.DigitalOutputCount}DO");
                if (r.AnalogInputCount > 0) ioParts.Add($"{r.AnalogInputCount}AI");
                variantText = ioParts.Count > 0 ? string.Join(" / ", ioParts) : $"Variant #{d.DeviceVariant}";
            }

            string targetModelName = !string.IsNullOrWhiteSpace(CurrentProjectMetadata?.TargetDevice)
                ? CurrentProjectMetadata.TargetDevice
                : $"SynaptiX {className}";

            vm = new DeviceInfoViewModel
            {
                DeviceModel = targetModelName,
                TargetModelText = targetModelName,
                DeviceClassText = className,
                DeviceVariantText = variantText,
                HwVersion = $"v{d.HwVersionString}",
                FwVersion = $"v{d.FwVersionString}",
                WireProfileText = r.WireProfile == 2 ? "Wire Profile V2 (Diag/Commissioning)" : "Wire Profile V1",
                RuleFormatText = $"v{d.RuleFormatVersion}.0",
                DigitalInputs = r.DigitalInputCount,
                DigitalOutputs = r.DigitalOutputCount,
                AnalogInputs = r.AnalogInputCount,
                VirtualFlags = r.VirtualFlagCount,
                VirtualRegs = r.VirtualRegisterCount,
                RetainRegs = r.RetentiveRegisterCount,
                Counters = r.CounterCount,
                MaxRules = r.MaxRules,
                PortName = string.IsNullOrWhiteSpace(SelectedPort) ? "COM10" : SelectedPort,
                BaudRate = 115200,
                SlaveId = session?.SlaveId ?? 1,
                ProtocolText = $"Modbus RTU (v{d.ProtocolVersion})",
                IsOnline = true
            };
        }
        else
        {
            var meta = CurrentProjectMetadata;
            string className = (SPLC_DeviceClass)meta.DeviceClass switch
            {
                SPLC_DeviceClass.REMOTE_IO => "Remote I/O",
                SPLC_DeviceClass.GATEWAY => "Gateway",
                SPLC_DeviceClass.DATALOGGER => "Datalogger",
                SPLC_DeviceClass.CONTROLLER => isVi ? "Bộ điều khiển" : "Controller",
                SPLC_DeviceClass.DTU => "DTU Gateway",
                _ => isVi ? "Thiết bị" : "Device"
            };

            string variantText;
            if ((SPLC_DeviceClass)meta.DeviceClass == SPLC_DeviceClass.DTU)
            {
                variantText = meta.PhysicalRs485PortCount > 0 ? $"{meta.PhysicalRs485PortCount}x RS-485 / Net" : "RS-485 / Net";
            }
            else
            {
                var ioParts = new List<string>(3);
                if (meta.DigitalInputs > 0) ioParts.Add($"{meta.DigitalInputs}DI");
                if (meta.DigitalOutputs > 0) ioParts.Add($"{meta.DigitalOutputs}DO");
                if (meta.AnalogInputs > 0) ioParts.Add($"{meta.AnalogInputs}AI");
                variantText = ioParts.Count > 0 ? string.Join(" / ", ioParts) : (meta.ProductVariant == 255 ? (isVi ? "Tùy chỉnh" : "Custom") : $"Variant #{meta.ProductVariant}");
            }

            string targetModelName = !string.IsNullOrWhiteSpace(meta.TargetDevice) ? meta.TargetDevice : (isVi ? "Mô phỏng (Virtual)" : "Simulator (Virtual)");
            string hwVersionStr = isVi ? "v1.0 (Mặc định)" : "v1.0 (Standard)";
            string fwStatus = isVi ? "Chưa kết nối" : "Offline";
            string portText = !string.IsNullOrWhiteSpace(SelectedPort) ? SelectedPort : (isVi ? "Chưa chọn cổng" : "None");

            vm = new DeviceInfoViewModel
            {
                DeviceModel = targetModelName,
                TargetModelText = targetModelName,
                DeviceClassText = className,
                DeviceVariantText = variantText,
                HwVersion = hwVersionStr,
                FwVersion = fwStatus,
                WireProfileText = "Wire Profile V2 (Standard)",
                RuleFormatText = "v2.0",
                DigitalInputs = meta.DigitalInputs,
                DigitalOutputs = meta.DigitalOutputs,
                AnalogInputs = meta.AnalogInputs,
                VirtualFlags = meta.VirtualFlags,
                VirtualRegs = meta.VirtualRegisters,
                RetainRegs = meta.RetentiveRegisters,
                Counters = meta.Counters,
                MaxRules = meta.MaxRules,
                PortName = portText,
                BaudRate = 115200,
                SlaveId = 1,
                ProtocolText = "Modbus RTU",
                IsOnline = false
            };
        }

        if (ShowDeviceInfoDialogHandler != null)
        {
            ShowDeviceInfoDialogHandler(vm);
            return;
        }

        var dialog = new Views.DeviceInfoDialogView(vm);
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenMqttBrokerConfig()
    {
        var vm = new MqttBrokerConfigViewModel(CurrentProjectMetadata);
        var dialog = new Views.MqttBrokerConfigDialogView(vm);
        if (dialog.ShowDialog() == true)
        {
            IsProjectDirty = true;
            DtuGatewayStudioVM?.ReloadFromMetadata();
        }
    }

    partial void OnCurrentProjectMetadataChanged(ProjectMetadata value)
    {
        DtuGatewayStudioVM = new DtuGatewayStudioViewModel(value, TagCatalogVM, LogicEditorVM);
        WireDtuGatewayStudioVM(DtuGatewayStudioVM);
        OnPropertyChanged(nameof(DtuGatewayStudioVM));
        OnPropertyChanged(nameof(IsDtuProject));
        NotifyHardwareCapabilities();

        if (CurrentView is StartupHubViewModel)
        {
            return;
        }

        if (!HasSystemTab && SelectedTabIndex == AppTabIndices.Device)
        {
            SelectTab(AppTabIndices.LogicGraph);
        }
        else if (SelectedTabIndex == AppTabIndices.Device)
        {
            CurrentView = DtuGatewayStudioVM;
        }
    }

    private void WireDtuGatewayStudioVM(DtuGatewayStudioViewModel vm)
    {
        vm.OpenMqttBrokerConfigRequested = () => OpenMqttBrokerConfig();
        vm.UploadFromDeviceRequested = () => UploadFromDeviceAsync();
        vm.OpenQuickWizardRequested = () => OpenQuickWizard();
        vm.NotifyConfigurationCommitted = () =>
        {
            IsProjectDirty = true;
        };
        vm.UpdateHardwareConnectionState(IsConnected, SelectedPort, DeviceProductBadgeText);
    }

    [RelayCommand]
    public void OpenQuickWizard()
    {
        var vm = DtuGatewayStudioVM ?? new DtuGatewayStudioViewModel(CurrentProjectMetadata, TagCatalogVM, LogicEditorVM);
        if (DtuGatewayStudioVM == null)
        {
            WireDtuGatewayStudioVM(vm);
        }
        var dialog = new Views.DtuGatewayConfigDialogView(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        if (dialog.ShowDialog() == true)
        {
            IsProjectDirty = true;
            vm.ReloadFromMetadata();
        }
    }


    [RelayCommand]
    private void OpenModbusPortConfig()
    {
        // Điều hướng trực tiếp tới Tab Thiết bị -> Phân vùng RS-485 Modbus (Single Source of Truth)
        SelectTab(AppTabIndices.Device);
        DtuGatewayStudioVM?.SelectSection(0);
    }

    [RelayCommand]
    private void OpenWifiConfig()
    {
        var vm = new WifiConfigViewModel(CurrentProjectMetadata);
        var dialog = new Views.WifiConfigDialogView(vm);
        if (dialog.ShowDialog() == true)
        {
            IsProjectDirty = true;
        }
    }

    private void UpdateLanguageTexts()
    {
        bool isVi = Loc.IsVietnamese;
        UpdateConnectionStatus();
        UpdateDeviceProductInfo(_lastDescriptor, _lastResourceInfo);
        SyncPortOptions();

        // Re-render DeviceStatusText and LifecycleStatusText from current lifecycle state
        var lifecycleState = AppServices.Instance.LifecycleManager.CurrentState;
        var failureReason = AppServices.Instance.LifecycleManager.FailureReason;
        var endpoint = AppServices.Instance.LifecycleManager.CurrentEndpoint;

        switch (lifecycleState)
        {
            case ConnectionLifecycleState.Connected:
                DeviceStatusText = isVi ? "● ĐÃ KẾT NỐI" : "● CONNECTED";
                var portDisplay = endpoint is UsbCdcEndpoint ue ? ue.PortName
                    : endpoint is SimulatorEndpoint ? "SIMULATOR" : SelectedPort;
                LifecycleStatusText = isVi
                    ? $"● Đã kết nối · {portDisplay}"
                    : $"● Connected · {portDisplay}";
                break;
            case ConnectionLifecycleState.Restarting:
                DeviceStatusText = isVi ? "◌ ĐANG KHỞI ĐỘNG LẠI" : "◌ RESTARTING";
                LifecycleStatusText = isVi ? "◌ Thiết bị đang khởi động lại..." : "◌ Device restarting...";
                break;
            case ConnectionLifecycleState.Reconnecting:
                DeviceStatusText = isVi ? "○ ĐANG KẾT NỐI LẠI" : "○ RECONNECTING";
                LifecycleStatusText = failureReason == ConnectionFailureReason.MultipleCompatibleDevices
                    ? (isVi ? "⚠ Phát hiện nhiều thiết bị tương thích · Vui lòng chọn cổng" : "⚠ Multiple compatible devices · Please select a port")
                    : (isVi ? "◌ Đang kết nối lại..." : "◌ Reconnecting...");
                break;
            case ConnectionLifecycleState.Connecting:
                DeviceStatusText = isVi ? "◌ ĐANG KẾT NỐI" : "◌ CONNECTING";
                LifecycleStatusText = isVi ? "◌ Đang kết nối..." : "◌ Connecting...";
                break;
            default:
                DeviceStatusText = isVi ? "○ CHƯA KẾT NỐI" : "○ DISCONNECTED";
                LifecycleStatusText = isVi ? "○ Chưa kết nối" : "○ Disconnected";
                break;
        }

        if (IsConnected && _lastHealthInfo != null)
        {
            DiagnosticsSummary = isVi
                ? $"CPU: {_lastHealthInfo.CpuLoadPercent}% · RAM: {_lastHealthInfo.RamUsagePercent}% · Chu kỳ: {_lastHealthInfo.ScanTimeMs} ms"
                : $"CPU: {_lastHealthInfo.CpuLoadPercent}% · RAM: {_lastHealthInfo.RamUsagePercent}% · Cycle: {_lastHealthInfo.ScanTimeMs} ms";
        }
        else
        {
            CompileState = LogicEditorVM.CompileState;
            DiagnosticsSummary = CompileState switch
            {
                CompileState.Valid => isVi ? $"Biên dịch: OK · {LogicEditorVM.ValidRuleCount} Rule" : $"Compile: OK · {LogicEditorVM.ValidRuleCount} rules",
                CompileState.Invalid => isVi ? "Biên dịch: Có lỗi cần xử lý" : "Compile: Errors need attention",
                CompileState.Stale => isVi ? "Biên dịch: Cấu hình đã thay đổi" : "Compile: Configuration changed",
                _ => isVi ? "Biên dịch: Chưa có chương trình hợp lệ" : "Compile: No valid program"
            };
        }

        OnPropertyChanged(nameof(Loc));
    }

    [RelayCommand]
    public void ChangeLanguage(string lang)
    {
        Loc.CurrentLanguage = lang;
    }

    [RelayCommand]
    public void SelectTab(object? param)
    {
        int index = 0;
        if (param is int i) index = i;
        else if (param != null && int.TryParse(param.ToString(), out int parsed)) index = parsed;

        SelectedTabIndex = index;
        CurrentView = index switch
        {
            AppTabIndices.Device => DtuGatewayStudioVM,
            AppTabIndices.LogicGraph => LogicEditorVM,
            AppTabIndices.TagCatalog => TagCatalogVM,
            AppTabIndices.RuleTable => RuleTableVM,
            AppTabIndices.Blueprints => BlueprintsVM,
            AppTabIndices.Deploy => DeployVM,
            AppTabIndices.LiveWatch => LiveWatchVM,
            AppTabIndices.Simulator => SimulatorVM,
            _ => DtuGatewayStudioVM
        };

        if (index == AppTabIndices.LiveWatch)
        {
            LiveWatchVM.SyncProjectAliases();
        }
    }

    [RelayCommand]
    public void SaveProject()
    {
        if (string.IsNullOrEmpty(CurrentProjectPath))
        {
            SaveProjectAs();
        }
        else
        {
            SaveProjectInternal(CurrentProjectPath);
        }
    }

    [RelayCommand]
    public void SaveProjectAs()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = ProjectFileService.ProjectFilter,
            FileName = $"{CurrentProjectName}.synx",
            Title = Loc["MenuSaveProject"]
        };
        if (sfd.ShowDialog() == true)
        {
            SaveProjectInternal(sfd.FileName);
        }
    }

    private void SaveProjectInternal(string filePath)
    {
        try
        {
            LogicEditorVM.SaveAndCompile();

            var (nodes, connections) = LogicEditorVM.ExportGraphData();
            
            // Nếu Canvas hiện tại đang trống nhưng Bảng Rule có luật, tự động tái tạo đồ thị cho tất cả các luật
            if ((nodes == null || nodes.Count == 0) && RuleTableVM.Rules.Count > 0)
            {
                LogicEditorVM.ReconstructGraphFromAllRules(RuleTableVM.Rules);
                (nodes, connections) = LogicEditorVM.ExportGraphData();
            }

            // Zero-Empty-Overwrite Safety Gate check
            bool isProjectEmpty = (nodes == null || nodes.Count == 0) && RuleTableVM.Rules.Count == 0;
            bool allowEmptyOverwrite = false;

            if (System.IO.File.Exists(filePath))
            {
                var fi = new System.IO.FileInfo(filePath);
                if (fi.Length > 512 && isProjectEmpty)
                {
                    bool isViWarning = Loc.IsVietnamese;
                    var confirm = ShowConfirm(
                        isViWarning
                            ? "CẢNH BÁO AN TOÀN: Dự án hiện tại không có sơ đồ logic hoặc quy tắc nào.\n\nThao tác này sẽ xóa sạch dữ liệu của tệp đã lưu trên đĩa. Bạn có chắc chắn muốn ghi đè tệp rỗng không?"
                            : "SAFETY WARNING: The current project has no logic nodes or rules.\n\nOverwriting will erase all existing content on disk. Are you sure you want to proceed?",
                        isViWarning ? "Cảnh báo ghi đè tệp rỗng" : "Empty Overwrite Safety Warning",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Warning);

                    if (confirm != System.Windows.MessageBoxResult.Yes)
                    {
                        return;
                    }
                    allowEmptyOverwrite = true;
                }
            }

            var tags = TagCatalogVM.ExportToProjectData();

            if (string.IsNullOrWhiteSpace(CurrentProjectMetadata.ProjectName) || CurrentProjectMetadata.ProjectName == "Untitled")
            {
                CurrentProjectMetadata.ProjectName = System.IO.Path.GetFileNameWithoutExtension(filePath);
            }
            CurrentProjectMetadata.LastModified = DateTime.UtcNow;

            var project = new ProjectModel
            {
                Metadata = CurrentProjectMetadata,
                Tags = tags,
                Nodes = nodes ?? new List<ProjectNodeData>(),
                Connections = connections ?? new List<ProjectConnectionData>(),
                Rules = RuleTableVM.ExportToProjectData(),
                CurrentEditingRuleId = LogicEditorVM.EditingRuleId,
                CurrentDiagramId = LogicEditorVM.CurrentDiagramId,
                WatchlistTagIndices = LiveWatchVM.GetWatchlistIndices()
            };

            ProjectFileService.SaveProject(filePath, project, allowEmptyOverwrite);

            CurrentProjectPath = filePath;
            CurrentProjectName = CurrentProjectMetadata.ProjectName;
            IsProjectDirty = false;
            RefreshRecentProjects();

            bool isVi = Loc.IsVietnamese;
            LogicEditorVM.SaveStatusMessage = isVi 
                ? $"● Đã lưu dự án vào tệp ({DateTime.Now:HH:mm:ss})" 
                : $"● Saved project to file ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message, "Error Saving Project", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public bool PromptSaveIfDirty()
    {
        if (!IsProjectDirty) return true;

        bool isVi = Loc.IsVietnamese;
        var res = ShowConfirm(
            isVi 
                ? $"Dự án '{CurrentProjectName}' có những thay đổi chưa được lưu.\n\nBạn có muốn lưu các thay đổi này trước không?" 
                : $"Project '{CurrentProjectName}' has unsaved changes.\n\nDo you want to save changes before proceeding?",
            isVi ? "Xác nhận lưu dự án" : "Save Project Confirmation",
            System.Windows.MessageBoxButton.YesNoCancel,
            System.Windows.MessageBoxImage.Question);

        if (res == System.Windows.MessageBoxResult.Cancel) return false;
        if (res == System.Windows.MessageBoxResult.Yes)
        {
            SaveProject();
            return !IsProjectDirty;
        }
        return true;
    }

    [RelayCommand]
    public void NewProject()
    {
        if (PromptSaveIfDirty() == false) return;

        var wizardVm = new NewProjectViewModel();

        bool shouldApply = false;

        if (ShowNewProjectDialogHandler != null)
        {
            shouldApply = ShowNewProjectDialogHandler(wizardVm) == true;
        }
        else if (System.Windows.Application.Current?.MainWindow != null)
        {
            var dialog = new Views.NewProjectDialogView(wizardVm);
            shouldApply = dialog.ShowDialog() == true;
        }
        else
        {
            // Headless / automated testing fallback
            if (wizardVm.HasConnectedDevice)
            {
                wizardVm.DetectFromConnectedDevice();
            }
            shouldApply = true;
        }

        if (!shouldApply) return;

        ApplyNewProject(wizardVm);
    }

    /// <summary>
    /// Mở màn hình Start Page toàn màn hình (In-App Fullscreen Dashboard).
    /// </summary>
    [RelayCommand]
    public void OpenStartPage()
    {
        StartupHubVM.LoadRecentProjects();
        StartupHubVM.RefreshPorts();
        CurrentView = StartupHubVM;
    }

    /// <summary>
    /// Hiển thị màn hình khởi động tiêu chuẩn quốc tế (Startup Hub / Welcome Window).
    /// Hỗ trợ cả delegate hook cho unit test và điều hướng toàn màn hình trực tiếp.
    /// </summary>
    [RelayCommand]
    public void PromptStartupHub()
    {
        if (ShowStartupHubDialogHandler != null)
        {
            var hubVm = new StartupHubViewModel();
            ShowStartupHubDialogHandler(hubVm);
            ExecuteStartupHubAction(hubVm);
            return;
        }

        OpenStartPage();
    }

    /// <summary>
    /// Đóng dự án hiện tại, lưu thay đổi nếu cần và quay lại màn hình Start Page.
    /// </summary>
    [RelayCommand]
    public void CloseProject()
    {
        if (PromptSaveIfDirty() == false) return;

        CurrentProjectPath = null;
        CurrentProjectName = Loc.IsVietnamese ? "Dự án mới" : "Untitled";
        CurrentProjectMetadata = new ProjectMetadata();
        LogicEditorVM.ClearCanvas();
        RuleTableVM.Rules.Clear();
        RuleTableVM.ApplyFilter();
        LiveWatchVM.ClearWatchlist();
        IsProjectDirty = false;

        OpenStartPage();
    }

    /// <summary>
    /// Thực thi hành động được chọn từ Startup Hub (Tạo mới, Mở tệp, Mở gần đây, Nạp từ thiết bị, Bỏ qua).
    /// </summary>
    public void ExecuteStartupHubAction(StartupHubViewModel hubVm)
    {
        ArgumentNullException.ThrowIfNull(hubVm);

        switch (hubVm.SelectedAction)
        {
            case StartupHubAction.NewProject:
                NewProject();
                break;

            case StartupHubAction.OpenLocalProject:
                OpenProject();
                break;

            case StartupHubAction.OpenRecentProject:
                if (!string.IsNullOrWhiteSpace(hubVm.SelectedRecentProjectPath) && System.IO.File.Exists(hubVm.SelectedRecentProjectPath))
                {
                    LoadProjectFile(hubVm.SelectedRecentProjectPath);
                }
                break;

            case StartupHubAction.UploadFromDevice:
                if (!string.IsNullOrWhiteSpace(hubVm.SelectedPort))
                {
                    SelectedPort = hubVm.SelectedPort;
                }
                _ = Task.Run(async () =>
                {
                    if (!IsConnected)
                    {
                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                await ToggleConnect();
                            });
                        }
                        else
                        {
                            await ToggleConnect();
                        }
                    }
                    if (IsConnected || CanDoDeviceAction)
                    {
                        if (System.Windows.Application.Current?.Dispatcher != null)
                        {
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                            {
                                await UploadFromDeviceAsync();
                            });
                        }
                        else
                        {
                            await UploadFromDeviceAsync();
                        }
                    }
                });
                break;

            case StartupHubAction.ContinueBlank:
                SelectTab(AppTabIndices.LogicGraph);
                break;

            case StartupHubAction.None:
            default:
                break;
        }
    }

    /// <summary>
    /// Hiển thị hộp thoại cấu hình dự án mới khi ứng dụng khởi động mà chưa mở file dự án (.synx).
    /// Cho phép kỹ sư chọn nhanh dòng phần cứng mục tiêu hoặc nhận diện tự động từ PLC đang kết nối.
    /// </summary>
    public void PromptStartupNewProject()
    {
        var wizardVm = new NewProjectViewModel();
        bool shouldApply = false;

        if (ShowNewProjectDialogHandler != null)
        {
            shouldApply = ShowNewProjectDialogHandler(wizardVm) == true;
        }
        else if (System.Windows.Application.Current?.MainWindow != null)
        {
            var dialog = new Views.NewProjectDialogView(wizardVm)
            {
                Owner = System.Windows.Application.Current.MainWindow
            };
            shouldApply = dialog.ShowDialog() == true;
        }

        if (shouldApply)
        {
            ApplyNewProject(wizardVm);
        }
    }

    public void ApplyNewProject(NewProjectViewModel wizardVm)
    {
        ArgumentNullException.ThrowIfNull(wizardVm);

        var product = wizardVm.BuildProductDefinition();
        ushort devClass = (ushort)(wizardVm.IsCustomMode ? 1 : (ushort)wizardVm.SelectedPreset.DeviceClass);
        bool isDtu = devClass == (ushort)SimplePLC.Domain.Enums.SPLC_DeviceClass.DTU;

        AppServices.Instance.CurrentProduct = product;

        CurrentProjectMetadata = new ProjectMetadata
        {
            ProjectName = wizardVm.ProjectName,
            Author = wizardVm.Author,
            Description = wizardVm.Description,
            TargetDevice = wizardVm.PreviewDeviceName,
            DeviceClass = devClass,
            ProductVariant = (ushort)(wizardVm.IsCustomMode ? 255 : wizardVm.SelectedPreset.DeviceVariant),
            DigitalInputs = product.Resources.DigitalInputs,
            DigitalOutputs = product.Resources.DigitalOutputs,
            AnalogInputs = product.Resources.AnalogInputs,
            VirtualFlags = product.Resources.VirtualFlags,
            VirtualRegisters = product.Resources.VirtualRegisters,
            RetentiveRegisters = product.Resources.RetentiveRegisters,
            Counters = product.Resources.Counters,
            MaxRules = product.MaxRules,
            PhysicalRs485PortCount = product.PhysicalRs485PortCount,
            HardwareCapabilities = (uint)product.HardwareCapabilities,
            WifiEnabled = product.SupportsWifi,
            MqttBrokerEnabled = product.SupportsMqttPublish || product.SupportsMqttSubscribe || product.SupportsWifi,
            DtuConfig = new DtuConfigModel { Enabled = isDtu },
            CreatedAt = DateTime.UtcNow,
            LastModified = DateTime.UtcNow
        };

        CurrentProjectName = wizardVm.ProjectName;
        CurrentProjectPath = System.IO.Path.Combine(wizardVm.ProjectLocation, wizardVm.ProjectName + ProjectFileService.ProjectExtension);
        TagCatalogVM.SyncWithProductDefinition(product);
        TagCatalogVM.ResetAllAliases();

        LogicEditorVM.NewRuleCanvas();
        RuleTableVM.Rules.Clear();
        RuleTableVM.ApplyFilter();
        LiveWatchVM.ClearWatchlist();

        IsProjectDirty = false;

        UpdateDeviceProductInfo();
        NotifyHardwareCapabilities();

        SelectTab(AppTabIndices.LogicGraph);

        bool isVi = Loc.IsVietnamese;
        LogicEditorVM.SaveStatusMessage = isVi
            ? $"● Đã tạo dự án mới: {CurrentProjectName} ({product.ProductName}) lúc {DateTime.Now:HH:mm:ss}"
            : $"● Created new project: {CurrentProjectName} ({product.ProductName}) at {DateTime.Now:HH:mm:ss}";
    }

    [RelayCommand]
    public void OpenProject()
    {
        if (PromptSaveIfDirty() == false) return;

        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = ProjectFileService.ProjectFilter,
            Title = Loc["ToolOpen"]
        };
        if (ofd.ShowDialog() == true)
        {
            LoadProjectFile(ofd.FileName);
        }
    }

    public void LoadProjectFile(string filePath)
    {
        try
        {
            var project = ProjectFileService.LoadProject(filePath);

            CurrentProjectMetadata = project.Metadata ?? new ProjectMetadata();

            // Phục hồi và đồng bộ cấu hình phần cứng từ metadata dự án
            var resources = new ProductResourceProfile(
                (ushort)CurrentProjectMetadata.DigitalInputs,
                (ushort)CurrentProjectMetadata.DigitalOutputs,
                (ushort)CurrentProjectMetadata.AnalogInputs,
                (ushort)CurrentProjectMetadata.VirtualFlags,
                (ushort)CurrentProjectMetadata.VirtualRegisters,
                (ushort)CurrentProjectMetadata.RetentiveRegisters,
                (ushort)CurrentProjectMetadata.Counters);

            if (DeviceProfileBuilder.TryBuild(
                CurrentProjectMetadata.DeviceClass,
                CurrentProjectMetadata.ProductVariant,
                protocolVersion: DeviceProfileBuilder.ProtocolVersionV2,
                wireProfile: DeviceProfileBuilder.WireProfileV2,
                maxRules: (ushort)CurrentProjectMetadata.MaxRules,
                runtimeTagCount: (ushort)resources.TotalTags,
                resources: resources,
                out var product,
                out _,
                hardwareCapabilities: (SimplePLC.Domain.Enums.SPLC_HardwareCapabilities)CurrentProjectMetadata.HardwareCapabilities,
                physicalRs485PortCount: CurrentProjectMetadata.PhysicalRs485PortCount) && product != null)
            {
                AppServices.Instance.CurrentProduct = product;
                TagCatalogVM.SyncWithProductDefinition(product);
            }

            TagCatalogVM.LoadFromProjectData(project.Tags);
            if (product != null)
            {
                AppServices.Instance.CurrentProduct = product;
            }

            // Reset filters in RuleTableVM so loaded rules are immediately visible
            RuleTableVM.SearchFilter = string.Empty;
            RuleTableVM.StatusFilter = "ALL";

            // 1. Phục hồi toàn bộ Bảng Rule trước
            if (project.Rules != null && project.Rules.Count > 0)
            {
                RuleTableVM.LoadFromProjectData(project.Rules);
            }
            else
            {
                RuleTableVM.Rules.Clear();
                RuleTableVM.ApplyFilter();
            }

            // 2. Phục hồi bản vẽ trên Canvas
            if (project.Nodes != null && project.Nodes.Count > 0)
            {
                LogicEditorVM.LoadGraphData(project.Nodes, project.Connections ?? Enumerable.Empty<ProjectConnectionData>());
                LogicEditorVM.EditingRuleId = project.CurrentEditingRuleId;
                if (!string.IsNullOrEmpty(project.CurrentDiagramId))
                {
                    LogicEditorVM.CurrentDiagramId = project.CurrentDiagramId;
                }
            }
            else if (RuleTableVM.Rules.Count > 0)
            {
                // Tự động tái tạo toàn bộ danh mục Rules thành các mạng Network độc lập trên Canvas (chuẩn TIA Portal)
                LogicEditorVM.ReconstructGraphFromAllRules(RuleTableVM.Rules);
            }
            else
            {
                LogicEditorVM.ClearCanvas();
            }

            LiveWatchVM.SetWatchlist(project.WatchlistTagIndices);

            CurrentProjectPath = filePath;
            string? metaName = project.Metadata?.ProjectName;
            CurrentProjectName = !string.IsNullOrWhiteSpace(metaName) && metaName != "Untitled"
                ? metaName
                : System.IO.Path.GetFileNameWithoutExtension(filePath);


            IsProjectDirty = false;
            RefreshRecentProjects();
            UpdateDeviceProductInfo();
        NotifyHardwareCapabilities();

            SelectTab(AppTabIndices.LogicGraph);

            bool isVi = Loc.IsVietnamese;
            LogicEditorVM.SaveStatusMessage = isVi 
                ? $"● Đã mở: {CurrentProjectName} ({DateTime.Now:HH:mm:ss})" 
                : $"● Opened: {CurrentProjectName} ({DateTime.Now:HH:mm:ss})";
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message, "Error Loading Project", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void LoadExample5FloatAlarm()
    {
        SelectTab(AppTabIndices.LogicGraph);
        LogicEditorVM.ClearCanvas();

        var tags = TagCatalogVM.AllTags;
        var ai0 = tags.FirstOrDefault(t => t.Name == "AI0") ?? tags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput) ?? tags[0];
        var do0 = tags.FirstOrDefault(t => t.Name == "DO0") ?? tags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput) ?? tags[0];

        var inp = new InputNodeViewModel(ai0) { Location = new System.Windows.Point(80, 140) };
        var comp = new ComparatorNodeViewModel(ai0)
        {
            CompareOp = CompareOp.GTE,
            ThresholdLo = 75.5,
            ThresholdHi = 0,
            Location = new System.Windows.Point(340, 140)
        };
        var act = new ActionNodeViewModel(do0)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = 1,
            Location = new System.Windows.Point(620, 140)
        };

        LogicEditorVM.Nodes.Add(inp);
        LogicEditorVM.Nodes.Add(comp);
        LogicEditorVM.Nodes.Add(act);

        LogicEditorVM.Connect(inp.OutputConnectors[0], comp.InputConnectors[0]);
        LogicEditorVM.Connect(comp.OutputConnectors[0], act.InputConnectors[0]);

        bool isVi = Loc.IsVietnamese;
        LogicEditorVM.SaveStatusMessage = isVi
            ? "● Đã nạp Ví dụ 5: Quá nhiệt Float32 (AI0 >= 75.5°C → DO0)"
            : "● Loaded Example 5: Float32 Temp Alarm (AI0 >= 75.5°C → DO0)";
    }

    [RelayCommand]
    public void OpenRecentProject(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath)) return;
        if (!System.IO.File.Exists(filePath))
        {
            bool isVi = Loc.IsVietnamese;
            ShowNotification(
                isVi ? $"Tệp dự án không còn tồn tại:\n{filePath}" : $"Project file no longer exists:\n{filePath}",
                isVi ? "Không tìm thấy tệp" : "File Not Found",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            RefreshRecentProjects();
            return;
        }

        if (PromptSaveIfDirty() == false) return;
        LoadProjectFile(filePath);
    }

    [RelayCommand]
    public void CompileRules()
    {
        LogicEditorVM.SaveAndCompile();
    }

    [RelayCommand]
    public void ValidateLogic()
    {
        LogicEditorVM.SaveAndCompile();
        bool isVi = Loc.IsVietnamese;
        if (LogicEditorVM.CompileState == CompileState.Valid)
        {
            ShowNotification(
                isVi 
                    ? $"Đồ thị logic hoàn toàn hợp lệ!\n\nĐã kiểm tra và biên dịch thành công {LogicEditorVM.ValidRuleCount} quy tắc nhị phân 32-Byte (V1.7) sẵn sàng nạp xuống thiết bị." 
                    : $"Logic graph is completely valid!\n\nSuccessfully verified and compiled {LogicEditorVM.ValidRuleCount} 32-Byte binary rules (V1.7) ready for device deployment.",
                isVi ? "Kiểm tra tính hợp lệ logic" : "Validate Logic Graph",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        else
        {
            ShowNotification(
                isVi 
                    ? $"Phát hiện vấn đề trong cấu hình logic:\n\n{LogicEditorVM.Diagnostics}" 
                    : $"Logic configuration issues detected:\n\n{LogicEditorVM.Diagnostics}",
                isVi ? "Cảnh báo kiểm tra logic" : "Logic Validation Warning",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    public void ExportBinary()
    {
        LogicEditorVM.SaveAndCompile();

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Binary Rule Table (*.bin)|*.bin|All Files (*.*)|*.*",
            FileName = "rules_v1.7.bin",
            Title = Loc["MenuExportBinary"]
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                var bytes = RuleBinaryEncoder.EncodeProgramV17(RuleTableVM.Rules);
                ushort crc = Crc16Modbus.Compute(bytes);
                System.IO.File.WriteAllBytes(sfd.FileName, bytes);

                bool isVi = Loc.IsVietnamese;
                ShowNotification(
                    isVi 
                        ? $"Đã xuất thành công {RuleTableVM.Rules.Count} quy tắc ({bytes.Length} bytes) chuẩn Modbus 32-Byte (Contract V1.7).\n\nĐộ toàn vẹn CRC-16/MODBUS: 0x{crc:X4}\nĐường dẫn: {sfd.FileName}" 
                        : $"Successfully exported {RuleTableVM.Rules.Count} rules ({bytes.Length} bytes) in 32-byte Modbus format (Contract V1.7).\n\nIntegrity CRC-16/MODBUS: 0x{crc:X4}\nPath: {sfd.FileName}",
                    Loc["MenuExportBinary"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowNotification(ex.Message, "Error Exporting Binary", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ImportBinary()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Binary Rule Table (*.bin)|*.bin|All Files (*.*)|*.*",
            Title = Loc.IsVietnamese ? "Nhập cấu hình tệp nhị phân Firmware (*.bin)" : "Import Firmware Binary Config (*.bin)"
        };
        if (ofd.ShowDialog() == true)
        {
            try
            {
                var bytes = System.IO.File.ReadAllBytes(ofd.FileName);
                var decodedRules = RuleBinaryEncoder.DecodeProgram(bytes, TagCatalogVM.AllTags);
                string crcInfo = $"CRC-16/MODBUS: 0x{Crc16Modbus.Compute(bytes):X4} (Chuẩn V1.7 32-Byte)";

                RuleTableVM.ReplaceRules(decodedRules);

                IsProjectDirty = true;
                SelectTab(AppTabIndices.RuleTable); // View Rule Table

                bool isVi = Loc.IsVietnamese;
                ShowNotification(
                    isVi
                        ? $"Đã giải mã và nạp thành công {decodedRules.Count} quy tắc ({bytes.Length} bytes) từ tệp nhị phân!\n\nMã kiểm tra: {crcInfo}"
                        : $"Successfully decoded and imported {decodedRules.Count} rules ({bytes.Length} bytes) from binary file!\n\nChecksum: {crcInfo}",
                    isVi ? "Nhập cấu hình nhị phân thành công" : "Binary Import Succeeded",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowNotification(ex.Message, "Error Importing Binary", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ExportTagsCsv()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
            FileName = "plc_tags.csv",
            Title = Loc["MenuExportTagsCsv"]
        };
        if (sfd.ShowDialog() == true)
        {
            try
            {
                TagCsvService.ExportCsv(sfd.FileName, TagCatalogVM.AllTags);
                bool isVi = Loc.IsVietnamese;
                ShowNotification(
                    isVi 
                        ? $"Đã xuất thành công {TagCatalogVM.AllTags.Count} Tag I/O ra tệp CSV:\n{sfd.FileName}" 
                        : $"Successfully exported {TagCatalogVM.AllTags.Count} I/O Tags to CSV:\n{sfd.FileName}",
                    Loc["MenuExportTagsCsv"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowNotification(ex.Message, "Error Exporting Tags CSV", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    public void ImportTagsCsv()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv|All Files (*.*)|*.*",
            Title = Loc.IsVietnamese ? "Nhập danh mục Tag từ tệp CSV (*.csv)" : "Import I/O Tags from CSV (*.csv)"
        };
        if (ofd.ShowDialog() == true)
        {
            try
            {
                var (updated, added, errors) = TagCsvService.ImportCsv(ofd.FileName, TagCatalogVM);
                IsProjectDirty = true;
                SelectTab(AppTabIndices.TagCatalog); // View Tag Catalog

                bool isVi = Loc.IsVietnamese;
                string errStr = errors.Count > 0 ? $"\n\nLưu ý ({errors.Count} lỗi):\n" + string.Join("\n", errors.Take(3)) : "";
                ShowNotification(
                    isVi
                        ? $"Đã nhập bảng Tag thành công!\n- Cập nhật: {updated} Tags\n- Thêm mới: {added} Tags{errStr}"
                        : $"Successfully imported Tag table!\n- Updated: {updated} Tags\n- Added: {added} Tags{errStr}",
                    isVi ? "Nhập bảng Tag CSV" : "Import Tags CSV",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowNotification(ex.Message, "Error Importing Tags CSV", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

        [RelayCommand]
    public async Task DeployToDeviceAsync()
    {
        // 1. Tự động đồng bộ / biên dịch canvas nếu có chỉnh sửa đang dở
        if (LogicEditorVM.CompileState == CompileState.Stale)
        {
            LogicEditorVM.CompileNow();
        }

        // 2. Nếu chưa kết nối phần cứng, giữ nguyên tab và hiển thị thông báo trạng thái trực quan
        if (!IsConnected && !DeployVM.IsDeviceConnected)
        {
            string notConnectedTip = Loc["DeviceDeployNotConnectedTip"];
            LogicEditorVM.SaveStatusMessage = notConnectedTip;
            if (DtuGatewayStudioVM != null)
            {
                DtuGatewayStudioVM.DeployStatusMessage = notConnectedTip;
            }
            return;
        }

        // 3. Nếu đã kết nối, kích hoạt tiến trình nạp Staged & Atomic Commit
        DeployVM.UpdatePreflightSummary();
        if (DeployVM.CanDeploy)
        {
            var previousMessage = LogicEditorVM.SaveStatusMessage;
            string deployingText = Loc.IsVietnamese 
                ? "⚡ Đang nạp..." 
                : "⚡ Deploying...";
            LogicEditorVM.SaveStatusMessage = deployingText;
            if (DtuGatewayStudioVM != null)
            {
                DtuGatewayStudioVM.DeployStatusMessage = deployingText;
            }

            await DeployVM.StartDeployAsync();
            if (DeployVM.IsSuccess)
            {
                string successText = Loc.IsVietnamese 
                    ? "⚡ Nạp cấu hình thành công!" 
                    : "⚡ Configuration flashed successfully!";
                LogicEditorVM.SaveStatusMessage = successText;
                if (DtuGatewayStudioVM != null)
                {
                    DtuGatewayStudioVM.DeployStatusMessage = successText;
                }

                // Tự động biến mất sau 3 giây và khôi phục trạng thái bình thường (bảo vệ chống crash khi dispatcher shutdown)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000);
                        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                        {
                            dispatcher.Invoke(() =>
                            {
                                if (LogicEditorVM.SaveStatusMessage == successText)
                                {
                                    LogicEditorVM.SaveStatusMessage = LogicEditorVM.CompileState == CompileState.Valid
                                        ? string.Format(LocalizationService.Tr("SaveStatusValid"), LogicEditorVM.ValidRuleCount)
                                        : previousMessage;
                                }
                            });
                        }
                    }
                    catch
                    {
                        // Bỏ qua ngoại lệ nếu Dispatcher hoặc tiến trình đang shutdown trong quá trình teardown/test
                    }
                });
            }
            else
            {
                LogicEditorVM.SaveStatusMessage = previousMessage;
                SelectTab(AppTabIndices.Deploy);
            }
        }
        else
        {
            if (SelectedTabIndex == AppTabIndices.Device || SelectedTabIndex == AppTabIndices.LogicGraph)
            {
                string blockedMsg = DeployVM.PreflightSummary;
                LogicEditorVM.SaveStatusMessage = blockedMsg;
                if (DtuGatewayStudioVM != null)
                {
                    DtuGatewayStudioVM.DeployStatusMessage = blockedMsg;
                }
                return;
            }
            SelectTab(AppTabIndices.Deploy);
        }
    }

    public Action<string, string, System.Windows.MessageBoxButton, System.Windows.MessageBoxImage>? MessageBoxHandler { get; set; }
    public Func<string, string, System.Windows.MessageBoxButton, System.Windows.MessageBoxImage, System.Windows.MessageBoxResult>? ConfirmBoxHandler { get; set; }

    private void ShowNotification(string message, string caption, System.Windows.MessageBoxButton button = System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage image = System.Windows.MessageBoxImage.Information)
    {
        if (MessageBoxHandler != null)
        {
            MessageBoxHandler(message, caption, button, image);
        }
        else
        {
            System.Windows.MessageBox.Show(message, caption, button, image);
        }
    }

    private System.Windows.MessageBoxResult ShowConfirm(string message, string caption, System.Windows.MessageBoxButton button = System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage image = System.Windows.MessageBoxImage.Question)
    {
        return ConfirmBoxHandler != null
            ? ConfirmBoxHandler(message, caption, button, image)
            : System.Windows.MessageBox.Show(message, caption, button, image);
    }

    [RelayCommand(CanExecute = nameof(CanDoDeviceAction))]
    public async Task UploadFromDeviceAsync()
    {
        if (!CanDoDeviceAction)
        {
            ShowNotification(
                Loc["UploadNoConnection"],
                Loc["UploadFailedTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        if (IsProjectDirty || (LogicEditorVM != null && LogicEditorVM.Nodes.Count > 0))
        {
            var res = ShowConfirm(
                Loc["UploadConfirmOverwriteMessage"],
                Loc["UploadConfirmOverwriteTitle"],
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (res != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            var product = AppServices.Instance.CurrentProduct;
            if (product != null)
            {
                TagCatalogVM.SyncWithProductDefinition(product);
            }

            var (domainTable, fullConfig) = await AppServices.Instance.LoadFullDeviceUseCase.ExecuteAsync(product: product);

            // Đồng bộ cấu hình phần cứng và truyền thông toàn cục vào Phân hệ Thiết bị (Device Layer / ProjectMetadata)
            if (fullConfig.BrokerConfig != null)
            {
                CurrentProjectMetadata.MqttBrokerEnabled = fullConfig.BrokerConfig.Enabled;
                CurrentProjectMetadata.MqttBrokerHost = fullConfig.BrokerConfig.Host ?? string.Empty;
                CurrentProjectMetadata.MqttBrokerPort = fullConfig.BrokerConfig.Port > 0 ? fullConfig.BrokerConfig.Port : 1883;
                CurrentProjectMetadata.MqttTlsEnabled = fullConfig.BrokerConfig.UseTls;
                CurrentProjectMetadata.MqttCleanSession = fullConfig.BrokerConfig.CleanSession;
                CurrentProjectMetadata.MqttKeepAliveSec = fullConfig.BrokerConfig.KeepAliveSec > 0 ? fullConfig.BrokerConfig.KeepAliveSec : (ushort)60;
                CurrentProjectMetadata.MqttClientId = fullConfig.BrokerConfig.ClientId ?? string.Empty;
                CurrentProjectMetadata.MqttUsername = fullConfig.BrokerConfig.Username ?? string.Empty;
                CurrentProjectMetadata.MqttPassword = fullConfig.BrokerConfig.Password ?? string.Empty;
            }

            if (fullConfig.ModbusPorts != null && fullConfig.ModbusPorts.Count > 0)
            {
                CurrentProjectMetadata.ModbusPorts = fullConfig.ModbusPorts.Select(p => new ModbusPortConfigModel
                {
                    PortIndex = p.PortIndex,
                    PortName = string.IsNullOrWhiteSpace(p.PortName) ? $"RS-485 #{p.PortIndex + 1}" : p.PortName,
                    Enabled = p.Enabled,
                    BaudRate = p.BaudRate > 0 ? p.BaudRate : 9600,
                    DataBits = p.DataBits > 0 ? p.DataBits : (byte)8,
                    Parity = p.Parity.ToString(),
                    StopBits = p.StopBits > 0 ? p.StopBits : (byte)1,
                    ResponseTimeoutMs = p.ResponseTimeoutMs > 0 ? p.ResponseTimeoutMs : (ushort)200,
                    InterFrameDelayMs = p.InterFrameDelayMs > 0 ? p.InterFrameDelayMs : (ushort)20,
                    RetryCount = p.RetryCount
                }).ToList();
            }

            // Đồng bộ cấu hình Wi-Fi (SPLC-SPEC-WIFI-004)
            if (fullConfig.WifiConfig != null)
            {
                CurrentProjectMetadata.WifiEnabled = fullConfig.WifiConfig.Enabled;
                CurrentProjectMetadata.WifiAutoReconnect = fullConfig.WifiConfig.AutoReconnect;
                CurrentProjectMetadata.WifiSsid = fullConfig.WifiConfig.Ssid ?? string.Empty;
                CurrentProjectMetadata.WifiPassword = fullConfig.WifiConfig.Password ?? string.Empty;
                CurrentProjectMetadata.WifiDhcpEnabled = fullConfig.WifiConfig.IsDhcp;
                CurrentProjectMetadata.WifiStaticIp = string.IsNullOrWhiteSpace(fullConfig.WifiConfig.StaticIp) ? "192.168.1.50" : fullConfig.WifiConfig.StaticIp;
                CurrentProjectMetadata.WifiGateway = string.IsNullOrWhiteSpace(fullConfig.WifiConfig.StaticGateway) ? "192.168.1.1" : fullConfig.WifiConfig.StaticGateway;
                CurrentProjectMetadata.WifiSubnetMask = string.IsNullOrWhiteSpace(fullConfig.WifiConfig.StaticSubnet) ? "255.255.255.0" : fullConfig.WifiConfig.StaticSubnet;
                CurrentProjectMetadata.WifiDns = string.IsNullOrWhiteSpace(fullConfig.WifiConfig.StaticDns) ? "8.8.8.8" : fullConfig.WifiConfig.StaticDns;
            }

            // Đồng bộ cấu hình Zigbee (SPLC-SPEC-ZIGBEE-006)
            if (fullConfig.ZigbeeConfig != null)
            {
                CurrentProjectMetadata.ZigbeeConfig.Enabled = fullConfig.ZigbeeConfig.Enabled;
                CurrentProjectMetadata.ZigbeeConfig.Role = fullConfig.ZigbeeConfig.Role.ToString();
                CurrentProjectMetadata.ZigbeeConfig.SlaveId = fullConfig.ZigbeeConfig.SlaveId;
                CurrentProjectMetadata.ZigbeeConfig.PanIdHex = fullConfig.ZigbeeConfig.PanId.ToString("X4");
                CurrentProjectMetadata.ZigbeeConfig.Channel = fullConfig.ZigbeeConfig.Channel;
                CurrentProjectMetadata.ZigbeeConfig.Antenna = fullConfig.ZigbeeConfig.Antenna.ToString();
                CurrentProjectMetadata.ZigbeeConfig.SecurityEnabled = fullConfig.ZigbeeConfig.SecurityEnabled;
                CurrentProjectMetadata.ZigbeeConfig.PreserveKeyOnDeploy = fullConfig.ZigbeeConfig.PreserveKey;
            }

            // Đồng bộ cấu hình DTU Gateway khi thiết bị có cấu hình DTU (SPLC-SPEC-DTU-001)
            if (fullConfig.DtuConfig != null)
            {
                var dtu = fullConfig.DtuConfig;
                CurrentProjectMetadata.DtuConfig.Enabled = dtu.Enabled;
                CurrentProjectMetadata.DtuConfig.PortIndex = dtu.PortIndex;
                CurrentProjectMetadata.DtuConfig.SerialMode = dtu.SerialMode.ToString();
                CurrentProjectMetadata.DtuConfig.UplinkMode = dtu.UplinkMode.ToString();
                CurrentProjectMetadata.DtuConfig.FrameTimeoutDelayMs = dtu.FrameTimeoutDelayMs > 0 ? dtu.FrameTimeoutDelayMs : (ushort)20;
                CurrentProjectMetadata.DtuConfig.MaxPacketSize = dtu.MaxPacketSize > 0 ? dtu.MaxPacketSize : (ushort)512;
                CurrentProjectMetadata.DtuConfig.PayloadMode = dtu.PayloadMode.ToString();
                CurrentProjectMetadata.DtuConfig.IncludeDeviceId = (dtu.PayloadFlags & 0x01) != 0;
                CurrentProjectMetadata.DtuConfig.IncludeTimestamp = (dtu.PayloadFlags & 0x02) != 0;
                CurrentProjectMetadata.DtuConfig.IncludePort = (dtu.PayloadFlags & 0x04) != 0;
                CurrentProjectMetadata.DtuConfig.IncludeSequence = (dtu.PayloadFlags & 0x08) != 0;
                CurrentProjectMetadata.DtuConfig.IncludeHexStream = (dtu.PayloadFlags & 0x10) != 0;
                CurrentProjectMetadata.DtuConfig.IncludePayloadLength = (dtu.PayloadFlags & 0x20) != 0;

                if (dtu.HttpConfig != null)
                {
                    CurrentProjectMetadata.DtuConfig.HttpSettings.Enabled = dtu.HttpConfig.Enabled;
                    CurrentProjectMetadata.DtuConfig.HttpSettings.Method = dtu.HttpConfig.Method == SimplePLC.Protocol.Enums.DtuHttpMethod.Put ? "PUT" : "POST";
                    CurrentProjectMetadata.DtuConfig.HttpSettings.Url = dtu.HttpConfig.Url ?? string.Empty;
                    CurrentProjectMetadata.DtuConfig.HttpSettings.Headers = dtu.HttpConfig.CustomHeaders ?? string.Empty;
                    CurrentProjectMetadata.DtuConfig.HttpSettings.TimeoutSec = dtu.HttpConfig.TimeoutSec > 0 ? dtu.HttpConfig.TimeoutSec : (ushort)10;
                    CurrentProjectMetadata.DtuConfig.HttpSettings.PeriodicIntervalSec = dtu.HttpConfig.PeriodicIntervalSec > 0 ? dtu.HttpConfig.PeriodicIntervalSec : (ushort)5;
                    CurrentProjectMetadata.DtuConfig.HttpSettings.TriggerMode = dtu.HttpConfig.TriggerMode.ToString();
                }

                if (dtu.TcpConfig != null)
                {
                    CurrentProjectMetadata.DtuConfig.TcpSettings.Enabled = dtu.TcpConfig.Enabled;
                    CurrentProjectMetadata.DtuConfig.TcpSettings.SocketMode = dtu.TcpConfig.SocketMode.ToString();
                    CurrentProjectMetadata.DtuConfig.TcpSettings.RemoteHost = dtu.TcpConfig.RemoteHost ?? string.Empty;
                    CurrentProjectMetadata.DtuConfig.TcpSettings.RemotePort = dtu.TcpConfig.RemotePort > 0 ? dtu.TcpConfig.RemotePort : (ushort)8888;
                    CurrentProjectMetadata.DtuConfig.TcpSettings.LocalPort = dtu.TcpConfig.LocalPort > 0 ? dtu.TcpConfig.LocalPort : (ushort)8888;
                    CurrentProjectMetadata.DtuConfig.TcpSettings.ReconnectDelaySec = dtu.TcpConfig.ReconnectDelaySec > 0 ? dtu.TcpConfig.ReconnectDelaySec : (ushort)5;
                    CurrentProjectMetadata.DtuConfig.TcpSettings.HeartbeatIntervalSec = dtu.TcpConfig.HeartbeatIntervalSec > 0 ? dtu.TcpConfig.HeartbeatIntervalSec : (ushort)30;
                    CurrentProjectMetadata.DtuConfig.TcpSettings.HeartbeatPayload = dtu.TcpConfig.HeartbeatPayload ?? string.Empty;
                    CurrentProjectMetadata.DtuConfig.TcpSettings.RegistrationType = dtu.TcpConfig.RegistrationType.ToString();
                    CurrentProjectMetadata.DtuConfig.TcpSettings.RegistrationPayload = dtu.TcpConfig.RegistrationPayload ?? string.Empty;
                }

                if (dtu.MqttWillConfig != null)
                {
                    CurrentProjectMetadata.MqttWillEnabled = dtu.MqttWillConfig.Enabled;
                    CurrentProjectMetadata.MqttWillTopic = dtu.MqttWillConfig.Topic ?? string.Empty;
                    CurrentProjectMetadata.MqttWillPayload = dtu.MqttWillConfig.Payload ?? string.Empty;
                    CurrentProjectMetadata.MqttWillQos = dtu.MqttWillConfig.Qos;
                    CurrentProjectMetadata.MqttWillRetain = dtu.MqttWillConfig.Retain;
                }

                DtuGatewayStudioVM?.ReloadFromMetadata();
            }
            else
            {
                DtuGatewayStudioVM?.ReloadFromMetadata();
            }

            RuleTableVM.LoadFromDomainRuleTable(domainTable);
            RuleTableVM.UpdateSubsystemsFromDevice(fullConfig);
            LogicEditorVM?.ReconstructFullGraphFromDevice(fullConfig, RuleTableVM.Rules);
            IsProjectDirty = false;
            SelectTab(AppTabIndices.LogicGraph); // Luôn chuyển về màn Logic Graph để kỹ sư lập trình

            ShowNotification(
                string.Format(Loc["UploadSuccessMessage"], domainTable.Count),
                Loc["UploadSuccessTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowNotification(
                ex.Message,
                Loc["UploadFailedTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDoDeviceAction))]
    public async Task RebootDeviceAsync()
    {
        if (!CanDoDeviceAction)
        {
            ShowNotification(
                Loc["UploadNoConnection"],
                Loc.IsVietnamese ? "Lỗi thao tác thiết bị" : "Device Command Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var confirm = ShowConfirm(
            Loc["RebootConfirmMessage"],
            Loc["RebootConfirmTitle"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var result = await AppServices.Instance.CommandUseCase.ExecuteAsync(SimplePLC.Protocol.Enums.SPLC_SystemCommand.REBOOT);
            if (result.IsSuccess)
            {
                ShowNotification(
                    Loc.IsVietnamese 
                        ? "Đã gửi lệnh khởi động lại thành công. Thiết bị đang khởi động lại. Vui lòng bấm 'Kết nối thiết bị' khi thiết bị đã sẵn sàng." 
                        : "Reboot command sent successfully. Device is restarting. Please click 'Connect Device' when ready.",
                    Loc["RebootConfirmTitle"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                string errDetail = Loc.IsVietnamese 
                    ? $"Lệnh bị từ chối: {result.ErrorCode} (Status: {result.Status})"
                    : $"Command rejected: {result.ErrorCode} (Status: {result.Status})";
                ShowNotification(
                    errDetail,
                    Loc.IsVietnamese ? "Lỗi khởi động lại" : "Reboot Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message, "Reboot Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDoDeviceAction))]
    public async Task FactoryResetDeviceAsync()
    {
        if (!CanDoDeviceAction)
        {
            ShowNotification(
                Loc["UploadNoConnection"],
                Loc.IsVietnamese ? "Lỗi thao tác thiết bị" : "Device Command Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        var confirm = ShowConfirm(
            Loc["FactoryResetConfirmMessage"],
            Loc["FactoryResetConfirmTitle"],
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            var result = await AppServices.Instance.CommandUseCase.ExecuteAsync(SimplePLC.Protocol.Enums.SPLC_SystemCommand.FACTORY_RESET);
            if (result.IsSuccess)
            {
                ShowNotification(
                    Loc.IsVietnamese 
                        ? "Khôi phục cài đặt gốc thành công. Toàn bộ quy tắc đã được xóa và thiết bị đang khởi động lại. Vui lòng bấm 'Kết nối thiết bị' khi thiết bị đã sẵn sàng." 
                        : "Factory reset successful. All rules erased and device is restarting. Please click 'Connect Device' when ready.",
                    Loc["FactoryResetConfirmTitle"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                string errDetail = Loc.IsVietnamese 
                    ? $"Lệnh bị từ chối: {result.ErrorCode} (Status: {result.Status})"
                    : $"Command rejected: {result.ErrorCode} (Status: {result.Status})";
                ShowNotification(
                    errDetail,
                    Loc.IsVietnamese ? "Lỗi Factory Reset" : "Factory Reset Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message, "Factory Reset Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void DeleteSelectedNode()
    {
        LogicEditorVM.DeleteSelectedNode();
    }

    [RelayCommand]
    public void AlignTop()
    {
        LogicEditorVM.AlignTop();
    }

    [RelayCommand]
    public void AlignMiddle()
    {
        LogicEditorVM.AlignMiddle();
    }

    [RelayCommand]
    public void AlignBottom()
    {
        LogicEditorVM.AlignBottom();
    }

    [RelayCommand]
    public void AlignLeft()
    {
        LogicEditorVM.AlignLeft();
    }

    [RelayCommand]
    public void AlignCenter()
    {
        LogicEditorVM.AlignCenter();
    }

    [RelayCommand]
    public void AlignRight()
    {
        LogicEditorVM.AlignRight();
    }

    [RelayCommand]
    public void AlignSelectedNodesHorizontally()
    {
        LogicEditorVM.AlignSelectedNodesHorizontally();
    }

    [RelayCommand]
    public void AlignSelectedNodesVertically()
    {
        LogicEditorVM.AlignSelectedNodesVertically();
    }

    [RelayCommand]
    public void DistributeNodesHorizontally()
    {
        LogicEditorVM.DistributeNodesHorizontally();
    }

    [RelayCommand]
    public void DistributeNodesVertically()
    {
        LogicEditorVM.DistributeNodesVertically();
    }

    [RelayCommand]
    public void SnapAllNodesToGrid()
    {
        LogicEditorVM.SnapAllNodesToGrid();
    }

    [RelayCommand]
    public void ToggleGrid()
    {
        LogicEditorVM.ToggleGrid();
    }

    [RelayCommand]
    public void LoadDefaultDemo()
    {
        if (PromptSaveIfDirty() == false) return;
        LogicEditorVM.LoadDefaultDemoGraph();
        LogicEditorVM.CompileNow();
        CurrentProjectPath = null;
        CurrentProjectName = Loc.IsVietnamese ? "Demo Giám Sát" : "Demo Graph";
        IsProjectDirty = false;
        SelectTab(AppTabIndices.LogicGraph);
    }


    [RelayCommand]
    public void Undo()
    {
        LogicEditorVM.Undo();
    }

    [RelayCommand]
    public void Redo()
    {
        LogicEditorVM.Redo();
    }

    [RelayCommand]
    public void Copy()
    {
        LogicEditorVM.Copy();
    }

    [RelayCommand]
    public void Cut()
    {
        LogicEditorVM.Cut();
    }

    [RelayCommand]
    public void Paste()
    {
        LogicEditorVM.Paste();
    }

    [RelayCommand]
    public void Duplicate()
    {
        LogicEditorVM.Duplicate();
    }

    [RelayCommand]
    public void SelectAll()
    {
        LogicEditorVM.SelectAll();
    }

    [RelayCommand]
    public async Task CheckUpdates()
    {
        try
        {
            var info = await UpdateService.CheckForUpdateAsync();
            if (info.HasUpdate)
            {
                IsUpdateAvailable = true;
                UpdateAvailableText = $"⚡ v{info.LatestVersion}";

                var dialog = new Views.UpdateDialogView(info, UpdateService);
                dialog.ShowDialog();
            }
            else if (!string.IsNullOrEmpty(info.ErrorMessage))
            {
                ShowNotification(
                    Loc.IsVietnamese
                        ? $"Thông tin cập nhật:\n\n{info.ErrorMessage}"
                        : $"Update notification:\n\n{info.ErrorMessage}",
                    Loc["UpdateTitle"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            else
            {
                ShowNotification(
                    string.Format(Loc.IsVietnamese
                        ? "Bạn đang sử dụng phiên bản mới nhất (SynaptiX IDE v{0}).\nHệ thống đã sẵn sàng với chuẩn điều khiển SynaptiX & Tiêu chuẩn an toàn SPLC-AF-003."
                        : "You are using the latest version (SynaptiX IDE v{0}).\nSystem is ready with SynaptiX standard & SPLC-AF-003 safety standard.", UpdateService.CurrentVersion),
                    Loc["UpdateTitle"],
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            ShowNotification(
                ex.Message,
                Loc["UpdateTitle"],
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void AboutApp()
    {
        string currentVer = AppUpdateService.GetCurrentAppVersion();
        string message = string.Format(Loc["AboutMessage"], currentVer);

        ShowNotification(
            message,
            Loc["AboutTitle"],
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    /// <summary>
    /// Tạo URL mở cổng Web Wiki tài liệu kỹ thuật dựa trên ngôn ngữ giao diện đang hoạt động.
    /// </summary>
    /// <returns>URL trang tài liệu Tiếng Việt (/vi/) hoặc Tiếng Anh (gốc).</returns>
    public string GetDocumentationUrl()
    {
        string baseUrl = AppUpdateService.DocumentationPortalUrl;
        return Loc.IsVietnamese ? $"{baseUrl}vi/" : baseUrl;
    }

    /// <summary>
    /// Mở cổng Web Wiki tài liệu kỹ thuật trực tuyến của SynaptiX trong trình duyệt mặc định của hệ điều hành.
    /// </summary>
    [RelayCommand]
    public void OpenDocSpec()
    {
        try
        {
            string url = GetDocumentationUrl();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowNotification(ex.Message, "Docs Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void ToggleFullScreen()
    {
        var win = System.Windows.Application.Current.MainWindow;
        if (win == null) return;
        if (win.WindowState == System.Windows.WindowState.Maximized && win.WindowStyle == System.Windows.WindowStyle.None)
        {
            win.WindowStyle = System.Windows.WindowStyle.SingleBorderWindow;
            win.WindowState = System.Windows.WindowState.Normal;
        }
        else
        {
            win.WindowStyle = System.Windows.WindowStyle.None;
            win.WindowState = System.Windows.WindowState.Maximized;
        }
    }

    [RelayCommand]
    public void ToggleSimulation()
    {
        if (SelectedTabIndex != AppTabIndices.LogicGraph)
        {
            SelectTab(AppTabIndices.LogicGraph);
        }
        LogicEditorVM.ToggleSimPlayPauseCommand.Execute(null);
    }

    [RelayCommand]
    public void ExitApp()
    {
        if (System.Windows.Application.Current.MainWindow != null)
        {
            System.Windows.Application.Current.MainWindow.Close();
        }
        else
        {
            System.Windows.Application.Current.Shutdown();
        }
    }

    public void ApplyAiRulesToTable(List<AiRuleSpecModel> specs, ChatMessageModel message)
    {
        if (specs == null || specs.Count == 0) return;

        int addedCount = 0;
        foreach (var spec in specs)
        {
            var triggerTag = FindTagByName(spec.InputTag);
            var actionTag = FindTagByName(spec.ActionTag);

            if (!Enum.TryParse<TriggerType>(spec.Trigger, true, out var triggerType))
            {
                triggerType = TriggerType.ON_RISE;
            }

            // 1. Phân tích toán tử so sánh (CompareOp) & Ngưỡng (ThresholdLo/Hi)
            CompareOp compareOp = CompareOp.NONE;
            int thresholdLo = spec.ThresholdLo;
            int thresholdHi = spec.ThresholdHi;

            if (Enum.TryParse<CompareOp>(spec.CompareOp, true, out var parsedTrigOp) && parsedTrigOp != CompareOp.NONE)
            {
                compareOp = parsedTrigOp;
            }
            else if (Enum.TryParse<CompareOp>(spec.GuardOp, true, out var parsedGuardOp) && parsedGuardOp != CompareOp.NONE)
            {
                // Phòng thủ: AI phân loại nhầm điều kiện ngưỡng của Trigger sang Guard
                compareOp = parsedGuardOp;
                if (thresholdLo == 0 && spec.GuardVal != 0)
                {
                    thresholdLo = spec.GuardVal;
                }
            }

            // 2. Phân tích Guard (Khóa an toàn Boolean: DI0..7, VFLAG0..15)
            TagModel? guardTag = null;
            if (!string.IsNullOrWhiteSpace(spec.GuardTag) && !string.Equals(spec.GuardTag, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                var candidateGuardTag = FindTagByName(spec.GuardTag);
                if (candidateGuardTag != null)
                {
                    bool isSameAsTrigger = triggerTag != null && candidateGuardTag.Index == triggerTag.Index;
                    bool isNonBoolean = !candidateGuardTag.IsDigital;

                    if (isSameAsTrigger || isNonBoolean)
                    {
                        // Tag của Guard là Analog hoặc trùng với Trigger -> Đây là điều kiện đo lường của Trigger, không phải Interlock Boolean
                        guardTag = null;
                        if (compareOp == CompareOp.NONE && Enum.TryParse<CompareOp>(spec.GuardOp, true, out var op))
                        {
                            compareOp = op;
                            if (thresholdLo == 0) thresholdLo = spec.GuardVal;
                        }
                    }
                    else
                    {
                        guardTag = candidateGuardTag;
                    }
                }
            }

            if (!Enum.TryParse<ActionType>(spec.ActionType, true, out var actionType))
            {
                actionType = ActionType.SET_TAG;
            }

            int newIndex = RuleTableVM.Rules.Count;
            var rule = new RuleItemModel
            {
                Index = newIndex,
                Id = $"R{newIndex + 1}",
                Enabled = true,
                TriggerTag = triggerTag,
                TriggerType = triggerType,
                ForMs = spec.ForMs,
                GuardTag = guardTag,
                CompareOp = compareOp,
                ThresholdLo = thresholdLo,
                ThresholdHi = thresholdHi,
                GuardNegated = spec.GuardNegated,
                ActionTag = actionTag,
                ActionType = actionType,
                ActionParam = spec.Param,
                Narrative = !string.IsNullOrWhiteSpace(spec.Narrative)
                    ? spec.Narrative
                    : $"Rule AI: {spec.InputTag} -> {spec.ActionTag}"
            };

            rule.UpdateNarrative();
            RuleTableVM.Rules.Add(rule);
            addedCount++;
        }

        if (addedCount > 0)
        {
            RuleTableVM.ApplyFilter();
            IsProjectDirty = true;
            message.IsApplied = true;

            // Chuyển sang Tab Bảng Rule để kỹ sư xem ngay
            SelectTab(AppTabIndices.RuleTable);

            string toast = LocalizationService.Instance.IsVietnamese
                ? $"✨ Đã nạp thành công {addedCount} Rule từ AI vào Bảng Rule!"
                : $"✨ Successfully added {addedCount} AI-generated rule(s) to Rule Table!";
            RuntimeStatusText = toast;
        }
    }

    private TagModel? FindTagByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || string.Equals(name, "NONE", StringComparison.OrdinalIgnoreCase))
            return null;

        return TagCatalogVM.AllTags.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.Alias, name, StringComparison.OrdinalIgnoreCase));
    }

    private void UpdateRtcState(SimplePLC.Protocol.Dto.RtcClockDto rtc)
    {
        int tzHours = rtc.TimezoneOffsetMinutes / 60;
        string tzSign = rtc.TimezoneOffsetMinutes >= 0 ? "+" : "";
        McuClockDisplayText = $"{rtc.LocalDateTime:HH:mm} (UTC{tzSign}{tzHours})";
        IsMcuClockSynced = rtc.IsSynced;
        HasHardwareRtc = rtc.HasHardwareRtc;
        IsRtcBatteryLow = rtc.IsBatteryLow;

        string hwText = rtc.HasHardwareRtc
            ? (Loc.IsVietnamese ? "Có IC RTC phần cứng (Duy trì giờ khi tắt nguồn)" : "Hardware RTC present (Retains time on power-off)")
            : (Loc.IsVietnamese ? "Đồng hồ phần mềm (SysTick)" : "Software clock (SysTick)");

        string syncText = rtc.IsSynced
            ? (Loc.IsVietnamese ? "Đã đồng bộ tin cậy" : "Synchronized")
            : (Loc.IsVietnamese ? "Chưa đồng bộ" : "Not synchronized");

        string warningText = rtc.IsBatteryLow
            ? (Loc.IsVietnamese ? "\n⚠ Cảnh báo: Đồng hồ vừa bị gián đoạn nguồn nuôi!" : "\n⚠ Warning: Clock was halted due to power interruption!")
            : string.Empty;

        RtcStatusTooltip = $"{hwText}\nTrạng thái: {syncText}{warningText}\nGiờ thiết bị: {rtc.LocalDateTime:HH:mm:ss dd/MM/yyyy}";
    }

    private async Task AutoSyncMcuClockAsync()
    {
        try
        {
            // Khi vừa kết nối: Chỉ ĐỌC thời gian và thông tin phần cứng từ MCU lên để cập nhật Status Bar,
            // TUYỆT ĐỐI KHÔNG tự động ghi đè múi giờ PC xuống MCU (tránh phá hỏng múi giờ quốc tế đã cấu hình của thiết bị).
            var rtc = await AppServices.Instance.RtcClockClient.ReadRtcClockAsync().ConfigureAwait(false);
            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            {
                dispatcher.Invoke(() =>
                {
                    UpdateRtcState(rtc);
                });
            }
        }
        catch
        {
            // Bỏ qua lỗi trong quá trình đọc thời gian khi vừa kết nối
        }
    }

    /// <summary>
    /// Cho phép can thiệp mở dialog trong kiểm thử tự động.
    /// </summary>
    public Func<RtcSyncViewModel, bool?>? RtcSyncDialogHandler { get; set; }

    [RelayCommand]
    public void SyncMcuClock()
    {
        if (!IsConnected)
            return;

        try
        {
            var vm = new RtcSyncViewModel(AppServices.Instance.RtcClockClient);

            bool? result;
            if (RtcSyncDialogHandler != null)
            {
                result = RtcSyncDialogHandler(vm);
            }
            else
            {
                var dialog = new Views.RtcSyncDialogView(vm);
                if (System.Windows.Application.Current?.MainWindow is { } owner && owner.IsVisible)
                {
                    // dialog.Owner is omitted to enable independent minimize
                }
                result = dialog.ShowDialog();
            }

            if (result == true && vm.SyncedResult is { } rtc)
            {
                UpdateRtcState(rtc);
                string tzSign = rtc.TimezoneOffsetMinutes >= 0 ? "+" : "";
                int tzHours = rtc.TimezoneOffsetMinutes / 60;
                RuntimeStatusText = Loc.IsVietnamese
                    ? $"Đã đồng bộ thời gian thực xuống thiết bị: {rtc.LocalDateTime:HH:mm:ss dd/MM/yyyy} (UTC{tzSign}{tzHours})"
                    : $"Synchronized Real-Time Clock to Controller: {rtc.LocalDateTime:HH:mm:ss yyyy-MM-dd} (UTC{tzSign}{tzHours})";
            }
        }
        catch (Exception ex)
        {
            ShowNotification(
                (Loc.IsVietnamese ? "Lỗi đồng bộ thời gian thực: " : "Failed to sync real-time clock: ") + ex.Message,
                Loc.IsVietnamese ? "Đồng bộ thời gian" : "Time Sync",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }
}

public partial class PortOption : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEnabled = true;
}

