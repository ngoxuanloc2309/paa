using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình khối MODBUS READ (Modbus RTU Master Poller).
/// Cung cấp giao diện cấu hình Slave ID, Function Code, Start Address, chu kỳ quét
/// và giả lập bóc tách trực tiếp chuỗi Hex nhận từ cảm biến thực tế.
/// </summary>
public partial class ModbusReadConfigDialogViewModel : ObservableObject
{
    public ModbusReadNodeViewModel Node { get; }
    public TagCatalogViewModel TagCatalog { get; }
    public ObservableCollection<SimplePLC.Studio.Models.TagModel> AvailableTags => TagCatalog.AllTags;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(PortSelectorTooltip))]
    [NotifyPropertyChangedFor(nameof(HasRestrictedIdError))]
    [NotifyPropertyChangedFor(nameof(RestrictedIdErrorMessage))]
    [NotifyPropertyChangedFor(nameof(CanImportSystemChannels))]
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
    }

    /// <summary>
    /// Cho biết trên cổng hiện tại ở System Tab có kênh Modbus Read (FC01..04) nào để nhập nhanh hay không.
    /// </summary>
    public bool CanImportSystemChannels
    {
        get
        {
            if (_modbusPorts == null) return false;
            var port = _modbusPorts.FirstOrDefault(p => p.PortIndex == PortIndex);
            return port?.MasterChannels != null && port.MasterChannels.Any(c => c.FunctionCode >= 1 && c.FunctionCode <= 4);
        }
    }

    /// <summary>
    /// Nhập nhanh toàn bộ các kênh Modbus Read đã khai báo ở System Tab trên cổng này vào bảng Pins.
    /// </summary>
    [RelayCommand]
    public void ImportSystemChannels()
    {
        if (_modbusPorts == null) return;
        var port = _modbusPorts.FirstOrDefault(p => p.PortIndex == PortIndex);
        if (port?.MasterChannels == null) return;

        var readChannels = port.MasterChannels
            .Where(c => c.FunctionCode >= 1 && c.FunctionCode <= 4)
            .OrderBy(c => c.Address)
            .ToList();

        if (readChannels.Count == 0) return;

        // Lấy kênh đầu tiên làm mẫu chuẩn cho Slave ID và Function Code
        var first = readChannels[0];
        SlaveId = first.SlaveId == 0 ? (byte)1 : first.SlaveId;
        FunctionCode = first.FunctionCode switch
        {
            1 => ModbusMasterFunctionCode.ReadCoils,
            2 => ModbusMasterFunctionCode.ReadDiscreteInputs,
            4 => ModbusMasterFunctionCode.ReadInputRegisters,
            _ => ModbusMasterFunctionCode.ReadHoldingRegisters
        };

        // Địa chỉ dây bắt đầu (0-based) là địa chỉ của kênh đầu tiên
        int baseWire = Services.Modbus.ModbusChannelSyncService.ToWireOffset(first.Address, first.FunctionCode);
        StartAddress = (ushort)baseWire;

        // Xóa các pin cũ và nạp lại từ các kênh của System Tab
        foreach (var p in Node.Pins)
        {
            p.PropertyChanged -= OnPinPropertyChanged;
        }
        Node.Pins.Clear();

        foreach (var ch in readChannels)
        {
            int chWire = Services.Modbus.ModbusChannelSyncService.ToWireOffset(ch.Address, ch.FunctionCode);
            int offset = Math.Max(0, chWire - baseWire);

            string pinLabel = !string.IsNullOrWhiteSpace(ch.TargetTagAlias)
                ? ch.TargetTagAlias
                : (!string.IsNullOrWhiteSpace(ch.Description)
                    ? ch.Description
                    : (!string.IsNullOrWhiteSpace(ch.TargetTagName) ? ch.TargetTagName : $"Pin_{Node.Pins.Count + 1}"));

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

        UpdateRegCountFromPins();
        OnPropertyChanged(nameof(SummaryBadgeText));
        OnPropertyChanged(nameof(HasPins));
        OnPropertyChanged(nameof(HasNoPins));
        OnPropertyChanged(nameof(CanImportSystemChannels));
        ResetSampleHex();
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
    private ModbusMasterFunctionCode _functionCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(StartAddressModiconPreview))]
    private ushort _startAddress;

    /// <summary>
    /// Hiển thị địa chỉ Modicon 1-based tương ứng để khớp hoàn toàn với bảng System Tab (vd: 40001 khi offset là 0).
    /// </summary>
    public string StartAddressModiconPreview =>
        $"= {Services.Modbus.ModbusChannelSyncService.ToModiconAddress(StartAddress, (int)FunctionCode)}";

    partial void OnStartAddressChanged(ushort value)
    {
        foreach (var pin in Node.Pins)
        {
            pin.RefreshTagOptions();
        }
    }

    partial void OnFunctionCodeChanged(ModbusMasterFunctionCode value)
    {
        OnPropertyChanged(nameof(StartAddressModiconPreview));
        OnPropertyChanged(nameof(SummaryBadgeText));
        foreach (var pin in Node.Pins)
        {
            pin.RefreshTagOptions();
        }
    }

    [ObservableProperty]
    private ushort _regCount;

    [ObservableProperty]
    private uint _pollIntervalMs;

    [ObservableProperty]
    private uint _timeoutMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsTriggerMode))]
    [NotifyPropertyChangedFor(nameof(IsCyclicMode))]
    private ModbusReadMode _readMode;

    public bool IsTriggerMode => ReadMode == ModbusReadMode.OnTriggerRisingEdge;
    public bool IsCyclicMode => ReadMode == ModbusReadMode.Cyclic;

    public ObservableCollection<OptionItem<ModbusReadMode>> AvailableReadModes { get; } = new();

    public string SummaryBadgeText
    {
        get
        {
            string portText = PortIndex == 4 ? "Zigbee" : $"{(LocalizationService.Instance.IsVietnamese ? "Cổng " : "Port ")}{PortIndex + 1}";
            return $"{portText} | Slave {SlaveId} | FC{(byte)FunctionCode:D2} | Addr {StartAddress} ({Node.Pins.Count} Pins)";
        }
    }

    public bool HasPins => Node.Pins.Count > 0;
    public bool HasNoPins => Node.Pins.Count == 0;

    [ObservableProperty]
    private string _simulationHexInput = string.Empty;

    [ObservableProperty]
    private string _simulationStatusMessage = LocalizationService.Tr("ModbusTestWaitingText") ?? "Nhập chuỗi Hex phản hồi từ cảm biến rồi nhấn 'Kiểm tra giải mã'.";

    [ObservableProperty]
    private bool _isSimulationSuccess = true;

    [ObservableProperty]
    private bool _hasRunSimulation = false;

    public ObservableCollection<OptionItem<ModbusMasterFunctionCode>> AvailableFunctionCodes { get; } = new();

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

    public ModbusReadConfigDialogViewModel(
        ModbusReadNodeViewModel node, 
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
        _functionCode = node.FunctionCode;
        _startAddress = node.StartAddress;
        _regCount = Math.Max((ushort)1, node.RegCount);
        _pollIntervalMs = node.PollIntervalMs;

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

        _readMode = node.ReadMode;

        foreach (var pin in node.Pins)
        {
            ConfigurePinTagProvider(pin);
            // Dọn dẹp nếu pin đang có tag không tương thích với DataType ban đầu
            if (pin.TargetTag != null && pin.TargetTag.Kind != SimplePLC.Domain.Enums.TagKind.None)
            {
                bool isTagBool = pin.TargetTag.IsDigital || pin.TargetTag.DataType == SimplePLC.Domain.Enums.TagDataType.Boolean;
                bool isTagReal = pin.TargetTag.DataType == SimplePLC.Domain.Enums.TagDataType.Float32;
                bool isTagInt = !isTagBool && !isTagReal;

                bool isCompatible = pin.DataType switch
                {
                    ModbusDataType.Bool => isTagBool,
                    ModbusDataType.Real => isTagReal,
                    _ => isTagInt
                };

                if (!isCompatible)
                {
                    pin.TargetTag = tagCatalog?.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None);
                }
            }
            else if (pin.TargetTag == null && tagCatalog != null)
            {
                pin.TargetTag = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None);
            }
            pin.PropertyChanged += OnPinPropertyChanged;
        }

        UpdateRegCountFromPins();

        PopulateLocalizedOptions();
        ResetSampleHex();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    public void PopulateLocalizedOptions()
    {
        UpdatePortOptions();
        UpdateReadModes();
        UpdateFunctionCodes();
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

    private void UpdateReadModes()
    {
        var targetModes = new List<OptionItem<ModbusReadMode>>
        {
            new(ModbusReadMode.Cyclic, LocalizationService.Tr("ModbusReadModeCyclic") ?? "Cyclic (Periodic Polling)"),
            new(ModbusReadMode.OnTriggerRisingEdge, LocalizationService.Tr("ModbusReadModeTrigger") ?? "On Rising Edge (Trig)")
        };

        SyncOptions(AvailableReadModes, targetModes);
        OnPropertyChanged(nameof(ReadMode));
    }

    private void UpdateFunctionCodes()
    {
        var targetFc = new List<OptionItem<ModbusMasterFunctionCode>>
        {
            new(ModbusMasterFunctionCode.ReadHoldingRegisters, LocalizationService.Tr("ModbusReadFc03") ?? "FC 03: Read Holding Registers (4x)"),
            new(ModbusMasterFunctionCode.ReadInputRegisters, LocalizationService.Tr("ModbusReadFc04") ?? "FC 04: Read Input Registers (3x)"),
            new(ModbusMasterFunctionCode.ReadCoils, LocalizationService.Tr("ModbusReadFc01") ?? "FC 01: Read Coils (0x)"),
            new(ModbusMasterFunctionCode.ReadDiscreteInputs, LocalizationService.Tr("ModbusReadFc02") ?? "FC 02: Read Discrete Inputs (1x)")
        };

        SyncOptions(AvailableFunctionCodes, targetFc);
        OnPropertyChanged(nameof(FunctionCode));
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
    }

    private void OnPinPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ModbusPinBindingViewModel.RegOffset) or nameof(ModbusPinBindingViewModel.DataType))
        {
            UpdateRegCountFromPins();
            OnPropertyChanged(nameof(SummaryBadgeText));
            if (HasRunSimulation && !string.IsNullOrWhiteSpace(SimulationHexInput))
            {
                RunSimulationTest();
            }
        }
    }

    /// <summary>
    /// Cấu hình hàm cung cấp Tag tương thích cho một chân Pin dựa trên TagCatalog và liên kết System Tab.
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
    /// Lấy danh sách các Tag tương thích theo kiểu dữ liệu Modbus từ TagCatalog với vai trò WriteTarget (Loại bỏ DI, AI).
    /// </summary>
    public IEnumerable<TagModel> GetCompatibleTagsForDataType(ModbusDataType dataType)
    {
        return TagCatalog.GetFilteredTags(TagAccessRole.WriteTarget, dataType, includeNone: true);
    }

    [RelayCommand]
    public void AddPin()
    {
        if (Node.Pins.Count >= 8) return;

        ushort nextOffset = 0;
        if (Node.Pins.Count > 0)
        {
            var last = Node.Pins.Last();
            int span = (last.DataType is ModbusDataType.Real or ModbusDataType.Int32 or ModbusDataType.UInt32) ? 2 : 1;
            nextOffset = (ushort)(last.RegOffset + span);
        }

        var newPin = new ModbusPinBindingViewModel
        {
            Label = $"Pin_{Node.Pins.Count + 1}",
            RegOffset = nextOffset,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Multiplier = 1.0f,
            TargetTag = TagCatalog?.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None)
        };
        ConfigurePinTagProvider(newPin);
        newPin.PropertyChanged += OnPinPropertyChanged;
        Node.Pins.Add(newPin);
        UpdateRegCountFromPins();
        OnPropertyChanged(nameof(SummaryBadgeText));
        OnPropertyChanged(nameof(HasPins));
        OnPropertyChanged(nameof(HasNoPins));
        if (HasRunSimulation && !string.IsNullOrWhiteSpace(SimulationHexInput))
        {
            RunSimulationTest();
        }
    }

    [RelayCommand]
    public void RemovePin(ModbusPinBindingViewModel pin)
    {
        if (pin != null && Node.Pins.Contains(pin))
        {
            pin.PropertyChanged -= OnPinPropertyChanged;
            Node.Pins.Remove(pin);
            UpdateRegCountFromPins();
            OnPropertyChanged(nameof(SummaryBadgeText));
            OnPropertyChanged(nameof(HasPins));
            OnPropertyChanged(nameof(HasNoPins));
            if (HasRunSimulation && !string.IsNullOrWhiteSpace(SimulationHexInput))
            {
                RunSimulationTest();
            }
        }
    }

    private void UpdateRegCountFromPins()
    {
        if (Node.Pins.Count == 0)
        {
            if (RegCount == 0) RegCount = 1;
            return;
        }

        ushort minNeeded = 1;
        foreach (var pin in Node.Pins)
        {
            int span = (pin.DataType is ModbusDataType.Real or ModbusDataType.Int32 or ModbusDataType.UInt32) ? 2 : 1;
            ushort endReg = (ushort)(pin.RegOffset + span);
            if (endReg > minNeeded) minNeeded = endReg;
        }

        // Tự động nâng số lượng đọc nếu số thanh ghi hiện tại chưa đủ cho các chân
        if (RegCount < minNeeded)
        {
            RegCount = minNeeded;
        }
    }

    [RelayCommand]
    public void RunSimulationTest()
    {
        HasRunSimulation = true;
        bool isVi = LocalizationService.Instance.IsVietnamese;

        if (Node.Pins.Count == 0)
        {
            IsSimulationSuccess = false;
            SimulationStatusMessage = isVi
                ? "Chưa có chân thanh ghi nào. Vui lòng thêm chân ở bảng bên trái."
                : "No register pins configured. Please add pins in the left table.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SimulationHexInput))
        {
            IsSimulationSuccess = false;
            SimulationStatusMessage = isVi
                ? "Vui lòng nhập chuỗi byte Hex phản hồi (hoặc nhấn 'Khung mẫu' để thử)."
                : "Please enter response hex frame (or click 'Sample Frame' to test).";
            return;
        }

        if (!ModbusMasterDataParser.TryParseHexToRegisters(SimulationHexInput, out var registers, out string hexError))
        {
            IsSimulationSuccess = false;
            SimulationStatusMessage = isVi
                ? $"Chuỗi Hex không hợp lệ: {hexError}"
                : $"Invalid hex input: {hexError}";
            return;
        }

        int successCount = 0;
        var details = new List<string>();

        foreach (var pin in Node.Pins)
        {
            if (ModbusMasterDataParser.TryParsePinValue(
                registers,
                pin.RegOffset,
                pin.DataType,
                pin.Endianness,
                pin.Multiplier,
                out double val))
            {
                pin.CurrentValue = val;
                successCount++;
                details.Add($"{pin.Label}: {val.ToString("0.##", CultureInfo.InvariantCulture)}");
            }
            else
            {
                details.Add(isVi 
                    ? $"{pin.Label}: [Thiếu thanh ghi offset +{pin.RegOffset}]"
                    : $"{pin.Label}: [Missing register offset +{pin.RegOffset}]");
            }
        }

        if (successCount == Node.Pins.Count)
        {
            IsSimulationSuccess = true;
            SimulationStatusMessage = isVi
                ? $"Khung tin hợp lệ ({registers.Length} thanh ghi · CRC OK)! Đã giải mã: {string.Join(" | ", details)}"
                : $"Valid frame ({registers.Length} regs · CRC OK)! Decoded: {string.Join(" | ", details)}";
        }
        else
        {
            IsSimulationSuccess = false;
            SimulationStatusMessage = isVi
                ? $"Độ dài khung tin chỉ có {registers.Length} thanh ghi, không đủ cho tất cả các chân. Chi tiết: {string.Join(" | ", details)}"
                : $"Frame length is only {registers.Length} registers, insufficient for all pins. Details: {string.Join(" | ", details)}";
        }
    }

    [RelayCommand]
    public void ResetSampleHex()
    {
        SimulationHexInput = GenerateRealisticSampleHex();
        HasRunSimulation = false;
        SimulationStatusMessage = LocalizationService.Tr("ModbusTestWaitingText") ?? (LocalizationService.Instance.IsVietnamese 
            ? "Nhập chuỗi Hex phản hồi từ cảm biến rồi nhấn 'Kiểm tra giải mã'."
            : "Enter raw hex response from sensor and click 'Run Frame Test'.");
    }

    private string GenerateRealisticSampleHex()
    {
        if (Node.Pins.Count == 0)
        {
            return "01 03 04 00 64 00 C8 3A 12";
        }

        int maxNeededWords = 2;
        foreach (var pin in Node.Pins)
        {
            int words = (pin.DataType is ModbusDataType.Real or ModbusDataType.UInt32 or ModbusDataType.Int32) ? 2 : 1;
            int needed = pin.RegOffset + words;
            if (needed > maxNeededWords) maxNeededWords = needed;
        }

        int totalRegs = Math.Max((int)Node.RegCount, maxNeededWords);
        if (totalRegs > 125) totalRegs = 125;

        ushort[] registers = new ushort[totalRegs];

        foreach (var pin in Node.Pins)
        {
            if (pin.RegOffset >= totalRegs) continue;

            float sampleVal = 0f;
            string lbl = pin.Label.ToLowerInvariant();
            if (lbl.Contains('v') || lbl.Contains("volt")) sampleVal = 230.5f;
            else if (lbl.Contains('i') || lbl.Contains("curr") || lbl.Contains("amp")) sampleVal = 15.2f;
            else if (lbl.Contains("kw") || lbl.Contains("p_") || lbl.Contains("power")) sampleVal = 10.5f;
            else if (lbl.Contains("kwh") || lbl.Contains("energy")) sampleVal = 1250.0f;
            else if (lbl.Contains("freq") || lbl.Contains("hz")) sampleVal = 50.0f;
            else if (lbl.Contains("pf") || lbl.Contains("cos")) sampleVal = 0.98f;
            else sampleVal = (float)(100 + pin.RegOffset);

            if (pin.DataType == ModbusDataType.Real)
            {
                if (pin.RegOffset + 1 < totalRegs)
                {
                    uint bits = BitConverter.SingleToUInt32Bits(sampleVal);
                    ushort w1 = (ushort)((bits >> 16) & 0xFFFF);
                    ushort w2 = (ushort)(bits & 0xFFFF);
                    if (pin.Endianness is ModbusEndianness.BigEndian_ABCD or ModbusEndianness.ByteSwap_BADC)
                    {
                        registers[pin.RegOffset] = w1;
                        registers[pin.RegOffset + 1] = w2;
                    }
                    else
                    {
                        registers[pin.RegOffset] = w2;
                        registers[pin.RegOffset + 1] = w1;
                    }
                }
            }
            else if (pin.DataType == ModbusDataType.Int16)
            {
                registers[pin.RegOffset] = (ushort)(short)sampleVal;
            }
            else if (pin.DataType == ModbusDataType.UInt16)
            {
                registers[pin.RegOffset] = (ushort)sampleVal;
            }
            else if (pin.DataType is ModbusDataType.Int32 or ModbusDataType.UInt32)
            {
                if (pin.RegOffset + 1 < totalRegs)
                {
                    uint val = (uint)sampleVal;
                    registers[pin.RegOffset] = (ushort)((val >> 16) & 0xFFFF);
                    registers[pin.RegOffset + 1] = (ushort)(val & 0xFFFF);
                }
            }
        }

        byte slaveId = SlaveId > 0 ? SlaveId : (byte)1;
        byte fc = (byte)FunctionCode;
        if (fc == 0) fc = 3;
        int byteCount = totalRegs * 2;
        byte[] frame = new byte[3 + byteCount + 2];
        frame[0] = slaveId;
        frame[1] = fc;
        frame[2] = (byte)byteCount;

        for (int i = 0; i < totalRegs; i++)
        {
            frame[3 + i * 2] = (byte)((registers[i] >> 8) & 0xFF);
            frame[3 + i * 2 + 1] = (byte)(registers[i] & 0xFF);
        }

        ushort crc = Crc16Modbus.Compute(frame.AsSpan(0, 3 + byteCount));
        frame[3 + byteCount] = (byte)(crc & 0xFF);
        frame[3 + byteCount + 1] = (byte)((crc >> 8) & 0xFF);

        return string.Join(" ", frame.Select(b => b.ToString("X2")));
    }

    [RelayCommand]
    public void Apply()
    {
        if (HasRestrictedIdError)
        {
            SlaveId = 1;
        }
        UpdateRegCountFromPins();
        Node.PortIndex = PortIndex;
        Node.SlaveId = Math.Clamp(SlaveId, (byte)1, (byte)247);
        Node.FunctionCode = FunctionCode;
        Node.StartAddress = StartAddress;
        Node.RegCount = RegCount;
        Node.PollIntervalMs = Math.Max(50u, PollIntervalMs);
        Node.TimeoutMs = Math.Max(50u, TimeoutMs);
        Node.ReadMode = ReadMode;

        // Tự động đồng bộ 2 chiều: Upsert các chân Pin vào bảng MasterChannels của cổng tương ứng ở System Tab
        if (_modbusPorts != null)
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
                        port.MasterChannels.Add(new ModbusMasterChannelModel
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
