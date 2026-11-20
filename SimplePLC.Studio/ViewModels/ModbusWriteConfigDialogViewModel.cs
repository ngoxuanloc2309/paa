using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Phần tử đại diện cho một kênh chấp hành trong bảng ánh xạ địa chỉ Modbus (Sort-to-Light / Cụm van / Tháp đèn).
/// </summary>
public class ActuatorChannelMappingItem
{
    public ushort ChannelIndex { get; set; }
    public string ChannelDisplay => $"CH {ChannelIndex:D2}";
    public string CommandRegHex { get; set; } = string.Empty;
    public string ParamRegHex { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Mục phân tích một trường trong khung tin Modbus RTU (Slave ID, FC, Addr, Data, CRC).
/// </summary>
public class ModbusFrameFieldBreakdown
{
    public string FieldName { get; set; } = string.Empty;
    public string RawHex { get; set; } = string.Empty;
    public string Interpretation { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình khối MB_WRITE (Modbus Master Write).
/// Hỗ trợ 2 chế độ:
/// 1. Channel-Indexed Target (Sort-to-Light, Cụm van, Tháp đèn) với bảng ánh xạ địa chỉ và tùy chọn Exclusive.
/// 2. Direct Registers (Ghi trực tiếp thanh ghi Modbus truyền thống FC 05, 06, 15, 16).
/// Cung cấp giao diện kỹ thuật công nghiệp chuẩn Slate Gray, phân tích khung tin Modbus RTU rõ ràng.
/// </summary>
public partial class ModbusWriteConfigDialogViewModel : ObservableObject
{
    public ModbusWriteNodeViewModel Node { get; }
    public TagCatalogViewModel TagCatalog { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(PortSelectorTooltip))]
    [NotifyPropertyChangedFor(nameof(HasRestrictedIdError))]
    [NotifyPropertyChangedFor(nameof(RestrictedIdErrorMessage))]
    [NotifyPropertyChangedFor(nameof(CanImportSystemChannels))]
    [NotifyPropertyChangedFor(nameof(HasPortRoleWarning))]
    [NotifyPropertyChangedFor(nameof(PortRoleWarningMessage))]
    [NotifyPropertyChangedFor(nameof(AvailableSystemWriteChannelsCount))]
    [NotifyPropertyChangedFor(nameof(ImportSystemChannelsText))]
    private byte _portIndex;

    partial void OnPortIndexChanged(byte value)
    {
        if (_modbusPorts != null)
        {
            var p = _modbusPorts.FirstOrDefault(x => x.PortIndex == value);
            if (p != null && p.ResponseTimeoutMs > 0)
            {
                TimeoutMs = p.ResponseTimeoutMs;
            }
        }
        foreach (var pin in Node.Pins)
        {
            pin.RefreshTagOptions();
        }
        OnPropertyChanged(nameof(CanImportSystemChannels));
        OnPropertyChanged(nameof(HasPortRoleWarning));
        OnPropertyChanged(nameof(PortRoleWarningMessage));
        OnPropertyChanged(nameof(AvailableSystemWriteChannelsCount));
        OnPropertyChanged(nameof(ImportSystemChannelsText));
    }

    /// <summary>
    /// Cho biết cổng hiện tại có bị cấu hình sai vai trò (ví dụ: đang là Slave hoặc bị tắt) ở Tab System hay không.
    /// Khối MB_WRITE (Master) không thể truyền lệnh trên cổng Slave.
    /// </summary>
    public bool HasPortRoleWarning
    {
        get
        {
            if (_modbusPorts == null) return false;
            var p = _modbusPorts.FirstOrDefault(x => x.PortIndex == PortIndex);
            return p != null && (string.Equals(p.Role, "Slave", StringComparison.OrdinalIgnoreCase) || !p.Enabled);
        }
    }

    /// <summary>
    /// Thông báo cảnh báo vai trò cổng chi tiết cho kỹ sư vận hành.
    /// </summary>
    public string PortRoleWarningMessage
    {
        get
        {
            if (_modbusPorts == null) return string.Empty;
            var p = _modbusPorts.FirstOrDefault(x => x.PortIndex == PortIndex);
            if (p == null) return string.Empty;
            bool isVi = LocalizationService.Instance.IsVietnamese;
            if (!p.Enabled)
            {
                return isVi
                    ? $"⚠ Cổng {p.PortName} hiện đang bị vô hiệu hóa (Disabled) trong Tab System. Khối MB_WRITE sẽ không thể truyền dữ liệu."
                    : $"⚠ Port {p.PortName} is currently disabled in System Tab. MB_WRITE block will not transmit.";
            }
            if (string.Equals(p.Role, "Slave", StringComparison.OrdinalIgnoreCase))
            {
                return isVi
                    ? $"⚠ Cổng {p.PortName} đang cấu hình là Modbus RTU Slave (Server). Khối MB_WRITE (Master) không thể phát lệnh ghi trên cổng Slave. Vui lòng chuyển vai trò sang Master."
                    : $"⚠ Port {p.PortName} is configured as Modbus RTU Slave (Server). MB_WRITE (Master) cannot initiate write commands on a Slave port. Please set role to Master.";
            }
            return string.Empty;
        }
    }

    /// <summary>
    /// Số lượng kênh Modbus Write (FC05, 06, 15, 16) đã khai báo sẵn ở System Tab trên cổng hiện tại.
    /// </summary>
    public int AvailableSystemWriteChannelsCount
    {
        get
        {
            if (_modbusPorts == null || WriteMode != ModbusWriteMode.DirectRegisters) return 0;
            var port = _modbusPorts.FirstOrDefault(p => p.PortIndex == PortIndex);
            return port?.MasterChannels?.Count(c => c.FunctionCode is 5 or 6 or 15 or 16) ?? 0;
        }
    }

    /// <summary>
    /// Chuỗi văn bản hiển thị trên nút nhập kênh từ System kèm số lượng kênh khả dụng.
    /// </summary>
    public string ImportSystemChannelsText
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            int count = AvailableSystemWriteChannelsCount;
            return isVi 
                ? $"📥 Nhập kênh từ System ({count})" 
                : $"📥 Import from System ({count})";
        }
    }

    /// <summary>
    /// Cho biết trên cổng hiện tại ở System Tab có kênh Modbus Write (FC05, 06, 15, 16) nào để nhập nhanh hay không.
    /// </summary>
    public bool CanImportSystemChannels
    {
        get
        {
            if (_modbusPorts == null || WriteMode != ModbusWriteMode.DirectRegisters) return false;
            var port = _modbusPorts.FirstOrDefault(p => p.PortIndex == PortIndex);
            return port?.MasterChannels != null && port.MasterChannels.Any(c => c.FunctionCode is 5 or 6 or 15 or 16);
        }
    }

    /// <summary>
    /// Nhập nhanh toàn bộ các kênh Modbus Write đã khai báo ở System Tab trên cổng này vào bảng Pins.
    /// </summary>
    [RelayCommand]
    public void ImportSystemChannels()
    {
        if (_modbusPorts == null || WriteMode != ModbusWriteMode.DirectRegisters) return;
        var port = _modbusPorts.FirstOrDefault(p => p.PortIndex == PortIndex);
        if (port?.MasterChannels == null) return;

        var writeChannels = port.MasterChannels
            .Where(c => c.FunctionCode is 5 or 6 or 15 or 16)
            .OrderBy(c => c.Address)
            .ToList();

        if (writeChannels.Count == 0) return;

        var first = writeChannels[0];
        SlaveId = first.SlaveId == 0 ? (byte)1 : first.SlaveId;
        FunctionCode = first.FunctionCode switch
        {
            5 => ModbusMasterWriteFunctionCode.WriteSingleCoil,
            15 => ModbusMasterWriteFunctionCode.WriteMultipleCoils,
            16 => ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            _ => ModbusMasterWriteFunctionCode.WriteSingleRegister
        };

        int baseWire = Services.Modbus.ModbusChannelSyncService.ToWireOffset(first.Address, first.FunctionCode);
        StartAddress = (ushort)baseWire;

        foreach (var p in Node.Pins)
        {
            p.PropertyChanged -= OnPinPropertyChanged;
        }
        Node.Pins.Clear();

        foreach (var ch in writeChannels)
        {
            int chWire = Services.Modbus.ModbusChannelSyncService.ToWireOffset(ch.Address, ch.FunctionCode);
            int offset = Math.Max(0, chWire - baseWire);

            string pinLabel = !string.IsNullOrWhiteSpace(ch.TargetTagAlias)
                ? ch.TargetTagAlias
                : (!string.IsNullOrWhiteSpace(ch.Description)
                    ? ch.Description
                    : (!string.IsNullOrWhiteSpace(ch.TargetTagName) ? ch.TargetTagName : $"In_{Node.Pins.Count + 1}"));

            var pin = new ModbusPinBindingViewModel
            {
                Label = pinLabel,
                RegOffset = (ushort)offset,
                DataType = ch.DataType,
                Endianness = ch.Endianness,
                Multiplier = (float)(ch.Scale == 0 ? 1.0 : ch.Scale)
            };
            ConfigurePinTagProvider(pin);
            if (!string.IsNullOrWhiteSpace(ch.TargetTagName) && TagCatalog?.AllTags != null)
            {
                pin.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => string.Equals(t.Name, ch.TargetTagName, StringComparison.OrdinalIgnoreCase));
            }
            if (pin.TargetTag == null && TagCatalog?.AllTags != null)
            {
                pin.TargetTag = TagCatalog.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None);
            }
            pin.PropertyChanged += OnPinPropertyChanged;
            Node.Pins.Add(pin);
        }

