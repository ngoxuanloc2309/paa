using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;


/// <summary>
/// Model quan sát biểu diễn từng kênh truyền Modbus Master trong bảng Master I/O Table của cổng RS-485.
/// Hỗ trợ cả 2 nhóm hàm: Đọc tuần hoàn (FC01..FC04) và Ghi theo sự kiện/chu kỳ (FC05, FC06, FC15, FC16).
/// Tuân thủ học thuyết First Principles (Axiom 1, 2, 3) về kiểu dữ liệu vật lý và máy trạng thái nhập liệu.
/// </summary>
public partial class ModbusMasterChannelItemViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequestFrameHex))]
    [NotifyPropertyChangedFor(nameof(ExpectedResponseHex))]
    private byte _slaveId = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadOperation))]
    [NotifyPropertyChangedFor(nameof(IsWriteOperation))]
    [NotifyPropertyChangedFor(nameof(OperationTypeBadge))]
    [NotifyPropertyChangedFor(nameof(FunctionCodeBadge))]
    [NotifyPropertyChangedFor(nameof(RequestFrameHex))]
    [NotifyPropertyChangedFor(nameof(ExpectedResponseHex))]
    [NotifyPropertyChangedFor(nameof(RegisterRangeDisplay))]
    [NotifyPropertyChangedFor(nameof(FilteredAvailableTags))]
    private byte _functionCode = 3;

    partial void OnFunctionCodeChanged(byte value)
    {
        OnPropertyChanged(nameof(FilteredAvailableTags));
        CoerceTargetTagCompatibility();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequestFrameHex))]
    [NotifyPropertyChangedFor(nameof(RegisterRangeDisplay))]
    private ushort _address = 40001;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RequestFrameHex))]
    [NotifyPropertyChangedFor(nameof(ExpectedResponseHex))]
    [NotifyPropertyChangedFor(nameof(RegisterSizeDisplay))]
    [NotifyPropertyChangedFor(nameof(IsEndiannessApplicable))]
    [NotifyPropertyChangedFor(nameof(FilteredAvailableTags))]
    private ModbusDataType _dataType = ModbusDataType.Int16;

    partial void OnDataTypeChanged(ModbusDataType value)
    {
        OnPropertyChanged(nameof(FilteredAvailableTags));
        CoerceTargetTagCompatibility();
    }

    private void CoerceTargetTagCompatibility()
    {
        if (TargetTag == null || TargetTag.Kind == TagKind.None || string.Equals(TargetTag.Name, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Axiom 1 (Hardware Ground Truth): Read operations (FC01..FC04) cannot target read-only tags (AI, DI, SYSTEM)
        if (IsReadOperation && TargetTag.IsReadOnly)
        {
            ResetTargetTag();
            return;
        }

        // Type safety: Bit vs Register
        if (DataType == ModbusDataType.Bool)
        {
            if (!TargetTag.IsDigital && TargetTag.DataType != SimplePLC.Domain.Enums.TagDataType.Boolean)
            {
                ResetTargetTag();
            }
        }
        else
        {
            if (TargetTag.IsDigital || TargetTag.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean)
            {
                ResetTargetTag();
                return;
            }

            if (DataType == ModbusDataType.Real 
                && TargetTag.DataType != SimplePLC.Domain.Enums.TagDataType.Float32 
                && TargetTag.Kind != TagKind.VirtualRegister 
                && TargetTag.Kind != TagKind.VirtualRegisterRetain)
            {
                ResetTargetTag();
                return;
            }

            if ((DataType == ModbusDataType.Int16 || DataType == ModbusDataType.UInt16 || DataType == ModbusDataType.Int32 || DataType == ModbusDataType.UInt32)
                && TargetTag.DataType != SimplePLC.Domain.Enums.TagDataType.Int32
                && TargetTag.Kind != TagKind.VirtualRegister
                && TargetTag.Kind != TagKind.VirtualRegisterRetain)
            {
                ResetTargetTag();
            }
        }
    }

    private void ResetTargetTag()
    {
        var noneTag = AvailableTagsProvider?.Invoke().FirstOrDefault(t => t.Kind == TagKind.None || string.Equals(t.Name, "NONE", StringComparison.OrdinalIgnoreCase));
        TargetTag = noneTag;
        TargetTagName = string.Empty;
        TargetTagAlias = string.Empty;
    }

    /// <summary>
    /// Hàm cung cấp danh sách toàn bộ Tag từ TagCatalog để lọc động.
    /// </summary>
    public Func<IEnumerable<TagModel>>? AvailableTagsProvider { get; set; }

    /// <summary>
    /// Danh sách Tag tương thích với kiểu dữ liệu vật lý (IEC 61131-3) của kênh Modbus hiện tại.
    /// BOOL: chỉ hiển thị cờ bit (DI, DO, VFLAG).
    /// REAL: chỉ hiển thị thanh ghi số thực (VREG Float32 / AI).
    /// INT16/INT32: chỉ hiển thị thanh ghi số nguyên (VREG Int32).
    /// Tag NONE (-- No Selection --) luôn xuất hiện đầu danh sách.
    /// </summary>
    public IEnumerable<TagModel> FilteredAvailableTags
    {
        get
        {
            var all = AvailableTagsProvider?.Invoke() ?? Enumerable.Empty<TagModel>();
            return FilterTagsByPhysicalDataType(all, DataType, IsReadOperation);
        }
    }

    /// <summary>
    /// Lọc danh sách Tag theo kiểu dữ liệu vật lý và chiều thao tác Đọc/Ghi theo Axiom 1 (IEC 61131-3).
    /// </summary>
    /// <param name="tags">Toàn bộ danh sách Tag từ Tag Catalog.</param>
    /// <param name="dataType">Kiểu dữ liệu Modbus đang cấu hình.</param>
    /// <param name="isReadOperation">True nếu là thao tác Đọc (FC01..FC04); False nếu là thao tác Ghi (FC05..FC16).</param>
    public static IEnumerable<TagModel> FilterTagsByPhysicalDataType(IEnumerable<TagModel> tags, ModbusDataType dataType, bool isReadOperation = true)
    {
        foreach (var tag in tags)
        {
            if (tag.Kind == TagKind.None || string.Equals(tag.Name, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                yield return tag;
                continue;
            }

            // Axiom 1 (Hardware Ground Truth):
            // Thao tác ĐỌC (FC01..04): PLC nhận dữ liệu từ cảm biến RS-485 và ghi vào bộ nhớ nội bộ.
            // Các chân đầu vào vật lý (AI, DI) và System inputs liên tục bị phần cứng vi điều khiển (ADC/GPIO sampler)
            // quét và ghi đè sau mỗi 10ms scan cycle. Do đó TUYỆT ĐỐI không cho phép ghi đè vào chân phần cứng chỉ đọc!
            if (isReadOperation && tag.IsReadOnly)
            {
                continue;
            }

            if (dataType == ModbusDataType.Bool)
            {
                if (tag.IsDigital || tag.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean)
                {
                    yield return tag;
                }
            }
            else if (dataType == ModbusDataType.Real)
            {
                if (!tag.IsDigital && (tag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32 || tag.Kind is TagKind.VirtualRegister or TagKind.VirtualRegisterRetain))
                {
                    yield return tag;
                }
            }
            else // Int16, Int32, UInt16, UInt32
            {
                if (!tag.IsDigital && (tag.DataType == SimplePLC.Domain.Enums.TagDataType.Int32 || tag.Kind is TagKind.VirtualRegister or TagKind.VirtualRegisterRetain))
                {
                    yield return tag;
                }
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegisterSizeDisplay))]
    private ModbusEndianness _endianness = ModbusEndianness.BigEndian_ABCD;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScalingFormulaDisplay))]
    private double _scale = 1.0;

    [ObservableProperty]
    private string _scaleText = "1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetTagDisplay))]
    private string _targetTagName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTagSelected))]
    [NotifyPropertyChangedFor(nameof(TargetTagDisplay))]
    private TagModel? _targetTag;

    [ObservableProperty]
    private string _targetTagAlias = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// Xác định hàm này thuộc nhóm Đọc (FC01, FC02, FC03, FC04).
    /// </summary>
    public bool IsReadOperation => FunctionCode is 1 or 2 or 3 or 4;

    /// <summary>
    /// Xác định hàm này thuộc nhóm Ghi (FC05, FC06, FC15, FC16).
    /// </summary>
    public bool IsWriteOperation => FunctionCode is 5 or 6 or 15 or 16;

    /// <summary>
    /// Kiểm tra xem Tag đích đã được chọn hợp lệ hay chưa (khác None).
    /// </summary>
    /// <summary>
    /// Xác định xem quy tắc Endianness có áp dụng cho kiểu dữ liệu này hay không.
    /// Chỉ áp dụng cho các kiểu dữ liệu 32-bit (Real, Int32, UInt32) chiếm từ 2 thanh ghi trở lên.
    /// Không áp dụng cho 1 thanh ghi 16-bit (Int16, UInt16) hoặc 1-bit (Bool).
    /// </summary>
    public bool IsEndiannessApplicable => DataType is ModbusDataType.Int32 or ModbusDataType.UInt32 or ModbusDataType.Real;

    public bool IsTagSelected => TargetTag != null && TargetTag.Kind != TagKind.None && !string.Equals(TargetTag.Name, "NONE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Nhãn trạng thái hiển thị thao tác (ĐỌC / GHI hoặc READ / WRITE).
    /// </summary>
    /// <summary>
    /// Nhãn hiển thị Function Code dưới dạng FCxx (ví dụ FC03).
    /// </summary>
    public string FunctionCodeBadge => $"FC{FunctionCode:D2}";

    public string OperationTypeBadge => IsReadOperation
        ? (LocalizationService.Instance.IsVietnamese ? "ĐỌC" : "READ")
        : (LocalizationService.Instance.IsVietnamese ? "GHI" : "WRITE");

    /// <summary>
    /// Tên Tag hiển thị an toàn.
    /// </summary>
    public string TargetTagDisplay => TargetTag != null && TargetTag.Kind != TagKind.None
        ? TargetTag.DisplayName
        : (!string.IsNullOrWhiteSpace(TargetTagName) ? TargetTagName : "—");

    partial void OnTargetTagChanged(TagModel? value)
    {
        if (value == null || value.Kind == TagKind.None || string.Equals(value.Name, "NONE", StringComparison.OrdinalIgnoreCase))
        {
            TargetTagName = string.Empty;
            TargetTagAlias = string.Empty;
        }
        else
        {
            TargetTagName = value.Name;
            TargetTagAlias = value.Alias ?? string.Empty;

            if (value.IsDigital || value.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean)
            {
                DataType = ModbusDataType.Bool;
            }
            else if (value.DataType == SimplePLC.Domain.Enums.TagDataType.Float32)
            {
                DataType = ModbusDataType.Real;
            }
            else if (value.DataType == SimplePLC.Domain.Enums.TagDataType.Int32 && DataType == ModbusDataType.Bool)
            {
                DataType = ModbusDataType.Int16;
            }
        }
    }

    partial void OnTargetTagAliasChanged(string value)
    {
        if (TargetTag != null && TargetTag.Kind != TagKind.None)
        {
            TargetTag.Alias = value;
        }
        Description = value;
    }

    partial void OnScaleTextChanged(string value)
    {
        string clean = value.Trim().Replace(',', '.');
        if (double.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal))
        {
            if (parsedVal > 0 && Math.Abs(Scale - parsedVal) > 1e-9)
            {
                Scale = parsedVal;
            }
        }
    }

    /// <summary>
    /// Chuẩn hóa giá trị ScaleText khi mất focus (LostFocus) hoặc nhấn Enter (Axiom 3).
    /// Tự động định dạng lại số hợp lệ hoặc khôi phục giá trị đã commit trước đó.
    /// </summary>
    public void CommitScaleText()
    {
        string clean = (ScaleText ?? string.Empty).Trim().Replace(',', '.');
        if (double.TryParse(clean, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedVal) && parsedVal > 0)
        {
            Scale = parsedVal;
            ScaleText = parsedVal.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            ScaleText = Scale.ToString("G", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Chuỗi biểu diễn Hex byte thực tế của bản tin Modbus RTU gửi đi (TX) kèm mã CRC-16 chuẩn.
    /// </summary>
    public string RequestFrameHex
    {
        get
        {
            ushort offset = CalculateOffset(Address);
            ushort quantity = (DataType is ModbusDataType.Int32 or ModbusDataType.Real) ? (ushort)2 : (ushort)1;

            Span<byte> frame = stackalloc byte[8];
            frame[0] = SlaveId;
            frame[1] = FunctionCode;
            frame[2] = (byte)(offset >> 8);
            frame[3] = (byte)(offset & 0xFF);

            if (IsWriteOperation && FunctionCode is 5 or 6)
            {
                frame[4] = 0x00;
                frame[5] = 0x01;
            }
            else
            {
                frame[4] = (byte)(quantity >> 8);
                frame[5] = (byte)(quantity & 0xFF);
            }

            ushort crc = SimplePLC.Protocol.Cryptography.Crc16Modbus.Compute(frame[..6]);
            byte crcLo = (byte)(crc & 0xFF);
            byte crcHi = (byte)(crc >> 8);
            frame[6] = crcLo;
            frame[7] = crcHi;

            return $"[ {frame[0]:X2} ] [ {frame[1]:X2} ] [ {frame[2]:X2} {frame[3]:X2} ] [ {frame[4]:X2} {frame[5]:X2} ] [ {frame[6]:X2} {frame[7]:X2} ]";
        }
    }

    /// <summary>
    /// Chuỗi biểu diễn Hex byte của bản tin Modbus RTU phản hồi dự kiến (RX).
    /// </summary>
    public string ExpectedResponseHex
    {
        get
        {
            ushort quantity = (DataType is ModbusDataType.Int32 or ModbusDataType.Real) ? (ushort)2 : (ushort)1;
            if (IsReadOperation)
            {
                byte byteCount = (byte)(FunctionCode is 1 or 2 ? 1 : (quantity * 2));
                return byteCount == 1
                    ? $"[ {SlaveId:X2} ] [ {FunctionCode:X2} ] [ 01 ] [ XX ] [ CRC ]"
                    : (byteCount == 2
                        ? $"[ {SlaveId:X2} ] [ {FunctionCode:X2} ] [ 02 ] [ HI LO ] [ CRC ]"
                        : $"[ {SlaveId:X2} ] [ {FunctionCode:X2} ] [ 04 ] [ B0 B1 B2 B3 ] [ CRC ]");
            }
            else
            {
                return $"[ {SlaveId:X2} ] [ {FunctionCode:X2} ] [ ACK ] [ CRC ]";
            }
        }
    }

    private static ushort CalculateOffset(ushort address) => address switch
    {
        >= 40001 and <= 49999 => (ushort)(address - 40001),
        >= 30001 and <= 39999 => (ushort)(address - 30001),
        >= 10001 and <= 19999 => (ushort)(address - 10001),
        >= 1 and <= 9999 => (ushort)(address - 1),
        _ => address
    };

    /// <summary>
    /// Định dạng hiển thị dải địa chỉ thanh ghi IEC theo chuẩn Modbus.
    /// </summary>
    public string RegisterRangeDisplay
    {
        get
        {
            ushort offset = CalculateOffset(Address);
            string fcName = FunctionCode switch
            {
                1 => "Coil (0x)",
                2 => "Discrete Input (1x)",
                3 => "Holding Reg (4x)",
                4 => "Input Reg (3x)",
                5 => "Write Coil (0x)",
                6 => "Write Reg (4x)",
                15 => "Write Multi-Coil (0x)",
                16 => "Write Multi-Reg (4x)",
                _ => "Register"
            };
            return $"{fcName} · {Address} (Offset 0x{offset:X4})";
        }
    }

    /// <summary>
    /// Kích thước dữ liệu và chiều dài thanh ghi chiếm dụng.
    /// </summary>
    public string RegisterSizeDisplay => DataType switch
    {
        ModbusDataType.Bool => "1 Bit (Coil / Contact)",
        ModbusDataType.Int16 => "1 Register (16-bit Signed, 2 Bytes)",
        ModbusDataType.UInt16 => "1 Register (16-bit Unsigned, 2 Bytes)",
        ModbusDataType.Int32 => $"2 Registers (32-bit Signed {Endianness}, 4 Bytes)",
        ModbusDataType.Real => $"2 Registers (32-bit Float {Endianness}, 4 Bytes)",
        _ => "1 Register (16-bit, 2 Bytes)"
    };

    /// <summary>
    /// Công thức toán học chuyển đổi đơn vị kỹ thuật (Scaling).
    /// </summary>
    public string ScalingFormulaDisplay
    {
        get
        {
            string baseFormula = $"Tag Value = Raw × {Scale:G}";
            if (Math.Abs(Scale - 1.0) > 1e-6)
            {
                double exampleVal = 100.0 * Scale;
                return $"{baseFormula} (vd: Raw 100 → {exampleVal:G})";
            }
            return $"{baseFormula} (1:1 Trực tiếp)";
        }
    }

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(OperationTypeBadge));
        OnPropertyChanged(nameof(ScalingFormulaDisplay));
        OnPropertyChanged(nameof(TargetTagDisplay));
        OnPropertyChanged(nameof(FilteredAvailableTags));
    }

    /// <summary>
    /// Danh sách các mã hàm Modbus Master khả dụng (FC01..FC04 đọc, FC05..FC16 ghi).
    /// </summary>
    public static IReadOnlyList<OptionItem<byte>> AvailableFunctionCodes { get; } = new List<OptionItem<byte>>
    {
        new(1, "FC01 · Read Coils (0x)"),
        new(2, "FC02 · Read Inputs (1x)"),
        new(3, "FC03 · Read Holding (4x)"),
        new(4, "FC04 · Read Input Regs (3x)"),
        new(5, "FC05 · Write Coil (0x)"),
        new(6, "FC06 · Write Register (4x)"),
        new(15, "FC15 · Write Coils (0x)"),
        new(16, "FC16 · Write Registers (4x)")
    };

    /// <summary>
    /// Danh sách các kiểu dữ liệu Modbus khả dụng.
    /// </summary>
    public static IReadOnlyList<OptionItem<ModbusDataType>> AvailableDataTypes { get; } = new List<OptionItem<ModbusDataType>>
    {
        new(ModbusDataType.Int16, "INT16 (Signed)"),
        new(ModbusDataType.UInt16, "UINT16 (Unsigned)"),
        new(ModbusDataType.Real, "REAL (Float32)"),
        new(ModbusDataType.Bool, "BOOL (1-bit)"),
        new(ModbusDataType.Int32, "INT32 (Signed)")
    };

    /// <summary>
    /// Danh sách thứ tự byte (Endianness) theo chuẩn công nghiệp.
    /// </summary>
    public static IReadOnlyList<OptionItem<ModbusEndianness>> AvailableEndianness { get; } = new List<OptionItem<ModbusEndianness>>
    {
        new(ModbusEndianness.BigEndian_ABCD, "Big-Endian (ABCD)"),
        new(ModbusEndianness.MidBig_CDAB, "Word-Swap (CDAB)"),
        new(ModbusEndianness.ByteSwap_BADC, "Byte-Swap (BADC)"),
        new(ModbusEndianness.LittleEndian_DCBA, "Little-Endian (DCBA)")
    };
}

