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
    [NotifyPropertyChangedFor(nameof(AvailableTags))]
    private ModbusDataType _dataType = ModbusDataType.Int16;

    partial void OnDataTypeChanged(ModbusDataType value)
    {
        OnPropertyChanged(nameof(AvailableTags));

        // Auto-Fallback / Invalidation: Nếu TargetTag hiện tại không tương thích với DataType mới, tự động xóa
        if (TargetTag != null && TargetTag.Kind != SimplePLC.Domain.Enums.TagKind.None)
        {
            bool isTagBool = TargetTag.IsDigital || TargetTag.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean;
            bool isTagReal = TargetTag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32;
            bool isTagInt = !isTagBool && !isTagReal;

            bool isCompatible = value switch
            {
                ModbusDataType.Bool => isTagBool,
                ModbusDataType.Real => isTagReal,
                _ => isTagInt
            };

            if (!isCompatible)
            {
                TargetTag = null;
            }
        }
    }

    [ObservableProperty]
    private ModbusEndianness _endianness = ModbusEndianness.BigEndian_ABCD;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultiplierWarning))]
    [NotifyPropertyChangedFor(nameof(MultiplierWarningText))]
    [NotifyPropertyChangedFor(nameof(MultiplierBorderBrush))]
    private float _multiplier = 1.0f;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMultiplierWarning))]
    [NotifyPropertyChangedFor(nameof(MultiplierWarningText))]
    [NotifyPropertyChangedFor(nameof(MultiplierBorderBrush))]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private TagModel? _targetTag;

    partial void OnTargetTagChanged(TagModel? value)
    {
        // Bi-directional Auto-Selection: Khi gán tag, tự động nhận diện và nhảy kiểu dữ liệu tương ứng nếu chưa khớp
        if (value != null && value.Kind != SimplePLC.Domain.Enums.TagKind.None)
        {
            if ((value.IsDigital || value.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean) && DataType != ModbusDataType.Bool)
            {
                DataType = ModbusDataType.Bool;
            }
            else if (value.DataType == SimplePLC.Domain.Enums.TagDataType.Float32 && DataType != ModbusDataType.Real)
            {
                DataType = ModbusDataType.Real;
            }
            else if (!value.IsDigital && value.DataType == SimplePLC.Domain.Enums.TagDataType.Int32 && DataType == ModbusDataType.Bool)
            {
                DataType = ModbusDataType.Int16;
            }
        }

        if (Connector != null)
        {
            Connector.Title = DisplayTitle;
            Connector.SetBilingualDescription(
                $"Giá trị đo '{Label}' lưu vào thanh ghi {value?.Name ?? "VREG"} (offset +{RegOffset}, {DataType}, x{Multiplier})",
                $"Measurement '{Label}' stored in register {value?.Name ?? "VREG"} (offset +{RegOffset}, {DataType}, x{Multiplier})");
        }
    }

    /// <summary>
    /// Hàm ủy quyền cung cấp danh sách Tag tương thích dựa trên kiểu dữ liệu Modbus.
    /// </summary>
    public Func<ModbusDataType, IEnumerable<TagModel>>? TagProvider { get; set; }

    /// <summary>
    /// Danh sách các Tag tương thích với DataType hiện tại của Pin.
    /// </summary>
    public IEnumerable<TagModel> AvailableTags => TagProvider != null
        ? TagProvider(DataType)
        : System.Linq.Enumerable.Empty<TagModel>();

    public string DisplayTitle => Label;

    public bool HasMultiplierWarning =>
        TargetTag != null &&
        TargetTag.DataType == SimplePLC.Domain.Enums.TagDataType.Int32 &&
        Math.Abs(Multiplier - Math.Round(Multiplier)) > 1e-5;

    public string MultiplierBorderBrush => HasMultiplierWarning ? "#C05621" : "#E2E8F0";

    public string? MultiplierWarningText => HasMultiplierWarning
        ? (Services.LocalizationService.Instance.IsVietnamese
            ? "⚠ Multiplier số lẻ sẽ làm tròn giá trị thành số nguyên trên tag INT32. Khuyến nghị đặt Multiplier = 1."
            : "⚠ Fractional multiplier will round to integer on INT32 tag. Recommended: Multiplier = 1.")
        : null;

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
            Connector.Title = DisplayTitle;
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
        TargetTagIndex = (TargetTag != null && TargetTag.Index < 65535 && !string.Equals(TargetTag.Name, "NONE", StringComparison.OrdinalIgnoreCase)) ? (ushort?)TargetTag.Index : null
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