        PointCount = (ushort)Math.Clamp(Node.Pins.Count, 1, 16);
        OnPropertyChanged(nameof(SummaryBadgeText));
        OnPropertyChanged(nameof(HasPins));
        OnPropertyChanged(nameof(HasNoPins));
        OnPropertyChanged(nameof(CanImportSystemChannels));
        GenerateFramePreview();
    }

    public ObservableCollection<OptionItem<byte>> AvailablePorts { get; } = new();

    /// <summary>
    /// Cho biết có ít nhất 1 cổng khả dụng (đã kích hoạt) để lựa chọn hay không.
    /// </summary>
    public bool HasAvailablePorts => AvailablePorts.Count > 0;

    /// <summary>
    /// Cho biết không có cổng nào được kích hoạt trong dự án.
    /// </summary>
    public bool HasNoAvailablePorts => AvailablePorts.Count == 0;

    /// <summary>
    /// Thông báo hiển thị khi chưa có cổng nào được kích hoạt.
    /// </summary>
    public string NoPortsAvailableMessage => LocalizationService.Tr("ModbusNoMasterPortsEnabled")
        ?? (LocalizationService.Instance.IsVietnamese ? "Chưa kích hoạt cổng RS-485 Master" : "No RS-485 Master Ports Enabled");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(HasRestrictedIdError))]
    [NotifyPropertyChangedFor(nameof(RestrictedIdErrorMessage))]
    private byte _slaveId;

    /// <summary>
    /// Cho biết có lỗi chọn Slave ID cấm (236 hoặc 237) khi trỏ tới cổng vô tuyến Zigbee hay không.
    /// </summary>
    public bool HasRestrictedIdError => PortIndex == 4 && (SlaveId == 236 || SlaveId == 237);

    /// <summary>
    /// Thông báo cảnh báo lỗi khi nhập Slave ID 236/237 trên cổng Zigbee.
    /// </summary>
    public string RestrictedIdErrorMessage => LocalizationService.Tr("ModbusZigbeeRestrictedIdError")
        ?? (LocalizationService.Instance.IsVietnamese
            ? "Cấm sử dụng Slave ID 236 (0xEC) và 237 (0xED) trên cổng Zigbee vì trùng mã lệnh cấu hình module RF."
            : "Slave IDs 236 (0xEC) and 237 (0xED) are restricted on Zigbee port due to RF module AT commands.");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsChannelIndexedMode))]
    [NotifyPropertyChangedFor(nameof(IsDirectRegistersMode))]
    private ModbusWriteMode _writeMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsMultipleMode))]
    private ModbusMasterWriteFunctionCode _functionCode;

    public bool IsMultipleMode => FunctionCode == ModbusMasterWriteFunctionCode.WriteMultipleRegisters ||
                                  FunctionCode == ModbusMasterWriteFunctionCode.WriteMultipleCoils;

    // Direct mode settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(StartAddressModiconPreview))]
    private ushort _startAddress;

    /// <summary>
    /// Hiển thị địa chỉ Modicon 1-based tương ứng để khớp hoàn toàn với bảng System Tab (vd: 40001 khi offset là 0).
    /// </summary>
    public string StartAddressModiconPreview =>
        $"= {Services.Modbus.ModbusChannelSyncService.ToModiconAddress(StartAddress, (int)FunctionCode)}";

    partial void OnFunctionCodeChanged(ModbusMasterWriteFunctionCode value)
    {
        OnPropertyChanged(nameof(StartAddressModiconPreview));
        OnPropertyChanged(nameof(SummaryBadgeText));
        OnPropertyChanged(nameof(IsMultipleMode));
        OnPropertyChanged(nameof(CanAddPin));
        OnPropertyChanged(nameof(AddPinTooltip));

        if (!IsMultipleMode)
        {
            PointCount = 1;
            // Modbus Rule: FC05 / FC06 writes strictly 1 entity.
            // If user previously had multiple pins, trim down to the first pin to prevent invalid wire frames.
            if (Node.Pins.Count > 1)
            {
                var extraPins = Node.Pins.Skip(1).ToList();
                foreach (var extra in extraPins)
                {
                    extra.PropertyChanged -= OnPinPropertyChanged;
                    Node.Pins.Remove(extra);
                }
                OnPropertyChanged(nameof(HasPins));
                OnPropertyChanged(nameof(HasNoPins));
            }
        }
        else
        {
            UpdatePointCountFromPins();
        }

        // Auto-normalize pin data types when switching between Coils and Registers
        if (value is ModbusMasterWriteFunctionCode.WriteSingleCoil or ModbusMasterWriteFunctionCode.WriteMultipleCoils)
        {
            foreach (var pin in Node.Pins)
            {
                if (pin.DataType != ModbusDataType.Bool)
                {
                    pin.DataType = ModbusDataType.Bool;
                }
            }
        }
        else
        {
            foreach (var pin in Node.Pins)
            {
                if (pin.DataType == ModbusDataType.Bool)
                {
                    pin.DataType = ModbusDataType.Int16;
                }
            }
        }

        foreach (var pin in Node.Pins)
        {
            pin.RefreshTagOptions();
        }
        GenerateFramePreview();
    }

    /// <summary>
    /// Cho biết có thể thêm chân Pin mới hay không (chỉ khả dụng ở chế độ đa điểm FC15/FC16 và số chân &lt; 8).
    /// </summary>
    public bool CanAddPin => WriteMode == ModbusWriteMode.DirectRegisters
        && (FunctionCode is ModbusMasterWriteFunctionCode.WriteMultipleRegisters or ModbusMasterWriteFunctionCode.WriteMultipleCoils)
        && Node.Pins.Count < 8;

    /// <summary>
    /// Chú thích hướng dẫn trên nút Add Pin khi bị vô hiệu hóa do chuẩn Modbus FC05/FC06.
    /// </summary>
    public string AddPinTooltip
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            if (FunctionCode is ModbusMasterWriteFunctionCode.WriteSingleRegister or ModbusMasterWriteFunctionCode.WriteSingleCoil)
            {
                return isVi
                    ? "Mã hàm FC 05 / FC 06 chỉ cho phép ghi 1 điểm đơn. Vui lòng chuyển sang FC 15 hoặc FC 16 để ghi nhiều chân."
                    : "Function FC 05 / FC 06 only supports a single point. Switch to FC 15 or FC 16 to write multiple pins.";
            }
            if (Node.Pins.Count >= 8)
            {
                return isVi
                    ? "Đã đạt giới hạn tối đa 8 chân Pin cho một khối ghi."
                    : "Maximum limit of 8 pins reached for this write block.";
            }
            return isVi ? "Thêm chân dữ liệu ghi vào khối" : "Add write register pin";
        }
    }

    /// <summary>
    /// Tự động cập nhật PointCount dựa trên tổng số Word / Coils chiếm dụng của các chân Pin.
    /// </summary>
    private void UpdatePointCountFromPins()
    {
        if (WriteMode != ModbusWriteMode.DirectRegisters) return;

        if (FunctionCode is ModbusMasterWriteFunctionCode.WriteSingleRegister or ModbusMasterWriteFunctionCode.WriteSingleCoil)
        {
            PointCount = 1;
            return;
        }

        if (FunctionCode == ModbusMasterWriteFunctionCode.WriteMultipleRegisters)
        {
            ushort totalWords = 0;
            foreach (var p in Node.Pins)
            {
                int span = (p.DataType is ModbusDataType.Real or ModbusDataType.Int32 or ModbusDataType.UInt32) ? 2 : 1;
                totalWords += (ushort)span;
            }
            PointCount = Math.Max((ushort)1, totalWords);
        }
        else if (FunctionCode == ModbusMasterWriteFunctionCode.WriteMultipleCoils)
        {
            PointCount = Math.Max((ushort)1, (ushort)Node.Pins.Count);
        }
    }

    [ObservableProperty]
    private ushort _pointCount;

    // Channel-Indexed mode settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ushort _baseAddressCommand;

    [ObservableProperty]
    private ushort _baseAddressParam;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ushort _channelCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private bool _isExclusiveChannel;

    // Trigger & Timing
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCyclicMode))]
    [NotifyPropertyChangedFor(nameof(IsOnDataChangeMode))]
    [NotifyPropertyChangedFor(nameof(IsTriggerEdgeMode))]
    [NotifyPropertyChangedFor(nameof(HasTriggerParameter))]
    private ModbusWriteTriggerMode _triggerMode;

    [ObservableProperty]
    private uint _cyclicIntervalMs;

    [ObservableProperty]
    private uint _timeoutMs;

    [ObservableProperty]
    private float _deadband = 0.5f;

    public bool IsCyclicMode => TriggerMode == ModbusWriteTriggerMode.Cyclic;
    public bool IsOnDataChangeMode => TriggerMode == ModbusWriteTriggerMode.OnDataChange;
    public bool IsTriggerEdgeMode => TriggerMode == ModbusWriteTriggerMode.OnTriggerRisingEdge;
    public bool HasTriggerParameter => TriggerMode == ModbusWriteTriggerMode.Cyclic || TriggerMode == ModbusWriteTriggerMode.OnDataChange;

    public bool IsChannelIndexedMode => WriteMode == ModbusWriteMode.ChannelIndexed;
    public bool IsDirectRegistersMode => WriteMode == ModbusWriteMode.DirectRegisters;

    public string SummaryBadgeText => GetSummaryBadgeText();
    private string GetSummaryBadgeText()
    {
        string portText = PortIndex == 4 ? "Zigbee" : $"{(LocalizationService.Instance.IsVietnamese ? "Cổng " : "Port ")}{PortIndex + 1}";
        if (WriteMode == ModbusWriteMode.ChannelIndexed)
        {
            return portText + " | " + $"Slave {SlaveId} | {ChannelCount} Channels | BaseCmd 0x{BaseAddressCommand:X4}{(IsExclusiveChannel ? " [Exclusive]" : "")}";
        }

        string unit = (FunctionCode is ModbusMasterWriteFunctionCode.WriteSingleCoil or ModbusMasterWriteFunctionCode.WriteMultipleCoils) ? "Coils" : "Regs";
        return $"{portText} | Slave {SlaveId} | FC{(byte)FunctionCode:D2} | Addr {StartAddress} ({Node.Pins.Count} Pins, {PointCount} {unit})";
    }




    public bool HasPins => Node.Pins.Count > 0;
    public bool HasNoPins => Node.Pins.Count == 0;

    // Actuator Channel Mappings Table (Channel-Indexed mode)
    public ObservableCollection<ActuatorChannelMappingItem> ChannelMappings { get; } = new();

    // Frame Preview & Simulator Fields
    [ObservableProperty]
    private ushort _simChannel = 1;

    [ObservableProperty]
    private ushort _simCommand = 1;

    [ObservableProperty]
    private string _simParamHex = "00FF00"; // Mặc định: Green (Xanh lá)

    [ObservableProperty]
    private ushort _simDirectValue = 100;

    [ObservableProperty]
    private string _simStatusMessage = string.Empty;

    [ObservableProperty]
    private string _simTxFrameHex = string.Empty;

    [ObservableProperty]
    private string _simParamTxFrameHex = string.Empty;

    [ObservableProperty]
    private bool _hasParamFrame = false;

    [ObservableProperty]
    private string _simGeneratedFrames = string.Empty;

    [ObservableProperty]
    private string _simExplanation = string.Empty;

    public ObservableCollection<ModbusFrameFieldBreakdown> FrameBreakdownItems { get; } = new();

    public ObservableCollection<OptionItem<ModbusWriteMode>> AvailableWriteModes { get; } = new();
    public ObservableCollection<OptionItem<ModbusMasterWriteFunctionCode>> AvailableFunctionCodes { get; } = new();
    public ObservableCollection<OptionItem<ModbusWriteTriggerMode>> AvailableTriggerModes { get; } = new();

    public IReadOnlyList<OptionItem<ModbusDataType>> AvailableDataTypes { get; } = new List<OptionItem<ModbusDataType>>
    {
        new(ModbusDataType.Int16, "INT16 (Signed)"),
        new(ModbusDataType.UInt16, "UINT16 (Unsigned)"),
        new(ModbusDataType.Real, "REAL (Float32)"),
        new(ModbusDataType.Int32, "INT32 (Signed)"),
        new(ModbusDataType.UInt32, "UINT32 (Unsigned)"),
        new(ModbusDataType.Bool, "BOOL (0 / 1)")
    };

    public IReadOnlyList<OptionItem<ModbusEndianness>> AvailableEndianness { get; } = new List<OptionItem<ModbusEndianness>>
    {
        new(ModbusEndianness.BigEndian_ABCD, "Big-Endian (ABCD)"),
        new(ModbusEndianness.MidBig_CDAB, "Word-Swap (CDAB)"),
        new(ModbusEndianness.ByteSwap_BADC, "Byte-Swap (BADC)"),
        new(ModbusEndianness.LittleEndian_DCBA, "Little-Endian (DCBA)")
    };

    private IReadOnlyList<SimplePLC.Studio.Models.ModbusPortConfigModel>? _modbusPorts;
    private SimplePLC.Studio.Models.ZigbeeConfigModel? _zigbeeConfig;

    public Action? OnOpenDevicePortSettings { get; set; }

    [RelayCommand]
    public void OpenDevicePortSettings()
    {
        OnOpenDevicePortSettings?.Invoke();
    }

    public void RefreshAvailablePorts(
        IEnumerable<SimplePLC.Studio.Models.ModbusPortConfigModel>? newPorts,
        SimplePLC.Studio.Models.ZigbeeConfigModel? zigbeeConfig = null)
    {
        _modbusPorts = newPorts?.ToList();
        if (zigbeeConfig != null) _zigbeeConfig = zigbeeConfig;
        UpdatePortOptions();
    }

    public ModbusWriteConfigDialogViewModel(
        ModbusWriteNodeViewModel node, 
        TagCatalogViewModel tagCatalog,
        IEnumerable<SimplePLC.Studio.Models.ModbusPortConfigModel>? availablePorts = null,
        SimplePLC.Studio.Models.ZigbeeConfigModel? zigbeeConfig = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        TagCatalog = tagCatalog;
        _modbusPorts = availablePorts?.ToList();
        _zigbeeConfig = zigbeeConfig;

        _portIndex = node.PortIndex;
        _slaveId = node.SlaveId;
        _writeMode = node.WriteMode;
        _functionCode = node.FunctionCode;
        _startAddress = node.StartAddress;
        _pointCount = Math.Clamp(node.PointCount == 0 ? (ushort)1 : node.PointCount, (ushort)1, (ushort)16);
        _baseAddressCommand = node.BaseAddressCommand;
        _baseAddressParam = node.BaseAddressParam;
        _channelCount = Math.Clamp(node.ChannelCount == 0 ? (ushort)1 : node.ChannelCount, (ushort)1, (ushort)64);
        _isExclusiveChannel = node.IsExclusiveChannel;
        _triggerMode = node.TriggerMode;
        _cyclicIntervalMs = node.CyclicIntervalMs;

        // Kế thừa Timeout từ cấu hình cổng vật lý ở System Tab (Axiom 1: Hardware Ground Truth)
        var currentPort = _modbusPorts?.FirstOrDefault(p => p.PortIndex == _portIndex);
        if (currentPort != null && currentPort.ResponseTimeoutMs > 0)
        {
            _timeoutMs = currentPort.ResponseTimeoutMs;
        }
        else
        {
            _timeoutMs = node.TimeoutMs > 0 ? node.TimeoutMs : (uint)200;
        }

        _deadband = node.Deadband;

        foreach (var pin in node.Pins)
        {
            ConfigurePinTagProvider(pin);
            if (pin.TargetTag == null && tagCatalog?.AllTags != null)
            {
                pin.TargetTag = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None);
            }
            pin.PropertyChanged += OnPinPropertyChanged;
        }

        if (_writeMode == ModbusWriteMode.DirectRegisters && node.Pins.Count == 0)
        {
            AddPin();
        }

        PopulateLocalizedOptions();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;

        UpdateChannelMappings();
        GenerateFramePreview();
    }

    public void PopulateLocalizedOptions()
    {
        UpdatePortOptions();
        UpdateWriteModes();
        UpdateFunctionCodes();
        UpdateTriggerModes();
    }

    private void UpdatePortOptions()
    {
        var curPort = PortIndex;
        bool isVi = LocalizationService.Instance.IsVietnamese;
        string prefix = isVi ? "Cổng" : "Port";

        int physicalLimit = AppServices.Instance.CurrentProduct?.PhysicalRs485PortCount ?? 4;
        if (_modbusPorts != null && _modbusPorts.Count > 0)
        {
            int maxProvided = _modbusPorts.Where(p => p.Enabled).Select(p => (int)p.PortIndex + 1).DefaultIfEmpty(0).Max();
            if (maxProvided > physicalLimit && (AppServices.Instance.CurrentProduct == null || AppServices.Instance.CurrentProduct.PhysicalRs485PortCount == 2))
            {
                physicalLimit = Math.Min(4, Math.Max(physicalLimit, maxProvided));
            }
        }
        if (physicalLimit > 4) physicalLimit = 4;
        if (physicalLimit < 1) physicalLimit = 1;

        var targetList = new List<OptionItem<byte>>();

        if (_modbusPorts != null && _modbusPorts.Count > 0)
        {
            var selectablePorts = _modbusPorts
                .Where(p => p.Enabled && p.PortIndex < physicalLimit && !string.Equals(p.Role, "Slave", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (selectablePorts.Count > 0)
            {
                foreach (var p in selectablePorts)
                {
                    targetList.Add(new OptionItem<byte>(p.PortIndex, p.FormatDropdownLabel(isVi)));
                }

                if (!selectablePorts.Any(p => p.PortIndex == curPort))
                {
                    curPort = selectablePorts[0].PortIndex;
                    PortIndex = curPort;
                }
            }
        }
        else
        {
            for (byte i = 0; i < physicalLimit; i++)
            {
                targetList.Add(new OptionItem<byte>(i, $"{prefix} {i + 1} (RS-485 #{i + 1})"));
            }
        }

        // Bổ sung cổng Zigbee Wireless (PortIndex = 4) nếu được kích hoạt
        if (_zigbeeConfig != null && _zigbeeConfig.Enabled)
        {
            string zigbeeLabel = isVi
                ? $"🌐 Zigbee Wireless (Kênh {_zigbeeConfig.Channel} · PAN 0x{_zigbeeConfig.PanIdHex})"
                : $"🌐 Zigbee Wireless (Ch {_zigbeeConfig.Channel} · PAN 0x{_zigbeeConfig.PanIdHex})";
            targetList.Add(new OptionItem<byte>(4, zigbeeLabel));
        }

        SyncOptions(AvailablePorts, targetList);

        if (!AvailablePorts.Any(p => p.Value == curPort) && AvailablePorts.Count > 0)
        {
            PortIndex = AvailablePorts[0].Value;
        }
        else
        {
            OnPropertyChanged(nameof(PortIndex));
        }

        OnPropertyChanged(nameof(HasAvailablePorts));
        OnPropertyChanged(nameof(HasNoAvailablePorts));
        OnPropertyChanged(nameof(NoPortsAvailableMessage));
        OnPropertyChanged(nameof(PortSelectorTooltip));
        OnPropertyChanged(nameof(SummaryBadgeText));
    }

    private void UpdateWriteModes()
    {
        var targetModes = new List<OptionItem<ModbusWriteMode>>
        {
            new(ModbusWriteMode.ChannelIndexed, LocalizationService.Tr("ModbusWriteModeIndexed") ?? "Channel-Indexed Target"),
            new(ModbusWriteMode.DirectRegisters, LocalizationService.Tr("ModbusWriteModeDirect") ?? "Direct Registers")
        };

        SyncOptions(AvailableWriteModes, targetModes);
        OnPropertyChanged(nameof(WriteMode));
    }

    private void UpdateFunctionCodes()
    {
        var targetFc = new List<OptionItem<ModbusMasterWriteFunctionCode>>
        {
            new(ModbusMasterWriteFunctionCode.WriteSingleRegister, LocalizationService.Tr("ModbusWriteFc06") ?? "FC 06: Write Register (4x)"),
            new(ModbusMasterWriteFunctionCode.WriteMultipleRegisters, LocalizationService.Tr("ModbusWriteFc16") ?? "FC 16: Write Multiple (4x)"),
            new(ModbusMasterWriteFunctionCode.WriteSingleCoil, LocalizationService.Tr("ModbusWriteFc05") ?? "FC 05: Write Coil (0x)"),
            new(ModbusMasterWriteFunctionCode.WriteMultipleCoils, LocalizationService.Tr("ModbusWriteFc15") ?? "FC 15: Write Multiple Coils (0x)")
        };

        SyncOptions(AvailableFunctionCodes, targetFc);
        OnPropertyChanged(nameof(FunctionCode));
    }

    private void UpdateTriggerModes()
    {
        var targetTrig = new List<OptionItem<ModbusWriteTriggerMode>>
        {
            new(ModbusWriteTriggerMode.OnTriggerRisingEdge, LocalizationService.Tr("ModbusWriteTrigEdge") ?? "Rising Edge (Trig)"),
            new(ModbusWriteTriggerMode.OnDataChange, LocalizationService.Tr("ModbusWriteTrigChange") ?? "On Data Change"),
            new(ModbusWriteTriggerMode.Cyclic, LocalizationService.Tr("ModbusWriteTrigCyclic") ?? "Cyclic (Periodic)")
        };

        SyncOptions(AvailableTriggerModes, targetTrig);
        OnPropertyChanged(nameof(TriggerMode));
    }

    private static void SyncOptions<T>(ObservableCollection<OptionItem<T>> current, IList<OptionItem<T>> target)
    {
        if (current.SequenceEqual(target)) return;

        for (int i = 0; i < target.Count; i++)
        {
            if (i < current.Count)
            {
                if (!EqualityComparer<OptionItem<T>>.Default.Equals(current[i], target[i]))
                {
                    current[i] = target[i];
                }
            }
            else
            {
                current.Add(target[i]);
            }
        }

        while (current.Count > target.Count)
        {
            current.RemoveAt(current.Count - 1);
        }
    }

    public string PortSelectorTooltip
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            if (_modbusPorts != null && !_modbusPorts.Any(p => p.Enabled))
            {
                return LocalizationService.Tr("ModbusNoPortsEnabledTooltip") 
                    ?? (isVi ? "Chưa có cổng RS-485 nào được kích hoạt trên thiết bị. Vui lòng vào Cài đặt > Cấu hình Cổng RS-485 để kích hoạt ít nhất 1 cổng." : "No RS-485 ports are currently enabled. Please open Settings > RS-485 Modbus Master Ports to enable at least one port.");
            }

            if (PortIndex == 4)
            {
                if (_zigbeeConfig != null && !_zigbeeConfig.Enabled)
                {
                    return isVi
                        ? "Phân hệ vô tuyến Zigbee đang bị VÔ HIỆU HÓA trong cấu hình thiết bị. Vui lòng vào Cấu hình Thiết bị > Zigbee Wireless để kích hoạt."
                        : "Zigbee wireless subsystem is DISABLED in device settings. Please open Device Configuration > Zigbee Wireless to enable.";
                }
                return LocalizationService.Tr("ModbusPortZigbeeTooltip")
                    ?? (isVi ? "Truyền nhận dữ liệu qua sóng vô tuyến Zigbee Mesh 2.4 GHz (IEEE 802.15.4) thay cho cáp RS-485 vật lý." : "Transmit and receive data over 2.4 GHz IEEE 802.15.4 Zigbee wireless mesh instead of RS-485 cable.");
            }

            if (_modbusPorts != null && _modbusPorts.Count > 0)
            {
                var curPortObj = _modbusPorts.FirstOrDefault(p => p.PortIndex == PortIndex);
                if (curPortObj != null)
                {
                    if (!curPortObj.Enabled)
                    {
                        return (isVi ? "Cổng này đang bị VÔ HIỆU HÓA trong cấu hình thiết bị: " : "This port is DISABLED in device settings: ") + curPortObj.FormatFullSummary(isVi, isForceDisabledWarning: true);
                    }
                    return curPortObj.FormatFullSummary(isVi);
                }
            }

            return (isVi ? "Cổng truyền thông RS-485 #" : "RS-485 Communication Port #") + (PortIndex + 1);
        }
    }

    private void OnLanguageChanged()
    {
        PopulateLocalizedOptions();
        OnPropertyChanged(nameof(SummaryBadgeText));
        OnPropertyChanged(nameof(PortSelectorTooltip));
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    private void OnPinPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ModbusPinBindingViewModel.DataType))
        {
            UpdatePointCountFromPins();
            OnPropertyChanged(nameof(CanAddPin));
            OnPropertyChanged(nameof(AddPinTooltip));
        }
        OnPropertyChanged(nameof(SummaryBadgeText));
        GenerateFramePreview();
    }

    partial void OnSimDirectValueChanged(ushort value)
    {
        GenerateFramePreview();
    }

    partial void OnWriteModeChanged(ModbusWriteMode value)
    {
        if (value == ModbusWriteMode.DirectRegisters && Node.Pins.Count == 0)
        {
            AddPin();
        }
        OnPropertyChanged(nameof(IsChannelIndexedMode));
        OnPropertyChanged(nameof(IsDirectRegistersMode));
        OnPropertyChanged(nameof(SummaryBadgeText));
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnChannelCountChanged(ushort value)
    {
        if (value > 64)
        {
            ChannelCount = 64;
            return;
        }
        if (value < 1)
        {
            ChannelCount = 1;
            return;
        }
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnBaseAddressCommandChanged(ushort value)
    {
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnBaseAddressParamChanged(ushort value)
    {
        UpdateChannelMappings();
        GenerateFramePreview();
    }

    partial void OnSlaveIdChanged(byte value)
    {
        GenerateFramePreview();
    }

    partial void OnStartAddressChanged(ushort value)
    {
        foreach (var pin in Node.Pins)
        {
            pin.RefreshTagOptions();
        }
        GenerateFramePreview();
    }

    partial void OnPointCountChanged(ushort value)
    {
        if (value > 16)
        {
            PointCount = 16;
            return;
        }
        if (value < 1)
        {
            PointCount = 1;
            return;
        }
        GenerateFramePreview();
    }

    public void UpdateChannelMappings()
    {
        ChannelMappings.Clear();
        int count = Math.Min((int)ChannelCount, 32);
        bool isVi = LocalizationService.Instance.IsVietnamese;
        for (int i = 1; i <= count; i++)
        {
            ushort cmdAddr = (ushort)(BaseAddressCommand + i - 1);
            ushort paramAddr = (ushort)(BaseAddressParam + (i - 1) * 2);
            ChannelMappings.Add(new ActuatorChannelMappingItem
            {
                ChannelIndex = (ushort)i,
                CommandRegHex = $"0x{cmdAddr:X4} ({cmdAddr})",
                ParamRegHex = $"0x{paramAddr:X4} ({paramAddr})",
                Description = isVi ? $"Kênh điều khiển {i}" : $"Actuator Channel {i}"
            });
        }
    }

    [RelayCommand]
    public void AddPin()
    {
        if (!CanAddPin && Node.Pins.Count > 0) return;

        ushort nextOffset = 0;
        if (Node.Pins.Count > 0)
        {
            var last = Node.Pins.Last();
            int span = (last.DataType is ModbusDataType.Real or ModbusDataType.Int32 or ModbusDataType.UInt32) ? 2 : 1;
            nextOffset = (ushort)(last.RegOffset + span);
        }

        var defaultType = (FunctionCode is ModbusMasterWriteFunctionCode.WriteSingleCoil or ModbusMasterWriteFunctionCode.WriteMultipleCoils)
            ? ModbusDataType.Bool
            : ModbusDataType.Int16;

        var newPin = new ModbusPinBindingViewModel
        {
            Label = $"In_{Node.Pins.Count + 1}",
            RegOffset = nextOffset,
            DataType = defaultType,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Multiplier = 1.0f,
            TargetTag = TagCatalog?.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None)
        };
        ConfigurePinTagProvider(newPin);
        newPin.PropertyChanged += OnPinPropertyChanged;
        Node.Pins.Add(newPin);
        UpdatePointCountFromPins();
        OnPropertyChanged(nameof(CanAddPin));
        OnPropertyChanged(nameof(AddPinTooltip));
        OnPropertyChanged(nameof(SummaryBadgeText));
        OnPropertyChanged(nameof(HasPins));
        OnPropertyChanged(nameof(HasNoPins));
        GenerateFramePreview();
    }

    /// <summary>
    /// Cấu hình hàm cung cấp Tag tương thích cho một chân Pin dựa trên TagCatalog và liên kết System Tab.
    /// Đối với Modbus Write, các Tag nguồn cung cấp dữ liệu có thể là Tag bất kỳ (Read/Write/Memory).
    /// </summary>
    public void ConfigurePinTagProvider(ModbusPinBindingViewModel pin)
    {
        pin.TagProvider = GetCompatibleTagsForDataType;
        pin.SystemChannelsProvider = () =>
        {
            var p = _modbusPorts?.FirstOrDefault(x => x.PortIndex == PortIndex);
            return p?.MasterChannels ?? Enumerable.Empty<SimplePLC.Studio.Models.ModbusMasterChannelModel>();
        };
        pin.PeerPinsProvider = () => Node.Pins;
        pin.BaseAddressProvider = () => StartAddress;
        pin.FunctionCodeProvider = () => (byte)FunctionCode;
        pin.RefreshTagOptions();
    }

    /// <summary>
    /// Lấy danh sách các Tag tương thích theo FunctionCode và kiểu dữ liệu Modbus từ TagCatalog.
    /// </summary>
    public IEnumerable<TagModel> GetCompatibleTagsForDataType(ModbusDataType dataType)
    {
        if (dataType == ModbusDataType.Bool)
        {
            return TagCatalog.GetFilteredTags(TagAccessRole.ReadSource, ModbusDataType.Bool, includeNone: true);
        }
        else
        {
            return TagCatalog.GetFilteredTags(TagAccessRole.ReadSource, dataType, includeNone: true)
                .Where(t => t.Kind == TagKind.None || (!t.IsDigital && t.DataType != SimplePLC.Domain.Enums.TagDataType.Boolean));
        }
    }

    [RelayCommand]
    public void RemovePin(ModbusPinBindingViewModel pin)
    {
        if (pin != null && Node.Pins.Contains(pin))
        {
            pin.PropertyChanged -= OnPinPropertyChanged;
            Node.Pins.Remove(pin);
            UpdatePointCountFromPins();
            OnPropertyChanged(nameof(CanAddPin));
            OnPropertyChanged(nameof(AddPinTooltip));
            OnPropertyChanged(nameof(SummaryBadgeText));
            OnPropertyChanged(nameof(HasPins));
            OnPropertyChanged(nameof(HasNoPins));
            GenerateFramePreview();
        }
    }

    [RelayCommand]
    public void GenerateFramePreview()
    {
        bool isVi = LocalizationService.Instance.IsVietnamese;
        FrameBreakdownItems.Clear();

        if (WriteMode == ModbusWriteMode.ChannelIndexed)
        {
            ushort ch = Math.Clamp(SimChannel, (ushort)1, Math.Max((ushort)1, ChannelCount));
            ushort cmd = SimCommand;
            
            // Clean simParamHex
            string paramHex = (SimParamHex ?? string.Empty).Replace(" ", "").Replace("0x", "").Trim();
            uint paramVal = 0;
            if (!uint.TryParse(paramHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out paramVal))
            {
                paramVal = 0;
            }

            ushort cmdRegAddr = (ushort)(BaseAddressCommand + ch - 1);
            ushort paramRegAddr = (ushort)(BaseAddressParam + ((ch - 1) * 2));

            var frames = new List<string>();

            // Frame 1: Write Single Register FC06 for Command
            byte[] cmdFrame = CreateFc06Frame(SlaveId, cmdRegAddr, cmd);
            string cmdFrameHex = FormatHex(cmdFrame);
            frames.Add(isVi 
                ? $"1. Gói lệnh điều khiển FC06 (FC 06) -> Thanh ghi 0x{cmdRegAddr:X4} (Giá trị = {cmd}):" 
                : $"1. Command Telegram FC06 (FC 06) -> Register 0x{cmdRegAddr:X4} (Value = {cmd}):");
            frames.Add(isVi ? $"   Khung truyền: {cmdFrameHex}" : $"   TX Frame: {cmdFrameHex}");
            SimTxFrameHex = cmdFrameHex;

            // Frame 2: Write Multiple Registers FC16 for Param (RGB 24-bit / 32-bit: 2 registers)
            ushort regHigh = (ushort)((paramVal >> 16) & 0xFFFF);
            ushort regLow = (ushort)(paramVal & 0xFFFF);
            byte[] paramFrame = CreateFc16Frame(SlaveId, paramRegAddr, new[] { regHigh, regLow });
            string paramFrameHex = FormatHex(paramFrame);
            frames.Add(isVi 
                ? $"2. Gói tham số/màu FC16 (FC 16) -> Thanh ghi 0x{paramRegAddr:X4} (Giá trị = 0x{paramVal:X6}):" 
                : $"2. Parameter Telegram FC16 (FC 16) -> Register 0x{paramRegAddr:X4} (Value = 0x{paramVal:X6}):");
            frames.Add(isVi ? $"   Khung truyền: {paramFrameHex}" : $"   TX Frame: {paramFrameHex}");
            SimParamTxFrameHex = paramFrameHex;
            HasParamFrame = true;

            SimGeneratedFrames = string.Join(Environment.NewLine, frames);
            SimStatusMessage = LocalizationService.Tr("ModbusWriteStatusSuccess") ?? "Valid Modbus RTU frame (CRC-16 verified)";

            // Breakdown
            ushort crcCmd = (ushort)(cmdFrame[6] | (cmdFrame[7] << 8));
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Địa chỉ thiết bị" : "Device Address",
                RawHex = $"{SlaveId:X2}",
                Interpretation = isVi ? $"Thiết bị số {SlaveId}" : $"Device {SlaveId}",
                Tooltip = LocalizationService.Tr("ModbusWriteBreakdownSlaveIdTooltip") ?? string.Empty
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Mã hàm" : "Function Code",
                RawHex = "06",
                Interpretation = isVi ? "FC 06: Ghi một thanh ghi" : "FC 06: Write Single Register",
                Tooltip = LocalizationService.Tr("ModbusWriteBreakdownFcTooltip") ?? string.Empty
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Thanh ghi lệnh" : "Command Register",
                RawHex = $"{cmdFrame[2]:X2} {cmdFrame[3]:X2}",
                Interpretation = isVi ? $"0x{cmdRegAddr:X4} (Kênh {ch})" : $"0x{cmdRegAddr:X4} (Channel {ch})",
                Tooltip = LocalizationService.Tr("ModbusWriteBreakdownCmdRegTooltip") ?? string.Empty
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Dữ liệu lệnh" : "Command Value",
                RawHex = $"{cmdFrame[4]:X2} {cmdFrame[5]:X2}",
                Interpretation = isVi ? $"Giá trị = {cmd}" : $"Value = {cmd}",
                Tooltip = LocalizationService.Tr("ModbusWriteBreakdownCmdValTooltip") ?? string.Empty
            });
            FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
            {
                FieldName = isVi ? "Mã kiểm tra" : "CRC Checksum",
                RawHex = $"{cmdFrame[6]:X2} {cmdFrame[7]:X2}",
                Interpretation = isVi ? $"0x{crcCmd:X4} (Chính xác)" : $"0x{crcCmd:X4} (Valid)",
                Tooltip = LocalizationService.Tr("ModbusWriteBreakdownCrcTooltip") ?? string.Empty
            });

            SimExplanation = isVi
                ? $"✔ Chế độ điều khiển theo chỉ số kênh (Kênh {ch}/{ChannelCount}):" + Environment.NewLine +
                  $"• Lệnh Command={cmd} ghi vào thanh ghi 0x{cmdRegAddr:X4}." + Environment.NewLine +
                  $"• Tham số Param=0x{paramVal:X6} ghi vào thanh ghi 0x{paramRegAddr:X4} (2 thanh ghi RGB/DWord)." + Environment.NewLine +
                  (IsExclusiveChannel 
                      ? $"• Chế độ chọn một (Exclusive): Tự động xóa các kênh khác (1..{ch - 1}, {ch + 1}..{ChannelCount}) về 0." 
                      : "• Chế độ đa kênh độc lập (Independent): Các kênh hoạt động riêng biệt, không tự ngắt lẫn nhau.")
                : $"✔ Channel-Indexed Target Mode (Channel {ch}/{ChannelCount}):" + Environment.NewLine +
                  $"• Command={cmd} written to register 0x{cmdRegAddr:X4}." + Environment.NewLine +
                  $"• Param=0x{paramVal:X6} written to register 0x{paramRegAddr:X4} (2 registers RGB/DWord)." + Environment.NewLine +
                  (IsExclusiveChannel 
                      ? $"• Exclusive Mode: Automatically clears other channels (1..{ch - 1}, {ch + 1}..{ChannelCount}) to 0." 
                      : "• Independent Mode: Channels operate independently without clearing other channels.");
        }
        else
        {
            // Direct Registers Mode
            var frames = new List<string>();
            byte[] frame;
            ushort directVal = SimDirectValue;

            switch (FunctionCode)
            {
                case ModbusMasterWriteFunctionCode.WriteSingleRegister:
                    frame = CreateFc06Frame(SlaveId, StartAddress, directVal);
                    frames.Add(isVi 
                        ? $"Gói ghi một thanh ghi FC06 (FC 06) -> Addr 0x{StartAddress:X4} (Giá trị = {directVal} / 0x{directVal:X4}):" 
                        : $"FC06 Write Single Register (FC 06) -> Addr 0x{StartAddress:X4} (Value = {directVal} / 0x{directVal:X4}):");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                case ModbusMasterWriteFunctionCode.WriteMultipleRegisters:
                    ushort count = Math.Max((ushort)1, PointCount);
                    var sampleVals = Enumerable.Range(0, count).Select(i => (ushort)(directVal + i * 10)).ToArray();
                    frame = CreateFc16Frame(SlaveId, StartAddress, sampleVals);
                    frames.Add(isVi 
                        ? $"Gói ghi nhiều thanh ghi FC16 (FC 16) -> Addr 0x{StartAddress:X4}, Count={count} (Giá trị bắt đầu = {directVal}):" 
                        : $"FC16 Write Multiple Registers (FC 16) -> Addr 0x{StartAddress:X4}, Count={count} (Starting Value = {directVal}):");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                case ModbusMasterWriteFunctionCode.WriteSingleCoil:
                    bool coilState = directVal > 0;
                    frame = CreateFc05Frame(SlaveId, StartAddress, coilState);
                    frames.Add(isVi 
                        ? $"Gói ghi một cuộn dây FC05 (FC 05) -> Addr 0x{StartAddress:X4} (Trạng thái = {(coilState ? "BẬT / ON" : "TẮT / OFF")}):" 
                        : $"FC05 Write Single Coil (FC 05) -> Addr 0x{StartAddress:X4} (State = {(coilState ? "ON" : "OFF")}):");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                case ModbusMasterWriteFunctionCode.WriteMultipleCoils:
                    ushort coilCount = Math.Max((ushort)1, PointCount);
                    byte byteCount = (byte)((coilCount + 7) / 8);
                    byte[] coilBytes = new byte[byteCount];
                    if (directVal > 0) coilBytes[0] = (byte)(directVal & 0xFF);
                    frame = CreateFc15Frame(SlaveId, StartAddress, coilCount, coilBytes);
                    frames.Add(isVi 
                        ? $"Gói ghi nhiều cuộn dây FC15 (FC 15) -> Addr 0x{StartAddress:X4}, Count={coilCount}:" 
                        : $"FC15 Write Multiple Coils (FC 15) -> Addr 0x{StartAddress:X4}, Count={coilCount}:");
                    frames.Add(isVi ? $"Khung truyền: {FormatHex(frame)}" : $"TX Frame: {FormatHex(frame)}");
                    SimTxFrameHex = FormatHex(frame);
                    break;

                default:
                    frame = Array.Empty<byte>();
                    break;
            }

            HasParamFrame = false;
            SimParamTxFrameHex = string.Empty;
            SimGeneratedFrames = string.Join(Environment.NewLine, frames);
            SimStatusMessage = LocalizationService.Tr("ModbusWriteStatusSuccess") ?? "Valid Modbus RTU frame (CRC-16 verified)";

            if (frame.Length >= 6)
            {
                int crcIdx = frame.Length - 2;
                ushort crc = (ushort)(frame[crcIdx] | (frame[crcIdx + 1] << 8));
                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Địa chỉ thiết bị" : "Device Address",
                    RawHex = $"{SlaveId:X2}",
                    Interpretation = isVi ? $"Thiết bị số {SlaveId}" : $"Device {SlaveId}",
                    Tooltip = LocalizationService.Tr("ModbusWriteBreakdownSlaveIdTooltip") ?? string.Empty
                });
                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Mã hàm" : "Function Code",
                    RawHex = $"{frame[1]:X2}",
                    Interpretation = $"FC {(byte)FunctionCode:D2}",
                    Tooltip = LocalizationService.Tr("ModbusWriteBreakdownFcTooltip") ?? string.Empty
                });
                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Địa chỉ bắt đầu" : "Start Address",
                    RawHex = $"{frame[2]:X2} {frame[3]:X2}",
                    Interpretation = $"0x{StartAddress:X4} ({StartAddress})",
                    Tooltip = LocalizationService.Tr("ModbusWriteBreakdownStartAddrTooltip") ?? string.Empty
                });

                // Field Breakdown chi tiết cho giá trị ghi
                switch (FunctionCode)
                {
                    case ModbusMasterWriteFunctionCode.WriteSingleRegister:
                        FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                        {
                            FieldName = isVi ? "Giá trị ghi" : "Register Value",
                            RawHex = $"{frame[4]:X2} {frame[5]:X2}",
                            Interpretation = $"{directVal} (0x{directVal:X4})",
                            Tooltip = LocalizationService.Tr("ModbusWriteBreakdownValueTooltip") ?? string.Empty
                        });
                        break;

                    case ModbusMasterWriteFunctionCode.WriteSingleCoil:
                        FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                        {
                            FieldName = isVi ? "Lệnh cuộn dây" : "Coil Command",
                            RawHex = $"{frame[4]:X2} {frame[5]:X2}",
                            Interpretation = (directVal > 0) ? "0xFF00 (BẬT / ON)" : "0x0000 (TẮT / OFF)",
                            Tooltip = LocalizationService.Tr("ModbusWriteBreakdownValueTooltip") ?? string.Empty
                        });
                        break;

                    case ModbusMasterWriteFunctionCode.WriteMultipleRegisters:
                        FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                        {
                            FieldName = isVi ? "Số thanh ghi" : "Register Count",
                            RawHex = $"{frame[4]:X2} {frame[5]:X2}",
                            Interpretation = isVi ? $"{Math.Max((ushort)1, PointCount)} thanh ghi" : $"{Math.Max((ushort)1, PointCount)} registers",
                            Tooltip = LocalizationService.Tr("ModbusWriteBreakdownCountTooltip") ?? string.Empty
                        });
                        FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                        {
                            FieldName = isVi ? "Số bytes dữ liệu" : "Byte Count",
                            RawHex = $"{frame[6]:X2}",
                            Interpretation = isVi ? $"{frame[6]} bytes" : $"{frame[6]} bytes",
                            Tooltip = LocalizationService.Tr("ModbusWriteBreakdownByteCountTooltip") ?? string.Empty
                        });
                        FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                        {
                            FieldName = isVi ? "Dữ liệu mẫu" : "Payload Data",
                            RawHex = string.Join(" ", frame.Skip(7).Take(frame[6]).Select(b => b.ToString("X2"))),
                            Interpretation = isVi ? $"Bắt đầu = {directVal}" : $"Start = {directVal}",
                            Tooltip = LocalizationService.Tr("ModbusWriteBreakdownPayloadTooltip") ?? string.Empty
                        });
                        break;

                    case ModbusMasterWriteFunctionCode.WriteMultipleCoils:
                        FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                        {
                            FieldName = isVi ? "Số cuộn dây" : "Coil Count",
                            RawHex = $"{frame[4]:X2} {frame[5]:X2}",
                            Interpretation = isVi ? $"{Math.Max((ushort)1, PointCount)} cuộn" : $"{Math.Max((ushort)1, PointCount)} coils",
                            Tooltip = LocalizationService.Tr("ModbusWriteBreakdownCountTooltip") ?? string.Empty
                        });
                        break;
                }

                FrameBreakdownItems.Add(new ModbusFrameFieldBreakdown
                {
                    FieldName = isVi ? "Mã kiểm tra" : "CRC Checksum",
                    RawHex = $"{frame[crcIdx]:X2} {frame[crcIdx + 1]:X2}",
                    Interpretation = isVi ? $"0x{crc:X4} (Chính xác)" : $"0x{crc:X4} (Valid)",
                    Tooltip = LocalizationService.Tr("ModbusWriteBreakdownCrcTooltip") ?? string.Empty
                });
            }

            SimExplanation = isVi
                ? $"✔ Chế độ ghi trực tiếp thanh ghi: Khối ghi dữ liệu của {Node.Pins.Count} chân vào thiết bị {SlaveId} bằng hàm FC {(byte)FunctionCode:D2}."
                : $"✔ Direct Registers Mode: The block writes data from {Node.Pins.Count} data pins directly to Device {SlaveId} via FC {(byte)FunctionCode:D2}.";
        }
    }

    private static byte[] CreateFc06Frame(byte slaveId, ushort address, ushort value)
    {
        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x06;
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((value >> 8) & 0xFF);
        frame[5] = (byte)(value & 0xFF);

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] CreateFc16Frame(byte slaveId, ushort address, ushort[] values)
    {
        byte byteCount = (byte)(values.Length * 2);
        byte[] frame = new byte[7 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = 0x10; // FC 16
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((values.Length >> 8) & 0xFF);
        frame[5] = (byte)(values.Length & 0xFF);
        frame[6] = byteCount;

        for (int i = 0; i < values.Length; i++)
        {
            frame[7 + i * 2] = (byte)((values[i] >> 8) & 0xFF);
            frame[7 + i * 2 + 1] = (byte)(values[i] & 0xFF);
        }

        int payloadLen = 7 + byteCount;
        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, payloadLen));
        frame[payloadLen] = (byte)(crc & 0xFF);
        frame[payloadLen + 1] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] CreateFc05Frame(byte slaveId, ushort address, bool state)
    {
        byte[] frame = new byte[8];
        frame[0] = slaveId;
        frame[1] = 0x05;
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = state ? (byte)0xFF : (byte)0x00;
        frame[5] = 0x00;

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 6));
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static byte[] CreateFc15Frame(byte slaveId, ushort address, ushort coilCount, byte[] coilBytes)
    {
        byte byteCount = (byte)coilBytes.Length;
        byte[] frame = new byte[7 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = 0x0F; // FC 15
        frame[2] = (byte)((address >> 8) & 0xFF);
        frame[3] = (byte)(address & 0xFF);
        frame[4] = (byte)((coilCount >> 8) & 0xFF);
        frame[5] = (byte)(coilCount & 0xFF);
        frame[6] = byteCount;

        Array.Copy(coilBytes, 0, frame, 7, byteCount);

        int payloadLen = 7 + byteCount;
        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, payloadLen));
        frame[payloadLen] = (byte)(crc & 0xFF);
        frame[payloadLen + 1] = (byte)((crc >> 8) & 0xFF);
        return frame;
    }

    private static string FormatHex(byte[] bytes)
    {
        return string.Join(" ", bytes.Select(b => b.ToString("X2")));
    }

    [RelayCommand]
    public void Apply()
    {
        if (HasRestrictedIdError)
        {
            SlaveId = 1;
        }
        UpdatePointCountFromPins();
        Node.PortIndex = PortIndex;
        Node.SlaveId = Math.Clamp(SlaveId, (byte)1, (byte)247);
        Node.WriteMode = WriteMode;
        Node.FunctionCode = FunctionCode;
        Node.StartAddress = StartAddress;
        Node.PointCount = Math.Clamp(PointCount, (ushort)1, (ushort)16);
        Node.BaseAddressCommand = BaseAddressCommand;
        Node.BaseAddressParam = BaseAddressParam;
        Node.ChannelCount = Math.Clamp(ChannelCount, (ushort)1, (ushort)64);
        Node.IsExclusiveChannel = IsExclusiveChannel;
        Node.TriggerMode = TriggerMode;
        Node.CyclicIntervalMs = Math.Max(50u, CyclicIntervalMs);
        Node.TimeoutMs = Math.Max(50u, TimeoutMs);
        Node.Deadband = Math.Max(0.01f, Deadband);
        Node.SyncConnectors();

        // Tự động đồng bộ 2 chiều: Upsert các chân Pin vào bảng MasterChannels của cổng tương ứng ở System Tab
        if (_modbusPorts != null && WriteMode == ModbusWriteMode.DirectRegisters)
        {
            var port = _modbusPorts.FirstOrDefault(p => p.PortIndex == PortIndex);
            if (port != null)
            {
                int baseWire = Node.StartAddress;
                byte fc = (byte)Node.FunctionCode;
                foreach (var pin in Node.Pins)
                {
                    string tagName = pin.TargetTag?.Name ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(tagName)) continue;

                    int pinWire = baseWire + pin.RegOffset;
                    int modiconAddr = Services.Modbus.ModbusChannelSyncService.ToModiconAddress(pinWire, fc);

                    var existing = port.MasterChannels.FirstOrDefault(c =>
                        c.SlaveId == Node.SlaveId &&
                        (c.Address == modiconAddr || string.Equals(c.TargetTagName, tagName, StringComparison.OrdinalIgnoreCase)));

                    if (existing != null)
                    {
                        existing.SlaveId = Node.SlaveId;
                        existing.FunctionCode = fc;
                        existing.Address = (ushort)modiconAddr;
                        existing.DataType = pin.DataType;
                        existing.Endianness = pin.Endianness;
                        existing.Scale = pin.Multiplier;
                        existing.TargetTagName = tagName;
                        existing.LinkedNodeId = Node.Id;
                    }
                    else
                    {
                        port.MasterChannels.Add(new SimplePLC.Studio.Models.ModbusMasterChannelModel
                        {
                            SlaveId = Node.SlaveId,
                            FunctionCode = fc,
                            Address = (ushort)modiconAddr,
                            DataType = pin.DataType,
                            Endianness = pin.Endianness,
                            Scale = pin.Multiplier,
                            TargetTagName = tagName,
                            LinkedNodeId = Node.Id
                        });
                    }
                }
            }
        }
    }
}
