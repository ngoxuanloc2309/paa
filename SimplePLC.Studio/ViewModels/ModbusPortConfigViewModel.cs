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
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadge))]
    private ushort _retryCount = 1;

    public bool IsMasterMode => string.Equals(Role, "Master", StringComparison.OrdinalIgnoreCase);
    public bool IsSlaveMode => string.Equals(Role, "Slave", StringComparison.OrdinalIgnoreCase);

    public string DisplayName =>
        (LocalizationService.Instance.IsVietnamese ? "Cổng " : "Port ") + $"{PortIndex + 1}: {PortName}";

    public void NotifyLanguageChanged()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(StatusPill));
        OnPropertyChanged(nameof(SummaryBadge));
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

    public ModbusPortItemViewModel(ModbusPortConfigModel model)
    {
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

        int physicalLimit = _metadata.PhysicalRs485PortCount;
        if (physicalLimit > 4) physicalLimit = 4;

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

        for (int i = 0; i < physicalLimit && i < _metadata.ModbusPorts.Count; i++)
        {
            Ports.Add(new ModbusPortItemViewModel(_metadata.ModbusPorts[i]));
        }

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