/// <summary>
/// Model quan sát biểu diễn từng mục ánh xạ thanh ghi Modbus Slave tại phân vùng 0x1500 (Memory Protection Sandbox).
/// </summary>
public partial class ModbusSlaveMapItemViewModel : ObservableObject
{
    [ObservableProperty]
    private ushort _modbusAddress;

    [ObservableProperty]
    private ushort _targetTagIndex;

    [ObservableProperty]
    private string _targetTagName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadOnly))]
    [NotifyPropertyChangedFor(nameof(IsReadWrite))]
    [NotifyPropertyChangedFor(nameof(AccessRightBadgeColor))]
    private string _accessRight = "ReadOnly"; // "ReadOnly", "ReadWrite"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataTypeName))]
    private byte _dataType = 2; // 1=BOOL, 2=INT32, 3=REAL, 4=UINT16

    [ObservableProperty]
    private byte _portMask = 0x0F;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccessRightEnabled))]
    [NotifyPropertyChangedFor(nameof(EffectiveAccessTooltip))]
    private bool _isAccessRightLocked = false;

    public bool IsAccessRightEnabled => !IsAccessRightLocked;

    public string EffectiveAccessTooltip => IsAccessRightLocked
        ? LocalizationService.Instance["ModbusSlaveAccessLockedTooltip"]
        : LocalizationService.Instance["ModbusSlaveAccessModeTooltip"];

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(EffectiveAccessTooltip));
    }

    [ObservableProperty]
    private string _lockReason = string.Empty;

    public bool IsReadOnly => string.Equals(AccessRight, "ReadOnly", StringComparison.OrdinalIgnoreCase);
    public bool IsReadWrite => string.Equals(AccessRight, "ReadWrite", StringComparison.OrdinalIgnoreCase);

    public string AccessRightBadgeColor => IsReadOnly ? "#006487" : "#C05621"; // Industrial Petrol Blue for RO, Safety Amber for RW

    public string DataTypeName => DataType switch
    {
        1 => "BOOL (1-bit)",
        2 => "INT32 (2 Regs)",
        3 => "REAL (Float32)",
        4 => "UINT16 (1 Reg)",
        _ => "INT32 (2 Regs)"
    };

    [ObservableProperty]
    private TagModel? _targetTag;

    partial void OnTargetTagChanged(TagModel? value)
    {
        if (value == null) return;
        TargetTagName = value.Name;
        TargetTagIndex = (ushort)value.Index;
        if (value.IsDigital)
        {
            DataType = 1;
        }
        else
        {
            DataType = value.DataType == TagDataType.Float32 ? (byte)3 : (byte)2;
        }

        if (value.Kind is TagKind.DiscreteInput or TagKind.AnalogInput)
        {
            IsAccessRightLocked = true;
            LockReason = "Physical Input (DI/AI) is hard-locked to Read-Only.";
            AccessRight = "ReadOnly";
        }
        else
        {
            IsAccessRightLocked = false;
            LockReason = string.Empty;
        }

        if (string.IsNullOrWhiteSpace(Description) || Description.StartsWith("Digital Input") || Description.StartsWith("Analog Input") || Description.StartsWith("Digital Output") || Description.StartsWith("Control Flag") || Description.StartsWith("Parameter") || Description.StartsWith("Custom Mapping"))
        {
            Description = !string.IsNullOrWhiteSpace(value.Alias) ? value.Alias : value.DisplayName;
        }
    }

    [RelayCommand]
    public void ToggleAccessRight()
    {
        if (IsAccessRightLocked)
        {
            AccessRight = "ReadOnly";
            return;
        }

        AccessRight = IsReadOnly ? "ReadWrite" : "ReadOnly";
    }

    public void SetAccessRight(string right)
    {
        if (IsAccessRightLocked && string.Equals(right, "ReadWrite", StringComparison.OrdinalIgnoreCase))
        {
            AccessRight = "ReadOnly";
            return;
        }

        AccessRight = right;
    }
}

