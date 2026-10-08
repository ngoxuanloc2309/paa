using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Application.Enums;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Model hiển thị của từng dòng trong Live Watch Table.
/// Chịu trách nhiệm format hiển thị chuẩn công nghiệp (TIA Portal / Codesys),
/// theo dõi trạng thái giá trị trực tiếp và thông số vận hành của từng Tag.
/// </summary>
public partial class WatchTagItemModel : ObservableObject
{
    private long _totalSum;
    private long _sampleCount;

    public ushort Index { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Alias { get; init; }
    public TagKind Kind { get; init; }
    public TagDataType DataType { get; init; }

    public ushort ModbusAddress => (ushort)(0x0900 + Index * 2);
    public string ModbusAddressHex => $"0x{ModbusAddress:X4}";
    public string ModbusAddressFull => $"0x{ModbusAddress:X4} (Dec: {ModbusAddress})";

    public string DataTypeText => DataType switch
    {
        TagDataType.Boolean => "BOOL",
        TagDataType.Int32 => "INT32",
        _ => DataType.ToString().ToUpperInvariant()
    };

    public bool IsReadOnly => Kind is TagKind.DiscreteInput or TagKind.AnalogInput;
    public bool IsForceable => !IsReadOnly && Kind != TagKind.ModbusCoil && Kind != TagKind.ModbusHolding;

    public string AccessModeText
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            if (IsReadOnly)
                return isVi ? "Chỉ đọc (Read-Only)" : "Read-Only";
            return isVi ? "Đọc / Ghi (Read / Write)" : "Read / Write";
        }
    }

    public string KindText => Kind switch
    {
        TagKind.DiscreteInput => "DI",
        TagKind.DiscreteOutput => "DO",
        TagKind.AnalogInput => "AI",
        TagKind.VirtualFlag => "VFLAG",
        TagKind.VirtualRegister => "VREG",
        TagKind.VirtualRegisterRetain => "VREG_R",
        TagKind.Counter => "COUNTER",
        TagKind.ModbusCoil => "MB_COIL",
        TagKind.ModbusHolding => "MB_HOLDING",
        _ => "TAG"
    };

    public string KindFullName
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            return Kind switch
            {
                TagKind.DiscreteInput => isVi ? "Ngõ vào số (DI)" : "Digital Input (DI)",
                TagKind.DiscreteOutput => isVi ? "Ngõ ra số (DO)" : "Digital Output (DO)",
                TagKind.AnalogInput => isVi ? "Ngõ vào tương tự (AI)" : "Analog Input (AI)",
                TagKind.VirtualFlag => isVi ? "Cờ ảo (VFLAG)" : "Virtual Flag (VFLAG)",
                TagKind.VirtualRegister => isVi ? "Thanh ghi RAM (VREG)" : "RAM Register (VREG)",
                TagKind.VirtualRegisterRetain => isVi ? "Thanh ghi lưu Flash (VREG_R)" : "Flash Retain Register (VREG_R)",
                TagKind.Counter => isVi ? "Bộ đếm sự kiện (COUNTER)" : "Event Counter (COUNTER)",
                _ => Kind.ToString()
            };
        }
    }

    public string KindBadgeColor => Kind switch
    {
        TagKind.DiscreteInput => "#006487",
        TagKind.DiscreteOutput => "#107C41",
        TagKind.AnalogInput => "#C05621",
        TagKind.VirtualFlag => "#475569",
        TagKind.VirtualRegister => "#006487",
        TagKind.VirtualRegisterRetain => "#C05621",
        TagKind.Counter => "#475569",
        _ => "#64748B"
    };

    public string LocalizedAlias
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            if (!string.IsNullOrWhiteSpace(Alias))
            {
                if (Alias.StartsWith("Digital Input ", StringComparison.OrdinalIgnoreCase))
                    return isVi ? $"Ngõ vào số {Index}" : Alias;
                if (Alias.StartsWith("Digital Output ", StringComparison.OrdinalIgnoreCase))
                    return isVi ? $"Ngõ ra số {Index - 8}" : Alias;
                if (Alias.StartsWith("Analog Input ", StringComparison.OrdinalIgnoreCase))
                    return isVi ? $"Ngõ vào tương tự {Index - 16}" : Alias;
                if (Alias.StartsWith("Virtual Flag ", StringComparison.OrdinalIgnoreCase))
                    return isVi ? $"Cờ ảo {Index - 20}" : Alias;
                if (Alias.StartsWith("Virtual Register ", StringComparison.OrdinalIgnoreCase))
                    return isVi ? $"Thanh ghi RAM {Index - 52}" : Alias;
                if (Alias.StartsWith("Retain Virtual Register ", StringComparison.OrdinalIgnoreCase))
                    return isVi ? $"Thanh ghi lưu Flash {Index - 84}" : Alias;
                if (Alias.StartsWith("Counter ", StringComparison.OrdinalIgnoreCase))
                    return isVi ? $"Bộ đếm {Index - 116}" : Alias;
                return Alias;
            }

            return isVi ? $"{KindFullName} #{Index}" : $"{KindText} #{Index}";
        }
    }

    public string DisplayName => $"{Name} · {LocalizedAlias}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    [NotifyPropertyChangedFor(nameof(FormattedDisplayValue))]
    [NotifyPropertyChangedFor(nameof(BooleanDisplayValue))]
    [NotifyPropertyChangedFor(nameof(RawHexText))]
    [NotifyPropertyChangedFor(nameof(IsOn))]
    private int _rawValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(QualityText))]
    [NotifyPropertyChangedFor(nameof(BooleanDisplayValue))]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    [NotifyPropertyChangedFor(nameof(FormattedDisplayValue))]
    private TagQuality _quality = TagQuality.Unknown;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LastUpdatedText))]
    private DateTimeOffset _lastUpdated = DateTimeOffset.MinValue;

    [ObservableProperty]
    private bool _isModifiedRecently;

    [ObservableProperty]
    private int _minValue = int.MaxValue;

    [ObservableProperty]
    private int _maxValue = int.MinValue;

    [ObservableProperty]
    private int _transitionCount;

    [ObservableProperty]
    private double _averageValue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BooleanDisplayValue))]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    [NotifyPropertyChangedFor(nameof(FormattedDisplayValue))]
    private bool _isForced;

    [ObservableProperty]
    private int? _forcedValue;

    [ObservableProperty]
    private string _manualInputValue = "0";

    [ObservableProperty]
    private bool _isPinned;

    public bool IsBoolean => DataType == TagDataType.Boolean;
    public bool IsOn => IsBoolean && RawValue != 0;

    public string BooleanDisplayValue
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            string prefix = IsForced ? "[F] " : "";
            string lastSuffix = Quality != TagQuality.Good ? (isVi ? " [Gần nhất]" : " [Last]") : "";
            if (IsOn)
            {
                return prefix + (isVi ? "● BẬT (1)" : "● ON (1)") + lastSuffix;
            }
            return prefix + (isVi ? "○ TẮT (0)" : "○ OFF (0)") + lastSuffix;
        }
    }

    public string FormattedValue
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            string prefix = IsForced ? "[F] " : "";
            string lastSuffix = Quality != TagQuality.Good ? (isVi ? " [Gần nhất]" : " [Last]") : "";
            string val = DataType switch
            {
                TagDataType.Boolean => RawValue != 0 ? "ON" : "OFF",
                _ => RawValue.ToString("N0")
            };
            return prefix + val + lastSuffix;
        }
    }

    public string FormattedDisplayValue
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            string prefix = IsForced ? "[F] " : "";
            string lastSuffix = Quality != TagQuality.Good ? (isVi ? " [Gần nhất]" : " [Last]") : "";
            if (DataType == TagDataType.Boolean)
            {
                return prefix + (RawValue != 0 ? (isVi ? "BẬT" : "ON") : (isVi ? "TẮT" : "OFF")) + lastSuffix;
            }
            return prefix + RawValue.ToString("N0") + lastSuffix;
        }
    }

    public string RawHexText => $"0x{RawValue:X8}";

    public string QualityText
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            return Quality switch
            {
                TagQuality.Good => isVi ? "TỐT" : "GOOD",
                TagQuality.Stale => isVi ? "CŨ" : "STALE",
                _ => isVi ? "MẤT TÍN HIỆU" : "UNKNOWN"
            };
        }
    }

    public string LastUpdatedText => LastUpdated == DateTimeOffset.MinValue
        ? "--"
        : LastUpdated.ToLocalTime().ToString("HH:mm:ss.fff");

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(LocalizedAlias));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(KindText));
        OnPropertyChanged(nameof(KindFullName));
        OnPropertyChanged(nameof(FormattedValue));
        OnPropertyChanged(nameof(BooleanDisplayValue));
        OnPropertyChanged(nameof(QualityText));
        OnPropertyChanged(nameof(AccessModeText));
    }

    public void UpdateFromSnapshot(RuntimeTagSnapshot snapshot)
    {
        if (RawValue != snapshot.RawValue)
        {
            IsModifiedRecently = true;
            TransitionCount++;
        }

        RawValue = snapshot.RawValue;
        Quality = snapshot.Quality;
        LastUpdated = snapshot.LastSuccessfulUpdateAt;

        // Cập nhật thống kê phiên
        if (RawValue < MinValue) MinValue = RawValue;
        if (RawValue > MaxValue) MaxValue = RawValue;
        _totalSum += RawValue;
        _sampleCount++;
        AverageValue = _sampleCount > 0 ? (double)_totalSum / _sampleCount : RawValue;
    }

    public void ApplyManualOverride(int value)
    {
        IsForced = true;
        ForcedValue = value;
        if (RawValue != value)
        {
            IsModifiedRecently = true;
            TransitionCount++;
        }
        RawValue = value;
        LastUpdated = DateTimeOffset.UtcNow;
    }

    public void ClearManualOverride()
    {
        IsForced = false;
        ForcedValue = null;
    }
}
