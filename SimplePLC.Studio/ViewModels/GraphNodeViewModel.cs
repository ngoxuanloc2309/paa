using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Domain.Enums;

namespace SimplePLC.Studio.ViewModels;

public enum NodeLiveVisualState
{
    Offline,
    Stale,
    Active,
    Inactive
}

public abstract partial class GraphNodeViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = "Node";

    [ObservableProperty]
    private string _icon = "⚡";

    [ObservableProperty]
    private Point _location;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isSimActive;

    [ObservableProperty]
    private bool _isGhost;

    [ObservableProperty]
    private DiffState _diffStatus = DiffState.None;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private bool _hasWarning;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private string _errorField = string.Empty;

    public void ClearDiagnostics()
    {
        HasError = false;
        HasWarning = false;
        ErrorMessage = string.Empty;
        ErrorField = string.Empty;
        foreach (var c in InputConnectors) c.ClearDiagnostics();
        foreach (var c in OutputConnectors) c.ClearDiagnostics();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string _customLabel = string.Empty;

    public virtual string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel) ? Title : CustomLabel;

    public ObservableCollection<ConnectorViewModel> InputConnectors { get; } = new();
    public ObservableCollection<ConnectorViewModel> OutputConnectors { get; } = new();

    public abstract string SummaryText { get; }

    protected GraphNodeViewModel()
    {
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    protected virtual void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(SummaryText));
    }

    public virtual void Dispose()
    {
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
        GC.SuppressFinalize(this);
    }
}

public partial class InputNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    public ushort? BoundTagIndex => (Tag != null && Tag.Kind != TagKind.None && Tag.Index < 65535) ? (ushort)Tag.Index : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveBadgeText))]
    private int _liveRawValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveBadgeText))]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveBadgeText))]
    private bool _isLiveOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isLiveActive;

    [ObservableProperty]
    private string _liveValueText = "--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveBadgeText))]
    private bool _hasReceivedUpdate;

    public NodeLiveVisualState LiveVisualState
    {
        get
        {
            if (!IsLiveOnline) return NodeLiveVisualState.Offline;
            if (LiveQuality == TagQuality.Stale) return NodeLiveVisualState.Stale;
            if (IsLiveActive) return NodeLiveVisualState.Active;
            return NodeLiveVisualState.Inactive;
        }
    }
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(TagCode))]
    [NotifyPropertyChangedFor(nameof(TagKindText))]
    [NotifyPropertyChangedFor(nameof(TagShortText))]
    [NotifyPropertyChangedFor(nameof(TagDescriptionText))]
    [NotifyPropertyChangedFor(nameof(ValueBadgeText))]
    [NotifyPropertyChangedFor(nameof(LiveBadgeText))]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private TagModel? _tag;

    public string TagCode => Tag?.Name ?? "TAG";

    public string TagKindText => Tag?.Kind switch
    {
        TagKind.DiscreteInput => "DI",
        TagKind.DiscreteOutput => "DO",
        TagKind.AnalogInput => "AI",
        TagKind.VirtualFlag => "VFLAG",
        TagKind.VirtualRegister => "VREG",
        TagKind.ModbusCoil => "MB_COIL",
        TagKind.ModbusHolding => "MB_HOLDING",
        TagKind.VirtualRegisterRetain => "VREG_R",
        TagKind.Counter => "COUNTER",
        _ => "DI"
    };

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel) 
        ? TagCode 
        : CustomLabel;

    public string TagDescriptionText
    {
        get
        {
            if (Tag == null) 
                return LocalizationService.Tr("NarrNoTag");
            
            if (!string.IsNullOrWhiteSpace(Tag.Alias))
                return Tag.Alias;

            return LocalizationService.Instance.IsVietnamese 
                ? $"Ngõ vào {Tag.Kind}" 
                : $"{Tag.Kind} Input";
        }
    }

    public string ValueBadgeText
    {
        get
        {
            if (Tag == null) return "—";
            if (Tag.IsDigital)
            {
                return Tag.Value != 0
                    ? $"● {LocalizationService.Tr("NodeLiveOn")}"
                    : $"○ {LocalizationService.Tr("NodeLiveOff")}";
            }
            return Tag.Value.ToString();
        }
    }

    public string LiveBadgeText
    {
        get
        {
            if (!IsLiveOnline)
            {
                if (HasReceivedUpdate)
                {
                    string valStr = (Tag?.IsDigital ?? true)
                        ? (LiveRawValue != 0 ? LocalizationService.Tr("NodeLiveOn") : LocalizationService.Tr("NodeLiveOff"))
                        : LiveRawValue.ToString("N0");
                    return string.Format(LocalizationService.Tr("NodeLastKnown"), valStr);
                }
                return ValueBadgeText;
            }

            if (Tag != null && !Tag.IsDigital)
            {
                string suffix = LiveQuality == TagQuality.Stale ? " ⚠️" : "";
                return $"{LiveRawValue:N0}{suffix}";
            }

            string onText = $"● {LocalizationService.Tr("NodeLiveOn")}";
            string offText = $"○ {LocalizationService.Tr("NodeLiveOff")}";
            if (LiveQuality == TagQuality.Stale)
            {
                return LiveRawValue != 0 ? $"{onText} ⚠️" : $"{offText} ⚠️";
            }

            return LiveRawValue != 0 ? onText : offText;
        }
    }

    public string TagShortText => TagDescriptionText;

    public override string SummaryText => Tag != null 
        ? Tag.DisplayName 
        : LocalizationService.Tr("NarrNoTag");

    public InputNodeViewModel(TagModel? tag = null)
    {
        Title = tag?.Name ?? "DI0";
        Icon = "🏷️";
        Tag = tag;
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", IsInput = false, IsEvent = false, Node = this });
    }

    [RelayCommand]
    public void ToggleSimInput()
    {
        if (Tag == null) return;
        if (Tag.IsDigital)
        {
            Tag.Value = Tag.Value == 0 ? 1 : 0;
            IsLiveActive = Tag.Value != 0;
            IsSimActive = Tag.Value != 0;
        }
        else
        {
            // For analog (AI) or register (VREG), cycle test values: 0 -> 50 -> 90 -> 100 -> 0
            Tag.Value = Tag.Value switch
            {
                < 50 => 50,
                < 90 => 90,
                < 100 => 100,
                _ => 0
            };
            IsLiveActive = Tag.Value > 0;
            IsSimActive = Tag.Value > 0;
        }
        OnPropertyChanged(nameof(ValueBadgeText));
        OnPropertyChanged(nameof(LiveBadgeText));
    }

    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        HasReceivedUpdate = true;
        LiveRawValue = snapshot.RawValue;
        LiveQuality = snapshot.Quality;

        // IsLiveActive strictly boolean
        bool isBoolean = snapshot.DataType == TagDataType.Boolean || 
                         snapshot.Kind is TagKind.DiscreteInput or TagKind.DiscreteOutput or TagKind.VirtualFlag ||
                         (Tag?.IsDigital == true);
        IsLiveActive = isBoolean && snapshot.RawValue != 0;
        LiveValueText = snapshot.RawValue.ToString();

        OnPropertyChanged(nameof(LiveBadgeText));
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        OnPropertyChanged(nameof(LiveBadgeText));
        OnPropertyChanged(nameof(LiveVisualState));
    }

    partial void OnTagChanged(TagModel? oldValue, TagModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnTagPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnTagPropertyChanged;
        }
        Title = newValue?.Name ?? "DI0";
        OnPropertyChanged(nameof(TagCode));
        OnPropertyChanged(nameof(TagKindText));
        OnPropertyChanged(nameof(TagShortText));
        OnPropertyChanged(nameof(TagDescriptionText));
        OnPropertyChanged(nameof(ValueBadgeText));
        OnPropertyChanged(nameof(LiveBadgeText));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(SimButtonContent));
    }

    public string SimButtonContent => (Tag != null && Tag.Value == 1)
        ? LocalizationService.Tr("SimBtnOn1")
        : LocalizationService.Tr("SimBtnOff0");

    private void OnTagPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(TagCode));
        OnPropertyChanged(nameof(TagKindText));
        OnPropertyChanged(nameof(TagShortText));
        OnPropertyChanged(nameof(TagDescriptionText));
        OnPropertyChanged(nameof(ValueBadgeText));
        OnPropertyChanged(nameof(LiveBadgeText));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(SimButtonContent));
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        OnPropertyChanged(nameof(TagDescriptionText));
        OnPropertyChanged(nameof(ValueBadgeText));
        OnPropertyChanged(nameof(LiveBadgeText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(SimButtonContent));
    }
}

