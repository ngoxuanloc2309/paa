using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Models;

namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Trình giả lập vi điều khiển MCU STM32 chuẩn tham chiếu (MCU Reference Simulator).
/// Mô phỏng 100% observable behavior tại ranh giới giao tiếp Modbus RTU / Native USB CDC V1.7:
/// - Phân định ranh giới chặt chẽ giữa Volatile RAM và Non-Volatile Flash.
/// - Kiểm tra tính hợp lệ của địa chỉ và quyền đọc/ghi nghiêm ngặt như firmware NanoModbus.
/// - Máy trạng thái Staging & Atomic Commit với Written Range Tracker hoàn chỉnh.
/// - Phân biệt tường minh giữa Software Reboot (SOFTWARE) và Power Cycle (POWER_ON).
/// - Tách biệt hoàn toàn API giao thức MCU và API điều khiển kiểm thử (SimulatorControl).
/// </summary>
public sealed class McuReferenceSimulator
{
    private readonly SimulatorRegisterMemory _memory = new();
    private readonly SimulatorFlash _flash = new();
    private readonly SimulatorClock _clock = new();
    private readonly RuntimeTagGenerator _tagGenerator;
    private readonly SimulatorFaultProfile _faults = new();

    private readonly bool[] _stagingRegistersWritten = new bool[ModbusRegisterMap.StagingRuleTableMaxLength];
    private ushort _stagedRuleCount = 0;
    private ushort _activeVersion = 0;
    private bool _hasCommittedCurrentStaging = false;

    private SPLC_ResetReason _resetReason = SPLC_ResetReason.POWER_ON;
    private int _writeTransactionCount = 0;
    private readonly ushort _wireProfile;

    private readonly bool[] _prevCounterCu = new bool[ModbusRegisterMap.FbMaxCounters];
    private readonly bool[] _prevCounterCd = new bool[ModbusRegisterMap.FbMaxCounters];
    private readonly bool[] _prevCounterR = new bool[ModbusRegisterMap.FbMaxCounters];
    private readonly bool[] _prevTimerIn = new bool[ModbusRegisterMap.FbMaxTimers];

    private uint _epochUtcSeconds = 0;
    private short _tzOffsetMinutes = 420; // default UTC+7
    private bool _rtcSynced = false;
    private bool _hasHardwareRtc = false;
    private bool _batteryLow = false;
    private uint _rtcAccumulatorMs = 0;
    private readonly int[] _lastTriggeredMinute = new int[ModbusRegisterMap.MaxRules];
    private readonly int[] _prevTagValues = new int[ModbusRegisterMap.MaxRuntimeTags];
    private readonly uint[] _ruleDwellElapsedMs = new uint[ModbusRegisterMap.MaxRules];
    private readonly uint[] _ruleIntervalElapsedMs = new uint[ModbusRegisterMap.MaxRules];

    internal SimulatorRegisterMemory Memory => _memory;

    public bool IsOnline { get; private set; } = true;
    public bool IsRebooting { get; private set; } = false;
    public bool SupportsFloatRules { get; set; } = true;

    /// <summary>
    /// Bề mặt điều khiển kiểm thử độc lập (Test & Simulation Control API).
    /// </summary>
    public SimulatorControl Control { get; }

