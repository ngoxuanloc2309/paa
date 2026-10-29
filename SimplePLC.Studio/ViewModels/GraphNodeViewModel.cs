using System.ComponentModel;
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
    private string _icon = string.Empty;

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
    [NotifyPropertyChangedFor(nameof(HasNetworkNumber))]
    [NotifyPropertyChangedFor(nameof(NetworkBadgeText))]
    private int _networkNumber;

    public bool HasNetworkNumber => NetworkNumber > 0;
    public string NetworkBadgeText => $"#{NetworkNumber}";

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

            return Tag.DisplayName;
        }
    }

    public string ValueBadgeText
    {
        get
        {
            if (Tag == null) return "—";
            if (Tag.IsDigital || Tag.DataType == TagDataType.Boolean)
            {
                return Tag.Value != 0 ? "1" : "0";
            }
            if (Tag.DataType == TagDataType.Float32)
            {
                return SmartInputParser.FormatReal(BitConverter.Int32BitsToSingle(Tag.Value));
            }
            return Tag.Value.ToString();
        }
    }

    public string LiveBadgeText
    {
        get
        {
            bool isBool = Tag?.IsDigital ?? (Tag?.DataType == TagDataType.Boolean);
            bool isFloat = Tag?.DataType == TagDataType.Float32;

            if (!IsLiveOnline)
            {
                if (HasReceivedUpdate)
                {
                    string valStr = isBool
                        ? (LiveRawValue != 0 ? "1" : "0")
                        : (isFloat ? SmartInputParser.FormatReal(BitConverter.Int32BitsToSingle(LiveRawValue)) : LiveRawValue.ToString("N0"));
                    return string.Format(LocalizationService.Tr("NodeLastKnown"), valStr);
                }
                return ValueBadgeText;
            }

            string suffix = LiveQuality == TagQuality.Stale ? " [STALE]" : "";
            if (isBool)
            {
                return LiveRawValue != 0 ? $"1{suffix}" : $"0{suffix}";
            }

            if (isFloat)
            {
                return $"{SmartInputParser.FormatReal(BitConverter.Int32BitsToSingle(LiveRawValue))}{suffix}";
            }

            return $"{LiveRawValue:N0}{suffix}";
        }
    }

    public string TagShortText => TagDescriptionText;

    public override string SummaryText => Tag != null 
        ? Tag.DisplayName 
        : LocalizationService.Tr("NarrNoTag");

    public InputNodeViewModel(TagModel? tag = null)
    {
        Title = tag?.Name ?? "DI0";
        Icon = "Input";
        Tag = tag;
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", TooltipKey = "PinTooltipOut", IsInput = false, IsEvent = false, Node = this });
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
        TagValueText = newValue?.FormattedValue ?? "0";
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
        ? "● 1"
        : "○ 0";

    [RelayCommand]
    public void SetTagValueBool(object? param)
    {
        if (Tag == null) return;
        int val = 1;
        if (param is int i) val = i;
        else if (param is string s && SmartInputParser.TryParseBool(s, out int parsed)) val = parsed;
        Tag.Value = val != 0 ? 1 : 0;
        OnPropertyChanged(nameof(ValueBadgeText));
        OnPropertyChanged(nameof(SimButtonContent));
    }

    [ObservableProperty]
    private string _tagValueText = "0";

    private bool _isUpdatingTagFromText;

    partial void OnTagValueTextChanged(string value)
    {
        if (Tag == null) return;
        if (SmartInputParser.TryParseForTag(value, Tag, out int rawVal, out _))
        {
            _isUpdatingTagFromText = true;
            try
            {
                Tag.Value = rawVal;
            }
            finally
            {
                _isUpdatingTagFromText = false;
            }
            OnPropertyChanged(nameof(ValueBadgeText));
            OnPropertyChanged(nameof(SimButtonContent));
        }
    }

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
        if (!_isUpdatingTagFromText && (e.PropertyName == nameof(TagModel.Value) || e.PropertyName == nameof(TagModel.DataType)))
        {
            TagValueText = Tag?.FormattedValue ?? "0";
        }
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
    [NotifyPropertyChangedFor(nameof(IsIntervalTrigger))]
    [NotifyPropertyChangedFor(nameof(IsStandardEdgeTrigger))]
    [NotifyPropertyChangedFor(nameof(IsTimeWindowRange))]
    [NotifyPropertyChangedFor(nameof(IsTimePointInTime))]
    private TriggerType _triggerType = TriggerType.ON_RISE;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriggerActionText))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(InputConnectionBadge))]
    [NotifyPropertyChangedFor(nameof(HasWireConnection))]
    private TagModel? _inputTag;

    partial void OnInputTagChanged(TagModel? oldValue, TagModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnTagPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnTagPropertyChanged;
        }
        OnPropertyChanged(nameof(TriggerActionText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(InputConnectionBadge));
        OnPropertyChanged(nameof(HasWireConnection));
    }

    private void OnTagPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TagModel.Name) || e.PropertyName == nameof(TagModel.DisplayName) || e.PropertyName == nameof(TagModel.Alias))
        {
            OnPropertyChanged(nameof(TriggerActionText));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(InputConnectionBadge));
        }
    }

    public string InputConnectionBadge => InputTag != null
        ? $"🔗 Connected: {InputTag.Name}"
        : string.Empty;

    public bool HasWireConnection => InputTag != null;

    public override void Dispose()
    {
        if (InputTag != null)
        {
            InputTag.PropertyChanged -= OnTagPropertyChanged;
        }
        base.Dispose();
    }

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

    public string TriggerActionText
    {
        get
        {
            string actionName = TriggerType switch
            {
                TriggerType.ON_RISE => LocalizationService.Tr("OptTrigRise"),
                TriggerType.ON_FALL => LocalizationService.Tr("OptTrigFall"),
                TriggerType.ON_CHANGE => LocalizationService.Tr("OptTrigChange"),
                TriggerType.INTERVAL => LocalizationService.Tr("OptTrigInterval"),
                TriggerType.TIME_WINDOW => LocalizationService.Tr("OptTrigWindow"),
                _ => TriggerType.ToString()
            };

            if (InputTag != null && !string.IsNullOrWhiteSpace(InputTag.Name))
            {
                return $"{InputTag.Name}: {actionName}";
            }
            return actionName;
        }
    }

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
        TriggerType.TIME_WINDOW => LocalizationService.Instance.IsVietnamese ? "THỜI GIAN THỰC (RTC)" : "TIME TRIGGER (RTC)",
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
                    return $"{TimeStartFormatted} → {TimeEndFormatted}";
                }
                return $"{TimeStartFormatted}";
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
    public bool IsIntervalTrigger => TriggerType == TriggerType.INTERVAL;
    public bool IsStandardEdgeTrigger => TriggerType is TriggerType.ON_RISE or TriggerType.ON_FALL or TriggerType.ON_CHANGE;

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
        get => Math.Clamp((int)ThresholdLo / 100, 0, 23);
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
        get => Math.Clamp((int)ThresholdLo % 100, 0, 59);
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
        get => Math.Clamp((int)ThresholdHi / 100, 0, 23);
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
        get => Math.Clamp((int)ThresholdHi % 100, 0, 59);
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
    private double _thresholdLo = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(CompareOpBadgeText))]
    [NotifyPropertyChangedFor(nameof(CompactBadgeText))]
    [NotifyPropertyChangedFor(nameof(ConditionExpression))]
    private double _thresholdHi = 0;

    [ObservableProperty]
    private string _thresholdLoText = "0";

    [ObservableProperty]
    private string _thresholdHiText = "0";

    partial void OnThresholdLoTextChanged(string value)
    {
        if (NumericInputHelper.TryParseFlexibleDouble(value, out double parsed))
        {
            if (Math.Abs(ThresholdLo - parsed) > 1e-9)
            {
                ThresholdLo = parsed;
            }
        }
    }

    partial void OnThresholdLoChanged(double value)
    {
        if (!NumericInputHelper.TryParseFlexibleDouble(ThresholdLoText, out double cur) || Math.Abs(cur - value) > 1e-9)
        {
            ThresholdLoText = NumericInputHelper.FormatDouble(value);
        }
    }

    partial void OnThresholdHiTextChanged(string value)
    {
        if (NumericInputHelper.TryParseFlexibleDouble(value, out double parsed))
        {
            if (Math.Abs(ThresholdHi - parsed) > 1e-9)
            {
                ThresholdHi = parsed;
            }
        }
    }

    partial void OnThresholdHiChanged(double value)
    {
        if (!NumericInputHelper.TryParseFlexibleDouble(ThresholdHiText, out double cur) || Math.Abs(cur - value) > 1e-9)
        {
            ThresholdHiText = NumericInputHelper.FormatDouble(value);
        }
    }

    public string ConditionExpression
    {
        get
        {
            if (CompareOp == CompareOp.NONE) return "Any Change";
            string loStr = ThresholdLo.ToString("0.##");
            string hiStr = ThresholdHi.ToString("0.##");
            string op = CompareOp switch
            {
                CompareOp.EQ => $"== {loStr}",
                CompareOp.NEQ => $"!= {loStr}",
                CompareOp.GT => $"> {loStr}",
                CompareOp.LT => $"< {loStr}",
                CompareOp.GTE => $">= {loStr}",
                CompareOp.LTE => $"<= {loStr}",
                CompareOp.BETWEEN => $"in [{loStr}..{hiStr}]",
                _ => "Pass"
            };
            return op;
        }
    }

    public string CompareOpBadgeText
    {
        get
        {
            string loStr = ThresholdLo.ToString("0.##");
            string hiStr = ThresholdHi.ToString("0.##");
            return CompareOp switch
            {
                CompareOp.EQ => string.Format(LocalizationService.Tr("BadgeCompareEq"), loStr),
                CompareOp.NEQ => string.Format(LocalizationService.Tr("BadgeCompareNeq"), loStr),
                CompareOp.GT => string.Format(LocalizationService.Tr("BadgeCompareGt"), loStr),
                CompareOp.LT => string.Format(LocalizationService.Tr("BadgeCompareLt"), loStr),
                CompareOp.GTE => string.Format(LocalizationService.Tr("BadgeCompareGte"), loStr),
                CompareOp.LTE => string.Format(LocalizationService.Tr("BadgeCompareLte"), loStr),
                CompareOp.BETWEEN => string.Format(LocalizationService.Tr("BadgeCompareBetween"), loStr, hiStr),
                _ => LocalizationService.Tr("BadgeCompareNone")
            };
        }
    }

    public TriggerNodeViewModel()
    {
        Title = "Trigger";
        Icon = "Trigger";
        InputConnectors.Add(new ConnectorViewModel { Title = "In", TooltipKey = "PinTooltipIn", IsInput = true, IsEvent = false, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", TooltipKey = "PinTooltipOut", IsInput = false, IsEvent = true, Node = this });
        ThresholdLoText = NumericInputHelper.FormatDouble(ThresholdLo);
        ThresholdHiText = NumericInputHelper.FormatDouble(ThresholdHi);
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

    /// <summary>
    /// Đánh giá điều kiện Trigger dựa trên giá trị thời gian thực (cho Live Debug / Online Monitor).
    /// Hỗ trợ đầy đủ phát hiện sườn lên (ON_RISE), sườn xuống (ON_FALL), biến thiên (ON_CHANGE),
    /// so sánh ngưỡng và khung giờ RTC.
    /// </summary>
    public bool EvaluateCondition(int value, int? prevValue = null)
    {
        if (TriggerType == TriggerType.ON_RISE)
        {
            if (prevValue == null) return false;
            return prevValue.Value == 0 && value != 0;
        }

        if (TriggerType == TriggerType.ON_FALL)
        {
            if (prevValue == null) return false;
            return prevValue.Value != 0 && value == 0;
        }

        if (TriggerType == TriggerType.ON_CHANGE && !HasComparison)
        {
            if (prevValue == null) return false;
            return prevValue.Value != value;
        }

        if (TriggerType == TriggerType.TIME_WINDOW)
        {
            var now = DateTime.Now;
            int currentHhmm = now.Hour * 100 + now.Minute;
            if (CompareOp == CompareOp.EQ)
            {
                return currentHhmm == (int)Math.Round(ThresholdLo);
            }
            int start = (int)Math.Round(ThresholdLo);
            int end = (int)Math.Round(ThresholdHi);
            return start <= end
                ? (currentHhmm >= start && currentHhmm <= end)
                : (currentHhmm >= start || currentHhmm <= end);
        }

        if (HasComparison)
        {
            bool isFloat = Math.Abs(ThresholdLo - Math.Round(ThresholdLo)) > 1e-6 ||
                           Math.Abs(ThresholdHi - Math.Round(ThresholdHi)) > 1e-6;

            if (isFloat)
            {
                float valFloat = BitConverter.Int32BitsToSingle(value);
                float lo = (float)ThresholdLo;
                float hi = (float)ThresholdHi;
                return CompareOp switch
                {
                    CompareOp.EQ => Math.Abs(valFloat - lo) <= 1e-5f,
                    CompareOp.NEQ => Math.Abs(valFloat - lo) > 1e-5f,
                    CompareOp.GT => valFloat > lo,
                    CompareOp.LT => valFloat < lo,
                    CompareOp.GTE => valFloat >= lo,
                    CompareOp.LTE => valFloat <= lo,
                    CompareOp.BETWEEN => valFloat >= lo && valFloat <= hi,
                    _ => valFloat != 0
                };
            }

            int loInt = (int)Math.Round(ThresholdLo);
            int hiInt = (int)Math.Round(ThresholdHi);
            return CompareOp switch
            {
                CompareOp.EQ => value == loInt,
                CompareOp.NEQ => value != loInt,
                CompareOp.GT => value > loInt,
                CompareOp.LT => value < loInt,
                CompareOp.GTE => value >= loInt,
                CompareOp.LTE => value <= loInt,
                CompareOp.BETWEEN => value >= loInt && value <= hiInt,
                _ => value != 0
            };
        }

        return value != 0;
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
                        ? (LiveRawValue != 0 ? "1" : "0")
                        : LiveRawValue.ToString("N0");
                    return $"{baseText} ({GuardTagShortText}: {tagVal})";
                }
                return baseText;
            }

            string tagValOnline = GuardTag.IsDigital
                ? (LiveRawValue != 0 ? "1" : "0")
                : LiveRawValue.ToString("N0");

            string staleSuffix = LiveQuality == TagQuality.Stale ? " [STALE]" : "";
            return $"{baseText} ({GuardTagShortText}: {tagValOnline}){staleSuffix}";
        }
    }

    public string SimButtonContent => (GuardTag != null && GuardTag.Value == 1)
        ? "● 1"
        : "○ 0";

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
        Icon = "Guard";
        _guardTag = guardTag;
        InputConnectors.Add(new ConnectorViewModel { Title = "In", TooltipKey = "PinTooltipIn", IsInput = true, IsEvent = true, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", TooltipKey = "PinTooltipOut", IsInput = false, IsEvent = true, Node = this });
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
    private string _actionParamText = "1";

    public bool IsTargetBoolean => TargetTag == null || TargetTag.IsDigital || TargetTag.DataType == TagDataType.Boolean;
    public bool IsTargetReal => TargetTag != null && TargetTag.DataType == TagDataType.Float32;
    public bool IsTargetInteger => TargetTag != null && !TargetTag.IsDigital && TargetTag.DataType != TagDataType.Float32;

    private bool _isUpdatingFromText;

    partial void OnActionParamChanged(int value)
    {
        if (_isUpdatingFromText) return;

        if (IsTargetBoolean)
        {
            ActionParamText = SmartInputParser.FormatBool(value);
        }
        else if (IsTargetReal)
        {
            ActionParamText = SmartInputParser.FormatReal(BitConverter.Int32BitsToSingle(value));
        }
        else
        {
            ActionParamText = value.ToString();
        }
    }

    partial void OnActionParamTextChanged(string value)
    {
        if (SmartInputParser.TryParseForTag(value, TargetTag, out int rawVal, out _))
        {
            _isUpdatingFromText = true;
            try
            {
                ActionParam = IsTargetBoolean ? (rawVal != 0 ? 1 : 0) : rawVal;
            }
            finally
            {
                _isUpdatingFromText = false;
            }
        }
    }

    [RelayCommand]
    public void SetActionParamBool(object? param)
    {
        int val = 1;
        if (param is int i) val = i;
        else if (param is string s && SmartInputParser.TryParseBool(s, out int parsed)) val = parsed;

        ActionParam = val != 0 ? 1 : 0;
        ActionParamText = ActionParam.ToString();
    }

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
    [NotifyPropertyChangedFor(nameof(IsTargetBoolean))]
    [NotifyPropertyChangedFor(nameof(IsTargetReal))]
    [NotifyPropertyChangedFor(nameof(IsTargetInteger))]
    private TagModel? _targetTag;


    public string TargetTagShortText
    {
        get
        {
            if (TargetTag == null) 
                return LocalizationService.Instance.IsVietnamese ? "Chưa chọn Tag" : "No Target";
            
            if (!string.IsNullOrWhiteSpace(TargetTag.Alias))
                return TargetTag.Alias;

            if (!string.IsNullOrWhiteSpace(TargetTag.DisplayName))
                return TargetTag.DisplayName;

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
                string suffix = LiveQuality == TagQuality.Stale ? " [STALE]" : "";
                return $"{baseText} [Current: {LiveRawValue:N0}]{suffix}";
            }

            string stateStr = LiveRawValue != 0 ? "ON" : "OFF";
            string staleSuffix = LiveQuality == TagQuality.Stale ? " [STALE]" : "";
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
        OnPropertyChanged(nameof(IsTargetBoolean));
        OnPropertyChanged(nameof(IsTargetReal));
        OnPropertyChanged(nameof(IsTargetInteger));

        if (IsTargetBoolean)
        {
            if (ActionParam != 0 && ActionParam != 1)
            {
                ActionParam = 1;
            }
            ActionParamText = SmartInputParser.FormatBool(ActionParam);
        }
        else if (IsTargetReal)
        {
            ActionParamText = SmartInputParser.FormatReal(BitConverter.Int32BitsToSingle(ActionParam));
        }
        else
        {
            ActionParamText = ActionParam.ToString();
        }
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
                ActionType.SET_TAG => IsTargetBoolean ? $"{tag} = {SmartInputParser.FormatBool(ActionParam)}" : $"{tag} = {ActionParam}", 
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
                ActionType.SET_TAG => IsTargetBoolean ? $"{tag} = {SmartInputParser.FormatBool(ActionParam)}" : $"{tag} = {ActionParam}", 
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
                ActionType.SET_TAG => IsTargetBoolean ? $"Set {tgt} = {SmartInputParser.FormatBool(ActionParam)}" : $"Set {tgt} = {ActionParam}", 
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
        Icon = "Action";
        TargetTag = target;
        InputConnectors.Add(new ConnectorViewModel { Title = "In", TooltipKey = "PinTooltipIn", IsInput = true, IsEvent = true, Node = this });
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

public class TimeUnitOption : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    public string Key { get; }
    public string LocKey { get; }
    public double MultiplierToMs { get; }

    public string Label => LocalizationService.Instance.Get(LocKey);

    public TimeUnitOption(string key, string locKey, double multiplier)
    {
        Key = key;
        LocKey = locKey;
        MultiplierToMs = multiplier;
        LocalizationService.Instance.LanguageChanged += () =>
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Label)));
        };
    }
}

