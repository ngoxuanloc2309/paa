using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using System;
using System.Globalization;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho một chân đầu vào (Input Pin) của khối tính toán MATH.
/// Đại diện cho một toán hạng: có thể là Biến Tag động hoặc Hằng số scalar cố định.
/// Thực thi máy trạng thái Drafting/Committed: tự động normalize/revert và chống nhập sai kiểu dữ liệu.
/// </summary>
public partial class MathPinBindingViewModel : ObservableObject
{
    private readonly Action? _onChangedCallback;

    /// <summary>
    /// Mã định danh duy nhất của chân pin.
    /// </summary>
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// Nhãn hiển thị của chân pin (ví dụ: IN1, IN2, IN3...).
    /// </summary>
    [ObservableProperty]
    private string _label = "IN1";

    /// <summary>
    /// Xác định chân pin đang sử dụng giá trị Hằng số (true) hay đọc từ Biến Tag (false).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private bool _isConstant;

    /// <summary>
    /// Giá trị số thực tế của hằng số khi ở chế độ IsConstant = true.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private double _constantValue;

    /// <summary>
    /// Chuỗi nhập liệu văn bản của hằng số (Drafting State).
    /// </summary>
    [ObservableProperty]
    private string _constantValueText = "0";

    /// <summary>
    /// Biến Tag được gán cho chân pin khi ở chế độ đọc Tag (IsConstant = false).
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    private TagModel? _tag;

    /// <summary>
    /// Connector đồ họa tương ứng trên khối Canvas (nếu có).
    /// </summary>
    public ConnectorViewModel? Connector { get; set; }

    /// <summary>
    /// Chuỗi hiển thị kiểu dữ liệu của biến Tag hoặc "Const".
    /// </summary>
    public string DataTypeText => IsConstant ? "Const" : (Tag?.DataTypeText ?? "-");

    /// <summary>
    /// Văn bản tóm tắt biểu diễn giá trị của Pin (Tên Tag hoặc số hằng).
    /// </summary>
    public string DisplayText
    {
        get
        {
            if (IsConstant)
            {
                return NumericInputHelper.FormatDouble(ConstantValue);
            }
            return Tag != null ? Tag.Name : Label;
        }
    }

    /// <summary>
    /// Khởi tạo một chân toán hạng MathPinBindingViewModel.
    /// </summary>
    /// <param name="label">Nhãn chân (IN1, IN2...).</param>
    /// <param name="isConstant">Chế độ hằng số.</param>
    /// <param name="constantValue">Giá trị hằng số khởi tạo.</param>
    /// <param name="tag">Biến Tag khởi tạo.</param>
    /// <param name="onChanged">Hàm callback thông báo thay đổi cho ViewModel cha.</param>
    public MathPinBindingViewModel(
        string label = "IN1",
        bool isConstant = false,
        double constantValue = 0.0,
        TagModel? tag = null,
        Action? onChanged = null)
    {
        _label = label;
        _isConstant = isConstant;
        _constantValue = constantValue;
        _constantValueText = NumericInputHelper.FormatDouble(constantValue);
        _tag = tag;
        _onChangedCallback = onChanged;
    }

    partial void OnLabelChanged(string value)
    {
        if (Connector != null)
        {
            Connector.Title = value;
            Connector.SetBilingualDescription(
                $"Chân đầu vào toán hạng {value} của khối toán học MATH",
                $"Operand input pin {value} of MATH function block");
        }
        OnPropertyChanged(nameof(DisplayText));
        _onChangedCallback?.Invoke();
    }

    partial void OnIsConstantChanged(bool value)
    {
        OnPropertyChanged(nameof(DataTypeText));
        OnPropertyChanged(nameof(DisplayText));
        _onChangedCallback?.Invoke();
    }

    partial void OnConstantValueChanged(double value)
    {
        if (!NumericInputHelper.TryParseFlexibleDouble(ConstantValueText, out double cur) || Math.Abs(cur - value) > 1e-9)
        {
            ConstantValueText = NumericInputHelper.FormatDouble(value);
        }
        OnPropertyChanged(nameof(DisplayText));
        _onChangedCallback?.Invoke();
    }

    partial void OnConstantValueTextChanged(string value)
    {
        if (NumericInputHelper.TryParseFlexibleDouble(value, out double parsed))
        {
            if (Math.Abs(ConstantValue - parsed) > 1e-9)
            {
                ConstantValue = parsed;
            }
        }
    }

    partial void OnTagChanged(TagModel? value)
    {
        OnPropertyChanged(nameof(DataTypeText));
        OnPropertyChanged(nameof(DisplayText));
        _onChangedCallback?.Invoke();
    }

    /// <summary>
    /// Xác nhận hoàn tất nhập liệu (Committed State) khi LostFocus hoặc nhấn Enter.
    /// Tự động Normalize định dạng nếu hợp lệ, hoặc Revert về giá trị hợp lệ cuối nếu lỗi/chuỗi rác.
    /// </summary>
    [RelayCommand]
    public void CommitConstantValue()
    {
        if (NumericInputHelper.TryParseFlexibleDouble(ConstantValueText, out double parsed))
        {
            ConstantValue = parsed;
            ConstantValueText = NumericInputHelper.FormatDouble(parsed);
        }
        else
        {
            // Auto-Revert về giá trị hợp lệ đã lưu
            ConstantValueText = NumericInputHelper.FormatDouble(ConstantValue);
        }
        OnPropertyChanged(nameof(DisplayText));
        _onChangedCallback?.Invoke();
    }
}
