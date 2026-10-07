using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Domain.Enums;

namespace SimplePLC.Studio.Models;

public partial class TagModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
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

    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias} ({Name})";

    public bool IsDigital => Kind is TagKind.DiscreteInput or TagKind.DiscreteOutput or TagKind.VirtualFlag or TagKind.ModbusCoil;
    public bool IsOutput => Kind is TagKind.DiscreteOutput or TagKind.ModbusCoil;
    public bool IsReadOnly => Kind is TagKind.DiscreteInput or TagKind.AnalogInput;

    public string DataTypeText => IsDigital ? "BOOL (Bit)" : "INT32";

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
