using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SimplePLC.Application.Models;
using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Models;
using SimplePLC.Domain.Validation;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Trạng thái của từng bước trong quy trình biên dịch và nạp cấu hình xuống thiết bị.
/// </summary>
public enum StepStatus { Pending, InProgress, Success, Failed }

/// <summary>
/// Mục hiển thị từng bước nạp cấu hình trong danh sách Deploy.
/// </summary>
public partial class DeployStepItem : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _detail = string.Empty;
    [ObservableProperty] private StepStatus _status = StepStatus.Pending;
}

public partial class DeployViewModel : ObservableObject
{
    private readonly DeployRulesUseCase? _injectedDeployUseCase;
    private readonly RuleTableViewModel? _ruleTableVM;
    private readonly LogicEditorViewModel? _logicEditorVM;
    private readonly Action<int>? _navigateTab;
    private readonly Func<ProjectMetadata>? _getMetadata;

    public ObservableCollection<DeployStepItem> Steps { get; } = new();

    /// <summary>
    /// Độ trễ (ms) mô phỏng tiến trình trên giao diện người dùng để thanh tiến trình chạy mượt mà. Mặc định 60ms.
    /// Tự động về 0ms khi làm việc với FakeModbusClient trong Unit/Integration Tests để tối ưu hóa thời gian thực thi.
    /// </summary>
    public int StepDelayMs { get; set; } = 60;

    private int EffectiveDelayMs =>
        (StepDelayMs <= 0 || AppServices.Instance.ModbusClient.GetType().Name.Contains("Fake"))
            ? 0
            : StepDelayMs;

    private CancellationTokenSource? _autoResetCts;

    /// <summary>
    /// Thời gian chờ (ms) trước khi tự động hoàn nguyên giao diện nạp về trạng thái Sẵn sàng (Ready).
    /// Mặc định 3000ms (3 giây). Trong Unit Test với FakeModbusClient, tự động vô hiệu hóa để đảm bảo tính bất biến khi assert.
    /// </summary>
    public int AutoResetSuccessDelayMs { get; set; } = 3000;

    private int EffectiveResetDelayMs => AutoResetSuccessDelayMs > 0 ? AutoResetSuccessDelayMs : 0;

    /// <summary>
    /// Lập lịch tự động hoàn nguyên Stepper và thanh tiến độ về trạng thái Sẵn sàng (Ready) sau khi nạp thành công.
    /// Toàn bộ lịch sử trong ConsoleLog được giữ nguyên để kỹ sư tra cứu.
    /// </summary>
    public void ScheduleAutoResetSuccess(int delayMs = 3000)
    {
        _autoResetCts?.Cancel();
        _autoResetCts?.Dispose();
        _autoResetCts = new CancellationTokenSource();
        var token = _autoResetCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, token).ConfigureAwait(false);
                if (token.IsCancellationRequested) return;

                void Reset()
                {
                    ResetToReadyState();
                }

