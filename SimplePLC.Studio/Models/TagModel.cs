using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Domain.Enums;

namespace SimplePLC.Studio.Models;

public partial class TagModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDigital))]
    [NotifyPropertyChangedFor(nameof(IsOutput))]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    [NotifyPropertyChangedFor(nameof(IsDataTypeEditable))]
    [NotifyPropertyChangedFor(nameof(ModbusAddressText))]
    private TagKind _kind;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _alias = string.Empty;

    [ObservableProperty]
    private int _channel;

    [ObservableProperty]
    private string _group = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private int _value;

    partial void OnValueChanged(int value)
    {
        if (IsDigital || DataType == TagDataType.Boolean)
        {
            if (value != 0 && value != 1)
            {
                Value = value != 0 ? 1 : 0;
                return;
            }
        }
        OnPropertyChanged(nameof(FormattedValue));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private TagDataType _dataType = TagDataType.Int32;

    partial void OnDataTypeChanged(TagDataType value)
    {
        if (value == TagDataType.Boolean)
        {
            if (Value != 0 && Value != 1)
            {
                Value = Value != 0 ? 1 : 0;
            }
        }
        OnPropertyChanged(nameof(FormattedValue));
        OnPropertyChanged(nameof(DataTypeText));
    }

    partial void OnKindChanged(TagKind value)
    {
        if (IsDigital)
        {
            DataType = TagDataType.Boolean;
            if (Value != 0 && Value != 1)
            {
                Value = Value != 0 ? 1 : 0;
            }
        }
        else if (DataType == TagDataType.Boolean)
        {
            DataType = TagDataType.Int32;
        }
    }

    public string FormattedValue
    {
        get
        {
            if (IsDigital || DataType == TagDataType.Boolean)
            {
                return Value != 0 ? "1" : "0";
            }
            if (DataType == TagDataType.Float32)
            {
                return Services.SmartInputParser.FormatReal(BitConverter.Int32BitsToSingle(Value));
            }
            return Value.ToString();
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias} ({Name})";

    public bool IsDigital => Kind is TagKind.DiscreteInput or TagKind.DiscreteOutput or TagKind.VirtualFlag or TagKind.ModbusCoil;
    public bool IsOutput => Kind is TagKind.DiscreteOutput or TagKind.ModbusCoil;
    public bool IsReadOnly => Kind is TagKind.DiscreteInput or TagKind.AnalogInput;
    public bool IsDataTypeEditable => Kind is TagKind.AnalogInput or TagKind.VirtualRegister or TagKind.VirtualRegisterRetain or TagKind.ModbusHolding;

    public string DataTypeText => DataType switch
    {
        TagDataType.Boolean => "BOOL",
        TagDataType.Float32 => "REAL",
        _ => "INT32"
    };

    public string AccessModeText => IsReadOnly ? "Read-Only" : "Read/Write";

    public string ModbusAddressText => Kind switch
    {
        TagKind.DiscreteInput => $"1000{Index + 1} (0x{Index:X4})",
        TagKind.DiscreteOutput => $"0000{Index - 8 + 1} (0x{Index:X4})",
        TagKind.AnalogInput => $"3000{Index - 16 + 1} (0x{Index:X4})",
        TagKind.VirtualFlag => $"000{Index + 1} (0x{Index:X4})",
        TagKind.VirtualRegister => $"400{Index + 1} (0x{Index:X4})",
        TagKind.VirtualRegisterRetain => $"400{Index + 1} (0x{Index:X4})",
        TagKind.Counter => $"40{Index + 1} (0x{Index:X4})",
        _ => $"0x{Index:X4}"
    };
}
