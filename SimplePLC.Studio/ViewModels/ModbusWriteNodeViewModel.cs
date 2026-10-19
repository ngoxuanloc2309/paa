using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa Modbus RTU Master Write (Bộ xuất lệnh chấp hành / ghi dữ liệu RS-485).
/// Hỗ trợ 2 chế độ:
/// 1. Channel-Indexed Target (Sort-to-Light, Cụm van, Tháp đèn): Điều khiển theo Channel, Command, Param với tùy chọn Exclusive.
/// 2. Direct Registers: Ghi thanh ghi Modbus truyền thống (FC 05, 06, 15, 16).
/// </summary>
public partial class ModbusWriteNodeViewModel : GraphNodeViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(DeviceIdDisplay))]
    private byte _slaveId = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(TargetConfigDisplay))]
    [NotifyPropertyChangedFor(nameof(WriteModeName))]
    [NotifyPropertyChangedFor(nameof(IsChannelIndexedMode))]
    [NotifyPropertyChangedFor(nameof(IsDirectRegistersMode))]
    private ModbusWriteMode _writeMode = ModbusWriteMode.ChannelIndexed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(TargetConfigDisplay))]
    [NotifyPropertyChangedFor(nameof(FunctionCodeDisplay))]
    [NotifyPropertyChangedFor(nameof(FunctionCodeBadge))]
    [NotifyPropertyChangedFor(nameof(FunctionCodeName))]
    private ModbusMasterWriteFunctionCode _functionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister;

    // Direct mode settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(TargetConfigDisplay))]
    private ushort _startAddress = 0;

    [ObservableProperty]
    private ushort _pointCount = 1;

    // Channel-Indexed mode settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private ushort _baseAddressCommand = 0x0100;

    [ObservableProperty]
    private ushort _baseAddressParam = 0x0120;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(TargetConfigDisplay))]
    [NotifyPropertyChangedFor(nameof(PinsCountSummary))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private ushort _channelCount = 9;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private bool _isExclusiveChannel = true;

    // Trigger settings
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriggerModeName))]
    private ModbusWriteTriggerMode _triggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge;

    [ObservableProperty]
    private uint _cyclicIntervalMs = 500;

    [ObservableProperty]
    private uint _timeoutMs = 300;

    public bool IsChannelIndexedMode => WriteMode == ModbusWriteMode.ChannelIndexed;
    public bool IsDirectRegistersMode => WriteMode == ModbusWriteMode.DirectRegisters;

    public string DeviceIdDisplay => $"ID: {SlaveId}";

    public string TargetConfigDisplay => WriteMode == ModbusWriteMode.ChannelIndexed
        ? $"CH [1..{ChannelCount}]"
        : $"FC{(byte)FunctionCode:D2} · Addr: {StartAddress}";

    public string NodeInfoSummary => WriteMode == ModbusWriteMode.ChannelIndexed
        ? $"ID:{SlaveId} | CH:{ChannelCount}"
        : $"ID:{SlaveId} | FC{(byte)FunctionCode:D2}";

    public string FunctionCodeBadge => $"FC {(byte)FunctionCode:D2}";

    public string FunctionCodeName => FunctionCode switch
    {
        ModbusMasterWriteFunctionCode.WriteSingleCoil => LocalizationService.Tr("ModbusWriteFc05") ?? "FC 05: Write Single Coil (0x)",
        ModbusMasterWriteFunctionCode.WriteSingleRegister => LocalizationService.Tr("ModbusWriteFc06") ?? "FC 06: Write Single Register (4x)",
        ModbusMasterWriteFunctionCode.WriteMultipleCoils => LocalizationService.Tr("ModbusWriteFc15") ?? "FC 15: Write Multiple Coils (0x)",
        ModbusMasterWriteFunctionCode.WriteMultipleRegisters => LocalizationService.Tr("ModbusWriteFc16") ?? "FC 16: Write Multiple Registers (4x)",
        _ => FunctionCode.ToString()
    };

    public string FunctionCodeDisplay => $"{FunctionCodeBadge} ({FunctionCodeName})";

    public string WriteModeName => WriteMode == ModbusWriteMode.ChannelIndexed
        ? LocalizationService.Tr("ModbusWriteModeIndexed") ?? "Channel-Indexed Target"
        : LocalizationService.Tr("ModbusWriteModeDirect") ?? "Direct Registers";

    public string TriggerModeName => TriggerMode switch
    {
        ModbusWriteTriggerMode.OnTriggerRisingEdge => LocalizationService.Tr("ModbusWriteTrigEdge") ?? "On Rising Edge (Trig)",
        ModbusWriteTriggerMode.OnDataChange => LocalizationService.Tr("ModbusWriteTrigChange") ?? "On Data Change",
        ModbusWriteTriggerMode.Cyclic => $"{LocalizationService.Tr("ModbusWriteTrigCyclic") ?? "Cyclic"} ({CyclicIntervalMs} ms)",
        _ => TriggerMode.ToString()
    };

    // Danh sách chân ngõ vào động cho Direct Mode
    public ObservableCollection<ModbusPinBindingViewModel> Pins { get; } = new();

    public string PinsCountSummary => WriteMode == ModbusWriteMode.ChannelIndexed
        ? (LocalizationService.Instance.IsVietnamese ? $"{ChannelCount} kênh (Chỉ số)" : $"{ChannelCount} Channels (Indexed)")
        : string.Format(LocalizationService.Tr("InspectorModbusWritePinsSummary") ?? "{0} Input Pins", Pins.Count);

    // Live Diagnostics & Trạng thái chạy
    [ObservableProperty]
    private bool _isLiveActive = false;

    [ObservableProperty]
    private bool _isDoneActive = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private bool _isBusyActive = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private bool _isErrorActive = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private string _lastErrorMessage = string.Empty;

    [ObservableProperty]
    private ushort _lastErrorCode = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private uint _writeCount = 0;

    // Connectors cố định
    public ConnectorViewModel TrigConnector { get; }
    public ConnectorViewModel DoneConnector { get; }
    public ConnectorViewModel BusyConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }
    public ConnectorViewModel ErrCodeConnector { get; }

    // Connectors cho Channel-Indexed Mode
    public ConnectorViewModel ChannelConnector { get; }
    public ConnectorViewModel CommandConnector { get; }
    public ConnectorViewModel ParamConnector { get; }

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? (WriteMode == ModbusWriteMode.ChannelIndexed
            ? (LocalizationService.Instance.IsVietnamese
                ? $"MB_WRITE [Thiết bị: {SlaveId} | {ChannelCount} kênh | {(IsExclusiveChannel ? "Độc quyền" : "Độc lập")}]"
                : $"MB_WRITE [Dev: {SlaveId} | CH:{ChannelCount} | {(IsExclusiveChannel ? "Exclusive" : "Independent")}]")
            : (LocalizationService.Instance.IsVietnamese
                ? $"MB_WRITE [Thiết bị: {SlaveId} | FC{(byte)FunctionCode:D2} | Địa chỉ: {StartAddress}] ({Pins.Count} chân)"
                : $"MB_WRITE [Dev: {SlaveId} | FC{(byte)FunctionCode:D2} | Addr:{StartAddress}] ({Pins.Count} Pins)"))
        : CustomLabel;

    public string StatusPillText
    {
        get
        {
            if (IsErrorActive) return !string.IsNullOrWhiteSpace(LastErrorMessage)
                ? (LocalizationService.Instance.IsVietnamese ? $"LỖI: {LastErrorMessage}" : $"ERR: {LastErrorMessage}")
                : (LocalizationService.Instance.IsVietnamese ? "LỖI" : "ERR");
            if (IsBusyActive) return LocalizationService.Instance.IsVietnamese ? "ĐANG GỬI..." : "TX...";
            if (WriteCount > 0) return LocalizationService.Instance.IsVietnamese ? $"HOÀN TẤT #{WriteCount}" : $"DONE #{WriteCount}";
            return LocalizationService.Instance.IsVietnamese ? "SẴN SÀNG" : "READY";
        }
    }

    public ModbusWriteNodeViewModel(byte slaveId = 1, ModbusWriteMode mode = ModbusWriteMode.ChannelIndexed)
    {
        Title = "MB_WRITE";
        Icon = "📤";
        SlaveId = slaveId;
        WriteMode = mode;

        // Khởi tạo các Connectors chuẩn
        TrigConnector = new ConnectorViewModel
        {
            Title = "Trig",
            TooltipKey = "PinTooltipMbWriteTrig",
            IsInput = true,
            IsEvent = true,
            Node = this
        };

        DoneConnector = new ConnectorViewModel
        {
            Title = "Done",
            TooltipKey = "PinTooltipMbWriteDone",
            IsInput = false,
            IsEvent = true,
            Node = this
        };

        BusyConnector = new ConnectorViewModel
        {
            Title = "Busy",
            TooltipKey = "PinTooltipMbWriteBusy",
            IsInput = false,
            IsEvent = false,
            Node = this
        };

        ErrorConnector = new ConnectorViewModel
        {
            Title = "Error",
            TooltipKey = "PinTooltipMbWriteErr",
            IsInput = false,
            IsEvent = false,
            Node = this
        };

        ErrCodeConnector = new ConnectorViewModel
        {
            Title = "ErrCode",
            TooltipKey = "PinTooltipMbWriteErrCode",
            IsInput = false,
            IsEvent = false,
            Node = this
        };

        ChannelConnector = new ConnectorViewModel
        {
            Title = "Channel",
            TooltipKey = "PinTooltipMbWriteChannel",
            IsInput = true,
            IsEvent = false,
            Node = this
        };

        CommandConnector = new ConnectorViewModel
        {
            Title = "Command",
            TooltipKey = "PinTooltipMbWriteCommand",
            IsInput = true,
            IsEvent = false,
            Node = this
        };

        ParamConnector = new ConnectorViewModel
        {
            Title = "Param",
            TooltipKey = "PinTooltipMbWriteParam",
            IsInput = true,
            IsEvent = false,
            Node = this
        };

        Pins.CollectionChanged += OnPinsCollectionChanged;

        SyncConnectors();
    }

    private void OnPinsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (WriteMode == ModbusWriteMode.DirectRegisters)
        {
            SyncConnectors();
        }
        OnPropertyChanged(nameof(PinsCountSummary));
        OnPropertyChanged(nameof(SummaryText));
    }

    partial void OnWriteModeChanged(ModbusWriteMode value)
    {
        SyncConnectors();
    }

    /// <summary>
    /// Đồng bộ hóa Input/Output Connectors trên Canvas FBD tương ứng với Chế độ hoạt động.
    /// </summary>
    public void SyncConnectors()
    {
        InputConnectors.Clear();
        OutputConnectors.Clear();

        // 1. Luôn có chân kích hoạt Trig ở đầu
        InputConnectors.Add(TrigConnector);

        if (WriteMode == ModbusWriteMode.ChannelIndexed)
        {
            // Chế độ Channel-Indexed Target
            InputConnectors.Add(ChannelConnector);
            InputConnectors.Add(CommandConnector);
            InputConnectors.Add(ParamConnector);
        }
        else
        {
            // Chế độ Direct Registers: Mỗi pin binding là 1 chân input
            foreach (var pin in Pins)
            {
                var connector = new ConnectorViewModel
                {
                    Title = string.IsNullOrWhiteSpace(pin.Label) ? pin.Id : pin.Label,
                    TooltipKey = "PinTooltipMbWriteDataIn",
                    IsInput = true,
                    IsEvent = false,
                    Node = this
                };
                InputConnectors.Add(connector);
            }
        }

        // 2. Các chân trạng thái ngõ ra
        OutputConnectors.Add(DoneConnector);
        OutputConnectors.Add(BusyConnector);
        OutputConnectors.Add(ErrorConnector);
        OutputConnectors.Add(ErrCodeConnector);
    }

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        OnPropertyChanged(nameof(NodeInfoSummary));
        OnPropertyChanged(nameof(StatusPillText));
        OnPropertyChanged(nameof(WriteModeName));
        OnPropertyChanged(nameof(TriggerModeName));
        OnPropertyChanged(nameof(PinsCountSummary));
        OnPropertyChanged(nameof(FunctionCodeName));
        OnPropertyChanged(nameof(FunctionCodeDisplay));
        OnPropertyChanged(nameof(SummaryText));
    }
}