/// <summary>
/// Model quan sát biểu diễn từng cổng RS-485 trong giao diện cấu hình đa cổng.
/// Hỗ trợ cả 2 chế độ: Modbus RTU Master và Modbus RTU Slave (Server).
/// </summary>
public partial class ModbusPortItemViewModel : ObservableObject
{
    public byte PortIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private string _portName = "RS-485 #1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPill))]
    [NotifyPropertyChangedFor(nameof(StatusPillColor))]
    private bool _enabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    [NotifyPropertyChangedFor(nameof(IsMasterMode))]
    [NotifyPropertyChangedFor(nameof(IsSlaveMode))]
    private string _role = "Master"; // "Master", "Slave"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private byte _slaveId = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private uint _baudRate = 9600;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private byte _dataBits = 8;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private string _parity = "None"; // "None", "Even", "Odd"

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private byte _stopBits = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private ushort _responseTimeoutMs = 200;

    [ObservableProperty]
    private ushort _interFrameDelayMs = 20;

    private readonly TagCatalogViewModel? _tagCatalog;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private ushort _retryCount = 1;

    [ObservableProperty]
    private ModbusMasterChannelItemViewModel? _selectedMasterChannel;

    /// <summary>
    /// Danh sách các kênh truyền Modbus Master tuần hoàn và phát lệnh trên cổng này.
    /// </summary>
    public ObservableCollection<ModbusMasterChannelItemViewModel> MasterChannels { get; } = new();

