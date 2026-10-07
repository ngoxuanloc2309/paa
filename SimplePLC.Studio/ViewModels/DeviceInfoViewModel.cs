using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.ViewModels;

public sealed class DeviceInfoViewModel : ObservableObject
{
    public string DeviceModel { get; init; } = "SynaptiX Remote I/O";
    public string DeviceClassText { get; init; } = "Remote I/O";
    public string DeviceVariantText { get; init; } = "8DI / 8DO / 4AI";
    public string HwVersion { get; init; } = "1.0.0";
    public string FwVersion { get; init; } = "1.7.0";
    public string WireProfileText { get; init; } = "Wire Profile V2";
    public string RuleFormatText { get; init; } = "v2.0";

    public int DigitalInputs { get; init; } = 8;
    public int DigitalOutputs { get; init; } = 8;
    public int AnalogInputs { get; init; } = 4;
    public int VirtualFlags { get; init; } = 32;
    public int VirtualRegs { get; init; } = 32;
    public int RetainRegs { get; init; } = 32;
    public int Counters { get; init; } = 8;
    public int MaxRules { get; init; } = 100;

    public string DigitalInputsText => DigitalInputs > 0 ? $"{DigitalInputs} (DI0 – DI{DigitalInputs - 1})" : "0";
    public string DigitalOutputsText => DigitalOutputs > 0 ? $"{DigitalOutputs} (DO0 – DO{DigitalOutputs - 1})" : "0";
    public string AnalogInputsText => AnalogInputs > 0 ? $"{AnalogInputs} (AI0 – AI{AnalogInputs - 1})" : "0";
    public string VirtualFlagsText => VirtualFlags > 0 ? $"{VirtualFlags} (VFLAG0 – VFLAG{VirtualFlags - 1})" : "0";
    public string VirtualRegsText => VirtualRegs > 0 ? $"{VirtualRegs} (VREG0 – VREG{VirtualRegs - 1})" : "0";
    public string RetainRegsText => RetainRegs > 0 ? $"{RetainRegs} (RETAIN0 – RETAIN{RetainRegs - 1})" : "0";
    public string CountersText => Counters > 0 ? $"{Counters} (C0 – C{Counters - 1})" : "0";
    public string MaxRulesText => $"{MaxRules} rules";

    public string PortName { get; init; } = "COM10";
    public int BaudRate { get; init; } = 115200;
    public byte SlaveId { get; init; } = 1;
    public string ProtocolText { get; init; } = "Modbus RTU (FC03 / FC16)";
    public bool IsOnline { get; init; } = true;

    public string ConnectionStatusText => IsOnline
        ? LocalizationService.Instance["DeviceInfoStatusOnline"]
        : LocalizationService.Instance["DeviceInfoStatusOffline"];
}
