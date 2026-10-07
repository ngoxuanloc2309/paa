using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using SimplePLC.Application.Models;
using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Models;
using SimplePLC.Domain.Validation;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public enum StepStatus { Pending, InProgress, Success, Failed }

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

    public ObservableCollection<DeployStepItem> Steps { get; } = new();

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
    [ObservableProperty] private string _actionButtonText = "Bắt Đầu Nạp Xuống PLC";
    [ObservableProperty] private string _consoleLog = "--- STAGED MODBUS DEPLOYMENT (R10) ---\n";
    [ObservableProperty] private string _deviceInfoBadge = "SimplePLC · USB CDC · Chưa kết nối";
    [ObservableProperty] private string _manifestRuleSummary = "";
    [ObservableProperty] private int _manifestRuleCount;
    [ObservableProperty] private string _manifestRuleCountText = "";
    [ObservableProperty] private long _lastDeployDurationMs;
    [ObservableProperty] private bool _hasValidRules;
    [ObservableProperty] private bool _isDeviceConnected;

    public bool CanDeploy => !IsDeploying
        && (
            // Kiểm tra kết nối nếu đang trong môi trường ứng dụng thực tế
            !AppServices.Instance.SessionManager.HasActiveSession && !AppServices.Instance.ModbusClient.IsConnected
                ? false 
                : true
        )
        && (_logicEditorVM == null || _logicEditorVM.CompileState == CompileState.Valid)
        && (
            (_ruleTableVM != null && _ruleTableVM.Rules.Count > 0) ||
            (_logicEditorVM?.CurrentProgram != null)
        );

    public DeployViewModel(
        DeployRulesUseCase? deployUseCase = null, 
        RuleTableViewModel? ruleTableVM = null,
        LogicEditorViewModel? logicEditorVM = null,
        Action<int>? navigateTab = null)
    {
        _injectedDeployUseCase = deployUseCase;
        _ruleTableVM = ruleTableVM;
        _logicEditorVM = logicEditorVM;
        _navigateTab = navigateTab;

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
            DeviceInfoBadge = $"SimplePLC · USB CDC ({port}) · {(isVi ? "Trực tuyến" : "Online")}";
        }
        else
        {
            DeviceInfoBadge = isVi
                ? "SimplePLC · USB CDC · Chưa kết nối"
                : "SimplePLC · USB CDC · Disconnected";
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
        ResetDeployStateOnConfigChange();
    }

    private void OnLogicEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogicEditorViewModel.CompileState) or nameof(LogicEditorViewModel.CurrentProgram))
        {
            ResetDeployStateOnConfigChange();
        }
    }

    private void ResetDeployStateOnConfigChange()
    {
        if (IsSuccess)
        {
            IsSuccess = false;
            DeployState = DeployState.Idle;
            InitializeSteps();
        }
        UpdatePreflightSummary();
        OnPropertyChanged(nameof(CanDeploy));
        StartDeployCommand.NotifyCanExecuteChanged();
        PrimaryActionCommand.NotifyCanExecuteChanged();
    }

    public void UpdatePreflightSummary()
    {
        bool isVi = LocalizationService.Instance.IsVietnamese;
        bool isConnected = AppServices.Instance.SessionManager.HasActiveSession || AppServices.Instance.ModbusClient.IsConnected;
        IsDeviceConnected = isConnected;

        if (_logicEditorVM != null && _logicEditorVM.CompileState != CompileState.Valid)
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
        if (_ruleTableVM != null && _ruleTableVM.Rules.Count > 0)
        {
            ruleCount = _ruleTableVM.Rules.Count;
            int enabledCount = _ruleTableVM.Rules.Count(r => r.Enabled);
            var firstRule = _ruleTableVM.Rules.FirstOrDefault(r => r.Enabled) ?? _ruleTableVM.Rules.FirstOrDefault();
            if (firstRule != null)
            {
                ruleDesc = $"{firstRule.Id} ({firstRule.TriggerSummary} ➔ {firstRule.ActionSummary})";
            }
            HasValidRules = enabledCount > 0;
            ManifestRuleCount = enabledCount;
            ManifestRuleSummary = ruleDesc;
            string baseSummary = isVi
                ? $"Sẵn sàng nạp: {ruleCount} quy tắc trong Bảng Rule ({enabledCount} đang bật)"
                : $"Ready to deploy: {ruleCount} rules from Rule Table ({enabledCount} enabled)";

            PreflightSummary = isConnected
                ? baseSummary + "."
                : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
        }
        else if (_logicEditorVM?.CurrentProgram != null)
        {
            ruleCount = _logicEditorVM.CurrentProgram.RuleCount;
            int fbCount = _logicEditorVM.CurrentProgram.FunctionBlockTimers.Count + _logicEditorVM.CurrentProgram.FunctionBlockCounters.Count;
            HasValidRules = ruleCount > 0 || fbCount > 0;
            ManifestRuleCount = ruleCount;

            if (fbCount > 0 && ruleCount == 0)
            {
                ruleDesc = isVi
                    ? $"{_logicEditorVM.CurrentProgram.FunctionBlockTimers.Count} Timers, {_logicEditorVM.CurrentProgram.FunctionBlockCounters.Count} Counters (0 Rule slots)"
                    : $"{_logicEditorVM.CurrentProgram.FunctionBlockTimers.Count} Timers, {_logicEditorVM.CurrentProgram.FunctionBlockCounters.Count} Counters (0 Rule slots)";
                string baseSummary = isVi
                    ? $"Sẵn sàng nạp {fbCount} Function Blocks xuống thiết bị."
                    : $"Ready to deploy {fbCount} Function Blocks to device.";
                PreflightSummary = isConnected
                    ? baseSummary
                    : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
            }
            else
            {
                ruleDesc = isVi ? $"{ruleCount} quy tắc từ bản vẽ logic" : $"{ruleCount} rules from logic canvas";
                string baseSummary = string.Format(LocalizationService.Tr("DeployPreflightReady"), ruleCount);
                PreflightSummary = isConnected
                    ? baseSummary
                    : baseSummary + (isVi ? " · ⚠️ Chưa kết nối thiết bị" : " · ⚠️ Device Disconnected");
            }
            ManifestRuleSummary = ruleDesc;
        }
        else
        {
            HasValidRules = false;
            ManifestRuleCount = 0;
            ManifestRuleSummary = "";
            PreflightSummary = LocalizationService.Tr("DeployPreflightNone");
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
            StatusTitle = isVi ? "Chưa Có Quy Tắc Hợp Lệ Để Nạp" : "No Valid Rules to Deploy";
            StatusSubtitle = isVi ? "Bảng Rule hiện tại chưa có quy tắc nào đang được bật." : "Rule Table has no active rules.";
            StatusMessage = PreflightSummary;
            StepperBadgeText = isVi ? "Chưa thể nạp" : "Blocked";
            ActionButtonText = isVi ? "Mở Bảng Rule Để Thêm Quy Tắc" : "Open Rule Table";
            ShowProgressBar = false;
        }
        else
        {
            StatusTitle = isVi ? $"Sẵn Sàng Nạp {ManifestRuleCount} Quy Tắc Xuống Thiết Bị" : $"Ready to Deploy {ManifestRuleCount} Rules to Device";
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
    public void NavigateToRules() => _navigateTab?.Invoke(2);

    [RelayCommand]
    public void NavigateToLogicEditor() => _navigateTab?.Invoke(0);

    [RelayCommand]
    public void NavigateToLiveWatch() => _navigateTab?.Invoke(5);

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

    [RelayCommand(CanExecute = nameof(CanDeploy))]
    public async Task StartDeployAsync()
    {
        if (IsDeploying) return;

        // Strict Pre-flight Safety Gate: If logic editor is present and invalid/stale, abort
        if (_logicEditorVM != null && _logicEditorVM.CompileState != CompileState.Valid)
        {
            DeployState = DeployState.Failed;
            UpdatePreflightSummary();
            StatusMessage = PreflightSummary;
            AppendLog(string.Format(LocalizationService.Tr("DeployLogStop"), PreflightSummary));
            return;
        }

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
            await Task.Delay(60).ConfigureAwait(true);

            var product = services.CurrentProduct;
            RuleTable ruleTable;
            IReadOnlyList<FbTimerRecordDto>? fbTimers = null;
            IReadOnlyList<FbCounterRecordDto>? fbCounters = null;

            if (_ruleTableVM != null && _ruleTableVM.Rules.Count > 0)
            {
                ruleTable = _ruleTableVM.ToDomainRuleTable(product);
            }
            else if (_logicEditorVM?.CurrentProgram != null)
            {
                ruleTable = _logicEditorVM.CurrentProgram.ToRuleTable();
                fbTimers = _logicEditorVM.CurrentProgram.FunctionBlockTimers;
                fbCounters = _logicEditorVM.CurrentProgram.FunctionBlockCounters;
            }
            else
            {
                Steps[0].Status = StepStatus.Failed;
                DeployState = DeployState.Failed;
                PreflightSummary = LocalizationService.Instance.IsVietnamese
                    ? "Không thể nạp: Bảng rule trống. Vui lòng thêm ít nhất 1 quy tắc vào Logic Editor hoặc Bảng Rule."
                    : "Cannot deploy: Rule table is empty. Please add at least one rule to the Logic Editor or Rule Table.";
                StatusMessage = PreflightSummary;
                AppendLog(PreflightSummary);
                IsDeploying = false;
                UpdatePreflightSummary();
                return;
            }

            int totalFbs = (fbTimers?.Count ?? 0) + (fbCounters?.Count ?? 0);
            if (ruleTable.Count == 0 && totalFbs == 0)
            {
                Steps[0].Status = StepStatus.Failed;
                DeployState = DeployState.Failed;
                PreflightSummary = LocalizationService.Instance.IsVietnamese
                    ? "Không thể nạp: Bảng rule và Function Blocks đều trống. Vui lòng thêm ít nhất 1 quy tắc hoặc khối Timer/Counter."
                    : "Cannot deploy: Both Rule table and Function Blocks are empty. Please add at least one rule or Timer/Counter node.";
                StatusMessage = PreflightSummary;
                AppendLog(PreflightSummary);
                IsDeploying = false;
                UpdatePreflightSummary();
                return;
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

            Steps[0].Status = StepStatus.Success;
            ProgressPercent = 30;
            AppendLog(string.Format(LocalizationService.Tr("DeployLogPreflightOk"), ruleTable.Count));
            if (totalFbs > 0)
            {
                bool isVi = LocalizationService.Instance.IsVietnamese;
                AppendLog(isVi
                    ? $"[Cấu hình FB V2] Phát hiện {fbTimers?.Count ?? 0} Timers và {fbCounters?.Count ?? 0} Counters (0 rule slots)."
                    : $"[FB V2 Config] Detected {fbTimers?.Count ?? 0} Timers and {fbCounters?.Count ?? 0} Counters (0 rule slots).");
            }
            await Task.Delay(60).ConfigureAwait(true);

            // Steps 2-4: Staging and Commit
            Steps[1].Status = StepStatus.InProgress;
            ProgressPercent = 45;
            AppendLog(LocalizationService.Tr("DeployLogStaging"));

            var deployUseCase = GetDeployUseCase();
            var deployResult = await deployUseCase.ExecuteAsync(ruleTable, fbTimers, fbCounters, slaveId: 1).ConfigureAwait(true);

            if (deployResult.IsSuccess)
            {
                Steps[1].Status = StepStatus.Success;
                AppendLog(LocalizationService.Tr("DeployLogStagingOk"));
                ProgressPercent = 60;
                await Task.Delay(70).ConfigureAwait(true);

                // Step 3: Xác minh CRC
                Steps[2].Status = StepStatus.InProgress;
                ProgressPercent = 75;
                await Task.Delay(70).ConfigureAwait(true);
                Steps[2].Status = StepStatus.Success;
                AppendLog(LocalizationService.Tr("DeployLogCrcOk"));

                // Step 4: Atomic Commit
                Steps[3].Status = StepStatus.InProgress;
                ProgressPercent = 88;
                await Task.Delay(70).ConfigureAwait(true);
                Steps[3].Status = StepStatus.Success;
                AppendLog(string.Format(LocalizationService.Tr("DeployLogCommitOk"), deployResult.ActiveVersion));

                // Step 5: Version Handshake
                Steps[4].Status = StepStatus.InProgress;
                ProgressPercent = 96;
                await Task.Delay(70).ConfigureAwait(true);
                Steps[4].Status = StepStatus.Success;
                ProgressPercent = 100;

                sw.Stop();
                LastDeployDurationMs = sw.ElapsedMilliseconds;
                IsSuccess = true;
                DeployState = DeployState.Success;
                StatusMessage = string.Format(LocalizationService.Tr("DeployLogSuccess"), deployResult.DeployedRuleCount, deployResult.ActiveVersion);
                string completionSummary = LocalizationService.Instance.IsVietnamese
                    ? $"[HOÀN TẤT] Nạp thành công trong {LastDeployDurationMs}ms · Phiên bản: {deployResult.ActiveVersion} · 100% hợp lệ."
                    : $"[SUCCESS] Deployed in {LastDeployDurationMs}ms · Version: {deployResult.ActiveVersion} · 100% valid.";
                AppendLog(completionSummary);
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
