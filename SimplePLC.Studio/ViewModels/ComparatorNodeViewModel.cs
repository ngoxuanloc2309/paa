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
    private TagModel? _inputTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(OperatorSymbol))]
    [NotifyPropertyChangedFor(nameof(IsRangeCompare))]
    private CompareOp _compareOp = CompareOp.GT;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    private int _thresholdLo = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    private int _thresholdHi = 100;

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
            return CompareOp switch
            {
                CompareOp.EQ => $"{tagName} == {ThresholdLo}",
                CompareOp.NEQ => $"{tagName} != {ThresholdLo}",
                CompareOp.GT => $"{tagName} > {ThresholdLo}",
                CompareOp.LT => $"{tagName} < {ThresholdLo}",
                CompareOp.GTE => $"{tagName} >= {ThresholdLo}",
                CompareOp.LTE => $"{tagName} <= {ThresholdLo}",
                CompareOp.BETWEEN => $"{ThresholdLo} <= {tagName} <= {ThresholdHi}",
                _ => $"{tagName} > {ThresholdLo}"
            };
        }
    }

    public string ConditionPassedText => IsConditionMet ? "✓ TRUE" : "FALSE";

    public override string DisplayTitle => string.IsNullOrWhiteSpace(CustomLabel)
        ? "CMP (Comparator)"
        : CustomLabel;

    public override string SummaryText => FormulaExpression;

    public ComparatorNodeViewModel(TagModel? inTag = null, CompareOp op = CompareOp.GT, int lo = 50, int hi = 100)
    {
        Title = "Comparator";
        Icon = "⚖️";
        InputTag = inTag;
        CompareOp = op;
        ThresholdLo = lo;
        ThresholdHi = hi;

        // Standard IEC 61131-3 Function construct: 1 Input (IN), 1 Output (OUT: BOOL)
        InputConnectors.Add(new ConnectorViewModel { Title = "In", IsInput = true, IsEvent = false, Node = this });
        OutputConnectors.Add(new ConnectorViewModel { Title = "Out", IsInput = false, IsEvent = false, Node = this });
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
        return CompareOp switch
        {
            CompareOp.EQ => rawValue == ThresholdLo,
            CompareOp.NEQ => rawValue != ThresholdLo,
            CompareOp.GT => rawValue > ThresholdLo,
            CompareOp.LT => rawValue < ThresholdLo,
            CompareOp.GTE => rawValue >= ThresholdLo,
            CompareOp.LTE => rawValue <= ThresholdLo,
            CompareOp.BETWEEN => rawValue >= ThresholdLo && rawValue <= ThresholdHi,
            _ => rawValue > ThresholdLo
        };
    }
}
