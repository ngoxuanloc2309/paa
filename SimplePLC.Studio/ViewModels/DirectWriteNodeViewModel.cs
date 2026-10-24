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
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private DirectWriteTargetPin _targetPin = DirectWriteTargetPin.Do0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(ModeName))]
    [NotifyPropertyChangedFor(nameof(IsDigitalOutputMode))]
    [NotifyPropertyChangedFor(nameof(IsPwmMode))]
    [NotifyPropertyChangedFor(nameof(IsPulseTrainMode))]
    [NotifyPropertyChangedFor(nameof(IsAddressableBusMode))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private DirectWriteMode _mode = DirectWriteMode.DigitalOutput;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriggerModeName))]
    [NotifyPropertyChangedFor(nameof(TriggerModeSummaryText))]
    [NotifyPropertyChangedFor(nameof(IsCovMode))]
    [NotifyPropertyChangedFor(nameof(IsTriggerMode))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private DirectWriteTriggerMode _triggerMode = DirectWriteTriggerMode.OnTriggerRisingEdge;

    // --- COV / Deadband Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TriggerModeSummaryText))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private float _deadband = 0.5f;

    // --- Mode 0: Digital Output (DO) Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private bool _isActiveHigh = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private byte _failSafeState = 0;

    // --- Mode 1: PWM Duty Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private uint _pwmFrequencyHz = 1000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private ushort _dutyMin = 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private ushort _dutyMax = 100;

    [ObservableProperty]
    private ushort _initialDuty = 0;

    // --- Mode 2: Pulse Train (PTO) Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private uint _pulseWidthUs = 50;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private uint _maxFrequencyHz = 20000;

    // --- Mode 3: Addressable Actuator Bus Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private ushort _channelCount = 18;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(ModeSummaryDisplay))]
    [NotifyPropertyChangedFor(nameof(NarrativeText))]
    private bool _isExclusiveChannel = true;

    [ObservableProperty]
    private uint _defaultColorRgb = 0x00FF00;

    [ObservableProperty]
    private uint _timeoutMs = 300;

    public bool IsDigitalOutputMode => Mode == DirectWriteMode.DigitalOutput;
    public bool IsPwmMode => Mode == DirectWriteMode.PwmDuty;
    public bool IsPulseTrainMode => Mode == DirectWriteMode.PulseTrain;
    public bool IsAddressableBusMode => Mode == DirectWriteMode.AddressableBus;

    // Compatibility getter
    public bool IsChannelIndexedMode => IsAddressableBusMode;

    /// <summary>True khi TriggerMode = OnTriggerRisingEdge → chân Trig hiện trên canvas.</summary>
    public bool IsTriggerMode => TriggerMode == DirectWriteTriggerMode.OnTriggerRisingEdge;

    /// <summary>True khi TriggerMode = OnChangeOfValue → hiện Deadband field trong Inspector.</summary>
    public bool IsCovMode => TriggerMode == DirectWriteTriggerMode.OnChangeOfValue;

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

    public IReadOnlyList<OptionItem<DirectWriteMode>> AvailableModes { get; } = new List<OptionItem<DirectWriteMode>>
    {
        new(DirectWriteMode.DigitalOutput, LocalizationService.Tr("DirectWriteModeDo") ?? "Digital Output (DO)"),
        new(DirectWriteMode.PwmDuty, LocalizationService.Tr("DirectWriteModePwm") ?? "PWM Duty Cycle (PWM)"),
        new(DirectWriteMode.PulseTrain, LocalizationService.Tr("DirectWriteModePulse") ?? "Pulse Train Output (PTO)"),
        new(DirectWriteMode.AddressableBus, LocalizationService.Tr("DirectWriteModeBus") ?? "Addressable Actuator Bus")
    };

    public IReadOnlyList<OptionItem<DirectWriteTriggerMode>> AvailableTriggerModes { get; } = new List<OptionItem<DirectWriteTriggerMode>>
    {
        new(DirectWriteTriggerMode.Continuous, LocalizationService.Tr("DirectWriteTrigContinuous") ?? "Continuous Cyclic"),
        new(DirectWriteTriggerMode.OnTriggerRisingEdge, LocalizationService.Tr("DirectWriteTrigEdge") ?? "On Rising Edge (Trig)"),
        new(DirectWriteTriggerMode.OnChangeOfValue, LocalizationService.Tr("DirectWriteTrigCov") ?? "On Change-of-Value (COV)")
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
        DirectWriteTriggerMode.OnChangeOfValue => LocalizationService.Instance.IsVietnamese ? $"Thay đổi giá trị (COV Δ{Deadband:F2})" : $"On Change-of-Value (COV Δ{Deadband:F2})",
        _ => TriggerMode.ToString()
    };

    /// <summary>Short summary text shown on the canvas node body — matches MQTT PUB pattern.</summary>
    public string TriggerModeSummaryText => TriggerMode switch
    {
        DirectWriteTriggerMode.Continuous => LocalizationService.Tr("DirectWriteTrigContShort") ?? "Cyclic",
        DirectWriteTriggerMode.OnTriggerRisingEdge => LocalizationService.Tr("DirectWriteTrigEdgeShort") ?? "On Trig",
        DirectWriteTriggerMode.OnChangeOfValue => $"COV (Δ{Deadband:F2})",
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

    // Connectors — created once, dynamically added/removed per SynchronizeConnectors()
    public ConnectorViewModel TrigConnector { get; }
    public ConnectorViewModel ChannelConnector { get; }
    public ConnectorViewModel ParamConnector { get; }
    public ConnectorViewModel DoneConnector { get; }
    public ConnectorViewModel ErrorConnector { get; }

    public string NarrativeText
    {
        get
        {
            string pin = TargetPinName;
            string trig = TriggerModeName;

            return Mode switch
            {
                DirectWriteMode.DigitalOutput => string.Format(
                    LocalizationService.Tr("NarrPulseOutDo"),
                    pin,
                    trig,
                    IsActiveHigh ? "Active-High (1=24V, 0=0V)" : "Active-Low (0=24V, 1=0V)",
                    FailSafeState == 0 ? "0 (OFF/Safe)" : "1 (ON/Energized)"),

                DirectWriteMode.PwmDuty => string.Format(
                    LocalizationService.Tr("NarrPulseOutPwm"),
                    pin,
                    trig,
                    DutyMin,
                    DutyMax,
                    PwmFrequencyHz),

                DirectWriteMode.PulseTrain => string.Format(
                    LocalizationService.Tr("NarrPulseOutPto"),
                    pin,
                    trig,
                    MaxFrequencyHz,
                    PulseWidthUs),

                DirectWriteMode.AddressableBus => string.Format(
                    LocalizationService.Tr("NarrPulseOutBus"),
                    pin,
                    ChannelCount,
                    IsExclusiveChannel
                        ? LocalizationService.Tr("NarrPulseOutBusExcl")
                        : LocalizationService.Tr("NarrPulseOutBusIndep")),

                _ => SummaryText
            };
        }
    }

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

        // Build connector pool — never destroyed, dynamically added/removed from InputConnectors
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

        // Initial sync — populates InputConnectors correctly for default mode
        SynchronizeConnectors();
    }

    partial void OnModeChanged(DirectWriteMode value)
    {
        SynchronizeConnectors();
    }

    partial void OnTriggerModeChanged(DirectWriteTriggerMode value)
    {
        SynchronizeConnectors();
    }

    /// <summary>
    /// Physically adds/removes connectors from InputConnectors based on Mode + TriggerMode.
    /// Follows the MQTT PUB "golden sample" pattern — no orphaned pins, correct semantics per mode.
    ///
    /// Connector Matrix:
    ///   DigitalOutput + Continuous/COV  → [In]
    ///   DigitalOutput + OnTrigger       → [Trig] [In]
    ///   PwmDuty + Continuous/COV        → [Duty%]
    ///   PwmDuty + OnTrigger             → [Trig] [Duty%]
    ///   PulseTrain + Continuous         → [Pulses] [Freq]
    ///   PulseTrain + OnTrigger          → [Trig] [Pulses] [Freq]
    ///   AddressableBus (any trigger)    → [Chan] [Param]
    /// </summary>
    public void SynchronizeConnectors()
    {
        if (TrigConnector == null || ChannelConnector == null || ParamConnector == null) return;

        bool wantTrig = TriggerMode == DirectWriteTriggerMode.OnTriggerRisingEdge
                        && Mode != DirectWriteMode.AddressableBus;

        // --- Update connector labels based on mode ---
        switch (Mode)
        {
            case DirectWriteMode.DigitalOutput:
                ChannelConnector.Title = "In";
                ChannelConnector.TooltipKey = "PinTooltipDirectWriteIn";
                ChannelConnector.IsEvent = false;
                break;
            case DirectWriteMode.PwmDuty:
                ChannelConnector.Title = "Duty";
                ChannelConnector.TooltipKey = "PinTooltipDirectWriteDuty";
                ChannelConnector.IsEvent = false;
                break;
            case DirectWriteMode.PulseTrain:
                ChannelConnector.Title = "Pulses";
                ChannelConnector.TooltipKey = "PinTooltipDirectWritePulses";
                ChannelConnector.IsEvent = false;
                break;
            case DirectWriteMode.AddressableBus:
                ChannelConnector.Title = "Chan";
                ChannelConnector.TooltipKey = "PinTooltipDirectWriteChannel";
                ChannelConnector.IsEvent = false;
                break;
        }

        // --- Manage Trig connector ---
        bool hasTrig = InputConnectors.Contains(TrigConnector);
        if (wantTrig && !hasTrig)
            InputConnectors.Insert(0, TrigConnector);
        else if (!wantTrig && hasTrig)
            InputConnectors.Remove(TrigConnector);

        // --- Manage ChannelConnector (primary value input) ---
        bool hasChannel = InputConnectors.Contains(ChannelConnector);
        if (!hasChannel)
        {
            int idx = wantTrig ? 1 : 0;
            InputConnectors.Insert(Math.Min(idx, InputConnectors.Count), ChannelConnector);
        }

        // --- Manage ParamConnector (En for DO, Freq for PulseTrain, Param for AddressableBus; hidden for PwmDuty) ---
        bool wantParam = Mode == DirectWriteMode.DigitalOutput
                         || Mode == DirectWriteMode.PulseTrain
                         || Mode == DirectWriteMode.AddressableBus;
        bool hasParam = InputConnectors.Contains(ParamConnector);

        if (wantParam)
        {
            if (Mode == DirectWriteMode.DigitalOutput)
            {
                ParamConnector.Title = "En";
                ParamConnector.TooltipKey = "PinTooltipDirectWriteParam";
            }
            else if (Mode == DirectWriteMode.PulseTrain)
            {
                ParamConnector.Title = "Freq";
                ParamConnector.TooltipKey = "PinTooltipDirectWriteFreq";
            }
            else // AddressableBus
            {
                ParamConnector.Title = "Param";
                ParamConnector.TooltipKey = "PinTooltipDirectWriteParam";
            }

            if (!hasParam)
                InputConnectors.Add(ParamConnector);
        }
        else
        {
            if (hasParam)
                InputConnectors.Remove(ParamConnector);
        }
    }

    /// <summary>Legacy shim — kept for backward compatibility with any call sites; delegates to SynchronizeConnectors.</summary>
    public void UpdateConnectorTitles() => SynchronizeConnectors();

    protected override void OnLanguageChanged()
    {
        base.OnLanguageChanged();
        Title = LocalizationService.Instance.IsVietnamese ? "Bộ Phát Xung" : "Pulse Output";
        OnPropertyChanged(nameof(NarrativeText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(ModeName));
        OnPropertyChanged(nameof(TriggerModeName));
        OnPropertyChanged(nameof(TriggerModeSummaryText));
        OnPropertyChanged(nameof(StatusPillText));
        OnPropertyChanged(nameof(TargetPinDisplay));
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
