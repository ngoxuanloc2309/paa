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
/// ViewModel cho khối đồ họa Modbus RTU Master Read (Đọc cảm biến/thiết bị RS-485).
/// Hỗ trợ cấu hình Slave ID, Function Code, Start Address, Polling Interval,
/// chân ngõ ra động (Dynamic Output Pins) và tự động đồng bộ hóa Connectors trên Canvas.
/// </summary>
public partial class ModbusReadNodeViewModel : GraphNodeViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    private byte _slaveId = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(NodeInfoSummary))]
    [NotifyPropertyChangedFor(nameof(FunctionCodeDisplay))]
    [NotifyPropertyChangedFor(nameof(FunctionCodeBadge))]
    [NotifyPropertyChangedFor(nameof(FunctionCodeName))]
    private ModbusMasterFunctionCode _functionCode = ModbusMasterFunctionCode.ReadHoldingRegisters;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private ushort _startAddress = 0;

    [ObservableProperty]
    private ushort _regCount = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(PollIntervalDisplay))]
    [NotifyPropertyChangedFor(nameof(ReadModeSummaryText))]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private uint _pollIntervalMs = 500;

    [ObservableProperty]
    private uint _timeoutMs = 300;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ReadModeName))]
    [NotifyPropertyChangedFor(nameof(ReadModeSummaryText))]
    [NotifyPropertyChangedFor(nameof(IsTriggerMode))]
    [NotifyPropertyChangedFor(nameof(IsCyclicMode))]
    [NotifyPropertyChangedFor(nameof(StatusPillText))]
    private ModbusReadMode _readMode = ModbusReadMode.Cyclic;

    partial void OnReadModeChanged(ModbusReadMode value)
    {
        SyncConnectors();
    }

    public IReadOnlyList<OptionItem<ModbusReadMode>> AvailableReadModes { get; } = new List<OptionItem<ModbusReadMode>>
    {
        new(ModbusReadMode.Cyclic, LocalizationService.Tr("ModbusReadModeCyclic") ?? "Cyclic (Periodic Polling)"),
        new(ModbusReadMode.OnTriggerRisingEdge, LocalizationService.Tr("ModbusReadModeTrigger") ?? "On Rising Edge (Trig)")
    };

    public bool IsTriggerMode => ReadMode == ModbusReadMode.OnTriggerRisingEdge;
    public bool IsCyclicMode => ReadMode == ModbusReadMode.Cyclic;

    public string ReadModeName => ReadMode switch
    {
        ModbusReadMode.OnTriggerRisingEdge => LocalizationService.Tr("ModbusReadModeTrigger") ?? "On Rising Edge (Trig)",
        _ => LocalizationService.Tr("ModbusReadModeCyclic") ?? "Cyclic"
    };

    public string ReadModeSummaryText => IsTriggerMode
        ? (LocalizationService.Tr("ModbusWriteTrigEdgeShort") ?? "On Trig")
        : $"{PollIntervalMs}ms";

    // Chuỗi tóm tắt thông số truyền thông trên Canvas (ví dụ: ID:1 | FC03)
    public string NodeInfoSummary => $"ID:{SlaveId} | FC{(byte)FunctionCode:D2}";

    public string FunctionCodeBadge => $"FC {(byte)FunctionCode:D2}";

    public string FunctionCodeName => FunctionCode switch
    {
        ModbusMasterFunctionCode.ReadCoils => "Read Coils",
        ModbusMasterFunctionCode.ReadDiscreteInputs => "Read Discrete Inputs",
        ModbusMasterFunctionCode.ReadHoldingRegisters => "Read Holding Registers",
        ModbusMasterFunctionCode.ReadInputRegisters => "Read Input Registers",
        _ => FunctionCode.ToString()
    };

    public string FunctionCodeDisplay => $"{FunctionCodeBadge} ({FunctionCodeName})";

    public string PollIntervalDisplay => $"{PollIntervalMs} ms";

    // Danh sách chân ngõ ra động (Dynamic Pins)
    public ObservableCollection<ModbusPinBindingViewModel> Pins { get; } = new();

    public string PinsCountSummary => string.Format(LocalizationService.Tr("InspectorModbusPinsSummary") ?? "{0} Out Pins", Pins.Count);

    // Live Diagnostics & Trạng thái chạy
    [ObservableProperty]
    private bool _isLiveActive = false;

    [ObservableProperty]
    private bool _isValidActive = false; // Xung báo nhận dữ liệu thành công

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
    private uint _pollCount = 0;

    public ConnectorViewModel EnableConnector { get; }
    public ConnectorViewModel TrigConnector { get; }
    public ConnectorViewModel ValidConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }
    public ConnectorViewModel ErrCodeConnector { get; }

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"MB_READ [ID:{SlaveId} | FC{(byte)FunctionCode:D2} | Addr:{StartAddress}] ({Pins.Count} Pins)"
        : $"{CustomLabel} [ID:{SlaveId} | FC{(byte)FunctionCode:D2}] ({Pins.Count} Pins)";

    public string StatusPillText
    {
        get
        {
            if (IsErrorActive) return !string.IsNullOrEmpty(LastErrorMessage) ? $"ERR: {LastErrorMessage}" : "ERR: TIMEOUT";
            if (PollCount > 0) return $"{PollCount} polls";
            return IsTriggerMode ? "READY" : $"POLL ({PollIntervalMs}ms)";
        }
    }

    public ModbusReadNodeViewModel()
    {
        Title = "MB_READ";
        Icon = "🔄";

        // Chân vào điều khiển EN & Trig
        EnableConnector = new ConnectorViewModel
        {
            Title = "EN",
            TooltipKey = "PinTooltipMbEn",
            IsInput = true,
            Node = this
        };

        TrigConnector = new ConnectorViewModel
        {
            Title = "Trig",
            TooltipKey = "PinTooltipMbReadTrig",
            IsInput = true,
            Node = this
        };

        SyncConnectors();

        // Chân ra trạng thái cố định
        ValidConnector = new ConnectorViewModel
        {
            Title = "Valid",
            TooltipKey = "PinTooltipMbValid",
            IsInput = false,
            Node = this
        };
        OutputConnectors.Add(ValidConnector);

        ErrorConnector = new ConnectorViewModel
        {
            Title = "Err",
            TooltipKey = "PinTooltipMbErr",
            IsInput = false,
            Node = this
        };
        OutputConnectors.Add(ErrorConnector);

        ErrCodeConnector = new ConnectorViewModel
        {
            Title = "Err_Code",
            TooltipKey = "PinTooltipMbErrCode",
            IsInput = false,
            Node = this
        };
        OutputConnectors.Add(ErrCodeConnector);

        Pins.CollectionChanged += OnPinsCollectionChanged;
    }

    private void OnPinsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (ModbusPinBindingViewModel oldPin in e.OldItems)
            {
                oldPin.PropertyChanged -= OnPinPropertyChanged;
                if (oldPin.Connector != null)
                {
                    OutputConnectors.Remove(oldPin.Connector);
                }
            }
        }

        if (e.NewItems != null)
        {
            foreach (ModbusPinBindingViewModel newPin in e.NewItems)
            {
                newPin.PropertyChanged += OnPinPropertyChanged;
                SynchronizeConnectorForPin(newPin);
            }
        }

        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(PinsCountSummary));
    }

    private void OnPinPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is ModbusPinBindingViewModel pin && e.PropertyName == nameof(ModbusPinBindingViewModel.Label))
        {
            if (pin.Connector != null)
            {
                pin.Connector.Title = pin.Label;
            }
        }
    }

    /// <summary>
    /// Đồng bộ chân Connector trên Canvas cho một Pin binding.
    /// </summary>
    public void SynchronizeConnectorForPin(ModbusPinBindingViewModel pin)
    {
        if (pin.Connector != null && OutputConnectors.Contains(pin.Connector))
        {
            pin.Connector.Title = pin.Label;
            pin.Connector.SetBilingualDescription(
                $"Giá trị thanh ghi Modbus offset +{pin.RegOffset} ({pin.DataType}, x{pin.Multiplier})",
                $"Modbus register offset +{pin.RegOffset} ({pin.DataType}, x{pin.Multiplier})");
            return;
        }

        var connector = new ConnectorViewModel
        {
            Id = pin.Id,
            Title = pin.Label,
            IsInput = false,
            Node = this
        };
        connector.SetBilingualDescription(
            $"Giá trị thanh ghi Modbus offset +{pin.RegOffset} ({pin.DataType}, x{pin.Multiplier})",
            $"Modbus register offset +{pin.RegOffset} ({pin.DataType}, x{pin.Multiplier})");

        pin.Connector = connector;
        OutputConnectors.Add(connector);
    }

    /// <summary>
    /// Đồng bộ hóa Input Connectors trên Canvas tương ứng với ReadMode.
    /// </summary>
    public void SyncConnectors()
    {
        InputConnectors.Clear();
        if (IsTriggerMode)
        {
            InputConnectors.Add(TrigConnector);
        }
        else
        {
            InputConnectors.Add(EnableConnector);
        }
    }
}
