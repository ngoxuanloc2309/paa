using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using System;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel đại diện cho cấu hình một chân ngõ ra động (Dynamic Pin) của khối MODBUS READ.
/// </summary>
public partial class ModbusPinBindingViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private string _label = "Val";

    [ObservableProperty]
    private ushort _regOffset = 0;

    [ObservableProperty]
    private ModbusDataType _dataType = ModbusDataType.Int16;

    [ObservableProperty]
    private ModbusEndianness _endianness = ModbusEndianness.BigEndian_ABCD;

    [ObservableProperty]
    private float _multiplier = 1.0f;

    [ObservableProperty]
    private TagModel? _targetTag;

    [ObservableProperty]
    private double _currentValue = 0.0;

    /// <summary>
    /// Tham chiếu tới Connector trên Canvas để cập nhật nhãn khi đổi tên.
    /// </summary>
    public ConnectorViewModel? Connector { get; set; }

    partial void OnLabelChanged(string? oldValue, string newValue)
    {
        if (Connector != null)
        {
            Connector.Title = newValue;
        }
    }

    public ModbusMasterPinBindingDto ToDto() => new()
    {
        Id = Id,
        Label = Label,
        RegOffset = RegOffset,
        DataType = DataType,
        Endianness = Endianness,
        Multiplier = Multiplier,
        TargetTagIndex = TargetTag != null ? (ushort?)TargetTag.Index : null
    };

    public static ModbusPinBindingViewModel FromDto(ModbusMasterPinBindingDto dto) => new()
    {
        Id = string.IsNullOrWhiteSpace(dto.Id) ? Guid.NewGuid().ToString("N")[..8] : dto.Id,
        Label = string.IsNullOrWhiteSpace(dto.Label) ? "Val" : dto.Label,
        RegOffset = dto.RegOffset,
        DataType = dto.DataType,
        Endianness = dto.Endianness,
        Multiplier = dto.Multiplier == 0.0f ? 1.0f : dto.Multiplier
    };
}