public partial class TriggerNodeViewModel : GraphNodeViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(TriggerTypeBadgeText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(TriggerActionText))]
    [NotifyPropertyChangedFor(nameof(TriggerParamText))]
    [NotifyPropertyChangedFor(nameof(IsTimeTrigger))]
    [NotifyPropertyChangedFor(nameof(IsTimeWindowRange))]
    [NotifyPropertyChangedFor(nameof(IsTimePointInTime))]
    private TriggerType _triggerType = TriggerType.ON_RISE;

    partial void OnTriggerTypeChanged(TriggerType value)
    {
        if (value == TriggerType.TIME_WINDOW)
        {
            if (CompareOp == CompareOp.NONE)
            {
                CompareOp = CompareOp.EQ;
            }
            if (ThresholdLo == 0 && ThresholdHi == 0)
            {
                ThresholdLo = 700;
                ThresholdHi = 1700;
            }
        }
        OnPropertyChanged(nameof(TimeStartHour));
        OnPropertyChanged(nameof(TimeStartMinute));
        OnPropertyChanged(nameof(TimeEndHour));
        OnPropertyChanged(nameof(TimeEndMinute));
        OnPropertyChanged(nameof(TimeStartFormatted));
        OnPropertyChanged(nameof(TimeEndFormatted));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(TriggerParamText))]
    private uint _forMs = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimStatusText))]
    private bool _isSimTriggered;

    public string SimStatusText => IsSimTriggered
        ? LocalizationService.Tr("SimTriggerFired")
        : LocalizationService.Tr("SimWaitingPulse");

    [ObservableProperty]
    private bool _isSimWaitingDwell;

    [ObservableProperty]
    private uint _simDwellRemainingMs;

    [ObservableProperty]
    private bool _isSimEvaluated;

    [ObservableProperty]
    private string _simDiagText = string.Empty;

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel)
        ? LocalizationService.Tr("NodeTitleTrigger")
        : CustomLabel;

    public string TriggerActionText => TriggerType switch
    {
        TriggerType.ON_RISE => LocalizationService.Tr("OptTrigRise"),
        TriggerType.ON_FALL => LocalizationService.Tr("OptTrigFall"),
        TriggerType.ON_CHANGE => LocalizationService.Tr("OptTrigChange"),
        TriggerType.INTERVAL => LocalizationService.Tr("OptTrigInterval"),
        TriggerType.TIME_WINDOW => LocalizationService.Tr("OptTrigWindow"),
        _ => TriggerType.ToString()
    };

    public string TriggerParamText
    {
        get
        {
            if (TriggerType == TriggerType.INTERVAL)
                return string.Format(LocalizationService.Tr("TrigTypeLoop"), ForMs);
            return ForMs > 0 ? $"{ForMs}ms" : LocalizationService.Tr("NodeDwellImmediate");
        }
    }

    public string TriggerTypeBadgeText => TriggerType switch
    {
        TriggerType.ON_RISE => LocalizationService.Instance.IsVietnamese ? "SƯỜN LÊN (0 → 1)" : "RISING EDGE (0 → 1)",
        TriggerType.ON_FALL => LocalizationService.Instance.IsVietnamese ? "SƯỜN XUỐNG (1 → 0)" : "FALLING EDGE (1 → 0)",
        TriggerType.ON_CHANGE => LocalizationService.Instance.IsVietnamese ? "BIẾN THIÊN GIÁ TRỊ" : "VALUE CHANGE",
        TriggerType.INTERVAL => LocalizationService.Instance.IsVietnamese ? "CHU KỲ LẶP" : "PERIODIC INTERVAL",
        TriggerType.TIME_WINDOW => LocalizationService.Instance.IsVietnamese ? "⏰ THỜI GIAN THỰC (RTC)" : "⏰ TIME TRIGGER (RTC)",
        _ => TriggerType.ToString()
    };

    public string CompactBadgeText
    {
        get
        {
            if (TriggerType == TriggerType.TIME_WINDOW)
            {
                if (CompareOp == CompareOp.BETWEEN)
                {
                    return $"⏰ {TimeStartFormatted} → {TimeEndFormatted}";
                }
                return $"⏰ {TimeStartFormatted}";
            }

            string baseTxt;
            if (HasComparison)
            {
                baseTxt = CompareOp switch
                {
                    CompareOp.EQ => $"== {ThresholdLo}",
                    CompareOp.NEQ => $"!= {ThresholdLo}",
                    CompareOp.GT => $"> {ThresholdLo}",
                    CompareOp.LT => $"< {ThresholdLo}",
                    CompareOp.GTE => $">= {ThresholdLo}",
                    CompareOp.LTE => $"<= {ThresholdLo}",
                    CompareOp.BETWEEN => $"[{ThresholdLo}..{ThresholdHi}]",
                    _ => TriggerType.ToString()
                };
            }
            else
            {
                baseTxt = TriggerType switch
                {
                    TriggerType.ON_RISE => LocalizationService.Tr("TrigTypeRise"),
                    TriggerType.ON_FALL => LocalizationService.Tr("TrigTypeFall"),
                    TriggerType.ON_CHANGE => LocalizationService.Tr("TrigTypeChange"),
                    TriggerType.INTERVAL => string.Format(LocalizationService.Tr("TrigTypeLoop"), ForMs),
                    TriggerType.TIME_WINDOW => LocalizationService.Tr("TrigTypeShift"),
                    _ => TriggerType.ToString()
                };
            }
            if (ForMs > 0 && TriggerType != TriggerType.INTERVAL)
            {
                return $"{baseTxt} ({ForMs}ms)";
            }
            return baseTxt;
        }
    }

    public override string SummaryText
    {
        get
        {
            if (TriggerType == TriggerType.TIME_WINDOW)
            {
                return IsTimeWindowRange
                    ? $"Time Window [{TimeStartFormatted} → {TimeEndFormatted}]"
                    : $"At Time [{TimeStartFormatted}]";
            }

            string tt = TriggerType switch
            {
                TriggerType.ON_RISE => "Rising Edge (0 → 1)",
                TriggerType.ON_FALL => "Falling Edge (1 → 0)",
                TriggerType.ON_CHANGE => "Value Change",
                TriggerType.TIME_WINDOW => "Time Window",
                TriggerType.INTERVAL => "Periodic Interval",
                _ => TriggerType.ToString()
            };
            return ForMs > 0 ? $"{tt} [Dwell {ForMs}ms]" : tt;
        }
    }

    #region Time Trigger (RTC) Properties
    public bool IsTimeTrigger => TriggerType == TriggerType.TIME_WINDOW;

    public bool IsTimeWindowRange
    {
        get => CompareOp == CompareOp.BETWEEN;
        set
        {
            CompareOp = value ? CompareOp.BETWEEN : CompareOp.EQ;
            if (!value)
            {
                ThresholdHi = ThresholdLo;
            }
            OnPropertyChanged(nameof(IsTimeWindowRange));
            OnPropertyChanged(nameof(IsTimePointInTime));
            OnPropertyChanged(nameof(CompactBadgeText));
            OnPropertyChanged(nameof(SummaryText));
        }
    }

    public bool IsTimePointInTime
    {
        get => !IsTimeWindowRange;
        set
        {
            IsTimeWindowRange = !value;
        }
    }

    public int TimeStartHour
    {
        get => Math.Clamp(ThresholdLo / 100, 0, 23);
        set
        {
            int h = Math.Clamp(value, 0, 23);
            ThresholdLo = h * 100 + TimeStartMinute;
            if (IsTimePointInTime)
            {
                ThresholdHi = ThresholdLo;
            }
            OnPropertyChanged(nameof(TimeStartHour));
            OnPropertyChanged(nameof(TimeStartFormatted));
            OnPropertyChanged(nameof(CompactBadgeText));
        }
    }

    public int TimeStartMinute
    {
        get => Math.Clamp(ThresholdLo % 100, 0, 59);
        set
        {
            int m = Math.Clamp(value, 0, 59);
            ThresholdLo = TimeStartHour * 100 + m;
            if (IsTimePointInTime)
            {
                ThresholdHi = ThresholdLo;
            }
            OnPropertyChanged(nameof(TimeStartMinute));
            OnPropertyChanged(nameof(TimeStartFormatted));
            OnPropertyChanged(nameof(CompactBadgeText));
        }
    }

    public int TimeEndHour
    {
        get => Math.Clamp(ThresholdHi / 100, 0, 23);
        set
        {
            int h = Math.Clamp(value, 0, 23);
            ThresholdHi = h * 100 + TimeEndMinute;
            OnPropertyChanged(nameof(TimeEndHour));
            OnPropertyChanged(nameof(TimeEndFormatted));
            OnPropertyChanged(nameof(CompactBadgeText));
        }
    }

    public int TimeEndMinute
    {
        get => Math.Clamp(ThresholdHi % 100, 0, 59);
        set
        {
            int m = Math.Clamp(value, 0, 59);
            ThresholdHi = TimeEndHour * 100 + m;
            OnPropertyChanged(nameof(TimeEndMinute));
            OnPropertyChanged(nameof(TimeEndFormatted));
            OnPropertyChanged(nameof(CompactBadgeText));
        }
    }

    public string TimeStartFormatted => $"{TimeStartHour:D2}:{TimeStartMinute:D2}";
    public string TimeEndFormatted => $"{TimeEndHour:D2}:{TimeEndMinute:D2}";
    #endregion

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompareOpBadgeText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ConditionExpression))]
    [NotifyPropertyChangedFor(nameof(IsRangeComparison))]
    [NotifyPropertyChangedFor(nameof(HasComparison))]
    [NotifyPropertyChangedFor(nameof(ThresholdPromptText))]
    private CompareOp _compareOp = CompareOp.NONE;

    public bool IsRangeComparison => CompareOp == CompareOp.BETWEEN;
    public bool HasComparison => CompareOp != CompareOp.NONE;

    public string ThresholdPromptText => IsRangeComparison 
        ? LocalizationService.Tr("PromptThresholdMin") 
        : LocalizationService.Tr("PromptThresholdVal");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompareOpBadgeText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ConditionExpression))]
    private int _thresholdLo = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompareOpBadgeText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ConditionExpression))]
    private int _thresholdHi = 0;

    public string ConditionExpression
    {
        get
        {
            if (CompareOp == CompareOp.NONE) return "Any Change";
            string op = CompareOp switch
            {
                CompareOp.EQ => $"== {ThresholdLo}",
                CompareOp.NEQ => $"!= {ThresholdLo}",
                CompareOp.GT => $"> {ThresholdLo}",
                CompareOp.LT => $"< {ThresholdLo}",
                CompareOp.GTE => $">= {ThresholdLo}",
                CompareOp.LTE => $"<= {ThresholdLo}",
                CompareOp.BETWEEN => $"in [{ThresholdLo}..{ThresholdHi}]",
                _ => "Pass"
            };
            return op;
        }
    }

    public string CompareOpBadgeText => CompareOp switch
    {
        CompareOp.EQ => LocalizationService.Tr("BadgeCompareEq"),
        CompareOp.NEQ => LocalizationService.Tr("BadgeCompareNeq"),
        CompareOp.GT => LocalizationService.Tr("BadgeCompareGt"),
        CompareOp.LT => LocalizationService.Tr("BadgeCompareLt"),
        CompareOp.GTE => LocalizationService.Tr("BadgeCompareGte"),
        CompareOp.LTE => LocalizationService.Tr("BadgeCompareLte"),
        CompareOp.BETWEEN => string.Format(LocalizationService.Tr("BadgeCompareBetween"), ThresholdLo, ThresholdHi),
        _ => LocalizationService.Tr("BadgeCompareNone")
    };

    public TriggerNodeViewModel()
    {
        Title = "Trigger";
        Icon = "⚡";
        InputConnectors.Add(new ConnectorViewModel { Title = "In", IsInput = true, IsEvent = false, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", IsInput = false, IsEvent = true, Node = this });
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        Title = "Trigger";
        OnPropertyChanged(nameof(TriggerTypeBadgeText));
        OnPropertyChanged(nameof(CompactBadgeText));
        OnPropertyChanged(nameof(TriggerActionText));
        OnPropertyChanged(nameof(TriggerParamText));
        OnPropertyChanged(nameof(CompareOpBadgeText));
        OnPropertyChanged(nameof(ConditionExpression));
        OnPropertyChanged(nameof(ThresholdPromptText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(SimStatusText));
    }
}

public partial class GuardNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    public ushort? BoundTagIndex => (GuardTag != null && GuardTag.Kind != TagKind.None && GuardTag.Index < 65535) ? (ushort)GuardTag.Index : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveConditionBadgeText))]
    private int _liveRawValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveConditionBadgeText))]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveConditionBadgeText))]
    private bool _isLiveOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isLiveActive;

    [ObservableProperty]
    private string _liveValueText = "--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveConditionBadgeText))]
    private bool _hasReceivedUpdate;

    public NodeLiveVisualState LiveVisualState
    {
        get
        {
            if (!IsLiveOnline) return NodeLiveVisualState.Offline;
            if (LiveQuality == TagQuality.Stale) return NodeLiveVisualState.Stale;
            if (IsLiveActive) return NodeLiveVisualState.Active;
            return NodeLiveVisualState.Inactive;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ConditionExpression))]
    [NotifyPropertyChangedFor(nameof(ConditionBadgeText))]
    private bool _negate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimStatusText))]
    private bool _isSimPassed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimStatusText))]
    private bool _isSimBlocked;

    public string SimStatusText => IsSimPassed
        ? LocalizationService.Tr("SimGuardPassed")
        : LocalizationService.Tr("SimGuardBlocked");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ConditionExpression))]
    [NotifyPropertyChangedFor(nameof(GuardTagShortText))]
    [NotifyPropertyChangedFor(nameof(LiveConditionBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsInvalidTagType))]
    private TagModel? _guardTag;

    public bool IsInvalidTagType => GuardTag != null && GuardTag.Kind != TagKind.None && !GuardTag.IsDigital;

    public string GuardTagShortText => !string.IsNullOrWhiteSpace(GuardTag?.Alias) ? GuardTag.Alias : (GuardTag?.Name ?? "Tag");

    public string LiveConditionBadgeText
    {
        get
        {
            string baseText = ConditionBadgeText;
            if (GuardTag == null)
            {
                return baseText;
            }

            bool isVi = LocalizationService.Instance.IsVietnamese;
            if (!IsLiveOnline)
            {
                if (HasReceivedUpdate)
                {
                    string tagVal = GuardTag.IsDigital
                        ? (LiveRawValue != 0 ? (isVi ? "BẬT" : "ON") : (isVi ? "TẮT" : "OFF"))
                        : LiveRawValue.ToString("N0");
                    return $"{baseText} ({GuardTagShortText}: {tagVal})";
                }
                return baseText;
            }

            string tagValOnline = GuardTag.IsDigital
                ? (LiveRawValue != 0 ? (isVi ? "BẬT" : "ON") : (isVi ? "TẮT" : "OFF"))
                : LiveRawValue.ToString("N0");

            string staleSuffix = LiveQuality == TagQuality.Stale ? " ⚠️" : "";
            return $"{baseText} ({GuardTagShortText}: {tagValOnline}){staleSuffix}";
        }
    }

    public string SimButtonContent => (GuardTag != null && GuardTag.Value == 1)
        ? LocalizationService.Tr("SimBtnOn1")
        : LocalizationService.Tr("SimBtnOff0");

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel)
        ? LocalizationService.Tr("NodeTitleGuard")
        : CustomLabel;

    public string ConditionExpression
    {
        get
        {
            string tagName = GuardTag != null ? (string.IsNullOrWhiteSpace(GuardTag.Alias) ? GuardTag.Name : GuardTag.Alias) : "Tag";
            return Negate ? $"NOT {tagName}" : tagName;
        }
    }

    public string ConditionBadgeText => Negate 
        ? LocalizationService.Tr("BadgeGuardInvert") 
        : LocalizationService.Tr("BadgeGuardPass");

    public string CompactBadgeText
    {
        get
        {
            string tag = GuardTagShortText;
            return Negate ? $"! {tag}" : tag;
        }
    }

    public string GuardAddressText => GuardTag?.Name ?? "TAG";

    public override string SummaryText
    {
        get
        {
            string tagName = GuardTag?.DisplayName ?? "Tag";
            return Negate ? $"NOT {tagName} (0/OFF)" : $"{tagName} (1/ON)";
        }
    }

    public GuardNodeViewModel(TagModel? guardTag = null)
    {
        Title = "Guard";
        Icon = "🛡️";
        _guardTag = guardTag;
        InputConnectors.Add(new ConnectorViewModel { Title = "In", IsInput = true, IsEvent = true, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", IsInput = false, IsEvent = true, Node = this });
    }

    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        HasReceivedUpdate = true;
        LiveRawValue = snapshot.RawValue;
        LiveQuality = snapshot.Quality;

        // IsLiveActive strictly boolean
        bool isBoolean = snapshot.DataType == TagDataType.Boolean || 
                         snapshot.Kind is TagKind.DiscreteInput or TagKind.VirtualFlag ||
                         (GuardTag?.IsDigital == true);
        IsLiveActive = isBoolean && snapshot.RawValue != 0;
        LiveValueText = snapshot.RawValue.ToString();

        OnPropertyChanged(nameof(LiveConditionBadgeText));
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        OnPropertyChanged(nameof(LiveConditionBadgeText));
        OnPropertyChanged(nameof(LiveVisualState));
    }

    partial void OnGuardTagChanged(TagModel? oldValue, TagModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnGuardTagPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnGuardTagPropertyChanged;
        }
        OnPropertyChanged(nameof(GuardTagShortText));
        OnPropertyChanged(nameof(ConditionExpression));
        OnPropertyChanged(nameof(LiveConditionBadgeText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsInvalidTagType));
    }

    private void OnGuardTagPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(GuardTagShortText));
        OnPropertyChanged(nameof(ConditionExpression));
        OnPropertyChanged(nameof(LiveConditionBadgeText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsInvalidTagType));
        OnPropertyChanged(nameof(SimButtonContent));
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        Title = "Guard";
        OnPropertyChanged(nameof(CompactBadgeText));
        OnPropertyChanged(nameof(ConditionExpression));
        OnPropertyChanged(nameof(ConditionBadgeText));
        OnPropertyChanged(nameof(LiveConditionBadgeText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(IsInvalidTagType));
        OnPropertyChanged(nameof(SimButtonContent));
        OnPropertyChanged(nameof(SimStatusText));
    }
}

