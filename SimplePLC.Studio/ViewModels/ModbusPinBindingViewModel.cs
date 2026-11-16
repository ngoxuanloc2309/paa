using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Modbus;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel đại diện cho cấu hình một chân ngõ ra động (Dynamic Pin) của khối MODBUS READ.
/// </summary>
public partial class ModbusPinBindingViewModel : ObservableObject
{
    private readonly ObservableCollection<TagOptionItem> _tagOptions = new();

    /// <summary>
    /// Danh sách các tùy chọn Tag trong Dropdown, có phân biệt nhãn liên kết với System Tab [🔗 ...] và nhãn xung đột [⛔ ...].
    /// </summary>
    public ReadOnlyObservableCollection<TagOptionItem> TagOptions { get; }

    public ModbusPinBindingViewModel()
    {
        TagOptions = new ReadOnlyObservableCollection<TagOptionItem>(_tagOptions);
    }

    /// <summary>
    /// Hàm cung cấp danh sách kênh đã khai báo trên cổng hiện tại ở System Tab để đối chiếu liên kết.
    /// </summary>
    public Func<IEnumerable<ModbusMasterChannelModel>>? SystemChannelsProvider { get; set; }

    /// <summary>
    /// Hàm cung cấp danh sách các chân Pin đồng cấp trong cùng khối để khóa chống chọn trùng Tag.
    /// </summary>
    public Func<IEnumerable<ModbusPinBindingViewModel>>? PeerPinsProvider { get; set; }

    /// <summary>
    /// Hàm cung cấp địa chỉ bắt đầu của khối (Wire Offset cơ sở) để đối chiếu địa chỉ chính xác.
    /// </summary>
    public Func<ushort>? BaseAddressProvider { get; set; }

    /// <summary>
    /// Hàm cung cấp mã hàm Modbus của khối để đối chiếu địa chỉ Modicon chính xác.
    /// </summary>
    public Func<byte>? FunctionCodeProvider { get; set; }

    /// <summary>
    /// Làm mới danh sách tùy chọn Tag với cơ chế Smart Tag Options (Kịch bản B).
    /// </summary>
    public void RefreshTagOptions()
    {
        var currentTags = AvailableTags.ToList();
        var sysChannels = SystemChannelsProvider?.Invoke()?.ToList() ?? new List<ModbusMasterChannelModel>();

        bool match = _tagOptions.Count == currentTags.Count;
        if (match)
        {
            for (int i = 0; i < currentTags.Count; i++)
            {
                if (!ReferenceEquals(_tagOptions[i].Tag, currentTags[i]))
                {
                    match = false;
                    break;
                }
            }
        }

        if (match)
        {
            foreach (var opt in _tagOptions)
            {
                UpdateTagOptionStatus(opt, sysChannels);
            }
        }
        else
        {
            var existingByTag = _tagOptions.ToDictionary(o => o.Tag);
            _tagOptions.Clear();
            foreach (var tag in currentTags)
            {
                if (existingByTag.TryGetValue(tag, out var existingOpt))
                {
                    UpdateTagOptionStatus(existingOpt, sysChannels);
                    _tagOptions.Add(existingOpt);
                }
                else
                {
                    var newOpt = new TagOptionItem(tag, true, tag.DisplayName);
                    UpdateTagOptionStatus(newOpt, sysChannels);
                    _tagOptions.Add(newOpt);
                }
            }
        }
    }

