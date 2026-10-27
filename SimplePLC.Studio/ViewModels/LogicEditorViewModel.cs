using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
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
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Studio.ViewModels;

public partial class LogicEditorViewModel : ObservableObject
{
    public TagCatalogViewModel TagCatalog { get; } = null!;

    public Func<SimplePLC.Studio.Models.ProjectMetadata>? GetProjectMetadata { get; set; }
    public Action? OpenDeviceModbusPortConfig { get; set; }
    public Action? OpenDeviceMqttBrokerConfig { get; set; }

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
    [NotifyPropertyChangedFor(nameof(ActiveGridCellSize))]
    private bool _isGridVisible = true;

    private bool _isSyncingSelection;

    /// <summary>
    /// Kích thước ô lưới căn chỉnh FBD Logic Editor (Phương án 1: 20px siêu mảnh khi bật, 1px vô hình khi tắt).
    /// </summary>
    public uint ActiveGridCellSize => IsGridVisible ? 20u : 1u;

    [RelayCommand]
    public void ToggleGrid()
    {
        IsGridVisible = !IsGridVisible;
    }

    [RelayCommand]
    public void OpenMqttConfig(MqttPublishNodeViewModel? node = null)
    {
        var targetNode = node ?? SelectedMqttNode;
        if (targetNode == null) return;

        var dialogVm = new MqttConfigDialogViewModel(targetNode, TagCatalog);
        dialogVm.OnOpenDeviceBrokerSettings = () => OpenDeviceMqttBrokerConfig?.Invoke();
        var dialog = new Views.MqttConfigDialogView(dialogVm);
        dialog.ShowDialog();
        NotifyGraphModified();
    }

    [RelayCommand]
    public void OpenMqttSubConfig(MqttSubscribeNodeViewModel? node = null)
    {
        var targetNode = node ?? SelectedMqttSubNode;
        if (targetNode == null) return;

        var dialogVm = new MqttSubConfigDialogViewModel(targetNode, TagCatalog);
        dialogVm.OnOpenDeviceBrokerSettings = () => OpenDeviceMqttBrokerConfig?.Invoke();
        var dialog = new Views.MqttSubConfigDialogView(dialogVm);
        dialog.ShowDialog();
        NotifyGraphModified();
    }

    [RelayCommand]
    public void OpenModbusReadConfig(ModbusReadNodeViewModel? node = null)
    {
        var targetNode = node ?? SelectedModbusReadNode;
        if (targetNode == null) return;

        var ports = GetProjectMetadata?.Invoke()?.ModbusPorts;
        var dialogVm = new ModbusReadConfigDialogViewModel(targetNode, TagCatalog, ports);
        dialogVm.OnOpenDevicePortSettings = () =>
        {
            OpenDeviceModbusPortConfig?.Invoke();
            var refreshedPorts = GetProjectMetadata?.Invoke()?.ModbusPorts;
            dialogVm.RefreshAvailablePorts(refreshedPorts);
        };
        var dialog = new Views.ModbusReadConfigDialogView(dialogVm);
        dialog.ShowDialog();
        NotifyGraphModified();
    }

    [RelayCommand]
    public void OpenModbusWriteConfig(ModbusWriteNodeViewModel? node = null)
    {
        var targetNode = node ?? SelectedModbusWriteNode;
        if (targetNode == null) return;

        var ports = GetProjectMetadata?.Invoke()?.ModbusPorts;
        var dialogVm = new ModbusWriteConfigDialogViewModel(targetNode, TagCatalog, ports);
        dialogVm.OnOpenDevicePortSettings = () =>
        {
            OpenDeviceModbusPortConfig?.Invoke();
            var refreshedPorts = GetProjectMetadata?.Invoke()?.ModbusPorts;
            dialogVm.RefreshAvailablePorts(refreshedPorts);
        };
        var dialog = new Views.ModbusWriteConfigDialogView(dialogVm);
        dialog.ShowDialog();
        NotifyGraphModified();
    }

    [RelayCommand]
    public void OpenDirectWriteConfig(DirectWriteNodeViewModel? node = null)
    {
        var targetNode = node ?? SelectedDirectWriteNode;
        if (targetNode == null) return;

        int availableDos = TagCatalog?.AllTags.Count(t => t.Kind == TagKind.DiscreteOutput)
            ?? AppServices.Instance.CurrentProduct?.Resources.DigitalOutputs
            ?? 8;

        var dialogVm = new DirectWriteConfigDialogViewModel(targetNode, availableDos);
        var dialog = new Views.DirectWriteConfigDialogView(dialogVm);
        dialog.ShowDialog();
        NotifyGraphModified();
    }

    public bool IsMqttPublishSupported => (TagCatalog?.CurrentProduct.SupportsMqttPublish ?? false) && (TagCatalog?.CurrentProduct.SupportsNetwork ?? false);
    public bool IsMqttSubscribeSupported => (TagCatalog?.CurrentProduct.SupportsMqttSubscribe ?? false) && (TagCatalog?.CurrentProduct.SupportsNetwork ?? false);
    public bool IsModbusMasterSupported => TagCatalog?.CurrentProduct.SupportsModbusMaster ?? false;
    public bool IsDtuGatewaySupported => (TagCatalog?.CurrentProduct.SupportsTransparentStream ?? false) || (TagCatalog?.CurrentProduct.SupportsModbusMaster ?? false) || (TagCatalog?.CurrentProduct.SupportsNetwork ?? false) || (TagCatalog?.CurrentProduct.IsDtuCapable ?? false);

    public string DtuGatewayDisabledTooltip => IsDtuGatewaySupported
        ? LocalizationService.Tr("ToolboxDtuGatewayDesc", "Bộ chuyển đổi dữ liệu RS-485 lên Internet (MQTT, HTTP REST, TCP)")
        : (LocalizationService.Instance.IsVietnamese
            ? "Yêu cầu thiết bị hỗ trợ cổng RS-485 hoặc cổng mạng."
            : "Requires hardware RS-485 port or network connectivity.");

    public string MqttPublishDisabledTooltip => IsMqttPublishSupported
        ? LocalizationService.Tr("ToolboxMqttDesc")
        : (LocalizationService.Instance.IsVietnamese
            ? "Yêu cầu thiết bị hỗ trợ cổng mạng (Ethernet / Wi-Fi) và tính năng MQTT Publish."
            : "Requires hardware network port (Ethernet / Wi-Fi) and MQTT Publish capability.");

    public string MqttSubscribeDisabledTooltip => IsMqttSubscribeSupported
        ? LocalizationService.Tr("ToolboxMqttSubDesc")
        : (LocalizationService.Instance.IsVietnamese
            ? "Yêu cầu thiết bị hỗ trợ cổng mạng (Ethernet / Wi-Fi) và tính năng MQTT Subscribe."
            : "Requires hardware network port (Ethernet / Wi-Fi) and MQTT Subscribe capability.");

    public string ModbusReadDisabledTooltip => IsModbusMasterSupported
        ? LocalizationService.Tr("ToolboxModbusReadDesc")
        : (LocalizationService.Instance.IsVietnamese
            ? "Yêu cầu thiết bị hỗ trợ tính năng Modbus Master Poller."
            : "Requires hardware Modbus Master Poller capability.");

    public string ModbusDisabledTooltip => ModbusReadDisabledTooltip;

    public string ModbusWriteDisabledTooltip => IsModbusMasterSupported
        ? LocalizationService.Tr("ToolboxModbusWriteDesc")
        : (LocalizationService.Instance.IsVietnamese
            ? "Yêu cầu thiết bị hỗ trợ tính năng Modbus Master Poller."
            : "Requires hardware Modbus Master Poller capability.");