                if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                {
                    dispatcher.Invoke(Reset);
                }
                else
                {
                    Reset();
                }
            }
            catch (OperationCanceledException)
            {
                // Bình thường khi hủy tác vụ nền
            }
            catch
            {
                // Bỏ qua ngoại lệ teardown/shutdown
            }
        }, token);
    }

    /// <summary>
    /// Hoàn nguyên giao diện về trạng thái Sẵn sàng (Ready) để sẵn sàng cho lần nạp tiếp theo.
    /// </summary>
    public void ResetToReadyState()
    {
        _autoResetCts?.Cancel();
        if (IsSuccess)
        {
            IsSuccess = false;
            DeployState = DeployState.Idle;
            ShowProgressBar = false;
            ProgressPercent = 0;
            InitializeSteps();
        }
        UpdatePreflightSummary();
        OnPropertyChanged(nameof(CanDeploy));
        StartDeployCommand.NotifyCanExecuteChanged();
        PrimaryActionCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty] private int _progressPercent;
    [ObservableProperty] private bool _isDeploying;
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private DeployState _deployState = DeployState.Idle;
    [ObservableProperty] private string _preflightSummary = "";
    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _statusTitle = "";
    [ObservableProperty] private string _statusSubtitle = "";
    [ObservableProperty] private string _stepperBadgeText = "Chờ bắt đầu";
    [ObservableProperty] private bool _showProgressBar;
    [ObservableProperty] private string _actionButtonText = "Bắt Đầu Nạp Xuống Thiết Bị";
    [ObservableProperty] private string _consoleLog = "--- STAGED MODBUS DEPLOYMENT (R10) ---\n";
    [ObservableProperty] private string _deviceInfoBadge = "SynaptiX Controller · RS-485/USB · Chưa kết nối";
    [ObservableProperty] private string _manifestRuleSummary = "";
    [ObservableProperty] private int _manifestRuleCount;
    [ObservableProperty] private string _manifestRuleCountText = "";
    [ObservableProperty] private long _lastDeployDurationMs;
    [ObservableProperty] private bool _hasValidRules;
    [ObservableProperty] private bool _isDeviceConnected;

    public bool HasDeviceModbusConfigToDeploy()
    {
        var meta = _getMetadata?.Invoke();
        if (meta?.ModbusPorts == null) return false;
        return meta.ModbusPorts.Any(p => p.Enabled && p.MasterChannels != null && p.MasterChannels.Count > 0);
    }

    public bool HasDeviceConfigToDeploy()
    {
        var meta = _getMetadata?.Invoke();
        if (meta == null) return false;
        bool hasMb = meta.ModbusPorts != null && meta.ModbusPorts.Any(p => p.Enabled);
        bool hasMqtt = meta.MqttBrokerEnabled || meta.MqttWillEnabled || meta.MqttTlsEnabled;
        bool hasDtu = meta.DtuConfig != null && meta.DtuConfig.Enabled;
        return hasMb || hasMqtt || hasDtu;
    }

    public int GetTab0ModbusChannelsCount(ProjectMetadata? meta)
    {
        if (meta?.ModbusPorts == null) return 0;
        return meta.ModbusPorts
            .Where(p => p.Enabled && p.MasterChannels != null)
            .Sum(p => p.MasterChannels.Count(c => string.IsNullOrEmpty(c.LinkedNodeId)));
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của quyền sở hữu ghi Tag đối với các luồng truyền thông ngoại vi (Modbus Read & MQTT Subscribe).
    /// Từ chối nạp cấu hình nếu phát hiện xung đột race condition (nhiều luồng ghi vào cùng một Tag).
    /// Hỗ trợ song ngữ chuẩn theo cài đặt hệ thống (LocalizationService).
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateIngestionOwnership(
        IReadOnlyList<ModbusMasterReadConfigDto>? modbusReads,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubs)
    {
        var claims = new Dictionary<int, string>();
        var allTags = _logicEditorVM?.TagCatalog?.AllTags;
        bool isVi = LocalizationService.Instance.IsVietnamese;

        if (modbusReads != null)
        {
            foreach (var read in modbusReads)
            {
                if (read.Pins == null) continue;
                foreach (var pin in read.Pins)
                {
                    if (pin.TargetTagIndex.HasValue)
                    {
                        int tagIdx = pin.TargetTagIndex.Value;
                        string nodeInfo = !string.IsNullOrWhiteSpace(read.NodeId)
                            ? (isVi ? $" (Khối '{read.NodeId}')" : $" (Node '{read.NodeId}')")
                            : "";
                        string currentSource = string.Format(
                            LocalizationService.Tr("DeployOwnershipModbusMasterSource"),
                            read.PortIndex,
                            read.StartAddress,
                            nodeInfo);

                        if (claims.TryGetValue(tagIdx, out var existingSource))
                        {
                            var tag = allTags?.FirstOrDefault(t => t.Index == tagIdx);
                            string tagDisplay = tag != null ? $"{tag.Name} (Index {tagIdx})" : $"Tag {tagIdx}";
                            return (false, string.Format(LocalizationService.Tr("DeployOwnershipConflict"), tagDisplay, existingSource, currentSource));
                        }
                        claims[tagIdx] = currentSource;
                    }
                }
            }
        }

        if (mqttSubs != null)
        {
            foreach (var sub in mqttSubs)
            {
                if (sub.Pins == null) continue;
                foreach (var pin in sub.Pins)
                {
                    if (pin.TargetTagIndex.HasValue)
                    {
                        int tagIdx = pin.TargetTagIndex.Value;
                        string currentSource = string.Format(
                            LocalizationService.Tr("DeployOwnershipMqttSubSource"),
                            sub.Topic);

                        if (claims.TryGetValue(tagIdx, out var existingSource))
                        {
                            var tag = allTags?.FirstOrDefault(t => t.Index == tagIdx);
                            string tagDisplay = tag != null ? $"{tag.Name} (Index {tagIdx})" : $"Tag {tagIdx}";
                            return (false, string.Format(LocalizationService.Tr("DeployOwnershipConflict"), tagDisplay, existingSource, currentSource));
                        }
                        claims[tagIdx] = currentSource;
                    }
                }
            }
        }

        return (true, null);
    }

    public bool CanDeploy => !IsDeploying
        && (
            // Kiểm tra kết nối nếu đang trong môi trường ứng dụng thực tế
            !AppServices.Instance.SessionManager.HasActiveSession && !AppServices.Instance.ModbusClient.IsConnected
                ? false 
                : true
        )
        && (
            _logicEditorVM == null 
            || (_logicEditorVM.CompileState != CompileState.Invalid && _logicEditorVM.CompileState != CompileState.Stale)
        );

    public DeployViewModel(
        DeployRulesUseCase? deployUseCase = null, 
        RuleTableViewModel? ruleTableVM = null,
        LogicEditorViewModel? logicEditorVM = null,
        Action<int>? navigateTab = null,
        Func<ProjectMetadata>? getMetadata = null)
    {
        _injectedDeployUseCase = deployUseCase;
        _ruleTableVM = ruleTableVM;
        _logicEditorVM = logicEditorVM;
        _navigateTab = navigateTab;
        _getMetadata = getMetadata;

        if (_ruleTableVM != null)
        {
            _ruleTableVM.Rules.CollectionChanged += OnRulesCollectionChanged;
            foreach (var rule in _ruleTableVM.Rules)
            {
                rule.PropertyChanged += OnRulePropertyChanged;
            }
        }

        if (_logicEditorVM != null)
        {
            _logicEditorVM.PropertyChanged += OnLogicEditorPropertyChanged;
        }

        // Theo dõi thay đổi trạng thái kết nối để cập nhật CanDeploy
        AppServices.Instance.SessionManager.SessionChanged += OnSessionChanged;

        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        InitializeSteps();
        UpdateDeviceInfo(AppServices.Instance.SessionManager.HasActiveSession ? AppServices.Instance.SessionManager.CurrentSession : null);
        UpdatePreflightSummary();
    }

    private void OnSessionChanged(SimplePLC.Application.Abstractions.IDeviceSession? session)
    {
        void Update()
        {
            UpdateDeviceInfo(session);
            UpdatePreflightSummary();
            OnPropertyChanged(nameof(CanDeploy));
            StartDeployCommand.NotifyCanExecuteChanged();
            PrimaryActionCommand.NotifyCanExecuteChanged();
        }

        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(Update);
        }
        else
        {
            Update();
        }
    }

    public void UpdateDeviceInfo(SimplePLC.Application.Abstractions.IDeviceSession? session)
    {
        var active = session ?? (AppServices.Instance.SessionManager.HasActiveSession ? AppServices.Instance.SessionManager.CurrentSession : null);
        bool isVi = LocalizationService.Instance.IsVietnamese;
        if (active?.Descriptor != null)
        {
            var d = active.Descriptor;
            string port = active.Endpoint is UsbCdcEndpoint usb ? usb.PortName : (active.Endpoint?.ToString() ?? "COM");
            DeviceInfoBadge = $"SynaptiX Controller · RS-485/USB ({port}) · {(isVi ? "Trực tuyến" : "Online")}";
        }
        else
        {
            DeviceInfoBadge = isVi
                ? "SynaptiX Controller · RS-485/USB · Chưa kết nối"
                : "SynaptiX Controller · RS-485/USB · Disconnected";
        }
    }

    private void OnLanguageChanged()
    {
        UpdateDeviceInfo(null);
        UpdatePreflightSummary();
        InitializeSteps();
    }

    private void OnRulesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (RuleItemModel r in e.OldItems)
                r.PropertyChanged -= OnRulePropertyChanged;
        }
        if (e.NewItems != null)
        {
            foreach (RuleItemModel r in e.NewItems)
                r.PropertyChanged += OnRulePropertyChanged;
        }

        ResetDeployStateOnConfigChange();
    }

    private void OnRulePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        // Khi lưu sơ đồ (Save / Ctrl+S), SourceNodes (chứa X, Y) trên RuleItemModel bị cập nhật.
        // Bỏ qua các thay đổi tọa độ thuần túy giao diện; bảo toàn trạng thái nạp nếu mã băm logic không đổi.
        if (e.PropertyName is nameof(RuleItemModel.SourceNodes) or nameof(RuleItemModel.SourceConnections))
        {
            return;
        }

        if (IsSuccess && _logicEditorVM?.CurrentProgram != null && !string.IsNullOrEmpty(_logicEditorVM.LastDeployedProgramHash))
        {
            string currentHash = _logicEditorVM.CurrentProgram.ComputeSemanticHash();
            if (string.Equals(currentHash, _logicEditorVM.LastDeployedProgramHash, StringComparison.Ordinal))
            {
                return;
            }
        }

        ResetDeployStateOnConfigChange();
    }

    private void OnLogicEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogicEditorViewModel.CompileState) or nameof(LogicEditorViewModel.CurrentProgram))
        {
            // Nếu chương trình logic thực thi có cùng mã băm với bản đã nạp gần nhất (ví dụ: chỉ thay đổi vị trí các khối X, Y),
            // không reset trạng thái nạp thành công!
            if (IsSuccess && _logicEditorVM?.CurrentProgram != null && !string.IsNullOrEmpty(_logicEditorVM.LastDeployedProgramHash))
            {
                string currentHash = _logicEditorVM.CurrentProgram.ComputeSemanticHash();
                if (string.Equals(currentHash, _logicEditorVM.LastDeployedProgramHash, StringComparison.Ordinal))
                {
                    UpdatePreflightSummary();
                    return;
                }
            }

            ResetDeployStateOnConfigChange();
        }
    }

    private void ResetDeployStateOnConfigChange()
    {
        ResetToReadyState();
    }

    public void UpdatePreflightSummary()
    {
        bool isVi = LocalizationService.Instance.IsVietnamese;
        bool isConnected = AppServices.Instance.SessionManager.HasActiveSession || AppServices.Instance.ModbusClient.IsConnected;
        IsDeviceConnected = isConnected;

        if (DeployState == DeployState.Failed)
        {
            StatusTitle = isVi ? "Nạp Thất Bại" : "Deploy Failed";
            StatusSubtitle = PreflightSummary;
            StatusMessage = PreflightSummary;
            StepperBadgeText = isVi ? "Thất bại" : "Failed";
            ActionButtonText = isVi ? "⚡ Thử Nạp Lại" : "⚡ Retry Deploy";
            ShowProgressBar = false;
            PrimaryActionCommand.NotifyCanExecuteChanged();
            return;
        }

        var meta = _getMetadata?.Invoke();
        int tab0ModbusCount = GetTab0ModbusChannelsCount(meta);

        if (_logicEditorVM != null && (_logicEditorVM.CompileState == CompileState.Invalid || _logicEditorVM.CompileState == CompileState.Stale))
        {
            HasValidRules = false;
            ManifestRuleCount = 0;
            PreflightSummary = _logicEditorVM.CompileState switch
            {
                CompileState.Stale => LocalizationService.Tr("DeployPreflightStale"),
                CompileState.Invalid => LocalizationService.Tr("DeployPreflightInvalid"),
                _ => LocalizationService.Tr("DeployPreflightNone")
            };
            StatusTitle = isVi ? "Chương Trình Logic Chưa Hợp Lệ" : "Invalid Logic Program";
            StatusSubtitle = PreflightSummary;
            StatusMessage = PreflightSummary;
            StepperBadgeText = isVi ? "Chưa thể nạp" : "Blocked";
            ActionButtonText = isVi ? "Mở Đồ Thị Logic Để Sửa" : "Open Logic Editor";
            ShowProgressBar = false;
            return;
        }

        int ruleCount = 0;
        string ruleDesc = "";
        int fbCount = _logicEditorVM?.CurrentProgram != null 
            ? (_logicEditorVM.CurrentProgram.FunctionBlockTimers.Count + _logicEditorVM.CurrentProgram.FunctionBlockCounters.Count) 
            : 0;
        int mqttCount = (_logicEditorVM?.CurrentProgram?.MqttTelemetryConfigs?.Count ?? 0)
            + (_logicEditorVM?.CurrentProgram?.MqttSubscriptionConfigs?.Count ?? 0);
        int modbusCount = (_logicEditorVM?.CurrentProgram?.ModbusMasterConfigs?.Count ?? 0)
            + (_logicEditorVM?.CurrentProgram?.ModbusMasterWriteConfigs?.Count ?? 0);
        int directWriteCount = _logicEditorVM?.CurrentProgram?.DirectWriteConfigs?.Count ?? 0;
        int totalCanvasItems = (_logicEditorVM?.CurrentProgram?.RuleCount ?? 0) + fbCount + mqttCount + modbusCount + directWriteCount;

        bool hasMqttBroker = meta?.MqttBrokerEnabled == true && !string.IsNullOrWhiteSpace(meta?.MqttBrokerHost);
        bool hasMqttTls = hasMqttBroker && meta?.MqttTlsEnabled == true;
        bool hasDtu = meta?.DtuConfig != null && meta.DtuConfig.Enabled;

        var configParts = new List<string>();
        if (hasMqttBroker)
        {
            configParts.Add(hasMqttTls ? "MQTT Broker (TLS)" : "MQTT Broker");
        }
        if (hasDtu)
        {
            configParts.Add("DTU Gateway");
        }
        if (tab0ModbusCount > 0)
        {
            configParts.Add(isVi ? $"{tab0ModbusCount} kênh Modbus Master" : $"{tab0ModbusCount} Modbus Master channels");
        }

        if (_ruleTableVM != null && _ruleTableVM.Rules.Count > 0)
        {
            ruleCount = _ruleTableVM.Rules.Count;
            int enabledCount = _ruleTableVM.Rules.Count(r => r.Enabled);
            var firstRule = _ruleTableVM.Rules.FirstOrDefault(r => r.Enabled) ?? _ruleTableVM.Rules.FirstOrDefault();
            if (firstRule != null)
            {
                ruleDesc = $"{firstRule.Id} ({firstRule.TriggerSummary} ➔ {firstRule.ActionSummary})";
            }
            HasValidRules = enabledCount > 0 || fbCount > 0 || mqttCount > 0 || modbusCount > 0 || directWriteCount > 0;
            ManifestRuleCount = enabledCount;
            ManifestRuleSummary = ruleDesc;
            string extraDesc = "";
            if (fbCount > 0 || mqttCount > 0 || modbusCount > 0 || directWriteCount > 0)
            {
                extraDesc = isVi ? $" (+ {fbCount} FBs, {mqttCount} MQTT, {modbusCount} Modbus, {directWriteCount} DirectOut)" : $" (+ {fbCount} FBs, {mqttCount} MQTT, {modbusCount} Modbus, {directWriteCount} DirectOut)";
            }
            string baseSummary = isVi
                ? $"Sẵn sàng nạp: {ruleCount} quy tắc trong Bảng Rule ({enabledCount} đang bật){extraDesc}"
                : $"Ready to deploy: {ruleCount} rules from Rule Table ({enabledCount} enabled){extraDesc}";

            PreflightSummary = isConnected
                ? baseSummary + "."
                : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
        }
        else if (_logicEditorVM?.CurrentProgram != null && totalCanvasItems > 0)
        {
            ruleCount = _logicEditorVM.CurrentProgram.RuleCount;
            HasValidRules = totalCanvasItems > 0;
            ManifestRuleCount = ruleCount;

            if ((fbCount > 0 || mqttCount > 0 || modbusCount > 0 || directWriteCount > 0) && ruleCount == 0)
            {
                ruleDesc = isVi
                    ? $"{_logicEditorVM.CurrentProgram.FunctionBlockTimers.Count} Timers, {_logicEditorVM.CurrentProgram.FunctionBlockCounters.Count} Counters, {mqttCount} MQTT, {modbusCount} Modbus (0 Rule slots)"
                    : $"{_logicEditorVM.CurrentProgram.FunctionBlockTimers.Count} Timers, {_logicEditorVM.CurrentProgram.FunctionBlockCounters.Count} Counters, {mqttCount} MQTT, {modbusCount} Modbus (0 Rule slots)";
                string baseSummary = isVi
                    ? $"Sẵn sàng nạp {fbCount} Function Blocks, {mqttCount} MQTT & {modbusCount} Modbus blocks xuống thiết bị."
                    : $"Ready to deploy {fbCount} Function Blocks, {mqttCount} MQTT & {modbusCount} Modbus blocks to device.";
                PreflightSummary = isConnected
                    ? baseSummary
                    : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
            }
            else
            {
                ruleDesc = isVi ? $"{ruleCount} quy tắc từ bản vẽ logic" : $"{ruleCount} rules from logic canvas";
                string baseSummary = string.Format(LocalizationService.Tr("DeployPreflightReady"), ruleCount);
                if (fbCount > 0 || mqttCount > 0 || modbusCount > 0)
                {
                    baseSummary += isVi ? $" (+ {fbCount} FBs, {mqttCount} MQTT, {modbusCount} Modbus)" : $" (+ {fbCount} FBs, {mqttCount} MQTT, {modbusCount} Modbus)";
                }
                PreflightSummary = isConnected
                    ? baseSummary
                    : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
            }
            ManifestRuleSummary = ruleDesc;
        }
        else if (configParts.Count > 0)
        {
            string configSummary = string.Join(" + ", configParts);
            HasValidRules = true;
            ManifestRuleCount = 0;
            ManifestRuleSummary = isVi ? $"0 Rule logic ({configSummary})" : $"0 Logic rules ({configSummary})";
            string baseSummary = string.Format(LocalizationService.Tr("DeployPreflightZeroRulesWithConfig"), configSummary);
            PreflightSummary = isConnected
                ? baseSummary
                : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
        }
        else
        {
            HasValidRules = true;
            ManifestRuleCount = 0;
            ManifestRuleSummary = isVi ? "0 Rule logic (Mặc định)" : "0 Logic rules (Default)";
            string baseSummary = LocalizationService.Tr("DeployPreflightCleanWipe");
            PreflightSummary = isConnected
                ? baseSummary
                : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
        }

        ManifestRuleCountText = isVi
            ? $"{ManifestRuleCount} quy tắc (Đang bật)"
            : $"{ManifestRuleCount} rule(s) (Enabled)";

        if (IsSuccess)
        {
            StatusTitle = isVi ? "Đã Nạp Thành Công Xuống Thiết Bị!" : "✓ Deployed Successfully to Device!";
            StatusSubtitle = isVi
                ? (LastDeployDurationMs > 0
                    ? $"Thiết bị đang chạy logic mới · Thời gian nạp: {LastDeployDurationMs}ms · Sẵn sàng nạp tiếp."
                    : "Thiết bị đang chạy cấu hình mới · Sẵn sàng nạp tiếp.")
                : (LastDeployDurationMs > 0
                    ? $"Device is running updated logic · Deployed in {LastDeployDurationMs}ms · Ready for next deploy."
                    : "Device is running updated logic safely · Ready for next deploy.");
            StepperBadgeText = isVi ? "Hoàn thành 100%" : "Completed 100%";
            ActionButtonText = isVi ? "⚡ Bắt Đầu Nạp Xuống Thiết Bị" : "⚡ Start Deploy to Device";
            ShowProgressBar = true;
        }
        else if (IsDeploying)
        {
            StatusTitle = isVi ? "Đang Nạp Xuống Thiết Bị..." : "Deploying to Device...";
            StatusSubtitle = isVi ? "Vui lòng không ngắt kết nối trong khi nạp." : "Please do not disconnect during deployment.";
            StepperBadgeText = isVi ? "Đang nạp..." : "Deploying...";
            ShowProgressBar = true;
        }
        else if (!isConnected)
        {
            StatusTitle = isVi ? "Chưa Kết Nối Thiết Bị" : "Device Disconnected";
            StatusSubtitle = isVi ? "Vui lòng kết nối cổng COM ở thanh công cụ phía trên trước khi nạp." : "Please connect COM port in the top bar before deploying.";
            StatusMessage = PreflightSummary;
            StepperBadgeText = isVi ? "Chưa thể nạp" : "Blocked";
            ActionButtonText = isVi ? "Chưa Kết Nối Thiết Bị" : "Device Disconnected";
            ShowProgressBar = false;
        }
        else if (!HasValidRules)
        {
            StatusTitle = isVi ? "Chưa Có Cấu Hình Hoặc Quy Tắc Để Nạp" : "No Configuration or Rules to Deploy";
            StatusSubtitle = isVi ? "Chưa có cấu hình kênh Modbus ở Tab Thiết Bị hoặc quy tắc logic nào." : "No Modbus channels or logic rules defined.";
            StatusMessage = PreflightSummary;
            StepperBadgeText = isVi ? "Chưa thể nạp" : "Blocked";
            ActionButtonText = isVi ? "Mở Cấu Hình Thiết Bị" : "Open Device Configuration";
            ShowProgressBar = false;
        }
        else
        {
            string targetTitle;
            if (ManifestRuleCount > 0)
            {
                targetTitle = isVi ? $"Sẵn Sàng Nạp {ManifestRuleCount} Quy Tắc Xuống Thiết Bị" : $"Ready to Deploy {ManifestRuleCount} Rules to Device";
            }
            else if (configParts.Count > 0)
            {
                targetTitle = isVi ? $"Sẵn Sàng Nạp Cấu Hình {string.Join(" & ", configParts)}" : $"Ready to Deploy {string.Join(" & ", configParts)} Config";
            }
            else
            {
                targetTitle = isVi ? "Sẵn Sàng Đặt Lại Thiết Bị (0 Rule Logic)" : "Ready to Reset Device (0 Logic Rules)";
            }
            StatusTitle = targetTitle;
            StatusSubtitle = string.IsNullOrEmpty(ruleDesc)
                ? (isVi ? "Nạp an toàn không ngắt quãng (Zero-Downtime Atomic Staging)" : "Zero-downtime atomic staging")
                : (isVi ? $"Quy tắc: {ruleDesc} · Nạp an toàn không ngắt quãng" : $"Rules: {ruleDesc} · Bumpless transfer");
            StatusMessage = PreflightSummary;
            StepperBadgeText = isVi ? "Sẵn sàng" : "Ready";
            ActionButtonText = isVi ? "⚡ Bắt Đầu Nạp Xuống Thiết Bị" : "⚡ Start Deploy to Device";
            ShowProgressBar = false;
        }
    }

    private DeployRulesUseCase GetDeployUseCase() =>
        AppServices.Instance.SessionManager.HasActiveSession
            ? AppServices.Instance.DeployUseCase
            : (_injectedDeployUseCase ?? AppServices.Instance.DeployUseCase);

    private void InitializeSteps()
    {
        Steps.Clear();
        Steps.Add(new() { Index = 1, Title = LocalizationService.Tr("DeployStep1Title"), Detail = LocalizationService.Tr("DeployStep1Detail"), Status = StepStatus.Pending });
        Steps.Add(new() { Index = 2, Title = LocalizationService.Tr("DeployStep2Title"), Detail = LocalizationService.Tr("DeployStep2Detail"), Status = StepStatus.Pending });
        Steps.Add(new() { Index = 3, Title = LocalizationService.Tr("DeployStep3Title"), Detail = LocalizationService.Tr("DeployStep3Detail"), Status = StepStatus.Pending });
        Steps.Add(new() { Index = 4, Title = LocalizationService.Tr("DeployStep4Title"), Detail = LocalizationService.Tr("DeployStep4Detail"), Status = StepStatus.Pending });
        Steps.Add(new() { Index = 5, Title = LocalizationService.Tr("DeployStep5Title"), Detail = LocalizationService.Tr("DeployStep5Detail"), Status = StepStatus.Pending });
    }

    private void AppendLog(string message) => ConsoleLog += $"[{DateTime.Now:HH:mm:ss}] {message}\n";

    [RelayCommand]
    public void PrimaryAction()
    {
        if (CanDeploy)
        {
            _ = StartDeployAsync();
        }
        else if (!HasValidRules)
        {
            NavigateToRules();
        }
        else if (_logicEditorVM != null && _logicEditorVM.CompileState != CompileState.Valid)
        {
            NavigateToLogicEditor();
        }
    }

    [RelayCommand]
    public void NavigateToRules() => _navigateTab?.Invoke(AppTabIndices.RuleTable);

    [RelayCommand]
    public void NavigateToLogicEditor() => _navigateTab?.Invoke(AppTabIndices.LogicGraph);

    [RelayCommand]
    public void NavigateToLiveWatch() => _navigateTab?.Invoke(AppTabIndices.LiveWatch);

    [RelayCommand]
    public void ClearLog() => ConsoleLog = LocalizationService.Instance.IsVietnamese ? "// Đã xóa nhật ký.\n" : "// Log cleared.\n";

    [RelayCommand]
    public void CopyLog()
    {
        try
        {
            if (!string.IsNullOrEmpty(ConsoleLog))
            {
                System.Windows.Clipboard.SetText(ConsoleLog);
            }
        }
        catch { }
    }

    /// <summary>
    /// Trích xuất các kênh Modbus Master cấu hình trực tiếp ở Tab Thiết Bị (Standalone),
    /// tự động đối soát và loại trừ các kênh/Tag đã được sở hữu bởi các khối đồ thị FBD trên Canvas.
    /// </summary>
    public (List<ModbusMasterReadConfigDto> reads, List<ModbusMasterWriteConfigDto> writes) ExtractTab0ModbusChannels(
        ProjectMetadata? meta,
        IReadOnlyList<ModbusMasterReadConfigDto>? canvasReads,
        IReadOnlyList<ModbusMasterWriteConfigDto>? canvasWrites)
    {
        var allTags = _logicEditorVM?.TagCatalog?.AllTags;
        var claimedReadTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (canvasReads != null)
        {
            foreach (var r in canvasReads)
            {
                foreach (var pin in r.Pins)
                {
                    if (pin.TargetTagIndex.HasValue && allTags != null)
                    {
                        var tag = allTags.FirstOrDefault(t => t.Index == pin.TargetTagIndex.Value);
                        if (tag != null && !string.IsNullOrWhiteSpace(tag.Name))
                        {
                            claimedReadTags.Add(tag.Name);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(pin.Label)) claimedReadTags.Add(pin.Label);
                }
            }
        }

        var claimedWriteTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (canvasWrites != null)
        {
            foreach (var w in canvasWrites)
            {
                // if direct write has pins or command tag
                if (w.CommandTagIndex.HasValue && allTags != null)
                {
                    var tag = allTags.FirstOrDefault(t => t.Index == w.CommandTagIndex.Value);
                    if (tag != null && !string.IsNullOrWhiteSpace(tag.Name)) claimedWriteTags.Add(tag.Name);
                }
                if (w.Pins != null && allTags != null)
                {
                    foreach (var pin in w.Pins)
                    {
                        if (pin.TargetTagIndex.HasValue)
                        {
                            var tag = allTags.FirstOrDefault(t => t.Index == pin.TargetTagIndex.Value);
                            if (tag != null && !string.IsNullOrWhiteSpace(tag.Name)) claimedWriteTags.Add(tag.Name);
                        }
                        if (!string.IsNullOrWhiteSpace(pin.Label)) claimedWriteTags.Add(pin.Label);
                    }
                }
            }
        }

        Func<string, ushort?> tagIndexResolver = (tagName) =>
        {
            if (string.IsNullOrWhiteSpace(tagName) || allTags == null) return null;
            var found = allTags.FirstOrDefault(t => string.Equals(t.Name, tagName, StringComparison.OrdinalIgnoreCase));
            return (found != null && found.Kind != SimplePLC.Domain.Enums.TagKind.None) ? (ushort)found.Index : null;
        };

        return SimplePLC.Studio.Services.Modbus.ModbusChannelSyncService.CoalesceStandalonePortChannels(
            meta?.ModbusPorts,
            claimedReadTags,
            claimedWriteTags,
            tagIndexResolver);
    }

    [RelayCommand(CanExecute = nameof(CanDeploy))]
    public async Task StartDeployAsync()
    {
        if (IsDeploying) return;

        // Strict Pre-flight Safety Gate: If logic editor is present and invalid/stale, abort
        if (_logicEditorVM != null && (_logicEditorVM.CompileState == CompileState.Invalid || _logicEditorVM.CompileState == CompileState.Stale))
        {
            DeployState = DeployState.Failed;
            UpdatePreflightSummary();
            StatusMessage = PreflightSummary;
            AppendLog(string.Format(LocalizationService.Tr("DeployLogStop"), PreflightSummary));
            return;
        }

        _autoResetCts?.Cancel();
        IsDeploying = true;
        IsSuccess = false;
        DeployState = DeployState.Preparing;
        ProgressPercent = 0;
        InitializeSteps();
        ConsoleLog = string.Empty;
        AppendLog(LocalizationService.Tr("DeployLogStart"));
        UpdatePreflightSummary();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var services = AppServices.Instance;
            var client = services.ModbusClient;

            // Check connection via SessionManager and ModbusClient
            if (!services.SessionManager.HasActiveSession && !client.IsConnected)
            {
                Steps[0].Status = StepStatus.Failed;
                DeployState = DeployState.Failed;
                PreflightSummary = LocalizationService.Tr("DeployLogNoDevice");
                StatusMessage = PreflightSummary;
                AppendLog(LocalizationService.Tr("DeployLogNoConn"));
                IsDeploying = false;
                UpdatePreflightSummary();
                return;
            }

            // Step 1: Pre-flight Domain Validation
            Steps[0].Status = StepStatus.InProgress;
            ProgressPercent = 15;
            AppendLog(LocalizationService.Tr("DeployLogPreflight"));
            if (EffectiveDelayMs > 0) await Task.Delay(EffectiveDelayMs).ConfigureAwait(true);

            var product = services.CurrentProduct;
            RuleTable ruleTable;
            IReadOnlyList<FbTimerRecordDto>? fbTimers = null;
            IReadOnlyList<FbCounterRecordDto>? fbCounters = null;
            IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs = null;
            IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs = null;
            IReadOnlyList<ModbusMasterReadConfigDto>? modbusMasterConfigs = null;

            // Luôn trích xuất Function Blocks (Timer, Counter), MQTT và Modbus Master blocks từ Logic Editor nếu có
            if (_logicEditorVM?.CurrentProgram != null)
            {
                fbTimers = _logicEditorVM.CurrentProgram.FunctionBlockTimers;
                fbCounters = _logicEditorVM.CurrentProgram.FunctionBlockCounters;
                mqttConfigs = _logicEditorVM.CurrentProgram.MqttTelemetryConfigs;
                mqttSubConfigs = _logicEditorVM.CurrentProgram.MqttSubscriptionConfigs;
                modbusMasterConfigs = _logicEditorVM.CurrentProgram.ModbusMasterConfigs;
            }

            // Trích xuất Rule Table: Ưu tiên bảng rule đã biên tập trong Rule Table, hoặc fallback về Logic Editor
            if (_ruleTableVM != null && _ruleTableVM.Rules.Count > 0)
            {
                ruleTable = _ruleTableVM.ToDomainRuleTable(product);
            }
            else if (_logicEditorVM?.CurrentProgram != null)
            {
                ruleTable = _logicEditorVM.CurrentProgram.ToRuleTable();
            }
            else
            {
                ruleTable = new RuleTable();
            }


            var meta = _getMetadata?.Invoke();
            var existingModbusWrites = _logicEditorVM?.CurrentProgram?.ModbusMasterWriteConfigs;
            var (tab0Reads, tab0Writes) = ExtractTab0ModbusChannels(meta, modbusMasterConfigs, existingModbusWrites);
            var mergedModbusReads = new List<ModbusMasterReadConfigDto>();
            if (modbusMasterConfigs != null) mergedModbusReads.AddRange(modbusMasterConfigs);
            mergedModbusReads.AddRange(tab0Reads);
            modbusMasterConfigs = mergedModbusReads;

            var mergedModbusWrites = new List<ModbusMasterWriteConfigDto>();
            if (existingModbusWrites != null) mergedModbusWrites.AddRange(existingModbusWrites);
            mergedModbusWrites.AddRange(tab0Writes);
            var modbusWriteConfigs = (IReadOnlyList<ModbusMasterWriteConfigDto>)mergedModbusWrites;

            int totalFbs = (fbTimers?.Count ?? 0) + (fbCounters?.Count ?? 0);
            int totalMqtt = (mqttConfigs?.Count ?? 0) + (_logicEditorVM?.CurrentProgram?.MqttSubscriptionConfigs?.Count ?? 0);
            int totalModbus = (modbusMasterConfigs?.Count ?? 0) + (modbusWriteConfigs?.Count ?? 0);
            int totalDirectWrite = _logicEditorVM?.CurrentProgram?.DirectWriteConfigs?.Count ?? 0;

            bool isDtuHardware = (product?.DeviceClass == (ushort)SimplePLC.Domain.Enums.SPLC_DeviceClass.DTU) ||
                                 (product?.SupportsHttpClient == true) || 
                                 (product?.SupportsTransparentStream == true);
            var fbdHttpNode = _logicEditorVM?.Nodes.OfType<HttpClientNodeViewModel>().FirstOrDefault();
            var fbdTcpNode = _logicEditorVM?.Nodes.OfType<TcpClientNodeViewModel>().FirstOrDefault();
            bool hasDtuCanvasNodes = fbdHttpNode != null || fbdTcpNode != null;
            bool isDtuEnabled = isDtuHardware && ((meta?.DtuConfig != null && meta.DtuConfig.Enabled) || hasDtuCanvasNodes);
            bool hasModbusPortConfig = meta?.ModbusPorts != null && meta.ModbusPorts.Any(p => p.Enabled);

            if (ruleTable.Count == 0)
            {
                AppendLog(LocalizationService.Instance.IsVietnamese
                    ? "[NẠP CẤU HÌNH] Dự án chứa 0 quy tắc logic. MCU sẽ được làm sạch bộ nhớ rule và áp dụng toàn bộ cấu hình thiết bị."
                    : "[CONFIG DEPLOY] Project contains 0 logic rules. MCU rule memory will be cleared and device configuration applied.");
            }

            var validationResult = RuleTableValidator.Validate(ruleTable);
            if (!validationResult.IsValid)
            {
                Steps[0].Status = StepStatus.Failed;
                DeployState = DeployState.Failed;
                var errStr = string.Join("; ", validationResult.Errors.Select(e => e.Message));
                PreflightSummary = string.Format(LocalizationService.Tr("DeployLogValidErr"), errStr);
                StatusMessage = PreflightSummary;
                AppendLog(string.Format(LocalizationService.Tr("DeployLogPreflightFail"), errStr));
                IsDeploying = false;
                UpdatePreflightSummary();
                return;
            }

            // Kiểm tra an toàn độc quyền ghi Tag (Exclusive Ingestion Ownership) trước khi tiến hành nạp
            var (isOwnershipValid, ownershipError) = ValidateIngestionOwnership(modbusMasterConfigs, mqttSubConfigs);
            if (!isOwnershipValid)
            {
                Steps[0].Status = StepStatus.Failed;
                DeployState = DeployState.Failed;
                PreflightSummary = ownershipError ?? LocalizationService.Tr("DeployOwnershipFallbackSummary");
                StatusMessage = PreflightSummary;
                AppendLog($"[SAFETY ABORT] {PreflightSummary}");
                IsDeploying = false;
                UpdatePreflightSummary();
                return;
            }

            Steps[0].Status = StepStatus.Success;
            ProgressPercent = 30;
            AppendLog(string.Format(LocalizationService.Tr("DeployLogPreflightOk"), ruleTable.Count));

            if (totalFbs > 0 || totalMqtt > 0 || totalModbus > 0 || totalDirectWrite > 0)
            {
                bool isVi = LocalizationService.Instance.IsVietnamese;
                AppendLog(isVi
                    ? $"[Cấu hình V2] Phát hiện {fbTimers?.Count ?? 0} Timers, {fbCounters?.Count ?? 0} Counters, {totalMqtt} MQTT blocks, {totalModbus} Modbus blocks, {totalDirectWrite} PULSE_OUT blocks."
                    : $"[V2 Config] Detected {fbTimers?.Count ?? 0} Timers, {fbCounters?.Count ?? 0} Counters, {totalMqtt} MQTT blocks, {totalModbus} Modbus blocks, {totalDirectWrite} PULSE_OUT blocks.");

                int subCount = mqttSubConfigs?.Count ?? 0;
                if (subCount > 0)
                {
                    if (product != null && product.SupportsMqttSubscribe)
                    {
                        AppendLog(isVi
                            ? $"[MQTT SUB] Phát hiện {subCount} khối Subscribe -> Nạp cấu hình Inbound xuống phân vùng 0x0D80 của MCU."
                            : $"[MQTT SUB] Detected {subCount} Subscribe block(s) -> Deploying inbound config to MCU partition 0x0D80.");
                    }
                    else
                    {
                        AppendLog(isVi
                            ? $"[MQTT SUB] {subCount} khối Subscribe đang vận hành ở chế độ Host Simulation & Sandbox (MCU chưa kích hoạt cờ 0x0020)."
                            : $"[MQTT SUB] {subCount} Subscribe block(s) running in Host Simulation & Sandbox mode (MCU flag 0x0020 inactive).");
                    }
                }

                int mbWriteCount = _logicEditorVM?.CurrentProgram?.ModbusMasterWriteConfigs?.Count ?? 0;
                if (mbWriteCount > 0)
                {
                    AppendLog(isVi
                        ? $"[MB WRITE] Phát hiện {mbWriteCount} khối Modbus Write -> Nạp cấu hình xuống phân vùng 0x1100 của MCU."
                        : $"[MB WRITE] Detected {mbWriteCount} Modbus Write block(s) -> Deploying config to MCU partition 0x1100.");
                }

                int dwCount = _logicEditorVM?.CurrentProgram?.DirectWriteConfigs?.Count ?? 0;
                if (dwCount > 0)
                {
                    AppendLog(isVi
                        ? $"[PULSE_OUT] Phát hiện {dwCount} khối phát xung / chấp hành -> Nạp cấu hình xuống phân vùng 0x1200 của MCU."
                        : $"[PULSE_OUT] Detected {dwCount} Pulse Output block(s) -> Deploying config to MCU partition 0x1200.");
                }

                if (product != null && !product.SupportsMqttPublish && (mqttConfigs?.Count > 0 || meta?.MqttBrokerEnabled == true))
                {
                    AppendLog(isVi
                        ? $"[Cảnh báo Phần cứng] Thiết bị '{product.ProductName}' không hỗ trợ MQTT Telemetry (Thiếu cờ SPLC_CAP_MQTT_PUB tại 0x0A08). Bỏ qua nạp cấu hình MQTT."
                        : $"[Hardware Warning] Device '{product.ProductName}' does not support MQTT Telemetry (Missing SPLC_CAP_MQTT_PUB at 0x0A08). Skipping MQTT configuration.");
                }
            }
            if (EffectiveDelayMs > 0) await Task.Delay(EffectiveDelayMs).ConfigureAwait(true);

            // Build MQTT Broker config if available
            MqttBrokerConfigDto? brokerConfig = null;
            if (meta != null && (totalMqtt > 0 || meta.MqttBrokerEnabled))
            {
                brokerConfig = new MqttBrokerConfigDto
                {
                    Enabled = meta.MqttBrokerEnabled,
                    Host = meta.MqttBrokerHost?.Trim() ?? string.Empty,
                    Port = (ushort)(meta.MqttBrokerPort > 0 ? meta.MqttBrokerPort : 1883),
                    UseTls = meta.MqttTlsEnabled,
                    UseMtls = meta.MqttTlsEnabled && meta.MqttTlsMtlsEnabled,
                    IgnoreHostname = meta.MqttTlsIgnoreHostname,
                    AllowSelfSigned = meta.MqttTlsAllowSelfSigned,
                    CleanSession = meta.MqttCleanSession,
                    KeepAliveSec = meta.MqttKeepAliveSec > 0 ? meta.MqttKeepAliveSec : (ushort)60,
                    ClientId = meta.MqttClientId ?? string.Empty,
                    Username = meta.MqttUsername ?? string.Empty,
                    Password = meta.MqttPassword ?? string.Empty
                };

                bool isVi = LocalizationService.Instance.IsVietnamese;
                AppendLog(isVi
                    ? $"[Cấu hình Broker] {brokerConfig.Host}:{brokerConfig.Port} (TLS: {brokerConfig.UseTls}, Kích hoạt: {brokerConfig.Enabled})"
                    : $"[Broker Config] {brokerConfig.Host}:{brokerConfig.Port} (TLS: {brokerConfig.UseTls}, Enabled: {brokerConfig.Enabled})");
            }

            // Steps 2-4: Staging and Commit
            Steps[1].Status = StepStatus.InProgress;
            ProgressPercent = 45;
            AppendLog(LocalizationService.Tr("DeployLogStaging"));

            // Build Modbus Ports config if available (SPLC-SPEC-MBM-003)
            IReadOnlyList<ModbusPortConfigDto>? modbusPortConfigs = null;
            if (meta?.ModbusPorts != null && meta.ModbusPorts.Count > 0)
            {
                modbusPortConfigs = meta.ModbusPorts.Select(p => new ModbusPortConfigDto
                {
                    PortIndex = p.PortIndex,
                    PortName = p.PortName,
                    Enabled = p.Enabled,
                    Role = Enum.TryParse<ModbusPortRole>(p.Role, true, out var role) ? role : ModbusPortRole.Master,
                    SlaveId = p.SlaveId > 0 ? p.SlaveId : (byte)1,
                    BaudRate = p.BaudRate,
                    DataBits = p.DataBits,
                    Parity = Enum.TryParse<ModbusParity>(p.Parity, true, out var par) ? par : ModbusParity.None,
                    StopBits = p.StopBits,
                    ResponseTimeoutMs = p.ResponseTimeoutMs,
                    InterFrameDelayMs = p.InterFrameDelayMs,
                    RetryCount = p.RetryCount
                }).ToList();
            }

            // Modbus Slave hoạt động theo bản đồ cố định tiêu chuẩn (không cần truyền bảng map 0x1500)
            IReadOnlyList<ModbusSlaveMapEntryDto>? modbusSlaveMapConfigs = null;

            if (modbusPortConfigs != null && modbusPortConfigs.Any(p => p.Role == ModbusPortRole.Slave))
            {
                var slavePorts = modbusPortConfigs.Where(p => p.Role == ModbusPortRole.Slave).ToList();
                AppendLog(LocalizationService.Instance.IsVietnamese
                    ? $"[Modbus Slave] Phát hiện {slavePorts.Count} cổng Slave (Hoạt động theo bản đồ địa chỉ cố định chuẩn thiết bị)."
                    : $"[Modbus Slave] Detected {slavePorts.Count} Slave port(s) (Operating in factory fixed memory addressing mode).");
            }

            var directWriteConfigs = _logicEditorVM?.CurrentProgram?.DirectWriteConfigs;
            // Dựng bảng kiểu dữ liệu Tag Registry (0x0080..0x00BF, 128 tags x 1 byte: 1=BOOL, 2=INT32, 3=REAL)
            byte[] tagDataTypes = new byte[SimplePLC.Protocol.Constants.ModbusRegisterMap.MaxRuntimeTags];
            var allTags = _logicEditorVM?.TagCatalog?.AllTags;
            if (allTags != null)
            {
                foreach (var tag in allTags)
                {
                    if (tag.Index < tagDataTypes.Length && tag.Kind != SimplePLC.Domain.Enums.TagKind.None)
                    {
                        byte typeCode = 2; // Default INT32 (DINT)
                        if (tag.Kind is SimplePLC.Domain.Enums.TagKind.DiscreteInput or SimplePLC.Domain.Enums.TagKind.DiscreteOutput or SimplePLC.Domain.Enums.TagKind.VirtualFlag)
                        {
                            typeCode = 1; // BOOL
                        }
                        else if (tag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32 || tag.Kind == SimplePLC.Domain.Enums.TagKind.AnalogInput)
                        {
                            typeCode = 3; // REAL (Float32)
                        }
                        tagDataTypes[tag.Index] = typeCode;
                    }
                }
            }

            // Đồng bộ kiểu REAL cho bất kỳ Tag nào được liên kết từ chân kiểu Real của MB_READ
            if (_logicEditorVM?.Nodes != null)
            {
                foreach (var mbReadNode in _logicEditorVM.Nodes.OfType<ModbusReadNodeViewModel>())
                {
                    foreach (var pin in mbReadNode.Pins)
                    {
                        if (pin.DataType == SimplePLC.Protocol.Enums.ModbusDataType.Real && pin.TargetTag != null && pin.TargetTag.Index < tagDataTypes.Length)
                        {
                            tagDataTypes[pin.TargetTag.Index] = 3; // SPLC_TAG_TYPE_REAL
                            pin.TargetTag.DataType = SimplePLC.Domain.Enums.TagDataType.Float32;
                        }
                    }
                }

                // Đồng bộ kiểu REAL cho bất kỳ Tag nào được liên kết từ chân kiểu Float của MQTT_SUB
                foreach (var mqttSubNode in _logicEditorVM.Nodes.OfType<MqttSubscribeNodeViewModel>())
                {
                    foreach (var pin in mqttSubNode.Pins)
                    {
                        if (pin.DataType == SimplePLC.Domain.Enums.MqttDataType.Float && pin.TargetTag != null && pin.TargetTag.Index < tagDataTypes.Length)
                        {
                            tagDataTypes[pin.TargetTag.Index] = 3; // SPLC_TAG_TYPE_REAL
                            pin.TargetTag.DataType = SimplePLC.Domain.Enums.TagDataType.Float32;
                        }
                    }
                }
            }

            DtuConfigDto? dtuConfigDto = null;
            if (isDtuHardware && meta?.DtuConfig != null && meta.DtuConfig.Enabled)
            {
                var dtuModel = meta.DtuConfig;
                byte portIdx = dtuModel.PortIndex;
                string sMode = dtuModel.SerialMode;
                string uMode = dtuModel.UplinkMode;

                dtuConfigDto = new DtuConfigDto
                {
                    Enabled = true,
                    PortIndex = portIdx,
                    SerialMode = sMode switch
                    {
                        "ModbusMaster" => DtuSerialMode.ModbusMaster,
                        "ModbusSlave" => DtuSerialMode.ModbusSlave,
                        _ => DtuSerialMode.Transference
                    },
                    UplinkMode = uMode switch
                    {
                        "Mqtt" => DtuUplinkMode.Mqtt,
                        "Http" => DtuUplinkMode.Http,
                        "TcpClient" => DtuUplinkMode.TcpClient,
                        "TcpServer" => DtuUplinkMode.TcpServer,
                        _ => DtuUplinkMode.None
                    },
                    FrameTimeoutDelayMs = dtuModel.FrameTimeoutDelayMs,
                    MaxPacketSize = dtuModel.MaxPacketSize,
                    PayloadMode = dtuModel.PayloadMode switch
                    {
                        "Custom" => DtuPayloadMode.Custom,
                        "Raw" => DtuPayloadMode.Raw,
                        _ => DtuPayloadMode.Standard
                    },
                    PayloadFlags = (byte)(
                        (dtuModel.IncludeDeviceId ? 1 : 0) |
                        (dtuModel.IncludeTimestamp ? 2 : 0) |
                        (dtuModel.IncludePort ? 4 : 0) |
                        (dtuModel.IncludeSequence ? 8 : 0) |
                        (dtuModel.IncludeHexStream ? 16 : 0) |
                        (dtuModel.IncludePayloadLength ? 32 : 0)
                    ),
                    HttpConfig = new DtuHttpConfigDto
                    {
                        Enabled = uMode == "Http" || dtuModel.HttpSettings.Enabled,
                        Method = dtuModel.HttpSettings.Method == "PUT" ? DtuHttpMethod.Put : DtuHttpMethod.Post,
                        Url = dtuModel.HttpSettings.Url ?? string.Empty,
                        CustomHeaders = dtuModel.HttpSettings.Headers ?? string.Empty,
                        TimeoutSec = dtuModel.HttpSettings.TimeoutSec,
                        PeriodicIntervalSec = dtuModel.HttpSettings.PeriodicIntervalSec,
                        TriggerMode = dtuModel.HttpSettings.TriggerMode == "Periodic" ? DtuHttpTriggerMode.Periodic : DtuHttpTriggerMode.OnDataReceived
                    },
                    TcpConfig = new DtuTcpConfigDto
                    {
                        Enabled = uMode is "TcpClient" or "TcpServer" || dtuModel.TcpSettings.Enabled,
                        SocketMode = dtuModel.TcpSettings.SocketMode == "Server" ? DtuTcpSocketMode.Server : DtuTcpSocketMode.Client,
                        RemoteHost = dtuModel.TcpSettings.RemoteHost ?? string.Empty,
                        RemotePort = dtuModel.TcpSettings.RemotePort,
                        LocalPort = dtuModel.TcpSettings.LocalPort,
                        ReconnectDelaySec = dtuModel.TcpSettings.ReconnectDelaySec,
                        HeartbeatIntervalSec = dtuModel.TcpSettings.HeartbeatIntervalSec,
                        HeartbeatPayload = dtuModel.TcpSettings.HeartbeatPayload ?? string.Empty,
                        RegistrationType = dtuModel.TcpSettings.RegistrationType switch
                        {
                            "OnConnect" => DtuTcpRegistrationType.OnConnect,
                            "PrependToEveryPacket" => DtuTcpRegistrationType.PrependToEveryPacket,
                            _ => DtuTcpRegistrationType.None
                        },
                        RegistrationPayload = dtuModel.TcpSettings.RegistrationPayload ?? string.Empty
                    }
                };

                bool isVi = LocalizationService.Instance.IsVietnamese;
                AppendLog(isVi
                    ? $"[DTU Gateway] Phát hiện cấu hình DTU (Serial: {dtuConfigDto.SerialMode}, Uplink: {dtuConfigDto.UplinkMode}, Cổng: {dtuConfigDto.PortIndex + 1}) -> Nạp xuống phân vùng 0x1600..0x16BF của MCU."
                    : $"[DTU Gateway] Detected DTU configuration (Serial: {dtuConfigDto.SerialMode}, Uplink: {dtuConfigDto.UplinkMode}, Port: {dtuConfigDto.PortIndex + 1}) -> Deploying to MCU partition 0x1600..0x16BF.");
            }

            // Synthesis from FBD Canvas (HttpClientNodeViewModel & TcpClientNodeViewModel)
            if (isDtuHardware && (fbdHttpNode != null || fbdTcpNode != null))
            {
                dtuConfigDto ??= new DtuConfigDto
                {
                    Enabled = true,
                    PortIndex = 0,
                    SerialMode = DtuSerialMode.Transference
                };

                if (fbdHttpNode != null)
                {
                    dtuConfigDto.Enabled = true;
                    dtuConfigDto.UplinkMode = DtuUplinkMode.Http;
                    dtuConfigDto.PayloadMode = fbdHttpNode.IsRawMode ? DtuPayloadMode.Raw : DtuPayloadMode.Custom;
                    dtuConfigDto.HttpConfig = new DtuHttpConfigDto
                    {
                        Enabled = true,
                        Method = string.Equals(fbdHttpNode.Method, "PUT", StringComparison.OrdinalIgnoreCase) ? DtuHttpMethod.Put : DtuHttpMethod.Post,
                        Url = fbdHttpNode.Url ?? string.Empty,
                        CustomHeaders = fbdHttpNode.CustomHeaders ?? string.Empty,
                        TimeoutSec = fbdHttpNode.TimeoutSec,
                        PeriodicIntervalSec = fbdHttpNode.PeriodicIntervalSec,
                        TriggerMode = fbdHttpNode.IsPeriodicMode ? DtuHttpTriggerMode.Periodic : DtuHttpTriggerMode.OnDataReceived
                    };
                }

                if (fbdTcpNode != null)
                {
                    dtuConfigDto.Enabled = true;
                    if (fbdHttpNode == null)
                    {
                        dtuConfigDto.UplinkMode = DtuUplinkMode.TcpClient;
                        dtuConfigDto.PayloadMode = fbdTcpNode.IsRawMode ? DtuPayloadMode.Raw : DtuPayloadMode.Custom;
                    }
                    dtuConfigDto.TcpConfig = new DtuTcpConfigDto
                    {
                        Enabled = true,
                        SocketMode = DtuTcpSocketMode.Client,
                        RemoteHost = fbdTcpNode.RemoteHost ?? string.Empty,
                        RemotePort = fbdTcpNode.RemotePort,
                        ReconnectDelaySec = fbdTcpNode.ReconnectDelaySec,
                        HeartbeatIntervalSec = fbdTcpNode.HeartbeatIntervalSec,
                        HeartbeatPayload = fbdTcpNode.HeartbeatPayload ?? string.Empty,
                        RegistrationType = fbdTcpNode.RegistrationType switch
                        {
                            "OnConnect" => DtuTcpRegistrationType.OnConnect,
                            "PrependToEveryPacket" => DtuTcpRegistrationType.PrependToEveryPacket,
                            _ => DtuTcpRegistrationType.None
                        },
                        RegistrationPayload = fbdTcpNode.RegistrationPayload ?? string.Empty
                    };
                }

                bool isVi = LocalizationService.Instance.IsVietnamese;
                AppendLog(isVi
                    ? $"[DTU Gateway FBD] Tự động tổng hợp cấu hình từ khối Canvas ({fbdHttpNode?.Title ?? ""} {fbdTcpNode?.Title ?? ""}) -> Nạp xuống MCU."
                    : $"[DTU Gateway FBD] Synthesized configuration from Canvas blocks ({fbdHttpNode?.Title ?? ""} {fbdTcpNode?.Title ?? ""}) -> Deploying to MCU.");
            }

            // MQTT TLS / mTLS Certificates (SPLC-SPEC-MQTT-003)
            MqttTlsCertificatesDto? tlsCertificates = null;
            if (meta?.MqttTlsEnabled == true)
            {
                tlsCertificates = new MqttTlsCertificatesDto();
                if (!string.IsNullOrWhiteSpace(meta.MqttTlsCaCertPem))
                {
                    tlsCertificates.RootCaBytes = System.Text.Encoding.UTF8.GetBytes(meta.MqttTlsCaCertPem);
                }
                else if (!string.IsNullOrWhiteSpace(meta.MqttTlsCaCertPath) && System.IO.File.Exists(meta.MqttTlsCaCertPath))
                {
                    try
                    {
                        tlsCertificates.RootCaBytes = System.IO.File.ReadAllBytes(meta.MqttTlsCaCertPath);
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"[TLS WARNING] Error reading Root CA: {ex.Message}");
                    }
                }

                if (meta.MqttTlsMtlsEnabled)
                {
                    if (!string.IsNullOrWhiteSpace(meta.MqttTlsClientCertPem))
                    {
                        tlsCertificates.ClientCertBytes = System.Text.Encoding.UTF8.GetBytes(meta.MqttTlsClientCertPem);
                    }
                    else if (!string.IsNullOrWhiteSpace(meta.MqttTlsClientCertPath) && System.IO.File.Exists(meta.MqttTlsClientCertPath))
                    {
                        try
                        {
                            tlsCertificates.ClientCertBytes = System.IO.File.ReadAllBytes(meta.MqttTlsClientCertPath);
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"[TLS WARNING] Error reading Client Cert: {ex.Message}");
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(meta.MqttTlsClientKeyPem))
                    {
                        tlsCertificates.ClientKeyBytes = System.Text.Encoding.UTF8.GetBytes(meta.MqttTlsClientKeyPem);
                    }
                    else if (!string.IsNullOrWhiteSpace(meta.MqttTlsClientKeyPath) && System.IO.File.Exists(meta.MqttTlsClientKeyPath))
                    {
                        try
                        {
                            tlsCertificates.ClientKeyBytes = System.IO.File.ReadAllBytes(meta.MqttTlsClientKeyPath);
                        }
                        catch (Exception ex)
                        {
                            AppendLog($"[TLS WARNING] Error reading Client Key: {ex.Message}");
                        }
                    }
                }
            }

                        MqttWillConfigDto? willConfigDto = null;
            if (meta != null)
            {
                string effectiveClientId = string.IsNullOrWhiteSpace(meta.MqttClientId) ? "SynaptiX_PLC" : meta.MqttClientId.Trim();
                string rawTopic = meta.MqttWillTopic ?? string.Empty;
                string resolvedTopic = rawTopic.Replace("{device}", effectiveClientId, StringComparison.OrdinalIgnoreCase).Trim();
                string rawPayload = string.IsNullOrWhiteSpace(meta.MqttWillPayload) ? "offline" : meta.MqttWillPayload;
                string resolvedPayload = rawPayload.Replace("{device}", effectiveClientId, StringComparison.OrdinalIgnoreCase);

                resolvedPayload = resolvedPayload.Replace("\"", string.Empty).Replace("\\", string.Empty);
                if (resolvedPayload.Length > MqttWillConfigCodec.MaxPayloadChars)
                {
                    resolvedPayload = resolvedPayload.Substring(0, MqttWillConfigCodec.MaxPayloadChars);
                }

                willConfigDto = new MqttWillConfigDto
                {
                    Enabled = meta.MqttWillEnabled,
                    Topic = resolvedTopic,
                    Payload = resolvedPayload,
                    Qos = meta.MqttWillQos,
                    Retain = meta.MqttWillRetain
                };
            }

            var deployUseCase = GetDeployUseCase();
            var deployResult = await deployUseCase.ExecuteAsync(
                ruleTable,
                fbTimers,
                fbCounters,
                mqttConfigs,
                brokerConfig,
                mqttSubConfigs,
                modbusMasterConfigs,
                modbusWriteConfigs,
                directWriteConfigs,
                modbusPortConfigs,
                modbusSlaveMapConfigs,
                tagDataTypes,
                dtuConfig: dtuConfigDto,
                mqttWillConfig: willConfigDto,
                tlsCertificates: tlsCertificates,
                slaveId: 1,
                ct: default,
                progressLogger: AppendLog).ConfigureAwait(true);

            if (deployResult.IsSuccess)
            {
                Steps[1].Status = StepStatus.Success;
                AppendLog(LocalizationService.Tr("DeployLogStagingOk"));
                ProgressPercent = 60;
                if (EffectiveDelayMs > 0) await Task.Delay(EffectiveDelayMs).ConfigureAwait(true);

                // Step 3: Xác minh CRC
                Steps[2].Status = StepStatus.InProgress;
                ProgressPercent = 75;
                if (EffectiveDelayMs > 0) await Task.Delay(EffectiveDelayMs).ConfigureAwait(true);
                Steps[2].Status = StepStatus.Success;
                AppendLog(LocalizationService.Tr("DeployLogCrcOk"));

                // Step 4: Atomic Commit
                Steps[3].Status = StepStatus.InProgress;
                ProgressPercent = 88;
                if (EffectiveDelayMs > 0) await Task.Delay(EffectiveDelayMs).ConfigureAwait(true);
                Steps[3].Status = StepStatus.Success;
                AppendLog(string.Format(LocalizationService.Tr("DeployLogCommitOk"), deployResult.ActiveVersion));

                // Step 4.1: Deploy Wi-Fi Configuration to MCU Partition 0x1300 if enabled
                if (meta != null && meta.WifiEnabled && !string.IsNullOrWhiteSpace(meta.WifiSsid))
                {
                    bool isVi = LocalizationService.Instance.IsVietnamese;
                    try
                    {
                        var wifiConfig = new WifiConfigDto
                        {
                            Enabled = meta.WifiEnabled,
                            AutoReconnect = meta.WifiAutoReconnect,
                            Ssid = meta.WifiSsid.Trim(),
                            Password = meta.WifiPassword ?? string.Empty,
                            IsDhcp = meta.WifiDhcpEnabled,
                            StaticIp = string.IsNullOrWhiteSpace(meta.WifiStaticIp) ? "192.168.1.50" : meta.WifiStaticIp.Trim(),
                            StaticSubnet = string.IsNullOrWhiteSpace(meta.WifiSubnetMask) ? "255.255.255.0" : meta.WifiSubnetMask.Trim(),
                            StaticGateway = string.IsNullOrWhiteSpace(meta.WifiGateway) ? "192.168.1.1" : meta.WifiGateway.Trim(),
                            StaticDns = string.IsNullOrWhiteSpace(meta.WifiDns) ? "8.8.8.8" : meta.WifiDns.Trim()
                        };

                        var wifiClient = AppServices.Instance.WifiClient;
                        if (wifiClient != null)
                        {
                            await wifiClient.WriteWifiConfigAsync(1, wifiConfig).ConfigureAwait(true);
                            AppendLog(isVi
                                ? $"[Wi-Fi] Đã nạp cấu hình mạng không dây (SSID: {meta.WifiSsid}) xuống phân vùng 0x1300."
                                : $"[Wi-Fi] Deployed wireless network configuration (SSID: {meta.WifiSsid}) to partition 0x1300.");
                        }
                    }
                    catch (Exception wifiEx)
                    {
                        AppendLog($"[Wi-Fi WARNING] Không thể nạp cấu hình Wi-Fi: {wifiEx.Message}");
                    }
                }

                // Step 5: Version Handshake
                Steps[4].Status = StepStatus.InProgress;
                ProgressPercent = 96;
                if (EffectiveDelayMs > 0) await Task.Delay(EffectiveDelayMs).ConfigureAwait(true);
                Steps[4].Status = StepStatus.Success;
                ProgressPercent = 100;

                sw.Stop();
                LastDeployDurationMs = sw.ElapsedMilliseconds;
                IsSuccess = true;
                DeployState = DeployState.Success;
                string? deployedHash = _logicEditorVM?.CurrentProgram?.ComputeSemanticHash();
                _logicEditorVM?.MarkGraphDeployed(deployedHash);
                if (deployResult.DeployedRuleCount == 0 && totalModbus > 0)
                {
                    StatusMessage = LocalizationService.Instance.IsVietnamese
                        ? $"Nạp thành công {totalModbus} kênh RS-485 Modbus Master! (Phiên bản {deployResult.ActiveVersion})"
                        : $"Deployed {totalModbus} RS-485 Modbus Master channel(s) successfully! (Version {deployResult.ActiveVersion})";
                }
                else
                {
                    StatusMessage = string.Format(LocalizationService.Tr("DeployLogSuccess"), deployResult.DeployedRuleCount, deployResult.ActiveVersion);
                }
                string completionSummary = LocalizationService.Instance.IsVietnamese
                    ? $"[HOÀN TẤT] Nạp thành công trong {LastDeployDurationMs}ms · Phiên bản: {deployResult.ActiveVersion} · 100% hợp lệ."
                    : $"[SUCCESS] Deployed in {LastDeployDurationMs}ms · Version: {deployResult.ActiveVersion} · 100% valid.";
                AppendLog(completionSummary);

                if (EffectiveResetDelayMs > 0)
                {
                    ScheduleAutoResetSuccess(EffectiveResetDelayMs);
                }
            }
            else
            {
                Steps[1].Status = StepStatus.Failed;
                Steps[2].Status = StepStatus.Failed;
                DeployState = DeployState.Failed;
                StatusMessage = string.Format(LocalizationService.Tr("DeployLogFailStatus"), deployResult.ErrorCode, deployResult.ErrorMessage);
                AppendLog(string.Format(LocalizationService.Tr("DeployLogFail"), deployResult.ErrorCode, deployResult.ErrorMessage));
            }
        }
        catch (Exception ex)
        {
            DeployState = DeployState.Failed;
            StatusMessage = string.Format(LocalizationService.Tr("DeployExceptionStatus"), ex.Message);
            AppendLog(string.Format(LocalizationService.Tr("DeployLogException"), ex.Message));
        }
        finally
        {
            IsDeploying = false;
            UpdatePreflightSummary();
            OnPropertyChanged(nameof(CanDeploy));
            StartDeployCommand.NotifyCanExecuteChanged();
            PrimaryActionCommand.NotifyCanExecuteChanged();
        }
    }
}