    /// <summary>
    /// Xác định cổng có kênh truyền Modbus Master nào đang được cấu hình hay không.
    /// </summary>
    public bool HasMasterChannels => MasterChannels.Count > 0;

    /// <summary>
    /// Số lượng kênh đọc tuần hoàn (FC01-04).
    /// </summary>
    public int ActivePolledChannelsCount => MasterChannels.Count(c => c.IsReadOperation);

    /// <summary>
    /// Số lượng lệnh ghi (FC05-16).
    /// </summary>
    public int ActiveWriteChannelsCount => MasterChannels.Count(c => c.IsWriteOperation);

    /// <summary>
    /// Ước tính chu kỳ quét toàn vòng (ms) dựa trên tổng số byte và tốc độ Baud.
    /// </summary>
    public int EstimatedScanCycleMs
    {
        get
        {
            int readCount = ActivePolledChannelsCount;
            if (readCount == 0) return 0;
            double msPerChannel = Math.Max(15.0, (180.0 / Math.Max(1200, BaudRate)) * 1000.0 + InterFrameDelayMs + 10.0);
            return (int)Math.Round(readCount * msPerChannel);
        }
    }

    public bool IsMasterMode => string.Equals(Role, "Master", StringComparison.OrdinalIgnoreCase);
    public bool IsSlaveMode => string.Equals(Role, "Slave", StringComparison.OrdinalIgnoreCase);

    public string DisplayName =>
        (LocalizationService.Instance.IsVietnamese ? "Cổng " : "Port ") + $"{PortIndex + 1}: {PortName}";

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(StatusPill));
        OnPropertyChanged(nameof(SummaryBadge));
        foreach (var ch in MasterChannels)
        {
            ch.NotifyLanguageChanged();
        }
    }

    public string SummaryBadge
    {
        get
        {
            string parityCode = !string.IsNullOrEmpty(Parity) ? Parity.Substring(0, 1).ToUpperInvariant() : "N";
            if (IsSlaveMode)
            {
                return $"{BaudRate} · {DataBits}-{parityCode}-{StopBits} · Slave (ID: {SlaveId})";
            }
            return $"{BaudRate} · {DataBits}-{parityCode}-{StopBits} · Master · {ResponseTimeoutMs}ms";
        }
    }

    public string StatusPill => Enabled 
        ? (LocalizationService.Instance.IsVietnamese ? "HOẠT ĐỘNG" : "ACTIVE") 
        : (LocalizationService.Instance.IsVietnamese ? "VÔ HIỆU" : "DISABLED");

    public string StatusPillColor => Enabled ? "#107C41" : "#64748B";

    public ModbusPortItemViewModel(ModbusPortConfigModel model, TagCatalogViewModel? tagCatalog = null)
    {
        _tagCatalog = tagCatalog;
        PortIndex = model.PortIndex;
        _portName = model.PortName;
        _enabled = model.Enabled;
        _role = string.IsNullOrWhiteSpace(model.Role) ? "Master" : model.Role;
        _slaveId = model.SlaveId == 0 ? (byte)1 : model.SlaveId;
        _baudRate = model.BaudRate;
        _dataBits = model.DataBits == 0 ? (byte)8 : model.DataBits;
        _parity = string.IsNullOrWhiteSpace(model.Parity) ? "None" : model.Parity;
        _stopBits = model.StopBits == 0 ? (byte)1 : model.StopBits;
        _responseTimeoutMs = model.ResponseTimeoutMs == 0 ? (ushort)200 : model.ResponseTimeoutMs;
        _interFrameDelayMs = model.InterFrameDelayMs;
        _retryCount = model.RetryCount;

        if (model.MasterChannels != null)
        {
            foreach (var ch in model.MasterChannels)
            {
                var item = new ModbusMasterChannelItemViewModel
                {
                    AvailableTagsProvider = () => tagCatalog?.AllTags ?? Enumerable.Empty<TagModel>(),
                    SlaveId = ch.SlaveId == 0 ? (byte)1 : ch.SlaveId,
                    FunctionCode = ch.FunctionCode == 0 ? (byte)3 : ch.FunctionCode,
                    Address = ch.Address,
                    DataType = ch.DataType,
                    Endianness = ch.Endianness,
                    Scale = ch.Scale == 0 ? 1.0 : ch.Scale,
                    ScaleText = (ch.Scale == 0 ? 1.0 : ch.Scale).ToString("G", System.Globalization.CultureInfo.InvariantCulture),
                    TargetTagName = ch.TargetTagName ?? string.Empty,
                    Description = ch.Description ?? string.Empty
                };
                if (tagCatalog != null)
                {
                    if (!string.IsNullOrWhiteSpace(item.TargetTagName))
                    {
                        item.TargetTag = tagCatalog.AllTags.FirstOrDefault(t => t.Name == item.TargetTagName);
                    }
                    if (item.TargetTag == null)
                    {
                        item.TargetTag = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.None);
                    }
                }
                if (item.TargetTag != null && item.TargetTag.Kind != TagKind.None)
                {
                    item.TargetTagAlias = item.TargetTag.Alias ?? item.Description;
                }
                else
                {
                    item.TargetTagAlias = item.Description;
                }
                MasterChannels.Add(item);
            }
        }
        SelectedMasterChannel = MasterChannels.FirstOrDefault();

        MasterChannels.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasMasterChannels));
            OnPropertyChanged(nameof(ActivePolledChannelsCount));
            OnPropertyChanged(nameof(ActiveWriteChannelsCount));
            OnPropertyChanged(nameof(EstimatedScanCycleMs));
        };
    }

    /// <summary>
    /// Thêm một kênh truyền Modbus Master mới với các giá trị mặc định chuẩn công nghiệp.
    /// </summary>
    [RelayCommand]
    public void AddMasterChannel()
    {
        var noneTag = _tagCatalog?.AllTags.FirstOrDefault(t => t.Kind == TagKind.None);
        ushort nextAddr = MasterChannels.Count > 0 ? (ushort)(MasterChannels.Max(c => c.Address) + 1) : (ushort)40001;
        var item = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => _tagCatalog?.AllTags ?? Enumerable.Empty<TagModel>(),
            SlaveId = 1,
            FunctionCode = 3,
            Address = nextAddr,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Scale = 1.0,
            ScaleText = "1",
            TargetTag = noneTag
        };
        MasterChannels.Add(item);
        SelectedMasterChannel = item;
    }

    /// <summary>
    /// Xóa một kênh truyền Modbus Master khỏi danh sách.
    /// </summary>
    /// <param name="channel">Kênh Modbus Master cần xóa.</param>
    [RelayCommand]
    public void RemoveMasterChannel(ModbusMasterChannelItemViewModel? channel)
    {
        if (channel != null && MasterChannels.Contains(channel))
        {
            MasterChannels.Remove(channel);
            if (SelectedMasterChannel == channel)
            {
                SelectedMasterChannel = MasterChannels.FirstOrDefault();
            }
        }
    }

    public void CopyToModel(ModbusPortConfigModel model)
    {
        model.PortIndex = PortIndex;
        model.PortName = PortName;
        model.Enabled = Enabled;
        model.Role = Role;
        model.SlaveId = SlaveId;
        model.BaudRate = BaudRate;
        model.DataBits = DataBits;
        model.Parity = Parity;
        model.StopBits = StopBits;
        model.ResponseTimeoutMs = ResponseTimeoutMs;
        model.InterFrameDelayMs = InterFrameDelayMs;
        model.RetryCount = RetryCount;

        model.MasterChannels = MasterChannels.Select(c => new ModbusMasterChannelModel
        {
            SlaveId = c.SlaveId,
            FunctionCode = c.FunctionCode,
            Address = c.Address,
            DataType = c.DataType,
            Endianness = c.Endianness,
            Scale = c.Scale,
            TargetTagName = c.TargetTagName,
            Description = !string.IsNullOrWhiteSpace(c.TargetTagAlias) ? c.TargetTagAlias : c.Description
        }).ToList();
    }
}

