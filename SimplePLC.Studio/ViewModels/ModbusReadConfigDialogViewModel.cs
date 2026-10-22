using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Enums;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(PortSelectorTooltip))]
    private byte _portIndex;

    public ObservableCollection<OptionItem<byte>> AvailablePorts { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private byte _slaveId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ModbusMasterFunctionCode _functionCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ushort _startAddress;

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

    public string SummaryBadgeText => $"{(LocalizationService.Instance.IsVietnamese ? "Cá»•ng " : "Port ")}{PortIndex + 1} | Slave {SlaveId} | FC{(byte)FunctionCode:D2} | Addr {StartAddress} ({Node.Pins.Count} Pins)";

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

    private readonly IReadOnlyList<SimplePLC.Studio.Models.ModbusPortConfigModel>? _modbusPorts;

    public ModbusReadConfigDialogViewModel(
        ModbusReadNodeViewModel node, 
        TagCatalogViewModel tagCatalog,
        IEnumerable<SimplePLC.Studio.Models.ModbusPortConfigModel>? availablePorts = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        TagCatalog = tagCatalog;
        _modbusPorts = availablePorts?.ToList();

        _portIndex = node.PortIndex;
        _slaveId = node.SlaveId;
        _functionCode = node.FunctionCode;
        _startAddress = node.StartAddress;
        _regCount = Math.Max((ushort)1, node.RegCount);
        _pollIntervalMs = node.PollIntervalMs;
        _timeoutMs = node.TimeoutMs;
        _readMode = node.ReadMode;

        foreach (var pin in node.Pins)
        {
            pin.PropertyChanged += OnPinPropertyChanged;
        }

        UpdateRegCountFromPins();

        PopulateLocalizedOptions();
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    public void PopulateLocalizedOptions()
    {
        var curPort = PortIndex;
        var curMode = ReadMode;
        var curFc = FunctionCode;

        AvailablePorts.Clear();
        bool isVi = LocalizationService.Instance.IsVietnamese;
        string prefix = isVi ? "Cổng" : "Port";

        if (_modbusPorts != null && _modbusPorts.Count > 0)
        {
            var enabledPorts = _modbusPorts.Where(p => p.Enabled).ToList();
            if (enabledPorts.Count > 0)
            {
                foreach (var p in enabledPorts)
                {
                    AvailablePorts.Add(new OptionItem<byte>(p.PortIndex, p.FormatDropdownLabel(isVi)));
                }

                if (!enabledPorts.Any(p => p.PortIndex == curPort))
                {
                    var disabledPort = _modbusPorts.FirstOrDefault(p => p.PortIndex == curPort);
                    if (disabledPort != null)
                    {
                        AvailablePorts.Add(new OptionItem<byte>(disabledPort.PortIndex, disabledPort.FormatDropdownLabel(isVi, isForceDisabledWarning: true)));
                    }
                }
            }
            else
            {
                string noPortsMsg = LocalizationService.Tr("ModbusNoPortsEnabled") ?? (isVi ? "[Chưa kích hoạt cổng RS-485]" : "[No RS-485 Ports Enabled]");
                AvailablePorts.Add(new OptionItem<byte>(curPort, noPortsMsg));
            }
        }
        else
        {
            for (byte i = 0; i < 4; i++)
            {
                AvailablePorts.Add(new OptionItem<byte>(i, $"{prefix} {i + 1} (RS-485 #{i + 1})"));
            }
        }

        AvailableReadModes.Clear();
        AvailableReadModes.Add(new(ModbusReadMode.Cyclic, LocalizationService.Tr("ModbusReadModeCyclic") ?? "Cyclic (Periodic Polling)"));
        AvailableReadModes.Add(new(ModbusReadMode.OnTriggerRisingEdge, LocalizationService.Tr("ModbusReadModeTrigger") ?? "On Rising Edge (Trig)"));

        AvailableFunctionCodes.Clear();
        AvailableFunctionCodes.Add(new(ModbusMasterFunctionCode.ReadHoldingRegisters, LocalizationService.Tr("ModbusReadFc03") ?? "FC 03: Read Holding Registers (4x)"));
        AvailableFunctionCodes.Add(new(ModbusMasterFunctionCode.ReadInputRegisters, LocalizationService.Tr("ModbusReadFc04") ?? "FC 04: Read Input Registers (3x)"));
        AvailableFunctionCodes.Add(new(ModbusMasterFunctionCode.ReadCoils, LocalizationService.Tr("ModbusReadFc01") ?? "FC 01: Read Coils (0x)"));
        AvailableFunctionCodes.Add(new(ModbusMasterFunctionCode.ReadDiscreteInputs, LocalizationService.Tr("ModbusReadFc02") ?? "FC 02: Read Discrete Inputs (1x)"));

        PortIndex = curPort;
        ReadMode = curMode;
        FunctionCode = curFc;
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
            Multiplier = 1.0f
        };
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
                details.Add($"{pin.Label}: [Thiếu thanh ghi offset +{pin.RegOffset}]");
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
        // Khung tin mẫu chuẩn Modbus RTU FC03 (2 thanh ghi: 0x0064 = 100, 0x00C8 = 200)
        SimulationHexInput = "01 03 04 00 64 00 C8 3A 12";
        HasRunSimulation = false;
        SimulationStatusMessage = LocalizationService.Tr("ModbusTestWaitingText") ?? "Nhập chuỗi Hex phản hồi từ cảm biến rồi nhấn 'Kiểm tra giải mã'.";
    }

    [RelayCommand]
    public void Apply()
    {
        UpdateRegCountFromPins();
        Node.PortIndex = PortIndex;
        Node.SlaveId = Math.Clamp(SlaveId, (byte)1, (byte)247);
        Node.FunctionCode = FunctionCode;
        Node.StartAddress = StartAddress;
        Node.RegCount = RegCount;
        Node.PollIntervalMs = Math.Max(50u, PollIntervalMs);
        Node.TimeoutMs = Math.Max(50u, TimeoutMs);
        Node.ReadMode = ReadMode;
    }
}