public partial class ActionNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    public ushort? BoundTagIndex => (TargetTag != null && TargetTag.Kind != TagKind.None && TargetTag.Index < 65535) ? (ushort)TargetTag.Index : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveTargetBadgeText))]
    private int _liveRawValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveTargetBadgeText))]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(LiveTargetBadgeText))]
    private bool _isLiveOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isLiveActive;

    [ObservableProperty]
    private string _liveValueText = "--";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveTargetBadgeText))]
    private bool _hasReceivedUpdate;

    public NodeLiveVisualState LiveVisualState
    {
        get
        {
            if (!IsLiveOnline) return NodeLiveVisualState.Offline;
            if (LiveQuality == TagQuality.Stale) return NodeLiveVisualState.Stale;
            if (IsLiveActive) return NodeLiveVisualState.Active;
            return NodeLiveVisualState.Inactive;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ActionTypeBadgeText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ActionExpression))]
    private ActionType _actionType = ActionType.SET_TAG;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ActionExpression))]
    private int _actionParam = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimLedStatusText))]
    [NotifyPropertyChangedFor(nameof(SimStatusText))]
    [NotifyPropertyChangedFor(nameof(ActionStatusPillText))]
    private bool _isSimFired;

    public string SimLedStatusText => IsSimFired
        ? (LocalizationService.Instance.IsVietnamese ? "● BẬT" : "● ON")
        : (LocalizationService.Instance.IsVietnamese ? "○ TẮT" : "○ OFF");

    public string SimStatusText => IsSimFired
        ? LocalizationService.Tr("SimActionFired")
        : LocalizationService.Tr("SimActionIdle");

    [ObservableProperty]
    private bool _isSimTargetOn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActionExpression))]
    [NotifyPropertyChangedFor(nameof(ActionStatusPillText))]
    private string _simTargetLiveText = string.Empty;

    public string ActionStatusPillText
    {
        get
        {
            if (ActionType == ActionType.SCALE_TAG && !string.IsNullOrEmpty(SimTargetLiveText))
            {
                return SimTargetLiveText;
            }
            if (IsSimFired)
            {
                return LocalizationService.Instance.IsVietnamese ? "● ĐÃ XUẤT LỆNH" : "● FIRED";
            }
            return TargetTagShortText;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ActionExpression))]
    [NotifyPropertyChangedFor(nameof(TargetTagShortText))]
    [NotifyPropertyChangedFor(nameof(LiveTargetBadgeText))]
    private TagModel? _targetTag;

    public string TargetTagShortText
    {
        get
        {
            if (TargetTag == null) 
                return LocalizationService.Instance.IsVietnamese ? "Chưa chọn Tag" : "No Target";
            
            if (!string.IsNullOrWhiteSpace(TargetTag.Alias))
                return TargetTag.Alias;

            return LocalizationService.Instance.IsVietnamese 
                ? $"Ngõ ra {TargetTag.Kind}" 
                : $"{TargetTag.Kind} Output";
        }
    }

    public string LiveTargetBadgeText
    {
        get
        {
            string baseText = TargetTagShortText;
            if (!IsLiveOnline)
            {
                if (HasReceivedUpdate)
                {
                    string valStr = (TargetTag?.IsDigital ?? true)
                        ? (LiveRawValue != 0 ? "ON" : "OFF")
                        : LiveRawValue.ToString("N0");
                    return $"{baseText} [Last Known: {valStr}]";
                }
                return baseText;
            }

            if (TargetTag != null && !TargetTag.IsDigital)
            {
                string suffix = LiveQuality == TagQuality.Stale ? " ⚠️" : "";
                return $"{baseText} [Current: {LiveRawValue:N0}]{suffix}";
            }

            string stateStr = LiveRawValue != 0 ? "ON" : "OFF";
            string staleSuffix = LiveQuality == TagQuality.Stale ? " ⚠️" : "";
            return $"{baseText} [Current: {stateStr}]{staleSuffix}";
        }
    }

    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        HasReceivedUpdate = true;
        LiveRawValue = snapshot.RawValue;
        LiveQuality = snapshot.Quality;

        // IsLiveActive strictly boolean (Point 9)
        bool isBoolean = snapshot.DataType == TagDataType.Boolean || 
                         snapshot.Kind is TagKind.DiscreteOutput or TagKind.VirtualFlag ||
                         (TargetTag?.IsDigital == true);
        IsLiveActive = isBoolean && snapshot.RawValue != 0;
        LiveValueText = snapshot.RawValue.ToString();

        OnPropertyChanged(nameof(LiveTargetBadgeText));
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        OnPropertyChanged(nameof(LiveTargetBadgeText));
        OnPropertyChanged(nameof(LiveVisualState));
    }

    partial void OnTargetTagChanged(TagModel? oldValue, TagModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnTargetTagPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnTargetTagPropertyChanged;
        }
        OnPropertyChanged(nameof(CompactBadgeText));
        OnPropertyChanged(nameof(ActionExpression));
        OnPropertyChanged(nameof(TargetTagShortText));
        OnPropertyChanged(nameof(LiveTargetBadgeText));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void OnTargetTagPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CompactBadgeText));
        OnPropertyChanged(nameof(ActionExpression));
        OnPropertyChanged(nameof(TargetTagShortText));
        OnPropertyChanged(nameof(LiveTargetBadgeText));
        OnPropertyChanged(nameof(SummaryText));
    }

    public string ActionExpression
    {
        get
        {
            string tag = TargetTag?.Name ?? "TAG";
            return ActionType switch
            {
                ActionType.SET_TAG => $"{tag} = {ActionParam}",
                ActionType.TOGGLE_TAG => $"TOGGLE {tag}",
                ActionType.INC_COUNTER => $"{tag} += {ActionParam}",
                ActionType.WRITE_REMOTE => $"MODBUS {ActionParam}",
                ActionType.SEND_ALARM => $"ALARM {tag}",
                ActionType.LOG_EVENT => $"LOG {tag}",
                ActionType.SCALE_TAG => !string.IsNullOrEmpty(SimTargetLiveText) ? $"{tag} = {SimTargetLiveText}" : $"{tag} ← SCALE",
                _ => $"{ActionType} {tag}"
            };
        }
    }

    public string ActionTypeBadgeText => ActionType switch
    {
        ActionType.SET_TAG => LocalizationService.Tr("BadgeActSet"),
        ActionType.TOGGLE_TAG => LocalizationService.Tr("BadgeActToggle"),
        ActionType.INC_COUNTER => LocalizationService.Tr("BadgeActInc"),
        ActionType.WRITE_REMOTE => LocalizationService.Tr("BadgeActRemote"),
        ActionType.SEND_ALARM => LocalizationService.Tr("BadgeActAlarm"),
        ActionType.LOG_EVENT => LocalizationService.Tr("BadgeActLog"),
        ActionType.SCALE_TAG => LocalizationService.Tr("BadgeActScale"),
        _ => ActionType.ToString()
    };

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel)
        ? LocalizationService.Tr("NodeTitleAction")
        : CustomLabel;

    public string CompactBadgeText
    {
        get
        {
            if (IsSimFired)
            {
                return LocalizationService.Tr("SimLedFired");
            }

            if (TargetTag != null && !string.IsNullOrWhiteSpace(TargetTag.Alias))
            {
                return TargetTag.Alias;
            }

            string tag = TargetTag?.Name ?? "TAG";
            return ActionType switch
            {
                ActionType.SET_TAG => $"{tag} = {ActionParam}",
                ActionType.TOGGLE_TAG => $"TOGGLE {tag}",
                ActionType.INC_COUNTER => $"{tag} += {ActionParam}",
                ActionType.WRITE_REMOTE => $"MODBUS {ActionParam}",
                ActionType.SEND_ALARM => $"ALARM {tag}",
                ActionType.LOG_EVENT => $"LOG {tag}",
                ActionType.SCALE_TAG => $"{tag} = SCALE (×{ActionParam})",
                _ => $"{ActionType} {tag}"
            };
        }
    }

    public override string SummaryText
    {
        get
        {
            string tgt = TargetTag?.DisplayName ?? "Target Tag";
            return ActionType switch
            {
                ActionType.SET_TAG => $"Set {tgt} = {ActionParam}",
                ActionType.TOGGLE_TAG => $"Toggle {tgt}",
                ActionType.INC_COUNTER => $"Increment {tgt} by {ActionParam}",
                ActionType.WRITE_REMOTE => $"Write {tgt} = {ActionParam} (Modbus)",
                ActionType.SEND_ALARM => $"Send Alarm ({tgt})",
                ActionType.LOG_EVENT => $"Log Event ({tgt})",
                ActionType.SCALE_TAG => $"Scale {tgt} (×{ActionParam})",
                _ => $"{ActionType} -> {tgt}"
            };
        }
    }

    public ActionNodeViewModel(TagModel? target = null)
    {
        Title = "Action";
        Icon = "⚙️";
        TargetTag = target;
        InputConnectors.Add(new ConnectorViewModel { Title = "In", IsInput = true, IsEvent = true, Node = this });
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        Title = "Action";
        OnPropertyChanged(nameof(ActionTypeBadgeText));
        OnPropertyChanged(nameof(CompactBadgeText));
        OnPropertyChanged(nameof(LiveTargetBadgeText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(SimLedStatusText));
        OnPropertyChanged(nameof(SimStatusText));
    }
}