/// <summary>
/// Các phân vùng bộ nhớ Modbus Slave để hiển thị theo dạng tab (Moxa style).
/// </summary>
public enum ModbusSlaveMemoryTab
{
    /// <summary>Tất cả các phân vùng (Holding Registers, Coils, Discrete Inputs).</summary>
    All,
    /// <summary>Chỉ phân vùng Holding Registers (4xxxx).</summary>
    HoldingRegisters,
    /// <summary>Chỉ phân vùng Coils (0xxxx).</summary>
    Coils,
    /// <summary>Chỉ phân vùng Discrete Inputs (1xxxx).</summary>
    DiscreteInputs
}

/// <summary>
/// ViewModel điều khiển hộp thoại cấu hình Đa cổng RS-485 Modbus Master & Slave (SPLC-SPEC-MBM-003 & SPLC-SPEC-SLAVE-001).
/// Cho phép kỹ sư cấu hình độc lập các cổng UART/RS-485 thành Master hoặc Slave, quản lý bảng ánh xạ vùng nhớ Modbus Slave (0x1500)
/// và bảo vệ vùng nhớ 3 cấp độ (Memory Protection Sandbox).
/// </summary>
public partial class ModbusPortConfigViewModel : ObservableObject
{
    private readonly ProjectMetadata _metadata;
    public TagCatalogViewModel? TagCatalog { get; }

    public ObservableCollection<ModbusPortItemViewModel> Ports { get; } = new();

    public ObservableCollection<ModbusSlaveMapItemViewModel> SlaveMapEntries { get; } = new();

    /// <summary>
    /// Bản đồ địa chỉ Modbus Slave cố định của thiết bị SynaptiX Controller.
    /// </summary>
    /// <summary>
    /// Thanh ghi Modbus Slave đang được chọn trên bảng để hiển thị tại Inspector.
    /// </summary>
    [ObservableProperty]
    private ModbusSlaveFixedRegisterViewModel? _selectedFixedRegisterItem;

