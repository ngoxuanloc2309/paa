using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa Toán tử So sánh Số CMP (IEC 61131-3 Comparator).
/// Đánh giá quan hệ giữa giá trị Tag đầu vào (AI, VREG, COUNTER_CV) với ngưỡng tham chiếu
/// và xuất ra tín hiệu logic BOOL thuần túy (True/False).
/// </summary>
public partial class ComparatorNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    public ushort? BoundTagIndex => (InputTag != null && InputTag.Kind != TagKind.None && InputTag.Index < 65535) ? (ushort)InputTag.Index : null;

    private bool _isFormattingText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(BoundTagIndex))]
    [NotifyPropertyChangedFor(nameof(InputConnectionBadge))]
    [NotifyPropertyChangedFor(nameof(HasWireConnection))]
    [NotifyPropertyChangedFor(nameof(IsInputReal))]
    [NotifyPropertyChangedFor(nameof(IsInputBoolean))]
    [NotifyPropertyChangedFor(nameof(IsInputInteger))]
    [NotifyPropertyChangedFor(nameof(InputTagDataTypeText))]
    private TagModel? _inputTag;

    public bool IsInputReal => InputTag != null && InputTag.DataType == TagDataType.Float32;
    public bool IsInputBoolean => InputTag != null && !IsInputReal && (InputTag.IsDigital || InputTag.DataType == TagDataType.Boolean);
    public bool IsInputInteger => InputTag != null && !IsInputBoolean && !IsInputReal;
    public string InputTagDataTypeText => InputTag?.DataTypeText ?? (InputTag != null ? (IsInputReal ? "REAL" : (IsInputBoolean ? "BOOL" : "INT32")) : "INT32");

    public string InputConnectionBadge => InputTag != null
        ? $"🔗 Connected: {InputTag.Name}"
        : string.Empty;

    public bool HasWireConnection => InputTag != null;

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
        OnPropertyChanged(nameof(FormulaExpression));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(InputConnectionBadge));
        OnPropertyChanged(nameof(HasWireConnection));
        OnPropertyChanged(nameof(IsInputReal));
        OnPropertyChanged(nameof(IsInputBoolean));
        OnPropertyChanged(nameof(IsInputInteger));
        OnPropertyChanged(nameof(InputTagDataTypeText));

        _isFormattingText = true;
        try
        {
            if (IsInputReal)
            {
                ThresholdLoText = SmartInputParser.FormatReal(ThresholdLo);
                ThresholdHiText = SmartInputParser.FormatReal(ThresholdHi);
            }
            else if (IsInputBoolean)
            {
                ThresholdLoText = (int)ThresholdLo != 0 ? "1" : "0";
                ThresholdHiText = (int)ThresholdHi != 0 ? "1" : "0";
            }
            else
            {
                ThresholdLoText = ((long)Math.Round(ThresholdLo)).ToString();
                ThresholdHiText = ((long)Math.Round(ThresholdHi)).ToString();
            }
        }
        finally
        {
            _isFormattingText = false;
        }
    }

    private void OnTagPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TagModel.Name) || e.PropertyName == nameof(TagModel.DisplayName) || e.PropertyName == nameof(TagModel.Alias) || e.PropertyName == nameof(TagModel.DataType) || e.PropertyName == nameof(TagModel.IsDigital))
        {
            OnPropertyChanged(nameof(FormulaExpression));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(InputConnectionBadge));
            OnPropertyChanged(nameof(IsInputReal));
            OnPropertyChanged(nameof(IsInputBoolean));
            OnPropertyChanged(nameof(IsInputInteger));
            OnPropertyChanged(nameof(InputTagDataTypeText));
        }
    }

    public override void Dispose()
    {
        if (InputTag != null)
        {
            InputTag.PropertyChanged -= OnTagPropertyChanged;
        }
        base.Dispose();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(OperatorSymbol))]
    [NotifyPropertyChangedFor(nameof(IsRangeCompare))]
    private CompareOp _compareOp = CompareOp.GT;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    private double _thresholdLo = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    private double _thresholdHi = 100;

    [ObservableProperty]
    private string _thresholdLoText = "50";

    [ObservableProperty]
    private string _thresholdHiText = "100";

    partial void OnThresholdLoTextChanged(string value)
    {
        if (_isFormattingText) return;

        if (IsInputReal)
        {
            if (SmartInputParser.TryParseFlexibleDouble(value, out double parsed))
            {
                if (Math.Abs(ThresholdLo - parsed) > 1e-9)
                {
                    ThresholdLo = parsed;
                }
            }
        }
        else if (IsInputBoolean)
        {
            if (SmartInputParser.TryParseBool(value, out int parsed))
            {
                ThresholdLo = parsed != 0 ? 1 : 0;
            }
        }
        else if (IsInputInteger)
        {
            if (SmartInputParser.TryParseInt(value, out int parsed))
            {
                ThresholdLo = parsed;
            }
        }
        else // unattached
        {
            if (NumericInputHelper.TryParseFlexibleDouble(value, out double parsed))
            {
                if (Math.Abs(ThresholdLo - parsed) > 1e-9)
                {
                    ThresholdLo = parsed;
                }
            }
        }
    }

    partial void OnThresholdLoChanged(double value)
    {
        string expected = IsInputReal
            ? SmartInputParser.FormatReal(value)
            : (IsInputBoolean ? ((int)value != 0 ? "1" : "0") : (IsInputInteger ? ((long)Math.Round(value)).ToString() : NumericInputHelper.FormatDouble(value)));

        if (ThresholdLoText != expected)
        {
            _isFormattingText = true;
            try
            {
                ThresholdLoText = expected;
            }
            finally
            {
                _isFormattingText = false;
            }
        }
    }

    partial void OnThresholdHiTextChanged(string value)
    {
        if (_isFormattingText) return;

        if (IsInputReal)
        {
            if (SmartInputParser.TryParseFlexibleDouble(value, out double parsed))
            {
                if (Math.Abs(ThresholdHi - parsed) > 1e-9)
                {
                    ThresholdHi = parsed;
                }
            }
        }
        else if (IsInputBoolean)
        {
            if (SmartInputParser.TryParseBool(value, out int parsed))
            {
                ThresholdHi = parsed != 0 ? 1 : 0;
            }
        }
        else if (IsInputInteger)
        {
            if (SmartInputParser.TryParseInt(value, out int parsed))
            {
                ThresholdHi = parsed;
            }
        }
        else // unattached
        {
            if (NumericInputHelper.TryParseFlexibleDouble(value, out double parsed))
            {
                if (Math.Abs(ThresholdHi - parsed) > 1e-9)
                {
                    ThresholdHi = parsed;
                }
            }
        }
    }

    partial void OnThresholdHiChanged(double value)
    {
        string expected = IsInputReal
            ? SmartInputParser.FormatReal(value)
            : (IsInputBoolean ? ((int)value != 0 ? "1" : "0") : (IsInputInteger ? ((long)Math.Round(value)).ToString() : NumericInputHelper.FormatDouble(value)));

        if (ThresholdHiText != expected)
        {
            _isFormattingText = true;
            try
            {
                ThresholdHiText = expected;
            }
            finally
            {
                _isFormattingText = false;
            }
        }
    }

    [RelayCommand]
    public void CommitThresholdLo()
    {
        if (IsInputReal)
        {
            if (SmartInputParser.TryParseFlexibleDouble(ThresholdLoText, out double parsed))
            {
                ThresholdLo = parsed;
                ThresholdLoText = SmartInputParser.FormatReal(parsed);
            }
            else
            {
                ThresholdLoText = SmartInputParser.FormatReal(ThresholdLo);
            }
        }
        else if (IsInputBoolean)
        {
            if (SmartInputParser.TryParseBool(ThresholdLoText, out int parsed))
            {
                ThresholdLo = parsed != 0 ? 1 : 0;
                ThresholdLoText = ((int)ThresholdLo).ToString();
            }
            else
            {
                ThresholdLoText = ((int)ThresholdLo).ToString();
            }
        }
        else if (IsInputInteger)
        {
            if (SmartInputParser.TryParseInt(ThresholdLoText, out int parsed))
            {
                ThresholdLo = parsed;
                ThresholdLoText = parsed.ToString();
            }
            else
            {
                ThresholdLoText = ((long)Math.Round(ThresholdLo)).ToString();
            }
        }
        else // unattached
        {
            if (NumericInputHelper.TryParseFlexibleDouble(ThresholdLoText, out double parsed))
            {
                ThresholdLo = parsed;
                ThresholdLoText = NumericInputHelper.FormatDouble(parsed);
            }
            else
            {
                ThresholdLoText = NumericInputHelper.FormatDouble(ThresholdLo);
            }
        }
    }

    [RelayCommand]
    public void CommitThresholdHi()
    {
        if (IsInputReal)
        {
            if (SmartInputParser.TryParseFlexibleDouble(ThresholdHiText, out double parsed))
            {
                ThresholdHi = parsed;
                ThresholdHiText = SmartInputParser.FormatReal(parsed);
            }
            else
            {
                ThresholdHiText = SmartInputParser.FormatReal(ThresholdHi);
            }
        }
        else if (IsInputBoolean)
        {
            if (SmartInputParser.TryParseBool(ThresholdHiText, out int parsed))
            {
                ThresholdHi = parsed != 0 ? 1 : 0;
                ThresholdHiText = ((int)ThresholdHi).ToString();
            }
            else
            {
                ThresholdHiText = ((int)ThresholdHi).ToString();
            }
        }
        else if (IsInputInteger)
        {
            if (SmartInputParser.TryParseInt(ThresholdHiText, out int parsed))
            {
                ThresholdHi = parsed;
                ThresholdHiText = parsed.ToString();
            }
            else
            {
                ThresholdHiText = ((long)Math.Round(ThresholdHi)).ToString();
            }
        }
        else // unattached
        {
            if (NumericInputHelper.TryParseFlexibleDouble(ThresholdHiText, out double parsed))
            {
                ThresholdHi = parsed;
                ThresholdHiText = NumericInputHelper.FormatDouble(parsed);
            }
            else
            {
                ThresholdHiText = NumericInputHelper.FormatDouble(ThresholdHi);
            }
        }
    }

    [RelayCommand]
    public void SetThresholdBool(object? param)
    {
        int val = 1;
        if (param is int i) val = i;
        else if (param is string s && SmartInputParser.TryParseBool(s, out int parsed)) val = parsed;

        ThresholdLo = val != 0 ? 1 : 0;
        ThresholdLoText = ((int)ThresholdLo).ToString();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConditionPassedText))]
    [NotifyPropertyChangedFor(nameof(IsLiveActive))]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isConditionMet;

    public bool IsLiveActive => IsConditionMet;

    public bool IsRangeCompare => CompareOp == CompareOp.BETWEEN;

    public string OperatorSymbol => CompareOp switch
    {
        CompareOp.EQ => "==",
        CompareOp.NEQ => "!=",
        CompareOp.GT => ">",
        CompareOp.LT => "<",
        CompareOp.GTE => ">=",
        CompareOp.LTE => "<=",
        CompareOp.BETWEEN => "IN_RANGE",
        _ => ">"
    };

    public string FormulaExpression
    {
        get
        {
            string tagName = (InputTag != null && InputTag.Kind != TagKind.None && !string.IsNullOrWhiteSpace(InputTag.Name))
                ? InputTag.Name
                : "IN";
            string loStr = IsInputReal 
                ? SmartInputParser.FormatReal(ThresholdLo) 
                : (IsInputBoolean ? ((int)ThresholdLo != 0 ? "1" : "0") : ((long)Math.Round(ThresholdLo)).ToString());
            string hiStr = IsInputReal 
                ? SmartInputParser.FormatReal(ThresholdHi) 
                : (IsInputBoolean ? ((int)ThresholdHi != 0 ? "1" : "0") : ((long)Math.Round(ThresholdHi)).ToString());
            return CompareOp switch
            {
                CompareOp.EQ => $"{tagName} == {loStr}",
                CompareOp.NEQ => $"{tagName} != {loStr}",
                CompareOp.GT => $"{tagName} > {loStr}",
                CompareOp.LT => $"{tagName} < {loStr}",
                CompareOp.GTE => $"{tagName} >= {loStr}",
                CompareOp.LTE => $"{tagName} <= {loStr}",
                CompareOp.BETWEEN => $"{loStr} <= {tagName} <= {hiStr}",
                _ => $"{tagName} > {loStr}"
            };
        }
    }

    public string ConditionPassedText => IsConditionMet ? "✓ TRUE" : "FALSE";

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel)
        ? "CMP (Comparator)"
        : CustomLabel;

    public override string SummaryText => FormulaExpression;

    public ComparatorNodeViewModel(TagModel? inTag = null, CompareOp op = CompareOp.GT, double lo = 50, double hi = 100)
    {
        Title = "Comparator";
        Icon = "⚖️";
        InputTag = inTag;
        CompareOp = op;
        ThresholdLo = lo;
        ThresholdHi = hi;
        ThresholdLoText = NumericInputHelper.FormatDouble(lo);
        ThresholdHiText = NumericInputHelper.FormatDouble(hi);

        // Standard IEC 61131-3 Function construct: 1 Input (IN), 1 Output (OUT: BOOL)
        InputConnectors.Add(new ConnectorViewModel { Title = "In", TooltipKey = "PinTooltipCmpIn", IsInput = true, IsEvent = false, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", TooltipKey = "PinTooltipCmpOut", IsInput = false, IsEvent = false, Node = this });
    }

    // ILiveTagBoundNode implementation for live monitoring
    [ObservableProperty]
    private int _liveRawValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isLiveOnline;

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
            if (IsConditionMet) return NodeLiveVisualState.Active;
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

        // Evaluate condition locally
        IsConditionMet = Evaluate(snapshot.RawValue);
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        OnPropertyChanged(nameof(LiveVisualState));
    }

    public bool Evaluate(int rawValue)
    {
        if (IsInputReal || Math.Abs(ThresholdLo - Math.Round(ThresholdLo)) > 1e-6 || Math.Abs(ThresholdHi - Math.Round(ThresholdHi)) > 1e-6)
        {
            float valFloat = BitConverter.Int32BitsToSingle(rawValue);
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
                _ => valFloat > lo
            };
        }

        if (IsInputBoolean)
        {
            int target = (int)ThresholdLo != 0 ? 1 : 0;
            return CompareOp switch
            {
                CompareOp.EQ => rawValue == target,
                CompareOp.NEQ => rawValue != target,
                _ => rawValue == target
            };
        }

        int loInt = (int)Math.Round(ThresholdLo);
        int hiInt = (int)Math.Round(ThresholdHi);
        return CompareOp switch
        {
            CompareOp.EQ => rawValue == loInt,
            CompareOp.NEQ => rawValue != loInt,
            CompareOp.GT => rawValue > loInt,
            CompareOp.LT => rawValue < loInt,
            CompareOp.GTE => rawValue >= loInt,
            CompareOp.LTE => rawValue <= loInt,
            CompareOp.BETWEEN => rawValue >= loInt && rawValue <= hiInt,
            _ => rawValue > loInt
        };
    }
}
