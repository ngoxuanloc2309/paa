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

    public McuReferenceSimulator(ushort wireProfile = 1)
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
        _memory.InternalWrite(5, 1); // FW Major
        _memory.InternalWrite(6, 7); // FW Minor
        _memory.InternalWrite(7, 0); // FW Patch
        _memory.InternalWrite(8, 1); // Protocol Version (0x0107 / v1.7)
        _memory.InternalWrite(9, 1); // Rule Format Version (v1.7)

        // 0x0020 - 0x0029: Device Resource Info (Wire Profile V1 / Contract V1.9)
        var defaultResourceInfo = DeviceResourceInfoDto.CreateRemoteIo8Di8Do4Ai();
        defaultResourceInfo.WireProfile = _wireProfile;
        var resourceRegs = RegisterCodec.EncodeDeviceResourceInfo(defaultResourceInfo);
        for (int i = 0; i < resourceRegs.Length; i++)
        {
            _memory.InternalWrite((ushort)(ModbusRegisterMap.DeviceResourceInfoAddress + i), resourceRegs[i]);
        }
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
                                    condMet = EvaluateCompare(rule.CompareOp, currVal, rule.ThresholdLo, rule.ThresholdHi);
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

    private static bool EvaluateCompare(SPLC_CompareOp op, int val, int lo, int hi) => op switch
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
                int trigVal = ReadTagValue(rule.TriggerTag);
                int scaledVal = (int)((long)trigVal * rule.ActionParam / 1000 + rule.ThresholdHi);
                WriteTagValue(rule.ActionTag, scaledVal);
                break;
            case SPLC_ActionType.ADD_TAG:
                int tVal = ReadTagValue(rule.TriggerTag);
                int aVal = ReadTagValue(rule.ActionTag);
                WriteTagValue(rule.ActionTag, aVal + tVal);
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

        // 1. Kiểm tra Completeness: phải có số rule > 0 và toàn bộ register đều đã ghi
        int requiredRegisters = _stagedRuleCount * ModbusRegisterMap.RegistersPerRule;
        if (requiredRegisters == 0)
        {
            _memory.InternalWrite(ModbusRegisterMap.ConfigStatusAddress, 4); // ERROR
            _memory.InternalWrite(ModbusRegisterMap.ConfigErrorCodeAddress, (ushort)SPLC_ErrorCode.INVALID_PARAMETER);
            return;
        }

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
}