    /// <summary>
    /// Bản đồ địa chỉ Modbus Slave cố định của thiết bị SynaptiX Controller.
    /// </summary>
    public IReadOnlyList<ModbusSlaveFixedRegisterViewModel> FixedRegisterMap { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredFixedRegisterMap))]
    [NotifyPropertyChangedFor(nameof(IsTabAllSelected))]
    [NotifyPropertyChangedFor(nameof(IsTabHoldingSelected))]
    [NotifyPropertyChangedFor(nameof(IsTabCoilsSelected))]
    [NotifyPropertyChangedFor(nameof(IsTabDiscreteInputsSelected))]
    private ModbusSlaveMemoryTab _selectedMemoryTab = ModbusSlaveMemoryTab.All;

    /// <summary>Xác định tab Tất cả đang được chọn.</summary>
    public bool IsTabAllSelected => SelectedMemoryTab == ModbusSlaveMemoryTab.All;

    /// <summary>Xác định tab Holding Registers đang được chọn.</summary>
    public bool IsTabHoldingSelected => SelectedMemoryTab == ModbusSlaveMemoryTab.HoldingRegisters;

    /// <summary>Xác định tab Coils đang được chọn.</summary>
    public bool IsTabCoilsSelected => SelectedMemoryTab == ModbusSlaveMemoryTab.Coils;

    /// <summary>Xác định tab Discrete Inputs đang được chọn.</summary>
    public bool IsTabDiscreteInputsSelected => SelectedMemoryTab == ModbusSlaveMemoryTab.DiscreteInputs;

    /// <summary>
    /// Danh sách thanh ghi Modbus Slave đã được lọc theo tab phân vùng đang chọn.
    /// </summary>
    public IReadOnlyList<ModbusSlaveFixedRegisterViewModel> FilteredFixedRegisterMap
    {
        get
        {
            if (FixedRegisterMap == null) return Array.Empty<ModbusSlaveFixedRegisterViewModel>();

            return SelectedMemoryTab switch
            {
                ModbusSlaveMemoryTab.HoldingRegisters => FixedRegisterMap.Where(r => r.Category == "Holding Registers").ToList(),
                ModbusSlaveMemoryTab.Coils => FixedRegisterMap.Where(r => r.Category == "Coils").ToList(),
                ModbusSlaveMemoryTab.DiscreteInputs => FixedRegisterMap.Where(r => r.Category == "Discrete Inputs").ToList(),
                _ => FixedRegisterMap
            };
        }
    }

    /// <summary>Tổng số lượng thanh ghi trên tất cả phân vùng.</summary>
    public int CountAll => FixedRegisterMap?.Count ?? 0;

    /// <summary>Số lượng thanh ghi Holding Registers (4xxxx).</summary>
    public int CountHolding => FixedRegisterMap?.Count(r => r.Category == "Holding Registers") ?? 0;

    /// <summary>Số lượng bit Coils (0xxxx).</summary>
    public int CountCoils => FixedRegisterMap?.Count(r => r.Category == "Coils") ?? 0;

    /// <summary>Số lượng bit Discrete Inputs (1xxxx).</summary>
    public int CountDiscreteInputs => FixedRegisterMap?.Count(r => r.Category == "Discrete Inputs") ?? 0;

    /// <summary>
    /// Lệnh chuyển đổi tab phân vùng bộ nhớ Modbus Slave (Moxa-style).
    /// </summary>
    /// <param name="tabName">Tên tab: All, Holding, Coils, DiscreteInputs.</param>
    [RelayCommand]
    public void SelectMemoryTab(string? tabName)
    {
        if (string.IsNullOrWhiteSpace(tabName))
        {
            SelectedMemoryTab = ModbusSlaveMemoryTab.All;
            return;
        }

        if (Enum.TryParse<ModbusSlaveMemoryTab>(tabName, true, out var tab))
        {
            SelectedMemoryTab = tab;
        }
        else if (string.Equals(tabName, "Holding", StringComparison.OrdinalIgnoreCase))
        {
            SelectedMemoryTab = ModbusSlaveMemoryTab.HoldingRegisters;
        }
        else if (string.Equals(tabName, "Discrete", StringComparison.OrdinalIgnoreCase) || string.Equals(tabName, "DI", StringComparison.OrdinalIgnoreCase))
        {
            SelectedMemoryTab = ModbusSlaveMemoryTab.DiscreteInputs;
        }
        else
        {
            SelectedMemoryTab = ModbusSlaveMemoryTab.All;
        }

        if (SelectedFixedRegisterItem == null || !FilteredFixedRegisterMap.Contains(SelectedFixedRegisterItem))
        {
            SelectedFixedRegisterItem = FilteredFixedRegisterMap.FirstOrDefault();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedPort))]
    [NotifyPropertyChangedFor(nameof(IsSlaveViewActive))]
    private ModbusPortItemViewModel? _selectedPort;

    [ObservableProperty]
    private ModbusSlaveMapItemViewModel? _selectedSlaveMapEntry;

    public bool HasSelectedPort => SelectedPort != null;

    public bool IsSlaveViewActive => SelectedPort != null && SelectedPort.IsSlaveMode;

    public IReadOnlyList<uint> AvailableBaudRates { get; } = new uint[]
    {
        4800, 9600, 19200, 38400, 57600, 115200
    };

    public IReadOnlyList<string> AvailableParities { get; } = new string[]
    {
        "None", "Even", "Odd"
    };

    public IReadOnlyList<byte> AvailableStopBits { get; } = new byte[]
    {
        1, 2
    };

    public IReadOnlyList<byte> AvailableDataBits { get; } = new byte[]
    {
        8, 7
    };

    public IReadOnlyList<string> AvailableRoles { get; } = new string[]
    {
        "Master", "Slave"
    };

    [ObservableProperty]
    private bool _hasPhysicalPorts = true;

    public ObservableCollection<TagModel> AvailableTags => TagCatalog?.AllTags ?? new();
    public bool HasAvailableTags => AvailableTags.Count > 0;
    public bool HasSlaveMapEntries => SlaveMapEntries.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExportNoticeMessage))]
    private string _exportNoticeMessage = string.Empty;

    public bool HasExportNoticeMessage => !string.IsNullOrEmpty(ExportNoticeMessage);

    public event Action? RequestClose;

    public ModbusPortConfigViewModel(ProjectMetadata metadata, TagCatalogViewModel? tagCatalog = null)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        TagCatalog = tagCatalog;

        ReloadPortsFromMetadata();

        // Tải các mục ánh xạ Modbus Slave đã lưu
        if (_metadata.ModbusSlaveMapEntries != null && _metadata.ModbusSlaveMapEntries.Count > 0)
        {
            foreach (var e in _metadata.ModbusSlaveMapEntries)
            {
                var tag = TagCatalog?.AllTags.FirstOrDefault(t => t.Index == e.TargetTagIndex);
                string tagName = tag != null ? tag.Name : $"TAG_{e.TargetTagIndex}";
                bool locked = tag != null && (tag.Kind is TagKind.DiscreteInput or TagKind.AnalogInput);
                var item = new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = e.ModbusAddress,
                    TargetTagIndex = e.TargetTagIndex,
                    TargetTagName = tagName,
                    AccessRight = locked ? "ReadOnly" : (string.IsNullOrWhiteSpace(e.AccessRight) ? "ReadOnly" : e.AccessRight),
                    DataType = e.DataType,
                    PortMask = e.PortMask,
                    Description = e.Description ?? string.Empty,
                    IsAccessRightLocked = locked,
                    LockReason = locked ? "Physical Input (DI/AI) locked to Read-Only." : string.Empty
                };
                SlaveMapEntries.Add(item);
            }
        }

        FixedRegisterMap = ModbusSlaveFixedMap.DefaultEntries
            .Select(dto => new ModbusSlaveFixedRegisterViewModel(dto))
            .ToList();
        SelectedFixedRegisterItem = FixedRegisterMap.FirstOrDefault();

        HasPhysicalPorts = Ports.Count > 0;
        SelectedPort = Ports.FirstOrDefault();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        SlaveMapEntries.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSlaveMapEntries));
        if (SlaveMapEntries.Count == 0 && Ports.Any(p => p.IsSlaveMode))
        {
            SmartPopulateDefault();
        }
    }

    partial void OnSelectedPortChanged(ModbusPortItemViewModel? value)
    {
        OnPropertyChanged(nameof(IsSlaveViewActive));
    }

    public IReadOnlyList<string> AvailableAccessRights { get; } = new[] { "ReadOnly", "ReadWrite" };

    private void OnLanguageChanged()
    {
        foreach (var port in Ports)
        {
            port.NotifyLanguageChanged();
        }
        foreach (var entry in SlaveMapEntries)
        {
            entry.NotifyLanguageChanged();
        }
        foreach (var reg in FixedRegisterMap)
        {
            reg.NotifyLanguageChanged();
        }
    }

    [RelayCommand]
    public void SetMasterRole()
    {
        if (SelectedPort != null)
        {
            SelectedPort.Role = "Master";
            OnPropertyChanged(nameof(IsSlaveViewActive));
        }
    }

    [RelayCommand]
    public void SetSlaveRole()
    {
        if (SelectedPort != null)
        {
            SelectedPort.Role = "Slave";
            OnPropertyChanged(nameof(IsSlaveViewActive));
            if (SlaveMapEntries.Count == 0)
            {
                SmartPopulateDefault();
            }
        }
    }

    [RelayCommand]
    public void SmartPopulateDefault()
    {
        SlaveMapEntries.Clear();
        ushort curAddr = 0;

        if (TagCatalog != null && TagCatalog.AllTags.Count > 0)
        {
            // 1. Ngõ vào số DI0..DI7 (Locked Read-Only)
            foreach (var di in TagCatalog.AllTags.Where(t => t.Kind == TagKind.DiscreteInput).OrderBy(t => t.Index).Take(8))
            {
                if (SlaveMapEntries.Count >= ModbusRegisterMap.ModbusSlaveMapMaxEntries) break;
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTag = di,
                    TargetTagIndex = (ushort)di.Index,
                    TargetTagName = di.Name,
                    AccessRight = "ReadOnly",
                    DataType = 1, // BOOL
                    PortMask = 0x0F,
                    Description = !string.IsNullOrWhiteSpace(di.Alias) ? di.Alias : $"Digital Input {di.Name}",
                    IsAccessRightLocked = true,
                    LockReason = "Physical Input (DI) is hard-locked to Read-Only."
                });
            }

            // 2. Ngõ vào tương tự AI0..AI3 (Locked Read-Only, REAL)
            foreach (var ai in TagCatalog.AllTags.Where(t => t.Kind == TagKind.AnalogInput).OrderBy(t => t.Index).Take(4))
            {
                if (SlaveMapEntries.Count >= ModbusRegisterMap.ModbusSlaveMapMaxEntries) break;
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTag = ai,
                    TargetTagIndex = (ushort)ai.Index,
                    TargetTagName = ai.Name,
                    AccessRight = "ReadOnly",
                    DataType = 3, // REAL
                    PortMask = 0x0F,
                    Description = !string.IsNullOrWhiteSpace(ai.Alias) ? ai.Alias : $"Analog Input {ai.Name}",
                    IsAccessRightLocked = true,
                    LockReason = "Physical Input (AI) is hard-locked to Read-Only."
                });
            }

            // 3. Ngõ ra số DO0..DO7 (Read/Write)
            foreach (var @do in TagCatalog.AllTags.Where(t => t.Kind == TagKind.DiscreteOutput).OrderBy(t => t.Index).Take(8))
            {
                if (SlaveMapEntries.Count >= ModbusRegisterMap.ModbusSlaveMapMaxEntries) break;
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTag = @do,
                    TargetTagIndex = (ushort)@do.Index,
                    TargetTagName = @do.Name,
                    AccessRight = "ReadWrite",
                    DataType = 1, // BOOL
                    PortMask = 0x0F,
                    Description = !string.IsNullOrWhiteSpace(@do.Alias) ? @do.Alias : $"Digital Output {@do.Name}",
                    IsAccessRightLocked = false
                });
            }

            // 4. Biến cờ lệnh VFLAG0..VFLAG7 (Read/Write Sandbox)
            foreach (var vf in TagCatalog.AllTags.Where(t => t.Kind == TagKind.VirtualFlag).OrderBy(t => t.Index).Take(8))
            {
                if (SlaveMapEntries.Count >= ModbusRegisterMap.ModbusSlaveMapMaxEntries) break;
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTag = vf,
                    TargetTagIndex = (ushort)vf.Index,
                    TargetTagName = vf.Name,
                    AccessRight = "ReadWrite",
                    DataType = 1, // BOOL
                    PortMask = 0x0F,
                    Description = !string.IsNullOrWhiteSpace(vf.Alias) ? vf.Alias : $"Control Flag {vf.Name}",
                    IsAccessRightLocked = false
                });
            }

            // 5. Thanh ghi thông số VREG0..VREG3 (Read/Write Sandbox)
            foreach (var vr in TagCatalog.AllTags.Where(t => t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.VirtualRegisterRetain).OrderBy(t => t.Index).Take(4))
            {
                if (SlaveMapEntries.Count >= ModbusRegisterMap.ModbusSlaveMapMaxEntries) break;
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTag = vr,
                    TargetTagIndex = (ushort)vr.Index,
                    TargetTagName = vr.Name,
                    AccessRight = "ReadWrite",
                    DataType = 2, // INT32
                    PortMask = 0x0F,
                    Description = !string.IsNullOrWhiteSpace(vr.Alias) ? vr.Alias : $"Parameter {vr.Name}",
                    IsAccessRightLocked = false
                });
            }
        }
        else
        {
            // Default 32-entry sandbox template
            for (ushort i = 0; i < 8; i++)
            {
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTagIndex = i,
                    TargetTagName = $"DI{i}",
                    AccessRight = "ReadOnly",
                    DataType = 1,
                    PortMask = 0x0F,
                    Description = $"Digital Input {i}",
                    IsAccessRightLocked = true,
                    LockReason = "Physical Input (DI) is hard-locked to Read-Only."
                });
            }

            for (ushort i = 0; i < 4; i++)
            {
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTagIndex = (ushort)(16 + i),
                    TargetTagName = $"AI{i}",
                    AccessRight = "ReadOnly",
                    DataType = 3,
                    PortMask = 0x0F,
                    Description = $"Analog Input {i}",
                    IsAccessRightLocked = true,
                    LockReason = "Physical Input (AI) is hard-locked to Read-Only."
                });
            }

            for (ushort i = 0; i < 8; i++)
            {
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTagIndex = (ushort)(8 + i),
                    TargetTagName = $"DO{i}",
                    AccessRight = "ReadWrite",
                    DataType = 1,
                    PortMask = 0x0F,
                    Description = $"Digital Output {i}",
                    IsAccessRightLocked = false
                });
            }

            for (ushort i = 0; i < 8; i++)
            {
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTagIndex = (ushort)(20 + i),
                    TargetTagName = $"VFLAG{i}",
                    AccessRight = "ReadWrite",
                    DataType = 1,
                    PortMask = 0x0F,
                    Description = $"Virtual Flag {i}",
                    IsAccessRightLocked = false
                });
            }

            for (ushort i = 0; i < 4; i++)
            {
                SlaveMapEntries.Add(new ModbusSlaveMapItemViewModel
                {
                    ModbusAddress = curAddr++,
                    TargetTagIndex = (ushort)(52 + i),
                    TargetTagName = $"VREG{i}",
                    AccessRight = "ReadWrite",
                    DataType = 2,
                    PortMask = 0x0F,
                    Description = $"Virtual Register {i}",
                    IsAccessRightLocked = false
                });
            }
        }
    }

    [RelayCommand]
    public void AddSlaveMapEntry()
    {
        if (SlaveMapEntries.Count >= ModbusRegisterMap.ModbusSlaveMapMaxEntries)
        {
            return;
        }

        ushort nextAddr = 0;
        if (SlaveMapEntries.Count > 0)
        {
            nextAddr = (ushort)(SlaveMapEntries.Max(e => e.ModbusAddress) + 1);
        }

        var newEntry = new ModbusSlaveMapItemViewModel
        {
            ModbusAddress = nextAddr,
            TargetTagIndex = 0,
            TargetTagName = "DI0",
            AccessRight = "ReadOnly",
            DataType = 1,
            PortMask = 0x0F,
            Description = $"Custom Mapping #{SlaveMapEntries.Count + 1}",
            IsAccessRightLocked = true,
            LockReason = "Physical Input locked to Read-Only."
        };

        SlaveMapEntries.Add(newEntry);
        SelectedSlaveMapEntry = newEntry;
    }

    [RelayCommand]
    public void RemoveSlaveMapEntry(ModbusSlaveMapItemViewModel? entry)
    {
        var target = entry ?? SelectedSlaveMapEntry;
        if (target != null)
        {
            SlaveMapEntries.Remove(target);
        }
    }

    [RelayCommand]
    public void ClearSlaveMapEntries()
    {
        SlaveMapEntries.Clear();
    }

    /// <summary>
    /// Xuất danh sách thanh ghi Modbus Slave theo phân vùng đang xem ra định dạng CSV.
    /// </summary>
    /// <returns>Chuỗi định dạng CSV của bản đồ thanh ghi.</returns>
    public string GenerateExportModbusMapCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Category,Hex Address,Modbus Address (1-based),Device Tag,Data Type,Access Right,Modbus Function,Description");
        foreach (var entry in FilteredFixedRegisterMap)
        {
            sb.AppendLine($"\"{entry.Category}\",{entry.AddressHex},{entry.ModbusAddress},{entry.DeviceTag},{entry.DataType},{entry.AccessRight},\"{entry.FunctionCode}\",\"{entry.Description}\"");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Xuất danh sách thanh ghi Modbus Slave theo phân vùng đang xem ra định dạng bảng Markdown.
    /// </summary>
    /// <returns>Chuỗi định dạng Markdown của bản đồ thanh ghi.</returns>
    public string GenerateExportModbusMapMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SynaptiX Controller - Modbus Slave Fixed Memory Map");
        sb.AppendLine();
        sb.AppendLine("| Category | Hex Address | Modbus Address | Device Tag | Data Type | Access | Function | Description |");
        sb.AppendLine("|:---|:---:|:---:|:---|:---:|:---:|:---:|:---|");
        foreach (var entry in FilteredFixedRegisterMap)
        {
            sb.AppendLine($"| {entry.Category} | `{entry.AddressHex}` | `{entry.ModbusAddress}` | **{entry.DeviceTag}** | `{entry.DataType}` | `{entry.AccessRight}` | `{entry.FunctionCode}` | {entry.Description} |");
        }
        return sb.ToString();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLastExportedFilePath))]
    private string? _lastExportedFilePath;

    public bool HasLastExportedFilePath => !string.IsNullOrWhiteSpace(LastExportedFilePath) && File.Exists(LastExportedFilePath);

    /// <summary>
    /// Hàm ủy nhiệm hiển thị hộp thoại lưu file (cho phép thay thế trong Unit Test).
    /// </summary>
    public Func<string, string, string?>? SaveFilePicker { get; set; } = (filter, defaultFileName) =>
    {
        // 1. Chỉ định rõ ràng Owner Window đang active để Win32 gắn trực tiếp, tránh xung đột modal message loop
        var owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                 ?? System.Windows.Application.Current?.MainWindow;

        // 2. Cố định thư mục cục bộ nhanh (Documents/Desktop), tránh bị Windows Shell quét chậm các ổ đĩa mạng/Cloud
        string defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(defaultDir) || !Directory.Exists(defaultDir))
        {
            defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            InitialDirectory = defaultDir,
            RestoreDirectory = true,
            AddExtension = true,
            ValidateNames = true,
            DereferenceLinks = true,
            Title = LocalizationService.Instance.IsVietnamese 
                ? "Lưu bảng ánh xạ Modbus Slave" 
                : "Save Modbus Slave Mapping Table"
        };

        if (owner != null)
        {
            return sfd.ShowDialog(owner) == true ? sfd.FileName : null;
        }

        return sfd.ShowDialog() == true ? sfd.FileName : null;
    };

    [RelayCommand]
    public void OpenExportFolder()
    {
        if (string.IsNullOrWhiteSpace(LastExportedFilePath) || !File.Exists(LastExportedFilePath)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{LastExportedFilePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    public async Task ExportDocumentationAsync()
    {
        await ExportModbusMapAsync();
    }

    [RelayCommand]
    public async Task ExportModbusMapAsync()
    {
        if (FixedRegisterMap == null || FixedRegisterMap.Count == 0)
        {
            ExportNoticeMessage = LocalizationService.Instance.IsVietnamese
                ? "Bảng chưa có dữ liệu để xuất file."
                : "No register entries available to export.";
            return;
        }

        try
        {
            string portSuffix = SelectedPort != null ? $"Port{SelectedPort.PortIndex + 1}" : "Port1";
            string defaultName = $"SynaptiX_ModbusSlaveMap_{portSuffix}.csv";
            string filter = LocalizationService.Instance.IsVietnamese
                ? "Bảng tính CSV (Excel / SCADA) (*.csv)|*.csv|Tài liệu Markdown (*.md)|*.md|Tất cả tệp (*.*)|*.*"
                : "CSV Spreadsheet (Excel / SCADA) (*.csv)|*.csv|Markdown Document (*.md)|*.md|All Files (*.*)|*.*";

            string? filePath = SaveFilePicker?.Invoke(filter, defaultName);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return; // Người dùng ấn Hủy (Cancel)
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            // Thực hiện ghi file bất đồng bộ trên Background Task để UI luôn mượt 60fps
            if (ext == ".md")
            {
                string md = GenerateExportModbusMapMarkdown();
                await Task.Run(() => File.WriteAllText(filePath, md, new UTF8Encoding(true)));
            }
            else
            {
                string csv = GenerateExportModbusMapCsv();
                await Task.Run(() => File.WriteAllText(filePath, csv, new UTF8Encoding(true)));
            }

            LastExportedFilePath = filePath;
            ExportNoticeMessage = LocalizationService.Instance.IsVietnamese
                ? $"✓ Đã lưu thành công ra file: {Path.GetFileName(filePath)}"
                : $"✓ Successfully saved to: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            ExportNoticeMessage = (LocalizationService.Instance.IsVietnamese ? "Lỗi lưu file: " : "Error saving file: ") + ex.Message;
        }
    }

    [RelayCommand]
    public void Apply()
    {
        for (int i = 0; i < Ports.Count && i < _metadata.ModbusPorts.Count; i++)
        {
            Ports[i].CopyToModel(_metadata.ModbusPorts[i]);
        }

        _metadata.ModbusSlaveMapEntries = SlaveMapEntries.Select(e => new ModbusSlaveMapEntryModel
        {
            ModbusAddress = e.ModbusAddress,
            TargetTagIndex = e.TargetTagIndex,
            AccessRight = e.AccessRight,
            DataType = e.DataType,
            PortMask = e.PortMask,
            Description = e.Description
        }).ToList();

        _metadata.LastModified = DateTime.UtcNow;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        RequestClose?.Invoke();
    }


    private int? _hardwarePortLimitOverride;

    /// <summary>
    /// Đồng bộ giới hạn số lượng cổng RS-485 vật lý thực tế từ MCU khi kết nối thiết bị.
    /// Truyền null khi ngắt kết nối để phục hồi giới hạn theo ProjectMetadata.
    /// </summary>
    /// <param name="portLimit">Số cổng RS-485 vật lý báo từ MCU, hoặc null khi offline.</param>
    public void SyncHardwarePortLimit(int? portLimit)
    {
        _hardwarePortLimitOverride = portLimit;
        ReloadPortsFromMetadata();
    }

    /// <summary>
    /// Tải lại danh sách cổng RS-485 từ ProjectMetadata (Single Source of Truth).
    /// </summary>
    public void ReloadPortsFromMetadata()
    {
        int physicalLimit = _hardwarePortLimitOverride ?? _metadata.PhysicalRs485PortCount;
        if (physicalLimit > 4) physicalLimit = 4;
        if (physicalLimit < 0) physicalLimit = 0;

        if (_metadata.ModbusPorts == null || _metadata.ModbusPorts.Count == 0)
        {
            _metadata.ModbusPorts = new List<ModbusPortConfigModel>();
            for (int i = 0; i < physicalLimit; i++)
            {
                _metadata.ModbusPorts.Add(new ModbusPortConfigModel
                {
                    PortIndex = (byte)i,
                    PortName = $"RS-485 #{i + 1}",
                    Enabled = i < 2,
                    Role = "Master",
                    SlaveId = 1,
                    BaudRate = i == 1 ? 19200u : 9600u,
                    DataBits = 8,
                    Parity = i == 1 ? "Even" : "None",
                    StopBits = 1,
                    ResponseTimeoutMs = 200,
                    InterFrameDelayMs = 20,
                    RetryCount = 1
                });
            }
        }
        else
        {
            while (_metadata.ModbusPorts.Count < physicalLimit)
            {
                int idx = _metadata.ModbusPorts.Count;
                _metadata.ModbusPorts.Add(new ModbusPortConfigModel
                {
                    PortIndex = (byte)idx,
                    PortName = $"RS-485 #{idx + 1}",
                    Enabled = false,
                    Role = "Master",
                    SlaveId = 1,
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = "None",
                    StopBits = 1,
                    ResponseTimeoutMs = 200,
                    InterFrameDelayMs = 20,
                    RetryCount = 1
                });
            }
        }

        byte prevSelectedIndex = SelectedPort?.PortIndex ?? 0;
        Ports.Clear();
        for (int i = 0; i < physicalLimit && i < _metadata.ModbusPorts.Count; i++)
        {
            Ports.Add(new ModbusPortItemViewModel(_metadata.ModbusPorts[i], TagCatalog));
        }

        HasPhysicalPorts = Ports.Count > 0;
        SelectedPort = Ports.FirstOrDefault(p => p.PortIndex == prevSelectedIndex) ?? Ports.FirstOrDefault();
    }

    /// <summary>
    /// Thêm một kênh truyền Modbus Master mới vào cổng đang chọn.
    /// </summary>
    [RelayCommand]
    public void AddMasterChannel()
    {
        SelectedPort?.AddMasterChannel();
    }

    /// <summary>
    /// Xóa một kênh truyền Modbus Master khỏi cổng đang chọn.
    /// </summary>
    /// <param name="channel">Kênh Modbus Master cần xóa.</param>
    [RelayCommand]
    public void RemoveMasterChannel(ModbusMasterChannelItemViewModel? channel)
    {
        SelectedPort?.RemoveMasterChannel(channel);
    }

    [RelayCommand]
    public void ResetDefaults()
    {
        if (SelectedPort != null)
        {
            SelectedPort.Role = "Master";
            SelectedPort.SlaveId = 1;
            SelectedPort.BaudRate = SelectedPort.PortIndex == 1 ? 19200u : 9600u;
            SelectedPort.Parity = SelectedPort.PortIndex == 1 ? "Even" : "None";
            SelectedPort.StopBits = 1;
            SelectedPort.DataBits = 8;
            SelectedPort.ResponseTimeoutMs = 200;
            SelectedPort.InterFrameDelayMs = 20;
            SelectedPort.RetryCount = 1;
            SelectedPort.PortName = $"RS-485 #{SelectedPort.PortIndex + 1}";
            OnPropertyChanged(nameof(IsSlaveViewActive));
        }
    }
}
