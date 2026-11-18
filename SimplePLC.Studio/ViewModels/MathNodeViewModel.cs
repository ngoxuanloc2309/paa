using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa Tính toán Toán học Đa năng MATH (IEC 61131-3 Math Block).
/// Hỗ trợ 8 toán tử số học: ADD, SUB, MUL, DIV, MOD, MIN, MAX, CLAMP.
/// Hỗ trợ đa chân đầu vào động (2 đến 8 chân toán hạng) cho các phép toán giao hoán/kết hợp.
/// Thực thi chuyển đổi kiểu dữ liệu tự động (Type Promotion) và bảo vệ an toàn công nghiệp (chia cho 0, NaN/Inf).
/// </summary>
public partial class MathNodeViewModel : GraphNodeViewModel, ILiveTagBoundNode
{
    /// <summary>
    /// Index của Tag giám sát trực tiếp trên thanh trạng thái (Ưu tiên OutputTag, nếu null lấy Tag của Pin đầu tiên).
    /// </summary>
    public ushort? BoundTagIndex
    {
        get
        {
            if (OutputTag != null && OutputTag.Kind != TagKind.None && OutputTag.Index >= 0 && OutputTag.Index < 65535)
            {
                return (ushort)OutputTag.Index;
            }
            var firstTag = Pins.FirstOrDefault(p => !p.IsConstant && p.Tag != null && p.Tag.Index >= 0 && p.Tag.Index < 65535)?.Tag;
            return firstTag != null ? (ushort)firstTag.Index : null;
        }
    }

    /// <summary>
    /// Toán tử tính toán được chọn (ADD, SUB, MUL, DIV, MOD, MIN, MAX, CLAMP).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(OperatorSymbol))]
    [NotifyPropertyChangedFor(nameof(IsDivOrMod))]
    [NotifyPropertyChangedFor(nameof(IsSubOrDivOrMod))]
    private MathOperator _operator = MathOperator.Add;

