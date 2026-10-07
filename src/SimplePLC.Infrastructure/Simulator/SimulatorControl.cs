namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Bề mặt điều khiển kiểm thử (Test & Simulation Control API) độc lập của MCU Reference Simulator.
/// Cho phép các ca kiểm thử và công cụ chẩn đoán thao tác cấy lỗi, đồng hồ, và xem trạng thái bộ nhớ
/// mà không làm ảnh hưởng đến giao thức Modbus chuẩn giữa App và MCU.
/// </summary>
public sealed class SimulatorControl
{
    public SimulatorFaultProfile Faults { get; }
    public SimulatorFlash Flash { get; }
    public SimulatorClock Clock { get; }
    public RuntimeTagGenerator Tags { get; }

    private readonly McuReferenceSimulator _simulator;

    internal SimulatorControl(
        McuReferenceSimulator simulator,
        SimulatorFaultProfile faults,
        SimulatorFlash flash,
        SimulatorClock clock,
        RuntimeTagGenerator tags)
    {
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
        Faults = faults ?? throw new ArgumentNullException(nameof(faults));
        Flash = flash ?? throw new ArgumentNullException(nameof(flash));
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
    }

    /// <summary>
    /// Kiểm tra xem thanh ghi tại vị trí index trong Staging Table đã thực sự được ghi hay chưa.
    /// </summary>
    public bool IsStagingRegisterWritten(int index) => _simulator.IsStagingRegisterWritten(index);

    /// <summary>
    /// Đặt giá trị Tag trực tiếp phục vụ kiểm thử.
    /// </summary>
    public void SetTagValue(ushort tagIndex, int value) => Tags.SetTagValue(tagIndex, value);

    /// <summary>
    /// Đọc giá trị Tag trực tiếp phục vụ kiểm thử.
    /// </summary>
    public int GetTagValue(ushort tagIndex) => Tags.GetTagValue(tagIndex);

    /// <summary>
    /// Tăng tick đồng hồ kiểm thử.
    /// </summary>
    public void AdvanceTicks(int seconds = 1) => Clock.AdvanceTicks(seconds);

    /// <summary>
    /// Đặt trạng thái phần cứng RTC và pin nuôi phục vụ kiểm thử.
    /// </summary>
    public void SetHardwareRtcState(bool hasHardwareRtc, bool batteryLow) => _simulator.SetHardwareRtcState(hasHardwareRtc, batteryLow);

    /// <summary>
    /// Giả lập ngắt nguồn vật lý và bật lại (Power Cycle).
    /// </summary>
    public void PowerCycle() => _simulator.PowerCycle();

    /// <summary>
    /// Giả lập kích hoạt khởi động lại phần mềm (Software Reboot).
    /// </summary>
    public void SoftwareReboot() => _simulator.SoftwareReboot();

    /// <summary>
    /// Giả lập khôi phục cài đặt gốc (Factory Reset).
    /// </summary>
    public void FactoryReset() => _simulator.FactoryReset();

    /// <summary>
    /// Thực thi một chu kỳ quét Scan Pass của MCU (danh định 10ms).
    /// </summary>
    public void ExecuteScanPass(uint deltaMs = 10) => _simulator.ExecuteScanPass(deltaMs);

    /// <summary>
    /// Đặt trực tiếp cấu hình và trạng thái của Timer block (0..7).
    /// </summary>
    public void SetTimer(int timerIndex, Protocol.Dto.FbTimerRecordDto timer)
    {
        ushort baseAddr = Protocol.Constants.ModbusRegisterMap.GetFbTimerAddress(timerIndex);
        Span<ushort> span = _simulator.Memory.InternalMutableSpan(baseAddr, Protocol.Constants.ModbusRegisterMap.FbRegistersPerBlock);
        Protocol.Codec.FunctionBlockCodec.EncodeTimer(timer, span);
    }

    /// <summary>
    /// Lấy trạng thái hiện tại của Timer block (0..7).
    /// </summary>
    public Protocol.Dto.FbTimerRecordDto GetTimer(int timerIndex)
    {
        ushort baseAddr = Protocol.Constants.ModbusRegisterMap.GetFbTimerAddress(timerIndex);
        ReadOnlySpan<ushort> span = _simulator.Memory.InternalSpan(baseAddr, Protocol.Constants.ModbusRegisterMap.FbRegistersPerBlock);
        return Protocol.Codec.FunctionBlockCodec.DecodeTimer(span);
    }

    /// <summary>
    /// Đặt trực tiếp cấu hình và trạng thái của Counter block (0..7).
    /// </summary>
    public void SetCounter(int counterIndex, Protocol.Dto.FbCounterRecordDto counter)
    {
        ushort baseAddr = Protocol.Constants.ModbusRegisterMap.GetFbCounterAddress(counterIndex);
        Span<ushort> span = _simulator.Memory.InternalMutableSpan(baseAddr, Protocol.Constants.ModbusRegisterMap.FbRegistersPerBlock);
        Protocol.Codec.FunctionBlockCodec.EncodeCounter(counter, span);
    }

    /// <summary>
    /// Lấy trạng thái hiện tại của Counter block (0..7).
    /// </summary>
    public Protocol.Dto.FbCounterRecordDto GetCounter(int counterIndex)
    {
        ushort baseAddr = Protocol.Constants.ModbusRegisterMap.GetFbCounterAddress(counterIndex);
        ReadOnlySpan<ushort> span = _simulator.Memory.InternalSpan(baseAddr, Protocol.Constants.ModbusRegisterMap.FbRegistersPerBlock);
        return Protocol.Codec.FunctionBlockCodec.DecodeCounter(span);
    }

    /// <summary>
    /// Đặt trực tiếp một quy tắc vào Active Rule Table phục vụ kiểm thử.
    /// </summary>
    public void SetActiveRule(int ruleIndex, Protocol.Dto.RuleRecordDto rule)
    {
        ushort baseAddr = (ushort)(Protocol.Constants.ModbusRegisterMap.ActiveRuleTableBaseAddress + ruleIndex * Protocol.Constants.ModbusRegisterMap.RegistersPerRule);
        Span<ushort> span = _simulator.Memory.InternalMutableSpan(baseAddr, Protocol.Constants.ModbusRegisterMap.RegistersPerRule);
        Protocol.Codec.RegisterCodec.EncodeRuleRecord(rule, span);
        ushort curCount = _simulator.Memory.InternalRead(Protocol.Constants.ModbusRegisterMap.ActiveRuleCountAddress);
        if (ruleIndex + 1 > curCount)
        {
            _simulator.Memory.InternalWrite(Protocol.Constants.ModbusRegisterMap.ActiveRuleCountAddress, (ushort)(ruleIndex + 1));
            _simulator.Memory.InternalWrite(Protocol.Constants.ModbusRegisterMap.RuleTableInfoAddress, (ushort)(ruleIndex + 1));
        }
    }
}