public partial class TimerNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(TimerModeBadgeText))]
    [NotifyPropertyChangedFor(nameof(ModeGlyph))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private string _timerMode = "TON"; // TON, TOF, TP

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(PresetText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private uint _presetMs = 3000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(OutputBadgeText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(BoundTagIndex))]
    private TagModel? _outputTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(InputBadgeText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private TagModel? _inputTag;

    // Simulation derived state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    private uint _elapsedMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    private double _progressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    private bool _isTiming;

    public string SimulationProgressText => IsTiming
        ? $"{ElapsedMs} / {PresetMs} ms"
        : (IsLiveActive ? $"DONE · {PresetMs} ms" : $"PT: {PresetMs} ms");

    public string TimerStatusPillText => IsLiveActive
        ? "Q = ON"
        : (IsTiming ? $"{(int)ProgressPercent}% ({(PresetMs / 1000.0):0.#}s)" : $"{PresetMs / 1000.0:0.#}s (OFF)");

    public ushort? BoundTagIndex => (OutputTag != null && OutputTag.Kind != TagKind.None && OutputTag.Index < 65535) ? (ushort)OutputTag.Index : null;

    [ObservableProperty]
    private int _liveRawValue;

    [ObservableProperty]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    private bool _isLiveOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    private bool _isLiveActive;

    [ObservableProperty]
    private string _liveValueText = "--";

    [ObservableProperty]
    private bool _hasReceivedUpdate;

    public NodeLiveVisualState LiveVisualState
    {
        get
        {
            if (!IsLiveOnline) return NodeLiveVisualState.Offline;
            if (LiveQuality == TagQuality.Stale) return NodeLiveVisualState.Stale;
            if (IsLiveActive) return NodeLiveVisualState.Active;
            return NodeLiveVisualState.Inactive;
        }
    }

    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        HasReceivedUpdate = true;
        LiveRawValue = snapshot.RawValue;
        LiveQuality = snapshot.Quality;
        LiveValueText = snapshot.RawValue.ToString();
        IsLiveActive = snapshot.RawValue != 0;
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"{TimerMode} Timer"
        : CustomLabel;

    public string TimerModeBadgeText => TimerMode switch
    {
        "TOF" => "TOF (Trễ ngắt)",
        "TP" => "TP (Phát xung)",
        _ => "TON (Trễ bật)"
    };

    public string ModeGlyph => TimerMode switch
    {
        "TOF" => "⌛",
        "TP" => "📶",
        _ => "🕒"
    };

    public string PresetText => $"{PresetMs} ms ({(PresetMs / 1000.0):0.#}s)";

    public string OutputBadgeText => OutputTag != null ? OutputTag.Name : "(Chưa chọn Q)";

    public string InputBadgeText => InputTag != null ? InputTag.Name : "(Tín hiệu vào IN)";

    public string NarrativeText
    {
        get
        {
            string inTag = InputTag?.DisplayName ?? LocalizationService.Tr("NarrNoTag");
            string outTag = OutputTag?.DisplayName ?? LocalizationService.Tr("NarrNoTargetTag");
            string sec = PresetMs >= 1000
                ? string.Format(LocalizationService.Tr("NarrDwellSec"), (PresetMs / 1000.0).ToString("0.#"))
                : "";
            return TimerMode switch
            {
                "TOF" => string.Format(LocalizationService.Tr("NarrTimerTof"), inTag, outTag, PresetMs, sec),
                "TP" => string.Format(LocalizationService.Tr("NarrTimerTp"), inTag, outTag, PresetMs, sec),
                _ => string.Format(LocalizationService.Tr("NarrTimerTon"), inTag, outTag, PresetMs, sec)
            };
        }
    }

    public override string SummaryText => $"{TimerMode} ({PresetMs}ms) → {OutputTag?.Name ?? "--"}";

    public TimerNodeViewModel(string mode = "TON", TagModel? inTag = null, TagModel? qTag = null, uint presetMs = 3000)
    {
        Title = $"{mode} Timer";
        Icon = "⏱️";
        TimerMode = mode;
        InputTag = inTag;
        OutputTag = qTag;
        PresetMs = presetMs;

        // Standard FBD construct: In as input, Q as output
        InputConnectors.Add(new ConnectorViewModel { Title = "In", IsInput = true, IsEvent = true, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Q", IsInput = false, IsEvent = false, Node = this });
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(TimerModeBadgeText));
        OnPropertyChanged(nameof(PresetText));
        OnPropertyChanged(nameof(OutputBadgeText));
        OnPropertyChanged(nameof(InputBadgeText));
        OnPropertyChanged(nameof(NarrativeText));
        OnPropertyChanged(nameof(SummaryText));
    }
}

public partial class CounterNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CounterModeBadgeText))]
    [NotifyPropertyChangedFor(nameof(ModeGlyph))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private string _counterMode = "CTU"; // CTU, CTD

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(PresetText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(SimulationCountText))]
    private int _presetValue = 10;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CvBadgeText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private TagModel? _cvTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(OutputBadgeText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(BoundTagIndex))]
    private TagModel? _outputTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResetBadgeText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private TagModel? _resetTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InputBadgeText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private TagModel? _inputTag;

    // Simulation derived state
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulationCountText))]
    [NotifyPropertyChangedFor(nameof(CounterStatusPillText))]
    private int _currentCount = 0;

    public string SimulationCountText => $"{CurrentCount} / {PresetValue}";

    public string CounterStatusPillText => IsLiveActive
        ? "Q = ON"
        : $"PV: {PresetValue}";

    public ushort? BoundTagIndex => (OutputTag != null && OutputTag.Kind != TagKind.None && OutputTag.Index < 65535) ? (ushort)OutputTag.Index : null;

    [ObservableProperty]
    private int _liveRawValue;

    [ObservableProperty]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    private bool _isLiveOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulationCountText))]
    [NotifyPropertyChangedFor(nameof(CounterStatusPillText))]
    private bool _isLiveActive;

    [ObservableProperty]
    private string _liveValueText = "--";

    [ObservableProperty]
    private bool _hasReceivedUpdate;

    public NodeLiveVisualState LiveVisualState
    {
        get
        {
            if (!IsLiveOnline) return NodeLiveVisualState.Offline;
            if (LiveQuality == TagQuality.Stale) return NodeLiveVisualState.Stale;
            if (IsLiveActive) return NodeLiveVisualState.Active;
            return NodeLiveVisualState.Inactive;
        }
    }

    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        HasReceivedUpdate = true;
        LiveRawValue = snapshot.RawValue;
        LiveQuality = snapshot.Quality;
        LiveValueText = snapshot.RawValue.ToString();
        IsLiveActive = snapshot.RawValue != 0;
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"{CounterMode} Counter"
        : CustomLabel;

    public string CounterModeBadgeText => CounterMode switch
    {
        "CTD" => "CTD (Đếm lùi)",
        _ => "CTU (Đếm tiến)"
    };

    public string ModeGlyph => CounterMode switch
    {
        "CTD" => "🔽",
        _ => "🔼"
    };

    public string PresetText => $"{PresetValue} (PV)";

    public string CvBadgeText => CvTag != null ? CvTag.Name : "(Chưa chọn CV)";

    public string OutputBadgeText => OutputTag != null ? OutputTag.Name : "(Chưa chọn Q)";

    public string ResetBadgeText => ResetTag != null && ResetTag.Kind != TagKind.None ? ResetTag.Name : "(Không dùng Reset)";

    public string InputBadgeText => InputTag != null ? InputTag.Name : (CounterMode == "CTD" ? "(Tín hiệu CD)" : "(Tín hiệu CU)");

    public string NarrativeText
    {
        get
        {
            string inTag = InputTag?.DisplayName ?? LocalizationService.Tr("NarrNoTag");
            string cvTag = CvTag?.DisplayName ?? "CV";
            string outTag = OutputTag?.DisplayName ?? LocalizationService.Tr("NarrNoTargetTag");
            bool hasReset = ResetTag != null && ResetTag.Kind != TagKind.None;

            if (CounterMode == "CTD")
            {
                string resetPart = hasReset
                    ? string.Format(LocalizationService.Tr("NarrCounterResetCtd"), ResetTag!.DisplayName, PresetValue)
                    : "";
                return string.Format(LocalizationService.Tr("NarrCounterCtd"), inTag, cvTag, outTag, PresetValue, resetPart);
            }
            else
            {
                string resetPart = hasReset
                    ? string.Format(LocalizationService.Tr("NarrCounterResetCtu"), ResetTag!.DisplayName)
                    : "";
                return string.Format(LocalizationService.Tr("NarrCounterCtu"), inTag, cvTag, outTag, PresetValue, resetPart);
            }
        }
    }

    public override string SummaryText => $"{CounterMode} ({PresetValue}) → {OutputTag?.Name ?? "--"}";

    partial void OnCounterModeChanged(string value)
    {
        Title = $"{value} Counter";
        if (InputConnectors.Count > 0)
        {
            InputConnectors[0].Title = value == "CTD" ? "CD" : "CU";
        }
    }

    public CounterNodeViewModel(string mode = "CTU", TagModel? inTag = null, TagModel? cvTag = null, TagModel? qTag = null, TagModel? resetTag = null, int presetValue = 10)
    {
        Title = $"{mode} Counter";
        Icon = "🔢";
        CounterMode = mode;
        InputTag = inTag;
        CvTag = cvTag;
        OutputTag = qTag;
        ResetTag = resetTag;
        PresetValue = presetValue;

        // Standard FBD construct: CU (or CD) and R as inputs, Q as output
        string countPort = mode == "CTD" ? "CD" : "CU";
        InputConnectors.Add(new ConnectorViewModel { Title = countPort, IsInput = true, IsEvent = true, Node = this });
        InputConnectors.Add(new ConnectorViewModel { Title = "R", IsInput = true, IsEvent = true, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Q", IsInput = false, IsEvent = false, Node = this });
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(CounterModeBadgeText));
        OnPropertyChanged(nameof(PresetText));
        OnPropertyChanged(nameof(CvBadgeText));
        OnPropertyChanged(nameof(OutputBadgeText));
        OnPropertyChanged(nameof(ResetBadgeText));
        OnPropertyChanged(nameof(InputBadgeText));
        OnPropertyChanged(nameof(NarrativeText));
        OnPropertyChanged(nameof(SummaryText));
    }
}