    private void UpdateTagOptionStatus(TagOptionItem opt, List<ModbusMasterChannelModel> sysChannels)
    {
        opt.DisplayText = opt.Tag.DisplayName;
        if (opt.Tag.Kind == SimplePLC.Domain.Enums.TagKind.None || string.Equals(opt.Tag.Name, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            opt.IsSelectable = true;
            opt.ConflictBadge = string.Empty;
            opt.ConflictTooltip = string.Empty;
            opt.LinkedBadge = string.Empty;
            return;
        }

        // Bước 1: Kiểm tra xung đột nội bộ trong cùng Block (Intra-Block Exclusion)
        // Nếu một Pin khác trong cùng khối đã chọn Tag này, Pin hiện tại không được phép chọn trùng
        var peerPins = PeerPinsProvider?.Invoke();
        if (peerPins != null)
        {
            var peer = peerPins.FirstOrDefault(p => !ReferenceEquals(p, this) && TagWriteOwnershipService.IsSameTag(p.TargetTag, opt.Tag));
            if (peer != null)
            {
                opt.IsSelectable = false;
                opt.ConflictBadge = $"[⛔ {peer.Label}]";
                opt.ConflictTooltip = LocalizationService.Instance.IsVietnamese 
                    ? $"Tag này đang được gán cho chân '{peer.Label}' trong cùng khối."
                    : $"This tag is already assigned to pin '{peer.Label}' in this block.";
                opt.LinkedBadge = string.Empty;
                return;
            }
        }

        // Bước 2: Kiểm tra xem Tag có thuộc kênh đã khai báo trên CỔNG NÀY ở System Tab không
        var linkedChannel = sysChannels.FirstOrDefault(c =>
            string.Equals(c.TargetTagName, opt.Tag.Name, StringComparison.OrdinalIgnoreCase));

        if (linkedChannel != null)
        {
            // Đối chiếu xem địa chỉ Modbus của Pin này có khớp đúng với địa chỉ của kênh System Tab không
            ushort baseAddr = BaseAddressProvider?.Invoke() ?? 0;
            byte fc = FunctionCodeProvider?.Invoke() ?? 3;
            int myWireOffset = baseAddr + RegOffset;
            int myModiconAddr = ModbusChannelSyncService.ToModiconAddress(myWireOffset, fc);

            if (linkedChannel.Address == myModiconAddr)
            {
                opt.LinkedBadge = $"[🔗 Addr {linkedChannel.Address}]";
            }
            else
            {
                opt.LinkedBadge = string.Empty;
            }

            opt.IsSelectable = true;
            opt.ConflictBadge = string.Empty;
            opt.ConflictTooltip = string.Empty;
            return;
        }

        opt.LinkedBadge = string.Empty;

        // Bước 3: Kiểm tra xem Tag có bị chiếm bởi nguồn khác (MQTT Sub, Port khác) không
        var claimant = TagWriteOwnershipService.Instance.GetClaimant(opt.Tag, this);
        if (claimant != null)
        {
            opt.IsSelectable = false;
            opt.ConflictBadge = claimant.DisplayBadge;
            opt.ConflictTooltip = claimant.TooltipDetail;
        }
        else
        {
            opt.IsSelectable = true;
            opt.ConflictBadge = string.Empty;
            opt.ConflictTooltip = string.Empty;
        }
    }
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString("N")[..8];

    [ObservableProperty]
    private string _label = "Val";

    [ObservableProperty]
    private ushort _regOffset = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableTags))]
    [NotifyPropertyChangedFor(nameof(IsEndiannessEnabled))]
    [NotifyPropertyChangedFor(nameof(EndiannessOptions))]
    private ModbusDataType _dataType = ModbusDataType.Int16;

    /// <summary>
    /// Cho biết tùy chọn thứ tự byte (Endianness) có áp dụng cho kiểu dữ liệu này hay không (False cho BOOL).
    /// </summary>
    public bool IsEndiannessEnabled => DataType != ModbusDataType.Bool;

    /// <summary>
    /// Danh sách tùy chọn Endianness tương thích theo kích thước dữ liệu (16-bit AB/BA, 32-bit ABCD/CDAB/BADC/DCBA, BOOL N/A).
    /// </summary>
    public IReadOnlyList<OptionItem<ModbusEndianness>> EndiannessOptions
    {
        get
        {
            if (DataType == ModbusDataType.Bool)
            {
                return new List<OptionItem<ModbusEndianness>>
                {
                    new(ModbusEndianness.BigEndian_ABCD, "— (N/A)")
                };
            }

            if (DataType is ModbusDataType.Int16 or ModbusDataType.UInt16)
            {
                return new List<OptionItem<ModbusEndianness>>
                {
                    new(ModbusEndianness.BigEndian_ABCD, "Big-Endian (AB)"),
                    new(ModbusEndianness.ByteSwap_BADC, "Byte-Swap (BA)")
                };
            }

            return new List<OptionItem<ModbusEndianness>>
            {
                new(ModbusEndianness.BigEndian_ABCD, "Big-Endian (ABCD)"),
                new(ModbusEndianness.MidBig_CDAB, "Word-Swap (CDAB)"),
                new(ModbusEndianness.ByteSwap_BADC, "Byte-Swap (BADC)"),
                new(ModbusEndianness.LittleEndian_DCBA, "Little-Endian (DCBA)")
            };
        }
    }

    partial void OnDataTypeChanged(ModbusDataType value)
    {
        OnPropertyChanged(nameof(AvailableTags));
        OnPropertyChanged(nameof(IsEndiannessEnabled));
        OnPropertyChanged(nameof(EndiannessOptions));

        // Chuẩn hóa Endianness khi chuyển đổi giữa 16-bit, 32-bit và Bool
        if (value is ModbusDataType.Int16 or ModbusDataType.UInt16)
        {
            if (Endianness is ModbusEndianness.MidBig_CDAB or ModbusEndianness.LittleEndian_DCBA)
            {
                Endianness = ModbusEndianness.BigEndian_ABCD;
            }
        }
        else if (value == ModbusDataType.Bool)
        {
            Endianness = ModbusEndianness.BigEndian_ABCD;
        }

        RefreshTagOptions();

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
            // Kiểm tra xem Tag có thuộc kênh đã khai báo trên System Tab không để tự động điền (Autofill)
            var sysChannels = SystemChannelsProvider?.Invoke();
            var linked = sysChannels?.FirstOrDefault(c => string.Equals(c.TargetTagName, value.Name, StringComparison.OrdinalIgnoreCase));
            if (linked != null)
            {
                DataType = linked.DataType;
                Endianness = linked.Endianness;
                Multiplier = (float)(linked.Scale == 0 ? 1.0 : linked.Scale);
            }
            else
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
        }

        if (Connector != null)
        {
            Connector.Title = DisplayTitle;
            Connector.SetBilingualDescription(
                $"Giá trị đo '{Label}' lưu vào thanh ghi {value?.Name ?? "VREG"} (offset +{RegOffset}, {DataType}, x{Multiplier})",
                $"Measurement '{Label}' stored in register {value?.Name ?? "VREG"} (offset +{RegOffset}, {DataType}, x{Multiplier})");
        }

        // Tự động thông báo cho các Pin đồng cấp trong khối làm mới lại TagOptions (khóa hoặc mở khóa chọn trùng)
        var peerPins = PeerPinsProvider?.Invoke();
        if (peerPins != null)
        {
            foreach (var peer in peerPins)
            {
                if (!ReferenceEquals(peer, this))
                {
                    peer.RefreshTagOptions();
                }
            }
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
