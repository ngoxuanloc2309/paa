using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(BoundTagIndex))]
    [NotifyPropertyChangedFor(nameof(InputConnectionBadge))]
    [NotifyPropertyChangedFor(nameof(HasWireConnection))]
    private TagModel? _inputTag;

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
        if (newValue?.DataType == SimplePLC.Domain.Enums.TagDataType.Float32)
        {
            ThresholdLoText = SmartInputParser.FormatReal(ThresholdLo);
            ThresholdHiText = SmartInputParser.FormatReal(ThresholdHi);
        }
    }

    private void OnTagPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TagModel.Name) || e.PropertyName == nameof(TagModel.DisplayName) || e.PropertyName == nameof(TagModel.Alias))
        {
            OnPropertyChanged(nameof(FormulaExpression));
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(InputConnectionBadge));
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
            bool isFloat = InputTag != null && InputTag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32;
            string loStr = isFloat 
                ? SmartInputParser.FormatReal(ThresholdLo) 
                : (InputTag?.IsDigital == true ? ((int)ThresholdLo != 0 ? "1" : "0") : ThresholdLo.ToString("0.##"));
            string hiStr = isFloat 
                ? SmartInputParser.FormatReal(ThresholdHi) 
                : ThresholdHi.ToString("0.##");
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
        bool isFloat = (InputTag != null && InputTag.DataType == TagDataType.Float32) ||
                       Math.Abs(ThresholdLo - Math.Round(ThresholdLo)) > 1e-6 ||
                       Math.Abs(ThresholdHi - Math.Round(ThresholdHi)) > 1e-6;

        if (isFloat)
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
