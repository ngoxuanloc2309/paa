using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Application.Enums;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Item ViewModel đại diện cho một chân toán hạng đầu vào trong hộp thoại cấu hình khối MATH.
/// </summary>
public partial class MathConfigPinItemViewModel : ObservableObject
{
    private readonly Action? _onChanged;

    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _name = "IN1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private bool _isConstant;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private TagModel? _tag;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    private double _constantValue;

    [ObservableProperty]
    private string _constantValueText = "0";

    [ObservableProperty]
    private double _simulationValue;

    [ObservableProperty]
    private bool _canDelete;

    [ObservableProperty]
    private bool _isWiredFromCanvas;

    public string DataTypeText
    {
        get
        {
            if (IsConstant)
            {
                return ConstantValueText.Contains('.') || ConstantValueText.Contains(',') ? "REAL" : "INT32";
            }
            return Tag?.DataType == TagDataType.Float32 || Tag?.Kind == TagKind.AnalogInput ? "REAL" : "INT32";
        }
    }

    public string DisplayText
    {
        get
        {
            if (IsConstant)
            {
                return NumericInputHelper.FormatDouble(ConstantValue);
            }
            return Tag != null ? Tag.Name : Name;
        }
    }

    public MathConfigPinItemViewModel(
        int index,
        string name,
        bool isConstant,
        double constantValue,
        TagModel? tag,
        bool isWiredFromCanvas,
        Action? onChanged)
    {
        _index = index;
        _name = name;
        _isConstant = isWiredFromCanvas ? false : isConstant;
        _constantValue = constantValue;
        _constantValueText = NumericInputHelper.FormatDouble(constantValue);
        _simulationValue = isConstant ? constantValue : (tag?.DataType == TagDataType.Float32 ? 25.0 : 10.0);
        _tag = tag;
        _isWiredFromCanvas = isWiredFromCanvas;
        _onChanged = onChanged;
    }

    partial void OnNameChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayText));
        _onChanged?.Invoke();
    }

    partial void OnIsConstantChanged(bool value)
    {
        if (value)
        {
            SimulationValue = ConstantValue;
        }
        else
        {
            SimulationValue = Tag?.DataType == TagDataType.Float32 ? 25.0 : 10.0;
        }
        OnPropertyChanged(nameof(DataTypeText));
        OnPropertyChanged(nameof(DisplayText));
        _onChanged?.Invoke();
    }

    partial void OnTagChanged(TagModel? value)
    {
        OnPropertyChanged(nameof(DataTypeText));
        OnPropertyChanged(nameof(DisplayText));
        _onChanged?.Invoke();
    }

    partial void OnConstantValueChanged(double value)
    {
        if (!NumericInputHelper.TryParseFlexibleDouble(ConstantValueText, out double cur) || Math.Abs(cur - value) > 1e-9)
        {
            ConstantValueText = NumericInputHelper.FormatDouble(value);
        }
        SimulationValue = value;
        OnPropertyChanged(nameof(DataTypeText));
        OnPropertyChanged(nameof(DisplayText));
        _onChanged?.Invoke();
    }

    partial void OnConstantValueTextChanged(string value)
    {
        if (NumericInputHelper.TryParseFlexibleDouble(value, out double parsed))
        {
            if (Math.Abs(ConstantValue - parsed) > 1e-9)
            {
                ConstantValue = parsed;
                SimulationValue = parsed;
            }
        }
        OnPropertyChanged(nameof(DataTypeText));
    }

    partial void OnSimulationValueChanged(double value)
    {
        _onChanged?.Invoke();
    }

    [RelayCommand]
    public void CommitConstantValue()
    {
        if (NumericInputHelper.TryParseFlexibleDouble(ConstantValueText, out double parsed))
        {
            ConstantValue = parsed;
            ConstantValueText = NumericInputHelper.FormatDouble(parsed);
            SimulationValue = parsed;
        }
        else
        {
            ConstantValueText = NumericInputHelper.FormatDouble(ConstantValue);
        }
        OnPropertyChanged(nameof(DataTypeText));
        OnPropertyChanged(nameof(DisplayText));
        _onChanged?.Invoke();
    }
}

/// <summary>
/// ViewModel quản lý toàn bộ trạng thái hộp thoại cấu hình khối tính toán số học Math ALU.
/// Hỗ trợ chuẩn hóa toán tử IEC 61131-3, chọn toán hạng ANY_NUM, và mô phỏng tức thời.
/// </summary>
public partial class MathConfigDialogViewModel : ObservableObject
{
    /// <summary>
    /// Tham chiếu tới Node đồ họa gốc trên canvas Logic Editor.
    /// </summary>
    public MathNodeViewModel Node { get; }