    partial void OnOperatorChanged(MathOperator value)
    {
        // Với DIV, MOD: chuẩn IEC chỉ hỗ trợ đúng 2 chân đầu vào
        if (value is MathOperator.Div or MathOperator.Mod)
        {
            while (Pins.Count > 2)
            {
                RemovePinInternal(Pins.Last());
            }
        }

        for (int i = 0; i < Pins.Count; i++)
        {
            Pins[i].Label = $"IN{i + 1}";
        }

        UpdatePinLabelsAndConnectors();
        OnPropertyChanged(nameof(CanAddPin));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(FormulaExpression));
    }

    /// <summary>
    /// Chế độ ép kiểu dữ liệu kết quả: Auto (Tự suy diễn), ForceInt32, ForceReal.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    private MathTypeMode _typeMode = MathTypeMode.Auto;

    /// <summary>
    /// Tag đích ghi kết quả tính toán.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(FormulaExpression))]
    [NotifyPropertyChangedFor(nameof(BoundTagIndex))]
    [NotifyPropertyChangedFor(nameof(OutputBadgeText))]
    [NotifyPropertyChangedFor(nameof(HasOutputConnection))]
    private TagModel? _outputTag;

    partial void OnOutputTagChanged(TagModel? oldValue, TagModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.PropertyChanged -= OnOutputTagPropertyChanged;
        }
        if (newValue != null)
        {
            newValue.PropertyChanged += OnOutputTagPropertyChanged;
        }
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(FormulaExpression));
        OnPropertyChanged(nameof(OutputBadgeText));
        OnPropertyChanged(nameof(OutputTagDisplayText));
        OnPropertyChanged(nameof(HasOutputConnection));
        OnPropertyChanged(nameof(BoundTagIndex));
    }

    private void OnOutputTagPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TagModel.Name) || e.PropertyName == nameof(TagModel.DisplayName) || e.PropertyName == nameof(TagModel.Alias))
        {
            OnPropertyChanged(nameof(SummaryText));
            OnPropertyChanged(nameof(FormulaExpression));
            OnPropertyChanged(nameof(OutputBadgeText));
            OnPropertyChanged(nameof(OutputTagDisplayText));
        }
    }

    /// <summary>
    /// Chuỗi hiển thị tên Tag ngõ ra (OUT) hoặc văn bản chưa gán theo ngôn ngữ hiện hành.
    /// </summary>
    public string OutputTagDisplayText => OutputTag != null ? OutputTag.Name : LocalizationService.Tr("CommonUnassigned");

    /// <summary>
    /// Huy hiệu hiển thị trạng thái gán ngõ ra trên khối đồ họa logic.
    /// </summary>
    public string OutputBadgeText => $"OUT: {OutputTagDisplayText}";

    /// <summary>
    /// Chuỗi tóm tắt số lượng chân toán hạng theo ngôn ngữ hiện hành (ví dụ: "2 pins" hoặc "2 chân").
    /// </summary>
    public string PinsCountSummary => string.Format(LocalizationService.Tr("CommonPinsCountFormat"), Pins.Count);

    public bool HasOutputConnection => OutputTag != null;

    /// <summary>
    /// Danh sách các chân đầu vào (2 đến 8 chân).
    /// </summary>
    public ObservableCollection<MathPinBindingViewModel> Pins { get; } = new();

    /// <summary>
    /// Cho biết toán tử hiện tại có phải là DIV hoặc MOD (giới hạn cứng 2 chân theo chuẩn IEC 61131-3).
    /// </summary>
    public bool IsDivOrMod => Operator is MathOperator.Div or MathOperator.Mod;

    /// <summary>
    /// Cho biết toán tử hiện tại có phải là phép toán giới hạn 2 chân (giữ tương thích).
    /// </summary>
    public bool IsSubOrDivOrMod => IsDivOrMod;

    /// <summary>
    /// Điều kiện cho phép thêm chân mới (tối đa 8 chân, hỗ trợ cho ADD, SUB, MUL).
    /// </summary>
    public bool CanAddPin => Pins.Count < 8 && !IsDivOrMod;

    /// <summary>
    /// Ký hiệu toán học hiển thị trực quan trên khối (badge).
    /// </summary>
    public string OperatorSymbol => Operator switch
    {
        MathOperator.Add => "+",
        MathOperator.Sub => "−",
        MathOperator.Mul => "×",
        MathOperator.Div => "÷",
        MathOperator.Mod => "%",
        _ => "?"
    };

    /// <summary>
    /// Biểu thức công thức toán học hoàn chỉnh hiển thị trên giao diện (Formula Expression).
    /// </summary>
    public string FormulaExpression
    {
        get
        {
            string outPart = OutputTag != null ? OutputTag.Name : "OUT";
            if (Pins.Count == 0) return $"{outPart} = 0";

            string body;
            switch (Operator)
            {
                case MathOperator.Add:
                    body = string.Join(" + ", Pins.Select(p => p.DisplayText));
                    break;
                case MathOperator.Sub:
                    body = string.Join(" - ", Pins.Select(p => p.DisplayText));
                    break;
                case MathOperator.Mul:
                    body = string.Join(" * ", Pins.Select(p => p.DisplayText));
                    break;
                case MathOperator.Div:
                    body = Pins.Count >= 2 ? $"{Pins[0].DisplayText} / {Pins[1].DisplayText}" : Pins[0].DisplayText;
                    break;
                case MathOperator.Mod:
                    body = Pins.Count >= 2 ? $"{Pins[0].DisplayText} % {Pins[1].DisplayText}" : Pins[0].DisplayText;
                    break;
                default:
                    body = "0";
                    break;
            }

            return $"{outPart} = {body}";
        }
    }

    /// <summary>
    /// Tóm tắt cấu hình cho Inspector và Tooltip.
    /// </summary>
    public override string SummaryText => $"{Operator} ({Pins.Count} inputs) → {OutputTag?.Name ?? "None"} [{TypeMode}]";

    // Giá trị mô phỏng / Giám sát trực tiếp
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LiveResultText))]
    private double _liveResult;

    [ObservableProperty]
    private bool _isLiveActive;

    [ObservableProperty]
    private bool _hasMathError;

    [ObservableProperty]
    private string _mathErrorMessage = string.Empty;

    public string LiveResultText => HasMathError
        ? $"ERR: {MathErrorMessage}"
        : NumericInputHelper.FormatDouble(LiveResult);

    /// <summary>
    /// Khởi tạo một khối toán học MathNodeViewModel.
    /// </summary>
    public MathNodeViewModel(TagModel? outTag = null)
    {
        Title = "MATH";
        Icon = "Math";
        OutputTag = outTag;

        // Khởi tạo 2 chân mặc định IN1, IN2
        AddPinInternal("IN1");
        AddPinInternal("IN2");

        // Khởi tạo connector đầu ra OUT
        var outConn = new ConnectorViewModel
        {
            Title = "Out",
            TooltipKey = "PinTooltipOut",
            IsInput = false,
            IsEvent = false,
            Node = this
        };
        outConn.SetBilingualDescription(
            "Chân kết quả xuất giá trị tính toán toán học",
            "Calculated result output pin");
        OutputConnectors.Add(outConn);

        Pins.CollectionChanged += OnPinsCollectionChanged;
    }

    private void OnPinsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanAddPin));
        OnPropertyChanged(nameof(FormulaExpression));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(PinsCountSummary));
        OnPropertyChanged(nameof(BoundTagIndex));
    }

    /// <inheritdoc/>
    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        OnPropertyChanged(nameof(OutputBadgeText));
        OnPropertyChanged(nameof(OutputTagDisplayText));
        OnPropertyChanged(nameof(PinsCountSummary));
        OnPropertyChanged(nameof(SummaryText));
    }

    private void AddPinInternal(string label)
    {
        var pin = new MathPinBindingViewModel(
            label: label,
            isConstant: false,
            constantValue: 0.0,
            tag: null,
            onChanged: () =>
            {
                OnPropertyChanged(nameof(FormulaExpression));
                OnPropertyChanged(nameof(SummaryText));
                OnPropertyChanged(nameof(BoundTagIndex));
            });

        var conn = new ConnectorViewModel
        {
            Title = label,
            TooltipKey = "PinTooltipIn",
            IsInput = true,
            IsEvent = false,
            Node = this
        };
        conn.SetBilingualDescription(
            $"Chân đầu vào toán hạng {label} của khối MATH",
            $"Operand input pin {label} of MATH function block");

        pin.Connector = conn;
        Pins.Add(pin);
        InputConnectors.Add(conn);
    }

    /// <summary>
    /// Thêm một chân toán hạng mới vào khối (tối đa 8 chân).
    /// </summary>
    [RelayCommand]
    public void AddPin()
    {
        if (Pins.Count >= 8) return;
        if (IsSubOrDivOrMod) return;

        int nextIndex = Pins.Count + 1;
        AddPinInternal($"IN{nextIndex}");
        UpdatePinLabelsAndConnectors();
    }

    /// <summary>
    /// Xóa một chân toán hạng khỏi khối (tối thiểu 2 chân).
    /// </summary>
    /// <param name="pin">Chân cần xóa.</param>
    [RelayCommand]
    public void RemovePin(MathPinBindingViewModel? pin)
    {
        if (pin == null) return;
        if (Pins.Count <= 2) return;

        RemovePinInternal(pin);
        UpdatePinLabelsAndConnectors();
    }

    private void RemovePinInternal(MathPinBindingViewModel pin)
    {
        int index = Pins.IndexOf(pin);
        if (index >= 0)
        {
            if (pin.Connector != null && InputConnectors.Contains(pin.Connector))
            {
                InputConnectors.Remove(pin.Connector);
            }
            Pins.RemoveAt(index);
        }
    }

    private void UpdatePinLabelsAndConnectors()
    {
        for (int i = 0; i < Pins.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(Pins[i].Label))
            {
                Pins[i].Label = $"IN{i + 1}";
            }
        }
    }

    /// <summary>
    /// Thực hiện tính toán thử nghiệm cục bộ với mảng giá trị đầu vào phục vụ mô phỏng Canvas.
    /// </summary>
    /// <param name="inputs">Mảng giá trị số thực từ các chân.</param>
    /// <returns>Kết quả tính toán.</returns>
    public double Calculate(double[] inputs)
    {
        if (inputs.Length == 0)
        {
            LiveResult = 0;
            HasMathError = false;
            MathErrorMessage = string.Empty;
            return 0;
        }

        HasMathError = false;
        MathErrorMessage = string.Empty;
        double result = inputs[0];

        try
        {
            switch (Operator)
            {
                case MathOperator.Add:
                    for (int i = 1; i < inputs.Length; i++) result += inputs[i];
                    break;

                case MathOperator.Sub:
                    for (int i = 1; i < inputs.Length; i++) result -= inputs[i];
                    break;

                case MathOperator.Mul:
                    for (int i = 1; i < inputs.Length; i++) result *= inputs[i];
                    break;

                case MathOperator.Div:
                    for (int i = 1; i < inputs.Length; i++)
                    {
                        if (Math.Abs(inputs[i]) < 1e-9)
                        {
                            HasMathError = true;
                            MathErrorMessage = "Div by Zero";
                            result = 0.0;
                            break;
                        }
                        result /= inputs[i];
                    }
                    break;

                case MathOperator.Mod:
                    for (int i = 1; i < inputs.Length; i++)
                    {
                        if (Math.Abs(inputs[i]) < 1e-9)
                        {
                            HasMathError = true;
                            MathErrorMessage = "Mod by Zero";
                            result = 0.0;
                            break;
                        }
                        result %= inputs[i];
                    }
                    break;


            }

            if (double.IsNaN(result) || double.IsInfinity(result))
            {
                HasMathError = true;
                MathErrorMessage = "NaN/Inf Overflow";
                result = 0.0;
            }
        }
        catch (Exception ex)
        {
            HasMathError = true;
            MathErrorMessage = ex.Message;
            result = 0.0;
        }

        LiveResult = result;
        return result;
    }

    /// <summary>
    /// Giá trị thô nhận được từ MCU Modbus RTU.
    /// </summary>
    [ObservableProperty]
    private int _liveRawValue;

    /// <summary>
    /// Chất lượng dữ liệu runtime (Good, Stale, Unknown).
    /// </summary>
    [ObservableProperty]
    private TagQuality _liveQuality = TagQuality.Unknown;

    /// <summary>
    /// Trạng thái kết nối trực tuyến tới MCU.
    /// </summary>
    [ObservableProperty]
    private bool _isLiveOnline;

    /// <summary>
    /// Đã từng nhận được ít nhất 1 snapshot từ thiết bị (dùng cho Last Known).
    /// </summary>
    [ObservableProperty]
    private bool _hasReceivedUpdate;

    /// <summary>
    /// Chuỗi văn bản hiển thị giá trị thời gian thực.
    /// </summary>
    public string LiveValueText => HasMathError ? MathErrorMessage : LiveResultText;

    /// <summary>
    /// Trạng thái hiển thị trực quan cho Converters.
    /// </summary>
    public NodeLiveVisualState LiveVisualState
    {
        get
        {
            if (!IsLiveOnline) return NodeLiveVisualState.Offline;
            if (LiveQuality == TagQuality.Stale) return NodeLiveVisualState.Stale;
            if (HasMathError) return NodeLiveVisualState.Inactive;
            return NodeLiveVisualState.Active;
        }
    }

    /// <summary>
    /// Thông báo cập nhật ràng buộc khi Pin binding thay đổi từ bên ngoài.
    /// </summary>
    public void NotifyBindingChanged()
    {
        OnPropertyChanged(nameof(FormulaExpression));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(BoundTagIndex));
    }

    /// <summary>
    /// Đặt trạng thái ngắt kết nối (bảo toàn Last Known RawValue).
    /// </summary>
    public void SetOffline()
    {
        IsLiveOnline = false;
        LiveQuality = TagQuality.Unknown;
        IsLiveActive = false;
        HasMathError = false;
        MathErrorMessage = string.Empty;
    }

    /// <summary>
    /// Nhận và áp dụng giá trị Tag Snapshot thực tế từ PLC/McuEmulator.
    /// </summary>
    public void ApplyRuntimeSnapshot(RuntimeTagSnapshot snapshot, bool isOnline)
    {
        IsLiveOnline = isOnline;
        IsLiveActive = isOnline;
        LiveQuality = snapshot.Quality;
        HasReceivedUpdate = true;
        LiveRawValue = snapshot.RawValue;

        if (!isOnline)
        {
            HasMathError = false;
            MathErrorMessage = string.Empty;
            return;
        }

        if (OutputTag != null && OutputTag.Index == snapshot.TagIndex)
        {
            LiveResult = OutputTag.DataType == TagDataType.Float32 
                ? BitConverter.Int32BitsToSingle(snapshot.RawValue) 
                : snapshot.RawValue;
        }
    }
}
