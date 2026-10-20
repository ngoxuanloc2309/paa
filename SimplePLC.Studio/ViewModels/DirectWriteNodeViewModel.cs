using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel cho khối đồ họa DIRECT_WRITE (Xuất trực tiếp cọc phần cứng).
/// Đại diện cho 1 cọc ngõ ra số vật lý duy nhất (DO0..DO7).
/// Hỗ trợ 4 chế độ vận hành:
/// 1. Digital Output (DO 0/1: Relay, Van từ, Đèn báo ON/OFF).
/// 2. PWM Duty Cycle (Điều chế xung 0..100%: Điều tốc, Van tỉ lệ, Độ sáng).
/// 3. Pulse Train Output (PTO: Phát xung bước cho Stepper / Servo).
/// 4. Addressable Actuator Bus (Thiết bị chấp hành định địa chỉ nối tiếp).
/// </summary>
public partial class DirectWriteNodeViewModel : GraphNodeViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(TargetPinDisplay))]
    [NotifyPropertyChangedFor(nameof(TargetPinName))]
    private DirectWriteTargetPin _targetPin = DirectWriteTargetPin.Do0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(ModeName))]
    [NotifyPropertyChangedFor(nameof(IsDigitalOutputMode))]
    [NotifyPropertyChangedFor(nameof(IsPwmMode))]
    [NotifyPropertyChangedFor(nameof(IsPulseTrainMode))]
    [NotifyPropertyChangedFor(nameof(IsAddressableBusMode))]
    private DirectWriteMode _mode = DirectWriteMode.DigitalOutput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriggerModeName))]
    private DirectWriteTriggerMode _triggerMode = DirectWriteTriggerMode.OnTriggerRisingEdge;

    // --- Mode 0: Digital Output (DO) Settings ---
    [ObservableProperty]
    private bool _isActiveHigh = true;

    [ObservableProperty]
    private byte _failSafeState = 0;

    // --- Mode 1: PWM Duty Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    private uint _pwmFrequencyHz = 1000;

    [ObservableProperty]
    private ushort _dutyMin = 0;

    [ObservableProperty]
    private ushort _dutyMax = 100;

    [ObservableProperty]
    private ushort _initialDuty = 0;

    // --- Mode 2: Pulse Train (PTO) Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    private uint _pulseWidthUs = 50;

    [ObservableProperty]
    private uint _maxFrequencyHz = 20000;

    // --- Mode 3: Addressable Actuator Bus Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    private ushort _channelCount = 18;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    private bool _isExclusiveChannel = true;

    [ObservableProperty]
    private uint _defaultColorRgb = 0x00FF00;

    [ObservableProperty]
    private uint _timeoutMs = 300;

    public bool IsDigitalOutputMode => Mode == DirectWriteMode.DigitalOutput;
    public bool IsPwmMode => Mode == DirectWriteMode.PwmDuty;
    public bool IsPulseTrainMode => Mode == DirectWriteMode.PulseTrain;
    public bool IsAddressableBusMode => Mode == DirectWriteMode.AddressableBus;

    // Compatibility getters
    public bool IsChannelIndexedMode => IsAddressableBusMode;

    public bool IsHardwarePinOutOfRange => (byte)TargetPin >= DirectWriteConfigDialogViewModel.ResolveHardwareDigitalOutputCount();

    public string TargetPinDisplay => IsHardwarePinOutOfRange 
        ? $"PIN: {TargetPinName} (!)" 
        : $"PIN: {TargetPinName}";

    public string TargetPinName => TargetPin switch
    {
        DirectWriteTargetPin.Do0 => "DO0",
        DirectWriteTargetPin.Do1 => "DO1",
        DirectWriteTargetPin.Do2 => "DO2",
        DirectWriteTargetPin.Do3 => "DO3",
        DirectWriteTargetPin.Do4 => "DO4",
        DirectWriteTargetPin.Do5 => "DO5",
        DirectWriteTargetPin.Do6 => "DO6",
        DirectWriteTargetPin.Do7 => "DO7",
        _ => $"DO{(byte)TargetPin}"
    };

    public string ModeName => Mode switch
    {
        DirectWriteMode.DigitalOutput => LocalizationService.Instance.IsVietnamese ? "Ngõ ra số (Digital Output - DO)" : "Digital Output (DO)",
        DirectWriteMode.PwmDuty => LocalizationService.Instance.IsVietnamese ? "Điều chế xung (PWM Duty Cycle)" : "PWM Duty Cycle",
        DirectWriteMode.PulseTrain => LocalizationService.Instance.IsVietnamese ? "Phát chuỗi xung (Pulse Train - PTO)" : "Pulse Train Output (PTO)",
        DirectWriteMode.AddressableBus => LocalizationService.Instance.IsVietnamese ? "Bus chấp hành nối tiếp (Addressable Bus)" : "Addressable Actuator Bus",
        _ => Mode.ToString()
    };

    public string ModeSummaryDisplay => Mode switch
    {
        DirectWriteMode.DigitalOutput => "DO [0/1]",
        DirectWriteMode.PwmDuty => $"PWM: {PwmFrequencyHz}Hz",
        DirectWriteMode.PulseTrain => $"PULSE: {PulseWidthUs}µs",
        DirectWriteMode.AddressableBus => $"CH [1..{ChannelCount}]" + (IsExclusiveChannel ? " (Excl)" : ""),
        _ => Mode.ToString()
    };

    public string TriggerModeName => TriggerMode switch
    {
        DirectWriteTriggerMode.Continuous => LocalizationService.Instance.IsVietnamese ? "Chu kỳ quét (Continuous Cyclic)" : "Continuous Cyclic",
        DirectWriteTriggerMode.OnTriggerRisingEdge => LocalizationService.Instance.IsVietnamese ? "Kích hoạt sườn lên (Trig)" : "On Rising Edge (Trig)",
        _ => TriggerMode.ToString()
    };

    // Live Diagnostics
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
    public ConnectorViewModel ChannelConnector { get; }
    public ConnectorViewModel ParamConnector { get; }
    public ConnectorViewModel DoneConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }

    public override string SummaryText => string.IsNullOrWhiteSpace(CustomLabel)
        ? $"PULSE_OUT [{TargetPinDisplay} | {ModeSummaryDisplay}]"
        : CustomLabel;

    public string StatusPillText
    {
        get
        {
            if (IsHardwarePinOutOfRange)
                return LocalizationService.Instance.IsVietnamese ? "LỖI: VƯỢT QUÁ DO" : "ERR: PIN EXCEEDED";

            if (IsErrorActive)
                return string.IsNullOrWhiteSpace(LastErrorMessage)
                    ? (LocalizationService.Instance.IsVietnamese ? "LỖI" : "ERR")
                    : $"ERR: {LastErrorMessage}";

            if (IsBusyActive)
                return LocalizationService.Instance.IsVietnamese ? "ĐANG PHÁT..." : "PULSING...";

            if (IsDoneActive)
                return LocalizationService.Instance.IsVietnamese ? $"HOÀN TẤT ({WriteCount})" : $"DONE ({WriteCount})";

            return LocalizationService.Instance.IsVietnamese ? "SẴN SÀNG" : "IDLE";
        }
    }

    public DirectWriteNodeViewModel(DirectWriteTargetPin targetPin = DirectWriteTargetPin.Do0, DirectWriteMode mode = DirectWriteMode.DigitalOutput)
    {
        _targetPin = targetPin;
        _mode = mode;
        Title = LocalizationService.Instance.IsVietnamese ? "Bộ Phát Xung" : "Pulse Output";

        // Input Connectors
        TrigConnector = new ConnectorViewModel
        {
            Title = "Trig",
            TooltipKey = "PinTooltipDirectWriteTrig",
            IsInput = true,
            IsEvent = true,
            Node = this
        };
        ChannelConnector = new ConnectorViewModel
        {
            Title = "Chan",
            TooltipKey = "PinTooltipDirectWriteChannel",
            IsInput = true,
            IsEvent = false,
            Node = this
        };
        ParamConnector = new ConnectorViewModel
        {
            Title = "Param",
            TooltipKey = "PinTooltipDirectWriteParam",
            IsInput = true,
            IsEvent = false,
            Node = this
        };

        InputConnectors.Add(TrigConnector);
        InputConnectors.Add(ChannelConnector);
        InputConnectors.Add(ParamConnector);

        // Output Connectors
        DoneConnector = new ConnectorViewModel
        {
            Title = "Done",
            TooltipKey = "PinTooltipDirectWriteDone",
            IsInput = false,
            IsEvent = true,
            Node = this
        };
        ErrorConnector = new ConnectorViewModel
        {
            Title = "Err",
            TooltipKey = "PinTooltipDirectWriteErr",
            IsInput = false,
            IsEvent = false,
            Node = this
        };

        OutputConnectors.Add(DoneConnector);
        OutputConnectors.Add(ErrorConnector);

        UpdateConnectorTitles();
    }

    partial void OnModeChanged(DirectWriteMode value)
    {
        UpdateConnectorTitles();
    }

    public void UpdateConnectorTitles()
    {
        if (ChannelConnector == null || ParamConnector == null) return;

        if (Mode == DirectWriteMode.DigitalOutput)
        {
            ChannelConnector.Title = "In";
            ParamConnector.Title = "En";
        }
        else if (Mode == DirectWriteMode.PwmDuty)
        {
            ChannelConnector.Title = "Duty";
            ParamConnector.Title = "En";
        }
        else if (Mode == DirectWriteMode.PulseTrain)
        {
            ChannelConnector.Title = "Pulses";
            ParamConnector.Title = "Freq";
        }
        else // AddressableBus / ChannelIndexed
        {
            ChannelConnector.Title = "Chan";
            ParamConnector.Title = "Param";
        }
    }

    [RelayCommand]
    private void OpenConfigDialog()
    {
        int availableDos = DirectWriteConfigDialogViewModel.ResolveHardwareDigitalOutputCount();
        var vm = new DirectWriteConfigDialogViewModel(this, availableDos);
        var dlg = new Views.DirectWriteConfigDialogView(vm)
        {
            Owner = System.Windows.Application.Current?.MainWindow
        };
        dlg.ShowDialog();
    }
}