    public DeviceResourceInfoDto GetResourceInfo()
    {
        if (_faults.OverrideResourceInfo != null)
        {
            return _faults.OverrideResourceInfo;
        }

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.DeviceResourceInfoLength];
        for (int i = 0; i < ModbusRegisterMap.DeviceResourceInfoLength; i++)
        {
            buffer[i] = _memory.InternalRead((ushort)(ModbusRegisterMap.DeviceResourceInfoAddress + i));
        }
        return RegisterCodec.DecodeDeviceResourceInfo(buffer);
    }

    public TagLayoutMap GetTagLayout()
    {
        var res = GetResourceInfo();
        return ModbusRegisterMap.ComputeLayout(
            res.DigitalInputCount,
            res.DigitalOutputCount,
            res.AnalogInputCount,
            res.VirtualFlagCount,
            res.VirtualRegisterCount,
            res.RetentiveRegisterCount,
            res.CounterCount);
    }

    public McuReferenceSimulator(ushort wireProfile = 2)
    {
        _wireProfile = wireProfile;
        _tagGenerator = new RuntimeTagGenerator(_memory);
        Control = new SimulatorControl(this, _faults, _flash, _clock, _tagGenerator);

        InitializeHardwareDescriptor();
        InitializeDefaultState();
    }

    private void InitializeHardwareDescriptor()
    {
        // 0x0000 - 0x0009: Bất biến phần cứng vĩnh cửu
        _memory.InternalWrite(0, (ushort)SPLC_DeviceClass.REMOTE_IO);
        _memory.InternalWrite(1, (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI);
        _memory.InternalWrite(2, 1); // HW Major
        _memory.InternalWrite(3, 0); // HW Minor
        _memory.InternalWrite(4, 0); // HW Patch

        if (_wireProfile >= 2)
        {
            _memory.InternalWrite(5, 2); // FW Major
            _memory.InternalWrite(6, 0); // FW Minor
            _memory.InternalWrite(7, 0); // FW Patch
            _memory.InternalWrite(8, 2); // Protocol Version (V2.0)
            _memory.InternalWrite(9, 7); // Rule Format Version (V2.0 32-byte rules)
        }
        else
        {
            _memory.InternalWrite(5, 1); // FW Major
            _memory.InternalWrite(6, 7); // FW Minor
            _memory.InternalWrite(7, 0); // FW Patch
            _memory.InternalWrite(8, 1); // Protocol Version (v1.7)
            _memory.InternalWrite(9, 1); // Rule Format Version (v1.7)
        }

        // 0x0020 - 0x0029: Device Resource Info (Wire Profile V1 / Contract V1.9)
        var defaultResourceInfo = DeviceResourceInfoDto.CreateRemoteIo8Di8Do4Ai();
        defaultResourceInfo.WireProfile = _wireProfile;
        var resourceRegs = RegisterCodec.EncodeDeviceResourceInfo(defaultResourceInfo);
        for (int i = 0; i < resourceRegs.Length; i++)
        {
            _memory.InternalWrite((ushort)(ModbusRegisterMap.DeviceResourceInfoAddress + i), resourceRegs[i]);
        }

        // 0x0A08 - 0x0A09: HardwareCapabilities (32-bit uint32, Big-Endian)
        if (_wireProfile >= 2)
        {
            var caps = SPLC_HardwareCapabilities.BASE_LOGIC |
                       SPLC_HardwareCapabilities.RETENTIVE |
                       SPLC_HardwareCapabilities.RTC |
                       SPLC_HardwareCapabilities.ETHERNET |
                       SPLC_HardwareCapabilities.WIFI |
                       SPLC_HardwareCapabilities.MQTT_PUB |
                       SPLC_HardwareCapabilities.MQTT_SUB |
                       SPLC_HardwareCapabilities.MODBUS_MASTER |
                       SPLC_HardwareCapabilities.MODBUS_TCP |
                       SPLC_HardwareCapabilities.FLOAT32 |
                       SPLC_HardwareCapabilities.SD_DATALOG |
                       SPLC_HardwareCapabilities.MODBUS_SLAVE;
            Span<ushort> capsRegs = stackalloc ushort[2];
            RegisterCodec.EncodeHardwareCapabilities(caps, capsRegs);
            _memory.InternalWrite(ModbusRegisterMap.HardwareCapabilitiesAddress, capsRegs[0]);
            _memory.InternalWrite((ushort)(ModbusRegisterMap.HardwareCapabilitiesAddress + 1), capsRegs[1]);

            // 0x0A0A: Số cổng RS-485 vật lý hiện hữu (Mặc định 2 cổng Standard)
            _memory.InternalWrite(ModbusRegisterMap.Rs485PhysicalPortCountAddress, 2);
        }
    }

    public void SetPhysicalRs485PortCount(ushort portCount)
    {
        _memory.InternalWrite(ModbusRegisterMap.Rs485PhysicalPortCountAddress, portCount);
    }

    public void SetHardwareCapabilities(SPLC_HardwareCapabilities caps)
    {
        Span<ushort> capsRegs = stackalloc ushort[2];
        RegisterCodec.EncodeHardwareCapabilities(caps, capsRegs);
        _memory.InternalWrite(ModbusRegisterMap.HardwareCapabilitiesAddress, capsRegs[0]);
        _memory.InternalWrite((ushort)(ModbusRegisterMap.HardwareCapabilitiesAddress + 1), capsRegs[1]);
    }

    private void InitializeDefaultState()
    {
        // 0x0010: Rule Table Info
        _memory.InternalWrite(ModbusRegisterMap.RuleTableInfoAddress, 0);

        // 0x0800 - 0x0809: Device Health
        UpdateHealthRegisters();

        // 0x9000 - 0x9005: Staging metadata
        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 0); // IDLE
        _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.NONE);
        _memory.InternalWrite(ModbusRegisterMap.RuleCountStagedAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.ExpectedCrc16Address, 0xFFFF);
        _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCountAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCrc16Address, 0xFFFF);

        // 0xA001: Active Rule Version
        _memory.InternalWrite(ModbusRegisterMap.ActiveRuleVersionAddress, _activeVersion);

        // Xóa Staging RAM
        Array.Clear(_stagingRegistersWritten, 0, _stagingRegistersWritten.Length);
        _memory.InternalClear(ModbusRegisterMap.StagingRuleTableBaseAddress, ModbusRegisterMap.StagingRuleTableMaxLength);

        // Phân vùng Function Block V2 (0x0B00..0x0B7F)
        _memory.InternalClear(ModbusRegisterMap.FbTimerTableBaseAddress, ModbusRegisterMap.FbTableTotalLength);
        Array.Clear(_prevCounterCu, 0, _prevCounterCu.Length);
        Array.Clear(_prevCounterCd, 0, _prevCounterCd.Length);
        Array.Clear(_prevCounterR, 0, _prevCounterR.Length);
        Array.Clear(_prevTimerIn, 0, _prevTimerIn.Length);

        // Phân vùng Diagnostic Block (0x0A20..0x0A24)
        _memory.InternalWrite(ModbusRegisterMap.DiagCommandAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.DiagStateAddress, (ushort)SPLC_DiagState.ENGINE_RUNNING);
        _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.DiagLeaseRemainingAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.NONE);

        // Phân vùng RTC Clock (0x0810..0x0813)
        UpdateRtcRegisters();
        Array.Fill(_lastTriggeredMinute, -1);
        Array.Clear(_prevTagValues, 0, _prevTagValues.Length);
        Array.Clear(_ruleDwellElapsedMs, 0, _ruleDwellElapsedMs.Length);
        Array.Clear(_ruleIntervalElapsedMs, 0, _ruleIntervalElapsedMs.Length);
    }

    private void UpdateRtcRegisters()
    {
        Span<ushort> rtcBuf = stackalloc ushort[ModbusRegisterMap.RtcClockLength];
        RegisterCodec.EncodeRtcClock(new RtcClockDto
        {
            EpochUtcSeconds = _epochUtcSeconds,
            TimezoneOffsetMinutes = _tzOffsetMinutes,
            IsSynced = _rtcSynced,
            HasHardwareRtc = _hasHardwareRtc,
            IsBatteryLow = _batteryLow
        }, rtcBuf);

        for (int i = 0; i < ModbusRegisterMap.RtcClockLength; i++)
        {
            _memory.InternalWrite((ushort)(ModbusRegisterMap.RtcClockAddress + i), rtcBuf[i]);
        }
    }

    internal void SetHardwareRtcState(bool hasHardwareRtc, bool batteryLow)
    {
        _hasHardwareRtc = hasHardwareRtc;
        _batteryLow = batteryLow;
        UpdateRtcRegisters();
    }

    internal void SyncRtcClock(uint epochUtcSeconds, short timezoneOffsetMinutes)
    {
        _epochUtcSeconds = epochUtcSeconds;
        _tzOffsetMinutes = timezoneOffsetMinutes;
        _rtcSynced = true;
        _hasHardwareRtc = true;
        _batteryLow = false;
        UpdateRtcRegisters();
    }

    private void UpdateHealthRegisters()
    {
        uint uptime = _clock.GetUptimeSeconds();
        _memory.InternalWrite(0x0800, (ushort)(uptime >> 16));
        _memory.InternalWrite(0x0801, (ushort)(uptime & 0xFFFF));
        _memory.InternalWrite(0x0802, (ushort)_resetReason);
        _memory.InternalWrite(0x0803, (ushort)SPLC_HealthFlags.NONE);
        _memory.InternalWrite(0x0804, _clock.GetCpuLoadPercent());
        _memory.InternalWrite(0x0805, 40); // RAM 40%

        uint scanTime = _clock.GetScanTimeMs();
        uint maxScanTime = _clock.GetMaxScanTimeMs();
        _memory.InternalWrite(0x0806, (ushort)(scanTime >> 16));
        _memory.InternalWrite(0x0807, (ushort)(scanTime & 0xFFFF));
        _memory.InternalWrite(0x0808, (ushort)(maxScanTime >> 16));
        _memory.InternalWrite(0x0809, (ushort)(maxScanTime & 0xFFFF));
    }

    /// <summary>
    /// Thực thi một chu kỳ quét Scan Pass của MCU (danh định 10ms), bao gồm:
    /// 1. Đánh giá bảng 8 Dedicated Timers (0x0B00..0x0B3F).
    /// 2. Đánh giá bảng 8 Dedicated Counters (0x0B40..0x0B7F) và đồng bộ VREG_RETAIN.
    /// Tuân thủ 100% thuật toán tham chiếu trong Mục 9.5 của Data Contract V2.
    /// </summary>
    public void ExecuteScanPass(uint deltaMs = 10)
    {
        EnsureOnline();

        var diagState = (SPLC_DiagState)_memory.InternalRead(ModbusRegisterMap.DiagStateAddress);
        bool isDiagMode = diagState == SPLC_DiagState.DIAG_CONTROL;

        // 1. Thực thi Dedicated Timers (0x0B00..0x0B3F) - Chỉ chạy khi không ở chế độ DIAG_CONTROL
        if (!isDiagMode)
        {
            for (int i = 0; i < ModbusRegisterMap.FbMaxTimers; i++)
            {
                ushort baseAddr = ModbusRegisterMap.GetFbTimerAddress(i);
            Span<ushort> timerRegs = _memory.InternalMutableSpan(baseAddr, ModbusRegisterMap.FbRegistersPerBlock);
            var timer = FunctionBlockCodec.DecodeTimer(timerRegs);

            if (timer.Mode == SPLC_TimerMode.DISABLED)
                continue;

            bool inState = timer.In;
            bool resetState = timer.Reset;

            if (resetState)
            {
                // Ngắt khẩn cấp / Reset có độ ưu tiên cao nhất
                timer.ElapsedMs = 0;
                timer.Running = false;
                timer.Q = false;
            }
            else
            {
                switch (timer.Mode)
                {
                    case SPLC_TimerMode.TON:
                        if (inState)
                        {
                            if (timer.ElapsedMs < timer.PresetMs)
                            {
                                timer.ElapsedMs += deltaMs;
                                if (timer.ElapsedMs >= timer.PresetMs)
                                {
                                    timer.ElapsedMs = timer.PresetMs;
                                    timer.Q = true;
                                    timer.Running = false;
                                }
                                else
                                {
                                    timer.Running = true;
                                    timer.Q = false;
                                }
                            }
                        }
                        else
                        {
                            timer.ElapsedMs = 0;
                            timer.Running = false;
                            timer.Q = false;
                        }
                        break;

                    case SPLC_TimerMode.TOF:
                        if (inState)
                        {
                            timer.ElapsedMs = 0;
                            timer.Q = true;
                            timer.Running = false;
                        }
                        else
                        {
                            if (timer.Q)
                            {
                                timer.ElapsedMs += deltaMs;
                                if (timer.ElapsedMs >= timer.PresetMs)
                                {
                                    timer.ElapsedMs = timer.PresetMs;
                                    timer.Q = false;
                                    timer.Running = false;
                                }
                                else
                                {
                                    timer.Running = true;
                                }
                            }
                        }
                        break;

                    case SPLC_TimerMode.TP:
                        bool rise = inState && !_prevTimerIn[i];
                        if (rise && !timer.Running)
                        {
                            timer.ElapsedMs = Math.Min(timer.PresetMs, deltaMs);
                            timer.Running = timer.ElapsedMs < timer.PresetMs;
                            timer.Q = true;
                        }
                        else if (timer.Running)
                        {
                            timer.ElapsedMs += deltaMs;
                            if (timer.ElapsedMs >= timer.PresetMs)
                            {
                                timer.ElapsedMs = timer.PresetMs;
                                timer.Running = false;
                                timer.Q = false;
                            }
                        }
                        break;

                    case SPLC_TimerMode.CYCLE:
                        if (inState)
                        {
                            timer.ElapsedMs += deltaMs;
                            uint period = timer.PresetMs > 0 ? timer.PresetMs : 1000;
                            if (timer.ElapsedMs >= period)
                            {
                                timer.ElapsedMs = 0;
                                timer.Q = !timer.Q;
                            }
                            timer.Running = true;
                        }
                        else
                        {
                            timer.ElapsedMs = 0;
                            timer.Running = false;
                            timer.Q = false;
                        }
                        break;
                }
            }

            _prevTimerIn[i] = inState;
            FunctionBlockCodec.EncodeTimer(timer, timerRegs);
        }
        }

        // 2. Thực thi Dedicated Counters (0x0B40..0x0B7F) - Chỉ chạy khi không ở chế độ DIAG_CONTROL
        if (!isDiagMode)
        {
            Span<ushort> cvRegs = stackalloc ushort[2];
            for (int i = 0; i < ModbusRegisterMap.FbMaxCounters; i++)
            {
                ushort baseAddr = ModbusRegisterMap.GetFbCounterAddress(i);
                Span<ushort> counterRegs = _memory.InternalMutableSpan(baseAddr, ModbusRegisterMap.FbRegistersPerBlock);
                var counter = FunctionBlockCodec.DecodeCounter(counterRegs);

                if (counter.Mode == SPLC_CounterMode.DISABLED)
                    continue;

                bool cuNow = counter.Cu;
                bool cdNow = counter.Cd;
                bool rNow = counter.Reset;

                if (rNow && !_prevCounterR[i])
                {
                    // Sườn lên của chân Reset: đưa về 0
                    counter.CurrentValue = 0;
                    counter.Q = false;
                }
                else if (cuNow && !_prevCounterCu[i])
                {
                    // Sườn lên của chân CU: đếm tiến
                    counter.CurrentValue++;
                    if (counter.CurrentValue >= counter.PresetValue)
                    {
                        counter.Q = true;
                    }
                }
                else if (cdNow && !_prevCounterCd[i])
                {
                    // Sườn lên của chân CD: đếm lùi
                    counter.CurrentValue--;
                    if (counter.CurrentValue <= 0)
                    {
                        counter.Q = true;
                    }
                }

                _prevCounterCu[i] = cuNow;
                _prevCounterCd[i] = cdNow;
                _prevCounterR[i] = rNow;

                FunctionBlockCodec.EncodeCounter(counter, counterRegs);

                var layout = GetTagLayout();

                // Đồng bộ sang thanh ghi lưu trữ (VREG, VREG_RETAIN, VFLAG, COUNTER...) nếu có cấu hình
                if (counter.RetainTagIndex != ModbusRegisterMap.FbCounterRetainNone &&
                    counter.RetainTagIndex < layout.TotalTags)
                {
                    WriteTagValue(counter.RetainTagIndex, counter.CurrentValue);
                }
            }
        }

        // 3. Quản lý Lease Watchdog của Diagnostic & Commissioning Subsystem (0x0A20..0x0A24)
        diagState = (SPLC_DiagState)_memory.InternalRead(ModbusRegisterMap.DiagStateAddress);
        if (diagState == SPLC_DiagState.DIAG_CONTROL)
        {
            ushort remainingMs = _memory.InternalRead(ModbusRegisterMap.DiagLeaseRemainingAddress);
            if (remainingMs <= deltaMs)
            {
                // Lease hết hạn: Tự động thu hồi quyền, chuyển về ENGINE_RUNNING, bật mã lỗi LEASE_EXPIRED
                _memory.InternalWrite(ModbusRegisterMap.DiagStateAddress, (ushort)SPLC_DiagState.ENGINE_RUNNING);
                var flags = (SPLC_DiagFlags)_memory.InternalRead(ModbusRegisterMap.DiagFlagsAddress);
                _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, (ushort)(flags & ~SPLC_DiagFlags.LEASE_ACTIVE));
                _memory.InternalWrite(ModbusRegisterMap.DiagLeaseRemainingAddress, 0);
                _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.LEASE_EXPIRED);

                // Fail-safe: Tự động trả các ngõ ra DO (0x0910..0x091F) về 0 an toàn
                var currentLayout = GetTagLayout();
                for (int doIdx = 0; doIdx < currentLayout.DoCount; doIdx++)
                {
                    ushort doAddr = ModbusRegisterMap.GetRuntimeTagAddress((ushort)(currentLayout.DoBase + doIdx));
                    _memory.InternalWrite(doAddr, 0);
                    _memory.InternalWrite((ushort)(doAddr + 1), 0);
                }
            }
            else
            {
                _memory.InternalWrite(ModbusRegisterMap.DiagLeaseRemainingAddress, (ushort)(remainingMs - deltaMs));
            }
        }

        // 4. Cập nhật đồng hồ thời gian thực RTC (0x0810..0x0813)
        _rtcAccumulatorMs += deltaMs;
        if (_rtcAccumulatorMs >= 1000)
        {
            uint addSec = _rtcAccumulatorMs / 1000;
            _rtcAccumulatorMs %= 1000;
            _epochUtcSeconds += addSec;
            UpdateRtcRegisters();
        }

        // [SAFETY GATE — SPLC-SPEC-DIAG-001]
        // Khi kỹ sư đang ở chế độ DIAG_CONTROL, Rule Engine phải nhường quyền kiểm soát I/O.
        // Timer (Step 1), Counter (Step 2), Watchdog Lease (Step 3) và RTC (Step 4) vẫn chạy bình thường
        // để tính năng auto-timeout (Lease Expired → ENGINE_RUNNING) hoạt động chính xác.
        // Nếu Rule Engine tiếp tục chạy, nó sẽ ghi đè ngõ ra DO mà kỹ sư đang cưỡng bức thủ công
        // → Race Condition nguy hiểm trong môi trường dây chuyền sản xuất thực tế.
        var diagStateForRuleEngine = (SPLC_DiagState)_memory.InternalRead(ModbusRegisterMap.DiagStateAddress);
        if (diagStateForRuleEngine == SPLC_DiagState.DIAG_CONTROL)
            return; // Rule Engine tạm nhường quyền; sẽ tự khôi phục khi Lease hết hạn hoặc EXIT_DIAG.

        // 5. Thực thi Rule Engine toàn diện (Active Rule Table 0x1000..)
        ushort activeRuleCount = _memory.InternalRead(ModbusRegisterMap.ActiveRuleCountAddress);
        if (activeRuleCount == 0)
        {
            activeRuleCount = _memory.InternalRead(ModbusRegisterMap.RuleTableInfoAddress);
        }

        if (activeRuleCount > 0)
        {
            long localSec = (long)_epochUtcSeconds + ((long)_tzOffsetMinutes * 60);
            uint secInDay = (uint)((localSec % 86400 + 86400) % 86400);
            int currentHour = (int)(secInDay / 3600);
            int currentMinute = (int)((secInDay % 3600) / 60);
            int currentHhmm = currentHour * 100 + currentMinute;
            int currentMinuteOfDay = currentHour * 60 + currentMinute;

            for (int r = 0; r < activeRuleCount && r < ModbusRegisterMap.MaxRules; r++)
            {
                ushort ruleAddr = (ushort)(ModbusRegisterMap.ActiveRuleTableBaseAddress + r * ModbusRegisterMap.RegistersPerRule);
                Span<ushort> memSpan = _memory.InternalMutableSpan(ruleAddr, ModbusRegisterMap.RegistersPerRule);
                var rule = RegisterCodec.DecodeRuleRecord(memSpan);
                if (!rule.Enabled)
                {
                    _ruleDwellElapsedMs[r] = 0;
                    continue;
                }

                // Check Guard
                if (rule.GuardTag != ModbusRegisterMap.GuardTagNone)
                {
                    ushort guardIndex = (ushort)(rule.GuardTag & ModbusRegisterMap.GuardTagIndexMask);
                    bool negate = (rule.GuardTag & ModbusRegisterMap.GuardTagNegateMask) != 0;
                    int guardVal = ReadTagValue(guardIndex);
                    bool guardOpen = negate ? (guardVal == 0) : (guardVal != 0);
                    if (!guardOpen)
                    {
                        _ruleDwellElapsedMs[r] = 0;
                        continue;
                    }
                }

                bool triggerFired = false;

                switch (rule.TriggerType)
                {
                    case SPLC_TriggerType.TIME_WINDOW:
                        if (_rtcSynced)
                        {
                            if (rule.CompareOp == SPLC_CompareOp.EQ)
                            {
                                // Mốc thời gian (At Time / Alarm): nổ 1 lần duy nhất trong phút đó
                                if (currentHhmm == rule.ThresholdLo)
                                {
                                    if (_lastTriggeredMinute[r] != currentMinuteOfDay)
                                    {
                                        _lastTriggeredMinute[r] = currentMinuteOfDay;
                                        triggerFired = true;
                                    }
                                }
                                else
                                {
                                    _lastTriggeredMinute[r] = -1;
                                }
                            }
                            else if (rule.CompareOp == SPLC_CompareOp.BETWEEN)
                            {
                                // Khung thời gian (Time Window / Range)
                                bool inWindow = rule.ThresholdLo <= rule.ThresholdHi
                                    ? (currentHhmm >= rule.ThresholdLo && currentHhmm < rule.ThresholdHi)
                                    : (currentHhmm >= rule.ThresholdLo || currentHhmm < rule.ThresholdHi);

                                if (inWindow)
                                {
                                    triggerFired = true;
                                }
                            }
                        }
                        break;

                    case SPLC_TriggerType.INTERVAL:
                        _ruleIntervalElapsedMs[r] += deltaMs;
                        uint targetInterval = rule.ForMs > 0 ? rule.ForMs : 1000;
                        if (_ruleIntervalElapsedMs[r] >= targetInterval)
                        {
                            _ruleIntervalElapsedMs[r] = 0;
                            triggerFired = true;
                        }
                        break;

                    case SPLC_TriggerType.ON_RISE:
                    case SPLC_TriggerType.ON_FALL:
                    case SPLC_TriggerType.ON_CHANGE:
                        if (rule.TriggerTag < ModbusRegisterMap.MaxRuntimeTags)
                        {
                            int currVal = ReadTagValue(rule.TriggerTag);
                            int prevVal = _prevTagValues[rule.TriggerTag];

                            bool condMet = false;
                            if (rule.TriggerType == SPLC_TriggerType.ON_RISE)
                            {
                                bool riseEdge = (prevVal == 0 && currVal != 0);
                                bool riseHeld = currVal != 0;
                                condMet = rule.ForMs > 0 ? riseHeld : riseEdge;
                            }
                            else if (rule.TriggerType == SPLC_TriggerType.ON_FALL)
                            {
                                bool fallEdge = (prevVal != 0 && currVal == 0);
                                bool fallHeld = currVal == 0;
                                condMet = rule.ForMs > 0 ? fallHeld : fallEdge;
                            }
                            else // ON_CHANGE
                            {
                                if (rule.CompareOp != SPLC_CompareOp.NONE)
                                {
                                    condMet = EvaluateCompare(rule.CompareOp, currVal, rule.ThresholdLo, rule.ThresholdHi, rule.IsTriggerFloat);
                                }
                                else
                                {
                                    condMet = (prevVal != currVal);
                                }
                            }

                            if (condMet)
                            {
                                if (rule.ForMs > 0)
                                {
                                    _ruleDwellElapsedMs[r] += deltaMs;
                                    if (_ruleDwellElapsedMs[r] >= rule.ForMs)
                                    {
                                        triggerFired = true;
                                    }
                                }
                                else
                                {
                                    triggerFired = true;
                                }
                            }
                            else
                            {
                                _ruleDwellElapsedMs[r] = 0;
                            }
                        }
                        break;
                }

                if (triggerFired)
                {
                    ExecuteAction(rule);
                }
            }
        }

        // Cập nhật giá trị chu kỳ trước cho lần quét tiếp theo
        for (int i = 0; i < ModbusRegisterMap.MaxRuntimeTags; i++)
        {
            _prevTagValues[i] = ReadTagValue((ushort)i);
        }
    }

    private static bool EvaluateCompare(SPLC_CompareOp op, int val, int lo, int hi, bool isFloat = false)
    {
        if (!isFloat)
        {
            return op switch
            {
                SPLC_CompareOp.EQ => val == lo,
                SPLC_CompareOp.NEQ => val != lo,
                SPLC_CompareOp.GT => val > lo,
                SPLC_CompareOp.LT => val < lo,
                SPLC_CompareOp.GTE => val >= lo,
                SPLC_CompareOp.LTE => val <= lo,
                SPLC_CompareOp.BETWEEN => val >= lo && val <= hi,
                _ => true
            };
        }

        float valFloat = BitConverter.Int32BitsToSingle(val);
        float loFloat = BitConverter.Int32BitsToSingle(lo);
        float hiFloat = BitConverter.Int32BitsToSingle(hi);

        return op switch
        {
            SPLC_CompareOp.EQ => Math.Abs(valFloat - loFloat) <= 1e-5f,
            SPLC_CompareOp.NEQ => Math.Abs(valFloat - loFloat) > 1e-5f,
            SPLC_CompareOp.GT => valFloat > loFloat,
            SPLC_CompareOp.LT => valFloat < loFloat,
            SPLC_CompareOp.GTE => valFloat >= loFloat,
            SPLC_CompareOp.LTE => valFloat <= loFloat,
            SPLC_CompareOp.BETWEEN => valFloat >= loFloat && valFloat <= hiFloat,
            _ => true
        };
    }

    private int ReadTagValue(ushort tagIndex)
    {
        ushort tagAddr = ModbusRegisterMap.GetRuntimeTagAddress(tagIndex);
        return RegisterCodec.DecodeInt32(_memory.InternalMutableSpan(tagAddr, 2));
    }

    private void WriteTagValue(ushort tagIndex, int value)
    {
        ushort tagAddr = ModbusRegisterMap.GetRuntimeTagAddress(tagIndex);
        Span<ushort> buf = stackalloc ushort[2];
        RegisterCodec.EncodeInt32(value, buf);
        _memory.InternalWrite(tagAddr, buf[0]);
        _memory.InternalWrite((ushort)(tagAddr + 1), buf[1]);
    }

    private void ExecuteAction(RuleRecordDto rule)
    {
        switch (rule.ActionType)
        {
            case SPLC_ActionType.SET_TAG:
                WriteTagValue(rule.ActionTag, rule.ActionParam);
                break;
            case SPLC_ActionType.TOGGLE_TAG:
                int cur = ReadTagValue(rule.ActionTag);
                WriteTagValue(rule.ActionTag, cur == 0 ? 1 : 0);
                break;
            case SPLC_ActionType.INC_COUNTER:
                int val = ReadTagValue(rule.ActionTag);
                WriteTagValue(rule.ActionTag, val + rule.ActionParam);
                break;
            case SPLC_ActionType.SCALE_TAG:
                if (rule.IsActionFloat || rule.IsTriggerFloat)
                {
                    int rawTrigVal = ReadTagValue(rule.TriggerTag);
                    float trigValFloat = rule.IsTriggerFloat ? BitConverter.Int32BitsToSingle(rawTrigVal) : (float)rawTrigVal;
                    float gain = rule.ActionParamFloat;
                    float offset = rule.ThresholdHiFloat;
                    float scaledFloat = trigValFloat * gain + offset;
                    WriteTagValue(rule.ActionTag, BitConverter.SingleToInt32Bits(scaledFloat));
                }
                else
                {
                    int trigVal = ReadTagValue(rule.TriggerTag);
                    int scaledVal = (int)((long)trigVal * rule.ActionParam / 1000 + rule.ThresholdHi);
                    WriteTagValue(rule.ActionTag, scaledVal);
                }
                break;
            case SPLC_ActionType.ADD_TAG:
                if (rule.IsActionFloat)
                {
                    int rawTrig = ReadTagValue(rule.TriggerTag);
                    float tValF = rule.IsTriggerFloat ? BitConverter.Int32BitsToSingle(rawTrig) : (float)rawTrig;
                    float aValF = BitConverter.Int32BitsToSingle(ReadTagValue(rule.ActionTag));
                    WriteTagValue(rule.ActionTag, BitConverter.SingleToInt32Bits(aValF + tValF));
                }
                else
                {
                    int tVal = ReadTagValue(rule.TriggerTag);
                    int aVal = ReadTagValue(rule.ActionTag);
                    WriteTagValue(rule.ActionTag, aVal + tVal);
                }
                break;
            case SPLC_ActionType.SEND_ALARM:
            case SPLC_ActionType.WRITE_REMOTE:
            case SPLC_ActionType.LOG_EVENT:
                WriteTagValue(rule.ActionTag, rule.ActionParam);
                break;
        }
    }

    // =========================================================
    // 1. GIAO THỨC TRUY XUẤT MODBUS THỰC TẾ (OBSERVABLE PROTOCOL)
    // =========================================================

    public Task<ushort[]> ReadHoldingRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ushort count,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOnline();

        if (_faults.TimeoutOnAddress.HasValue && _faults.TimeoutOnAddress.Value == startAddress)
        {
            throw new TimeoutException($"Simulated Modbus read timeout at address 0x{startAddress:X4}.");
        }

        // Cập nhật telemetry động trước khi trả về
        if (startAddress <= 0x0809 && startAddress + count > 0x0800)
        {
            UpdateHealthRegisters();
        }

        // Cập nhật RTC trước khi đọc nếu đọc vùng 0x0810..0x0813
        if (startAddress <= ModbusRegisterMap.RtcClockAddress + ModbusRegisterMap.RtcClockLength - 1 &&
            startAddress + count > ModbusRegisterMap.RtcClockAddress)
        {
            UpdateRtcRegisters();
        }

        // Đọc từ bộ nhớ thanh ghi với kiểm tra phạm vi hợp lệ
        var result = _memory.ReadRegisters(startAddress, count);

        // Xử lý cấy lỗi Descriptor không tương thích
        if (_faults.UnsupportedDescriptor && startAddress <= 9 && startAddress + count > 9)
        {
            int offset = 9 - startAddress;
            result[offset] = 0x0200; // Fake version 2.0
        }

        if (_faults.OverrideDescriptor != null && startAddress <= 9 && startAddress + count > 0)
        {
            Span<ushort> descRegs = stackalloc ushort[10];
            RegisterCodec.EncodeDeviceDescriptor(_faults.OverrideDescriptor, descRegs);
            for (int i = 0; i < 10; i++)
            {
                int regAddr = i;
                if (regAddr >= startAddress && regAddr < startAddress + count)
                {
                    result[regAddr - startAddress] = descRegs[i];
                }
            }
        }

        if (_faults.OverrideDeviceClass.HasValue && startAddress == 0 && count > 0)
        {
            result[0] = _faults.OverrideDeviceClass.Value;
        }

        if (_faults.OverrideDeviceVariant.HasValue && startAddress <= 1 && startAddress + count > 1)
        {
            int offset = 1 - startAddress;
            result[offset] = _faults.OverrideDeviceVariant.Value;
        }

        if (_faults.OverrideResourceInfo != null &&
            startAddress <= ModbusRegisterMap.DeviceResourceInfoAddress + ModbusRegisterMap.DeviceResourceInfoLength - 1 &&
            startAddress + count > ModbusRegisterMap.DeviceResourceInfoAddress)
        {
            var encoded = RegisterCodec.EncodeDeviceResourceInfo(_faults.OverrideResourceInfo);
            for (int i = 0; i < ModbusRegisterMap.DeviceResourceInfoLength; i++)
            {
                int regAddr = ModbusRegisterMap.DeviceResourceInfoAddress + i;
                if (regAddr >= startAddress && regAddr < startAddress + count)
                {
                    result[regAddr - startAddress] = encoded[i];
                }
            }
        }

        return Task.FromResult(result);
    }

    public Task WriteMultipleRegistersAsync(
        byte slaveId,
        ushort startAddress,
        ReadOnlyMemory<ushort> values,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOnline();
        ProcessWriteFaults(startAddress);

        ReadOnlySpan<ushort> span = values.Span;
        ushort count = (ushort)span.Length;

        // Xử lý ghi RULE_COUNT_STAGED (0x9002) và/hoặc EXPECTED_CRC16 (0x9003)
        if (startAddress == ModbusRegisterMap.RuleCountStagedAddress)
        {
            HandleStagingSessionInit(span[0]);
            if (count > 1)
            {
                _memory.InternalWrite(ModbusRegisterMap.ExpectedCrc16Address, span[1]);
            }
            return Task.CompletedTask;
        }

        // Xử lý ghi STAGING_RULE_TABLE (0x9010..)
        if (startAddress >= ModbusRegisterMap.StagingRuleTableBaseAddress &&
            startAddress < ModbusRegisterMap.StagingRuleTableBaseAddress + ModbusRegisterMap.StagingRuleTableMaxLength)
        {
            HandleStagingRuleWrite(startAddress, span);
            return Task.CompletedTask;
        }

        // Xử lý ghi RuntimeTagValues (0x0900..0x09FF): chỉ cho phép khi đang ở trạng thái DIAG_CONTROL
        if (startAddress >= ModbusRegisterMap.RuntimeTagValuesBaseAddress &&
            startAddress < ModbusRegisterMap.RuntimeTagValuesBaseAddress + ModbusRegisterMap.RuntimeTagValuesMaxLength)
        {
            var diagState = (SPLC_DiagState)_memory.InternalRead(ModbusRegisterMap.DiagStateAddress);
            if (diagState != SPLC_DiagState.DIAG_CONTROL)
            {
                throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): RuntimeTagValues 0x{startAddress:X4} is read-only when not in DIAG_CONTROL mode.");
            }

            for (int i = 0; i < span.Length; i++)
            {
                _memory.InternalWrite((ushort)(startAddress + i), span[i]);
            }

            // Nếu ghi vào vùng VREG_RETAIN (0x09A8..0x09E7), bật cờ RETAIN_DIRTY
            if (startAddress + count > 0x09A8 && startAddress <= 0x09E7)
            {
                var flags = (SPLC_DiagFlags)_memory.InternalRead(ModbusRegisterMap.DiagFlagsAddress);
                _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, (ushort)(flags | SPLC_DiagFlags.RETAIN_DIRTY));
            }

            return Task.CompletedTask;
        }

        // Xử lý ghi RTC Clock (0x0810..0x0813)
        if (startAddress >= ModbusRegisterMap.RtcClockAddress &&
            startAddress < ModbusRegisterMap.RtcClockAddress + ModbusRegisterMap.RtcClockLength)
        {
            _memory.WriteRegisters(startAddress, span);
            var rtcRegs = _memory.ReadRegisters(ModbusRegisterMap.RtcClockAddress, ModbusRegisterMap.RtcClockLength);
            var decoded = RegisterCodec.DecodeRtcClock(rtcRegs);
            _epochUtcSeconds = decoded.EpochUtcSeconds;
            _tzOffsetMinutes = decoded.TimezoneOffsetMinutes;
            _rtcSynced = decoded.IsSynced;
            _hasHardwareRtc = decoded.HasHardwareRtc || _hasHardwareRtc;
            _batteryLow = decoded.IsBatteryLow || _batteryLow;
            UpdateRtcRegisters();
            return Task.CompletedTask;
        }

        // Ghi các thanh ghi cho phép khác
        _memory.WriteRegisters(startAddress, span);
        return Task.CompletedTask;
    }

    public Task WriteSingleRegisterAsync(
        byte slaveId,
        ushort address,
        ushort value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureOnline();
        ProcessWriteFaults(address);

        if (address == ModbusRegisterMap.RuleCountStagedAddress)
        {
            HandleStagingSessionInit(value);
            return Task.CompletedTask;
        }

        if (address == ModbusRegisterMap.ExpectedCrc16Address)
        {
            _memory.InternalWrite(ModbusRegisterMap.ExpectedCrc16Address, value);
            return Task.CompletedTask;
        }

        if (address == ModbusRegisterMap.CommitCommandAddress)
        {
            if (value == ModbusRegisterMap.CommitMagic)
            {
                ExecuteCommitProcess();
            }
            return Task.CompletedTask;
        }

        if (address == ModbusRegisterMap.SystemCommandAddress)
        {
            ExecuteSystemCommand((SPLC_SystemCommand)value);
            return Task.CompletedTask;
        }

        if (address == ModbusRegisterMap.DiagCommandAddress)
        {
            ExecuteDiagCommand((SPLC_DiagCommand)value);
            return Task.CompletedTask;
        }

        if (address >= ModbusRegisterMap.RuntimeTagValuesBaseAddress &&
            address < ModbusRegisterMap.RuntimeTagValuesBaseAddress + ModbusRegisterMap.RuntimeTagValuesMaxLength)
        {
            var diagState = (SPLC_DiagState)_memory.InternalRead(ModbusRegisterMap.DiagStateAddress);
            if (diagState != SPLC_DiagState.DIAG_CONTROL)
            {
                throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): RuntimeTagValues 0x{address:X4} is read-only when not in DIAG_CONTROL mode.");
            }

            _memory.InternalWrite(address, value);
            if (address >= 0x09A8 && address <= 0x09E7)
            {
                var flags = (SPLC_DiagFlags)_memory.InternalRead(ModbusRegisterMap.DiagFlagsAddress);
                _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, (ushort)(flags | SPLC_DiagFlags.RETAIN_DIRTY));
            }
            return Task.CompletedTask;
        }

        // Ghi thanh ghi thông thường
        _memory.WriteSingleRegister(address, value);
        return Task.CompletedTask;
    }

    private void ExecuteDiagCommand(SPLC_DiagCommand cmd)
    {
        _memory.InternalWrite(ModbusRegisterMap.DiagCommandAddress, (ushort)cmd);
        var currentState = (SPLC_DiagState)_memory.InternalRead(ModbusRegisterMap.DiagStateAddress);
        var currentFlags = (SPLC_DiagFlags)_memory.InternalRead(ModbusRegisterMap.DiagFlagsAddress);

        switch (cmd)
        {
            case SPLC_DiagCommand.ENTER_DIAG:
                _memory.InternalWrite(ModbusRegisterMap.DiagStateAddress, (ushort)SPLC_DiagState.DIAG_CONTROL);
                _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, (ushort)(currentFlags | SPLC_DiagFlags.LEASE_ACTIVE));
                _memory.InternalWrite(ModbusRegisterMap.DiagLeaseRemainingAddress, ModbusRegisterMap.DiagDefaultLeaseMs);
                _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.NONE);
                break;

            case SPLC_DiagCommand.HEARTBEAT:
                if (currentState == SPLC_DiagState.DIAG_CONTROL)
                {
                    _memory.InternalWrite(ModbusRegisterMap.DiagLeaseRemainingAddress, ModbusRegisterMap.DiagDefaultLeaseMs);
                    _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.NONE);
                }
                else
                {
                    _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.INVALID_COMMAND);
                }
                break;

            case SPLC_DiagCommand.EXIT_DIAG:
                if (currentState == SPLC_DiagState.DIAG_CONTROL)
                {
                    if (currentFlags.HasFlag(SPLC_DiagFlags.RETAIN_DIRTY))
                    {
                        _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.RETAIN_DIRTY);
                    }
                    else
                    {
                        _memory.InternalWrite(ModbusRegisterMap.DiagStateAddress, (ushort)SPLC_DiagState.ENGINE_RUNNING);
                        _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, (ushort)(currentFlags & ~SPLC_DiagFlags.LEASE_ACTIVE));
                        _memory.InternalWrite(ModbusRegisterMap.DiagLeaseRemainingAddress, 0);
                        _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.NONE);
                    }
                }
                break;

            case SPLC_DiagCommand.COMMIT_RETAIN:
                if (currentState == SPLC_DiagState.DIAG_CONTROL)
                {
                    _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, (ushort)(currentFlags & ~SPLC_DiagFlags.RETAIN_DIRTY));
                    _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.NONE);
                }
                else
                {
                    _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.INVALID_COMMAND);
                }
                break;

            case SPLC_DiagCommand.DISCARD_RETAIN:
                if (currentState == SPLC_DiagState.DIAG_CONTROL)
                {
                    _memory.InternalWrite(ModbusRegisterMap.DiagFlagsAddress, (ushort)(currentFlags & ~SPLC_DiagFlags.RETAIN_DIRTY));
                    _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.NONE);
                }
                else
                {
                    _memory.InternalWrite(ModbusRegisterMap.DiagErrorCodeAddress, (ushort)SPLC_DiagErrorCode.INVALID_COMMAND);
                }
                break;
        }
    }

    private void ProcessWriteFaults(ushort address)
    {
        _writeTransactionCount++;

        if (_faults.TimeoutOnWriteNumber.HasValue && _faults.TimeoutOnWriteNumber.Value == _writeTransactionCount)
        {
            throw new TimeoutException($"Simulated write timeout at transaction #{_writeTransactionCount}.");
        }

        if (_faults.TimeoutOnAddress.HasValue && _faults.TimeoutOnAddress.Value == address)
        {
            throw new TimeoutException($"Simulated write timeout at address 0x{address:X4}.");
        }
    }

    // =========================================================
    // 2. MÁY TRẠNG THÁI STAGING & ATOMIC COMMIT
    // =========================================================

    private void HandleStagingSessionInit(ushort count)
    {
        if (count > ModbusRegisterMap.MaxRules)
        {
            _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 4); // ERROR
            _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.INVALID_PARAMETER);
            throw new ArgumentOutOfRangeException(nameof(count), $"Rule count {count} exceeds MaxRules {ModbusRegisterMap.MaxRules}.");
        }

        _stagedRuleCount = count;
        _memory.InternalWrite(ModbusRegisterMap.RuleCountStagedAddress, count);

        // Phục hồi khỏi trạng thái lỗi hoặc reset về IDLE cho phiên mới
        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 0); // IDLE
        _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.NONE);

        // Xóa sạch Staging tracker cho phiên mới
        Array.Clear(_stagingRegistersWritten, 0, _stagingRegistersWritten.Length);
        _hasCommittedCurrentStaging = false;
    }

    private void HandleStagingRuleWrite(ushort startAddress, ReadOnlySpan<ushort> values)
    {
        int offset = startAddress - ModbusRegisterMap.StagingRuleTableBaseAddress;
        int maxAllowedRegisters = _stagedRuleCount * ModbusRegisterMap.RegistersPerRule;

        // Kiểm tra không ghi vượt quá số lượng rule đã khai báo
        if (offset + values.Length > maxAllowedRegisters)
        {
            _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 4); // ERROR
            _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.INVALID_PARAMETER);
            throw new InvalidOperationException($"Write at offset {offset} with length {values.Length} exceeds declared staged capacity {maxAllowedRegisters} registers.");
        }

        // Ghi vào bộ nhớ RAM Staging
        _memory.WriteRegisters(startAddress, values);

        // Đánh dấu completeness tracker
        for (int i = 0; i < values.Length; i++)
        {
            _stagingRegistersWritten[offset + i] = true;
        }

        // Chuyển sang RECEIVING (1)
        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 1);
    }

    private void ExecuteCommitProcess()
    {
        if (_faults.DeviceBusy)
        {
            _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 4); // ERROR
            _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.BUSY);
            return;
        }

        // Chuyển sang VERIFYING (2)
        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 2);

        // 1. Kiểm tra Completeness: toàn bộ register đã khai báo đều phải được ghi đầy đủ
        int requiredRegisters = _stagedRuleCount * ModbusRegisterMap.RegistersPerRule;

        for (int i = 0; i < requiredRegisters; i++)
        {
            if (!_stagingRegistersWritten[i])
            {
                // Thiếu payload / hổng chunk!
                _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 4); // ERROR
                _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.INVALID_PARAMETER);
                return;
            }
        }

        // 2. Chống commit đúp (Idempotent)
        if (_hasCommittedCurrentStaging)
        {
            _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 3); // READY
            return;
        }

        // 3. Tính toán và xác minh CRC-16
        ReadOnlySpan<ushort> stagingSpan = _memory.InternalSpan(ModbusRegisterMap.StagingRuleTableBaseAddress, requiredRegisters);
        ushort calculatedCrc = Crc16Modbus.ComputeFromRegisters(stagingSpan);

        if (_faults.CorruptCommitCrc)
        {
            calculatedCrc ^= 0xFFFF; // Gây sai lệch CRC
        }

        ushort expectedCrc = _memory.InternalRead(ModbusRegisterMap.ExpectedCrc16Address);

        if (calculatedCrc == expectedCrc)
        {
            if (!SupportsFloatRules)
            {
                for (int r = 0; r < _stagedRuleCount; r++)
                {
                    var ruleSpan = stagingSpan.Slice(r * ModbusRegisterMap.RegistersPerRule, ModbusRegisterMap.RegistersPerRule);
                    var rule = RegisterCodec.DecodeRuleRecord(ruleSpan);
                    if (rule.IsFloatData)
                    {
                        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 4); // ERROR
                        _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.UNSUPPORTED);
                        return;
                    }
                }
            }

            // CRC KHỚP: Hoán đổi sang Active Table & Ghi Flash
            stagingSpan.CopyTo(_memory.InternalMutableSpan(ModbusRegisterMap.ActiveRuleTableBaseAddress, requiredRegisters));

            _activeVersion++;
            _flash.Commit(_stagedRuleCount, _activeVersion, calculatedCrc, stagingSpan);

            // Cập nhật thông tin Active
            _memory.InternalWrite(ModbusRegisterMap.RuleTableInfoAddress, _stagedRuleCount);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCountAddress, _stagedRuleCount);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCrc16Address, calculatedCrc);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleVersionAddress, _activeVersion);

            _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 3); // READY
            _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.NONE);
            _hasCommittedCurrentStaging = true;
        }
        else
        {
            // CRC SAI LỆCH: Giữ nguyên Active table cũ, không tăng version, báo lỗi
            _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 4); // ERROR
            _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.CRC_MISMATCH);
        }
    }

    private void ExecuteSystemCommand(SPLC_SystemCommand command)
    {
        if (command == SPLC_SystemCommand.REBOOT)
        {
            SoftwareReboot();
            return;
        }

        if (command == SPLC_SystemCommand.FACTORY_RESET)
        {
            FactoryReset();
            _memory.InternalWrite(ModbusRegisterMap.SystemCommandResultAddress, (ushort)SPLC_CommandStatus.DONE);
            _memory.InternalWrite(ModbusRegisterMap.SystemCommandResultAddress + 1, (ushort)SPLC_ErrorCode.NONE);
            return;
        }

        if (command == SPLC_SystemCommand.CLEAR_RULES)
        {
            _memory.InternalWrite(ModbusRegisterMap.RuleTableInfoAddress, 0);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCountAddress, 0);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCrc16Address, 0xFFFF);
            _activeVersion++;
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleVersionAddress, _activeVersion);

            _memory.InternalWrite(ModbusRegisterMap.SystemCommandResultAddress, (ushort)SPLC_CommandStatus.DONE);
            _memory.InternalWrite(ModbusRegisterMap.SystemCommandResultAddress + 1, (ushort)SPLC_ErrorCode.NONE);
            return;
        }

        _memory.InternalWrite(ModbusRegisterMap.SystemCommandResultAddress, (ushort)SPLC_CommandStatus.ERROR);
        _memory.InternalWrite(ModbusRegisterMap.SystemCommandResultAddress + 1, (ushort)SPLC_ErrorCode.UNSUPPORTED);
    }

    // =========================================================
    // 3. VÒNG ĐỜI THIẾT BỊ (LIFECYCLE & RESET)
    // =========================================================

    internal void SoftwareReboot()
    {
        IsOnline = false;
        IsRebooting = true;
        _resetReason = SPLC_ResetReason.SOFTWARE;
        _clock.Reset();

        // Xóa Staging RAM chưa commit
        Array.Clear(_stagingRegistersWritten, 0, _stagingRegistersWritten.Length);
        _memory.InternalClear(ModbusRegisterMap.StagingRuleTableBaseAddress, ModbusRegisterMap.StagingRuleTableMaxLength);
        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 0); // IDLE
        _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.NONE);

        // Khôi phục Active Table từ Flash
        RestoreFromFlashIfAvailable();

        UpdateHealthRegisters();
        IsRebooting = false;
        IsOnline = true;
    }

    internal void PowerCycle()
    {
        IsOnline = false;
        _resetReason = SPLC_ResetReason.POWER_ON;
        _clock.Reset();

        // Xóa RAM hoàn toàn
        Array.Clear(_stagingRegistersWritten, 0, _stagingRegistersWritten.Length);
        _memory.InternalClear(ModbusRegisterMap.StagingRuleTableBaseAddress, ModbusRegisterMap.StagingRuleTableMaxLength);
        _memory.InternalClear(ModbusRegisterMap.ActiveRuleTableBaseAddress, ModbusRegisterMap.ActiveRuleTableMaxLength);

        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 0); // IDLE
        _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.NONE);
        _memory.InternalWrite(ModbusRegisterMap.RuleCountStagedAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.ExpectedCrc16Address, 0xFFFF);

        // Khôi phục Active Table từ Flash
        RestoreFromFlashIfAvailable();

        UpdateHealthRegisters();
        IsOnline = true;
    }

    internal void FactoryReset()
    {
        _flash.Clear();

        _activeVersion = 0;
        _stagedRuleCount = 0;
        _hasCommittedCurrentStaging = false;

        Array.Clear(_stagingRegistersWritten, 0, _stagingRegistersWritten.Length);
        _memory.InternalClear(ModbusRegisterMap.ActiveRuleTableBaseAddress, ModbusRegisterMap.ActiveRuleTableMaxLength);
        _memory.InternalClear(ModbusRegisterMap.StagingRuleTableBaseAddress, ModbusRegisterMap.StagingRuleTableMaxLength);

        _memory.InternalWrite(ModbusRegisterMap.RuleTableInfoAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCountAddress, 0);
        _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCrc16Address, 0xFFFF);
        _memory.InternalWrite(ModbusRegisterMap.ActiveRuleVersionAddress, _activeVersion);
        _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 0); // IDLE
        _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.NONE);

        // Bảo toàn Device Descriptor nguyên vẹn
        InitializeHardwareDescriptor();
        UpdateHealthRegisters();
    }

    private void RestoreFromFlashIfAvailable()
    {
        if (_flash.HasCommittedData)
        {
            if (_flash.Load(out ushort count, out ushort version, out ushort crc,
                _memory.InternalMutableSpan(ModbusRegisterMap.ActiveRuleTableBaseAddress, _flash.StoredRuleCount * 16)))
            {
                _activeVersion = version;
                _memory.InternalWrite(ModbusRegisterMap.RuleTableInfoAddress, count);
                _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCountAddress, count);
                _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCrc16Address, crc);
                _memory.InternalWrite(ModbusRegisterMap.ActiveRuleVersionAddress, version);
            }
        }
        else
        {
            _activeVersion = 0;
            _memory.InternalWrite(ModbusRegisterMap.RuleTableInfoAddress, 0);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCountAddress, 0);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleCrc16Address, 0xFFFF);
            _memory.InternalWrite(ModbusRegisterMap.ActiveRuleVersionAddress, 0);
        }
    }

    private void EnsureOnline()
    {
        if (!IsOnline || _faults.CableDisconnected)
        {
            throw new IOException("Simulated USB CDC communication failure: Device is offline or cable disconnected.");
        }
    }

    internal bool IsStagingRegisterWritten(int index) =>
        index >= 0 && index < _stagingRegistersWritten.Length && _stagingRegistersWritten[index];

    /// <summary>
    /// Giả lập Modbus Master bên ngoài (SCADA/HMI) gửi yêu cầu FC03 đọc thanh ghi qua cổng RS-485 Slave.
    /// Tra cứu trong bảng ánh xạ Modbus Slave tại 0x1500 và trả về giá trị Tag nội bộ trong sandbox an toàn.
    /// </summary>
    /// <summary>
    /// Phương thức hỗ trợ kiểm thử nội bộ để ghi trực tiếp vào bộ nhớ giả lập.
    /// </summary>
    public void InternalWriteRegisters(ushort startAddress, ushort[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            _memory.InternalWrite((ushort)(startAddress + i), values[i]);
        }
    }

    private void ValidateSlavePort(byte portIndex, byte slaveId)
    {
        var portBuffer = _memory.ReadRegisters(
            ModbusRegisterMap.ModbusPortConfigBaseAddress,
            ModbusRegisterMap.ModbusPortConfigTableTotalLength);
        var ports = ModbusPortConfigCodec.DecodeAllPorts(portBuffer);

        var targetPort = ports.FirstOrDefault(p => p.PortIndex == portIndex);
        if (targetPort == null || !targetPort.Enabled || targetPort.Role != ModbusPortRole.Slave)
        {
            throw new InvalidOperationException($"Port {portIndex + 1} is not configured or not running in Modbus Slave mode.");
        }

        if (targetPort.SlaveId != slaveId)
        {
            throw new InvalidOperationException($"Slave ID mismatch on Port {portIndex + 1}. Expected: {targetPort.SlaveId}, Received: {slaveId}.");
        }
    }

    private ushort ReadFixedHoldingRegister(ushort addr, SimplePLC.Protocol.Models.TagLayoutMap layout)
    {
        // 0x0000: DI_WORD (Packed DI0..DI15)
        if (addr == ModbusSlaveFixedMap.DiWordAddress)
        {
            ushort diWord = 0;
            for (int b = 0; b < Math.Min(16, (int)layout.DiCount); b++)
            {
                if (ReadTagValue((ushort)(layout.DiBase + b)) != 0)
                {
                    diWord |= (ushort)(1 << b);
                }
            }
            return diWord;
        }

        // 0x0001: DO_WORD (Packed DO0..DO15)
        if (addr == ModbusSlaveFixedMap.DoWordAddress)
        {
            ushort doWord = 0;
            for (int b = 0; b < Math.Min(16, (int)layout.DoCount); b++)
            {
                if (ReadTagValue((ushort)(layout.DoBase + b)) != 0)
                {
                    doWord |= (ushort)(1 << b);
                }
            }
            return doWord;
        }

        // 0x0002..0x0003: VFLAG_DWORD (Packed VFLAG0..VFLAG31)
        if (addr >= ModbusSlaveFixedMap.VflagDWordAddress && addr < ModbusSlaveFixedMap.VflagDWordAddress + 2)
        {
            uint vflagDWord = 0;
            for (int b = 0; b < Math.Min(32, (int)layout.VflagCount); b++)
            {
                if (ReadTagValue((ushort)(layout.VflagBase + b)) != 0)
                {
                    vflagDWord |= (1u << b);
                }
            }

            if (addr == ModbusSlaveFixedMap.VflagDWordAddress)
            {
                return (ushort)(vflagDWord >> 16);
            }
            return (ushort)(vflagDWord & 0xFFFF);
        }

        // 0x0010..0x0017: AI0..AI3 (Float32, 2 regs/ch)
        if (addr >= ModbusSlaveFixedMap.AiBaseAddress && addr < ModbusSlaveFixedMap.AiBaseAddress + ModbusSlaveFixedMap.AiTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.AiBaseAddress;
            int chIndex = offset / 2;
            int regIndex = offset % 2;
            if (chIndex < layout.AiCount)
            {
                ushort tagRegAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.AiBase + chIndex) * 2 + regIndex);
                return _memory.ReadRegisters(tagRegAddr, 1)[0];
            }
            return 0;
        }

        // 0x0020..0x005F: VREG0..VREG31 (32-bit, 2 regs/var)
        if (addr >= ModbusSlaveFixedMap.VregBaseAddress && addr < ModbusSlaveFixedMap.VregBaseAddress + ModbusSlaveFixedMap.VregTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.VregBaseAddress;
            int varIndex = offset / 2;
            int regIndex = offset % 2;
            if (varIndex < layout.VregCount)
            {
                ushort tagRegAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.VregBase + varIndex) * 2 + regIndex);
                return _memory.ReadRegisters(tagRegAddr, 1)[0];
            }
            return 0;
        }

        // 0x0060..0x009F: VREG_R0..VREG_R31 (Retentive 32-bit, 2 regs/var)
        if (addr >= ModbusSlaveFixedMap.VregRetainBaseAddress && addr < ModbusSlaveFixedMap.VregRetainBaseAddress + ModbusSlaveFixedMap.VregRetainTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.VregRetainBaseAddress;
            int varIndex = offset / 2;
            int regIndex = offset % 2;
            if (varIndex < layout.VregRetainCount)
            {
                ushort tagRegAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.VregRetainBase + varIndex) * 2 + regIndex);
                return _memory.ReadRegisters(tagRegAddr, 1)[0];
            }
            return 0;
        }

        // 0x00A0..0x00AF: COUNTER0..COUNTER7 (Int32 PV, 2 regs/unit)
        if (addr >= ModbusSlaveFixedMap.CounterBaseAddress && addr < ModbusSlaveFixedMap.CounterBaseAddress + ModbusSlaveFixedMap.CounterTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.CounterBaseAddress;
            int unitIndex = offset / 2;
            int regIndex = offset % 2;
            if (unitIndex < layout.CounterCount)
            {
                ushort tagRegAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.CounterBase + unitIndex) * 2 + regIndex);
                return _memory.ReadRegisters(tagRegAddr, 1)[0];
            }
            return 0;
        }

        // 0x00F0..0x00FF: SYS_DIAG (Uptime, Scan ms, Diag state, RTC)
        if (addr >= ModbusSlaveFixedMap.SystemDiagBaseAddress && addr < ModbusSlaveFixedMap.SystemDiagBaseAddress + ModbusSlaveFixedMap.SystemDiagTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.SystemDiagBaseAddress;
            UpdateHealthRegisters();
            UpdateRtcRegisters();
            if (offset == 0) return _memory.InternalRead(0x0800); // Uptime High Word
            if (offset == 1) return _memory.InternalRead(0x0801); // Uptime Low Word
            if (offset == 2) return _memory.InternalRead(0x0807); // Scan Time ms
            if (offset == 3) return _memory.InternalRead(ModbusRegisterMap.DiagStateAddress);
            if (offset >= 4 && offset < 4 + ModbusRegisterMap.RtcClockLength)
            {
                return _memory.InternalRead((ushort)(ModbusRegisterMap.RtcClockAddress + (offset - 4)));
            }
            return 0;
        }

        throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Modbus address 0x{addr:X04} is not valid in Slave Fixed Register Map.");
    }

    private static int GetHoldingRegisterPartition(ushort addr)
    {
        if (addr == ModbusSlaveFixedMap.DiWordAddress) return 1;
        if (addr == ModbusSlaveFixedMap.DoWordAddress) return 2;
        if (addr >= ModbusSlaveFixedMap.VflagDWordAddress && addr < ModbusSlaveFixedMap.VflagDWordAddress + 2) return 3;
        if (addr >= ModbusSlaveFixedMap.AiBaseAddress && addr < ModbusSlaveFixedMap.AiBaseAddress + ModbusSlaveFixedMap.AiTotalRegisters) return 4;
        if (addr >= ModbusSlaveFixedMap.VregBaseAddress && addr < ModbusSlaveFixedMap.VregBaseAddress + ModbusSlaveFixedMap.VregTotalRegisters) return 5;
        if (addr >= ModbusSlaveFixedMap.VregRetainBaseAddress && addr < ModbusSlaveFixedMap.VregRetainBaseAddress + ModbusSlaveFixedMap.VregRetainTotalRegisters) return 6;
        if (addr >= ModbusSlaveFixedMap.CounterBaseAddress && addr < ModbusSlaveFixedMap.CounterBaseAddress + ModbusSlaveFixedMap.CounterTotalRegisters) return 7;
        if (addr >= ModbusSlaveFixedMap.SystemDiagBaseAddress && addr < ModbusSlaveFixedMap.SystemDiagBaseAddress + ModbusSlaveFixedMap.SystemDiagTotalRegisters) return 8;
        return -1;
    }

    private static void ValidateHoldingRegisterWritable(ushort addr)
    {
        if (addr == ModbusSlaveFixedMap.DiWordAddress)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Address 0x{addr:X04} (DI_WORD) is Read-Only.");
        }

        if (addr >= ModbusSlaveFixedMap.AiBaseAddress && addr < ModbusSlaveFixedMap.AiBaseAddress + ModbusSlaveFixedMap.AiTotalRegisters)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Address 0x{addr:X04} (AI) is Read-Only.");
        }

        if (addr >= ModbusSlaveFixedMap.SystemDiagBaseAddress && addr < ModbusSlaveFixedMap.SystemDiagBaseAddress + ModbusSlaveFixedMap.SystemDiagTotalRegisters)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Address 0x{addr:X04} (SYS_DIAG) is Read-Only.");
        }

        bool isWritable =
            addr == ModbusSlaveFixedMap.DoWordAddress ||
            (addr >= ModbusSlaveFixedMap.VflagDWordAddress && addr < ModbusSlaveFixedMap.VflagDWordAddress + 2) ||
            (addr >= ModbusSlaveFixedMap.VregBaseAddress && addr < ModbusSlaveFixedMap.VregBaseAddress + ModbusSlaveFixedMap.VregTotalRegisters) ||
            (addr >= ModbusSlaveFixedMap.VregRetainBaseAddress && addr < ModbusSlaveFixedMap.VregRetainBaseAddress + ModbusSlaveFixedMap.VregRetainTotalRegisters) ||
            (addr >= ModbusSlaveFixedMap.CounterBaseAddress && addr < ModbusSlaveFixedMap.CounterBaseAddress + ModbusSlaveFixedMap.CounterTotalRegisters);

        if (!isWritable)
        {
            throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Modbus address 0x{addr:X04} is not valid in Slave Fixed Register Map.");
        }
    }

    private void WriteFixedHoldingRegister(ushort addr, ushort val, SimplePLC.Protocol.Models.TagLayoutMap layout)
    {
        if (addr == ModbusSlaveFixedMap.DoWordAddress)
        {
            for (int b = 0; b < Math.Min(16, (int)layout.DoCount); b++)
            {
                bool bitOn = (val & (1 << b)) != 0;
                WriteTagValue((ushort)(layout.DoBase + b), bitOn ? 1 : 0);
            }
            return;
        }

        if (addr == ModbusSlaveFixedMap.VflagDWordAddress)
        {
            // Bits 16..31
            for (int b = 0; b < Math.Min(16, Math.Max(0, layout.VflagCount - 16)); b++)
            {
                bool bitOn = (val & (1 << b)) != 0;
                WriteTagValue((ushort)(layout.VflagBase + 16 + b), bitOn ? 1 : 0);
            }
            return;
        }

        if (addr == (ushort)(ModbusSlaveFixedMap.VflagDWordAddress + 1))
        {
            // Bits 0..15
            for (int b = 0; b < Math.Min(16, (int)layout.VflagCount); b++)
            {
                bool bitOn = (val & (1 << b)) != 0;
                WriteTagValue((ushort)(layout.VflagBase + b), bitOn ? 1 : 0);
            }
            return;
        }

        if (addr >= ModbusSlaveFixedMap.VregBaseAddress && addr < ModbusSlaveFixedMap.VregBaseAddress + ModbusSlaveFixedMap.VregTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.VregBaseAddress;
            int varIndex = offset / 2;
            int regIndex = offset % 2;
            if (varIndex < layout.VregCount)
            {
                ushort tagRegAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.VregBase + varIndex) * 2 + regIndex);
                _memory.InternalWrite(tagRegAddr, val);
            }
            return;
        }

        if (addr >= ModbusSlaveFixedMap.VregRetainBaseAddress && addr < ModbusSlaveFixedMap.VregRetainBaseAddress + ModbusSlaveFixedMap.VregRetainTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.VregRetainBaseAddress;
            int varIndex = offset / 2;
            int regIndex = offset % 2;
            if (varIndex < layout.VregRetainCount)
            {
                ushort tagRegAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.VregRetainBase + varIndex) * 2 + regIndex);
                _memory.InternalWrite(tagRegAddr, val);
            }
            return;
        }

        if (addr >= ModbusSlaveFixedMap.CounterBaseAddress && addr < ModbusSlaveFixedMap.CounterBaseAddress + ModbusSlaveFixedMap.CounterTotalRegisters)
        {
            int offset = addr - ModbusSlaveFixedMap.CounterBaseAddress;
            int unitIndex = offset / 2;
            int regIndex = offset % 2;
            if (unitIndex < layout.CounterCount)
            {
                ushort tagRegAddr = (ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.CounterBase + unitIndex) * 2 + regIndex);
                _memory.InternalWrite(tagRegAddr, val);
            }
            return;
        }
    }

    /// <summary>
    /// Giả lập Modbus Master đọc các thanh ghi giữ (FC 03) từ cổng RS-485 Slave theo bản đồ địa chỉ cố định.
    /// </summary>
    public Task<ushort[]> ExecuteSlaveReadHoldingRegistersAsync(
        byte portIndex,
        byte slaveId,
        ushort modbusAddress,
        ushort count,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureOnline();
        ValidateSlavePort(portIndex, slaveId);

        if (count == 0 || count > 125)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Count {count} must be between 1 and 125.");
        }

        ushort endAddress = (ushort)(modbusAddress + count - 1);
        int startPart = GetHoldingRegisterPartition(modbusAddress);
        int endPart = GetHoldingRegisterPartition(endAddress);
        if (startPart == -1 || endPart == -1 || startPart != endPart)
        {
            throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Request range 0x{modbusAddress:X04}..0x{endAddress:X04} is invalid or spans across partition boundaries.");
        }

        var layout = GetTagLayout();

        // Kiểm tra tính hợp lệ của toàn bộ dải trước khi đọc
        for (ushort i = 0; i < count; i++)
        {
            ushort addr = (ushort)(modbusAddress + i);
            _ = ReadFixedHoldingRegister(addr, layout);
        }

        var result = new ushort[count];
        for (ushort i = 0; i < count; i++)
        {
            ushort addr = (ushort)(modbusAddress + i);
            result[i] = ReadFixedHoldingRegister(addr, layout);
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Giả lập Modbus Master gửi yêu cầu FC06/FC16 ghi thanh ghi giữ qua cổng RS-485 Slave.
    /// Áp dụng cơ chế Sandbox: Cấm ghi ngõ vào vật lý/cảm biến (Exception 0x03), cấm ghi ngoài bản đồ cố định (Exception 0x02).
    /// </summary>
    public Task ExecuteSlaveWriteHoldingRegistersAsync(
        byte portIndex,
        byte slaveId,
        ushort modbusAddress,
        ushort[] values,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureOnline();
        ValidateSlavePort(portIndex, slaveId);

        if (values == null || values.Length == 0 || values.Length > 123)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Values count must be between 1 and 123.");
        }

        ushort endAddress = (ushort)(modbusAddress + values.Length - 1);
        int startPart = GetHoldingRegisterPartition(modbusAddress);
        int endPart = GetHoldingRegisterPartition(endAddress);
        if (startPart == -1 || endPart == -1 || startPart != endPart)
        {
            throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Request range 0x{modbusAddress:X04}..0x{endAddress:X04} is invalid or spans across partition boundaries.");
        }

        var layout = GetTagLayout();

        // 1. Kiểm tra tính hợp lệ và quyền ghi cho toàn bộ khối thanh ghi (Atomic check)
        for (int i = 0; i < values.Length; i++)
        {
            ushort addr = (ushort)(modbusAddress + i);
            ValidateHoldingRegisterWritable(addr);
        }

        // 2. Thực hiện ghi giá trị
        for (int i = 0; i < values.Length; i++)
        {
            ushort addr = (ushort)(modbusAddress + i);
            WriteFixedHoldingRegister(addr, values[i], layout);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Giả lập Modbus Master đọc trạng thái các chân ngõ vào số DI0..DI15 (FC 02) theo địa chỉ bit cố định.
    /// </summary>
    public Task<bool[]> ExecuteSlaveReadDiscreteInputsAsync(
        byte portIndex,
        byte slaveId,
        ushort inputAddress,
        ushort count,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureOnline();
        ValidateSlavePort(portIndex, slaveId);

        if (count == 0 || count > 2000)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Count {count} must be between 1 and 2000.");
        }

        if (inputAddress < ModbusSlaveFixedMap.DiscreteInputsDiBaseAddress ||
            (inputAddress + count) > (ModbusSlaveFixedMap.DiscreteInputsDiBaseAddress + ModbusSlaveFixedMap.DiscreteInputsDiCount))
        {
            throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Input address range {inputAddress}..{inputAddress + count - 1} is out of bounds.");
        }

        var layout = GetTagLayout();
        var result = new bool[count];
        for (int i = 0; i < count; i++)
        {
            int diIdx = (inputAddress - ModbusSlaveFixedMap.DiscreteInputsDiBaseAddress) + i;
            if (diIdx < layout.DiCount)
            {
                result[i] = ReadTagValue((ushort)(layout.DiBase + diIdx)) != 0;
            }
        }

        return Task.FromResult(result);
    }

    /// <summary>
    /// Giả lập Modbus Master đọc trạng thái Coils (FC 01) DO0..DO15 hoặc VFLAG0..VFLAG31 theo địa chỉ bit cố định.
    /// </summary>
    public Task<bool[]> ExecuteSlaveReadCoilsAsync(
        byte portIndex,
        byte slaveId,
        ushort coilAddress,
        ushort count,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureOnline();
        ValidateSlavePort(portIndex, slaveId);

        if (count == 0 || count > 2000)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Count {count} must be between 1 and 2000.");
        }

        var layout = GetTagLayout();
        var result = new bool[count];

        // Vùng DO: 0x0000 .. 0x000F
        if (coilAddress >= ModbusSlaveFixedMap.CoilsDoBaseAddress &&
            (coilAddress + count) <= (ModbusSlaveFixedMap.CoilsDoBaseAddress + ModbusSlaveFixedMap.CoilsDoCount))
        {
            for (int i = 0; i < count; i++)
            {
                int doIdx = (coilAddress - ModbusSlaveFixedMap.CoilsDoBaseAddress) + i;
                if (doIdx < layout.DoCount)
                {
                    result[i] = ReadTagValue((ushort)(layout.DoBase + doIdx)) != 0;
                }
            }
            return Task.FromResult(result);
        }

        // Vùng VFLAG: 0x0064 .. 0x0083 (100 .. 131)
        if (coilAddress >= ModbusSlaveFixedMap.CoilsVflagBaseAddress &&
            (coilAddress + count) <= (ModbusSlaveFixedMap.CoilsVflagBaseAddress + ModbusSlaveFixedMap.CoilsVflagCount))
        {
            for (int i = 0; i < count; i++)
            {
                int vfIdx = (coilAddress - ModbusSlaveFixedMap.CoilsVflagBaseAddress) + i;
                if (vfIdx < layout.VflagCount)
                {
                    result[i] = ReadTagValue((ushort)(layout.VflagBase + vfIdx)) != 0;
                }
            }
            return Task.FromResult(result);
        }

        throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Coil address range {coilAddress}..{coilAddress + count - 1} is out of bounds.");
    }

    /// <summary>
    /// Giả lập Modbus Master ghi trạng thái Coils (FC 05/15) DO0..DO15 hoặc VFLAG0..VFLAG31 theo địa chỉ bit cố định.
    /// </summary>
    public Task ExecuteSlaveWriteCoilsAsync(
        byte portIndex,
        byte slaveId,
        ushort coilAddress,
        bool[] values,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        EnsureOnline();
        ValidateSlavePort(portIndex, slaveId);

        if (values == null || values.Length == 0 || values.Length > 1968)
        {
            throw new InvalidOperationException($"Modbus Exception 0x03 (ILLEGAL_DATA_VALUE): Values count must be between 1 and 1968.");
        }

        var layout = GetTagLayout();
        int count = values.Length;

        // Vùng DO: 0x0000 .. 0x000F
        if (coilAddress >= ModbusSlaveFixedMap.CoilsDoBaseAddress &&
            (coilAddress + count) <= (ModbusSlaveFixedMap.CoilsDoBaseAddress + ModbusSlaveFixedMap.CoilsDoCount))
        {
            for (int i = 0; i < count; i++)
            {
                int doIdx = (coilAddress - ModbusSlaveFixedMap.CoilsDoBaseAddress) + i;
                if (doIdx < layout.DoCount)
                {
                    WriteTagValue((ushort)(layout.DoBase + doIdx), values[i] ? 1 : 0);
                }
            }
            return Task.CompletedTask;
        }

        // Vùng VFLAG: 0x0064 .. 0x0083
        if (coilAddress >= ModbusSlaveFixedMap.CoilsVflagBaseAddress &&
            (coilAddress + count) <= (ModbusSlaveFixedMap.CoilsVflagBaseAddress + ModbusSlaveFixedMap.CoilsVflagCount))
        {
            for (int i = 0; i < count; i++)
            {
                int vfIdx = (coilAddress - ModbusSlaveFixedMap.CoilsVflagBaseAddress) + i;
                if (vfIdx < layout.VflagCount)
                {
                    WriteTagValue((ushort)(layout.VflagBase + vfIdx), values[i] ? 1 : 0);
                }
            }
            return Task.CompletedTask;
        }

        throw new InvalidOperationException($"Modbus Exception 0x02 (ILLEGAL_DATA_ADDRESS): Coil address range {coilAddress}..{coilAddress + count - 1} is out of bounds.");
    }
}
