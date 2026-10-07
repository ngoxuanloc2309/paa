using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa Xử lý Tín hiệu Tương tự SCALE (Analog Linear Scaler / Amplifier).
/// Thực thi mô hình tuyến tính công nghiệp chuẩn: y = Gain * x + Offset.
/// Hỗ trợ Clamping an toàn và công cụ tính nhanh Gain & Offset từ 2 điểm hiệu chuẩn thực tế.
/// </summary>
public partial class ScaleNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    public ushort? BoundTagIndex => (InputTag != null && InputTag.Kind != TagKind.None && InputTag.Index < 65535) ? (ushort)InputTag.Index : null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(LiveFormattedValue))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(ScaleStatusPillText))]
    private double _gain = 0.01;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(LiveFormattedValue))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(ScaleStatusPillText))]
    private double _offset = 0.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(LiveFormattedValue))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(ScaleStatusPillText))]
    private string _unit = "bar";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveFormattedValue))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(ScaleStatusPillText))]
    private int _decimalPlaces = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private bool _isClamped = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private double _clampMin = 0.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private double _clampMax = 100.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(BoundTagIndex))]
    [NotifyPropertyChangedFor(nameof(InputBadgeText))]
    [NotifyPropertyChangedFor(nameof(InputConnectionSummary))]
    private TagModel? _inputTag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    [NotifyPropertyChangedFor(nameof(OutputBadgeText))]
    [NotifyPropertyChangedFor(nameof(OutputConnectionSummary))]
    private TagModel? _outputTag;

    // Two-Point Calibration Helper Properties
    [ObservableProperty]
    private double _calibX1 = 0.0;

    [ObservableProperty]
    private double _calibY1 = 0.0;

    [ObservableProperty]
    private double _calibX2 = 10000.0;

    [ObservableProperty]
    private double _calibY2 = 100.0;

    // Live Monitoring & Simulation
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    private int _liveRawInput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveFormattedValue))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    [NotifyPropertyChangedFor(nameof(ScaleStatusPillText))]
    private double _liveScaledValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScaleStatusPillText))]
    private double _liveProgressPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private TagQuality _liveQuality = TagQuality.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    private bool _isLiveOnline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveVisualState))]
    [NotifyPropertyChangedFor(nameof(SimulationProgressText))]
    private bool _isLiveActive;

    [ObservableProperty]
    private string _liveValueText = "--";

    [ObservableProperty]
    private bool _hasReceivedUpdate;

    public int LiveRawValue => LiveRawInput;

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

    public string FormulaExpression => Offset < 0 
        ? $"y = {Gain:0.#####}·x - {Math.Abs(Offset):0.#####}" 
        : $"y = {Gain:0.#####}·x + {Offset:0.#####}";

    public string LiveFormattedValue => $"{LiveScaledValue.ToString($"F{Math.Clamp(DecimalPlaces, 0, 4)}")} {Unit}";

    public string SimulationProgressText
    {
        get
        {
            if (!IsLiveActive && !IsSimActive && LiveRawInput == 0)
            {
                return FormulaExpression;
            }

            string unitStr = (InputTag == null || InputTag.Kind == TagKind.AnalogInput) ? "mV" : "";
            return string.IsNullOrEmpty(unitStr)
                ? $"IN: {LiveRawInput:N0}"
                : $"IN: {LiveRawInput:N0} {unitStr}";
        }
    }


    public string ScaleStatusPillText => $"{LiveFormattedValue} ({(int)LiveProgressPercent}%)";

    public string InputBadgeText => InputTag != null ? InputTag.Name : "—";

    public string OutputBadgeText => OutputTag != null ? OutputTag.Name : "—";

    public string InputConnectionSummary => InputTag != null
        ? string.Format(LocalizationService.Tr("InspectorScalePortInConnected"), InputTag.DisplayName)
        : LocalizationService.Tr("InspectorScalePortInNone");

    public string OutputConnectionSummary => OutputTag != null
        ? string.Format(LocalizationService.Tr("InspectorScalePortOutConnected"), OutputTag.DisplayName)
        : LocalizationService.Tr("InspectorScalePortOutNone");

    public string NarrativeText
    {
        get
        {
            string inStr = InputTag != null ? InputTag.DisplayName : LocalizationService.Tr("InspectorScalePortInNone");
            string outStr = OutputTag != null ? OutputTag.DisplayName : LocalizationService.Tr("InspectorScalePortOutNone");
            string clampStr = IsClamped ? $" [Min: {ClampMin}, Max: {ClampMax}]" : "";
            return string.Format(LocalizationService.Tr("InspectorScaleNarrative"), inStr, FormulaExpression, Unit, clampStr, outStr);
        }
    }

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"{Title} [{FormulaExpression}] ({Unit})"
        : $"{CustomLabel} [{FormulaExpression}] ({Unit})";

    public ScaleNodeViewModel(TagModel? inTag = null, TagModel? outTag = null)
    {
        Title = "SCALE";
        Icon = "📐";
        InputTag = inTag;
        OutputTag = outTag;

        InputConnectors.Add(new ConnectorViewModel
        {
            Title = "IN",
            IsInput = true,
            Node = this
        });

        OutputConnectors.Add(new ConnectorViewModel
        {
            Title = "OUT",
            IsInput = false,
            Node = this
        });

        // Initialize Calibration helper with default 0..10V to 0..100
        CalibX1 = 0;
        CalibY1 = 0;
        CalibX2 = 10000;
        CalibY2 = 100;
    }

    /// <summary>
    /// Tính toán giá trị scaled và progress % dựa trên raw input.
    /// </summary>
    public double Calculate(double rawInput)
    {
        LiveRawInput = (int)rawInput;

        double calc = Gain * rawInput + Offset;
        if (IsClamped)
        {
            double min = Math.Min(ClampMin, ClampMax);
            double max = Math.Max(ClampMin, ClampMax);
            calc = Math.Clamp(calc, min, max);
        }

        LiveScaledValue = calc;

        // Tính progress bar (0..100%)
        double span = Math.Abs(ClampMax - ClampMin);
        if (span > 1e-6)
        {
            double progress = (calc - Math.Min(ClampMin, ClampMax)) / span * 100.0;
            LiveProgressPercent = Math.Clamp(progress, 0.0, 100.0);
        }
        else
        {
            LiveProgressPercent = 0.0;
        }

        IsLiveActive = Math.Abs(calc) > 1e-6;
        LiveValueText = LiveFormattedValue;

        return calc;
    }

    /// <summary>
    /// Công cụ tính nhanh Gain & Offset từ 2 điểm hiệu chuẩn thực tế (X1, Y1) và (X2, Y2).
    /// </summary>
    [RelayCommand]
    public void CalculateFromCalibPoints()
    {
        double dx = CalibX2 - CalibX1;
        if (Math.Abs(dx) < 1e-6)
        {
            return; // Tránh chia cho 0
        }

        double newGain = (CalibY2 - CalibY1) / dx;
        double newOffset = CalibY1 - newGain * CalibX1;

        Gain = Math.Round(newGain, 6);
        Offset = Math.Round(newOffset, 6);

        ClampMin = Math.Min(CalibY1, CalibY2);
        ClampMax = Math.Max(CalibY1, CalibY2);
        IsClamped = true;

        // Recalculate with current input
        Calculate(LiveRawInput);
    }

    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        LiveQuality = snapshot.Quality;
        HasReceivedUpdate = true;
        Calculate(snapshot.RawValue);
    }

    public void SetOffline()
    {
        IsLiveOnline = false;
        LiveQuality = TagQuality.Unknown;
        IsLiveActive = false;
    }
}