    private void OnTagCatalogPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TagCatalogViewModel.CurrentProduct))
        {
            OnPropertyChanged(nameof(IsMqttPublishSupported));
            OnPropertyChanged(nameof(IsMqttSubscribeSupported));
            OnPropertyChanged(nameof(IsModbusMasterSupported));
            OnPropertyChanged(nameof(MqttPublishDisabledTooltip));
            OnPropertyChanged(nameof(MqttSubscribeDisabledTooltip));
            OnPropertyChanged(nameof(ModbusReadDisabledTooltip));
            OnPropertyChanged(nameof(ModbusWriteDisabledTooltip));
            OnPropertyChanged(nameof(IsDtuGatewaySupported));
            OnPropertyChanged(nameof(DtuGatewayDisabledTooltip));
        }
    }

    [ObservableProperty]
    private string _aiProposalSummaryText = string.Empty;

    private DraftGraphTransaction? _currentAiTransaction;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedInputNode))]
    [NotifyPropertyChangedFor(nameof(SelectedTriggerNode))]
    [NotifyPropertyChangedFor(nameof(SelectedComparatorNode))]
    [NotifyPropertyChangedFor(nameof(SelectedGuardNode))]
    [NotifyPropertyChangedFor(nameof(SelectedActionNode))]
    [NotifyPropertyChangedFor(nameof(SelectedTimerNode))]
    [NotifyPropertyChangedFor(nameof(SelectedCounterNode))]
    [NotifyPropertyChangedFor(nameof(SelectedScaleNode))]
    [NotifyPropertyChangedFor(nameof(SelectedMqttNode))]
    [NotifyPropertyChangedFor(nameof(SelectedMqttSubNode))]
    [NotifyPropertyChangedFor(nameof(SelectedModbusReadNode))]
    [NotifyPropertyChangedFor(nameof(SelectedModbusWriteNode))]
    [NotifyPropertyChangedFor(nameof(SelectedDirectWriteNode))]
    [NotifyPropertyChangedFor(nameof(SelectedDtuGatewayNode))]
    [NotifyPropertyChangedFor(nameof(NarrativePreview))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private GraphNodeViewModel? _selectedNode;

    public InputNodeViewModel? SelectedInputNode => SelectedNode as InputNodeViewModel;
    public TriggerNodeViewModel? SelectedTriggerNode => SelectedNode as TriggerNodeViewModel;
    public ComparatorNodeViewModel? SelectedComparatorNode => SelectedNode as ComparatorNodeViewModel;
    public GuardNodeViewModel? SelectedGuardNode => SelectedNode as GuardNodeViewModel;
    public ActionNodeViewModel? SelectedActionNode => SelectedNode as ActionNodeViewModel;
    public TimerNodeViewModel? SelectedTimerNode => SelectedNode as TimerNodeViewModel;
    public CounterNodeViewModel? SelectedCounterNode => SelectedNode as CounterNodeViewModel;
    public ScaleNodeViewModel? SelectedScaleNode => SelectedNode as ScaleNodeViewModel;
    public MqttPublishNodeViewModel? SelectedMqttNode => SelectedNode as MqttPublishNodeViewModel;
    public MqttSubscribeNodeViewModel? SelectedMqttSubNode => SelectedNode as MqttSubscribeNodeViewModel;
    public ModbusReadNodeViewModel? SelectedModbusReadNode => SelectedNode as ModbusReadNodeViewModel;
    public ModbusWriteNodeViewModel? SelectedModbusWriteNode => SelectedNode as ModbusWriteNodeViewModel;
    public DirectWriteNodeViewModel? SelectedDirectWriteNode => SelectedNode as DirectWriteNodeViewModel;
    public DtuGatewayNodeViewModel? SelectedDtuGatewayNode => SelectedNode as DtuGatewayNodeViewModel;

    [RelayCommand]
    public void OpenDtuGatewayConfig(DtuGatewayNodeViewModel? node = null)
    {
        var targetNode = node ?? SelectedDtuGatewayNode;
        var metadata = GetProjectMetadata?.Invoke() ?? new Models.ProjectMetadata();
        var dtuVm = new DtuGatewayStudioViewModel(metadata);
        if (targetNode != null)
        {
            dtuVm.SelectedPortIndex = targetNode.PortIndex;
            dtuVm.SerialMode = targetNode.SerialMode;
            dtuVm.UplinkMode = targetNode.UplinkMode;
            if (!string.IsNullOrWhiteSpace(targetNode.CustomJsonTemplate))
            {
                dtuVm.JsonTemplate = targetNode.CustomJsonTemplate;
            }
            if (targetNode.HttpFields.Count > 0)
            {
                dtuVm.HttpFields.Clear();
                foreach (var f in targetNode.HttpFields)
                {
                    dtuVm.HttpFields.Add(new DtuHttpFieldModel { Key = f.Key, ValueSource = f.ValueSource, CustomValue = f.CustomValue });
                }
            }
        }

        var dialog = new Views.DtuGatewayConfigDialogView(dtuVm);
        dialog.Owner = System.Windows.Application.Current?.MainWindow;
        if (dialog.ShowDialog() == true)
        {
            if (targetNode != null)
            {
                targetNode.PortIndex = (byte)dtuVm.SelectedPortIndex;
                targetNode.SerialMode = dtuVm.SerialMode;
                targetNode.UplinkMode = dtuVm.UplinkMode;
                targetNode.CustomJsonTemplate = dtuVm.JsonTemplate;
                targetNode.HttpFields.Clear();
                foreach (var f in dtuVm.HttpFields)
                {
                    targetNode.HttpFields.Add(new DtuHttpFieldModel { Key = f.Key, ValueSource = f.ValueSource, CustomValue = f.CustomValue });
                }
            }
            NotifyGraphModified();
        }
    }

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
            if (oldValue is ComparatorNodeViewModel oldCmp)
            {
                if (oldCmp.InputTag != null) oldCmp.InputTag.PropertyChanged -= OnTagPropertyChanged;
            }
            if (oldValue is MqttPublishNodeViewModel oldMqtt)
            {
                if (oldMqtt.DataTag != null) oldMqtt.DataTag.PropertyChanged -= OnTagPropertyChanged;
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
            if (newValue is ComparatorNodeViewModel newCmp)
            {
                if (newCmp.InputTag != null) newCmp.InputTag.PropertyChanged += OnTagPropertyChanged;
            }
            if (newValue is MqttPublishNodeViewModel newMqtt)
            {
                if (newMqtt.DataTag != null) newMqtt.DataTag.PropertyChanged += OnTagPropertyChanged;
            }

            if (!_isSyncingSelection)
            {
                _isSyncingSelection = true;
                try
                {
                    if (!newValue.IsSelected)
                        newValue.IsSelected = true;
                    if (!SelectedNodes.Contains(newValue))
                        SelectedNodes.Add(newValue);
                }
                finally
                {
                    _isSyncingSelection = false;
                }
            }
            else
            {
                if (!newValue.IsSelected)
                    newValue.IsSelected = true;
            }
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
                e.PropertyName == nameof(TimerNodeViewModel.TimerMode) ||
                e.PropertyName == nameof(TimerNodeViewModel.OnTimeMs) ||
                e.PropertyName == nameof(TimerNodeViewModel.OffTimeMs) ||
                e.PropertyName == nameof(TimerNodeViewModel.StartWithOn))
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
        else if (sender is MqttPublishNodeViewModel mqtt)
        {
            if (e.PropertyName == nameof(MqttPublishNodeViewModel.DataTag))
            {
                if (mqtt.DataTag != null)
                {
                    mqtt.DataTag.PropertyChanged -= OnTagPropertyChanged;
                    mqtt.DataTag.PropertyChanged += OnTagPropertyChanged;
                }
                OnPropertyChanged(nameof(NarrativePreview));
                NotifyGraphModified();
                return;
            }

            if (e.PropertyName != null && SemanticNodeProperties.Contains(e.PropertyName))
            {
                OnPropertyChanged(nameof(NarrativePreview));
                NotifyGraphModified();
            }
            return;
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

            if (SelectedNode is DirectWriteNodeViewModel dwNode)
            {
                return dwNode.NarrativeText;
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
    [NotifyPropertyChangedFor(nameof(CompileStateColor))]
    [NotifyPropertyChangedFor(nameof(CompileStateBackground))]
    [NotifyPropertyChangedFor(nameof(CompileStateBorder))]
    private CompileState _compileState = CompileState.NotCompiled;

    public string CompileStateDisplay => CompileState switch
    {
        CompileState.Valid => LocalizationService.Tr("CompileStateValid"),
        CompileState.Stale => LocalizationService.Tr("CompileStateStale"),
        _ => LocalizationService.Tr("CompileStateInvalid")
    };

    public string CompileStateColor => CompileState switch
    {
        CompileState.Valid => "#107C41",      // Forest Green
        CompileState.Stale => "#C05621",      // Safety Amber
        _ => "#B91C1C"                       // Safety Red
    };

    public string CompileStateBackground => CompileState switch
    {
        CompileState.Valid => "#E6F4EA",     // Green soft light
        CompileState.Stale => "#FEF3C7",     // Amber soft light
        _ => "#FEE2E2"                       // Red soft light
    };

    public string CompileStateBorder => CompileState switch
    {
        CompileState.Valid => "#A7F3D0",
        CompileState.Stale => "#FDE68A",
        _ => "#FECACA"
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
    private bool _isGraphDeployed = false;

    [ObservableProperty]
    private string? _lastDeployedProgramHash;

    public void MarkGraphDeployed(string? programHash = null)
    {
        LastDeployedProgramHash = programHash ?? CurrentProgram?.ComputeSemanticHash();
        IsGraphDeployed = true;
        SimGateWarning = string.Empty;
    }

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
        if (TagCatalog != null)
        {
            TagCatalog.PropertyChanged += OnTagCatalogPropertyChanged;
        }
        _ruleTable = ruleTable;
        _navigateToTab = navigateToTab;
        _compiler = compiler ?? new RuleCompiler();
        _stateStore = stateStore ?? AppServices.Instance.StateStore;
        _uiDispatcher = uiDispatcher ?? (action =>
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.HasShutdownStarted)
            {
                dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, action);
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
                    node.InputConnectors.CollectionChanged += OnNodeConnectorsChanged;
                    node.OutputConnectors.CollectionChanged += OnNodeConnectorsChanged;
                    if (node.IsSelected && !SelectedNodes.Contains(node))
                    {
                        if (!_isSyncingSelection)
                        {
                            _isSyncingSelection = true;
                            try
                            {
                                SelectedNodes.Add(node);
                            }
                            finally
                            {
                                _isSyncingSelection = false;
                            }
                        }
                    }
                }
            }
            if (e.OldItems != null)
            {
                foreach (GraphNodeViewModel node in e.OldItems)
                {
                    node.PropertyChanged -= OnNodeSelectionPropertyChanged;
                    node.InputConnectors.CollectionChanged -= OnNodeConnectorsChanged;
                    node.OutputConnectors.CollectionChanged -= OnNodeConnectorsChanged;
                    if (!_isSyncingSelection)
                    {
                        _isSyncingSelection = true;
                        try
                        {
                            node.IsSelected = false;
                            SelectedNodes.Remove(node);
                        }
                        finally
                        {
                            _isSyncingSelection = false;
                        }
                    }
                    else
                    {
                        node.IsSelected = false;
                    }
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
            if (_isSyncingSelection) return;

            _isSyncingSelection = true;
            try
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
            }
            finally
            {
                _isSyncingSelection = false;
            }
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveDebugStatusText))]
    [NotifyPropertyChangedFor(nameof(LiveDebugBannerSubText))]
    [NotifyPropertyChangedFor(nameof(LiveDebugTooltip))]
    private bool _isLiveDebugActive;

    public bool IsDeviceConnected => _stateStore?.CurrentSnapshot?.ConnectionStatus == ConnectionStatus.Connected;

    public string LiveDebugStatusText => IsLiveDebugActive
        ? (LocalizationService.Instance.IsVietnamese ? "Đang giám sát trực tiếp" : "Live Monitoring Active")
        : (LocalizationService.Instance.IsVietnamese ? "Chế độ giám sát tắt" : "Live Monitor Inactive");

    public string LiveDebugTooltip => IsLiveDebugActive
        ? LocalizationService.Tr("LiveDebugActiveTooltip")
        : LocalizationService.Tr("LiveDebugInactiveTooltip");

    public string LiveDebugBannerSubText
    {
        get
        {
            if (!IsDeviceConnected)
            {
                return LocalizationService.Tr("LiveDebugHudDisconnected");
            }

            int activeBlockCount = ValidRuleCount;
            if (activeBlockCount <= 0 && Nodes.Count > 0)
            {
                activeBlockCount = Nodes.Count;
            }

            if (activeBlockCount > 0)
            {
                return string.Format(LocalizationService.Tr("LiveDebugHudConnectedWithCount"), activeBlockCount);
            }

            return LocalizationService.Tr("LiveDebugHudConnected");
        }
    }

    [RelayCommand]
    public void ToggleLiveDebug()
    {
        if (!IsLiveDebugActive)
        {
            // Lớp 1: Khóa an toàn kết nối thiết bị
            if (!IsDeviceConnected)
            {
                SimGateWarning = LocalizationService.Tr("LiveDebugConnectRequired");
                return;
            }

            // Lớp 2: Khóa an toàn khung vẽ trống rỗng
            if (Nodes.Count == 0)
            {
                SimGateWarning = LocalizationService.Tr("LiveDebugEmptyGraphWarning");
                return;
            }

            // Lớp 3: Tự động biên dịch kiểm tra tính toàn vẹn (tương tự StartSimulation)
            if (CompileState != CompileState.Valid || CurrentProgram == null || ValidRuleCount == 0)
            {
                CompileAndSaveRules(isAutoSync: false);
            }

            // Lớp 4: Kiểm tra kết quả biên dịch
            if (CompileState == CompileState.Invalid)
            {
                SimGateWarning = LocalizationService.Tr("LiveDebugCompileErrorWarning");
                return;
            }

            if (ValidRuleCount == 0 || CurrentProgram == null)
            {
                SimGateWarning = LocalizationService.Tr("LiveDebugNoRulesWarning");
                return;
            }

            // Lớp 5: Khóa an toàn đồng bộ nạp cấu hình xuống thiết bị (Strict Deployment Safety Gate)
            if (!IsGraphDeployed)
            {
                SimGateWarning = LocalizationService.Tr("LiveDebugDeployRequiredWarning");
                return;
            }

            SimGateWarning = string.Empty;
        }

        IsLiveDebugActive = !IsLiveDebugActive;
    }

    partial void OnIsLiveDebugActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(LiveDebugBannerSubText));
        if (value)
        {
            if (_stateStore?.CurrentSnapshot != null)
            {
                ApplySnapshotDiff(_stateStore.CurrentSnapshot);
            }
            else
            {
                ApplyLiveOnlinePropagation();
            }
        }
        else
        {
            if (!IsSimulationMode)
            {
                ClearSimVisuals();
            }
        }
    }

    private readonly IRuntimeStateStore? _stateStore;
    private readonly Action<Action> _uiDispatcher;
    private readonly Dictionary<ushort, List<ILiveTagBoundNode>> _tagToNodesIndex = new();
    private readonly Dictionary<ushort, (int RawValue, TagQuality Quality)> _lastAppliedTagState = new();
    private bool _lastOnlineState;
    private RuntimeDeviceSnapshot? _latestSnapshot;
    private int _isUpdateScheduled;
    private readonly Dictionary<string, DateTime> _liveTimerStartTimes = new();
    private readonly Dictionary<string, bool> _liveTimerPrevIn = new();
    private readonly Dictionary<string, int> _liveTriggerPrevIn = new();
    private readonly Dictionary<string, DateTime> _liveTriggerPulseTimes = new();

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
        bool connectionChanged = (isOnline != _lastOnlineState);

        if (connectionChanged)
        {
            _lastOnlineState = isOnline;
            _lastAppliedTagState.Clear();
            OnPropertyChanged(nameof(IsDeviceConnected));
            OnPropertyChanged(nameof(LiveDebugBannerSubText));

            if (!isOnline)
            {
                foreach (var nodes in _tagToNodesIndex.Values)
                {
                    foreach (var node in nodes)
                    {
                        node.SetOffline();
                    }
                }

                if (IsLiveDebugActive)
                {
                    IsLiveDebugActive = false;
                }
                ClearSimVisuals();
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

                if (tmNode.ElapsedMs != tmDto.ElapsedMs) tmNode.ElapsedMs = tmDto.ElapsedMs;
                double newProgress = tmNode.PresetMs > 0
                    ? Math.Min(100.0, ((double)tmDto.ElapsedMs / tmNode.PresetMs) * 100.0)
                    : 0.0;
                if (Math.Abs(tmNode.ProgressPercent - newProgress) > 0.01) tmNode.ProgressPercent = newProgress;
                if (tmNode.IsTiming != tmDto.Running) tmNode.IsTiming = tmDto.Running;
                if (tmNode.IsLiveActive != tmDto.Q) tmNode.IsLiveActive = tmDto.Q;
                if (tmNode.IsLiveOnline != isOnline) tmNode.IsLiveOnline = isOnline;
            }
        }

        if (snapshot.Counters != null && snapshot.Counters.Count > 0)
        {
            var counterNodes = Nodes.OfType<CounterNodeViewModel>().ToList();
            for (int i = 0; i < counterNodes.Count && i < snapshot.Counters.Count; i++)
            {
                var cntNode = counterNodes[i];
                var cntDto = snapshot.Counters[i];

                if (cntNode.CurrentCount != cntDto.CurrentValue) cntNode.CurrentCount = cntDto.CurrentValue;
                if (cntNode.IsLiveActive != cntDto.Q) cntNode.IsLiveActive = cntDto.Q;
                if (cntNode.IsLiveOnline != isOnline) cntNode.IsLiveOnline = isOnline;
            }
        }

        // CHỈ lan truyền đồ họa khi chế độ Live Debug được người dùng BẬT chủ động!
        if (IsLiveDebugActive)
        {
            ApplyLiveOnlinePropagation();
        }
    }

    /// <summary>
    /// Lan truyền trạng thái thực thi thời gian thực trên đồ thị FBD khi chạy chế độ Live Debug hoặc Online Monitor.
    /// Bơm trực tiếp dữ liệu từ Device Snapshot / TagCatalog vào các chân cắm và dây dẫn.
    /// TỐI ƯU HÓA: Áp dụng Dirty Checking tuyệt đối trên mọi thuộc tính nhằm triệt tiêu tối đa các thông báo PropertyChanged thừa.
    /// </summary>
    public void ApplyLiveOnlinePropagation()
    {
        // 1. Inputs: Cập nhật chân ra và dây nối trực tiếp từ Input tags
        foreach (var inp in Nodes.OfType<InputNodeViewModel>())
        {
            if (inp.IsLiveOnline && inp.Tag != null)
            {
                if (inp.Tag.Value != inp.LiveRawValue)
                {
                    inp.Tag.Value = inp.LiveRawValue;
                }
            }

            bool isBoolean = (inp.Tag?.IsDigital == true) ||
                             (inp.Tag?.Kind is TagKind.DiscreteInput or TagKind.DiscreteOutput or TagKind.VirtualFlag);

            int val = inp.IsLiveOnline ? inp.LiveRawValue : (inp.Tag?.Value ?? 0);
            bool inActive = isBoolean && (val != 0);

            if (inp.IsLiveActive != inActive) inp.IsLiveActive = inActive;
            if (inp.IsSimActive != inActive) inp.IsSimActive = inActive;

            bool connTargetState = isBoolean ? inActive : (val != 0);
            foreach (var conn in Connections.Where(c => c.Source?.Node == inp))
            {
                if (conn.IsActive != connTargetState)
                {
                    conn.IsActive = connTargetState;
                }
            }
        }

        // 2. Scale Nodes: Chuyển đổi tuyến tính giá trị Analog
        foreach (var scaleNode in Nodes.OfType<ScaleNodeViewModel>())
        {
            var scaleConn = Connections.FirstOrDefault(c => c.Target?.Node == scaleNode);
            if (scaleConn?.Source?.Node is InputNodeViewModel inp)
            {
                int rawIn = inp.IsLiveOnline ? inp.LiveRawValue : (inp.Tag?.Value ?? 0);
                if (inp.Tag != null && scaleNode.InputTag != inp.Tag)
                {
                    scaleNode.InputTag = inp.Tag;
                }
                double scaled = scaleNode.Calculate(rawIn);
                bool isActive = Math.Abs(scaled) > 1e-6 || rawIn != 0;
                if (scaleNode.IsSimActive != isActive) scaleNode.IsSimActive = isActive;
                if (scaleNode.IsLiveActive != isActive) scaleNode.IsLiveActive = isActive;
                bool scaleConnActive = rawIn != 0;
                if (scaleConn.IsActive != scaleConnActive) scaleConn.IsActive = scaleConnActive;

                if (scaleNode.OutputTag != null)
                {
                    int scaledVal = (int)Math.Round(scaled);
                    if (scaleNode.OutputTag.Value != scaledVal)
                    {
                        scaleNode.OutputTag.Value = scaledVal;
                    }
                }

                foreach (var outConn in Connections.Where(c => c.Source?.Node == scaleNode))
                {
                    if (outConn.IsActive != isActive) outConn.IsActive = isActive;
                    if (outConn.Target?.Node is ActionNodeViewModel act)
                    {
                        if (act.IsSimActive != isActive) act.IsSimActive = isActive;
                        if (act.IsSimFired != isActive) act.IsSimFired = isActive;
                        string liveText = scaleNode.LiveFormattedValue;
                        if (act.SimTargetLiveText != liveText) act.SimTargetLiveText = liveText;
                    }
                }
            }
        }

        // 3. Comparator Nodes: Đánh giá điều kiện so sánh
        foreach (var cmpNode in Nodes.OfType<ComparatorNodeViewModel>())
        {
            var cmpConn = Connections.FirstOrDefault(c => c.Target?.Node == cmpNode);
            int cmpVal = 0;
            if (cmpConn?.Source?.Node is InputNodeViewModel inp)
            {
                cmpVal = inp.IsLiveOnline ? inp.LiveRawValue : (inp.Tag?.Value ?? 0);
                if (inp.Tag != null && cmpNode.InputTag != inp.Tag) cmpNode.InputTag = inp.Tag;
                bool cmpConnActive = cmpVal != 0;
                if (cmpConn.IsActive != cmpConnActive) cmpConn.IsActive = cmpConnActive;
            }
            else if (cmpConn?.Source?.Node is ScaleNodeViewModel scl)
            {
                if (scl.OutputTag != null)
                {
                    if (cmpNode.InputTag != scl.OutputTag) cmpNode.InputTag = scl.OutputTag;
                    cmpVal = scl.OutputTag.Value;
                }
                else
                {
                    cmpVal = (int)Math.Round(scl.LiveScaledValue);
                }
                if (cmpConn.IsActive != scl.IsLiveActive) cmpConn.IsActive = scl.IsLiveActive;
            }

            if (cmpNode.InputTag != null || cmpConn?.Source?.Node is ScaleNodeViewModel)
            {
                bool condMet = cmpNode.Evaluate(cmpVal);
                if (cmpNode.IsConditionMet != condMet) cmpNode.IsConditionMet = condMet;
                foreach (var outConn in Connections.Where(c => c.Source?.Node == cmpNode))
                {
                    if (outConn.IsActive != condMet) outConn.IsActive = condMet;
                }
            }
        }

        // 4. Trigger Nodes: Đánh giá điều kiện Trigger (sườn lên/xuống, biến thiên, so sánh ngưỡng)
        foreach (var trigNode in Nodes.OfType<TriggerNodeViewModel>())
        {
            var trigConn = Connections.FirstOrDefault(c => c.Target?.Node == trigNode);
            int inpVal = 0;
            bool hasInp = false;
            if (trigConn?.Source?.Node is InputNodeViewModel inp)
            {
                inpVal = inp.IsLiveOnline ? inp.LiveRawValue : (inp.Tag?.Value ?? 0);
                hasInp = true;
                bool inpActive = inpVal != 0;
                if (trigConn.IsActive != inpActive) trigConn.IsActive = inpActive;
            }
            else if (trigConn?.Source?.Node is ComparatorNodeViewModel cmp)
            {
                inpVal = cmp.IsConditionMet ? 1 : 0;
                hasInp = true;
                if (trigConn.IsActive != cmp.IsConditionMet) trigConn.IsActive = cmp.IsConditionMet;
            }

            bool trigMet = false;
            bool hasPrev = _liveTriggerPrevIn.TryGetValue(trigNode.Id, out int prevVal);

            if (hasInp)
            {
                int prev = hasPrev ? prevVal : 0;
                bool edgeFired = trigNode.EvaluateCondition(inpVal, prev);
                _liveTriggerPrevIn[trigNode.Id] = inpVal;

                if (edgeFired)
                {
                    _liveTriggerPulseTimes[trigNode.Id] = DateTime.UtcNow;
                    trigMet = true;
                }
                else if (_liveTriggerPulseTimes.TryGetValue(trigNode.Id, out var pulseTime))
                {
                    if ((DateTime.UtcNow - pulseTime).TotalMilliseconds < 250)
                    {
                        trigMet = true;
                    }
                    else
                    {
                        _liveTriggerPulseTimes.Remove(trigNode.Id);
                    }
                }
            }
            else
            {
                trigMet = trigNode.EvaluateCondition(0);
            }

            if (trigNode.IsSimTriggered != trigMet) trigNode.IsSimTriggered = trigMet;
            if (trigNode.IsSimActive != trigMet) trigNode.IsSimActive = trigMet;
            if (!trigNode.IsSimEvaluated) trigNode.IsSimEvaluated = true;

            string expectedDiag;
            if (trigMet)
            {
                expectedDiag = LocalizationService.Instance.Get("SimStatusTrue");
            }
            else
            {
                if (trigNode.HasComparison)
                {
                    expectedDiag = string.Format(LocalizationService.Tr("SimDiagNotMet"), inpVal);
                }
                else if (trigNode.TriggerType == TriggerType.ON_RISE)
                {
                    expectedDiag = inpVal == 0
                        ? LocalizationService.Tr("SimDiagWaitRise")
                        : LocalizationService.Tr("SimDiagNoEdge");
                }
                else if (trigNode.TriggerType == TriggerType.ON_FALL)
                {
                    expectedDiag = inpVal != 0
                        ? LocalizationService.Tr("SimDiagWaitFall")
                        : LocalizationService.Tr("SimDiagNoEdge");
                }
                else
                {
                    expectedDiag = LocalizationService.Instance.Get("SimDiagFalse");
                }
            }
            if (trigNode.SimDiagText != expectedDiag) trigNode.SimDiagText = expectedDiag;

            foreach (var outConn in Connections.Where(c => c.Source?.Node == trigNode))
            {
                if (outConn.IsActive != trigMet) outConn.IsActive = trigMet;
            }
        }

        // 5. Guard Nodes: Đánh giá điều kiện khóa liên động an toàn
        foreach (var guardNode in Nodes.OfType<GuardNodeViewModel>())
        {
            var guardConn = Connections.FirstOrDefault(c => c.Target?.Node == guardNode);
            bool upstreamActive = guardConn != null && guardConn.IsActive;
            bool guardPassed = false;
            bool guardBlocked = false;

            int guardVal = guardNode.IsLiveOnline ? guardNode.LiveRawValue : (guardNode.GuardTag?.Value ?? 0);
            if (guardNode.GuardTag != null || guardNode.IsLiveOnline)
            {
                bool tagVal = guardVal != 0;
                bool condition = guardNode.Negate ? !tagVal : tagVal;
                if (upstreamActive)
                {
                    guardPassed = condition;
                    guardBlocked = !condition;
                }
            }
            else
            {
                guardPassed = upstreamActive;
            }

            if (guardNode.IsSimPassed != guardPassed) guardNode.IsSimPassed = guardPassed;
            if (guardNode.IsSimBlocked != guardBlocked) guardNode.IsSimBlocked = guardBlocked;
            if (guardNode.IsSimActive != guardPassed) guardNode.IsSimActive = guardPassed;

            foreach (var outConn in Connections.Where(c => c.Source?.Node == guardNode))
            {
                if (outConn.IsActive != guardPassed) outConn.IsActive = guardPassed;
            }
        }

        // 6. Timers: Cập nhật dây vào, tính toán đếm giờ Live và cập nhật dây ra Q
        foreach (var timerNode in Nodes.OfType<TimerNodeViewModel>())
        {
            var timerConn = Connections.FirstOrDefault(c => c.Target?.Node == timerNode);
            bool inActive = false;
            if (timerConn?.Source?.Node is InputNodeViewModel inNode)
            {
                int val = inNode.IsLiveOnline ? inNode.LiveRawValue : (inNode.Tag?.Value ?? 0);
                inActive = val != 0;
                if (timerConn.IsActive != inActive) timerConn.IsActive = inActive;
            }
            else if (timerConn?.Source?.Node is TriggerNodeViewModel trg)
            {
                inActive = trg.IsSimTriggered;
                if (timerConn.IsActive != inActive) timerConn.IsActive = inActive;
            }
            else if (timerConn?.Source?.Node is GuardNodeViewModel grd)
            {
                inActive = grd.IsSimPassed;
                if (timerConn.IsActive != inActive) timerConn.IsActive = inActive;
            }

            // Đọc trạng thái ngõ ra Q từ live TagCatalog nếu có output tag
            bool qActive = false;
            if (timerNode.OutputTag != null)
            {
                var qTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == timerNode.OutputTag.Index);
                if (qTag != null)
                {
                    qActive = qTag.Value != 0;
                }
            }
            if (!qActive && timerNode.IsLiveActive)
            {
                qActive = timerNode.IsLiveActive;
            }

            // Bộ đếm Live Timer Client-side nội suy mượt mà
            bool hasPrev = _liveTimerPrevIn.TryGetValue(timerNode.Id, out bool prevIn);
            string mode = timerNode.TimerMode?.ToUpperInvariant() ?? "TON";
            uint preset = timerNode.PresetMs > 0 ? timerNode.PresetMs : 1000;

            switch (mode)
            {
                case "TON":
                    if (inActive)
                    {
                        if (!hasPrev || !prevIn)
                        {
                            _liveTimerStartTimes[timerNode.Id] = DateTime.UtcNow;
                        }

                        if (qActive)
                        {
                            if (timerNode.IsTiming) timerNode.IsTiming = false;
                            if (timerNode.ElapsedMs != preset) timerNode.ElapsedMs = preset;
                            if (Math.Abs(timerNode.ProgressPercent - 100.0) > 0.01) timerNode.ProgressPercent = 100.0;
                        }
                        else
                        {
                            var startTime = _liveTimerStartTimes.TryGetValue(timerNode.Id, out var st) ? st : DateTime.UtcNow;
                            double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                            if (elapsed < preset)
                            {
                                if (!timerNode.IsTiming) timerNode.IsTiming = true;
                                uint elMs = (uint)elapsed;
                                if (timerNode.ElapsedMs != elMs) timerNode.ElapsedMs = elMs;
                                double prog = Math.Min(100.0, (elapsed / preset) * 100.0);
                                if (Math.Abs(timerNode.ProgressPercent - prog) > 0.01) timerNode.ProgressPercent = prog;
                            }
                            else
                            {
                                if (timerNode.IsTiming) timerNode.IsTiming = false;
                                if (timerNode.ElapsedMs != preset) timerNode.ElapsedMs = preset;
                                if (Math.Abs(timerNode.ProgressPercent - 100.0) > 0.01) timerNode.ProgressPercent = 100.0;
                            }
                        }
                    }
                    else
                    {
                        if (timerNode.IsTiming) timerNode.IsTiming = false;
                        if (timerNode.ElapsedMs != 0) timerNode.ElapsedMs = 0;
                        if (timerNode.ProgressPercent != 0.0) timerNode.ProgressPercent = 0.0;
                        _liveTimerStartTimes.Remove(timerNode.Id);
                    }
                    break;

                case "TOF":
                    if (inActive)
                    {
                        if (timerNode.IsTiming) timerNode.IsTiming = false;
                        if (timerNode.ElapsedMs != 0) timerNode.ElapsedMs = 0;
                        if (timerNode.ProgressPercent != 0.0) timerNode.ProgressPercent = 0.0;
                        _liveTimerStartTimes.Remove(timerNode.Id);
                        qActive = true;
                    }
                    else
                    {
                        if (hasPrev && prevIn)
                        {
                            _liveTimerStartTimes[timerNode.Id] = DateTime.UtcNow;
                        }

                        if (qActive)
                        {
                            var startTime = _liveTimerStartTimes.TryGetValue(timerNode.Id, out var st) ? st : DateTime.UtcNow;
                            double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                            if (elapsed < preset)
                            {
                                if (!timerNode.IsTiming) timerNode.IsTiming = true;
                                uint elMs = (uint)elapsed;
                                if (timerNode.ElapsedMs != elMs) timerNode.ElapsedMs = elMs;
                                double prog = Math.Min(100.0, (elapsed / preset) * 100.0);
                                if (Math.Abs(timerNode.ProgressPercent - prog) > 0.01) timerNode.ProgressPercent = prog;
                            }
                            else
                            {
                                if (timerNode.IsTiming) timerNode.IsTiming = false;
                                if (timerNode.ElapsedMs != preset) timerNode.ElapsedMs = preset;
                                if (Math.Abs(timerNode.ProgressPercent - 100.0) > 0.01) timerNode.ProgressPercent = 100.0;
                            }
                        }
                        else
                        {
                            if (timerNode.IsTiming) timerNode.IsTiming = false;
                            if (timerNode.ElapsedMs != 0) timerNode.ElapsedMs = 0;
                            if (timerNode.ProgressPercent != 0.0) timerNode.ProgressPercent = 0.0;
                            _liveTimerStartTimes.Remove(timerNode.Id);
                        }
                    }
                    break;

                case "TP":
                    if (inActive && (!hasPrev || !prevIn))
                    {
                        _liveTimerStartTimes[timerNode.Id] = DateTime.UtcNow;
                    }

                    if (_liveTimerStartTimes.TryGetValue(timerNode.Id, out var pulseStart))
                    {
                        double elapsed = (DateTime.UtcNow - pulseStart).TotalMilliseconds;
                        if (elapsed < preset && qActive)
                        {
                            if (!timerNode.IsTiming) timerNode.IsTiming = true;
                            uint elMs = (uint)elapsed;
                            if (timerNode.ElapsedMs != elMs) timerNode.ElapsedMs = elMs;
                            double prog = Math.Min(100.0, (elapsed / preset) * 100.0);
                            if (Math.Abs(timerNode.ProgressPercent - prog) > 0.01) timerNode.ProgressPercent = prog;
                        }
                        else
                        {
                            if (timerNode.IsTiming) timerNode.IsTiming = false;
                            if (timerNode.ElapsedMs != 0) timerNode.ElapsedMs = 0;
                            if (timerNode.ProgressPercent != 0.0) timerNode.ProgressPercent = 0.0;
                            _liveTimerStartTimes.Remove(timerNode.Id);
                        }
                    }
                    else
                    {
                        if (timerNode.IsTiming) timerNode.IsTiming = false;
                        if (timerNode.ElapsedMs != 0) timerNode.ElapsedMs = 0;
                        if (timerNode.ProgressPercent != 0.0) timerNode.ProgressPercent = 0.0;
                    }
                    break;
            }

            _liveTimerPrevIn[timerNode.Id] = inActive;
            if (timerNode.IsLiveActive != qActive) timerNode.IsLiveActive = qActive;
            bool timerActive = qActive || timerNode.IsTiming;
            if (timerNode.IsSimActive != timerActive) timerNode.IsSimActive = timerActive;

            foreach (var outConn in Connections.Where(c => c.Source?.Node == timerNode))
            {
                if (outConn.IsActive != qActive) outConn.IsActive = qActive;
                if (outConn.Target?.Node is ActionNodeViewModel act)
                {
                    if (act.IsSimActive != qActive) act.IsSimActive = qActive;
                    if (act.IsSimTargetOn != qActive) act.IsSimTargetOn = qActive;
                }
            }
        }

        // 7. Counters: Cập nhật dây vào, giá trị CV từ live TagCatalog và dây ra Q
        foreach (var counterNode in Nodes.OfType<CounterNodeViewModel>())
        {
            foreach (var counterConn in Connections.Where(c => c.Target?.Node == counterNode))
            {
                if (counterConn.Source?.Node is InputNodeViewModel inNode)
                {
                    int val = inNode.IsLiveOnline ? inNode.LiveRawValue : (inNode.Tag?.Value ?? 0);
                    bool valActive = val != 0;
                    if (counterConn.IsActive != valActive) counterConn.IsActive = valActive;
                }
                else if (counterConn.Source?.Node is TriggerNodeViewModel trg)
                {
                    if (counterConn.IsActive != trg.IsSimTriggered) counterConn.IsActive = trg.IsSimTriggered;
                }
                else if (counterConn.Source?.Node is GuardNodeViewModel grd)
                {
                    if (counterConn.IsActive != grd.IsSimPassed) counterConn.IsActive = grd.IsSimPassed;
                }
            }

            // Đọc giá trị CV từ live TagCatalog (ví dụ COUNTER0, VREG...)
            if (counterNode.CvTag != null)
            {
                var cvTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == counterNode.CvTag.Index);
                if (cvTag != null && counterNode.CurrentCount != cvTag.Value)
                {
                    counterNode.CurrentCount = cvTag.Value;
                }
            }

            // Đọc trạng thái ngõ ra Q từ live TagCatalog
            bool qActive = false;
            if (counterNode.OutputTag != null)
            {
                var qTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == counterNode.OutputTag.Index);
                if (qTag != null)
                {
                    qActive = qTag.Value != 0;
                }
            }
            else
            {
                qActive = counterNode.CounterMode == "CTD"
                    ? counterNode.CurrentCount <= 0
                    : counterNode.CurrentCount >= counterNode.PresetValue;
            }

            if (counterNode.IsLiveActive != qActive) counterNode.IsLiveActive = qActive;
            if (counterNode.IsSimActive != qActive) counterNode.IsSimActive = qActive;

            foreach (var outConn in Connections.Where(c => c.Source?.Node == counterNode))
            {
                if (outConn.IsActive != qActive) outConn.IsActive = qActive;
                if (outConn.Target?.Node is ActionNodeViewModel act)
                {
                    if (act.IsSimActive != qActive) act.IsSimActive = qActive;
                    if (act.IsSimTargetOn != qActive) act.IsSimTargetOn = qActive;
                }
            }
        }

        // 8. Action Nodes: Nhận tín hiệu kích hoạt từ upstream
        foreach (var actionNode in Nodes.OfType<ActionNodeViewModel>())
        {
            var actConn = Connections.FirstOrDefault(c => c.Target?.Node == actionNode);
            bool actActive = actConn != null && actConn.IsActive;
            if (actionNode.IsSimActive != actActive) actionNode.IsSimActive = actActive;
            if (actionNode.IsSimFired != actActive) actionNode.IsSimFired = actActive;

            int targetVal = actionNode.IsLiveOnline ? actionNode.LiveRawValue : (actionNode.TargetTag?.Value ?? 0);
            bool targetOn = targetVal != 0;
            if (actionNode.IsSimTargetOn != targetOn) actionNode.IsSimTargetOn = targetOn;
            if (actionNode.TargetTag != null)
            {
                string targetLiveText = $"{actionNode.TargetTag.Name}: {targetVal}";
                if (actionNode.SimTargetLiveText != targetLiveText)
                {
                    actionNode.SimTargetLiveText = targetLiveText;
                }
            }
        }

        // 9. MQTT Publish Nodes: Nhận tín hiệu kích hoạt từ upstream và hiển thị giá trị live
        foreach (var mqttNode in Nodes.OfType<MqttPublishNodeViewModel>())
        {
            var mqttConn = Connections.FirstOrDefault(c => c.Target?.Node == mqttNode);
            bool mqttActive = mqttConn != null && mqttConn.IsActive;
            if (mqttNode.IsSimActive != mqttActive) mqttNode.IsSimActive = mqttActive;
            if (mqttNode.IsLiveActive != mqttActive) mqttNode.IsLiveActive = mqttActive;

            if (mqttNode.DataTag != null)
            {
                var currentTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == mqttNode.DataTag.Index);
                if (currentTag != null)
                {
                    mqttNode.LiveValue = currentTag.Value;
                    mqttNode.LiveValueText = currentTag.Value.ToString();
                }
            }
        }

        // 10. MQTT Subscribe Nodes: Nhận dữ liệu live và truyền xuống downstream Actions (Always-on Source Node)
        foreach (var subNode in Nodes.OfType<MqttSubscribeNodeViewModel>())
        {
            bool enActive = true;

            foreach (var pin in subNode.Pins)
            {
                int pinVal = pin.TargetTag != null ? (TagCatalog.AllTags.FirstOrDefault(t => t.Index == pin.TargetTag.Index)?.Value ?? pin.TargetTag.Value) : 0;
                bool pinActive = enActive && (pinVal != 0);

                foreach (var outConn in Connections.Where(c => c.Source?.Node == subNode && (c.Source == pin.Connector || string.Equals(c.Source.Title, pin.Label, StringComparison.OrdinalIgnoreCase))))
                {
                    if (outConn.IsActive != pinActive) outConn.IsActive = pinActive;
                    if (outConn.Target?.Node is ActionNodeViewModel act)
                    {
                        if (act.IsSimActive != pinActive) act.IsSimActive = pinActive;
                        if (act.IsSimFired != pinActive) act.IsSimFired = pinActive;
                        string liveText = $"{pin.Label}: {pinVal}";
                        if (act.SimTargetLiveText != liveText) act.SimTargetLiveText = liveText;
                    }
                }
            }
        }

        // 11. Direct Write Nodes (PULSE_OUT): Cập nhật dây kích hoạt và trạng thái cọc DO vật lý
        foreach (var dwNode in Nodes.OfType<DirectWriteNodeViewModel>())
        {
            var dwConn = Connections.FirstOrDefault(c => c.Target?.Node == dwNode &&
                (c.Target == dwNode.TrigConnector || string.Equals(c.Target.Title, "Trig", StringComparison.OrdinalIgnoreCase) || c.Target == dwNode.InputConnectors.FirstOrDefault()));
            bool trigActive = dwConn != null && dwConn.IsActive;
            if (dwConn != null && dwConn.IsActive != trigActive) dwConn.IsActive = trigActive;

            byte pinIdx = (byte)dwNode.TargetPin;
            var doTag = TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput && t.Index == pinIdx);
            bool isHardwarePinActive = doTag != null && doTag.Value != 0;

            bool isDwActive = trigActive || isHardwarePinActive;
            if (dwNode.IsSimActive != isDwActive) dwNode.IsSimActive = isDwActive;
            if (dwNode.IsLiveActive != isDwActive) dwNode.IsLiveActive = isDwActive;
            if (dwNode.IsDoneActive != isHardwarePinActive) dwNode.IsDoneActive = isHardwarePinActive;

            foreach (var outConn in Connections.Where(c => c.Source?.Node == dwNode && (c.Source == dwNode.DoneConnector || string.Equals(c.Source.Title, "Done", StringComparison.OrdinalIgnoreCase))))
            {
                if (outConn.IsActive != isHardwarePinActive) outConn.IsActive = isHardwarePinActive;
            }
        }

        // 12. Modbus Master Read Nodes: Cập nhật EN, dữ liệu đọc và truyền xuống các chân ra
        foreach (var mbNode in Nodes.OfType<ModbusReadNodeViewModel>())
        {
            var enConn = Connections.FirstOrDefault(c => c.Target?.Node == mbNode &&
                (c.Target == mbNode.TrigConnector || c.Target == mbNode.EnableConnector || string.Equals(c.Target.Title, "Trig", StringComparison.OrdinalIgnoreCase) || string.Equals(c.Target.Title, "EN", StringComparison.OrdinalIgnoreCase) || c.Target == mbNode.InputConnectors.FirstOrDefault()));
            bool enActive = enConn == null || enConn.IsActive;
            if (enConn != null && enConn.IsActive != enActive) enConn.IsActive = enActive;

            if (mbNode.IsSimActive != enActive) mbNode.IsSimActive = enActive;
            if (mbNode.IsLiveActive != enActive) mbNode.IsLiveActive = enActive;

            foreach (var pin in mbNode.Pins)
            {
                int pinVal = pin.TargetTag != null ? (TagCatalog.AllTags.FirstOrDefault(t => t.Index == pin.TargetTag.Index)?.Value ?? pin.TargetTag.Value) : 0;
                bool pinActive = enActive && (pinVal != 0);

                foreach (var outConn in Connections.Where(c => c.Source?.Node == mbNode && (c.Source == pin.Connector || string.Equals(c.Source.Title, pin.Label, StringComparison.OrdinalIgnoreCase))))
                {
                    if (outConn.IsActive != pinActive) outConn.IsActive = pinActive;
                    if (outConn.Target?.Node is ActionNodeViewModel act)
                    {
                        if (act.IsSimActive != pinActive) act.IsSimActive = pinActive;
                        if (act.IsSimFired != pinActive) act.IsSimFired = pinActive;
                        string liveText = $"{pin.Label}: {pinVal}";
                        if (act.SimTargetLiveText != liveText) act.SimTargetLiveText = liveText;
                    }
                }
            }
        }

        // 13. Modbus Master Write Nodes: Cập nhật Trig và tín hiệu Done
        foreach (var mbwNode in Nodes.OfType<ModbusWriteNodeViewModel>())
        {
            var trigConn = Connections.FirstOrDefault(c => c.Target?.Node == mbwNode &&
                (c.Target == mbwNode.TrigConnector || string.Equals(c.Target.Title, "Trig", StringComparison.OrdinalIgnoreCase) || c.Target == mbwNode.InputConnectors.FirstOrDefault()));
            bool trigActive = trigConn != null && trigConn.IsActive;
            if (trigConn != null && trigConn.IsActive != trigActive) trigConn.IsActive = trigActive;

            if (mbwNode.IsSimActive != trigActive) mbwNode.IsSimActive = trigActive;
            if (mbwNode.IsLiveActive != trigActive) mbwNode.IsLiveActive = trigActive;

            foreach (var outConn in Connections.Where(c => c.Source?.Node == mbwNode && (c.Source == mbwNode.DoneConnector || string.Equals(c.Source.Title, "Done", StringComparison.OrdinalIgnoreCase))))
            {
                if (outConn.IsActive != trigActive) outConn.IsActive = trigActive;
            }
        }
    }

    private void OnNodeSelectionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is GraphNodeViewModel node && e.PropertyName == nameof(GraphNodeViewModel.IsSelected))
        {
            if (_isSyncingSelection) return;

            _isSyncingSelection = true;
            try
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
                OnPropertyChanged(nameof(HasSelection));
            }
            finally
            {
                _isSyncingSelection = false;
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

    private void OnNodeConnectorsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            var oldConnectors = e.OldItems.OfType<ConnectorViewModel>().ToHashSet();
            var toRemove = Connections.Where(c => oldConnectors.Contains(c.Source) || oldConnectors.Contains(c.Target)).ToList();
            if (toRemove.Count > 0)
            {
                foreach (var conn in toRemove)
                {
                    Connections.Remove(conn);
                    conn.Source.IsConnected = Connections.Any(c => c.Source == conn.Source || c.Target == conn.Source);
                    conn.Target.IsConnected = Connections.Any(c => c.Source == conn.Target || c.Target == conn.Target);
                }
                NotifyGraphModified();
                OnPropertyChanged(nameof(Connections));
            }
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
        nameof(TimerNodeViewModel.OnTimeMs),
        nameof(TimerNodeViewModel.OffTimeMs),
        nameof(TimerNodeViewModel.StartWithOn),
        nameof(TimerNodeViewModel.InputTag),
        nameof(TimerNodeViewModel.OutputTag),
        nameof(CounterNodeViewModel.CounterMode),
        nameof(CounterNodeViewModel.PresetValue),
        nameof(CounterNodeViewModel.InputTag),
        nameof(CounterNodeViewModel.CvTag),
        nameof(CounterNodeViewModel.OutputTag),
        nameof(CounterNodeViewModel.ResetTag),
        nameof(MqttPublishNodeViewModel.Topic),
        nameof(MqttPublishNodeViewModel.PayloadMode),
        nameof(MqttPublishNodeViewModel.CustomTemplate),
        nameof(MqttPublishNodeViewModel.Qos),
        nameof(MqttPublishNodeViewModel.Retain),
        nameof(MqttPublishNodeViewModel.PublishMode),
        nameof(MqttPublishNodeViewModel.Deadband),
        nameof(MqttPublishNodeViewModel.Unit),
        nameof(MqttPublishNodeViewModel.DataTag),
        nameof(MqttPublishNodeViewModel.IncludeDevice),
        nameof(MqttPublishNodeViewModel.IncludeTimestamp),
        nameof(MqttSubscribeNodeViewModel.Topic),
        nameof(MqttSubscribeNodeViewModel.Qos),
        nameof(MqttSubscribeNodeViewModel.QueueMode),
        nameof(MqttSubscribeNodeViewModel.IgnoreRetained),
        nameof(MqttSubscribeNodeViewModel.FailSafeAction),
        nameof(MqttSubscribeNodeViewModel.FailSafeTimeoutMs),
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
        else if (target.Node is MqttPublishNodeViewModel mqttNode)
        {
            TagModel? incomingTag = source.Node switch
            {
                InputNodeViewModel inMqtt => inMqtt.Tag,
                ScaleNodeViewModel scaleMqtt => scaleMqtt.OutputTag ?? scaleMqtt.InputTag,
                _ => null
            };

            if (incomingTag != null)
            {
                if (target == mqttNode.TriggerConnector || string.Equals(target.Title, "Trig", StringComparison.OrdinalIgnoreCase) || string.Equals(target.Title, "EN", StringComparison.OrdinalIgnoreCase))
                {
                    // Trig pin connected: do not overwrite DataTag
                }
                else if (target == mqttNode.DefaultValConnector || string.Equals(target.Title, "Val", StringComparison.OrdinalIgnoreCase))
                {
                    mqttNode.DataTag = incomingTag;
                }
                else if (mqttNode.IsCustomTemplateVisible)
                {
                    mqttNode.SetCustomConnectorTag(target.Title, incomingTag);
                }
                else
                {
                    var matchedField = mqttNode.Fields.FirstOrDefault(f => f.Connector == target || string.Equals(f.Key, target.Title, StringComparison.OrdinalIgnoreCase));
                    if (matchedField != null)
                    {
                        matchedField.Tag = incomingTag;
                    }
                }
            }
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
        else if (source.Node is ModbusReadNodeViewModel mbReadNode && target.Node is ActionNodeViewModel mbActNode)
        {
            var pin = mbReadNode.Pins.FirstOrDefault(p =>
                p.Connector == source ||
                string.Equals(p.Id, source.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, source.Title, StringComparison.OrdinalIgnoreCase));
            if (pin != null && mbActNode.TargetTag != null)
            {
                pin.TargetTag = mbActNode.TargetTag;
            }
        }
        else if (source.Node is ModbusReadNodeViewModel mbReadToMqtt && target.Node is MqttPublishNodeViewModel mqttTarget)
        {
            var pin = mbReadToMqtt.Pins.FirstOrDefault(p =>
                p.Connector == source ||
                string.Equals(p.Id, source.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, source.Title, StringComparison.OrdinalIgnoreCase));
            var targetField = mqttTarget.Fields.FirstOrDefault(fld =>
                fld.Connector == target ||
                string.Equals(fld.Key, target.Title, StringComparison.OrdinalIgnoreCase));
            if (pin != null && targetField?.Tag != null)
            {
                pin.TargetTag = targetField.Tag;
            }
        }
    }

    internal static SimplePLC.Application.Logic.Graph.LogicNodeKind? GetNodeKind(GraphNodeViewModel? node) => node switch
    {
        InputNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Input,
        TriggerNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Trigger,
        GuardNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Guard,
        ActionNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Action,
        MqttPublishNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.MqttPublish,
        MqttSubscribeNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.MqttSubscribe,
        ModbusReadNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.ModbusRead,
        ModbusWriteNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.ModbusWrite,
        DirectWriteNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.DirectWrite,
        TimerNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Timer,
        CounterNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Counter,
        ScaleNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Scale,
        ComparatorNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.Comparator,
        DtuGatewayNodeViewModel => SimplePLC.Application.Logic.Graph.LogicNodeKind.DtuGateway,
        _ => null
    };

    public static double GetConnectorVerticalOffset(GraphNodeViewModel? node, ConnectorViewModel? connector)
    {
        const double singlePinOffset = 58.0; // Row 0 (4px accent) + Row 1 (26px header) + 56px body / 2
        if (node == null || connector == null)
            return singlePinOffset;

        var list = connector.IsInput ? node.InputConnectors : node.OutputConnectors;
        if (list.Count <= 1)
            return singlePinOffset;

        int index = list.IndexOf(connector);
        if (index < 0)
            return singlePinOffset;

        if (node is MqttSubscribeNodeViewModel || node is MqttPublishNodeViewModel)
        {
            // Dynamic nodes layout connectors from top:
            // 30px (4px accent + 26px header) + 6px top margin + index * 18px + 9px center
            return 36.0 + (index * 18.0) + 9.0;
        }

        // Chiều cao mỗi cổng là 16px, tổng chiều cao các cổng = list.Count * 16px
        // Khoảng trống trên/dưới trong vùng body 56px: (56 - list.Count * 16) / 2
        // Vị trí tâm của cổng index: 30 + topMargin + index * 16 + 8
        double totalPortsHeight = list.Count * 16.0;
        double topMargin = Math.Max(0, (56.0 - totalPortsHeight) / 2.0);
        return 30.0 + topMargin + (index * 16.0) + 8.0;
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
        else if (target.Node is ComparatorNodeViewModel cmpNode && source.Node is InputNodeViewModel inCmp)
        {
            cmpNode.InputTag = inCmp.Tag;
        }
        else if (target.Node is ComparatorNodeViewModel cmpFromScl && source.Node is ScaleNodeViewModel sclNode)
        {
            cmpFromScl.InputTag = sclNode.OutputTag ?? sclNode.InputTag;
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
        else if (source.Node is ModbusReadNodeViewModel mbReadNode && target.Node is ActionNodeViewModel mbActNode)
        {
            var pin = mbReadNode.Pins.FirstOrDefault(p =>
                p.Connector == source ||
                string.Equals(p.Id, source.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, source.Title, StringComparison.OrdinalIgnoreCase));
            if (pin != null && mbActNode.TargetTag != null)
            {
                pin.TargetTag = mbActNode.TargetTag;
            }
        }
        else if (source.Node is ModbusReadNodeViewModel mbReadToMqtt && target.Node is MqttPublishNodeViewModel mqttTarget)
        {
            var pin = mbReadToMqtt.Pins.FirstOrDefault(p =>
                p.Connector == source ||
                string.Equals(p.Id, source.Id, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Label, source.Title, StringComparison.OrdinalIgnoreCase));
            var targetField = mqttTarget.Fields.FirstOrDefault(fld =>
                fld.Connector == target ||
                string.Equals(fld.Key, target.Title, StringComparison.OrdinalIgnoreCase));
            if (pin != null && targetField?.Tag != null)
            {
                pin.TargetTag = targetField.Tag;
            }
        }

        // Tự động căn thẳng hàng ngang hai khối theo chân cắm nếu độ cao ban đầu tương đối gần nhau (chênh lệch Pin <= 50px)
        if (source.Node != null && target.Node != null)
        {
            double srcPinY = source.Node.Location.Y + GetConnectorVerticalOffset(source.Node, source);
            double tgtPinY = target.Node.Location.Y + GetConnectorVerticalOffset(target.Node, target);

            if (Math.Abs(srcPinY - tgtPinY) <= 50.0)
            {
                double alignedY = source.Node.Location.Y + GetConnectorVerticalOffset(source.Node, source) - GetConnectorVerticalOffset(target.Node, target);
                target.Node.Location = new Point(target.Node.Location.X, alignedY);
            }
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
            else if (conn.Target?.Node is ComparatorNodeViewModel cmpTarget)
            {
                cmpTarget.InputTag = null;
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
        DeselectAllConnections();
        if (_isSyncingSelection) return;

        _isSyncingSelection = true;
        try
        {
            foreach (var node in Nodes)
            {
                node.IsSelected = false;
            }
            SelectedNodes.Clear();
            SelectedNode = null;
            OnPropertyChanged(nameof(HasSelection));
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    public void SelectExclusive(GraphNodeViewModel node)
    {
        DeselectAllConnections();
        if (_isSyncingSelection) return;

        _isSyncingSelection = true;
        try
        {
            foreach (var n in Nodes)
            {
                if (n != node && n.IsSelected)
                {
                    n.IsSelected = false;
                }
            }
            SelectedNodes.Clear();
            node.IsSelected = true;
            SelectedNodes.Add(node);
            SelectedNode = node;
            OnPropertyChanged(nameof(HasSelection));
        }
        finally
        {
            _isSyncingSelection = false;
        }
    }

    public GraphNodeViewModel? CreateNodeInstance(string nodeType, TagModel? tag = null)
    {
        return nodeType switch
        {
            "Input" => new InputNodeViewModel(tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteInput)),
            "Trigger" => new TriggerNodeViewModel(),
            "Guard" => new GuardNodeViewModel(tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0") ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteInput)),
            "Action" => new ActionNodeViewModel(tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput)),
            "Timer" or "TON" => new TimerNodeViewModel("TON", tag, null),
            "TOF" => new TimerNodeViewModel("TOF", tag, null),
            "TP" => new TimerNodeViewModel("TP", tag, null),
            "CYCLE" or "Cycle" or "Cycle Timer" or "REPEAT_CYCLE" => new TimerNodeViewModel("CYCLE", tag, null),
            "Counter" or "CTU" => CreateCounterNode("CTU", tag),
            "CTD" => CreateCounterNode("CTD", tag),
            "Scale" => new ScaleNodeViewModel(
                tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput),
                null),
            "MqttPublish" or "MQTT" or "MqttPub" => new MqttPublishNodeViewModel(
                tag ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput || t.Kind == TagKind.DiscreteInput)),
            "MqttSubscribe" or "MqttSub" => new MqttSubscribeNodeViewModel(),
            "MB_READ" or "ModbusRead" or "Modbus" or "MbRead" or "MB_RD" => new ModbusReadNodeViewModel(),
            "MB_WRITE" or "ModbusWrite" or "MbWrite" or "MB_WR" => new ModbusWriteNodeViewModel(),
            "PULSE_OUT" or "PulseOut" or "PULSE" or "DIRECT_WRITE" or "DirectWrite" or "DirWrite" or "DIR_WR" => new DirectWriteNodeViewModel(),
            "DTU" or "DtuGateway" or "DTU_GATEWAY" or "Dtu" => new DtuGatewayNodeViewModel { ConfigureHandler = n => OpenDtuGatewayConfig(n) },
            "Comparator" or "CMP" => new ComparatorNodeViewModel(
                tag,
                CompareOp.GT,
                50,
                100),
            _ => null
        };
    }

    public void AddNode(string nodeType, Point position, TagModel? tag = null)
    {
        var newNode = CreateNodeInstance(nodeType, tag);
        if (newNode != null)
        {
            RecordSnapshot("Add Node");
            newNode.Location = position;

            Nodes.Add(newNode);
            SelectExclusive(newNode);
        }
    }

    public Point GetNextRuleSpawnPosition()
    {
        if (Nodes.Count == 0)
        {
            return new Point(60.0, 60.0);
        }

        // Điểm đáy của các khối hiện có (Location.Y + 86px chiều cao chuẩn của khối)
        double maxY = Nodes.Max(n => n.Location.Y + 86.0);

        // Khoảng cách giữa các Rule/Network là 54px (tổng bước nhảy là 86 + 54 = 140px, là bội số của lưới 20px)
        double nextY = Math.Round((maxY + 54.0) / 20.0) * 20.0;
        return new Point(60.0, nextY);
    }

    [RelayCommand]
    public void InsertNextConnectedNode(string nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            return;

        var newNode = CreateNodeInstance(nodeType);
        if (newNode == null)
            return;

        var currentSelected = SelectedNode;
        if (currentSelected != null && currentSelected.OutputConnectors.Count > 0 && newNode.InputConnectors.Count > 0)
        {
            var srcKind = GetNodeKind(currentSelected);
            var tgtKind = GetNodeKind(newNode);

            bool canConnect = srcKind != null && tgtKind != null &&
                              SimplePLC.Application.Logic.Graph.GraphGrammarV1.CanConnect(srcKind.Value, tgtKind.Value);

            if (canConnect)
            {
                RecordSnapshot("Insert Connected Node");

                var srcPin = currentSelected.OutputConnectors[0];
                var tgtPin = newNode.InputConnectors[0];

                double srcPinOffset = GetConnectorVerticalOffset(currentSelected, srcPin);
                double tgtPinOffset = GetConnectorVerticalOffset(newNode, tgtPin);

                // Đặt sang phải khối đang chọn: khoảng cách 240px (chuẩn lưới 20px)
                double newX = currentSelected.Location.X + 240.0;
                // Căn thẳng hàng ngang chân cắm tuyệt đối
                double newY = currentSelected.Location.Y + srcPinOffset - tgtPinOffset;

                newNode.Location = new Point(newX, newY);

                Nodes.Add(newNode);
                SelectExclusive(newNode);

                // Nối dây tự động giữa 2 cổng
                CompleteConnection((srcPin, tgtPin));
                return;
            }
        }

        // Nếu không có node chọn hoặc không thể nối tiếp: tách thành Rule mới ở hàng dưới
        RecordSnapshot("Insert Node");
        Point spawnPos = GetNextRuleSpawnPosition();
        newNode.Location = spawnPos;

        Nodes.Add(newNode);
        SelectExclusive(newNode);

        CompileAndSaveRules(true);
    }

    private CounterNodeViewModel CreateCounterNode(string mode, TagModel? tag)
    {
        var usedCvNames = Nodes.OfType<CounterNodeViewModel>()
            .Where(c => c.CvTag != null)
            .Select(c => c.CvTag!.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Quy ước: Counter i ưu tiên dùng tag COUNTER[i] làm CV để MCU hiển thị và đồng bộ
        var counterTags = TagCatalog.AllTags
            .Where(t => t.Kind == TagKind.Counter)
            .ToList();

        var cvTag = counterTags.FirstOrDefault(t => !usedCvNames.Contains(t.Name))
            ?? counterTags.FirstOrDefault()
            ?? TagCatalog.RegisterTags.FirstOrDefault(t => !usedCvNames.Contains(t.Name))
            ?? TagCatalog.RegisterTags.FirstOrDefault();

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
        // 1. Tìm các kết nối trực tiếp với khối này để căn gióng chân cắm
        var directConnections = Connections
            .Where(c => c.Source.Node == node || c.Target.Node == node)
            .ToList();

        bool snappedY = false;
        foreach (var conn in directConnections)
        {
            GraphNodeViewModel? other;
            ConnectorViewModel myConn;
            ConnectorViewModel otherConn;

            if (conn.Source.Node == node)
            {
                other = conn.Target.Node;
                myConn = conn.Source;
                otherConn = conn.Target;
            }
            else
            {
                other = conn.Source.Node;
                myConn = conn.Target;
                otherConn = conn.Source;
            }

            if (other == null) continue;

            // Tính vị trí Y lý tưởng của node để 2 chân cắm nối với nhau nằm trên cùng 1 đường chân trời (Horizontal Line)
            double otherPinOffset = GetConnectorVerticalOffset(other, otherConn);
            double myPinOffset = GetConnectorVerticalOffset(node, myConn);
            double idealY = other.Location.Y + otherPinOffset - myPinOffset;

            // Ngưỡng hít từ tính (Magnetic Snap Threshold): trong khoảng +-20px quanh đường thẳng tắp
            if (Math.Abs(node.Location.Y - idealY) <= 20.0)
            {
                node.Location = new Point(
                    Math.Round(node.Location.X),
                    idealY
                );
                snappedY = true;
                break;
            }
        }

        if (!snappedY)
        {
            // Giữ nguyên đúng vị trí người dùng đặt, làm tròn pixel nguyên để hiển thị sắc nét, không bị nhòe
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

            _isSyncingSelection = true;
            try
            {
                foreach (var node in nodesToDelete)
                {
                    node.PropertyChanged -= OnNodeSelectionPropertyChanged;
                    node.IsSelected = false;
                    SelectedNodes.Remove(node);
                    Nodes.Remove(node);
                }

                SelectedNode = SelectedNodes.LastOrDefault() ?? Nodes.LastOrDefault();
            }
            finally
            {
                _isSyncingSelection = false;
            }
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

        _isSyncingSelection = true;
        try
        {
            foreach (var node in Nodes)
            {
                node.PropertyChanged -= OnNodeSelectionPropertyChanged;
                node.IsSelected = false;
            }
            Connections.Clear();
            SelectedNodes.Clear();
            Nodes.Clear();
            SelectedNode = null;
            EditingRuleId = null;
        }
        finally
        {
            _isSyncingSelection = false;
        }

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
            pNode.TimeUnit = tm.TimeUnit;
            pNode.DurationValue = tm.DurationValue;
            pNode.TagName = tm.InputTag?.Name ?? string.Empty;
            pNode.OutputTagName = tm.OutputTag?.Name ?? string.Empty;
            pNode.OnTimeMs = tm.OnTimeMs;
            pNode.OffTimeMs = tm.OffTimeMs;
            pNode.StartWithOn = tm.StartWithOn;
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
        else if (node is ComparatorNodeViewModel cmp)
        {
            pNode.Type = "Comparator";
            pNode.CompareOp = cmp.CompareOp;
            pNode.ThresholdLo = cmp.ThresholdLo;
            pNode.ThresholdHi = cmp.ThresholdHi;
            pNode.TagName = cmp.InputTag?.Name ?? string.Empty;
        }
        else if (node is MqttPublishNodeViewModel mqtt)
        {
            pNode.Type = "MqttPublish";
            pNode.MqttTopic = mqtt.Topic;
            pNode.MqttPayloadMode = mqtt.PayloadMode;
            pNode.MqttCustomTemplate = mqtt.CustomTemplate;
            pNode.MqttQos = mqtt.Qos;
            pNode.MqttRetain = mqtt.Retain;
            pNode.MqttPublishMode = mqtt.PublishMode;
            pNode.MqttDeadband = mqtt.Deadband;
            pNode.MqttUnit = mqtt.Unit;
            pNode.MqttIncludeDevice = mqtt.IncludeDevice;
            pNode.MqttIncludeTimestamp = mqtt.IncludeTimestamp;
            pNode.MqttFields = mqtt.Fields.Select(f => new MqttPayloadFieldData
            {
                Key = f.Key,
                TagName = f.Tag?.Name ?? string.Empty,
                Unit = f.Unit
            }).ToList();
            pNode.TagName = mqtt.DataTag?.Name ?? string.Empty;
        }
        else if (node is MqttSubscribeNodeViewModel sub)
        {
            pNode.Type = "MqttSubscribe";
            pNode.MqttTopic = sub.Topic;
            pNode.MqttQos = sub.Qos;
            pNode.MqttSubQueueMode = sub.QueueMode.ToString();
            pNode.MqttSubIgnoreRetained = sub.IgnoreRetained;
            pNode.MqttSubFailSafeAction = sub.FailSafeAction.ToString();
            pNode.MqttSubTimeoutMs = sub.FailSafeTimeoutMs;
            pNode.MqttSubPins = sub.Pins.Select(p => new MqttSubPinData
            {
                Id = p.Id,
                Label = p.Label,
                JsonPath = p.JsonPath,
                DataType = p.DataType.ToString(),
                TargetTagIndex = p.TargetTagIndex,
                MinValue = p.MinValue,
                MaxValue = p.MaxValue,
                DefaultValue = p.DefaultValue
            }).ToList();
        }
        else if (node is ModbusReadNodeViewModel mb)
        {
            pNode.Type = "ModbusRead";
            pNode.ModbusPortIndex = mb.PortIndex;
            pNode.ModbusSlaveId = mb.SlaveId;
            pNode.ModbusFunctionCode = mb.FunctionCode.ToString();
            pNode.ModbusReadMode = mb.ReadMode.ToString();
            pNode.ModbusStartAddress = mb.StartAddress;
            pNode.ModbusRegCount = mb.RegCount;
            pNode.ModbusPollIntervalMs = mb.PollIntervalMs;
            pNode.ModbusTimeoutMs = mb.TimeoutMs;
            pNode.ModbusPins = mb.Pins.Select(p => new ModbusPinData
            {
                Id = p.Id,
                Label = p.Label,
                RegOffset = p.RegOffset,
                DataType = p.DataType.ToString(),
                Endianness = p.Endianness.ToString(),
                Multiplier = p.Multiplier,
                TargetTagName = p.TargetTag?.Name ?? string.Empty,
                TargetTagIndex = p.TargetTag != null ? (ushort?)p.TargetTag.Index : null
            }).ToList();
        }
        else if (node is ModbusWriteNodeViewModel mbw)
        {
            pNode.Type = "ModbusWrite";
            pNode.ModbusWritePortIndex = mbw.PortIndex;
            pNode.ModbusWriteSlaveId = mbw.SlaveId;
            pNode.ModbusWriteMode = mbw.WriteMode.ToString();
            pNode.ModbusWriteFunctionCode = mbw.FunctionCode.ToString();
            pNode.ModbusWriteStartAddress = mbw.StartAddress;
            pNode.ModbusWritePointCount = mbw.PointCount;
            pNode.ModbusWriteBaseAddressCommand = mbw.BaseAddressCommand;
            pNode.ModbusWriteBaseAddressParam = mbw.BaseAddressParam;
            pNode.ModbusWriteChannelCount = mbw.ChannelCount;
            pNode.ModbusWriteIsExclusiveChannel = mbw.IsExclusiveChannel;
            pNode.ModbusWriteTriggerMode = mbw.TriggerMode.ToString();
            pNode.ModbusWriteCyclicIntervalMs = mbw.CyclicIntervalMs;
            pNode.ModbusWriteTimeoutMs = mbw.TimeoutMs;
            pNode.ModbusWriteDeadband = mbw.Deadband;
            pNode.ModbusWritePins = mbw.Pins.Select(p => new ModbusPinData
            {
                Id = p.Id,
                Label = p.Label,
                RegOffset = p.RegOffset,
                DataType = p.DataType.ToString(),
                Endianness = p.Endianness.ToString(),
                Multiplier = p.Multiplier,
                TargetTagName = p.TargetTag?.Name ?? string.Empty,
                TargetTagIndex = p.TargetTag != null ? (ushort?)p.TargetTag.Index : null
            }).ToList();
        }
        else if (node is DirectWriteNodeViewModel dw)
        {
            pNode.Type = "DirectWrite";
            pNode.DirectWriteTargetPin = dw.TargetPin.ToString();
            pNode.DirectWriteMode = dw.Mode.ToString();
            pNode.DirectWriteChannelCount = dw.ChannelCount;
            pNode.DirectWriteIsExclusiveChannel = dw.IsExclusiveChannel;
            pNode.DirectWritePulseWidthUs = dw.PulseWidthUs;
            pNode.DirectWritePwmFrequencyHz = dw.PwmFrequencyHz;
            pNode.DirectWriteTriggerMode = dw.TriggerMode.ToString();
            pNode.DirectWriteDeadband = dw.Deadband;
            pNode.DirectWriteTimeoutMs = dw.TimeoutMs;
            pNode.DirectWriteDefaultColorRgb = dw.DefaultColorRgb;
        }
        else if (node is DtuGatewayNodeViewModel dtuNode)
        {
            pNode.Type = "DtuGateway";
            pNode.DtuPortIndex = dtuNode.PortIndex;
            pNode.DtuSerialMode = dtuNode.SerialMode;
            pNode.DtuUplinkMode = dtuNode.UplinkMode;
            pNode.DtuCustomJsonTemplate = dtuNode.CustomJsonTemplate;
            pNode.DtuHttpFields = dtuNode.HttpFields.ToList();
        }

        return pNode;
    }

    public GraphNodeViewModel? CreateNodeFromData(ProjectNodeData pNode)
    {
        GraphNodeViewModel? node = null;
        string type = pNode.Type ?? string.Empty;

        if (string.Equals(type, "Input", StringComparison.OrdinalIgnoreCase))
        {
            var tag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase))
                      ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteInput);
            node = new InputNodeViewModel(tag);
        }
        else if (string.Equals(type, "Trigger", StringComparison.OrdinalIgnoreCase))
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
        else if (string.Equals(type, "Guard", StringComparison.OrdinalIgnoreCase))
        {
            string guardTagName = !string.IsNullOrWhiteSpace(pNode.TargetTagName) ? pNode.TargetTagName : pNode.TagName;
            var tag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, guardTagName, StringComparison.OrdinalIgnoreCase))
                      ?? TagCatalog.AllTags.FirstOrDefault(t => t.Name == "VFLAG0");
            node = new GuardNodeViewModel(tag)
            {
                Negate = pNode.Negate
            };
        }
        else if (string.Equals(type, "Action", StringComparison.OrdinalIgnoreCase))
        {
            string actionTagName = !string.IsNullOrWhiteSpace(pNode.TargetTagName) ? pNode.TargetTagName : pNode.TagName;
            var tag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, actionTagName, StringComparison.OrdinalIgnoreCase))
                      ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput);
            node = new ActionNodeViewModel(tag)
            {
                ActionType = pNode.ActionType,
                ActionParam = pNode.ActionParam
            };
        }
        else if (string.Equals(type, "Timer", StringComparison.OrdinalIgnoreCase))
        {
            var inTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase));
            var qTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.OutputTagName, StringComparison.OrdinalIgnoreCase))
                       ?? TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput || t.Kind == TagKind.VirtualFlag);
            var timerNode = new TimerNodeViewModel(
                mode: string.IsNullOrWhiteSpace(pNode.TimerMode) ? "TON" : pNode.TimerMode,
                inTag: inTag,
                qTag: qTag,
                presetMs: pNode.PresetMs > 0 ? pNode.PresetMs : 3000,
                onTimeMs: pNode.OnTimeMs > 0 ? pNode.OnTimeMs : 1000,
                offTimeMs: pNode.OffTimeMs > 0 ? pNode.OffTimeMs : 1000,
                startWithOn: pNode.StartWithOn);
            if (!string.IsNullOrEmpty(pNode.TimeUnit))
            {
                timerNode.SetDuration(pNode.DurationValue > 0 ? pNode.DurationValue : (pNode.PresetMs / 1000.0), pNode.TimeUnit);
            }
            node = timerNode;
        }
        else if (string.Equals(type, "Counter", StringComparison.OrdinalIgnoreCase))
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
        else if (string.Equals(type, "Scale", StringComparison.OrdinalIgnoreCase))
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
        else if (string.Equals(type, "Comparator", StringComparison.OrdinalIgnoreCase) || string.Equals(type, "CMP", StringComparison.OrdinalIgnoreCase))
        {
            var inTag = !string.IsNullOrWhiteSpace(pNode.TagName)
                ? TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase))
                : null;
            node = new ComparatorNodeViewModel(inTag, pNode.CompareOp, pNode.ThresholdLo, pNode.ThresholdHi);
        }
        else if (string.Equals(type, "MqttPublish", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "MQTT", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "MqttPub", StringComparison.OrdinalIgnoreCase))
        {
            var dataTag = !string.IsNullOrWhiteSpace(pNode.TagName)
                ? TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, pNode.TagName, StringComparison.OrdinalIgnoreCase))
                : TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput || t.Kind == TagKind.DiscreteInput);

            var mqtt = new MqttPublishNodeViewModel(dataTag, string.IsNullOrWhiteSpace(pNode.MqttTopic) ? "synaptix/telemetry/data" : pNode.MqttTopic)
            {
                PayloadMode = string.IsNullOrWhiteSpace(pNode.MqttPayloadMode) ? "JSON" : pNode.MqttPayloadMode,
                CustomTemplate = string.IsNullOrWhiteSpace(pNode.MqttCustomTemplate) ? "{\n  \"device\": \"{device}\",\n  \"tag\": \"{tag}\",\n  \"value\": {val},\n  \"unit\": \"{unit}\",\n  \"ts\": {ts}\n}" : pNode.MqttCustomTemplate,
                Qos = pNode.MqttQos,
                Retain = pNode.MqttRetain,
                PublishMode = string.IsNullOrWhiteSpace(pNode.MqttPublishMode) ? "Event" : pNode.MqttPublishMode,
                Deadband = pNode.MqttDeadband,
                Unit = string.IsNullOrWhiteSpace(pNode.MqttUnit) ? "bar" : pNode.MqttUnit,
                IncludeDevice = pNode.MqttIncludeDevice,
                IncludeTimestamp = pNode.MqttIncludeTimestamp
            };

            if (pNode.MqttFields != null && pNode.MqttFields.Count > 0)
            {
                foreach (var fieldData in pNode.MqttFields)
                {
                    var fTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, fieldData.TagName, StringComparison.OrdinalIgnoreCase));
                    mqtt.AddFieldWithDetails(fieldData.Key, fTag, fieldData.Unit);
                }
            }

            node = mqtt;
        }
        else if (string.Equals(type, "MqttSubscribe", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "MqttSub", StringComparison.OrdinalIgnoreCase))
        {
            var sub = new MqttSubscribeNodeViewModel(string.IsNullOrWhiteSpace(pNode.MqttTopic) ? "spx/station/+/cmd" : pNode.MqttTopic)
            {
                Qos = pNode.MqttQos,
                IgnoreRetained = pNode.MqttSubIgnoreRetained,
                FailSafeTimeoutMs = pNode.MqttSubTimeoutMs
            };
            if (Enum.TryParse<MqttQueueMode>(pNode.MqttSubQueueMode, true, out var qm))
                sub.QueueMode = qm;
            if (Enum.TryParse<MqttFailSafeAction>(pNode.MqttSubFailSafeAction, true, out var fsa))
                sub.FailSafeAction = fsa;

            if (pNode.MqttSubPins != null && pNode.MqttSubPins.Count > 0)
            {
                sub.Pins.Clear();
                foreach (var p in pNode.MqttSubPins)
                {
                    var dt = Enum.TryParse<MqttDataType>(p.DataType, true, out var parsedDt) ? parsedDt : MqttDataType.Int16;
                    var binding = new MqttPinBindingViewModel(p.Label, p.JsonPath, dt, p.TargetTagIndex, p.MinValue, p.MaxValue, p.DefaultValue)
                    {
                        Id = p.Id
                    };
                    sub.Pins.Add(binding);
                }
            }

            node = sub;
        }
        else if (string.Equals(type, "MB_READ", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "ModbusRead", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "Modbus", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "MB_RD", StringComparison.OrdinalIgnoreCase))
        {
            var mb = new ModbusReadNodeViewModel
            {
                PortIndex = pNode.ModbusPortIndex,
                SlaveId = pNode.ModbusSlaveId == 0 ? (byte)1 : pNode.ModbusSlaveId,
                StartAddress = pNode.ModbusStartAddress,
                RegCount = pNode.ModbusRegCount == 0 ? (ushort)2 : pNode.ModbusRegCount,
                PollIntervalMs = pNode.ModbusPollIntervalMs == 0 ? 500u : pNode.ModbusPollIntervalMs,
                TimeoutMs = pNode.ModbusTimeoutMs == 0 ? 300u : pNode.ModbusTimeoutMs
            };
            if (Enum.TryParse<ModbusMasterFunctionCode>(pNode.ModbusFunctionCode, true, out var fc))
                mb.FunctionCode = fc;
            if (Enum.TryParse<ModbusReadMode>(pNode.ModbusReadMode, true, out var rm))
                mb.ReadMode = rm;

            if (pNode.ModbusPins != null && pNode.ModbusPins.Count > 0)
            {
                mb.Pins.Clear();
                foreach (var p in pNode.ModbusPins)
                {
                    var dt = Enum.TryParse<ModbusDataType>(p.DataType, true, out var parsedDt) ? parsedDt : ModbusDataType.Int16;
                    var ed = Enum.TryParse<ModbusEndianness>(p.Endianness, true, out var parsedEd) ? parsedEd : ModbusEndianness.BigEndian_ABCD;
                    var binding = new ModbusPinBindingViewModel
                    {
                        Id = p.Id,
                        Label = p.Label,
                        RegOffset = p.RegOffset,
                        DataType = dt,
                        Endianness = ed,
                        Multiplier = p.Multiplier == 0.0f ? 1.0f : p.Multiplier
                    };
                    if (!string.IsNullOrEmpty(p.TargetTagName))
                    {
                        binding.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, p.TargetTagName, StringComparison.OrdinalIgnoreCase));
                    }
                    if (binding.TargetTag == null && p.TargetTagIndex.HasValue)
                    {
                        binding.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == p.TargetTagIndex.Value);
                    }
                    mb.Pins.Add(binding);
                }
            }

            node = mb;
        }
        else if (string.Equals(type, "MB_WRITE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "ModbusWrite", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "MbWrite", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "MB_WR", StringComparison.OrdinalIgnoreCase))
        {
            var mbw = new ModbusWriteNodeViewModel
            {
                PortIndex = pNode.ModbusWritePortIndex,
                SlaveId = pNode.ModbusWriteSlaveId == 0 ? (byte)1 : pNode.ModbusWriteSlaveId,
                StartAddress = pNode.ModbusWriteStartAddress,
                PointCount = pNode.ModbusWritePointCount == 0 ? (ushort)1 : pNode.ModbusWritePointCount,
                BaseAddressCommand = pNode.ModbusWriteBaseAddressCommand == 0 ? (ushort)0x0100 : pNode.ModbusWriteBaseAddressCommand,
                BaseAddressParam = pNode.ModbusWriteBaseAddressParam == 0 ? (ushort)0x0120 : pNode.ModbusWriteBaseAddressParam,
                ChannelCount = pNode.ModbusWriteChannelCount == 0 ? (ushort)9 : pNode.ModbusWriteChannelCount,
                IsExclusiveChannel = pNode.ModbusWriteIsExclusiveChannel,
                CyclicIntervalMs = pNode.ModbusWriteCyclicIntervalMs == 0 ? 500u : pNode.ModbusWriteCyclicIntervalMs,
                TimeoutMs = pNode.ModbusWriteTimeoutMs == 0 ? 300u : pNode.ModbusWriteTimeoutMs
            };
            if (Enum.TryParse<ModbusWriteMode>(pNode.ModbusWriteMode, true, out var wm))
                mbw.WriteMode = wm;
            if (Enum.TryParse<ModbusMasterWriteFunctionCode>(pNode.ModbusWriteFunctionCode, true, out var fc))
                mbw.FunctionCode = fc;
            if (Enum.TryParse<ModbusWriteTriggerMode>(pNode.ModbusWriteTriggerMode, true, out var tm))
                mbw.TriggerMode = tm;
            if (pNode.ModbusWriteDeadband > 0f)
                mbw.Deadband = pNode.ModbusWriteDeadband;

            if (pNode.ModbusWritePins != null && pNode.ModbusWritePins.Count > 0)
            {
                mbw.Pins.Clear();
                foreach (var p in pNode.ModbusWritePins)
                {
                    var dt = Enum.TryParse<ModbusDataType>(p.DataType, true, out var parsedDt) ? parsedDt : ModbusDataType.Int16;
                    var ed = Enum.TryParse<ModbusEndianness>(p.Endianness, true, out var parsedEd) ? parsedEd : ModbusEndianness.BigEndian_ABCD;
                    var binding = new ModbusPinBindingViewModel
                    {
                        Id = p.Id,
                        Label = p.Label,
                        RegOffset = p.RegOffset,
                        DataType = dt,
                        Endianness = ed,
                        Multiplier = p.Multiplier == 0.0f ? 1.0f : p.Multiplier
                    };
                    if (!string.IsNullOrEmpty(p.TargetTagName))
                    {
                        binding.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, p.TargetTagName, StringComparison.OrdinalIgnoreCase));
                    }
                    if (binding.TargetTag == null && p.TargetTagIndex.HasValue)
                    {
                        binding.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => t.Index == p.TargetTagIndex.Value);
                    }
                    mbw.Pins.Add(binding);
                }
            }

            mbw.SyncConnectors();
            node = mbw;
        }
        else if (string.Equals(type, "PULSE_OUT", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "PulseOut", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "PULSE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "DIRECT_WRITE", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "DirectWrite", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "DirWrite", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "DIR_WR", StringComparison.OrdinalIgnoreCase))
        {
            var dw = new DirectWriteNodeViewModel
            {
                ChannelCount = pNode.DirectWriteChannelCount == 0 ? (ushort)18 : pNode.DirectWriteChannelCount,
                IsExclusiveChannel = pNode.DirectWriteIsExclusiveChannel,
                PulseWidthUs = pNode.DirectWritePulseWidthUs == 0 ? 50u : pNode.DirectWritePulseWidthUs,
                PwmFrequencyHz = pNode.DirectWritePwmFrequencyHz == 0 ? 1000u : pNode.DirectWritePwmFrequencyHz,
                TimeoutMs = pNode.DirectWriteTimeoutMs == 0 ? 300u : pNode.DirectWriteTimeoutMs,
                DefaultColorRgb = pNode.DirectWriteDefaultColorRgb == 0 ? 0x00FF00 : pNode.DirectWriteDefaultColorRgb
            };
            if (Enum.TryParse<DirectWriteTargetPin>(pNode.DirectWriteTargetPin, true, out var tp))
                dw.TargetPin = tp;
            if (Enum.TryParse<DirectWriteMode>(pNode.DirectWriteMode, true, out var wm))
                dw.Mode = wm;
            if (Enum.TryParse<DirectWriteTriggerMode>(pNode.DirectWriteTriggerMode, true, out var tm))
                dw.TriggerMode = tm;
            if (pNode.DirectWriteDeadband > 0f)
                dw.Deadband = pNode.DirectWriteDeadband;

            dw.SynchronizeConnectors();
            node = dw;
        }
        else if (string.Equals(type, "DTU", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "DtuGateway", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(type, "DTU_GATEWAY", StringComparison.OrdinalIgnoreCase))
        {
            var dtuNode = new DtuGatewayNodeViewModel
            {
                PortIndex = pNode.DtuPortIndex,
                SerialMode = string.IsNullOrEmpty(pNode.DtuSerialMode) ? "Transference" : pNode.DtuSerialMode,
                UplinkMode = string.IsNullOrEmpty(pNode.DtuUplinkMode) ? "Mqtt" : pNode.DtuUplinkMode,
                CustomJsonTemplate = pNode.DtuCustomJsonTemplate ?? string.Empty
            };
            if (pNode.DtuHttpFields != null)
            {
                dtuNode.HttpFields.Clear();
                foreach (var f in pNode.DtuHttpFields)
                {
                    dtuNode.HttpFields.Add(new DtuHttpFieldModel { Key = f.Key, ValueSource = f.ValueSource, CustomValue = f.CustomValue });
                }
            }
            dtuNode.ConfigureHandler = n => OpenDtuGatewayConfig(n);
            node = dtuNode;
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
                if (!string.IsNullOrEmpty(pConn.TargetConnectorTitle))
                {
                    tgtConn = tgtNode.InputConnectors.FirstOrDefault(c => string.Equals(c.Title, pConn.TargetConnectorTitle, StringComparison.OrdinalIgnoreCase));
                }
                if (tgtConn == null && pConn.TargetConnectorIndex >= 0 && pConn.TargetConnectorIndex < tgtNode.InputConnectors.Count)
                {
                    tgtConn = tgtNode.InputConnectors[pConn.TargetConnectorIndex];
                }
                else if (tgtConn == null)
                {
                    tgtConn = tgtNode.InputConnectors.FirstOrDefault();
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

        RecordSnapshot("Reconstruct Graph From Rule");

        var nodesData = new List<ProjectNodeData>();
        var connsData = new List<ProjectConnectionData>();

        double x = 60;
        double y = Nodes.Count > 0 ? (Nodes.Max(n => n.Location.Y) + 160) : 100;

        string suffix = Guid.NewGuid().ToString("N")[..8];

        // 1. Khối Input cho Trigger
        string inputId = $"reconstruct_in_trg_{suffix}";
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
        string triggerId = $"reconstruct_trg_{suffix}";
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
            string guardId = $"reconstruct_grd_{suffix}";
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
            string actionId = $"reconstruct_act_{suffix}";
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

        // Tái tạo không phá hủy (Non-destructive): Thêm node vào canvas hiện có thay vì xóa sạch Nodes.Clear()
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

        foreach (var pConn in connsData)
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
    /// Tự động tái tạo toàn bộ danh mục Rules thành các mạng Network độc lập trên Canvas (chuẩn TIA Portal).
    /// Mỗi Rule được bố trí thành một hàng ngang (Input -> Trigger -> Guard -> Action) và cách nhau 260px theo trục Y.
    /// </summary>
    public void ReconstructGraphFromAllRules(IEnumerable<RuleItemModel> rules)
    {
        if (rules == null) return;
        var ruleList = rules.ToList();
        if (ruleList.Count == 0) return;

        RecordSnapshot("Reconstruct All Rules to Canvas");

        Connections.Clear();
        Nodes.Clear();
        SelectedNode = null;

        double startX = 60;
        double currentY = 100;

        foreach (var rule in ruleList)
        {
            // Nếu rule đã có sẵn SourceNodes và SourceConnections riêng thì ưu tiên phục hồi nguyên trạng
            if (rule.SourceNodes != null && rule.SourceNodes.Count > 0)
            {
                var localMap = new Dictionary<string, GraphNodeViewModel>();
                foreach (var pNode in rule.SourceNodes)
                {
                    var node = CreateNodeFromData(pNode);
                    if (node != null)
                    {
                        Nodes.Add(node);
                        localMap[node.Id] = node;
                    }
                }
                if (rule.SourceConnections != null)
                {
                    foreach (var pConn in rule.SourceConnections)
                    {
                        if (localMap.TryGetValue(pConn.SourceNodeId, out var srcNode) &&
                            localMap.TryGetValue(pConn.TargetNodeId, out var tgtNode))
                        {
                            var srcConn = (pConn.SourceConnectorIndex >= 0 && pConn.SourceConnectorIndex < srcNode.OutputConnectors.Count)
                                ? srcNode.OutputConnectors[pConn.SourceConnectorIndex]
                                : srcNode.OutputConnectors.FirstOrDefault();
                            var tgtConn = (pConn.TargetConnectorIndex >= 0 && pConn.TargetConnectorIndex < tgtNode.InputConnectors.Count)
                                ? tgtNode.InputConnectors[pConn.TargetConnectorIndex]
                                : tgtNode.InputConnectors.FirstOrDefault();
                            if (srcConn != null && tgtConn != null)
                            {
                                Connect(srcConn, tgtConn);
                            }
                        }
                    }
                }
                currentY = (Nodes.Count > 0 ? Nodes.Max(n => n.Location.Y) : currentY) + 260;
                continue;
            }

            // Nếu rule chưa có SourceNodes, tự động sinh chuỗi Input -> Trigger -> Guard -> Action chuẩn
            string suffix = Guid.NewGuid().ToString("N")[..8];
            var ruleNodes = new List<ProjectNodeData>();
            var ruleConns = new List<ProjectConnectionData>();

            string inputId = $"net_in_{rule.Id}_{suffix}";
            if (rule.TriggerTag != null)
            {
                ruleNodes.Add(new ProjectNodeData
                {
                    Id = inputId,
                    Type = "Input",
                    TagName = rule.TriggerTag.Name,
                    LocationX = startX,
                    LocationY = currentY
                });
            }

            string triggerId = $"net_trg_{rule.Id}_{suffix}";
            ruleNodes.Add(new ProjectNodeData
            {
                Id = triggerId,
                Type = "Trigger",
                TriggerType = rule.TriggerType,
                ForMs = rule.ForMs,
                CompareOp = rule.CompareOp,
                ThresholdLo = rule.ThresholdLo,
                ThresholdHi = rule.ThresholdHi,
                LocationX = startX + 240,
                LocationY = currentY
            });

            if (rule.TriggerTag != null)
            {
                ruleConns.Add(new ProjectConnectionData
                {
                    SourceNodeId = inputId,
                    SourceConnectorIndex = 0,
                    TargetNodeId = triggerId,
                    TargetConnectorIndex = 0
                });
            }

            string lastNodeId = triggerId;
            int lastNodeOutIdx = 0;

            bool hasValidGuard = rule.GuardTag != null &&
                                 rule.GuardTag.Kind != TagKind.None &&
                                 !string.Equals(rule.GuardTag.Name, "NONE", StringComparison.OrdinalIgnoreCase);

            if (hasValidGuard && rule.GuardTag != null)
            {
                string guardId = $"net_grd_{rule.Id}_{suffix}";
                ruleNodes.Add(new ProjectNodeData
                {
                    Id = guardId,
                    Type = "Guard",
                    TargetTagName = rule.GuardTag.Name,
                    Negate = rule.GuardNegated,
                    LocationX = startX + 480,
                    LocationY = currentY
                });

                ruleConns.Add(new ProjectConnectionData
                {
                    SourceNodeId = lastNodeId,
                    SourceConnectorIndex = lastNodeOutIdx,
                    TargetNodeId = guardId,
                    TargetConnectorIndex = 0
                });

                lastNodeId = guardId;
                lastNodeOutIdx = 0;
            }

            if (rule.ActionTag != null && rule.ActionTag.Kind != TagKind.None)
            {
                string actionId = $"net_act_{rule.Id}_{suffix}";
                double actionX = hasValidGuard ? (startX + 720) : (startX + 480);
                ruleNodes.Add(new ProjectNodeData
                {
                    Id = actionId,
                    Type = "Action",
                    ActionType = rule.ActionType,
                    TargetTagName = rule.ActionTag.Name,
                    ActionParam = rule.ActionParam,
                    LocationX = actionX,
                    LocationY = currentY
                });

                ruleConns.Add(new ProjectConnectionData
                {
                    SourceNodeId = lastNodeId,
                    SourceConnectorIndex = lastNodeOutIdx,
                    TargetNodeId = actionId,
                    TargetConnectorIndex = 0
                });
            }

            var localCreated = new Dictionary<string, GraphNodeViewModel>();
            foreach (var pNode in ruleNodes)
            {
                var node = CreateNodeFromData(pNode);
                if (node != null)
                {
                    Nodes.Add(node);
                    localCreated[node.Id] = node;
                }
            }

            foreach (var pConn in ruleConns)
            {
                if (localCreated.TryGetValue(pConn.SourceNodeId, out var srcNode) &&
                    localCreated.TryGetValue(pConn.TargetNodeId, out var tgtNode))
                {
                    var srcConn = (pConn.SourceConnectorIndex >= 0 && pConn.SourceConnectorIndex < srcNode.OutputConnectors.Count)
                        ? srcNode.OutputConnectors[pConn.SourceConnectorIndex]
                        : srcNode.OutputConnectors.FirstOrDefault();
                    var tgtConn = (pConn.TargetConnectorIndex >= 0 && pConn.TargetConnectorIndex < tgtNode.InputConnectors.Count)
                        ? tgtNode.InputConnectors[pConn.TargetConnectorIndex]
                        : tgtNode.InputConnectors.FirstOrDefault();
                    if (srcConn != null && tgtConn != null)
                    {
                        Connect(srcConn, tgtConn);
                    }
                }
            }

            currentY += 260;
        }

        CompileAndSaveRules(true);
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
                TargetTagName = dn.TagName,
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
                OutputTagName = dn.OutputTagName,
                Gain = dn.Gain,
                Offset = dn.Offset,
                Unit = string.IsNullOrWhiteSpace(dn.Unit) ? "bar" : dn.Unit,
                DecimalPlaces = dn.DecimalPlaces,
                IsClamped = dn.IsClamped,
                ClampMin = dn.ClampMin,
                ClampMax = dn.ClampMax
            };

            // Fix Macro Binding:
            // For Timer and Counter nodes, check if TagName == "NONE" or empty; if so, assign dn.InputTagName so the node has a valid input tag
            bool isTimerOrCounter = string.Equals(dn.NodeType, "TIMER", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(dn.NodeType, "COUNTER", StringComparison.OrdinalIgnoreCase);
            if (isTimerOrCounter && (string.IsNullOrEmpty(pNode.TagName) || pNode.TagName == "NONE") &&
                !string.IsNullOrEmpty(dn.InputTagName) && dn.InputTagName != "NONE")
            {
                pNode.TagName = dn.InputTagName;
            }

            // For Scale nodes, also ensure valid InputTagName if TagName was NONE or empty
            bool isScale = string.Equals(dn.NodeType, "SCALE", StringComparison.OrdinalIgnoreCase);
            if (isScale && (string.IsNullOrEmpty(pNode.TagName) || pNode.TagName == "NONE") &&
                !string.IsNullOrEmpty(dn.InputTagName) && dn.InputTagName != "NONE")
            {
                pNode.TagName = dn.InputTagName;
            }

            var node = CreateNodeFromData(pNode);
            if (node != null)
            {
                node.IsGhost = true;
                node.DiffStatus = DiffState.Added;

                // Explicitly map Scale properties to ScaleNodeViewModel
                if (node is ScaleNodeViewModel scaleVm)
                {
                    scaleVm.Gain = dn.Gain;
                    scaleVm.Offset = dn.Offset;
                    scaleVm.Unit = string.IsNullOrWhiteSpace(dn.Unit) ? "bar" : dn.Unit;
                    scaleVm.DecimalPlaces = dn.DecimalPlaces;
                    scaleVm.IsClamped = dn.IsClamped;
                    scaleVm.ClampMin = dn.ClampMin;
                    scaleVm.ClampMax = dn.ClampMax;
                }

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

        // Thực hiện biên dịch rõ ràng (Explicit Compile) để tự động sinh và nạp Rule vào Bảng Rule (Rule Table)
        CompileAndSaveRules(false);
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
        IsGraphDeployed = false;
        SaveStatusMessage = LocalizationService.Tr("SaveStatusStale");

        if (IsLiveDebugActive)
        {
            IsLiveDebugActive = false;
            ClearSimVisuals();
            SimGateWarning = LocalizationService.Tr("LiveDebugDeployRequiredWarning");
        }

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

        var ports = GetProjectMetadata?.Invoke()?.ModbusPorts;
        var result = _compiler.Compile(activeNodes, activeConnections, TagCatalog, modbusPorts: ports);

        if (isAutoSync)
        {
            UpdateNetworkNumbersOnNodes();

            int totalDeployable = result.Rules.Count + 
                (result.Program?.FunctionBlockTimers.Count ?? 0) + 
                (result.Program?.FunctionBlockCounters.Count ?? 0) + 
                (result.Program?.MqttTelemetryConfigs.Count ?? 0) +
                (result.Program?.MqttSubscriptionConfigs.Count ?? 0) +
                (result.Program?.ModbusMasterConfigs.Count ?? 0) +
                (result.Program?.ModbusMasterWriteConfigs.Count ?? 0) +
                (result.Program?.DirectWriteConfigs.Count ?? 0);

            // Trong chế độ tự động đồng bộ khi vẽ (Authoring / Editing mode theo chuẩn TIA Portal / CODESYS):
            // Nếu toàn bộ mạch đã hoàn chỉnh và hợp lệ: tự động cập nhật Program để sẵn sàng
            if (result.IsValid && totalDeployable > 0)
            {
                Diagnostics = Array.Empty<Diagnostic>();
                OnPropertyChanged(nameof(Diagnostics));
                CompileState = CompileState.Valid;
                CurrentProgram = result.Program;
                LastSuccessfulProgram = result.Program;
                ValidRuleCount = result.Rules.Count > 0 ? result.Rules.Count : totalDeployable;
                SimGateWarning = string.Empty;

                // Nếu chương trình logic thực thi có cùng mã băm với bản đã nạp gần nhất (LastDeployedProgramHash)
                // (ví dụ: chỉ thay đổi vị trí các khối X, Y hoặc căn gióng đồ họa), duy trì trạng thái đã nạp!
                string currentHash = result.Program?.ComputeSemanticHash() ?? string.Empty;
                if (!string.IsNullOrEmpty(LastDeployedProgramHash) && string.Equals(currentHash, LastDeployedProgramHash, StringComparison.Ordinal))
                {
                    IsGraphDeployed = true;
                }
                IsSaved = true;
                SaveStatusMessage = result.Rules.Count > 0
                    ? string.Format(LocalizationService.Tr("SaveStatusValid"), result.Rules.Count)
                    : (LocalizationService.Instance.IsVietnamese 
                        ? $"✓ {totalDeployable} khối chức năng (FBs/MQTT) sẵn sàng nạp" 
                        : $"✓ {totalDeployable} function/MQTT blocks ready");
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

        int totalDeployableExplicit = result.Rules.Count + 
            (result.Program?.FunctionBlockTimers.Count ?? 0) + 
            (result.Program?.FunctionBlockCounters.Count ?? 0) + 
            (result.Program?.MqttTelemetryConfigs.Count ?? 0) +
            (result.Program?.MqttSubscriptionConfigs.Count ?? 0) +
            (result.Program?.ModbusMasterConfigs.Count ?? 0) +
            (result.Program?.ModbusMasterWriteConfigs.Count ?? 0) +
            (result.Program?.DirectWriteConfigs.Count ?? 0);

        if (totalDeployableExplicit == 0)
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
        ValidRuleCount = result.Rules.Count > 0 ? result.Rules.Count : totalDeployableExplicit;
        SimGateWarning = string.Empty;

        // Bảo toàn cờ IsGraphDeployed nếu bytecode logic không đổi so với bản đã nạp
        string currentExplicitHash = result.Program?.ComputeSemanticHash() ?? string.Empty;
        if (!string.IsNullOrEmpty(LastDeployedProgramHash) && string.Equals(currentExplicitHash, LastDeployedProgramHash, StringComparison.Ordinal))
        {
            IsGraphDeployed = true;
        }

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
            _ruleTable.UpdateSubsystemsFromProgram(result.Program);
        }

        UpdateNetworkNumbersOnNodes();

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

    /// <summary>
    /// Tự động phát hiện và đánh số thứ tự Network (Rule #1, #2, #3...) chuẩn Siemens TIA Portal
    /// cho các khối đứng đầu chuỗi logic (Input/Trigger/Timer/Counter/Scale không có upstream)
    /// sắp xếp theo thứ tự quét chu kỳ PLC từ trên xuống dưới (Location.Y).
    /// </summary>
    public void UpdateNetworkNumbersOnNodes()
    {
        // 1. Xóa số Network cũ trên toàn bộ node
        foreach (var node in Nodes)
        {
            node.NetworkNumber = 0;
        }

        // 2. Tìm tất cả các khối đầu chuỗi (không có dây nối vào InputConnectors của nó)
        var headNodes = Nodes.Where(n => !n.IsGhost &&
            (n.InputConnectors.Count == 0 || !Connections.Any(c => c.Target?.Node == n)))
            .OrderBy(n => n.Location.Y)
            .ThenBy(n => n.Location.X)
            .ToList();

        // 3. Đánh số Network thứ tự 1, 2, 3... cho các khối đầu chuỗi
        int currentNetwork = 1;
        foreach (var headNode in headNodes)
        {
            headNode.NetworkNumber = currentNetwork++;
        }
    }

    #region Canvas-Centric Simulation

    [RelayCommand]
    public void StartSimulation()
    {
        if (CompileState != CompileState.Valid || CurrentProgram == null || ValidRuleCount == 0)
        {
            CompileAndSaveRules(isAutoSync: false);
        }

        if (CompileState != CompileState.Valid || CurrentProgram == null || ValidRuleCount == 0)
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
            if (CompileState != CompileState.Valid || CurrentProgram == null || ValidRuleCount == 0)
            {
                CompileAndSaveRules(isAutoSync: false);
            }

            if (CompileState != CompileState.Valid || CurrentProgram == null || ValidRuleCount == 0)
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
            if (CompileState != CompileState.Valid || CurrentProgram == null || ValidRuleCount == 0)
            {
                CompileAndSaveRules(isAutoSync: false);
            }

            if (CompileState != CompileState.Valid || CurrentProgram == null || ValidRuleCount == 0)
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
        if (IsLiveDebugActive)
        {
            ApplyLiveOnlinePropagation();
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

            if (IsLiveDebugActive)
            {
                ApplyLiveOnlinePropagation();
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
                HasInputChange = prevVal != currVal,
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

        // Kích hoạt Input và dây nối trực tiếp sang Comparator
        foreach (var cmpNode in Nodes.OfType<ComparatorNodeViewModel>())
        {
            var cmpConn = Connections.FirstOrDefault(c => c.Target?.Node == cmpNode);
            if (cmpConn?.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
            {
                cmpNode.InputTag = inp.Tag;
                cmpConn.IsActive = inp.Tag.Value != 0;
            }
            else if (cmpConn?.Source?.Node is ScaleNodeViewModel scl && (scl.OutputTag != null || scl.InputTag != null))
            {
                cmpNode.InputTag = scl.OutputTag ?? scl.InputTag;
                cmpConn.IsActive = scl.IsLiveActive;
            }

            if (cmpNode.InputTag != null)
            {
                cmpNode.IsConditionMet = cmpNode.Evaluate(cmpNode.InputTag.Value);
                foreach (var outConn in Connections.Where(c => c.Source?.Node == cmpNode))
                {
                    outConn.IsActive = cmpNode.IsConditionMet;
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
            // 1. Upstream connection highlight (supports Input, Trigger, Guard)
            var timerConn = Connections.FirstOrDefault(c => c.Target?.Node == timerNode);
            if (timerConn?.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
            {
                timerConn.IsActive = inp.Tag.Value != 0;
            }
            else if (timerConn?.Source?.Node is TriggerNodeViewModel trigVm)
            {
                timerConn.IsActive = trigVm.IsSimTriggered;
            }
            else if (timerConn?.Source?.Node is GuardNodeViewModel guardVm)
            {
                timerConn.IsActive = guardVm.IsSimPassed;
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

            timerNode.IsSimActive = timerNode.IsLiveActive || timerNode.IsTiming;
        }
    }

    private void ApplyCounterSimVisuals(RuntimeSnapshot snapshot)
    {
        if (CurrentProgram == null) return;

        foreach (var counterNode in Nodes.OfType<CounterNodeViewModel>())
        {
            // 1. Upstream connections highlight (CU and R; supports Input, Trigger, Guard)
            foreach (var counterConn in Connections.Where(c => c.Target?.Node == counterNode))
            {
                if (counterConn.Source?.Node is InputNodeViewModel inp && inp.Tag != null)
                {
                    counterConn.IsActive = inp.Tag.Value != 0;
                }
                else if (counterConn.Source?.Node is TriggerNodeViewModel trigVm)
                {
                    counterConn.IsActive = trigVm.IsSimTriggered;
                }
                else if (counterConn.Source?.Node is GuardNodeViewModel guardVm)
                {
                    counterConn.IsActive = guardVm.IsSimPassed;
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

            counterNode.IsSimActive = qActive;
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

        _liveTimerStartTimes.Clear();
        _liveTimerPrevIn.Clear();
        _liveTriggerPrevIn.Clear();
        _liveTriggerPulseTimes.Clear();

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
            else if (node is MqttPublishNodeViewModel mqtt)
            {
                mqtt.IsLiveActive = false;
                mqtt.HasPublishError = false;
            }
            else if (node is MqttSubscribeNodeViewModel sub)
            {
                sub.IsLiveActive = false;
                sub.IsValidActive = false;
                sub.HasSubscribeError = false;
            }
            else if (node is DirectWriteNodeViewModel dw)
            {
                dw.IsLiveActive = false;
                dw.IsDoneActive = false;
                dw.IsBusyActive = false;
                dw.IsErrorActive = false;
            }
            else if (node is ModbusReadNodeViewModel mbr)
            {
                mbr.IsLiveActive = false;
                mbr.IsValidActive = false;
                mbr.IsErrorActive = false;
            }
            else if (node is ModbusWriteNodeViewModel mbw)
            {
                mbw.IsLiveActive = false;
                mbw.IsDoneActive = false;
                mbw.IsBusyActive = false;
                mbw.IsErrorActive = false;
            }
        }
    }

    #endregion
}
