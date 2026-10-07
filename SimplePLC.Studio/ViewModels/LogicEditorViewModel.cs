using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;

namespace SimplePLC.Studio.ViewModels;

public partial class LogicEditorViewModel : ObservableObject
{
    public TagCatalogViewModel TagCatalog { get; }

    public ObservableCollection<GraphNodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionViewModel> Connections { get; } = new();
    public ObservableCollection<GraphNodeViewModel> SelectedNodes { get; } = new();

    public GraphHistoryService History { get; } = new();
    public bool CanUndo => History.CanUndo;
    public bool CanRedo => History.CanRedo;

    private static GraphClipboardData? _inMemoryClipboard;
    private int _pasteCount = 1;
    public bool CanPaste => _inMemoryClipboard != null && _inMemoryClipboard.Nodes.Count > 0;
    public bool HasSelection => SelectedNodes.Count > 0 || SelectedNode != null || Nodes.Any(n => n.IsSelected) || Connections.Any(c => c.IsSelected);

    [ObservableProperty]
    private bool _hasPendingAiProposal;

    [ObservableProperty]
    private string _aiProposalSummaryText = string.Empty;

    private DraftGraphTransaction? _currentAiTransaction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedInputNode))]
    [NotifyPropertyChangedFor(nameof(SelectedTriggerNode))]
    [NotifyPropertyChangedFor(nameof(SelectedGuardNode))]
    [NotifyPropertyChangedFor(nameof(SelectedActionNode))]
    [NotifyPropertyChangedFor(nameof(SelectedTimerNode))]
    [NotifyPropertyChangedFor(nameof(SelectedCounterNode))]
    [NotifyPropertyChangedFor(nameof(SelectedScaleNode))]
    [NotifyPropertyChangedFor(nameof(NarrativePreview))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private GraphNodeViewModel? _selectedNode;

    public InputNodeViewModel? SelectedInputNode => SelectedNode as InputNodeViewModel;
    public TriggerNodeViewModel? SelectedTriggerNode => SelectedNode as TriggerNodeViewModel;
    public GuardNodeViewModel? SelectedGuardNode => SelectedNode as GuardNodeViewModel;
    public ActionNodeViewModel? SelectedActionNode => SelectedNode as ActionNodeViewModel;
    public TimerNodeViewModel? SelectedTimerNode => SelectedNode as TimerNodeViewModel;
    public CounterNodeViewModel? SelectedCounterNode => SelectedNode as CounterNodeViewModel;
    public ScaleNodeViewModel? SelectedScaleNode => SelectedNode as ScaleNodeViewModel;

    partial void OnSelectedNodeChanged(GraphNodeViewModel? oldValue, GraphNodeViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnSelectedNodePropertyChanged;
            if (oldValue is InputNodeViewModel oldInp && oldInp.Tag != null)
                oldInp.Tag.PropertyChanged -= OnTagPropertyChanged;
            if (oldValue is ActionNodeViewModel oldAct && oldAct.TargetTag != null)
                oldAct.TargetTag.PropertyChanged -= OnTagPropertyChanged;
            if (oldValue is GuardNodeViewModel oldGuard && oldGuard.GuardTag != null)
                oldGuard.GuardTag.PropertyChanged -= OnTagPropertyChanged;
            if (oldValue is TimerNodeViewModel oldTm)
            {
                if (oldTm.InputTag != null) oldTm.InputTag.PropertyChanged -= OnTagPropertyChanged;
                if (oldTm.OutputTag != null) oldTm.OutputTag.PropertyChanged -= OnTagPropertyChanged;
            }
            if (oldValue is CounterNodeViewModel oldCnt)
            {
                if (oldCnt.InputTag != null) oldCnt.InputTag.PropertyChanged -= OnTagPropertyChanged;
                if (oldCnt.CvTag != null) oldCnt.CvTag.PropertyChanged -= OnTagPropertyChanged;
                if (oldCnt.OutputTag != null) oldCnt.OutputTag.PropertyChanged -= OnTagPropertyChanged;
                if (oldCnt.ResetTag != null) oldCnt.ResetTag.PropertyChanged -= OnTagPropertyChanged;
            }
            if (oldValue is ScaleNodeViewModel oldScl)
            {
                if (oldScl.InputTag != null) oldScl.InputTag.PropertyChanged -= OnTagPropertyChanged;
                if (oldScl.OutputTag != null) oldScl.OutputTag.PropertyChanged -= OnTagPropertyChanged;
            }
        }

        if (newValue != null)
        {
            newValue.PropertyChanged += OnSelectedNodePropertyChanged;
            if (newValue is InputNodeViewModel newInp && newInp.Tag != null)
                newInp.Tag.PropertyChanged += OnTagPropertyChanged;
            if (newValue is ActionNodeViewModel newAct && newAct.TargetTag != null)
                newAct.TargetTag.PropertyChanged += OnTagPropertyChanged;
            if (newValue is GuardNodeViewModel newGuard && newGuard.GuardTag != null)
                newGuard.GuardTag.PropertyChanged += OnTagPropertyChanged;
            if (newValue is TimerNodeViewModel newTm)
            {
                if (newTm.InputTag != null) newTm.InputTag.PropertyChanged -= OnTagPropertyChanged;
                if (newTm.OutputTag != null) newTm.OutputTag.PropertyChanged -= OnTagPropertyChanged;
            }
            if (newValue is CounterNodeViewModel newCnt)
            {
                if (newCnt.InputTag != null) newCnt.InputTag.PropertyChanged -= OnTagPropertyChanged;
                if (newCnt.CvTag != null) newCnt.CvTag.PropertyChanged -= OnTagPropertyChanged;
                if (newCnt.OutputTag != null) newCnt.OutputTag.PropertyChanged -= OnTagPropertyChanged;
                if (newCnt.ResetTag != null) newCnt.ResetTag.PropertyChanged -= OnTagPropertyChanged;
            }
            if (newValue is ScaleNodeViewModel newScl)
            {
                if (newScl.InputTag != null) newScl.InputTag.PropertyChanged += OnTagPropertyChanged;
                if (newScl.OutputTag != null) newScl.OutputTag.PropertyChanged += OnTagPropertyChanged;
            }

            if (!newValue.IsSelected)
                newValue.IsSelected = true;
            if (!SelectedNodes.Contains(newValue))
                SelectedNodes.Add(newValue);
        }

        OnPropertyChanged(nameof(NarrativePreview));
    }

    private void OnTagPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Runtime simulation or monitoring value changes MUST NOT invalidate the graph schema
        if (e.PropertyName == nameof(TagModel.Value))
        {
            return;
        }

        OnPropertyChanged(nameof(NarrativePreview));
        NotifyGraphModified();
    }

    private void OnSelectedNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is InputNodeViewModel inp)
        {
            if (e.PropertyName == nameof(InputNodeViewModel.Tag))
            {
                if (inp.Tag != null)
                {
                    inp.Tag.PropertyChanged -= OnTagPropertyChanged;
                    inp.Tag.PropertyChanged += OnTagPropertyChanged;
                }
                OnPropertyChanged(nameof(NarrativePreview));
                NotifyGraphModified();
                return;
            }
        }
        else if (sender is ActionNodeViewModel act)
        {
            if (e.PropertyName == nameof(ActionNodeViewModel.TargetTag))
            {
                if (act.TargetTag != null)
                {
                    act.TargetTag.PropertyChanged -= OnTagPropertyChanged;
                    act.TargetTag.PropertyChanged += OnTagPropertyChanged;
                }
                OnPropertyChanged(nameof(NarrativePreview));
                NotifyGraphModified();
                return;
            }
        }
        else if (sender is GuardNodeViewModel guard)
        {
            if (e.PropertyName == nameof(GuardNodeViewModel.GuardTag))
            {
                if (guard.GuardTag != null)
                {
                    guard.GuardTag.PropertyChanged -= OnTagPropertyChanged;
                    guard.GuardTag.PropertyChanged += OnTagPropertyChanged;
                }
                OnPropertyChanged(nameof(NarrativePreview));
                NotifyGraphModified();
                return;
            }
        }
        else if (sender is TimerNodeViewModel timerNode)
        {
            if (e.PropertyName == nameof(TimerNodeViewModel.InputTag) ||
                e.PropertyName == nameof(TimerNodeViewModel.OutputTag) ||
                e.PropertyName == nameof(TimerNodeViewModel.PresetMs) ||
                e.PropertyName == nameof(TimerNodeViewModel.TimerMode))
            {
                OnPropertyChanged(nameof(NarrativePreview));
                NotifyGraphModified();
                return;
            }
        }
        else if (sender is CounterNodeViewModel counterNode)
        {
            if (e.PropertyName == nameof(CounterNodeViewModel.InputTag) ||
                e.PropertyName == nameof(CounterNodeViewModel.CvTag) ||
                e.PropertyName == nameof(CounterNodeViewModel.OutputTag) ||
                e.PropertyName == nameof(CounterNodeViewModel.ResetTag) ||
                e.PropertyName == nameof(CounterNodeViewModel.PresetValue) ||
                e.PropertyName == nameof(CounterNodeViewModel.CounterMode))
            {
                OnPropertyChanged(nameof(NarrativePreview));
                NotifyGraphModified();
                return;
            }
        }

        OnPropertyChanged(nameof(NarrativePreview));

        // Only notify graph modified for actual semantic configuration changes!
        if (e.PropertyName != null && SemanticNodeProperties.Contains(e.PropertyName))
        {
            NotifyGraphModified();
        }
    }

    public string NarrativePreview
    {
        get
        {
            if (SelectedNode == null)
                return LocalizationService.Tr("NarrativeNoSelection");

            if (SelectedNode is TriggerNodeViewModel trig)
            {
                if (trig.TriggerType == TriggerType.TIME_WINDOW)
                {
                    string timeDesc;
                    if (trig.IsTimePointInTime || trig.CompareOp == CompareOp.EQ)
                    {
                        timeDesc = string.Format(LocalizationService.Tr("NarrTrigWindowPoint"), trig.TimeStartFormatted);
                    }
                    else
                    {
                        bool isOvernight = trig.ThresholdLo > trig.ThresholdHi;
                        timeDesc = isOvernight
                            ? string.Format(LocalizationService.Tr("NarrTrigWindowOvernight"), trig.TimeStartFormatted, trig.TimeEndFormatted)
                            : string.Format(LocalizationService.Tr("NarrTrigWindowRange"), trig.TimeStartFormatted, trig.TimeEndFormatted);
                    }

                    string dwellDesc = "";
                    if (trig.ForMs > 0)
                    {
                        string sec = trig.ForMs >= 1000
                            ? string.Format(LocalizationService.Tr("NarrDwellSec"), (trig.ForMs / 1000.0).ToString("0.#"))
                            : "";
                        dwellDesc = string.Format(LocalizationService.Tr("NarrDwellDesc"), trig.ForMs, sec);
                    }

                    return $"{timeDesc}{dwellDesc}.";
                }

                string edgeDesc = trig.TriggerType switch
                {
                    TriggerType.ON_FALL => LocalizationService.Tr("NarrTrigFall"),
                    TriggerType.ON_RISE => LocalizationService.Tr("NarrTrigRise"),
                    TriggerType.ON_CHANGE => LocalizationService.Tr("NarrTrigChange"),
                    TriggerType.INTERVAL => string.Format(LocalizationService.Tr("NarrTrigInterval"), trig.ForMs),
                    _ => trig.TriggerType.ToString()
                };

                string dwellDescRest = "";
                if (trig.ForMs > 0 && trig.TriggerType != TriggerType.INTERVAL)
                {
                    string sec = trig.ForMs >= 1000
                        ? string.Format(LocalizationService.Tr("NarrDwellSec"), (trig.ForMs / 1000.0).ToString("0.#"))
                        : "";
                    dwellDescRest = string.Format(LocalizationService.Tr("NarrDwellDesc"), trig.ForMs, sec);
                }

                string condDesc = "";
                if (trig.HasComparison)
                {
                    string opStr = trig.CompareOp switch
                    {
                        CompareOp.EQ => string.Format(LocalizationService.Tr("NarrCondEq"), trig.ThresholdLo),
                        CompareOp.NEQ => string.Format(LocalizationService.Tr("NarrCondNeq"), trig.ThresholdLo),
                        CompareOp.GT => string.Format(LocalizationService.Tr("NarrCondGt"), trig.ThresholdLo),
                        CompareOp.LT => string.Format(LocalizationService.Tr("NarrCondLt"), trig.ThresholdLo),
                        CompareOp.GTE => string.Format(LocalizationService.Tr("NarrCondGte"), trig.ThresholdLo),
                        CompareOp.LTE => string.Format(LocalizationService.Tr("NarrCondLte"), trig.ThresholdLo),
                        CompareOp.BETWEEN => string.Format(LocalizationService.Tr("NarrCondBetween"), trig.ThresholdLo, trig.ThresholdHi),
                        _ => ""
                    };
                    condDesc = LocalizationService.Tr("NarrCondPrefix") + opStr;
                }

                return $"{edgeDesc}{condDesc}{dwellDescRest}.";
            }

            if (SelectedNode is GuardNodeViewModel grd)
            {
                string tagName = grd.GuardTag?.DisplayName ?? LocalizationService.Tr("NarrNoTag");
                string state = grd.Negate ? LocalizationService.Tr("NarrGuardOff") : LocalizationService.Tr("NarrGuardOn");
                return string.Format(LocalizationService.Tr("NarrGuard"), tagName, state);
            }

            if (SelectedNode is ActionNodeViewModel act)
            {
                string tgt = act.TargetTag != null ? act.TargetTag.DisplayName : LocalizationService.Tr("NarrNoTargetTag");
                string actDesc = act.ActionType switch
                {
                    ActionType.SET_TAG => string.Format(LocalizationService.Tr("NarrActionSet"), tgt, act.ActionParam),
                    ActionType.TOGGLE_TAG => string.Format(LocalizationService.Tr("NarrActionToggle"), tgt),
                    ActionType.INC_COUNTER => string.Format(LocalizationService.Tr("NarrActionInc"), act.ActionParam, tgt),
                    ActionType.WRITE_REMOTE => string.Format(LocalizationService.Tr("NarrActionRemote"), act.ActionParam),
                    ActionType.SEND_ALARM => string.Format(LocalizationService.Tr("NarrActionAlarm"), tgt),
                    ActionType.LOG_EVENT => string.Format(LocalizationService.Tr("NarrActionLog"), tgt),
                    _ => $"{act.ActionType} -> {tgt}"
                };
                return string.Format(LocalizationService.Tr("NarrActionExec"), actDesc);
            }

            if (SelectedNode is InputNodeViewModel inp)
            {
                string tagName = inp.Tag != null ? inp.Tag.DisplayName : LocalizationService.Tr("NarrNoTag");
                return string.Format(LocalizationService.Tr("NarrInputBlock"), tagName);
            }

            if (SelectedNode is TimerNodeViewModel timerNode)
            {
                return timerNode.NarrativeText;
            }

            if (SelectedNode is CounterNodeViewModel counterNode)
            {
                return counterNode.NarrativeText;
            }

            if (SelectedNode is ScaleNodeViewModel scaleNode)
            {
                return scaleNode.NarrativeText;
            }

            return SelectedNode.SummaryText;
        }
    }

    private readonly RuleTableViewModel? _ruleTable;
    private readonly Action<int>? _navigateToTab;
    private readonly IRuleCompiler _compiler;

    public IReadOnlyList<Diagnostic> Diagnostics { get; private set; } = Array.Empty<Diagnostic>();

    /// <summary>
    /// Kích hoạt khi người dùng click vào một lỗi chẩn đoán, yêu cầu View cuộn tâm Canvas vào Node đó
    /// </summary>
    public Action<GraphNodeViewModel>? RequestCenterOnNode { get; set; }

    [RelayCommand]
    public void NavigateToDiagnostic(Diagnostic? diag)
    {
        if (diag == null || string.IsNullOrEmpty(diag.NodeId)) return;

        var targetNode = Nodes.FirstOrDefault(n => n.Id == diag.NodeId);
        if (targetNode != null)
        {
            SelectExclusive(targetNode);
            RequestCenterOnNode?.Invoke(targetNode);
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CompileStateDisplay))]
    private CompileState _compileState = CompileState.NotCompiled;

    public string CompileStateDisplay => CompileState switch
    {
        CompileState.Valid => LocalizationService.Tr("CompileStateValid"),
        CompileState.Stale => LocalizationService.Tr("CompileStateStale"),
        _ => LocalizationService.Tr("CompileStateInvalid")
    };

    [ObservableProperty]
    private SimplePLC.Application.Logic.Compilation.CompiledProgram? _currentProgram;

    [ObservableProperty]
    private SimplePLC.Application.Logic.Compilation.CompiledProgram? _lastSuccessfulProgram;

    [ObservableProperty]
    private bool _hasNodes;

    [ObservableProperty]
    private string _saveStatusMessage = "● Tự động lưu";

    [ObservableProperty]
    private bool _isSaved = true;

    [ObservableProperty]
    private int _validRuleCount;

    [ObservableProperty]
    private string _currentDiagramId = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string? _editingRuleId;

    public bool IsEditingExistingRule => !string.IsNullOrEmpty(EditingRuleId);

    public string EditorModeTitle => IsEditingExistingRule
        ? string.Format(LocalizationService.Instance.IsVietnamese ? "✏️ Đang sửa: {0}" : "✏️ Editing: {0}", EditingRuleId)
        : (LocalizationService.Instance.IsVietnamese ? "✨ Tạo Rule Mới" : "✨ New Rule");

    public string EditorModeBadge => IsEditingExistingRule
        ? $"✏️ {EditingRuleId}"
        : (LocalizationService.Instance.IsVietnamese ? "✨ Mới" : "✨ New");

    partial void OnEditingRuleIdChanged(string? value)
    {
        OnPropertyChanged(nameof(IsEditingExistingRule));
        OnPropertyChanged(nameof(EditorModeTitle));
        OnPropertyChanged(nameof(EditorModeBadge));
    }

    [ObservableProperty]
    private string _saveToastMessage = string.Empty;

    [ObservableProperty]
    private bool _showSaveToast;

    [ObservableProperty]
    private bool _isSimulationMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimRunStatusText))]
    [NotifyPropertyChangedFor(nameof(SimPlayPauseButtonText))]
    [NotifyPropertyChangedFor(nameof(SimPlayPauseIcon))]
    private bool _isSimRunning;

    public string SimRunStatusText => IsSimRunning
        ? LocalizationService.Instance["SimBarSimulating"]
        : LocalizationService.Instance["SimBarPaused"];

    public string SimPlayPauseButtonText => IsSimRunning
        ? LocalizationService.Instance["SimBarPause"]
        : LocalizationService.Instance["SimBarAutoRun"];

    public string SimPlayPauseIcon => IsSimRunning ? "⏸" : "▶";

    [ObservableProperty]
    private long _simScanNumber;

    [ObservableProperty]
    private long _simElapsedMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSimGateWarning))]
    private string _simGateWarning = string.Empty;

    public bool HasSimGateWarning => !string.IsNullOrWhiteSpace(SimGateWarning);

    [ObservableProperty]
    private bool _showSimGateToast;

    private CancellationTokenSource? _simGateToastCts;

    partial void OnSimGateWarningChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            ShowSimGateToast = true;
            _simGateToastCts?.Cancel();
            _simGateToastCts = new CancellationTokenSource();
            var token = _simGateToastCts.Token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3500, token);
                    if (!token.IsCancellationRequested)
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            ShowSimGateToast = false;
                        });
                    }
                }
                catch (TaskCanceledException) { }
            });
        }
        else
        {
            ShowSimGateToast = false;
        }
    }

    [ObservableProperty]
    private string _simStatusText = string.Empty;

    [ObservableProperty]
    private bool _isHistoryPanelOpen = true;

    public ObservableCollection<SimScanHistoryItem> SimScanHistory { get; } = new();

    public ICollectionView FilteredSimScanHistory { get; }

    [ObservableProperty]
    private SimScanHistoryItem? _selectedScanItem;

    [ObservableProperty]
    private bool _filterChangedScansOnly;

    partial void OnFilterChangedScansOnlyChanged(bool value)
    {
        FilteredSimScanHistory?.Refresh();
    }

    private bool FilterScanHistoryPredicate(object obj)
    {
        if (!FilterChangedScansOnly) return true;
        if (obj is SimScanHistoryItem item)
        {
            return item.HasActivity;
        }
        return true;
    }

    [RelayCommand]
    public void ClearScanLog()
    {
        SimScanHistory.Clear();
        SelectedScanItem = null;
    }

    public IReadOnlyList<SimSpeedOption> SimSpeedOptions { get; } = new List<SimSpeedOption>
    {
        new(50, "50 ms (Mượt / Smooth)"),
        new(100, "100 ms"),
        new(500, "500 ms"),
        new(1000, "1.0 s"),
        new(2000, "2.0 s")
    };

    [ObservableProperty]
    private SimSpeedOption _selectedSimSpeed;

    [ObservableProperty]
    private bool _isVirtualIoBoardOpen = true;

    [RelayCommand]
    public void ToggleVirtualIoBoard()
    {
        IsVirtualIoBoardOpen = !IsVirtualIoBoardOpen;
    }

    public IEnumerable<TagModel> VirtualDiTags => TagCatalog.AllTags.Where(t => t.Kind == TagKind.DiscreteInput);
    public IEnumerable<TagModel> VirtualDoTags => TagCatalog.AllTags.Where(t => t.Kind == TagKind.DiscreteOutput);
    public IEnumerable<TagModel> VirtualAiTags => TagCatalog.AllTags.Where(t => t.Kind == TagKind.AnalogInput);

    [RelayCommand]
    public void ToggleVirtualDi(TagModel? tag)
    {
        if (tag == null) return;
        ToggleSimTag(tag);
        if (IsSimulationMode && !IsSimRunning)
        {
            StepSimScan();
        }
    }

    [RelayCommand]
    public void SetAllVirtualDi(object? param)
    {
        int value = 0;
        if (param is int iv) value = iv;
        else if (param != null && int.TryParse(param.ToString(), out var parsed)) value = parsed;
        foreach (var tag in VirtualDiTags)
        {
            tag.Value = value;
            foreach (var node in Nodes.OfType<InputNodeViewModel>())
            {
                if (node.Tag?.Index == tag.Index)
                {
                    node.IsLiveActive = value != 0;
                    node.IsSimActive = value != 0;
                    foreach (var conn in Connections.Where(c => c.Source?.Node == node))
                    {
                        conn.IsActive = node.IsSimActive;
                    }
                }
            }
        }
        if (IsSimulationMode && !IsSimRunning)
        {
            StepSimScan();
        }
    }

    [RelayCommand]
    public void ResetAllVirtualIo()
    {
        ResetSim();
    }

    partial void OnSelectedSimSpeedChanged(SimSpeedOption value)
    {
        if (value != null && IsSimRunning && _simTimer != null)
        {
            _simTimer.Interval = TimeSpan.FromMilliseconds(value.IntervalMs);
        }
    }

    [RelayCommand]
    public void ToggleHistoryPanel()
    {
        IsHistoryPanelOpen = !IsHistoryPanelOpen;
    }

    public IEnumerable<InputNodeViewModel> CanvasInputNodes => Nodes.OfType<InputNodeViewModel>();
    public IEnumerable<ActionNodeViewModel> CanvasActionNodes => Nodes.OfType<ActionNodeViewModel>();

    [RelayCommand]
    public void ToggleSimInputNode(InputNodeViewModel? node)
    {
        if (node == null) return;
        ToggleInputNode(node);
    }

    [RelayCommand]
    public void DecrementSimAnalog(InputNodeViewModel? node)
    {
        if (node?.Tag == null) return;
        node.Tag.Value = Math.Max(0, node.Tag.Value - 10);
    }

    [RelayCommand]
    public void IncrementSimAnalog(InputNodeViewModel? node)
    {
        if (node?.Tag == null) return;
        node.Tag.Value = Math.Min(65535, node.Tag.Value + 10);
    }

    private readonly Dictionary<int, int> _lastScanTagValues = new();

    private RuntimeEngine? _simEngine;
    private readonly SimulatedClock _simClock = new();
    private DispatcherTimer? _simTimer;
    private CancellationTokenSource? _simAnimationCts;

    public int SimPropagationDelayMs { get; set; } = 180;
    public Task? LastAnimationTask { get; private set; }

    private CancellationTokenSource? _toastCts;
    private CancellationTokenSource? _compileDebounceCts;
    private bool _isInitializing;

    public int DebounceDelayMs { get; set; } = 600;

    [RelayCommand]
    public void DismissToast()
    {
        ShowSaveToast = false;
    }

    [RelayCommand]
    public void DismissSimGateToast()
    {
        ShowSimGateToast = false;
    }

    public LogicEditorViewModel(
        TagCatalogViewModel tagCatalog,
        RuleTableViewModel? ruleTable = null,
        Action<int>? navigateToTab = null,
        IRuleCompiler? compiler = null,
        IRuntimeStateStore? stateStore = null,
        Action<Action>? uiDispatcher = null)
    {
        FilteredSimScanHistory = CollectionViewSource.GetDefaultView(SimScanHistory);
        FilteredSimScanHistory.Filter = FilterScanHistoryPredicate;

        TagCatalog = tagCatalog;
        _ruleTable = ruleTable;
        _navigateToTab = navigateToTab;
        _compiler = compiler ?? new RuleCompiler();
        _stateStore = stateStore ?? AppServices.Instance.StateStore;
        _uiDispatcher = uiDispatcher ?? (action =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
            {
                dispatcher.BeginInvoke(DispatcherPriority.DataBind, action);
            }
            else
            {
                action();
            }
        });

        if (_stateStore != null)
        {
            _stateStore.SnapshotUpdated += OnSnapshotUpdated;
        }

        Connections.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (ConnectionViewModel conn in e.NewItems)
                {
                    conn.PropertyChanged += OnConnectionPropertyChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (ConnectionViewModel conn in e.OldItems)
                {
                    conn.PropertyChanged -= OnConnectionPropertyChanged;
                    conn.IsSelected = false;
                }
            }
            NotifyGraphModified();
            OnPropertyChanged(nameof(HasSelection));
        };
        Nodes.CollectionChanged += (s, e) =>
        {
            HasNodes = Nodes.Count > 0;
            if (e.NewItems != null)
            {
                foreach (GraphNodeViewModel node in e.NewItems)
                {
                    node.PropertyChanged += OnNodeSelectionPropertyChanged;
                    if (node.IsSelected && !SelectedNodes.Contains(node))
                    {
                        SelectedNodes.Add(node);
                    }
                }
            }
            if (e.OldItems != null)
            {
                foreach (GraphNodeViewModel node in e.OldItems)
                {
                    node.PropertyChanged -= OnNodeSelectionPropertyChanged;
                    node.IsSelected = false;
                    SelectedNodes.Remove(node);
                    node.Dispose();
                }
            }
            RebuildTagToNodesIndex();
            NotifyGraphModified();
            OnPropertyChanged(nameof(CanvasInputNodes));
            OnPropertyChanged(nameof(CanvasActionNodes));
            if (_stateStore?.CurrentSnapshot != null)
            {
                ApplySnapshotDiff(_stateStore.CurrentSnapshot);
            }
        };

        SelectedNodes.CollectionChanged += (s, e) =>
        {
            if (e.NewItems != null)
            {
                foreach (GraphNodeViewModel node in e.NewItems)
                {
                    node.IsSelected = true;
                }
            }
            if (e.OldItems != null)
            {
                foreach (GraphNodeViewModel node in e.OldItems)
                {
                    node.IsSelected = false;
                }
            }

            if (SelectedNodes.Count > 0)
            {
                if (SelectedNode == null || !SelectedNodes.Contains(SelectedNode))
                {
                    SelectedNode = SelectedNodes.Last();
                }
            }
            else
            {
                SelectedNode = null;
            }
            OnPropertyChanged(nameof(HasSelection));
        };

        History.HistoryChanged += (s, e) =>
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
        };

        SimplePLC.Studio.Services.LocalizationService.Instance.LanguageChanged += () =>
        {
            OnPropertyChanged(nameof(NarrativePreview));
            OnPropertyChanged(nameof(CompileStateDisplay));
            OnPropertyChanged(nameof(SimRunStatusText));
            OnPropertyChanged(nameof(SimPlayPauseButtonText));
            CompileNow();
        };

        _selectedSimSpeed = SimSpeedOptions.FirstOrDefault(o => o.IntervalMs == 100) ?? SimSpeedOptions[0];

        _isInitializing = true;
        try
        {
            RebuildTagToNodesIndex();
            CompileAndSaveRules(true);
            if (_stateStore?.CurrentSnapshot != null)
            {
                ApplySnapshotDiff(_stateStore.CurrentSnapshot);
            }
        }
        finally
        {
            _isInitializing = false;
        }
    }

    [ObservableProperty]
    private bool _isOnlineDebugEnabled;

    private readonly IRuntimeStateStore? _stateStore;
    private readonly Action<Action> _uiDispatcher;
    private readonly Dictionary<ushort, List<ILiveTagBoundNode>> _tagToNodesIndex = new();
    private readonly Dictionary<ushort, (int RawValue, TagQuality Quality)> _lastAppliedTagState = new();
    private bool _lastOnlineState;
    private RuntimeDeviceSnapshot? _latestSnapshot;
    private int _isUpdateScheduled;

    public IReadOnlyDictionary<ushort, List<ILiveTagBoundNode>> TagToNodesIndex => _tagToNodesIndex;

    private void RebuildTagToNodesIndex()
    {
        _tagToNodesIndex.Clear();
        foreach (var node in Nodes.OfType<ILiveTagBoundNode>())
        {
            IndexNode(node);
        }
    }

    private void IndexNode(ILiveTagBoundNode node)
    {
        if (node.BoundTagIndex is { } tagIndex)
        {
            if (!_tagToNodesIndex.TryGetValue(tagIndex, out var list))
            {
                list = new List<ILiveTagBoundNode>();
                _tagToNodesIndex[tagIndex] = list;
            }
            if (!list.Contains(node))
            {
                list.Add(node);
            }
        }
    }

    private void OnSnapshotUpdated(RuntimeDeviceSnapshot snapshot)
    {
        if (IsSimulationMode) return;

        _latestSnapshot = snapshot;
        if (Interlocked.Exchange(ref _isUpdateScheduled, 1) == 0)
        {
            _uiDispatcher(() =>
            {
                _isUpdateScheduled = 0;
                var target = Interlocked.Exchange(ref _latestSnapshot, null);
                if (target != null)
                {
                    ApplySnapshotDiff(target);
                }
            });
        }
    }

    public void ApplySnapshotDiff(RuntimeDeviceSnapshot snapshot)
    {
        if (IsSimulationMode) return;

        bool isOnline = snapshot.ConnectionStatus == ConnectionStatus.Connected;

        if (isOnline && !IsOnlineDebugEnabled)
        {
            IsOnlineDebugEnabled = true;
        }

        bool connectionChanged = (isOnline != _lastOnlineState);

        if (connectionChanged)
        {
            _lastOnlineState = isOnline;
            _lastAppliedTagState.Clear();

            if (!isOnline)
            {
                foreach (var nodes in _tagToNodesIndex.Values)
                {
                    foreach (var node in nodes)
                    {
                        node.SetOffline();
                    }
                }
                return;
            }
        }

        if (snapshot.Tags != null && snapshot.Tags.Count > 0)
        {
            foreach (var tag in snapshot.Tags)
            {
                bool hasChanged = connectionChanged ||
                                  !_lastAppliedTagState.TryGetValue(tag.TagIndex, out var prev) ||
                                  prev.RawValue != tag.RawValue ||
                                  prev.Quality != tag.Quality;

                if (hasChanged)
                {
                    _lastAppliedTagState[tag.TagIndex] = (tag.RawValue, tag.Quality);

                    if (_tagToNodesIndex.TryGetValue(tag.TagIndex, out var boundNodes))
                    {
                        foreach (var node in boundNodes)
                        {
                            node.ApplyRuntimeSnapshot(tag, isOnline);
                        }
                    }
                }
            }
        }

        // Cập nhật phân vùng Function Blocks (0x0B00..0x0B7F) cho Timer và Counter nodes trên canvas
        if (snapshot.Timers != null && snapshot.Timers.Count > 0)
        {
            var timerNodes = Nodes.OfType<TimerNodeViewModel>().ToList();
            for (int i = 0; i < timerNodes.Count && i < snapshot.Timers.Count; i++)
            {
                var tmNode = timerNodes[i];
                var tmDto = snapshot.Timers[i];

                tmNode.ElapsedMs = tmDto.ElapsedMs;
                tmNode.ProgressPercent = tmNode.PresetMs > 0
                    ? Math.Min(100.0, ((double)tmDto.ElapsedMs / tmNode.PresetMs) * 100.0)
                    : 0.0;
                tmNode.IsTiming = tmDto.Running;
                tmNode.IsLiveActive = tmDto.Q;
                tmNode.IsLiveOnline = isOnline;
            }
        }

        if (snapshot.Counters != null && snapshot.Counters.Count > 0)
        {
            var counterNodes = Nodes.OfType<CounterNodeViewModel>().ToList();
            for (int i = 0; i < counterNodes.Count && i < snapshot.Counters.Count; i++)
            {
                var cntNode = counterNodes[i];
                var cntDto = snapshot.Counters[i];

                cntNode.CurrentCount = cntDto.CurrentValue;
                cntNode.IsLiveActive = cntDto.Q;
                cntNode.IsLiveOnline = isOnline;
            }
        }
    }

    private void OnNodeSelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is GraphNodeViewModel node && e.PropertyName == nameof(GraphNodeViewModel.IsSelected))
        {
            if (node.IsSelected)
            {
                if (!SelectedNodes.Contains(node))
                {
                    SelectedNodes.Add(node);
                }
                SelectedNode = node;
            }
            else
            {
                SelectedNodes.Remove(node);
                if (SelectedNode == node)
                {
                    SelectedNode = SelectedNodes.LastOrDefault();
                }
            }
        }
        else if (e.PropertyName != null && SemanticNodeProperties.Contains(e.PropertyName))
        {
            if (e.PropertyName is nameof(InputNodeViewModel.Tag) 
                     or nameof(ActionNodeViewModel.TargetTag) 
                     or nameof(GuardNodeViewModel.GuardTag)
                     or nameof(TimerNodeViewModel.OutputTag)
                     or nameof(CounterNodeViewModel.CvTag)
                     or nameof(CounterNodeViewModel.OutputTag))
            {
                RebuildTagToNodesIndex();
                if (_stateStore?.CurrentSnapshot != null)
                {
                    ApplySnapshotDiff(_stateStore.CurrentSnapshot);
                }
            }
            NotifyGraphModified();
        }
    }

    private static readonly HashSet<string> SemanticNodeProperties = new()
    {
        nameof(InputNodeViewModel.Tag),
        nameof(TriggerNodeViewModel.TriggerType),
        nameof(TriggerNodeViewModel.ForMs),
        nameof(TriggerNodeViewModel.CompareOp),
        nameof(TriggerNodeViewModel.ThresholdLo),
        nameof(TriggerNodeViewModel.ThresholdHi),
        nameof(TriggerNodeViewModel.HasComparison),
        nameof(GuardNodeViewModel.GuardTag),
        nameof(GuardNodeViewModel.Negate),
        nameof(ActionNodeViewModel.ActionType),
        nameof(ActionNodeViewModel.ActionParam),
        nameof(ActionNodeViewModel.TargetTag),
        nameof(TimerNodeViewModel.TimerMode),
        nameof(TimerNodeViewModel.PresetMs),
        nameof(TimerNodeViewModel.InputTag),
        nameof(TimerNodeViewModel.OutputTag),
        nameof(CounterNodeViewModel.CounterMode),
        nameof(CounterNodeViewModel.PresetValue),
        nameof(CounterNodeViewModel.InputTag),
        nameof(CounterNodeViewModel.CvTag),
        nameof(CounterNodeViewModel.OutputTag),
        nameof(CounterNodeViewModel.ResetTag),
        nameof(GraphNodeViewModel.CustomLabel)
    };

    private void OnConnectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConnectionViewModel.IsSelected))
        {
            OnPropertyChanged(nameof(HasSelection));
        }
    }

    public void LoadDefaultDemoGraph()
    {
        SelectedNodes.Clear();
        Nodes.Clear();
        Connections.Clear();

        var tagDI0 = TagCatalog.AllTags.FirstOrDefault(t => t.Name == "DI0") ?? TagCatalog.AllTags[1];
        var tagDO0 = TagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO0") ?? TagCatalog.AllTags[9];
        var tagAI0 = TagCatalog.AllTags.FirstOrDefault(t => t.Name == "AI0") ?? TagCatalog.AllTags[17];
        var tagDO1 = TagCatalog.AllTags.FirstOrDefault(t => t.Name == "DO1") ?? TagCatalog.AllTags[10];
        var tagVFlag0 = TagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0") ?? TagCatalog.AllTags[33];

        // ==================== QUY TẮC 1 (Rule #0 - Giám sát dừng máy) ====================
        // 1. Input Node: DI0 (Cảm biến dừng khẩn)
        var inputNode1 = new InputNodeViewModel(tagDI0) { Location = new Point(80, 100) };
        Nodes.Add(inputNode1);

        // 2. Trigger Node: Sườn xuống (Máy dập dừng)
        var triggerNode1 = new TriggerNodeViewModel
        {
            TriggerType = TriggerType.ON_FALL,
            ForMs = 15000,
            Location = new Point(340, 100)
        };
        Nodes.Add(triggerNode1);

        // 3. Guard Node: Trong ca sản xuất (VFLAG0 == 1)
        var guardNode1 = new GuardNodeViewModel(tagVFlag0)
        {
            Negate = false,
            Location = new Point(600, 100)
        };
        Nodes.Add(guardNode1);

        // 4. Action Node: Bật đèn còi đỏ DO0
        var actionNode1 = new ActionNodeViewModel(tagDO0)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = 1,
            Location = new Point(860, 100)
        };
        Nodes.Add(actionNode1);

        // Nối dây Quy tắc 1
        Connect(inputNode1.OutputConnectors[0], triggerNode1.InputConnectors[0]);
        Connect(triggerNode1.OutputConnectors[0], guardNode1.InputConnectors[0]);
        Connect(guardNode1.OutputConnectors[0], actionNode1.InputConnectors[0]);

        // ==================== QUY TẮC 2 (Rule #1 - Quá nhiệt làm mát) ====================
        // 1. Input Node: AI0 (Cảm biến nhiệt độ buồng sấy)
        var inputNode2 = new InputNodeViewModel(tagAI0) { Location = new Point(80, 260) };
        Nodes.Add(inputNode2);

        // 2. Trigger Node: Khi nhiệt độ thay đổi vượt ngưỡng > 85°C
        var triggerNode2 = new TriggerNodeViewModel
        {
            TriggerType = TriggerType.ON_CHANGE,
            CompareOp = CompareOp.GT,
            ThresholdLo = 85,
            ForMs = 0,
            Location = new Point(340, 260)
        };
        Nodes.Add(triggerNode2);

        // 3. Action Node: Kích hoạt quạt giải nhiệt DO1
        var actionNode2 = new ActionNodeViewModel(tagDO1)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = 1,
            Location = new Point(600, 260)
        };
        Nodes.Add(actionNode2);

        // Nối dây Quy tắc 2
        Connect(inputNode2.OutputConnectors[0], triggerNode2.InputConnectors[0]);
        Connect(triggerNode2.OutputConnectors[0], actionNode2.InputConnectors[0]);

        SelectedNode = null;
    }

    [RelayCommand]
    public void LoadDefaultDemoGraphCommand()
    {
        LoadDefaultDemoGraph();
    }

    public void Connect(ConnectorViewModel source, ConnectorViewModel target)
    {
        if (Connections.Any(c => c.Source == source && c.Target == target))
            return;

        var conn = new ConnectionViewModel(source, target);
        Connections.Add(conn);

        // Đồng bộ Tag cho TimerNodeViewModel / CounterNodeViewModel / ScaleNodeViewModel
        if (target.Node is TimerNodeViewModel timerNode && source.Node is InputNodeViewModel inputNode)
        {
            timerNode.InputTag = inputNode.Tag;
        }
        else if (target.Node is CounterNodeViewModel counterNode && source.Node is InputNodeViewModel inNode)
        {
            if (string.Equals(target.Title, "R", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target.Title, "Reset", StringComparison.OrdinalIgnoreCase))
            {
                counterNode.ResetTag = inNode.Tag;
            }
            else
            {
                counterNode.InputTag = inNode.Tag;
            }
        }
        else if (target.Node is ScaleNodeViewModel scaleNode && source.Node is InputNodeViewModel inScale)
        {
            scaleNode.InputTag = inScale.Tag;
        }
        else if (source.Node is CounterNodeViewModel counterNodeFrom && target.Node is ActionNodeViewModel actNode)
        {
            counterNodeFrom.OutputTag = actNode.TargetTag;
        }
        else if (source.Node is TimerNodeViewModel timerNodeFrom && target.Node is ActionNodeViewModel tmActNode)
        {
            timerNodeFrom.OutputTag = tmActNode.TargetTag;
        }
        else if (source.Node is ScaleNodeViewModel scaleNodeFrom && target.Node is ActionNodeViewModel sclActNode)
        {
            scaleNodeFrom.OutputTag = sclActNode.TargetTag;
        }
    }

    [RelayCommand]
    public void CompleteConnection(object? param)
    {
        ConnectorViewModel? source = null;
        ConnectorViewModel? target = null;

        if (param is ValueTuple<object, object> tuple)
        {
            source = tuple.Item1 as ConnectorViewModel;
            target = tuple.Item2 as ConnectorViewModel;
        }
        else if (param is ValueTuple<ConnectorViewModel, ConnectorViewModel> typedTuple)
        {
            source = typedTuple.Item1;
            target = typedTuple.Item2;
        }

        if (source == null || target == null)
            return;

        // Nếu người dùng kéo ngược từ Input sang Output, tự động đổi chiều
        if (source.IsInput && !target.IsInput)
        {
            var temp = source;
            source = target;
            target = temp;
        }

        // Không cho phép nối vào cùng khối hoặc nối Output-Output / Input-Input
        if (source.Node == target.Node || source.IsInput || !target.IsInput)
            return;

        // Kiểm tra ngữ pháp Logic Core V1 (GraphGrammarV1)
        static SimplePLC.Application.Logic.Graph.LogicNodeKind? GetNodeKind(GraphNodeViewModel? node) => node switch
        {
            InputNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Input,
            TriggerNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Trigger,
            GuardNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Guard,
            ActionNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Action,
            TimerNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Timer,
            CounterNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Counter,
            ScaleNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Scale,
            _ => null
        };

        var srcKind = GetNodeKind(source.Node);
        var tgtKind = GetNodeKind(target.Node);
        if (srcKind == null || tgtKind == null || !SimplePLC.Application.Logic.Graph.GraphGrammarV1.CanConnect(srcKind.Value, tgtKind.Value))
            return;

        // Không cho phép nối trùng lặp nếu giữa 2 cổng này đã có kết nối
        if (Connections.Any(c => c.Source == source && c.Target == target))
            return;

        // Tự động gỡ dây kết nối cũ tới cổng Input này nếu đã có
        RecordSnapshot("Connect");

        var existingIn = Connections.FirstOrDefault(c => c.Target == target);
        if (existingIn != null)
        {
            Connections.Remove(existingIn);
            existingIn.Source.IsConnected = Connections.Any(c => c.Source == existingIn.Source || c.Target == existingIn.Source);
        }

        Connect(source, target);

        // Đồng bộ Tag cho TimerNodeViewModel / CounterNodeViewModel / ScaleNodeViewModel khi được nối từ InputNodeViewModel hoặc sang ActionNodeViewModel
        if (target.Node is TimerNodeViewModel timerNode && source.Node is InputNodeViewModel inputNode)
        {
            timerNode.InputTag = inputNode.Tag;
        }
        else if (target.Node is CounterNodeViewModel counterNode && source.Node is InputNodeViewModel inNode)
        {
            if (string.Equals(target.Title, "R", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(target.Title, "Reset", StringComparison.OrdinalIgnoreCase))
            {
                counterNode.ResetTag = inNode.Tag;
            }
            else
            {
                counterNode.InputTag = inNode.Tag;
            }
        }
        else if (target.Node is ScaleNodeViewModel scaleNode && source.Node is InputNodeViewModel inScale)
        {
            scaleNode.InputTag = inScale.Tag;
        }
        else if (source.Node is CounterNodeViewModel counterNodeFrom && target.Node is ActionNodeViewModel actNode)
        {
            counterNodeFrom.OutputTag = actNode.TargetTag;
        }
        else if (source.Node is TimerNodeViewModel timerNodeFrom && target.Node is ActionNodeViewModel tmActNode)
        {
            timerNodeFrom.OutputTag = tmActNode.TargetTag;
        }
        else if (source.Node is ScaleNodeViewModel scaleNodeFrom && target.Node is ActionNodeViewModel sclActNode)
        {
            scaleNodeFrom.OutputTag = sclActNode.TargetTag;
        }

        // Tự động căn thẳng hàng ngang hai khối nếu độ cao ban đầu tương đương nhau (chênh lệch <= 35px)
        if (source.Node != null && target.Node != null && Math.Abs(source.Node.Location.Y - target.Node.Location.Y) <= 35.0)
        {
            target.Node.Location = new Point(target.Node.Location.X, source.Node.Location.Y);
        }
    }

    [RelayCommand]
    public void DisconnectConnector(object? param)
    {
        if (param is ConnectorViewModel connector)
        {
            var toRemove = Connections.Where(c => c.Source == connector || c.Target == connector).ToList();
            if (toRemove.Count == 0) return;

            RecordSnapshot("Disconnect");
            foreach (var conn in toRemove)
            {
                RemoveConnection(conn);
            }
            connector.IsConnected = false;
        }
    }

    [RelayCommand]
    public void RemoveConnection(object? param)
    {
        if (param is ConnectionViewModel conn)
        {
            RecordSnapshot("Remove Connection");
            Connections.Remove(conn);
            conn.Source.IsConnected = Connections.Any(c => c.Source == conn.Source || c.Target == conn.Source);
            conn.Target.IsConnected = Connections.Any(c => c.Source == conn.Target || c.Target == conn.Target);

            if (conn.Target?.Node is CounterNodeViewModel cntTarget)
            {
                if (string.Equals(conn.Target.Title, "R", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(conn.Target.Title, "Reset", StringComparison.OrdinalIgnoreCase))
                {
                    cntTarget.ResetTag = null;
                }
                else
                {
                    cntTarget.InputTag = null;
                }
            }
            else if (conn.Target?.Node is TimerNodeViewModel tmTarget)
            {
                tmTarget.InputTag = null;
            }
            else if (conn.Target?.Node is ScaleNodeViewModel sclTarget)
            {
                sclTarget.InputTag = null;
            }

            if (conn.Source?.Node is CounterNodeViewModel cntSource)
            {
                cntSource.OutputTag = null;
            }
            else if (conn.Source?.Node is TimerNodeViewModel tmSource)
            {
                tmSource.OutputTag = null;
            }
            else if (conn.Source?.Node is ScaleNodeViewModel sclSource)
            {
                sclSource.OutputTag = null;
            }
        }
    }

    public void DeselectAllConnections()
    {
        foreach (var conn in Connections)
        {
            conn.IsSelected = false;
        }
        OnPropertyChanged(nameof(HasSelection));
    }

    public void DeselectAllNodes()
    {
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
        SelectedNodes.Clear();
        SelectedNode = null;
        DeselectAllConnections();
    }

    public void SelectExclusive(GraphNodeViewModel node)
    {
        DeselectAllConnections();
        foreach (var n in Nodes)
        {
            if (n != node && n.IsSelected)
            {
                n.IsSelected = false;
            }
        }
        SelectedNodes.Clear();
        node.IsSelected = true;
        if (!SelectedNodes.Contains(node))
        {
            SelectedNodes.Add(node);
        }
        SelectedNode = node;
    }

    public void AddNode(string nodeType, Point position, TagModel? tag = null)
    {
        GraphNodeViewModel? newNode = nodeType switch
        {
            "Input" => new InputNodeViewModel(tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteInput)),
            "Trigger" => new TriggerNodeViewModel(),
            "Guard" => new GuardNodeViewModel(tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0") ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteInput)),
            "Action" => new ActionNodeViewModel(tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput)),
            "Timer" or "TON" => new TimerNodeViewModel("TON", tag, null),
            "TOF" => new TimerNodeViewModel("TOF", tag, null),
            "TP" => new TimerNodeViewModel("TP", tag, null),
            "Counter" or "CTU" => CreateCounterNode("CTU", tag),
            "CTD" => CreateCounterNode("CTD", tag),
            "Scale" => new ScaleNodeViewModel(
                tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput),
                null),
            _ => null
        };

        if (newNode != null)
        {
            RecordSnapshot("Add Node");
            newNode.Location = position;

            // Hủy chọn tất cả các khối cũ trước khi chọn khối mới kéo vào
            DeselectAllNodes();

            Nodes.Add(newNode);
            newNode.IsSelected = true;
            if (!SelectedNodes.Contains(newNode))
            {
                SelectedNodes.Add(newNode);
            }
            SelectedNode = newNode;
        }
    }

    private CounterNodeViewModel CreateCounterNode(string mode, TagModel? tag)
    {
        var usedCvNames = Nodes.OfType<CounterNodeViewModel>()
            .Where(c => c.CvTag != null)
            .Select(c => c.CvTag!.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var retainTags = TagCatalog.RegisterTags
            .Where(t => t.Name.StartsWith("VREG_RETAIN", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var cvTag = retainTags.FirstOrDefault(t => !usedCvNames.Contains(t.Name))
            ?? TagCatalog.RegisterTags.FirstOrDefault(t => !usedCvNames.Contains(t.Name))
            ?? TagCatalog.RegisterTags.FirstOrDefault()
            ?? TagCatalog.AllTags.FirstOrDefault(t => t.Name == "VREG_RETAIN0");

        return new CounterNodeViewModel(mode, inTag: tag, cvTag: cvTag, qTag: null);
    }

    private List<GraphNodeViewModel>? GetAlignmentTargetNodes(out GraphNodeViewModel? connectedNeighbor)
    {
        connectedNeighbor = null;
        var targetNodes = SelectedNodes.Count > 1 
            ? SelectedNodes.ToList() 
            : (Nodes.Count(n => n.IsSelected) > 1 ? Nodes.Where(n => n.IsSelected).ToList() : null);

        if (targetNodes == null || targetNodes.Count < 2)
        {
            if (SelectedNode != null)
            {
                var connected = Connections
                    .Where(c => c.Source.Node == SelectedNode || c.Target.Node == SelectedNode)
                    .Select(c => c.Source.Node == SelectedNode ? c.Target.Node : c.Source.Node)
                    .OfType<GraphNodeViewModel>()
                    .FirstOrDefault();

                if (connected != null)
                {
                    connectedNeighbor = connected;
                    return new List<GraphNodeViewModel> { SelectedNode };
                }
            }

            if (Nodes.Count > 1)
            {
                return Nodes.ToList();
            }

            return null;
        }

        return targetNodes;
    }

    [RelayCommand]
    public void AlignTop()
    {
        var targetNodes = GetAlignmentTargetNodes(out var connected);
        if (targetNodes == null || targetNodes.Count == 0) return;

        RecordSnapshot("Align Top");

        if (connected != null && targetNodes.Count == 1)
        {
            targetNodes[0].Location = new Point(targetNodes[0].Location.X, connected.Location.Y);
            CompileAndSaveRules(true);
            return;
        }

        double minY = Math.Round(targetNodes.Min(n => n.Location.Y) / 10.0) * 10.0;
        foreach (var node in targetNodes)
        {
            node.Location = new Point(Math.Round(node.Location.X / 10.0) * 10.0, minY);
        }
        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void AlignMiddle()
    {
        var targetNodes = GetAlignmentTargetNodes(out var connected);
        if (targetNodes == null || targetNodes.Count == 0) return;

        RecordSnapshot("Align Middle");

        if (connected != null && targetNodes.Count == 1)
        {
            targetNodes[0].Location = new Point(targetNodes[0].Location.X, connected.Location.Y);
            CompileAndSaveRules(true);
            return;
        }

        double avgY = Math.Round(targetNodes.Average(n => n.Location.Y) / 10.0) * 10.0;
        foreach (var node in targetNodes)
        {
            node.Location = new Point(Math.Round(node.Location.X / 10.0) * 10.0, avgY);
        }
        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void AlignBottom()
    {
        var targetNodes = GetAlignmentTargetNodes(out var connected);
        if (targetNodes == null || targetNodes.Count == 0) return;

        RecordSnapshot("Align Bottom");

        if (connected != null && targetNodes.Count == 1)
        {
            targetNodes[0].Location = new Point(targetNodes[0].Location.X, connected.Location.Y);
            CompileAndSaveRules(true);
            return;
        }

        double maxY = Math.Round(targetNodes.Max(n => n.Location.Y) / 10.0) * 10.0;
        foreach (var node in targetNodes)
        {
            node.Location = new Point(Math.Round(node.Location.X / 10.0) * 10.0, maxY);
        }
        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void AlignLeft()
    {
        var targetNodes = GetAlignmentTargetNodes(out var connected);
        if (targetNodes == null || targetNodes.Count == 0) return;

        RecordSnapshot("Align Left");

        if (connected != null && targetNodes.Count == 1)
        {
            targetNodes[0].Location = new Point(connected.Location.X, targetNodes[0].Location.Y);
            CompileAndSaveRules(true);
            return;
        }

        double minX = Math.Round(targetNodes.Min(n => n.Location.X) / 10.0) * 10.0;
        foreach (var node in targetNodes)
        {
            node.Location = new Point(minX, Math.Round(node.Location.Y / 10.0) * 10.0);
        }
        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void AlignCenter()
    {
        var targetNodes = GetAlignmentTargetNodes(out var connected);
        if (targetNodes == null || targetNodes.Count == 0) return;

        RecordSnapshot("Align Center");

        if (connected != null && targetNodes.Count == 1)
        {
            targetNodes[0].Location = new Point(connected.Location.X, targetNodes[0].Location.Y);
            CompileAndSaveRules(true);
            return;
        }

        double avgX = Math.Round(targetNodes.Average(n => n.Location.X) / 10.0) * 10.0;
        foreach (var node in targetNodes)
        {
            node.Location = new Point(avgX, Math.Round(node.Location.Y / 10.0) * 10.0);
        }
        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void AlignRight()
    {
        var targetNodes = GetAlignmentTargetNodes(out var connected);
        if (targetNodes == null || targetNodes.Count == 0) return;

        RecordSnapshot("Align Right");

        if (connected != null && targetNodes.Count == 1)
        {
            targetNodes[0].Location = new Point(connected.Location.X, targetNodes[0].Location.Y);
            CompileAndSaveRules(true);
            return;
        }

        double maxX = Math.Round(targetNodes.Max(n => n.Location.X) / 10.0) * 10.0;
        foreach (var node in targetNodes)
        {
            node.Location = new Point(maxX, Math.Round(node.Location.Y / 10.0) * 10.0);
        }
        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void AlignSelectedNodesHorizontally()
    {
        RecordSnapshot("Align Horizontal");
        var targetNodes = SelectedNodes.Count > 1 
            ? SelectedNodes.ToList() 
            : (Nodes.Count(n => n.IsSelected) > 1 ? Nodes.Where(n => n.IsSelected).ToList() : null);

        if (targetNodes == null || targetNodes.Count < 2)
        {
            if (SelectedNode != null)
            {
                // Nếu chỉ chọn 1 khối, tìm khối mà nó đang kết nối để căn hàng ngang với khối đó!
                var connected = Connections
                    .Where(c => c.Source.Node == SelectedNode || c.Target.Node == SelectedNode)
                    .Select(c => c.Source.Node == SelectedNode ? c.Target.Node : c.Source.Node)
                    .OfType<GraphNodeViewModel>()
                    .FirstOrDefault();

                if (connected != null)
                {
                    SelectedNode.Location = new Point(SelectedNode.Location.X, connected.Location.Y);
                    CompileAndSaveRules(true);
                    return;
                }
            }

            if (Nodes.Count > 1)
            {
                targetNodes = Nodes.ToList();
            }
            else
            {
                return;
            }
        }

        // Sử dụng tọa độ Y của khối được chọn đầu tiên hoặc khối mốc làm chuẩn
        double targetY = targetNodes[0].Location.Y;
        targetY = Math.Round(targetY / 10.0) * 10.0;

        foreach (var node in targetNodes)
        {
            node.Location = new Point(Math.Round(node.Location.X / 10.0) * 10.0, targetY);
        }

        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void AlignSelectedNodesVertically()
    {
        RecordSnapshot("Align Vertical");
        var targetNodes = SelectedNodes.Count > 1 
            ? SelectedNodes.ToList() 
            : (Nodes.Count(n => n.IsSelected) > 1 ? Nodes.Where(n => n.IsSelected).ToList() : null);

        if (targetNodes == null || targetNodes.Count < 2)
        {
            if (SelectedNode != null)
            {
                // Nếu chỉ chọn 1 khối, tìm khối mà nó đang kết nối để căn hàng dọc với khối đó
                var connected = Connections
                    .Where(c => c.Source.Node == SelectedNode || c.Target.Node == SelectedNode)
                    .Select(c => c.Source.Node == SelectedNode ? c.Target.Node : c.Source.Node)
                    .OfType<GraphNodeViewModel>()
                    .FirstOrDefault();

                if (connected != null)
                {
                    SelectedNode.Location = new Point(connected.Location.X, SelectedNode.Location.Y);
                    CompileAndSaveRules(true);
                    return;
                }
            }

            if (Nodes.Count > 1)
            {
                targetNodes = Nodes.ToList();
            }
            else
            {
                return;
            }
        }

        // Sử dụng tọa độ X của khối được chọn đầu tiên hoặc khối mốc làm chuẩn
        double targetX = targetNodes[0].Location.X;
        targetX = Math.Round(targetX / 10.0) * 10.0;

        foreach (var node in targetNodes)
        {
            node.Location = new Point(targetX, Math.Round(node.Location.Y / 10.0) * 10.0);
        }

        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void DistributeNodesHorizontally()
    {
        RecordSnapshot("Distribute Horizontal");
        var targetNodes = SelectedNodes.Count > 1 
            ? SelectedNodes.OrderBy(n => n.Location.X).ToList() 
            : (Nodes.Count(n => n.IsSelected) > 1 
                ? Nodes.Where(n => n.IsSelected).OrderBy(n => n.Location.X).ToList() 
                : null);

        if (targetNodes == null || targetNodes.Count < 3)
        {
            // Trường hợp 1: Nếu chỉ chọn 1 khối ở giữa hai khối kết nối
            if (SelectedNode != null)
            {
                var connectedNeighbors = Connections
                    .Where(c => c.Source.Node == SelectedNode || c.Target.Node == SelectedNode)
                    .Select(c => c.Source.Node == SelectedNode ? c.Target.Node : c.Source.Node)
                    .OfType<GraphNodeViewModel>()
                    .Distinct()
                    .OrderBy(n => n.Location.X)
                    .ToList();

                if (connectedNeighbors.Count >= 2)
                {
                    double midX = Math.Round(((connectedNeighbors.First().Location.X + connectedNeighbors.Last().Location.X) / 2.0) / 10.0) * 10.0;
                    SelectedNode.Location = new Point(midX, SelectedNode.Location.Y);
                    CompileAndSaveRules(true);
                    return;
                }
            }

            // Trường hợp 2: Phân bố toàn bộ các khối trên canvas nếu có từ 3 khối trở lên
            if (Nodes.Count >= 3)
            {
                targetNodes = Nodes.OrderBy(n => n.Location.X).ToList();
            }
            else
            {
                return;
            }
        }

        double minX = targetNodes[0].Location.X;
        double maxX = targetNodes[^1].Location.X;
        double step = (maxX - minX) / (targetNodes.Count - 1);

        for (int i = 1; i < targetNodes.Count - 1; i++)
        {
            targetNodes[i].Location = new Point(
                Math.Round((minX + i * step) / 10.0) * 10.0,
                targetNodes[i].Location.Y
            );
        }

        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void DistributeNodesVertically()
    {
        RecordSnapshot("Distribute Vertical");
        var targetNodes = SelectedNodes.Count > 1 
            ? SelectedNodes.OrderBy(n => n.Location.Y).ToList() 
            : (Nodes.Count(n => n.IsSelected) > 1 
                ? Nodes.Where(n => n.IsSelected).OrderBy(n => n.Location.Y).ToList() 
                : null);

        if (targetNodes == null || targetNodes.Count < 3)
        {
            if (SelectedNode != null)
            {
                var connectedNeighbors = Connections
                    .Where(c => c.Source.Node == SelectedNode || c.Target.Node == SelectedNode)
                    .Select(c => c.Source.Node == SelectedNode ? c.Target.Node : c.Source.Node)
                    .OfType<GraphNodeViewModel>()
                    .Distinct()
                    .OrderBy(n => n.Location.Y)
                    .ToList();

                if (connectedNeighbors.Count >= 2)
                {
                    double midY = Math.Round(((connectedNeighbors.First().Location.Y + connectedNeighbors.Last().Location.Y) / 2.0) / 10.0) * 10.0;
                    SelectedNode.Location = new Point(SelectedNode.Location.X, midY);
                    CompileAndSaveRules(true);
                    return;
                }
            }

            if (Nodes.Count >= 3)
            {
                targetNodes = Nodes.OrderBy(n => n.Location.Y).ToList();
            }
            else
            {
                return;
            }
        }

        double minY = targetNodes[0].Location.Y;
        double maxY = targetNodes[^1].Location.Y;
        double step = (maxY - minY) / (targetNodes.Count - 1);

        for (int i = 1; i < targetNodes.Count - 1; i++)
        {
            targetNodes[i].Location = new Point(
                targetNodes[i].Location.X,
                Math.Round((minY + i * step) / 10.0) * 10.0
            );
        }

        CompileAndSaveRules(true);
    }

    public void ApplyMagneticAlignment(GraphNodeViewModel node)
    {
        // 1. Chỉ tìm các khối có dây nối trực tiếp với khối này
        var connectedNeighbors = Connections
            .Where(c => c.Source.Node == node || c.Target.Node == node)
            .Select(c => c.Source.Node == node ? c.Target.Node : c.Source.Node)
            .OfType<GraphNodeViewModel>()
            .Distinct()
            .ToList();

        bool snappedY = false;
        foreach (var other in connectedNeighbors)
        {
            // Bắt dính nam châm thông minh: chỉ khi độ lệch trục Y với khối nối trực tiếp <= 10px thì mới hít thẳng hàng ngang
            if (Math.Abs(node.Location.Y - other.Location.Y) <= 10.0)
            {
                node.Location = new Point(
                    Math.Round(node.Location.X),
                    other.Location.Y
                );
                snappedY = true;
                break;
            }
        }

        if (!snappedY)
        {
            // Giữ nguyên đúng vị trí người dùng đặt, làm tròn pixel nguyên để hiển thị sắc nét, không bị nhảy
            node.Location = new Point(
                Math.Round(node.Location.X),
                Math.Round(node.Location.Y)
            );
        }

        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void SnapAllNodesToGrid()
    {
        RecordSnapshot("Snap To Grid");
        var targetNodes = SelectedNodes.Count > 0 
            ? SelectedNodes.ToList() 
            : Nodes.ToList();

        foreach (var node in targetNodes)
        {
            node.Location = new Point(
                Math.Round(node.Location.X / 10.0) * 10.0,
                Math.Round(node.Location.Y / 10.0) * 10.0
            );
        }

        CompileAndSaveRules(true);
    }

    [RelayCommand]
    public void DeleteSelectedNode()
    {
        // 1. Thu thập tất cả các khối đang được chọn (quét chọn trên canvas, cờ IsSelected, SelectedNodes, hoặc SelectedNode)
        var nodesToDelete = Nodes.Where(n => n.IsSelected).ToList();
        foreach (var node in SelectedNodes)
        {
            if (!nodesToDelete.Contains(node))
                nodesToDelete.Add(node);
        }
        if (nodesToDelete.Count == 0 && SelectedNode != null)
        {
            nodesToDelete.Add(SelectedNode);
        }

        // 2. Thu thập tất cả các dây đang được chọn trực tiếp
        var selectedConns = Connections.Where(c => c.IsSelected).ToList();

        if (nodesToDelete.Count == 0 && selectedConns.Count == 0)
            return;

        RecordSnapshot("Delete");

        // 3. Xóa các dây nối được chọn trực tiếp
        foreach (var conn in selectedConns)
        {
            Connections.Remove(conn);
            conn.Source.IsConnected = Connections.Any(c => c.Source == conn.Source || c.Target == conn.Source);
            conn.Target.IsConnected = Connections.Any(c => c.Source == conn.Target || c.Target == conn.Target);
        }

        // 4. Nếu có khối bị xóa, tìm tất cả các dây kết nối liên quan đến các khối đó
        if (nodesToDelete.Count > 0)
        {
            var toRemove = Connections
                .Where(c => nodesToDelete.Contains(c.Source.Node) || nodesToDelete.Contains(c.Target.Node))
                .ToList();

            foreach (var conn in toRemove)
            {
                Connections.Remove(conn);
            }

            foreach (var conn in toRemove)
            {
                if (!nodesToDelete.Contains(conn.Source.Node))
                {
                    conn.Source.IsConnected = Connections.Any(c => c.Source == conn.Source || c.Target == conn.Source);
                }
                if (!nodesToDelete.Contains(conn.Target.Node))
                {
                    conn.Target.IsConnected = Connections.Any(c => c.Source == conn.Target || c.Target == conn.Target);
                }
            }

            foreach (var node in nodesToDelete)
            {
                node.IsSelected = false;
                SelectedNodes.Remove(node);
                Nodes.Remove(node);
            }

            SelectedNode = SelectedNodes.LastOrDefault() ?? Nodes.LastOrDefault();
        }

        CompileAndSaveRules(true);
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void DeleteConnection(ConnectionViewModel? conn = null)
    {
        var toRemove = conn != null ? new List<ConnectionViewModel> { conn } : Connections.Where(c => c.IsSelected).ToList();
        if (toRemove.Count == 0) return;

        RecordSnapshot("Delete Connection");
        foreach (var c in toRemove)
        {
            Connections.Remove(c);
            c.Source.IsConnected = Connections.Any(x => x.Source == c.Source || x.Target == c.Source);
            c.Target.IsConnected = Connections.Any(x => x.Source == c.Target || x.Target == c.Target);
        }
        CompileAndSaveRules(true);
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void ClearCanvas()
    {
        if (Nodes.Count == 0 && Connections.Count == 0) return;
        RecordSnapshot("Clear Canvas");

        Connections.Clear();
        SelectedNodes.Clear();
        Nodes.Clear();
        SelectedNode = null;
        EditingRuleId = null;
        CompileAndSaveRules(true);
        OnPropertyChanged(nameof(HasSelection));
    }

    public static ProjectNodeData ExportSingleNodeData(GraphNodeViewModel node)
    {
        var pNode = new ProjectNodeData
        {
            Id = node.Id,
            LocationX = node.Location.X,
            LocationY = node.Location.Y,
            CustomLabel = node.CustomLabel
        };

        if (node is InputNodeViewModel inp)
        {
            pNode.Type = "Input";
            pNode.TagName = inp.Tag?.Name ?? string.Empty;
        }
        else if (node is TriggerNodeViewModel trig)
        {
            pNode.Type = "Trigger";
            pNode.TriggerType = trig.TriggerType;
            pNode.ForMs = trig.ForMs;
            pNode.CompareOp = trig.CompareOp;
            pNode.ThresholdLo = trig.ThresholdLo;
            pNode.ThresholdHi = trig.ThresholdHi;
        }
        else if (node is GuardNodeViewModel grd)
        {
            pNode.Type = "Guard";
            pNode.Negate = grd.Negate;
            pNode.TargetTagName = grd.GuardTag?.Name ?? string.Empty;
        }
        else if (node is ActionNodeViewModel act)
        {
            pNode.Type = "Action";
            pNode.ActionType = act.ActionType;
            pNode.TargetTagName = act.TargetTag?.Name ?? string.Empty;
            pNode.ActionParam = act.ActionParam;
        }
        else if (node is TimerNodeViewModel tm)
        {
            pNode.Type = "Timer";
            pNode.TimerMode = tm.TimerMode;
            pNode.PresetMs = tm.PresetMs;
            pNode.TagName = tm.InputTag?.Name ?? string.Empty;
            pNode.OutputTagName = tm.OutputTag?.Name ?? string.Empty;
        }
        else if (node is CounterNodeViewModel cnt)
        {
            pNode.Type = "Counter";
            pNode.CounterMode = cnt.CounterMode;
            pNode.PresetValue = cnt.PresetValue;
            pNode.TagName = cnt.InputTag?.Name ?? string.Empty;
            pNode.CvTagName = cnt.CvTag?.Name ?? string.Empty;
            pNode.OutputTagName = cnt.OutputTag?.Name ?? string.Empty;
            pNode.ResetTagName = cnt.ResetTag?.Name ?? string.Empty;
        }
        else if (node is ScaleNodeViewModel scl)
        {
            pNode.Type = "Scale";
            pNode.Gain = scl.Gain;
            pNode.Offset = scl.Offset;
            pNode.Unit = scl.Unit;
            pNode.DecimalPlaces = scl.DecimalPlaces;
            pNode.IsClamped = scl.IsClamped;
            pNode.ClampMin = scl.ClampMin;
            pNode.ClampMax = scl.ClampMax;
            pNode.TagName = scl.InputTag?.Name ?? string.Empty;
            pNode.OutputTagName = scl.OutputTag?.Name ?? string.Empty;
        }

        return pNode;
    }

    public GraphNodeViewModel? CreateNodeFromData(ProjectNodeData pNode)
    {
        GraphNodeViewModel? node = null;
        if (pNode.Type == "Input")
        {
            var tag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase))
                      ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteInput);
            node = new InputNodeViewModel(tag);
        }
        else if (pNode.Type == "Trigger")
        {
            node = new TriggerNodeViewModel
            {
                TriggerType = pNode.TriggerType,
                ForMs = pNode.ForMs,
                CompareOp = pNode.CompareOp,
                ThresholdLo = pNode.ThresholdLo,
                ThresholdHi = pNode.ThresholdHi
            };
        }
        else if (pNode.Type == "Guard")
        {
            var tag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TargetTagName, StringComparison.OrdinalIgnoreCase))
                      ?? TagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0");
            node = new GuardNodeViewModel(tag)
            {
                Negate = pNode.Negate
            };
        }
        else if (pNode.Type == "Action")
        {
            var tag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TargetTagName, StringComparison.OrdinalIgnoreCase))
                      ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput);
            node = new ActionNodeViewModel(tag)
            {
                ActionType = pNode.ActionType,
                ActionParam = pNode.ActionParam
            };
        }
        else if (pNode.Type == "Timer")
        {
            var inTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase));
            var qTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.OutputTagName, StringComparison.OrdinalIgnoreCase))
                       ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput || t.Kind == TagKind.VirtualFlag);
            node = new TimerNodeViewModel(
                mode: string.IsNullOrWhiteSpace(pNode.TimerMode) ? "TON" : pNode.TimerMode,
                inTag: inTag,
                qTag: qTag,
                presetMs: pNode.PresetMs > 0 ? pNode.PresetMs : 3000);
        }
        else if (pNode.Type == "Counter")
        {
            var inTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase));
            var cvTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.CvTagName, StringComparison.OrdinalIgnoreCase))
                        ?? TagCatalog.RegisterTags.FirstOrDefault()
                        ?? TagCatalog.AllTags.FirstOrDefault(t => t.Name == "VREG_RETAIN0");
            var qTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.OutputTagName, StringComparison.OrdinalIgnoreCase))
                       ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput || t.Kind == TagKind.VirtualFlag);
            var resetTag = string.IsNullOrWhiteSpace(pNode.ResetTagName)
                ? null
                : TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.ResetTagName, StringComparison.OrdinalIgnoreCase));

            node = new CounterNodeViewModel(
                mode: string.IsNullOrWhiteSpace(pNode.CounterMode) ? "CTU" : pNode.CounterMode,
                inTag: inTag,
                cvTag: cvTag,
                qTag: qTag,
                resetTag: resetTag,
                presetValue: pNode.PresetValue > 0 ? pNode.PresetValue : 10);
        }
        else if (pNode.Type == "Scale")
        {
            var inTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase))
                       ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput);
            var outTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.OutputTagName, StringComparison.OrdinalIgnoreCase))
                        ?? TagCatalog.RegisterTags.FirstOrDefault();
            node = new ScaleNodeViewModel(inTag, outTag)
            {
                Gain = pNode.Gain,
                Offset = pNode.Offset,
                Unit = string.IsNullOrWhiteSpace(pNode.Unit) ? "bar" : pNode.Unit,
                DecimalPlaces = pNode.DecimalPlaces,
                IsClamped = pNode.IsClamped,
                ClampMin = pNode.ClampMin,
                ClampMax = pNode.ClampMax
            };
        }

        if (node != null)
        {
            node.Id = pNode.Id;
            node.Location = new Point(pNode.LocationX, pNode.LocationY);
            if (!string.IsNullOrEmpty(pNode.CustomLabel))
            {
                node.CustomLabel = pNode.CustomLabel;
            }
        }

        return node;
    }

    public (List<ProjectNodeData> Nodes, List<ProjectConnectionData> Connections) ExportGraphData()
    {
        var nodesData = new List<ProjectNodeData>();
        foreach (var node in Nodes.Where(n => !n.IsGhost))
        {
            nodesData.Add(ExportSingleNodeData(node));
        }

        var connsData = new List<ProjectConnectionData>();
        foreach (var conn in Connections.Where(c => !c.IsGhost))
        {
            if (conn.Source.Node != null && conn.Target.Node != null)
            {
                int srcIdx = conn.Source.Node.OutputConnectors.IndexOf(conn.Source);
                int tgtIdx = conn.Target.Node.InputConnectors.IndexOf(conn.Target);
                connsData.Add(new ProjectConnectionData
                {
                    SourceNodeId = conn.Source.Node.Id,
                    SourceConnectorIndex = srcIdx >= 0 ? srcIdx : 0,
                    SourceConnectorTitle = conn.Source.Title,
                    TargetNodeId = conn.Target.Node.Id,
                    TargetConnectorIndex = tgtIdx >= 0 ? tgtIdx : 0,
                    TargetConnectorTitle = conn.Target.Title
                });
            }
        }

        return (nodesData, connsData);
    }

    public void LoadGraphData(IEnumerable<ProjectNodeData> nodesData, IEnumerable<ProjectConnectionData> connectionsData)
    {
        Connections.Clear();
        Nodes.Clear();
        SelectedNode = null;

        var nodeMap = new Dictionary<string, GraphNodeViewModel>();

        foreach (var pNode in nodesData)
        {
            var node = CreateNodeFromData(pNode);
            if (node != null)
            {
                Nodes.Add(node);
                nodeMap[node.Id] = node;
            }
        }

        foreach (var pConn in connectionsData)
        {
            if (nodeMap.TryGetValue(pConn.SourceNodeId, out var srcNode) &&
                nodeMap.TryGetValue(pConn.TargetNodeId, out var tgtNode))
            {
                ConnectorViewModel? srcConn = null;
                if (pConn.SourceConnectorIndex >= 0 && pConn.SourceConnectorIndex < srcNode.OutputConnectors.Count)
                {
                    srcConn = srcNode.OutputConnectors[pConn.SourceConnectorIndex];
                }
                else
                {
                    srcConn = srcNode.OutputConnectors.FirstOrDefault(c => c.Title == pConn.SourceConnectorTitle) ?? srcNode.OutputConnectors.FirstOrDefault();
                }

                ConnectorViewModel? tgtConn = null;
                if (pConn.TargetConnectorIndex >= 0 && pConn.TargetConnectorIndex < tgtNode.InputConnectors.Count)
                {
                    tgtConn = tgtNode.InputConnectors[pConn.TargetConnectorIndex];
                }
                else
                {
                    tgtConn = tgtNode.InputConnectors.FirstOrDefault(c => c.Title == pConn.TargetConnectorTitle) ?? tgtNode.InputConnectors.FirstOrDefault();
                }

                if (srcConn != null && tgtConn != null)
                {
                    Connect(srcConn, tgtConn);
                }
            }
        }

        CompileAndSaveRules(true);
    }

    /// <summary>
    /// Nạp sơ đồ khối của một Rule từ Bảng Rule lên Canvas để người dùng xem và chỉnh sửa.
    /// </summary>
    public void LoadRuleToCanvas(RuleItemModel rule)
    {
        if (rule == null) return;

        CurrentDiagramId = rule.DiagramId ?? rule.Id;
        rule.DiagramId = CurrentDiagramId;

        var siblingRules = _ruleTable?.Rules.Where(r => r.DiagramId == CurrentDiagramId).ToList();
        if (siblingRules != null && siblingRules.Count > 1)
        {
            EditingRuleId = string.Join(", ", siblingRules.Select(r => r.Id));
        }
        else
        {
            EditingRuleId = rule.Id;
        }

        if (rule.SourceNodes != null && rule.SourceNodes.Count > 0 &&
            rule.SourceConnections != null && rule.SourceConnections.Count > 0)
        {
            LoadGraphData(rule.SourceNodes, rule.SourceConnections);
        }
        else
        {
            ReconstructGraphFromRule(rule);
        }

        _navigateToTab?.Invoke(0);
    }

    /// <summary>
    /// Tự động tái tạo các khối Input -> Trigger -> Guard -> Action trên Canvas
    /// đối với các rule chưa có sẵn dữ liệu đồ thị (ví dụ import từ tệp binary hoặc tạo từ mẫu).
    /// </summary>
    public void ReconstructGraphFromRule(RuleItemModel rule)
    {
        if (rule == null) return;

        var nodesData = new List<ProjectNodeData>();
        var connsData = new List<ProjectConnectionData>();

        double x = 60;
        double y = 100;

        // 1. Khối Input cho Trigger
        string inputId = "reconstruct_in_trg";
        if (rule.TriggerTag != null)
        {
            nodesData.Add(new ProjectNodeData
            {
                Id = inputId,
                Type = "Input",
                TagName = rule.TriggerTag.Name,
                LocationX = x,
                LocationY = y
            });
        }

        // 2. Khối Trigger (hỗ trợ cả so sánh ngưỡng cảm biến Analog/Register)
        string triggerId = "reconstruct_trg";
        nodesData.Add(new ProjectNodeData
        {
            Id = triggerId,
            Type = "Trigger",
            TriggerType = rule.TriggerType,
            ForMs = rule.ForMs,
            CompareOp = rule.CompareOp,
            ThresholdLo = rule.ThresholdLo,
            ThresholdHi = rule.ThresholdHi,
            LocationX = x + 240,
            LocationY = y
        });

        if (rule.TriggerTag != null)
        {
            connsData.Add(new ProjectConnectionData
            {
                SourceNodeId = inputId,
                SourceConnectorIndex = 0,
                TargetNodeId = triggerId,
                TargetConnectorIndex = 0
            });
        }

        string lastNodeId = triggerId;
        int lastNodeOutIdx = 0;

        // 3. Khối Guard (Khóa liên động an toàn Boolean: DI0..7, VFLAG0..15)
        bool hasValidGuard = rule.GuardTag != null && 
                             rule.GuardTag.Kind != TagKind.None && 
                             !string.Equals(rule.GuardTag.Name, "NONE", StringComparison.OrdinalIgnoreCase);

        if (hasValidGuard && rule.GuardTag != null)
        {
            string guardId = "reconstruct_grd";
            nodesData.Add(new ProjectNodeData
            {
                Id = guardId,
                Type = "Guard",
                TargetTagName = rule.GuardTag.Name,
                Negate = rule.GuardNegated,
                LocationX = x + 480,
                LocationY = y
            });

            // Nối luồng kích hoạt từ Trigger sang chân In của Guard
            connsData.Add(new ProjectConnectionData
            {
                SourceNodeId = lastNodeId,
                SourceConnectorIndex = lastNodeOutIdx,
                TargetNodeId = guardId,
                TargetConnectorIndex = 0
            });

            lastNodeId = guardId;
            lastNodeOutIdx = 0;
        }

        // 4. Khối Action
        if (rule.ActionTag != null && rule.ActionTag.Kind != TagKind.None)
        {
            string actionId = "reconstruct_act";
            double actionX = hasValidGuard ? (x + 720) : (x + 480);
            nodesData.Add(new ProjectNodeData
            {
                Id = actionId,
                Type = "Action",
                ActionType = rule.ActionType,
                TargetTagName = rule.ActionTag.Name,
                ActionParam = rule.ActionParam,
                LocationX = actionX,
                LocationY = y
            });

            connsData.Add(new ProjectConnectionData
            {
                SourceNodeId = lastNodeId,
                SourceConnectorIndex = lastNodeOutIdx,
                TargetNodeId = actionId,
                TargetConnectorIndex = 0
            });
        }

        LoadGraphData(nodesData, connsData);
    }

    /// <summary>
    /// Nạp đề xuất thay đổi đồ thị của AI lên Canvas dưới dạng Ghost Elements (Layer 4 & 5).
    /// Hoàn toàn không xóa sơ đồ thật hiện có của người dùng.
    /// </summary>
    public void ApplyAiProposalToCanvas(DraftGraphTransaction tx, string summaryText = "")
    {
        _currentAiTransaction = tx;
        AiProposalSummaryText = string.IsNullOrWhiteSpace(summaryText)
            ? $"Đề xuất AI: +{tx.AddedNodes.Count} khối, +{tx.AddedWires.Count} dây"
            : summaryText;

        // 1. Tạo các node Ghost
        var nodeMap = new Dictionary<string, GraphNodeViewModel>();
        foreach (var dn in tx.AddedNodes)
        {
            var pNode = new ProjectNodeData
            {
                Id = dn.Id,
                Type = dn.NodeType,
                CustomLabel = dn.Label,
                LocationX = dn.PositionX,
                LocationY = dn.PositionY,
                TagName = dn.TagName,
                TriggerType = Enum.TryParse<TriggerType>(dn.TriggerType, true, out var tt) ? tt : TriggerType.ON_RISE,
                CompareOp = Enum.TryParse<CompareOp>(dn.CompareOp, true, out var cmp) ? cmp : CompareOp.NONE,
                ThresholdLo = dn.ThresholdLo,
                ThresholdHi = dn.ThresholdHi,
                ForMs = (uint)dn.DebounceMs,
                ActionType = Enum.TryParse<ActionType>(dn.ActionType, true, out var at) ? at : ActionType.SET_TAG,
                ActionParam = dn.ActionParam,
                TimerMode = string.IsNullOrEmpty(dn.Mode) ? "TON" : dn.Mode,
                PresetMs = (uint)dn.PresetValue,
                CounterMode = string.IsNullOrEmpty(dn.Mode) ? "CTU" : dn.Mode,
                PresetValue = dn.PresetValue,
                CvTagName = dn.CvTagName,
                ResetTagName = dn.ResetTagName,
                OutputTagName = dn.OutputTagName
            };

            var node = CreateNodeFromData(pNode);
            if (node != null)
            {
                node.IsGhost = true;
                node.DiffStatus = DiffState.Added;
                Nodes.Add(node);
                nodeMap[node.Id] = node;
            }
        }

        // 2. Tạo các wire Ghost
        foreach (var dw in tx.AddedWires)
        {
            var srcNode = Nodes.FirstOrDefault(n => n.Id == dw.SourceNodeId);
            var tgtNode = Nodes.FirstOrDefault(n => n.Id == dw.TargetNodeId);
            if (srcNode != null && tgtNode != null)
            {
                var srcPort = srcNode.OutputConnectors.FirstOrDefault(c => c.Title.Equals(dw.SourcePort, StringComparison.OrdinalIgnoreCase))
                              ?? srcNode.OutputConnectors.FirstOrDefault();
                var tgtPort = tgtNode.InputConnectors.FirstOrDefault(c => c.Title.Equals(dw.TargetPort, StringComparison.OrdinalIgnoreCase))
                              ?? tgtNode.InputConnectors.FirstOrDefault();

                if (srcPort != null && tgtPort != null)
                {
                    var conn = new ConnectionViewModel(srcPort, tgtPort)
                    {
                        IsGhost = true,
                        DiffStatus = DiffState.Added
                    };
                    Connections.Add(conn);
                }
            }
        }

        HasPendingAiProposal = true;
    }

    [RelayCommand]
    public void AcceptAiProposal()
    {
        if (!HasPendingAiProposal) return;

        // Lưu snapshot để người dùng có thể Ctrl+Z hoàn tác bất kỳ lúc nào
        RecordSnapshot(AiProposalSummaryText);

        // Biến các phần tử Ghost thành phần tử chính thức
        foreach (var node in Nodes.Where(n => n.IsGhost))
        {
            node.IsGhost = false;
            node.DiffStatus = DiffState.None;
        }

        foreach (var conn in Connections.Where(c => c.IsGhost))
        {
            conn.IsGhost = false;
            conn.DiffStatus = DiffState.None;
        }

        HasPendingAiProposal = false;
        _currentAiTransaction = null;

        CompileAndSaveRules(true);
        NotifyGraphModified();
    }

    [RelayCommand]
    public void RejectAiProposal()
    {
        if (!HasPendingAiProposal) return;

        // Thu hồi toàn bộ các node và wire Ghost
        var ghostConns = Connections.Where(c => c.IsGhost).ToList();
        foreach (var c in ghostConns)
        {
            c.Source.IsConnected = false;
            c.Target.IsConnected = false;
            Connections.Remove(c);
        }

        var ghostNodes = Nodes.Where(n => n.IsGhost).ToList();
        foreach (var n in ghostNodes)
        {
            Nodes.Remove(n);
        }

        HasPendingAiProposal = false;
        _currentAiTransaction = null;
    }

    public GraphSnapshot CaptureSnapshot(string description = "")
    {
        var (nodesData, connsData) = ExportGraphData();
        var selectedIds = SelectedNodes.Select(n => n.Id).ToList();
        return new GraphSnapshot(nodesData, connsData, selectedIds, SelectedNode?.Id, description);
    }

    public void PushSnapshot(GraphSnapshot snapshot)
    {
        History.PushSnapshot(snapshot);
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    public void RecordSnapshot(string description = "")
    {
        PushSnapshot(CaptureSnapshot(description));
    }

    public void RestoreSnapshot(GraphSnapshot snapshot)
    {
        LoadGraphData(snapshot.Nodes, snapshot.Connections);

        DeselectAllNodes();
        foreach (var id in snapshot.SelectedNodeIds)
        {
            var node = Nodes.FirstOrDefault(n => n.Id == id);
            if (node != null)
            {
                node.IsSelected = true;
            }
        }

        if (snapshot.ActiveNodeId != null)
        {
            SelectedNode = Nodes.FirstOrDefault(n => n.Id == snapshot.ActiveNodeId);
        }
        if (SelectedNode == null)
        {
            SelectedNode = SelectedNodes.LastOrDefault() ?? Nodes.LastOrDefault();
        }

        NotifyGraphModified();
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void Undo()
    {
        if (!CanUndo) return;
        var current = CaptureSnapshot("Before Undo");
        var previous = History.Undo(current);
        if (previous != null)
        {
            RestoreSnapshot(previous);
        }
    }

    [RelayCommand]
    public void Redo()
    {
        if (!CanRedo) return;
        var current = CaptureSnapshot("Before Redo");
        var next = History.Redo(current);
        if (next != null)
        {
            RestoreSnapshot(next);
        }
    }

    [RelayCommand]
    public void Copy()
    {
        var targets = Nodes.Where(n => n.IsSelected).ToList();
        if (targets.Count == 0 && SelectedNode != null)
        {
            targets.Add(SelectedNode);
        }
        if (targets.Count == 0) return;

        var nodesData = new List<ProjectNodeData>();
        foreach (var node in targets)
        {
            nodesData.Add(ExportSingleNodeData(node));
        }

        var connsData = new List<ProjectConnectionData>();
        foreach (var conn in Connections)
        {
            if (conn.Source.Node != null && conn.Target.Node != null &&
                targets.Contains(conn.Source.Node) && targets.Contains(conn.Target.Node))
            {
                int srcIdx = conn.Source.Node.OutputConnectors.IndexOf(conn.Source);
                int tgtIdx = conn.Target.Node.InputConnectors.IndexOf(conn.Target);
                connsData.Add(new ProjectConnectionData
                {
                    SourceNodeId = conn.Source.Node.Id,
                    SourceConnectorIndex = srcIdx >= 0 ? srcIdx : 0,
                    SourceConnectorTitle = conn.Source.Title,
                    TargetNodeId = conn.Target.Node.Id,
                    TargetConnectorIndex = tgtIdx >= 0 ? tgtIdx : 0,
                    TargetConnectorTitle = conn.Target.Title
                });
            }
        }

        _inMemoryClipboard = new GraphClipboardData(nodesData, connsData);
        _pasteCount = 1;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_inMemoryClipboard);
            System.Windows.Clipboard.SetText(json);
        }
        catch { }

        OnPropertyChanged(nameof(CanPaste));
    }

    [RelayCommand]
    public void Cut()
    {
        Copy();
        DeleteSelectedNode();
    }

    [RelayCommand]
    public void Paste()
    {
        GraphClipboardData? clip = _inMemoryClipboard;

        if (clip == null || clip.Nodes.Count == 0)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string text = System.Windows.Clipboard.GetText();
                    if (!string.IsNullOrWhiteSpace(text) && text.Contains("LocationX"))
                    {
                        clip = System.Text.Json.JsonSerializer.Deserialize<GraphClipboardData>(text);
                    }
                }
            }
            catch { }
        }

        if (clip == null || clip.Nodes.Count == 0) return;

        RecordSnapshot("Paste");

        DeselectAllNodes();

        double offset = 30.0 * _pasteCount;
        _pasteCount++;

        var idMap = new Dictionary<string, string>();
        var newNodes = new List<GraphNodeViewModel>();

        foreach (var pNode in clip.Nodes)
        {
            string newId = Guid.NewGuid().ToString("N")[..8];
            idMap[pNode.Id] = newId;

            var copyPNode = new ProjectNodeData
            {
                Id = newId,
                Type = pNode.Type,
                LocationX = pNode.LocationX + offset,
                LocationY = pNode.LocationY + offset,
                CustomLabel = pNode.CustomLabel,
                TagName = pNode.TagName,
                TriggerType = pNode.TriggerType,
                ForMs = pNode.ForMs,
                CompareOp = pNode.CompareOp,
                ThresholdLo = pNode.ThresholdLo,
                ThresholdHi = pNode.ThresholdHi,
                Negate = pNode.Negate,
                TargetTagName = pNode.TargetTagName,
                ActionType = pNode.ActionType,
                ActionParam = pNode.ActionParam
            };

            var node = CreateNodeFromData(copyPNode);
            if (node != null)
            {
                Nodes.Add(node);
                node.IsSelected = true;
                newNodes.Add(node);
            }
        }

        foreach (var pConn in clip.Connections)
        {
            if (idMap.TryGetValue(pConn.SourceNodeId, out string? newSrcId) &&
                idMap.TryGetValue(pConn.TargetNodeId, out string? newTgtId))
            {
                var srcNode = newNodes.FirstOrDefault(n => n.Id == newSrcId);
                var tgtNode = newNodes.FirstOrDefault(n => n.Id == newTgtId);
                if (srcNode != null && tgtNode != null)
                {
                    var srcConnector = (pConn.SourceConnectorIndex >= 0 && pConn.SourceConnectorIndex < srcNode.OutputConnectors.Count)
                        ? srcNode.OutputConnectors[pConn.SourceConnectorIndex]
                        : (srcNode.OutputConnectors.FirstOrDefault(c => c.Title == pConn.SourceConnectorTitle) ?? srcNode.OutputConnectors.FirstOrDefault());

                    var tgtConnector = (pConn.TargetConnectorIndex >= 0 && pConn.TargetConnectorIndex < tgtNode.InputConnectors.Count)
                        ? tgtNode.InputConnectors[pConn.TargetConnectorIndex]
                        : (tgtNode.InputConnectors.FirstOrDefault(c => c.Title == pConn.TargetConnectorTitle) ?? tgtNode.InputConnectors.FirstOrDefault());

                    if (srcConnector != null && tgtConnector != null)
                    {
                        Connect(srcConnector, tgtConnector);
                    }
                }
            }
        }

        SelectedNode = newNodes.LastOrDefault();
        NotifyGraphModified();
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void Duplicate()
    {
        Copy();
        Paste();
    }

    [RelayCommand]
    public void SelectAll()
    {
        DeselectAllNodes();
        foreach (var node in Nodes)
        {
            node.IsSelected = true;
        }
        SelectedNode = Nodes.LastOrDefault();
        OnPropertyChanged(nameof(HasSelection));
    }

    [RelayCommand]
    public void NewRuleCanvas()
    {
        ClearCanvas();
        CurrentDiagramId = Guid.NewGuid().ToString("N");
        EditingRuleId = null;
    }

    [RelayCommand]
    public void CompileNow()
    {
        _compileDebounceCts?.Cancel();
        _compileDebounceCts?.Dispose();
        _compileDebounceCts = null;
        CompileAndSaveRules(isAutoSync: false);
    }

    [RelayCommand]
    public void SaveAndCompile()
    {
        CompileNow();
    }

    [RelayCommand]
    public void SaveAsNewRule()
    {
        CurrentDiagramId = Guid.NewGuid().ToString("N");
        EditingRuleId = null;
        CompileNow();
    }

    [RelayCommand]
    public void ViewRuleTable()
    {
        _navigateToTab?.Invoke(2);
    }

    [RelayCommand]
    public void DeployNow()
    {
        _navigateToTab?.Invoke(4);
    }

    public void NotifyGraphModified()
    {
        if (_isInitializing) return;

        CompileState = CompileState.Stale;
        CurrentProgram = null;
        IsSaved = false;
        SaveStatusMessage = LocalizationService.Tr("SaveStatusStale");

        if (IsSimulationMode)
        {
            if (IsSimRunning)
            {
                IsSimRunning = false;
                _simTimer?.Stop();
            }
            SimGateWarning = LocalizationService.Instance.Get("SimBarGateWarning");
            ClearSimVisuals();
        }

        _compileDebounceCts?.Cancel();
        _compileDebounceCts?.Dispose();

        if (DebounceDelayMs <= 0)
        {
            _compileDebounceCts = null;
            CompileAndSaveRules(isAutoSync: true);
            return;
        }

        var cts = new CancellationTokenSource();
        _compileDebounceCts = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(DebounceDelayMs, token);
                if (!token.IsCancellationRequested)
                {
                    _uiDispatcher(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            CompileAndSaveRules(isAutoSync: true);
                        }
                    });
                }
            }
            catch (OperationCanceledException) { }
        });
    }

    public void CompileAndSaveRules(bool isAutoSync = false)
    {
        _compileDebounceCts?.Cancel();
        _compileDebounceCts?.Dispose();
        _compileDebounceCts = null;

        var activeNodes = Nodes.Where(n => !n.IsGhost).ToList();
        var activeConnections = Connections.Where(c => !c.IsGhost).ToList();

        // Xóa trạng thái lỗi chẩn đoán cũ trên toàn bộ Nodes trước khi biên dịch
        foreach (var node in Nodes)
        {
            node.ClearDiagnostics();
        }

        var result = _compiler.Compile(activeNodes, activeConnections, TagCatalog);

        if (isAutoSync)
        {
            // Trong chế độ tự động đồng bộ khi vẽ (Authoring / Editing mode theo chuẩn TIA Portal / CODESYS):
            // Nếu toàn bộ mạch đã hoàn chỉnh và hợp lệ: tự động cập nhật Program để sẵn sàng
            if (result.IsValid && result.Rules.Count > 0)
            {
                Diagnostics = Array.Empty<Diagnostic>();
                OnPropertyChanged(nameof(Diagnostics));
                CompileState = CompileState.Valid;
                CurrentProgram = result.Program;
                LastSuccessfulProgram = result.Program;
                ValidRuleCount = result.Rules.Count;
                SimGateWarning = string.Empty;
                IsSaved = true;
                SaveStatusMessage = string.Format(LocalizationService.Tr("SaveStatusValid"), result.Rules.Count);
                return;
            }

            // Nếu mạch đang vẽ dở (chưa nối xong chân, thiếu ngõ ra, hoặc chưa có rule):
            // Giữ trạng thái soạn thảo trung tính "Cần biên dịch" (Stale), KHÔNG báo lỗi đỏ, KHÔNG viền đỏ các khối
            Diagnostics = Array.Empty<Diagnostic>();
            OnPropertyChanged(nameof(Diagnostics));
            CompileState = activeNodes.Count > 0 ? CompileState.Stale : CompileState.NotCompiled;
            CurrentProgram = null;
            ValidRuleCount = 0;
            IsSaved = false;
            SaveStatusMessage = activeNodes.Count > 0
                ? LocalizationService.Tr("SaveStatusStale")
                : LocalizationService.Tr("SaveStatusNoRules");
            return;
        }

        // Chế độ Biên dịch chủ động (Explicit Compile: Bấm nút Lưu & Biên Dịch / Ctrl+S, hoặc khi chuyển sang Mô phỏng / Nạp PLC)
        Diagnostics = result.Diagnostics;
        OnPropertyChanged(nameof(Diagnostics));

        // Phân phối trạng thái lỗi chẩn đoán trực tiếp vào từng Node & Connector (In-Place Diagnostics)
        if (result.Diagnostics != null && result.Diagnostics.Count > 0)
        {
            foreach (var diag in result.Diagnostics)
            {
                if (!string.IsNullOrEmpty(diag.NodeId))
                {
                    var targetNode = Nodes.FirstOrDefault(n => n.Id == diag.NodeId);
                    if (targetNode != null)
                    {
                        if (diag.Severity == DiagnosticSeverity.Error)
                        {
                            targetNode.HasError = true;
                            targetNode.ErrorMessage = diag.Message;
                            targetNode.ErrorField = diag.FieldName ?? string.Empty;
                        }
                        else if (diag.Severity == DiagnosticSeverity.Warning && !targetNode.HasError)
                        {
                            targetNode.HasWarning = true;
                            targetNode.ErrorMessage = diag.Message;
                            targetNode.ErrorField = diag.FieldName ?? string.Empty;
                        }

                        // Nếu lỗi chỉ rõ cổng (Pin), đánh dấu lỗi trên Connector tương ứng
                        if (!string.IsNullOrEmpty(diag.FieldName))
                        {
                            var targetConnector = targetNode.InputConnectors.FirstOrDefault(c =>
                                string.Equals(c.Title, diag.FieldName, StringComparison.OrdinalIgnoreCase))
                                ?? targetNode.OutputConnectors.FirstOrDefault(c =>
                                string.Equals(c.Title, diag.FieldName, StringComparison.OrdinalIgnoreCase));

                            if (targetConnector != null)
                            {
                                targetConnector.HasError = true;
                                targetConnector.ErrorMessage = diag.Message;
                            }
                        }
                    }
                }
            }
        }

        int errCount = result.Diagnostics?.Count(d => d.Severity == DiagnosticSeverity.Error) ?? 0;
        if (!result.IsValid)
        {
            CompileState = CompileState.Invalid;
            CurrentProgram = null;
            ValidRuleCount = 0;
            IsSaved = false;
            SaveStatusMessage = LocalizationService.Instance.IsVietnamese
                ? $"⚠ {errCount} lỗi biên dịch"
                : $"⚠ {errCount} compile errors";

            return;
        }

        if (result.Rules.Count == 0)
        {
            CompileState = CompileState.NotCompiled;
            CurrentProgram = null;
            ValidRuleCount = 0;
            IsSaved = false;
            SaveStatusMessage = LocalizationService.Tr("SaveStatusNoRules");
            return;
        }

        CompileState = CompileState.Valid;
        CurrentProgram = result.Program;
        LastSuccessfulProgram = result.Program;
        ValidRuleCount = result.Rules.Count;
        SimGateWarning = string.Empty;

        // Chỉ thêm/cập nhật vào Bảng Rule khi người dùng chủ động bấm Compile / Save & Compile
        if (!isAutoSync && _ruleTable != null && result.Program != null)
        {
            var (nodesData, connsData) = ExportGraphData();
            var updated = _ruleTable.AddOrUpdateCompiledRules(result.Program.Rules, nodesData, connsData, EditingRuleId, CurrentDiagramId);
            if (updated.Count > 0)
            {
                EditingRuleId = updated.Count == 1
                    ? updated[0].Id
                    : string.Join(", ", updated.Select(r => r.Id));
            }
        }

        IsSaved = true;
        SaveStatusMessage = string.Format(LocalizationService.Tr("SaveStatusValid"), result.Rules.Count);

        if (!isAutoSync && result.Rules.Count > 0)
        {
            SaveToastMessage = LocalizationService.Instance.IsVietnamese
                ? $"Đã lưu kịch bản vào Bảng Rule ({EditingRuleId ?? "R1"})"
                : $"Saved rule into Rule Table ({EditingRuleId ?? "R1"})";
            ShowSaveToast = true;

            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2500, token);
                    if (!token.IsCancellationRequested)
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            ShowSaveToast = false;
                        });
                    }
                }
                catch (TaskCanceledException) { }
            });
        }
    }

    #region Canvas-Centric Simulation

    [RelayCommand]
    public void StartSimulation()
    {
        if (CompileState != CompileState.Valid || CurrentProgram == null || CurrentProgram.RuleCount == 0)
        {
            CompileAndSaveRules(isAutoSync: false);
        }

        if (CompileState != CompileState.Valid || CurrentProgram == null || CurrentProgram.RuleCount == 0)
        {
            SimGateWarning = LocalizationService.Instance.Get("SimBarGateWarning");
            return;
        }

        SimGateWarning = string.Empty;
        IsSimulationMode = true;
        IsSimRunning = false; // MANUAL-FIRST: Mặc định ở trạng thái Chờ / Pause
        _simEngine = new RuntimeEngine(TagCatalog.AllTags.ToList());
        _simClock.Reset();
        _simTimer?.Stop();
        _simTimer = null;
        SimScanHistory.Clear();
        _lastScanTagValues.Clear();
        foreach (var tag in TagCatalog.AllTags)
        {
            _lastScanTagValues[tag.Index] = tag.Value;
        }
        SimScanNumber = 0;
        SimElapsedMs = 0;
        SimStatusText = string.Format(
            LocalizationService.Instance.Get("SimBarCycleInfo"),
            0,
            0.0,
            SelectedSimSpeed?.IntervalMs ?? 100);
        ClearSimVisuals();
    }

    [RelayCommand]
    public void ToggleSimPlayPause()
    {
        if (!IsSimulationMode)
        {
            StartSimulation();
            if (IsSimulationMode && string.IsNullOrEmpty(SimGateWarning))
            {
                IsSimRunning = true;
                int intervalMs = SelectedSimSpeed?.IntervalMs ?? 50;
                if (_simTimer == null)
                {
                    _simTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
                    _simTimer.Tick += OnSimScanCycle;
                }
                else
                {
                    _simTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
                }
                _simTimer.Start();
            }
            return;
        }

        if (IsSimRunning)
        {
            IsSimRunning = false;
            _simTimer?.Stop();
        }
        else
        {
            if (CompileState != CompileState.Valid || CurrentProgram == null || CurrentProgram.RuleCount == 0)
            {
                CompileAndSaveRules(isAutoSync: false);
            }

            if (CompileState != CompileState.Valid || CurrentProgram == null || CurrentProgram.RuleCount == 0)
            {
                SimGateWarning = LocalizationService.Instance.Get("SimBarGateWarning");
                return;
            }

            SimGateWarning = string.Empty;
            IsSimRunning = true;
            int intervalMs = SelectedSimSpeed?.IntervalMs ?? 100;
            if (_simTimer == null)
            {
                _simTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
                _simTimer.Tick += OnSimScanCycle;
            }
            else
            {
                _simTimer.Interval = TimeSpan.FromMilliseconds(intervalMs);
            }
            _simTimer.Start();
        }
    }

    [RelayCommand]
    public void StopSimulation()
    {
        _simTimer?.Stop();
        _simTimer = null;
        IsSimRunning = false;
        IsSimulationMode = false;
        SimGateWarning = string.Empty;
        ClearSimVisuals();

        // Đồng bộ lại trạng thái sống từ Device/StateStore nếu đang kết nối Online
        if (_stateStore?.CurrentSnapshot != null)
        {
            ApplySnapshotDiff(_stateStore.CurrentSnapshot);
        }
    }

    [RelayCommand]
    public void StepSimScan()
    {
        if (IsSimRunning)
        {
            IsSimRunning = false;
            _simTimer?.Stop();
        }

        if (!IsSimulationMode)
        {
            if (CompileState != CompileState.Valid || CurrentProgram == null || CurrentProgram.RuleCount == 0)
            {
                CompileAndSaveRules(isAutoSync: false);
            }

            if (CompileState != CompileState.Valid || CurrentProgram == null || CurrentProgram.RuleCount == 0)
            {
                SimGateWarning = LocalizationService.Instance.Get("SimBarGateWarning");
                return;
            }
            IsSimulationMode = true;
            _simEngine = new RuntimeEngine(TagCatalog.AllTags.ToList());
            _simClock.Reset();
            SimScanHistory.Clear();
            _lastScanTagValues.Clear();
            foreach (var tag in TagCatalog.AllTags)
            {
                _lastScanTagValues[tag.Index] = tag.Value;
            }
            ClearSimVisuals();
        }

        if (CompileState != CompileState.Valid || CurrentProgram == null)
        {
            CompileAndSaveRules(isAutoSync: false);
        }

        if (CompileState != CompileState.Valid || CurrentProgram == null)
        {
            SimGateWarning = LocalizationService.Instance.Get("SimBarGateWarning");
            return;
        }

        SimGateWarning = string.Empty;
        int stepMs = SelectedSimSpeed?.IntervalMs ?? 100;
        _simClock.Advance(stepMs);
        RunSimScan(stepMs, animate: true);
    }

    [RelayCommand]
    public void ResetSim()
    {
        if (IsSimRunning)
        {
            IsSimRunning = false;
            _simTimer?.Stop();
        }
        _simAnimationCts?.Cancel();
        _simAnimationCts?.Dispose();
        _simAnimationCts = null;
        _simClock.Reset();
        _simEngine?.Reset();
        SimScanNumber = 0;
        SimElapsedMs = 0;
        SimScanHistory.Clear();
        SelectedScanItem = null;
        _lastScanTagValues.Clear();
        foreach (var tag in TagCatalog.AllTags)
        {
            if (tag.Kind is TagKind.DiscreteOutput or TagKind.VirtualFlag or TagKind.VirtualRegister or TagKind.Counter)
            {
                tag.Value = 0;
            }
            _lastScanTagValues[tag.Index] = tag.Value;
        }
        ClearSimVisuals();
        SimStatusText = string.Format(
            LocalizationService.Instance.Get("SimBarCycleInfo"),
            0,
            0.0,
            SelectedSimSpeed?.IntervalMs ?? 100);
    }

    [RelayCommand]
    public void ToggleSimTag(TagModel? tag)
    {
        if (tag == null) return;
        tag.Value = tag.Value == 0 ? 1 : 0;
        foreach (var node in Nodes.OfType<InputNodeViewModel>())
        {
            if (node.Tag?.Index == tag.Index)
            {
                node.IsLiveActive = tag.Value != 0;
                node.IsSimActive = tag.Value != 0;
                foreach (var conn in Connections.Where(c => c.Source?.Node == node))
                {
                    conn.IsActive = node.IsSimActive;
                }
            }
        }
        foreach (var node in Nodes.OfType<GuardNodeViewModel>())
        {
            if (node.GuardTag?.Index == tag.Index)
            {
                node.IsSimPassed = tag.Value != 0;
            }
        }
    }

    [RelayCommand]
    public void ToggleInputNode(InputNodeViewModel? inp)
    {
        if (inp?.Tag != null)
        {
            inp.ToggleSimInput();
            inp.IsSimActive = inp.Tag.Value != 0;

            // Cập nhật ngay lập tức trạng thái dây dẫn xuất phát từ Input node này để người dùng thấy phản hồi trực quan
            foreach (var conn in Connections.Where(c => c.Source?.Node == inp))
            {
                conn.IsActive = inp.IsSimActive;
            }
        }
    }

    public void AdvanceSimClock(long ms) => _simClock.Advance(ms);

    private void OnSimScanCycle(object? sender, EventArgs e)
    {
        int stepMs = SelectedSimSpeed?.IntervalMs ?? 100;
        _simClock.Advance(stepMs);
        bool animate = stepMs >= 500;
        RunSimScan(stepMs, animate: animate);
    }

    public void RunSimScan(int stepMs = 100, bool? animate = null)
    {
        if (CurrentProgram == null) return;
        _simEngine ??= new RuntimeEngine(TagCatalog.AllTags.ToList());

        SimElapsedMs = _simClock.NowMs;
        var snapshot = _simEngine.Scan(CurrentProgram, _simClock.NowMs);
        SimScanNumber = snapshot.ScanNumber;
        SimStatusText = string.Format(
            LocalizationService.Instance.Get("SimBarCycleInfo"),
            SimScanNumber,
            SimElapsedMs / 1000.0,
            stepMs);

        RecordScanHistory(snapshot);

        bool shouldAnimate = animate ?? (SimPropagationDelayMs > 0);

        if (shouldAnimate && SimPropagationDelayMs > 0)
        {
            int stageDelay = stepMs switch
            {
                >= 1000 => Math.Max(SimPropagationDelayMs, 200),
                >= 500 => Math.Min(SimPropagationDelayMs, 120),
                _ => SimPropagationDelayMs
            };
            LastAnimationTask = AnimateScanPropagationAsync(snapshot, stageDelay);
        }
        else
        {
            UpdateSimVisuals(snapshot);
            LastAnimationTask = Task.CompletedTask;
        }
    }

    private void RecordScanHistory(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var eval in snapshot.Evaluations)
        {
            var rule = CurrentProgram.Rules.FirstOrDefault(r => r.RuleIndex == eval.RuleIndex);
            if (rule == null) continue;

            int trigTagIndex = rule.Trigger.Tag.TagIndex;
            string trigTagName = rule.Trigger.Tag.Name;
            int currVal = TagCatalog.AllTags.FirstOrDefault(t => t.Index == trigTagIndex)?.Value ?? 0;
            int prevVal = _lastScanTagValues.TryGetValue(trigTagIndex, out var p) ? p : currVal;

            string inputSummary = prevVal != currVal
                ? $"{trigTagName}: {prevVal} → {currVal}"
                : $"{trigTagName}: {currVal} ({LocalizationService.Tr("SimScanStatic")})";

            string trigRes;
            string trigReason;
            bool trigPassed = eval.Status is RuleEvaluationStatus.Pass or RuleEvaluationStatus.BlockedByGuard or RuleEvaluationStatus.WaitingDwell;

            bool hasComparison = rule.Trigger.CompareOp != SimplePLC.Domain.Enums.CompareOperator.None;
            bool isBetween = rule.Trigger.CompareOp == SimplePLC.Domain.Enums.CompareOperator.Between;
            string compSymbol = rule.Trigger.CompareOp switch
            {
                SimplePLC.Domain.Enums.CompareOperator.Equal => "==",
                SimplePLC.Domain.Enums.CompareOperator.NotEqual => "!=",
                SimplePLC.Domain.Enums.CompareOperator.GreaterThan => ">",
                SimplePLC.Domain.Enums.CompareOperator.LessThan => "<",
                SimplePLC.Domain.Enums.CompareOperator.GreaterThanOrEqual => ">=",
                SimplePLC.Domain.Enums.CompareOperator.LessThanOrEqual => "<=",
                SimplePLC.Domain.Enums.CompareOperator.Between => $"[{rule.Trigger.ThresholdLo} .. {rule.Trigger.ThresholdHi}]",
                _ => string.Empty
            };

            if (trigPassed)
            {
                trigRes = LocalizationService.Tr("SimScanTrigTrue");
                if (hasComparison)
                {
                    trigReason = isBetween
                        ? string.Format(LocalizationService.Tr("SimScanInRange"), trigTagName, currVal, compSymbol)
                        : $"{trigTagName}: {currVal} {compSymbol} {rule.Trigger.ThresholdLo}";
                }
                else
                {
                    trigReason = rule.Trigger.Type switch
                    {
                        SimplePLC.Domain.Enums.TriggerKind.OnRise => LocalizationService.Tr("SimScanRising"),
                        SimplePLC.Domain.Enums.TriggerKind.OnFall => LocalizationService.Tr("SimScanFalling"),
                        SimplePLC.Domain.Enums.TriggerKind.OnChange => string.Format(LocalizationService.Tr("SimScanValueChange"), prevVal, currVal),
                        _ => LocalizationService.Tr("SimScanCondMet")
                    };
                }
            }
            else
            {
                trigRes = LocalizationService.Tr("SimScanTrigFalse");
                if (hasComparison)
                {
                    trigReason = isBetween
                        ? string.Format(LocalizationService.Tr("SimScanOutRange"), trigTagName, currVal, compSymbol)
                        : $"{trigTagName}: {currVal} not {compSymbol} {rule.Trigger.ThresholdLo}";
                }
                else
                {
                    trigReason = rule.Trigger.Type switch
                    {
                        SimplePLC.Domain.Enums.TriggerKind.OnRise => currVal == 0
                            ? LocalizationService.Tr("SimScanWaitRise")
                            : LocalizationService.Tr("SimScanNoRiseEdge"),
                        SimplePLC.Domain.Enums.TriggerKind.OnFall => currVal != 0
                            ? LocalizationService.Tr("SimScanWaitFall")
                            : LocalizationService.Tr("SimScanNoFallEdge"),
                        SimplePLC.Domain.Enums.TriggerKind.OnChange => LocalizationService.Tr("SimScanValueUnchanged"),
                        _ => LocalizationService.Tr("SimScanCondNotMet")
                    };
                }
            }

            string guardStatus = "—";
            string guardReason = "—";
            bool guardPassed = eval.Status == RuleEvaluationStatus.Pass;

            if (rule.Guard.HasGuard && rule.Guard.Tag != null)
            {
                if (eval.Status == RuleEvaluationStatus.BlockedByGuard)
                {
                    guardStatus = LocalizationService.Tr("SimScanGuardBlocked");
                    guardReason = eval.GuardSummary ?? LocalizationService.Tr("SimScanGuardBlockedBy");
                }
                else if (eval.Status == RuleEvaluationStatus.Pass)
                {
                    guardStatus = LocalizationService.Tr("SimScanGuardPass");
                    guardReason = eval.GuardSummary ?? LocalizationService.Tr("SimScanGuardValid");
                }
                else
                {
                    guardStatus = "—";
                    guardReason = LocalizationService.Tr("SimScanGuardSkipped");
                }
            }

            string actionStatus;
            string outputDelta;
            bool actionFired = eval.Status == RuleEvaluationStatus.Pass;

            if (actionFired)
            {
                actionStatus = LocalizationService.Tr("SimScanActionFired");
                if (eval.ActionDelta != null)
                {
                    outputDelta = eval.ActionDelta.BeforeValue == eval.ActionDelta.AfterValue
                        ? $"{eval.ActionDelta.TargetTagName} = {eval.ActionDelta.AfterValue} ({LocalizationService.Tr("SimScanActionHolding")})"
                        : $"{eval.ActionDelta.TargetTagName}: {eval.ActionDelta.BeforeValue} → {eval.ActionDelta.AfterValue}";
                }
                else
                {
                    outputDelta = LocalizationService.Tr("SimScanActionExecuted");
                }
            }
            else if (eval.Status == RuleEvaluationStatus.BlockedByGuard)
            {
                actionStatus = LocalizationService.Tr("SimScanActionSkipped");
                outputDelta = LocalizationService.Tr("SimScanActionBlocked");
            }
            else if (eval.Status == RuleEvaluationStatus.WaitingDwell)
            {
                actionStatus = LocalizationService.Tr("SimScanActionWaiting");
                outputDelta = $"⏳ {eval.DwellElapsedMs}/{eval.DwellRequiredMs} ms";
            }
            else
            {
                actionStatus = LocalizationService.Tr("SimScanActionSkipped");
                outputDelta = "—";
            }

            string dwellText = eval.DwellRequiredMs > 0
                ? (eval.Status == RuleEvaluationStatus.WaitingDwell
                    ? $"⏳ {eval.DwellElapsedMs}/{eval.DwellRequiredMs} ms"
                    : LocalizationService.Tr("SimScanActionComplete"))
                : "—";

            var historyItem = new SimScanHistoryItem
            {
                ScanNumber = snapshot.ScanNumber,
                RuleIndex = eval.RuleIndex + 1,
                TimestampMs = snapshot.TickMs,
                InputChange = inputSummary,
                TriggerResult = trigRes,
                TriggerReason = trigReason,
                TriggerPassed = trigPassed,
                GuardStatus = guardStatus,
                GuardReason = guardReason,
                GuardPassed = guardPassed,
                ActionStatus = actionStatus,
                OutputDelta = outputDelta,
                ActionFired = actionFired,
                DwellStatus = dwellText,
                SummaryLine = eval.SummaryLine
            };

            SimScanHistory.Insert(0, historyItem);
            if (SimScanHistory.Count > 100)
            {
                SimScanHistory.RemoveAt(SimScanHistory.Count - 1);
            }

            if (SelectedScanItem == null || SelectedScanItem.ScanNumber == snapshot.ScanNumber - 1)
            {
                SelectedScanItem = historyItem;
            }
        }

        foreach (var tag in TagCatalog.AllTags)
        {
            _lastScanTagValues[tag.Index] = tag.Value;
        }
    }

    public void UpdateSimVisuals(RuntimeSnapshot snapshot)
    {
        ApplyPropagationStage1(snapshot);
        ApplyPropagationStage2(snapshot);
        ApplyPropagationStage3(snapshot);
        ApplyPropagationStage4(snapshot);
        ApplyTimerSimVisuals(snapshot);
        ApplyCounterSimVisuals(snapshot);
    }

    public async Task AnimateScanPropagationAsync(RuntimeSnapshot snapshot, int stageDelayMs = 200)
    {
        if (stageDelayMs <= 0)
        {
            UpdateSimVisuals(snapshot);
            return;
        }

        _simAnimationCts?.Cancel();
        _simAnimationCts?.Dispose();
        var cts = new CancellationTokenSource();
        _simAnimationCts = cts;
        var token = cts.Token;

        try
        {
            // Giai đoạn 1: Kích hoạt Input và dây dẫn số 1 (Input -> Trigger)
            _uiDispatcher(() => ApplyPropagationStage1(snapshot));
            if (token.IsCancellationRequested) return;

            await Task.Delay(stageDelayMs, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            // Giai đoạn 2: Đánh giá Trigger. Nếu đạt -> dây tiếp theo sáng
            _uiDispatcher(() => ApplyPropagationStage2(snapshot));
            if (token.IsCancellationRequested) return;

            await Task.Delay(stageDelayMs, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            // Giai đoạn 3: Đánh giá Guard (nếu có). Nếu cho phép -> dây sang Action sáng
            _uiDispatcher(() => ApplyPropagationStage3(snapshot));
            if (token.IsCancellationRequested) return;

            await Task.Delay(stageDelayMs, token).ConfigureAwait(false);
            if (token.IsCancellationRequested) return;

            // Giai đoạn 4: Action nhận tín hiệu và xuất lệnh
            _uiDispatcher(() => ApplyPropagationStage4(snapshot));
        }
        catch (OperationCanceledException)
        {
            // Hủy animation an toàn
        }
    }

    private void ApplyPropagationStage1(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var eval in snapshot.Evaluations)
        {
            var rule = CurrentProgram.Rules.FirstOrDefault(r => r.RuleIndex == eval.RuleIndex);
            if (rule == null || string.IsNullOrEmpty(rule.ActionNodeId)) continue;

            var actionNode = Nodes.OfType<ActionNodeViewModel>().FirstOrDefault(n => n.Id == rule.ActionNodeId);
            if (actionNode == null) continue;

            var actionConn = Connections.FirstOrDefault(c => c.Target?.Node == actionNode);
            if (actionConn == null) continue;

            GuardNodeViewModel? guardNode = null;
            ConnectionViewModel? guardConn = null;
            TriggerNodeViewModel? trigNode = null;
            ConnectionViewModel? trigConn = null;
            InputNodeViewModel? inpNode = null;

            if (actionConn.Source?.Node is GuardNodeViewModel grd)
            {
                guardNode = grd;
                guardConn = Connections.FirstOrDefault(c => c.Target?.Node == guardNode);
                trigNode = guardConn?.Source?.Node as TriggerNodeViewModel;
                if (trigNode != null)
                {
                    trigConn = Connections.FirstOrDefault(c => c.Target?.Node == trigNode);
                    inpNode = trigConn?.Source?.Node as InputNodeViewModel;
                }
            }
            else if (actionConn.Source?.Node is TriggerNodeViewModel trg)
            {
                trigNode = trg;
                trigConn = Connections.FirstOrDefault(c => c.Target?.Node == trigNode);
                inpNode = trigConn?.Source?.Node as InputNodeViewModel;
            }

            // Xóa downstream của chu kỳ trước để chuẩn bị đợt phát xung mới
            actionNode.IsSimActive = false;
            actionNode.IsSimFired = false;
            actionConn.IsActive = false;

            if (guardNode != null)
            {
                guardNode.IsSimActive = false;
                guardNode.IsSimPassed = false;
                guardNode.IsSimBlocked = false;
            }
            if (guardConn != null) guardConn.IsActive = false;

            if (trigNode != null)
            {
                trigNode.IsSimActive = false;
                trigNode.IsSimTriggered = false;
                trigNode.SimDiagText = string.Empty;
            }
            if (trigConn != null) trigConn.IsActive = false;

            // Kích hoạt Input và dây nối sang Trigger
            if (inpNode?.Tag != null)
            {
                bool inActive = inpNode.Tag.Value != 0;
                inpNode.IsSimActive = inActive;
                if (trigConn != null)
                {
                    trigConn.IsActive = inActive;
                }
            }
        }

        // Kích hoạt Input và dây nối trực tiếp sang Timer
        foreach (var timerNode in Nodes.OfType<TimerNodeViewModel>())
        {
            var timerConn = Connections.FirstOrDefault(c => c.Target?.Node == timerNode);
            if (timerConn?.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
            {
                bool inActive = inp.Tag.Value != 0;
                inp.IsSimActive = inActive;
                timerConn.IsActive = inActive;
            }
        }

        // Kích hoạt Input và dây nối trực tiếp sang Counter (CU, R)
        foreach (var counterNode in Nodes.OfType<CounterNodeViewModel>())
        {
            foreach (var counterConn in Connections.Where(c => c.Target?.Node == counterNode))
            {
                if (counterConn.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
                {
                    bool inActive = inp.Tag.Value != 0;
                    inp.IsSimActive = inActive;
                    counterConn.IsActive = inActive;
                }
            }
        }

        // Kích hoạt Input và dây nối trực tiếp sang Scale
        foreach (var scaleNode in Nodes.OfType<ScaleNodeViewModel>())
        {
            var scaleConn = Connections.FirstOrDefault(c => c.Target?.Node == scaleNode);
            if (scaleConn?.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
            {
                scaleNode.InputTag = inp.Tag;
                double scaled = scaleNode.Calculate(inp.Tag.Value);
                bool isActive = IsSimulationMode || IsSimRunning || Math.Abs(scaled) > 1e-6;
                scaleNode.IsSimActive = isActive;
                scaleNode.IsLiveActive = isActive;
                scaleConn.IsActive = inp.Tag.Value != 0;

                if (scaleNode.OutputTag != null)
                {
                    scaleNode.OutputTag.Value = (int)Math.Round(scaled);
                }

                foreach (var outConn in Connections.Where(c => c.Source?.Node == scaleNode))
                {
                    outConn.IsActive = isActive;
                    if (outConn.Target?.Node is ActionNodeViewModel act)
                    {
                        act.IsSimActive = isActive;
                        act.IsSimFired = isActive;
                        act.SimTargetLiveText = scaleNode.LiveFormattedValue;
                    }
                }
            }
        }
    }

    private void ApplyPropagationStage2(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var eval in snapshot.Evaluations)
        {
            var rule = CurrentProgram.Rules.FirstOrDefault(r => r.RuleIndex == eval.RuleIndex);
            if (rule == null || string.IsNullOrEmpty(rule.ActionNodeId)) continue;

            var actionNode = Nodes.OfType<ActionNodeViewModel>().FirstOrDefault(n => n.Id == rule.ActionNodeId);
            if (actionNode == null) continue;

            var actionConn = Connections.FirstOrDefault(c => c.Target?.Node == actionNode);
            if (actionConn == null) continue;

            bool triggerPassed = eval.Status is RuleEvaluationStatus.Pass or RuleEvaluationStatus.BlockedByGuard;
            bool isWaitingDwell = eval.Status == RuleEvaluationStatus.WaitingDwell;
            uint dwellRemaining = (uint)Math.Max(0, eval.DwellRequiredMs - eval.DwellElapsedMs);

            if (actionConn.Source?.Node is GuardNodeViewModel guardNode)
            {
                var guardConn = Connections.FirstOrDefault(c => c.Target?.Node == guardNode);
                if (guardConn?.Source?.Node is TriggerNodeViewModel trigNode)
                {
                    ApplyTriggerDiagnostics(trigNode, triggerPassed, isWaitingDwell, dwellRemaining);
                    guardConn.IsActive = triggerPassed;
                }
            }
            else if (actionConn.Source?.Node is TriggerNodeViewModel trigNode)
            {
                ApplyTriggerDiagnostics(trigNode, triggerPassed, isWaitingDwell, dwellRemaining);
                actionConn.IsActive = triggerPassed;
            }
        }
    }

    private void ApplyPropagationStage3(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var eval in snapshot.Evaluations)
        {
            var rule = CurrentProgram.Rules.FirstOrDefault(r => r.RuleIndex == eval.RuleIndex);
            if (rule == null || string.IsNullOrEmpty(rule.ActionNodeId)) continue;

            var actionNode = Nodes.OfType<ActionNodeViewModel>().FirstOrDefault(n => n.Id == rule.ActionNodeId);
            if (actionNode == null) continue;

            var actionConn = Connections.FirstOrDefault(c => c.Target?.Node == actionNode);
            if (actionConn == null) continue;

            bool guardPassed = eval.Status == RuleEvaluationStatus.Pass;
            bool isBlocked = eval.Status == RuleEvaluationStatus.BlockedByGuard;

            if (actionConn.Source?.Node is GuardNodeViewModel guardNode)
            {
                guardNode.IsSimPassed = guardPassed;
                guardNode.IsSimBlocked = isBlocked;
                guardNode.IsSimActive = guardPassed;
                actionConn.IsActive = guardPassed;
            }
        }
    }

    private void ApplyPropagationStage4(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var eval in snapshot.Evaluations)
        {
            var rule = CurrentProgram.Rules.FirstOrDefault(r => r.RuleIndex == eval.RuleIndex);
            if (rule == null || string.IsNullOrEmpty(rule.ActionNodeId)) continue;

            var actionNode = Nodes.OfType<ActionNodeViewModel>().FirstOrDefault(n => n.Id == rule.ActionNodeId);
            if (actionNode == null) continue;

            bool actionFired = eval.Status == RuleEvaluationStatus.Pass;
            actionNode.IsSimFired = actionFired;
            actionNode.IsSimActive = actionFired;
            if (actionNode.TargetTag != null)
            {
                actionNode.IsSimTargetOn = actionNode.TargetTag.Value != 0;
                actionNode.SimTargetLiveText = $"{actionNode.TargetTag.Name}: {actionNode.TargetTag.Value}";
            }
            else if (eval.ActionDelta != null)
            {
                actionNode.SimTargetLiveText = $"{eval.ActionDelta.TargetTagName}: {eval.ActionDelta.AfterValue}";
                actionNode.IsSimTargetOn = eval.ActionDelta.AfterValue != 0;
            }
        }

        ApplyTimerSimVisuals(snapshot);
        ApplyCounterSimVisuals(snapshot);
    }

    private void ApplyTimerSimVisuals(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var timerNode in Nodes.OfType<TimerNodeViewModel>())
        {
            // 1. Upstream connection highlight
            var timerConn = Connections.FirstOrDefault(c => c.Target?.Node == timerNode);
            if (timerConn?.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
            {
                timerConn.IsActive = inp.Tag.Value != 0;
            }

            // 2. Output tag state & downstream connection highlight (Q -> Action)
            bool qActive = false;
            if (timerNode.OutputTag != null)
            {
                var currentTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == timerNode.OutputTag.Index);
                if (currentTag != null)
                {
                    qActive = currentTag.Value != 0;
                    timerNode.IsLiveActive = qActive;
                }
            }

            foreach (var outConn in Connections.Where(c => c.Source?.Node == timerNode))
            {
                outConn.IsActive = qActive;
                if (outConn.Target?.Node is ActionNodeViewModel act)
                {
                    act.IsSimActive = qActive;
                    act.IsSimTargetOn = qActive;
                }
            }

            // 3. Derived ET & progress from macro rules
            bool foundTiming = false;
            foreach (var eval in snapshot.Evaluations)
            {
                if (CurrentProgram.SourceMap != null &&
                    CurrentProgram.SourceMap.TryGetValue(eval.RuleIndex, out var origin) &&
                    origin.SourceNodeId == timerNode.Id)
                {
                    if (eval.Status == RuleEvaluationStatus.WaitingDwell)
                    {
                        timerNode.IsTiming = true;
                        timerNode.ElapsedMs = (uint)eval.DwellElapsedMs;
                        timerNode.ProgressPercent = eval.DwellRequiredMs > 0
                            ? Math.Min(100.0, (double)eval.DwellElapsedMs / eval.DwellRequiredMs * 100.0)
                            : 0;
                        foundTiming = true;
                        break;
                    }
                }
            }

            if (!foundTiming)
            {
                timerNode.IsTiming = false;
                if (timerNode.IsLiveActive)
                {
                    timerNode.ElapsedMs = timerNode.PresetMs;
                    timerNode.ProgressPercent = 100.0;
                }
                else
                {
                    timerNode.ElapsedMs = 0;
                    timerNode.ProgressPercent = 0.0;
                }
            }
        }
    }

    private void ApplyCounterSimVisuals(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var counterNode in Nodes.OfType<CounterNodeViewModel>())
        {
            // 1. Upstream connections highlight (CU and R)
            foreach (var counterConn in Connections.Where(c => c.Target?.Node == counterNode))
            {
                if (counterConn.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
                {
                    counterConn.IsActive = inp.Tag.Value != 0;
                }
            }

            // 2. CV value state
            if (counterNode.CvTag != null)
            {
                var cvTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == counterNode.CvTag.Index);
                if (cvTag != null)
                {
                    counterNode.CurrentCount = cvTag.Value;
                }
            }

            // 3. Output tag state & downstream connection highlight (Q -> Action)
            bool qActive = false;
            if (counterNode.OutputTag != null)
            {
                var qTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == counterNode.OutputTag.Index);
                if (qTag != null)
                {
                    qActive = qTag.Value != 0;
                    counterNode.IsLiveActive = qActive;
                }
            }

            foreach (var outConn in Connections.Where(c => c.Source?.Node == counterNode))
            {
                outConn.IsActive = qActive;
                if (outConn.Target?.Node is ActionNodeViewModel act)
                {
                    act.IsSimActive = qActive;
                }
            }
        }
    }

    private void ApplyTriggerDiagnostics(TriggerNodeViewModel trigNode, bool triggerPassed, bool isWaitingDwell, uint dwellRemaining)
    {
        trigNode.IsSimEvaluated = true;
        trigNode.IsSimTriggered = triggerPassed;
        trigNode.IsSimWaitingDwell = isWaitingDwell;
        trigNode.SimDwellRemainingMs = dwellRemaining;
        trigNode.IsSimActive = triggerPassed;

        if (triggerPassed)
        {
            trigNode.SimDiagText = LocalizationService.Instance.Get("SimStatusTrue");
        }
        else if (isWaitingDwell)
        {
            trigNode.SimDiagText = string.Format("⏳ {0}ms", dwellRemaining);
        }
        else
        {
            var trigConn = Connections.FirstOrDefault(c => c.Target?.Node == trigNode);
            if (trigConn?.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
            {
                if (trigNode.HasComparison)
                {
                    trigNode.SimDiagText = string.Format(LocalizationService.Tr("SimDiagNotMet"), inp.Tag.Value);
                }
                else if (trigNode.TriggerType == TriggerType.ON_RISE)
                {
                    trigNode.SimDiagText = inp.Tag.Value == 0
                        ? LocalizationService.Tr("SimDiagWaitRise")
                        : LocalizationService.Tr("SimDiagNoEdge");
                }
                else if (trigNode.TriggerType == TriggerType.ON_FALL)
                {
                    trigNode.SimDiagText = inp.Tag.Value != 0
                        ? LocalizationService.Tr("SimDiagWaitFall")
                        : LocalizationService.Tr("SimDiagNoEdge");
                }
                else
                {
                    trigNode.SimDiagText = LocalizationService.Tr("SimDiagFalse");
                }
            }
            else
            {
                trigNode.SimDiagText = LocalizationService.Tr("SimDiagFalse");
            }
        }
    }

    public void ClearSimVisuals()
    {
        _simAnimationCts?.Cancel();
        _simAnimationCts?.Dispose();
        _simAnimationCts = null;

        foreach (var conn in Connections)
        {
            conn.IsActive = false;
        }
        foreach (var node in Nodes)
        {
            node.IsSimActive = false;
            if (node is TriggerNodeViewModel trig)
            {
                trig.IsSimTriggered = false;
                trig.IsSimWaitingDwell = false;
                trig.SimDwellRemainingMs = 0;
                trig.IsSimEvaluated = false;
                trig.SimDiagText = string.Empty;
            }
            else if (node is GuardNodeViewModel grd)
            {
                grd.IsSimPassed = false;
                grd.IsSimBlocked = false;
            }
            else if (node is ActionNodeViewModel act)
            {
                act.IsSimFired = false;
                act.IsSimTargetOn = false;
                act.SimTargetLiveText = string.Empty;
            }
            else if (node is TimerNodeViewModel tm)
            {
                tm.IsLiveActive = false;
                tm.IsTiming = false;
                tm.ElapsedMs = 0;
                tm.ProgressPercent = 0;
            }
            else if (node is CounterNodeViewModel cnt)
            {
                cnt.IsLiveActive = false;
                cnt.CurrentCount = 0;
            }
            else if (node is ScaleNodeViewModel scl)
            {
                scl.IsLiveActive = false;
                scl.LiveRawInput = 0;
                scl.Calculate(0);
            }
        }
    }

    #endregion
}