    /// <summary>
    /// Catalog biến Tag của dự án PLC.
    /// </summary>
    public TagCatalogViewModel? TagCatalog { get; }

    /// <summary>
    /// Danh sách toàn bộ toán hạng Tag kiểu số ANY_NUM (AI, VREG, COUNTER).
    /// Tuyệt đối loại trừ BOOL (DI, DO, VFLAG).
    /// </summary>
    public ObservableCollection<TagModel> AvailableNumericTags { get; } = new();

    /// <summary>
    /// Danh sách Tag đích nhận kết quả (RegisterTags + option None).
    /// </summary>
    public ObservableCollection<TagModel?> AvailableOutputTags { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDivOrMod))]
    [NotifyPropertyChangedFor(nameof(IsSubOrDivOrMod))]
    [NotifyPropertyChangedFor(nameof(CanAddPin))]
    [NotifyPropertyChangedFor(nameof(OperatorSymbol))]
    [NotifyPropertyChangedFor(nameof(CurrentOpBadge))]
    private MathOperator _operator;

    partial void OnOperatorChanged(MathOperator value)
    {
        if (IsDivOrMod && Pins.Count > 2)
        {
            while (Pins.Count > 2)
            {
                Pins.RemoveAt(Pins.Count - 1);
            }
            RefreshPinIndices();
        }
        UpdateSimulation();
    }

    [ObservableProperty]
    private MathTypeMode _typeMode;

    [ObservableProperty]
    private TagModel? _outputTag;

    [ObservableProperty]
    private bool _isOutputWiredFromCanvas;

    partial void OnOutputTagChanged(TagModel? value)
    {
        UpdateSimulation();
    }

    public ObservableCollection<MathConfigPinItemViewModel> Pins { get; } = new();

    /// <summary>
    /// Cho biết toán tử hiện tại có phải là DIV hoặc MOD (giới hạn cứng 2 chân theo chuẩn IEC 61131-3).
    /// </summary>
    public bool IsDivOrMod => Operator is MathOperator.Div or MathOperator.Mod;

    /// <summary>
    /// Cho biết toán tử hiện tại có phải là phép toán giới hạn 2 chân (giữ tương thích).
    /// </summary>
    public bool IsSubOrDivOrMod => IsDivOrMod;

    public bool CanAddPin => Pins.Count < 8 && !IsDivOrMod;

    public string OperatorSymbol => Operator switch
    {
        MathOperator.Add => "+",
        MathOperator.Sub => "−",
        MathOperator.Mul => "×",
        MathOperator.Div => "÷",
        MathOperator.Mod => "%",
        _ => "+"
    };

    public string CurrentOpBadge => $"{Operator} (ALU)";

    [ObservableProperty]
    private string _formulaExpression = string.Empty;

    [ObservableProperty]
    private double _simulationResult;

    [ObservableProperty]
    private string _simulationResultText = "0";

    [ObservableProperty]
    private bool _hasMathError;

    [ObservableProperty]
    private string _statusBadgeText = string.Empty;

    [ObservableProperty]
    private bool _isStatusValid = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InferredTypeDisplayText))]
    private string _inferredType = "INT32";

    /// <summary>
    /// Chuỗi hiển thị kiểu dữ liệu suy diễn theo ngôn ngữ hiện hành.
    /// </summary>
    public string InferredTypeDisplayText => string.Format(LocalizationService.Tr("MathConfigInferredTypePrefix"), InferredType);

    public string PinCountSummary => $"({Pins.Count} / 8)";

    public MathConfigDialogViewModel(MathNodeViewModel node, TagCatalogViewModel? tagCatalog)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        TagCatalog = tagCatalog;

        _operator = node.Operator;
        _typeMode = node.TypeMode;
        _outputTag = node.OutputTag;
        _isOutputWiredFromCanvas = node.OutputConnectors.FirstOrDefault()?.IsConnected == true;

        // Xây dựng danh sách Tag hợp lệ
        BuildAvailableTags();

        // Nạp các chân hiện tại từ Node
        foreach (var pin in node.Pins)
        {
            bool isWired = pin.Connector?.IsConnected == true;
            var item = new MathConfigPinItemViewModel(
                index: Pins.Count + 1,
                name: pin.Label,
                isConstant: isWired ? false : pin.IsConstant,
                constantValue: pin.ConstantValue,
                tag: pin.Tag,
                isWiredFromCanvas: isWired,
                onChanged: UpdateSimulation);

            Pins.Add(item);
        }

        // Đảm bảo tối thiểu 2 chân
        while (Pins.Count < 2)
        {
            int idx = Pins.Count + 1;
            var defaultTag = AvailableNumericTags.FirstOrDefault();
            var item = new MathConfigPinItemViewModel(
                index: idx,
                name: $"IN{idx}",
                isConstant: false,
                constantValue: 0.0,
                tag: defaultTag,
                isWiredFromCanvas: false,
                onChanged: UpdateSimulation);
            Pins.Add(item);
        }

        RefreshPinIndices();
        StatusBadgeText = LocalizationService.Tr("MathConfigStatusValid");
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
        UpdateSimulation();
    }

    private void OnLanguageChanged()
    {
        OnPropertyChanged(nameof(StatusBadgeText));
        OnPropertyChanged(nameof(InferredTypeDisplayText));
        UpdateSimulation();
    }

    /// <summary>
    /// Hủy đăng ký sự kiện và giải phóng tài nguyên khi hộp thoại đóng.
    /// </summary>
    public void Cleanup()
    {
        LocalizationService.Instance.LanguageChanged -= OnLanguageChanged;
    }

    private void BuildAvailableTags()
    {
        AvailableNumericTags.Clear();
        AvailableOutputTags.Clear();

        // Option None cho ngõ ra (để nối dây FBD trên Canvas)
        AvailableOutputTags.Add(null);

        if (TagCatalog != null)
        {
            // Tag số ANY_NUM: AI, VREG, COUNTER
            foreach (var t in TagCatalog.NumericTags)
            {
                AvailableNumericTags.Add(t);
            }

            // Tag ghi ngõ ra OUT: VREG, VREG_RETAIN, COUNTER
            foreach (var t in TagCatalog.RegisterTags)
            {
                AvailableOutputTags.Add(t);
            }
        }
    }

    private void RefreshPinIndices()
    {
        for (int i = 0; i < Pins.Count; i++)
        {
            Pins[i].Index = i + 1;
            if (string.IsNullOrWhiteSpace(Pins[i].Name))
            {
                Pins[i].Name = $"IN{i + 1}";
            }
            Pins[i].CanDelete = Pins.Count > 2 && i >= 2 && !Pins[i].IsWiredFromCanvas;
        }

        OnPropertyChanged(nameof(CanAddPin));
        OnPropertyChanged(nameof(PinCountSummary));
    }

    private void UpdateAvailableOutputTags(bool isReal)
    {
        var currentSelected = OutputTag;
        AvailableOutputTags.Clear();
        // Option None cho ngõ ra (để nối dây FBD trên Canvas)
        AvailableOutputTags.Add(null);

        if (TagCatalog != null)
        {
            var targetTags = TagCatalog.RegisterTags.Where(t => isReal ? t.DataType == TagDataType.Float32 : t.DataType != TagDataType.Float32);
            foreach (var t in targetTags)
            {
                AvailableOutputTags.Add(t);
            }
        }

        if (IsOutputWiredFromCanvas && currentSelected != null)
        {
            if (!AvailableOutputTags.Contains(currentSelected))
            {
                AvailableOutputTags.Add(currentSelected);
            }
            OutputTag = currentSelected;
        }
        else if (currentSelected != null && AvailableOutputTags.Contains(currentSelected))
        {
            OutputTag = currentSelected;
        }
        else if (currentSelected != null && !IsOutputWiredFromCanvas)
        {
            OutputTag = AvailableOutputTags.FirstOrDefault(t => t != null);
        }
    }

    [RelayCommand]
    public void AddPin()
    {
        if (!CanAddPin) return;

        int newIdx = Pins.Count + 1;
        var defaultTag = AvailableNumericTags.Skip(Pins.Count).FirstOrDefault() ?? AvailableNumericTags.FirstOrDefault();

        var item = new MathConfigPinItemViewModel(
            index: newIdx,
            name: $"IN{newIdx}",
            isConstant: false,
            constantValue: 0.0,
            tag: defaultTag,
            isWiredFromCanvas: false,
            onChanged: UpdateSimulation);

        Pins.Add(item);
        RefreshPinIndices();
        UpdateSimulation();
    }

    [RelayCommand]
    public void RemovePin(MathConfigPinItemViewModel? item)
    {
        if (item == null || item.IsWiredFromCanvas || Pins.Count <= 2) return;

        Pins.Remove(item);
        RefreshPinIndices();
        UpdateSimulation();
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        Operator = MathOperator.Add;
        TypeMode = MathTypeMode.Auto;
        OutputTag = AvailableOutputTags.FirstOrDefault(t => t != null && t.Name == "VREG0") ?? AvailableOutputTags.FirstOrDefault(t => t != null);

        Pins.Clear();
        var tag1 = AvailableNumericTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput) ?? AvailableNumericTags.FirstOrDefault();
        var tag2 = AvailableNumericTags.Skip(1).FirstOrDefault() ?? AvailableNumericTags.FirstOrDefault();

        Pins.Add(new MathConfigPinItemViewModel(1, "IN1", false, 0.0, tag1, false, UpdateSimulation));
        Pins.Add(new MathConfigPinItemViewModel(2, "IN2", true, 10.0, tag2, false, UpdateSimulation));

        RefreshPinIndices();
        UpdateSimulation();
    }

    private bool _isUpdatingSimulation;

    public void UpdateSimulation()
    {
        if (_isUpdatingSimulation) return;
        _isUpdatingSimulation = true;
        try
        {
            string outPart = OutputTag != null ? OutputTag.Name : "OUT";
            if (Pins.Count == 0)
            {
                FormulaExpression = $"{outPart} = 0";
                SimulationResult = 0;
                SimulationResultText = "0";
                HasMathError = false;
                IsStatusValid = true;
                StatusBadgeText = LocalizationService.Tr("MathConfigStatusValid");
                return;
            }

            string sep = $" {OperatorSymbol} ";
            var pinLabels = Pins.Select(p => p.DisplayText);
            FormulaExpression = $"{outPart} = {string.Join(sep, pinLabels)}";

            // Tính toán mô phỏng ALU
            double result = Pins[0].SimulationValue;
            bool hasDivZero = false;

            for (int i = 1; i < Pins.Count; i++)
            {
                double val = Pins[i].SimulationValue;
                switch (Operator)
                {
                    case MathOperator.Add:
                        result += val;
                        break;
                    case MathOperator.Sub:
                        result -= val;
                        break;
                    case MathOperator.Mul:
                        result *= val;
                        break;
                    case MathOperator.Div:
                        if (Math.Abs(val) < 1e-9)
                            hasDivZero = true;
                        else
                            result /= val;
                        break;
                    case MathOperator.Mod:
                        if (Math.Abs(val) < 1e-9)
                            hasDivZero = true;
                        else
                            result %= val;
                        break;
                }
            }

            bool hasReal = (TypeMode == MathTypeMode.ForceReal) || (TypeMode != MathTypeMode.ForceInt32 && Pins.Any(p => p.DataTypeText == "REAL"));
            InferredType = hasReal ? "REAL" : "INT32";
            UpdateAvailableOutputTags(hasReal);

            if (hasDivZero || double.IsNaN(result) || double.IsInfinity(result))
            {
                HasMathError = true;
                IsStatusValid = false;
                SimulationResult = 0;
                SimulationResultText = "NaN / FAULT";
                StatusBadgeText = LocalizationService.Tr("MathConfigStatusDivZero");
            }
            else
            {
                HasMathError = false;
                IsStatusValid = true;
                SimulationResult = result;
                SimulationResultText = Math.Abs(result % 1) < 1e-6
                    ? ((long)Math.Round(result)).ToString(CultureInfo.InvariantCulture)
                    : result.ToString("F2", CultureInfo.InvariantCulture);
                StatusBadgeText = LocalizationService.Tr("MathConfigStatusValid");
            }

            OnPropertyChanged(nameof(FormulaExpression));
            OnPropertyChanged(nameof(SimulationResultText));
            OnPropertyChanged(nameof(PinCountSummary));
        }
        finally
        {
            _isUpdatingSimulation = false;
        }
    }

    /// <summary>
    /// Áp dụng các thay đổi từ dialog vào Node ViewModel chính trên canvas.
    /// </summary>
    public void Apply()
    {
        Node.Operator = Operator;
        Node.TypeMode = TypeMode;
        Node.OutputTag = OutputTag;

        // Đồng bộ số lượng chân và giá trị
        while (Node.Pins.Count > Pins.Count)
        {
            Node.RemovePinCommand.Execute(Node.Pins.Last());
        }

        while (Node.Pins.Count < Pins.Count)
        {
            Node.AddPinCommand.Execute(null);
        }

        for (int i = 0; i < Pins.Count; i++)
        {
            var src = Pins[i];
            var dest = Node.Pins[i];

            dest.Label = src.Name;
            dest.IsConstant = src.IsConstant;
            dest.Tag = src.Tag;
            dest.ConstantValue = src.ConstantValue;
            dest.ConstantValueText = src.ConstantValueText;
        }

        Node.NotifyBindingChanged();
    }
}
