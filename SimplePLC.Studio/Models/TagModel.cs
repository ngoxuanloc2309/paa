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
    private int _value;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataTypeText))]
    private TagDataType _dataType = TagDataType.Int32;

    partial void OnKindChanged(TagKind value)
    {
        if (IsDigital)
        {
            DataType = TagDataType.Boolean;
        }
        else if (DataType == TagDataType.Boolean)
        {
            DataType = TagDataType.Int32;
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias} ({Name})";

    public bool IsDigital => Kind is TagKind.DiscreteInput or TagKind.DiscreteOutput or TagKind.VirtualFlag or TagKind.ModbusCoil;
    public bool IsOutput => Kind is TagKind.DiscreteOutput or TagKind.ModbusCoil;
    public bool IsReadOnly => Kind is TagKind.DiscreteInput or TagKind.AnalogInput;
    public bool IsDataTypeEditable => Kind is TagKind.AnalogInput or TagKind.VirtualRegister or TagKind.VirtualRegisterRetain or TagKind.ModbusHolding;

    public string DataTypeText => DataType switch
    {
        TagDataType.Boolean => "BOOL (Bit)",
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
