using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class McuReferenceSimulatorScanEngineTests
{
    private static void CommitRules(McuReferenceSimulator sim, params RuleRecordDto[] rules)
    {
        sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.RuleCountStagedAddress, (ushort)rules.Length).GetAwaiter().GetResult();
        ushort[] stagedBuf = new ushort[rules.Length * ModbusRegisterMap.RegistersPerRule];
        SimplePLC.Protocol.Codec.RegisterCodec.EncodeRuleRecords(rules, stagedBuf);

        sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, stagedBuf).GetAwaiter().GetResult();

        ushort crc = SimplePLC.Protocol.Cryptography.Crc16Modbus.ComputeFromRegisters(stagedBuf);
        sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.ExpectedCrc16Address, crc).GetAwaiter().GetResult();
        sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic).GetAwaiter().GetResult();
    }

    [Fact]
    public void ExecuteScanPass_OnRiseRule_SetsOutputWhenInputTransitionsHigh()
    {
        var sim = new McuReferenceSimulator();
        // Rule: DI0 (tag 0) OnRise -> Set DO0 (tag 8) = 1
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            TriggerTag = 0, // DI0
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 8,  // DO0
            ActionParam = 1
        };
        CommitRules(sim, rule);

        // Initial scan: DI0 = 0 -> DO0 = 0
        sim.ExecuteScanPass(20);
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Set DI0 = 1
        sim.Control.SetTagValue(0, 1);
        sim.ExecuteScanPass(20);

        // Assert: DO0 should be 1
        Assert.Equal(1, sim.Control.GetTagValue(8));
    }

    [Fact]
    public void ExecuteScanPass_OnFallRule_ResetsOutputWhenInputTransitionsLow()
    {
        var sim = new McuReferenceSimulator();
        // Rule 1: DI0 (tag 0) OnRise -> Set DO0 (tag 8) = 1
        // Rule 2: DI0 (tag 0) OnFall -> Set DO0 (tag 8) = 0
        var rule1 = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            TriggerTag = 0,
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 8,
            ActionParam = 1
        };
        var rule2 = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_FALL,
            TriggerTag = 0,
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 8,
            ActionParam = 0
        };
        CommitRules(sim, rule1, rule2);

        // Step 1: Turn ON
        sim.Control.SetTagValue(0, 1);
        sim.ExecuteScanPass(20);
        Assert.Equal(1, sim.Control.GetTagValue(8));

        // Step 2: Turn OFF
        sim.Control.SetTagValue(0, 0);
        sim.ExecuteScanPass(20);
        Assert.Equal(0, sim.Control.GetTagValue(8));
    }

    [Fact]
    public void ExecuteScanPass_DwellTimer_FiresOnlyAfterDwellDurationReached()
    {
        var sim = new McuReferenceSimulator();
        // Rule: DI0 OnRise with ForMs = 100ms -> Set DO0 = 1
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            TriggerTag = 0,
            ForMs = 100,
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 8,
            ActionParam = 1
        };
        CommitRules(sim, rule);

        sim.Control.SetTagValue(0, 1);

        // Scan 1: 40ms -> DO0 still 0
        sim.ExecuteScanPass(40);
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Scan 2: 40ms (total 80ms) -> DO0 still 0
        sim.ExecuteScanPass(40);
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Scan 3: 30ms (total 110ms >= 100ms) -> DO0 should turn ON (1)
        sim.ExecuteScanPass(30);
        Assert.Equal(1, sim.Control.GetTagValue(8));
    }

    [Fact]
    public void ExecuteScanPass_AnalogThreshold_FiresWhenThresholdCrossed()
    {
        var sim = new McuReferenceSimulator();
        // Rule: AI0 (tag 16) > 5000 -> Set DO1 (tag 9) = 1
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_CHANGE,
            TriggerTag = 16, // AI0
            CompareOp = SPLC_CompareOp.GT,
            ThresholdLo = 5000,
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 9,   // DO1
            ActionParam = 1
        };
        CommitRules(sim, rule);

        // Below threshold: 3000 mV
        sim.Control.SetTagValue(16, 3000);
        sim.ExecuteScanPass(20);
        Assert.Equal(0, sim.Control.GetTagValue(9));

        // Above threshold: 6500 mV
        sim.Control.SetTagValue(16, 6500);
        sim.ExecuteScanPass(20);
        Assert.Equal(1, sim.Control.GetTagValue(9));
    }

    [Fact]
    public void ExecuteScanPass_GuardTag_BlocksActionWhenGuardIsClosed()
    {
        var sim = new McuReferenceSimulator();
        // Rule: DI0 OnRise -> Set DO0 = 1, GUARDED by DI1 (tag 1)
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_RISE,
            TriggerTag = 0,
            GuardTag = 1, // DI1 must be 1
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 8,
            ActionParam = 1
        };
        CommitRules(sim, rule);

        // DI1 = 0 (guard closed), DI0 = 1
        sim.Control.SetTagValue(1, 0);
        sim.Control.SetTagValue(0, 1);
        sim.ExecuteScanPass(20);
        Assert.Equal(0, sim.Control.GetTagValue(8)); // Blocked

        // Reset DI0 to 0
        sim.Control.SetTagValue(0, 0);
        sim.ExecuteScanPass(20);

        // Now open guard: DI1 = 1, and pulse DI0: 0 -> 1
        sim.Control.SetTagValue(1, 1);
        sim.Control.SetTagValue(0, 1);
        sim.ExecuteScanPass(20);
        Assert.Equal(1, sim.Control.GetTagValue(8)); // Passed
    }

    // ──────────────────────────────────────────────────────────────────────────
    // SAFETY GATE — CRIT-R3-01: Rule Engine phải nhường quyền khi DIAG_CONTROL
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Khi kỹ sư ở chế độ DIAG_CONTROL và cưỡng bức DO0 = 1,
    /// Rule Engine KHÔNG được ghi đè lại dù trigger của rule đã được thỏa mãn.
    /// Đây là safety gate để tránh Race Condition trên dây chuyền công nghiệp.
    /// </summary>
    [Fact]
    public async Task ExecuteScanPass_WhenDiagControl_RuleEngineDoesNotOverwriteForcedOutput()
    {
        var sim = new McuReferenceSimulator(wireProfile: 2);

        // 1. Deploy rule: DI0 (tag 0) OnChange == 1 → SET DO0 (tag 8) = 0
        //    (Mô phỏng rule "tắt DO0 khi DI0 lên HIGH")
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_CHANGE,
            TriggerTag = 0,   // DI0
            CompareOp = SPLC_CompareOp.EQ,
            ThresholdLo = 1,
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 8,    // DO0
            ActionParam = 0   // ghi 0 (tắt)
        };
        CommitRules(sim, rule);

        // 2. ENTER_DIAG qua Modbus write (ghi 0x0001 vào 0x0A20)
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.DiagCommandAddress,
            (ushort)SPLC_DiagCommand.ENTER_DIAG);

        // 3. Kỹ sư cưỡng bức DO0 = 1 (forced open)
        sim.Control.SetTagValue(8, 1);

        // 4. Kích hoạt điều kiện trigger của rule (DI0 = 1)
        sim.Control.SetTagValue(0, 1);

        // 5. Chạy scan pass — Rule Engine phải bị suspend hoàn toàn
        sim.ExecuteScanPass(20);

        // Assert: DO0 phải VẪN = 1, Rule Engine không được ghi đè
        var do0Value = sim.Control.GetTagValue(8);
        Assert.Equal(1, do0Value);
    }

    /// <summary>
    /// Khi đang ở DIAG_CONTROL, Watchdog Lease countdown vẫn phải hoạt động bình thường.
    /// Sau khi Lease hết hạn → tự động chuyển về ENGINE_RUNNING và Rule Engine tiếp tục thực thi.
    /// </summary>
    [Fact]
    public async Task ExecuteScanPass_WhenDiagControlLeaseExpires_RuleEngineResumesAutomatically()
    {
        var sim = new McuReferenceSimulator(wireProfile: 2);

        // 1. Deploy rule: DI0 = 1 → SET DO0 = 1
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_CHANGE,
            TriggerTag = 0,
            CompareOp = SPLC_CompareOp.EQ,
            ThresholdLo = 1,
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = 8,
            ActionParam = 1
        };
        CommitRules(sim, rule);
        sim.Control.SetTagValue(0, 1); // DI0 = 1 (condition met)

        // 2. ENTER_DIAG
        await sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.DiagCommandAddress,
            (ushort)SPLC_DiagCommand.ENTER_DIAG);

        // 3. Scan 50 lần × 20ms = 1000ms — Lease còn (3000ms default), rule bị suspend
        for (int i = 0; i < 50; i++)
            sim.ExecuteScanPass(20);
        Assert.Equal(0, sim.Control.GetTagValue(8)); // Rule vẫn bị chặn

        // 4. Tiêu hết Lease (3000ms): scan thêm 150 lần × 20ms = 3000ms
        for (int i = 0; i < 150; i++)
            sim.ExecuteScanPass(20);

        // 5. Sau khi Lease expired, scan thêm 1 lần → Rule Engine phải tự khôi phục
        sim.ExecuteScanPass(20);
        Assert.Equal(1, sim.Control.GetTagValue(8)); // Rule đã được thực thi lại
    }
}