public partial class TimerNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    public static IReadOnlyList<TimeUnitOption> AvailableUnits { get; } = new List<TimeUnitOption>
    {
        new("ms", "TimeUnit_ms", 1.0),
        new("s", "TimeUnit_s", 1000.0),
        new("min", "TimeUnit_min", 60000.0),
        new("h", "TimeUnit_h", 3600000.0)
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCycleMode))]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(TimerModeBadgeText))]
    [NotifyPropertyChangedFor(nameof(ModeGlyph))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    private string _timerMode = "TON"; // TON, TOF, TP, CYCLE

    public bool IsCycleMode => string.Equals(TimerMode, "CYCLE", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    [NotifyPropertyChangedFor(nameof(CalculatedOnMsSummary))]
    private uint _onTimeMs = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    [NotifyPropertyChangedFor(nameof(CalculatedOffMsSummary))]
    private uint _offTimeMs = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(StartPhaseIndex))]
    private bool _startWithOn = true;

    public int StartPhaseIndex
    {
        get => StartWithOn ? 0 : 1;
        set
        {
            if (value != StartPhaseIndex)
            {
                StartWithOn = value == 0;
                OnPropertyChanged(nameof(StartPhaseIndex));
            }
        }
    }

    [ObservableProperty]
    private string _onTimeUnit = "s";

    [ObservableProperty]
    private string _onDurationValueText = "1";

    public double OnDurationValue { get; private set; } = 1.0;

    [ObservableProperty]
    private string _offTimeUnit = "s";

    [ObservableProperty]
    private string _offDurationValueText = "1";

    public double OffDurationValue { get; private set; } = 1.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(PresetText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(CalculatedMsSummary))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(TimerStatusPillText))]
    private uint _presetMs = 3000;

    [ObservableProperty]
    private string _timeUnit = "s";

    [ObservableProperty]
    private string _durationValueText = "3";

    public double DurationValue { get; private set; } = 3.0;

    private bool _isInternalUpdating = false;

    partial void OnPresetMsChanged(uint value)
    {
        if (!_isInternalUpdating)
        {
            SyncFromPresetMs();
        }
    }

    partial void OnTimeUnitChanged(string value)
    {
        RecalculatePresetMsFromInput();
    }

    partial void OnDurationValueTextChanged(string value)
    {
        RecalculatePresetMsFromInput();
    }

    partial void OnOnTimeMsChanged(uint value)
    {
        if (!_isInternalUpdating)
        {
            SyncFromOnTimeMs();
        }
    }

    partial void OnOffTimeMsChanged(uint value)
    {
        if (!_isInternalUpdating)
        {
            SyncFromOffTimeMs();
        }
    }

    partial void OnOnTimeUnitChanged(string value)
    {
        RecalculateOnTimeMsFromInput();
    }

    partial void OnOnDurationValueTextChanged(string value)
    {
        RecalculateOnTimeMsFromInput();
    }

    partial void OnOffTimeUnitChanged(string value)
    {
        RecalculateOffTimeMsFromInput();
    }

    partial void OnOffDurationValueTextChanged(string value)
    {
        RecalculateOffTimeMsFromInput();
    }

    private void RecalculateOnTimeMsFromInput()
    {
        if (_isInternalUpdating) return;
        if (double.TryParse(OnDurationValueText.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val) && val >= 0)
        {
            OnDurationValue = val;
            double factor = OnTimeUnit switch
            {
                "h" => 3600000.0,
                "min" => 60000.0,
                "s" => 1000.0,
                _ => 1.0
            };
            uint computed = (uint)Math.Clamp(Math.Round(val * factor), 1, uint.MaxValue);
            _isInternalUpdating = true;
            try
            {
                OnTimeMs = computed;
            }
            finally
            {
                _isInternalUpdating = false;
            }
            OnPropertyChanged(nameof(CalculatedOnMsSummary));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(NarrativeText));
            OnPropertyChanged(nameof(SimulationProgressText));
        }
    }

    private void RecalculateOffTimeMsFromInput()
    {
        if (_isInternalUpdating) return;
        if (double.TryParse(OffDurationValueText.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val) && val >= 0)
        {
            OffDurationValue = val;
            double factor = OffTimeUnit switch
            {
                "h" => 3600000.0,
                "min" => 60000.0,
                "s" => 1000.0,
                _ => 1.0
            };
            uint computed = (uint)Math.Clamp(Math.Round(val * factor), 1, uint.MaxValue);
            _isInternalUpdating = true;
            try
            {
                OffTimeMs = computed;
            }
            finally
            {
                _isInternalUpdating = false;
            }
            OnPropertyChanged(nameof(CalculatedOffMsSummary));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(NarrativeText));
            OnPropertyChanged(nameof(SimulationProgressText));
        }
    }

    public void SyncFromOnTimeMs()
    {
        _isInternalUpdating = true;
        try
        {
            if (OnTimeMs >= 3600000 && OnTimeMs % 3600000 == 0)
            {
                OnTimeUnit = "h";
                OnDurationValue = OnTimeMs / 3600000.0;
                OnDurationValueText = OnDurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (OnTimeMs >= 60000 && OnTimeMs % 60000 == 0)
            {
                OnTimeUnit = "min";
                OnDurationValue = OnTimeMs / 60000.0;
                OnDurationValueText = OnDurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (OnTimeMs >= 1000 && OnTimeMs % 1000 == 0)
            {
                OnTimeUnit = "s";
                OnDurationValue = OnTimeMs / 1000.0;
                OnDurationValueText = OnDurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                OnTimeUnit = "ms";
                OnDurationValue = OnTimeMs;
                OnDurationValueText = OnTimeMs.ToString();
            }
            OnPropertyChanged(nameof(CalculatedOnMsSummary));
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    public void SyncFromOffTimeMs()
    {
        _isInternalUpdating = true;
        try
        {
            if (OffTimeMs >= 3600000 && OffTimeMs % 3600000 == 0)
            {
                OffTimeUnit = "h";
                OffDurationValue = OffTimeMs / 3600000.0;
                OffDurationValueText = OffDurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (OffTimeMs >= 60000 && OffTimeMs % 60000 == 0)
            {
                OffTimeUnit = "min";
                OffDurationValue = OffTimeMs / 60000.0;
                OffDurationValueText = OffDurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (OffTimeMs >= 1000 && OffTimeMs % 1000 == 0)
            {
                OffTimeUnit = "s";
                OffDurationValue = OffTimeMs / 1000.0;
                OffDurationValueText = OffDurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                OffTimeUnit = "ms";
                OffDurationValue = OffTimeMs;
                OffDurationValueText = OffTimeMs.ToString();
            }
            OnPropertyChanged(nameof(CalculatedOffMsSummary));
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    public string CalculatedOnMsSummary => $"= {OnTimeMs:N0} ms ({FormatDurationHumanReadable(OnTimeMs)})";
    public string CalculatedOffMsSummary => $"= {OffTimeMs:N0} ms ({FormatDurationHumanReadable(OffTimeMs)})";

    private void RecalculatePresetMsFromInput()
    {
        if (_isInternalUpdating) return;

        if (double.TryParse(DurationValueText.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val) && val >= 0)
        {
            DurationValue = val;
            double factor = TimeUnit switch
            {
                "h" => 3600000.0,
                "min" => 60000.0,
                "s" => 1000.0,
                _ => 1.0
            };
            uint computed = (uint)Math.Clamp(Math.Round(val * factor), 1, uint.MaxValue);
            _isInternalUpdating = true;
            try
            {
                PresetMs = computed;
            }
            finally
            {
                _isInternalUpdating = false;
            }
            OnPropertyChanged(nameof(CalculatedMsSummary));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(PresetText));
            OnPropertyChanged(nameof(NarrativeText));
        }
    }

    public void SyncFromPresetMs()
    {
        _isInternalUpdating = true;
        try
        {
            if (PresetMs >= 3600000 && PresetMs % 3600000 == 0)
            {
                TimeUnit = "h";
                DurationValue = PresetMs / 3600000.0;
                DurationValueText = DurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (PresetMs >= 60000 && PresetMs % 60000 == 0)
            {
                TimeUnit = "min";
                DurationValue = PresetMs / 60000.0;
                DurationValueText = DurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (PresetMs >= 1000 && PresetMs % 1000 == 0)
            {
                TimeUnit = "s";
                DurationValue = PresetMs / 1000.0;
                DurationValueText = DurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (PresetMs >= 100 && PresetMs % 100 == 0)
            {
                TimeUnit = "s";
                DurationValue = PresetMs / 1000.0;
                DurationValueText = DurationValue.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            }
            else
            {
                TimeUnit = "ms";
                DurationValue = PresetMs;
                DurationValueText = PresetMs.ToString();
            }

            OnPropertyChanged(nameof(CalculatedMsSummary));
        }
        finally
        {
            _isInternalUpdating = false;
        }
    }

    public void SetDuration(double val, string unit)
    {
        _isInternalUpdating = true;
        try
        {
            TimeUnit = AvailableUnits.Any(u => u.Key == unit) ? unit : "s";
            DurationValue = val;
            DurationValueText = val.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        }
        finally
        {
            _isInternalUpdating = false;
        }
        RecalculatePresetMsFromInput();
    }

    public static string FormatDurationHumanReadable(uint ms)
    {
        if (ms >= 3600000 && ms % 3600000 == 0) return $"{ms / 3600000}h";
        if (ms >= 60000 && ms % 60000 == 0) return $"{ms / 60000}min";
        if (ms >= 1000 && ms % 1000 == 0) return $"{ms / 1000}s";
        if (ms >= 1000) return $"{(ms / 1000.0):0.##}s";
        return $"{ms}ms";
    }

    public string CalculatedMsSummary => $"= {PresetMs:N0} ms ({FormatDurationHumanReadable(PresetMs)})";

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

    public string SimulationProgressText => IsCycleMode
        ? $"ON: {FormatDurationHumanReadable(OnTimeMs)} | OFF: {FormatDurationHumanReadable(OffTimeMs)}"
        : (IsTiming
            ? $"{ElapsedMs} / {PresetMs} ms"
            : (IsLiveActive ? $"DONE · {PresetMs} ms" : $"PT: {PresetMs} ms"));

    public string TimerStatusPillText
    {
        get
        {
            if (IsCycleMode)
            {
                return IsLiveActive ? "● ON" : "● OFF";
            }
            return IsLiveActive
                ? "Q = ON"
                : (IsTiming ? $"{(int)ProgressPercent}% ({FormatDurationHumanReadable(PresetMs)})" : $"{FormatDurationHumanReadable(PresetMs)} (OFF)");
        }
    }

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
        ? (IsCycleMode ? LocalizationService.Tr("DisplayTitleCycleTimer") : $"{TimerMode} Timer")
        : CustomLabel;

    public string TimerModeBadgeText => TimerMode switch
    {
        "TOF" => LocalizationService.Tr("TimerBadgeTof"),
        "TP" => LocalizationService.Tr("TimerBadgeTp"),
        "CYCLE" => LocalizationService.Tr("TimerBadgeCycle"),
        _ => LocalizationService.Tr("TimerBadgeTon")
    };

    public string ModeGlyph => TimerMode switch
    {
        "TOF" => "TOF",
        "TP" => "TP",
        "CYCLE" => "CYCLE",
        _ => "TON"
    };

    public string PresetText => $"{PresetMs:N0} ms ({FormatDurationHumanReadable(PresetMs)})";

    public string OutputBadgeText => OutputTag != null ? OutputTag.Name : LocalizationService.Tr("BadgeNoQ");

    public string InputBadgeText => InputTag != null ? InputTag.Name : LocalizationService.Tr("BadgeInSignal");

    public string NarrativeText
    {
        get
        {
            string inTag = InputTag?.DisplayName ?? LocalizationService.Tr("NarrNoTag");
            string outTag = OutputTag?.DisplayName ?? LocalizationService.Tr("NarrNoTargetTag");
            string sec = PresetMs >= 1000
                ? string.Format(LocalizationService.Tr("NarrDwellSec"), FormatDurationHumanReadable(PresetMs))
                : $"{PresetMs} ms";
            return TimerMode switch
            {
                "TOF" => string.Format(LocalizationService.Tr("NarrTimerTof"), inTag, outTag, PresetMs, sec),
                "TP" => string.Format(LocalizationService.Tr("NarrTimerTp"), inTag, outTag, PresetMs, sec),
                "CYCLE" => string.Format(LocalizationService.Tr("NarrTimerCycle"), inTag, outTag, FormatDurationHumanReadable(OnTimeMs), FormatDurationHumanReadable(OffTimeMs)),
                _ => string.Format(LocalizationService.Tr("NarrTimerTon"), inTag, outTag, PresetMs, sec)
            };
        }
    }

    public override string SummaryText => IsCycleMode
        ? $"CYCLE ({FormatDurationHumanReadable(OnTimeMs)}/{FormatDurationHumanReadable(OffTimeMs)}) → {OutputTag?.Name ?? "--"}"
        : $"{TimerMode} ({FormatDurationHumanReadable(PresetMs)}) → {OutputTag?.Name ?? "--"}";

    public TimerNodeViewModel(string mode = "TON", TagModel? inTag = null, TagModel? qTag = null, uint presetMs = 3000, uint onTimeMs = 1000, uint offTimeMs = 1000, bool startWithOn = true)
    {
        Title = string.Equals(mode, "CYCLE", StringComparison.OrdinalIgnoreCase) ? "Cycle Timer" : $"{mode} Timer";
        Icon = mode;
        TimerMode = mode;
        InputTag = inTag;
        OutputTag = qTag;
        _presetMs = presetMs;
        _onTimeMs = onTimeMs;
        _offTimeMs = offTimeMs;
        _startWithOn = startWithOn;
        SyncFromPresetMs();
        SyncFromOnTimeMs();
        SyncFromOffTimeMs();

        // Standard FBD construct: In as input, Q as output
        InputConnectors.Add(new ConnectorViewModel { Title = "In", TooltipKey = "PinTooltipIn", IsInput = true, IsEvent = true, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Q", TooltipKey = "PinTooltipQ", IsInput = false, IsEvent = false, Node = this });
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
        OnPropertyChanged(nameof(TimeUnit));
        OnPropertyChanged(nameof(TimerStatusPillText));
        OnPropertyChanged(nameof(CalculatedMsSummary));
        OnPropertyChanged(nameof(CalculatedOnMsSummary));
        OnPropertyChanged(nameof(CalculatedOffMsSummary));
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
        "CTD" => "CTD",
        _ => "CTU"
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
        Icon = "Counter";
        CounterMode = mode;
        InputTag = inTag;
        CvTag = cvTag;
        OutputTag = qTag;
        ResetTag = resetTag;
        PresetValue = presetValue;

        // Standard FBD construct: CU (or CD) and R as inputs, Q as output
        string countPort = mode == "CTD" ? "CD" : "CU";
        string countTooltipKey = mode == "CTD" ? "PinTooltipCD" : "PinTooltipCU";
        InputConnectors.Add(new ConnectorViewModel { Title = countPort, TooltipKey = countTooltipKey, IsInput = true, IsEvent = true, Node = this });
        InputConnectors.Add(new ConnectorViewModel { Title = "R", TooltipKey = "PinTooltipR", IsInput = true, IsEvent = true, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Q", TooltipKey = "PinTooltipQ", IsInput = false, IsEvent = false, Node = this });
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