public record EnumOption<T>(T Value, string DisplayName);

public static class RuleOptions
{
    public static ObservableCollection<EnumOption<CompareOp>> CompareOps { get; } = new();
    public static ObservableCollection<EnumOption<TriggerType>> TriggerTypes { get; } = new();
    public static ObservableCollection<EnumOption<ActionType>> ActionTypes { get; } = new();

    static RuleOptions()
    {
        Refresh();
        LocalizationService.Instance.LanguageChanged += Refresh;
    }

    public static void Refresh()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            try
            {
                dispatcher.Invoke(Refresh);
                return;
            }
            catch
            {
                return;
            }
        }

        CompareOps.Clear();
        CompareOps.Add(new(CompareOp.EQ, LocalizationService.Tr("OptCompareEq")));
        CompareOps.Add(new(CompareOp.NEQ, LocalizationService.Tr("OptCompareNeq")));
        CompareOps.Add(new(CompareOp.GT, LocalizationService.Tr("OptCompareGt")));
        CompareOps.Add(new(CompareOp.LT, LocalizationService.Tr("OptCompareLt")));
        CompareOps.Add(new(CompareOp.GTE, LocalizationService.Tr("OptCompareGte")));
        CompareOps.Add(new(CompareOp.LTE, LocalizationService.Tr("OptCompareLte")));
        CompareOps.Add(new(CompareOp.BETWEEN, LocalizationService.Tr("OptCompareBetween")));
        CompareOps.Add(new(CompareOp.NONE, LocalizationService.Tr("OptCompareNone")));

        TriggerTypes.Clear();
        TriggerTypes.Add(new(TriggerType.ON_RISE, LocalizationService.Tr("OptTrigRise")));
        TriggerTypes.Add(new(TriggerType.ON_FALL, LocalizationService.Tr("OptTrigFall")));
        TriggerTypes.Add(new(TriggerType.ON_CHANGE, LocalizationService.Tr("OptTrigChange")));
        TriggerTypes.Add(new(TriggerType.INTERVAL, LocalizationService.Tr("OptTrigInterval")));
        TriggerTypes.Add(new(TriggerType.TIME_WINDOW, LocalizationService.Tr("OptTrigWindow")));

        ActionTypes.Clear();
        ActionTypes.Add(new(ActionType.SET_TAG, LocalizationService.Tr("OptActSet")));
        ActionTypes.Add(new(ActionType.TOGGLE_TAG, LocalizationService.Tr("OptActToggle")));
        ActionTypes.Add(new(ActionType.INC_COUNTER, LocalizationService.Tr("OptActInc")));
        ActionTypes.Add(new(ActionType.WRITE_REMOTE, LocalizationService.Tr("OptActRemote")));
        ActionTypes.Add(new(ActionType.SEND_ALARM, LocalizationService.Tr("OptActAlarm")));
        ActionTypes.Add(new(ActionType.LOG_EVENT, LocalizationService.Tr("OptActLog")));
        ActionTypes.Add(new(ActionType.SCALE_TAG, LocalizationService.Tr("OptActScale")));
    }
}
