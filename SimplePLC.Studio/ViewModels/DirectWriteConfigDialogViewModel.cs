using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// Mục phân tích một trường đặc tính kỹ thuật của tín hiệu xuất ra cọc phần cứng.
/// </summary>
public class DirectWriteSignalBreakdownItem
{
    public string FieldName { get; set; } = string.Empty;
    public string RawHex { get; set; } = string.Empty;
    public string Interpretation { get; set; } = string.Empty;
    public string Tooltip { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình khối DIRECT_WRITE (Khối điều khiển 1 cọc vật lý duy nhất).
/// Thiết kế chuẩn giao diện công nghiệp Slate Gray theo chuẩn IEC/ISA-101.
/// </summary>
public partial class DirectWriteConfigDialogViewModel : ObservableObject
{
    public DirectWriteNodeViewModel Node { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(PinDescription))]
    [NotifyPropertyChangedFor(nameof(PinLoadType))]
    private DirectWriteTargetPin _targetPin;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    [NotifyPropertyChangedFor(nameof(IsDigitalOutputMode))]
    [NotifyPropertyChangedFor(nameof(IsPwmMode))]
    [NotifyPropertyChangedFor(nameof(IsPulseTrainMode))]
    [NotifyPropertyChangedFor(nameof(IsAddressableBusMode))]
    private DirectWriteMode _mode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCovMode))]
    private DirectWriteTriggerMode _triggerMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCovMode))]
    private float _deadband = 0.5f;

    public bool IsCovMode => TriggerMode == DirectWriteTriggerMode.OnChangeOfValue;

    // --- Mode 0: Digital Output (DO) Settings ---
    [ObservableProperty]
    private bool _isActiveHigh = true;

    [ObservableProperty]
    private byte _failSafeState = 0;

    // --- Mode 1: PWM Duty Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private uint _pwmFrequencyHz = 1000;

    [ObservableProperty]
    private ushort _dutyMin = 0;

    [ObservableProperty]
    private ushort _dutyMax = 100;

    [ObservableProperty]
    private ushort _initialDuty = 0;

    // --- Mode 2: Pulse Train (PTO) Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private uint _pulseWidthUs = 50;

    [ObservableProperty]
    private uint _maxFrequencyHz = 20000;

    // --- Mode 3: Addressable Actuator Bus Settings ---
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private ushort _channelCount = 18;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryBadgeText))]
    private bool _isExclusiveChannel = true;

    [ObservableProperty]
    private string _defaultColorHex = "#00FF00";

    // --- Interactive Live Test & Simulation ---
    [ObservableProperty]
    private bool _simDoValue = true;

    [ObservableProperty]
    private double _simPwmDuty = 50.0;

    [ObservableProperty]
    private uint _simPtoPulses = 1000;

    [ObservableProperty]
    private uint _simPtoFreq = 5000;

    [ObservableProperty]
    private ushort _simBusChannel = 1;

    [ObservableProperty]
    private string _simBusParamHex = "00FF00";

    [ObservableProperty]
    private string _simStatusMessage = string.Empty;

    [ObservableProperty]
    private string _simDispatchedSignal = string.Empty;

    public bool IsDigitalOutputMode => Mode == DirectWriteMode.DigitalOutput;
    public bool IsPwmMode => Mode == DirectWriteMode.PwmDuty;
    public bool IsPulseTrainMode => Mode == DirectWriteMode.PulseTrain;
    public bool IsAddressableBusMode => Mode == DirectWriteMode.AddressableBus;

    // Compatibility getters
    public bool IsChannelIndexedMode => IsAddressableBusMode;

    public string SummaryBadgeText
    {
        get
        {
            string pinName = FormatTargetPin(TargetPin);
            return Mode switch
            {
                DirectWriteMode.DigitalOutput => $"{pinName} | Digital Output (DO) | {(IsActiveHigh ? "Active-High" : "Active-Low")}",
                DirectWriteMode.PwmDuty => $"{pinName} | PWM | {PwmFrequencyHz} Hz ({DutyMin}%..{DutyMax}%)",
                DirectWriteMode.PulseTrain => $"{pinName} | PTO | {PulseWidthUs} µs (Max {MaxFrequencyHz} Hz)",
                DirectWriteMode.AddressableBus => $"{pinName} | Bus | {ChannelCount} Ch",
                _ => $"{pinName} | {Mode}"
            };
        }
    }

    public string PinDescription => $"{FormatTargetPin(TargetPin)} (24VDC)";

    public string PinLoadType => Mode switch
    {
        DirectWriteMode.DigitalOutput => LocalizationService.Instance.IsVietnamese ? "Rơ-le / Van điện từ / Đèn báo" : "Relay / Solenoid / Indicator",
        DirectWriteMode.PwmDuty => LocalizationService.Instance.IsVietnamese ? "Đèn LED Dimmer / Động cơ DC" : "LED Dimmer / DC Motor",
        DirectWriteMode.PulseTrain => LocalizationService.Instance.IsVietnamese ? "Động cơ bước / Servo (Step/Dir)" : "Stepper / Servo Driver (Step/Dir)",
        DirectWriteMode.AddressableBus => LocalizationService.Instance.IsVietnamese ? "Dải LED / Bus chấp hành nối tiếp" : "Addressable LED / Serial Bus",
        _ => "24VDC"
    };

    public int MaxDigitalOutputs { get; }

    public bool HasAvailablePins => AvailableTargetPins.Count > 0;

    public string Subtitle
    {
        get
        {
            bool isVi = LocalizationService.Instance.IsVietnamese;
            string rangeText = MaxDigitalOutputs switch
            {
                <= 0 => isVi ? "(Không có ngõ ra DO)" : "(No DO available)",
                1 => "DO0",
                _ => $"DO0..DO{Math.Min(7, MaxDigitalOutputs - 1)}"
            };

            return isVi
                ? $"Điều khiển bộ phát xung tốc độ cao, PWM và bus chấp hành thông minh trên {rangeText}"
                : $"High-speed pulse, PWM, PTO, and smart actuator bus on {rangeText}";
        }
    }

    public ObservableCollection<DirectWriteSignalBreakdownItem> SignalBreakdownItems { get; } = new();

    public IReadOnlyList<OptionItem<DirectWriteTargetPin>> AvailableTargetPins { get; }

    public IReadOnlyList<OptionItem<DirectWriteMode>> AvailableModes { get; } = new List<OptionItem<DirectWriteMode>>
    {
        new(DirectWriteMode.DigitalOutput, LocalizationService.Tr("DirectWriteModeDo") ?? "Digital Output (DO)"),
        new(DirectWriteMode.PwmDuty, LocalizationService.Tr("DirectWriteModePwm") ?? "PWM Duty Cycle (PWM)"),
        new(DirectWriteMode.PulseTrain, LocalizationService.Tr("DirectWriteModePulse") ?? "Pulse Train Output (PTO)"),
        new(DirectWriteMode.AddressableBus, LocalizationService.Tr("DirectWriteModeBus") ?? "Addressable Actuator Bus")
    };

    public IReadOnlyList<OptionItem<DirectWriteTriggerMode>> AvailableTriggerModes { get; } = new List<OptionItem<DirectWriteTriggerMode>>
    {
        new(DirectWriteTriggerMode.Continuous, LocalizationService.Tr("DirectWriteTrigContinuous") ?? "Continuous Cyclic Output"),
        new(DirectWriteTriggerMode.OnTriggerRisingEdge, LocalizationService.Tr("DirectWriteTrigEdge") ?? "On Rising Edge (Trig)"),
        new(DirectWriteTriggerMode.OnChangeOfValue, LocalizationService.Tr("DirectWriteTrigCov") ?? "On Change-of-Value (COV)")
    };

    public IReadOnlyList<OptionItem<bool>> AvailableActiveLevels { get; } = new List<OptionItem<bool>>
    {
        new(true, LocalizationService.Tr("DirectWriteActiveHigh") ?? "Active High (1 = 24V ON, 0 = OFF)"),
        new(false, LocalizationService.Tr("DirectWriteActiveLow") ?? "Active Low (0 = 24V ON, 1 = OFF)")
    };

    public IReadOnlyList<OptionItem<byte>> AvailableFailSafeStates { get; } = new List<OptionItem<byte>>
    {
        new(0, LocalizationService.Tr("DirectWriteFailSafeOff") ?? "0 (OFF / De-energized safe)"),
        new(1, LocalizationService.Tr("DirectWriteFailSafeOn") ?? "1 (ON / Energized safe)")
    };

    public event Action<bool>? RequestClose;

    public DirectWriteConfigDialogViewModel(DirectWriteNodeViewModel node, int? maxDigitalOutputs = null)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));

        MaxDigitalOutputs = maxDigitalOutputs.HasValue && maxDigitalOutputs.Value >= 0
            ? maxDigitalOutputs.Value
            : ResolveHardwareDigitalOutputCount();

        var pins = new List<OptionItem<DirectWriteTargetPin>>();
        int limit = Math.Clamp(MaxDigitalOutputs, 0, 8);
        for (int i = 0; i < limit; i++)
        {
            var pinEnum = (DirectWriteTargetPin)i;
            pins.Add(new OptionItem<DirectWriteTargetPin>(pinEnum, $"DO{i}"));
        }
        AvailableTargetPins = pins;

        TargetPin = node.TargetPin;
        if ((int)TargetPin >= limit && limit > 0)
        {
            TargetPin = DirectWriteTargetPin.Do0;
        }

        Mode = node.Mode;
        TriggerMode = node.TriggerMode;
        Deadband = node.Deadband;

        IsActiveHigh = node.IsActiveHigh;
        FailSafeState = node.FailSafeState;

        PwmFrequencyHz = Math.Max((uint)10, node.PwmFrequencyHz);
        DutyMin = node.DutyMin;
        DutyMax = Math.Max((ushort)1, node.DutyMax);
        InitialDuty = node.InitialDuty;

        PulseWidthUs = Math.Max((uint)1, node.PulseWidthUs);
        MaxFrequencyHz = Math.Max((uint)100, node.MaxFrequencyHz);

        ChannelCount = Math.Max((ushort)1, node.ChannelCount);
        IsExclusiveChannel = node.IsExclusiveChannel;
        DefaultColorHex = $"#{node.DefaultColorRgb:X6}";

        GenerateSignalPreview();
    }

    public static int ResolveHardwareDigitalOutputCount()
    {
        var session = AppServices.Instance.SessionManager.CurrentSession;
        if (session?.ResourceInfo != null && session.ResourceInfo.DigitalOutputCount > 0)
        {
            return session.ResourceInfo.DigitalOutputCount;
        }

        var product = AppServices.Instance.CurrentProduct;
        if (product?.Resources != null && product.Resources.DigitalOutputs > 0)
        {
            return product.Resources.DigitalOutputs;
        }

        return 8;
    }

    partial void OnTargetPinChanged(DirectWriteTargetPin value) => GenerateSignalPreview();
    partial void OnModeChanged(DirectWriteMode value) => GenerateSignalPreview();
    partial void OnIsActiveHighChanged(bool value) => GenerateSignalPreview();
    partial void OnFailSafeStateChanged(byte value) => GenerateSignalPreview();
    partial void OnPwmFrequencyHzChanged(uint value) => GenerateSignalPreview();
    partial void OnPulseWidthUsChanged(uint value) => GenerateSignalPreview();
    partial void OnChannelCountChanged(ushort value) => GenerateSignalPreview();
    partial void OnIsExclusiveChannelChanged(bool value) => GenerateSignalPreview();

    partial void OnSimDoValueChanged(bool value) => GenerateSignalPreview();
    partial void OnSimPwmDutyChanged(double value) => GenerateSignalPreview();
    partial void OnSimPtoPulsesChanged(uint value) => GenerateSignalPreview();
    partial void OnSimPtoFreqChanged(uint value) => GenerateSignalPreview();
    partial void OnSimBusChannelChanged(ushort value) => GenerateSignalPreview();
    partial void OnSimBusParamHexChanged(string value) => GenerateSignalPreview();

    public static string FormatTargetPin(DirectWriteTargetPin pin) => pin switch
    {
        DirectWriteTargetPin.Do0 => "DO0",
        DirectWriteTargetPin.Do1 => "DO1",
        DirectWriteTargetPin.Do2 => "DO2",
        DirectWriteTargetPin.Do3 => "DO3",
        DirectWriteTargetPin.Do4 => "DO4",
        DirectWriteTargetPin.Do5 => "DO5",
        DirectWriteTargetPin.Do6 => "DO6",
        DirectWriteTargetPin.Do7 => "DO7",
        _ => $"DO{(byte)pin}"
    };

    [RelayCommand]
    public void GenerateSignalPreview()
    {
        bool isVi = LocalizationService.Instance.IsVietnamese;
        string pinDisplay = FormatTargetPin(TargetPin);

        SimStatusMessage = isVi
            ? $"SẴN SÀNG: Tín hiệu xuất trực tiếp tới chân [{pinDisplay}], độ trễ đáp ứng vi điều khiển < 1 chu kỳ quét."
            : $"READY: Direct deterministic output to pin [{pinDisplay}], microcontroller latency < 1 scan cycle.";

        SignalBreakdownItems.Clear();

        if (Mode == DirectWriteMode.DigitalOutput)
        {
            string stateText = SimDoValue ? (isVi ? "MỨC CAO (24V - ĐÓNG TẢI)" : "HIGH (24VDC - ENERGIZED)") : (isVi ? "MỨC THẤP (0V - NGẮT TẢI)" : "LOW (0VDC - DE-ENERGIZED)");
            SimDispatchedSignal = $"PIN {pinDisplay} ➔ [{stateText}] | POLARITY: {(IsActiveHigh ? "ACTIVE_HIGH" : "ACTIVE_LOW")}";

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chân phần cứng đích:" : "Target Hardware Pin:",
                RawHex = pinDisplay,
                Interpretation = isVi ? $"Chân ngõ ra số {pinDisplay} trên bo mạch (Local IO)" : $"Local digital output pin {pinDisplay}",
                Tooltip = isVi ? "Cọc ngõ ra transistor điều khiển trực tiếp trên vi điều khiển." : "Direct microcontroller transistor output pin."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chế độ hoạt động:" : "Operating Mode:",
                RawHex = "DO_01",
                Interpretation = isVi ? "Ngõ ra số logic trực tiếp 0/1 (Relay / Van ON-OFF)" : "Digital Output (DO 0/1: Relay / Solenoid)",
                Tooltip = isVi ? "Chế độ phát tín hiệu số bật/tắt tiếp điểm." : "Binary on/off contact output mode."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Trạng thái mô phỏng:" : "Simulated Output:",
                RawHex = SimDoValue ? "HIGH (1)" : "LOW (0)",
                Interpretation = SimDoValue
                    ? (isVi ? "Xuất điện áp 24VDC cấp nguồn cho tải" : "Dispatches 24VDC power to load")
                    : (isVi ? "Ngắt điện áp 0VDC đưa tải về nghỉ" : "Cuts voltage to 0VDC (de-energized)"),
                Tooltip = isVi ? "Trạng thái điện áp xuất ra cọc thực tế khi mô phỏng." : "Simulated voltage output on the physical pin."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Mức logic tích cực:" : "Active Polarity:",
                RawHex = IsActiveHigh ? "HIGH_ON" : "LOW_ON",
                Interpretation = IsActiveHigh
                    ? (isVi ? "Mức 1 = Bật tải, Mức 0 = Tắt tải" : "Logic 1 = Active, Logic 0 = Inactive")
                    : (isVi ? "Mức 0 = Bật tải, Mức 1 = Tắt tải" : "Logic 0 = Active, Logic 1 = Inactive"),
                Tooltip = isVi ? "Quy định mức điện áp 24V ứng với giá trị logic 1 hay 0." : "Specifies whether 24V corresponds to logic 1 or 0."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Trạng thái an toàn:" : "Fail-Safe State:",
                RawHex = $"STATE_{FailSafeState}",
                Interpretation = FailSafeState == 0
                    ? (isVi ? "Ngắt tải khi khởi động hoặc có lỗi an toàn" : "De-energized safe upon startup or fault")
                    : (isVi ? "Duy trì đóng tải an toàn" : "Maintain energized state"),
                Tooltip = isVi ? "Bảo vệ an toàn phần cứng khi PLC dừng quét hoặc mất kết nối." : "Hardware safety interlock upon PLC STOP or fault."
            });
        }
        else if (Mode == DirectWriteMode.PwmDuty)
        {
            SimDispatchedSignal = $"PIN {pinDisplay} ➔ [PWM: {SimPwmDuty:F1}% DUTY] @ {PwmFrequencyHz} Hz | VOLTAGE: {(24.0 * SimPwmDuty / 100.0):F1} VDC";

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chân phần cứng đích:" : "Target Hardware Pin:",
                RawHex = pinDisplay,
                Interpretation = isVi ? $"Chân ngõ ra điều chế xung {pinDisplay}" : $"Pulse width modulated terminal {pinDisplay}",
                Tooltip = isVi ? "Cọc ngõ ra transistor băm xung tốc độ cao." : "High-speed PWM transistor output pin."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chế độ hoạt động:" : "Operating Mode:",
                RawHex = "PWM_DUTY",
                Interpretation = isVi ? "Điều chế độ rộng xung công suất (0..100%)" : "PWM Power Duty Cycle Modulation",
                Tooltip = isVi ? "Điều chế tỷ lệ thời gian đóng ngắt để thay đổi điện áp hiệu dụng." : "Modulates duty cycle to vary effective DC voltage."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Tần số sóng mang:" : "Carrier Frequency:",
                RawHex = $"{PwmFrequencyHz} Hz",
                Interpretation = isVi ? $"Chu kỳ xung T = {(1000.0 / Math.Max(1, PwmFrequencyHz)):F2} ms" : $"Pulse period T = {(1000.0 / Math.Max(1, PwmFrequencyHz)):F2} ms",
                Tooltip = isVi ? "Tần số chuyển mạch đóng ngắt transistor của vi điều khiển." : "Microcontroller transistor switching frequency."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Độ rộng xung (Duty):" : "Active Duty Cycle:",
                RawHex = $"{SimPwmDuty:F1}%",
                Interpretation = isVi ? $"Điện áp trung bình cấp tải: {(24.0 * SimPwmDuty / 100.0):F1} VDC" : $"Equivalent average voltage: {(24.0 * SimPwmDuty / 100.0):F1} VDC",
                Tooltip = isVi ? "Phần trăm chu kỳ mà cọc ở mức điện áp cao." : "Percentage of cycle period with high voltage level."
            });
        }
        else if (Mode == DirectWriteMode.PulseTrain)
        {
            SimDispatchedSignal = $"PIN {pinDisplay} ➔ [PTO: {SimPtoPulses} PULSES @ {SimPtoFreq} Hz] | PULSE WIDTH: {PulseWidthUs} µs";

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chân phần cứng đích:" : "Target Hardware Pin:",
                RawHex = pinDisplay,
                Interpretation = isVi ? $"Chân phát xung tốc độ cao {pinDisplay}" : $"High-speed pulse train terminal {pinDisplay}",
                Tooltip = isVi ? "Cọc phát xung tốc độ cao điều khiển động cơ bước/servo." : "High-speed PTO terminal for stepper/servo driver."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chế độ hoạt động:" : "Operating Mode:",
                RawHex = "PTO_TRAIN",
                Interpretation = isVi ? "Phát chuỗi xung bước định lượng (Stepper / Servo)" : "Quantized Pulse Train Output (Stepper/Servo)",
                Tooltip = isVi ? "Phát đúng số lượng xung yêu cầu để dịch chuyển vị trí chính xác." : "Emits exact pulse quantity for precise motion positioning."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Số lượng xung phát:" : "Pulse Count:",
                RawHex = $"{SimPtoPulses}",
                Interpretation = isVi ? $"Định lượng {SimPtoPulses} bước dịch chuyển" : $"Quantized motion of {SimPtoPulses} step increments",
                Tooltip = isVi ? "Tổng số xung cấp cho driver." : "Total pulse count dispatched to motor driver."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Độ rộng xung cơ sở:" : "Base Pulse Width:",
                RawHex = $"{PulseWidthUs} µs",
                Interpretation = isVi ? $"Độ rộng xung tối thiểu đảm bảo opto thu nhận" : $"Minimum pulse width for optocoupler timing",
                Tooltip = isVi ? "Thời gian mức cao tối thiểu của một xung để cách ly quang nhận được." : "Minimum high-time width for optocoupler reception."
            });
        }
        else // AddressableBus
        {
            var cleanParam = SimBusParamHex?.Trim().TrimStart('#') ?? "000000";
            SimDispatchedSignal = $"PIN {pinDisplay} ➔ [BUS DISPATCH: CH {SimBusChannel:D2}] | PARAM: 0x{cleanParam.ToUpperInvariant()}";

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chân phần cứng đích:" : "Target Hardware Pin:",
                RawHex = pinDisplay,
                Interpretation = isVi ? $"Chân truyền dữ liệu nối tiếp {pinDisplay}" : $"Serial data bus terminal {pinDisplay}",
                Tooltip = isVi ? "Cọc bus truyền thông dữ liệu nối tiếp một dây." : "Single-wire serial bus communication terminal."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chế độ hoạt động:" : "Operating Mode:",
                RawHex = "BUS_ADDR",
                Interpretation = isVi ? "Bus chấp hành định địa chỉ (Addressable Serial Bus)" : "Addressable Actuator Serial Bus",
                Tooltip = isVi ? "Giao tiếp định địa chỉ cho nhiều cơ cấu chấp hành trên cùng 1 chân." : "Addressable multi-drop protocol on single terminal."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Kênh kích hoạt thử:" : "Active Test Channel:",
                RawHex = $"CH {SimBusChannel:D2}",
                Interpretation = isVi ? $"Kênh điều khiển logic #{SimBusChannel} / {ChannelCount}" : $"Active logic channel #{SimBusChannel} of {ChannelCount}",
                Tooltip = isVi ? "Chỉ số kênh chấp hành được nhắm tới." : "Target actuator channel index."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Chính sách độc quyền:" : "Exclusive Policy:",
                RawHex = IsExclusiveChannel ? "EXCL_ON" : "INDEP_ON",
                Interpretation = IsExclusiveChannel
                    ? (isVi ? "Tự động ngắt toàn bộ kênh khác khi kích hoạt" : "Auto deactivates all other channels on trigger")
                    : (isVi ? "Cho phép nhiều kênh cùng hoạt động" : "Allows multiple simultaneous active channels"),
                Tooltip = isVi ? "Chế độ tự động dập tắt các kênh khác khi có kênh kích hoạt." : "Single-active channel policy."
            });

            SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
            {
                FieldName = isVi ? "Tham số tải xuất:" : "Dispatched Payload:",
                RawHex = $"0x{cleanParam.ToUpperInvariant()}",
                Interpretation = isVi ? "Tham số điều khiển ngõ ra (RGB Color / Mã lệnh)" : "Dispatched parameter (Color / Command)",
                Tooltip = isVi ? "Dữ liệu tham số hoặc mã màu gửi tới cơ cấu chấp hành." : "Parameter or color data dispatched to actuator."
            });
        }

        SignalBreakdownItems.Add(new DirectWriteSignalBreakdownItem
        {
            FieldName = isVi ? "Độ trễ truyền động:" : "Execution Latency:",
            RawHex = "< 1 Cycle",
            Interpretation = isVi ? "Xuất tức thì thanh ghi phần cứng, không trễ mạng" : "Zero network overhead, instant hardware dispatch",
            Tooltip = isVi ? "Độ trễ thực thi phần cứng tức thì tại chân vi điều khiển." : "Instant hardware pin update latency."
        });
    }

    public void Apply()
    {
        Node.TargetPin = TargetPin;
        Node.Mode = Mode;
        Node.TriggerMode = TriggerMode;
        Node.Deadband = Deadband;

        Node.IsActiveHigh = IsActiveHigh;
        Node.FailSafeState = FailSafeState;

        Node.PwmFrequencyHz = Math.Max((uint)10, PwmFrequencyHz);
        Node.DutyMin = DutyMin;
        Node.DutyMax = Math.Max((ushort)1, DutyMax);
        Node.InitialDuty = InitialDuty;

        Node.PulseWidthUs = Math.Max((uint)1, PulseWidthUs);
        Node.MaxFrequencyHz = Math.Max((uint)100, MaxFrequencyHz);

        Node.ChannelCount = Math.Max((ushort)1, ChannelCount);
        Node.IsExclusiveChannel = IsExclusiveChannel;

        var clean = DefaultColorHex.Trim().TrimStart('#');
        if (uint.TryParse(clean, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint parsedColor))
        {
            Node.DefaultColorRgb = parsedColor;
        }

        Node.UpdateConnectorTitles();
    }

    [RelayCommand]
    private void Save()
    {
        Apply();
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel() => RequestClose?.Invoke(false);
}
