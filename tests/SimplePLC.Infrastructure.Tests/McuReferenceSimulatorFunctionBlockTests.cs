using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Phase 1 Verification: Kiểm thử toàn diện Lõi Mô Phỏng MCU (MCU Reference Simulator FB Engine)
/// Kiểm chứng hành vi thời gian thực của Timer (TON, TOF, TP), Counter (CTU, CTD), ngắt khẩn cấp,
/// sườn xung và đồng bộ hóa Flash Retentive theo Mục 9.5 của Data Contract V2.
/// </summary>
public class McuReferenceSimulatorFunctionBlockTests
{
    [Fact]
    public void Timer_TON_CountsElapsedTime_AndSetsQWhenPresetReached()
    {
        var sim = new McuReferenceSimulator();
        var timer = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TON,
            PresetMs = 100,
            In = true
        };
        sim.Control.SetTimer(0, timer);

        // Scan 1: 30ms -> Elapsed = 30ms, Running = true, Q = false
        sim.ExecuteScanPass(30);
        var t = sim.Control.GetTimer(0);
        Assert.Equal(30u, t.ElapsedMs);
        Assert.True(t.Running);
        Assert.False(t.Q);

        // Scan 2: 40ms -> Elapsed = 70ms, Running = true, Q = false
        sim.ExecuteScanPass(40);
        t = sim.Control.GetTimer(0);
        Assert.Equal(70u, t.ElapsedMs);
        Assert.True(t.Running);
        Assert.False(t.Q);

        // Scan 3: 30ms -> Elapsed = 100ms, Running = false, Q = true
        sim.ExecuteScanPass(30);
        t = sim.Control.GetTimer(0);
        Assert.Equal(100u, t.ElapsedMs);
        Assert.False(t.Running);
        Assert.True(t.Q);
    }

    [Fact]
    public void Timer_TON_ResetsImmediately_WhenResetPinEnergized()
    {
        var sim = new McuReferenceSimulator();
        var timer = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TON,
            PresetMs = 500,
            In = true
        };
        sim.Control.SetTimer(0, timer);

        // Đếm được 200ms
        sim.ExecuteScanPass(200);
        Assert.Equal(200u, sim.Control.GetTimer(0).ElapsedMs);

        // Kích chân Reset = true
        var tNow = sim.Control.GetTimer(0);
        tNow.Reset = true;
        sim.Control.SetTimer(0, tNow);

        // Scan tiếp theo: Phải bị ngắt và đưa về 0 ngay lập tức
        sim.ExecuteScanPass(10);
        var tAfter = sim.Control.GetTimer(0);
        Assert.Equal(0u, tAfter.ElapsedMs);
        Assert.False(tAfter.Running);
        Assert.False(tAfter.Q);
    }

    [Fact]
    public void Timer_TON_ResetsImmediately_WhenInputDrops()
    {
        var sim = new McuReferenceSimulator();
        var timer = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TON,
            PresetMs = 500,
            In = true
        };
        sim.Control.SetTimer(0, timer);

        sim.ExecuteScanPass(150);
        Assert.Equal(150u, sim.Control.GetTimer(0).ElapsedMs);

        // Tín hiệu In rớt xuống 0
        var tNow = sim.Control.GetTimer(0);
        tNow.In = false;
        sim.Control.SetTimer(0, tNow);

        sim.ExecuteScanPass(10);
        var tAfter = sim.Control.GetTimer(0);
        Assert.Equal(0u, tAfter.ElapsedMs);
        Assert.False(tAfter.Running);
        Assert.False(tAfter.Q);
    }

    [Fact]
    public void Timer_TOF_MaintainsOutput_DuringTimingAfterInputDrops()
    {
        var sim = new McuReferenceSimulator();
        var timer = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TOF,
            PresetMs = 50,
            In = true
        };
        sim.Control.SetTimer(1, timer);

        // Khi In = 1, TOF đóng ngõ ra Q ngay lập tức
        sim.ExecuteScanPass(10);
        var t = sim.Control.GetTimer(1);
        Assert.True(t.Q);
        Assert.Equal(0u, t.ElapsedMs);

        // Khi In rớt xuống 0: TOF duy trì Q = 1 và bắt đầu tính thời gian trễ ngắt
        t.In = false;
        sim.Control.SetTimer(1, t);

        sim.ExecuteScanPass(30);
        t = sim.Control.GetTimer(1);
        Assert.True(t.Q);
        Assert.True(t.Running);
        Assert.Equal(30u, t.ElapsedMs);

        // Hết thời gian trễ ngắt (thêm 20ms = 50ms) -> Q ngắt về false
        sim.ExecuteScanPass(20);
        t = sim.Control.GetTimer(1);
        Assert.False(t.Q);
        Assert.False(t.Running);
        Assert.Equal(50u, t.ElapsedMs);
    }

    [Fact]
    public void Timer_TP_GeneratesPulse_ForPresetDuration()
    {
        var sim = new McuReferenceSimulator();
        var timer = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.TP,
            PresetMs = 100,
            In = false
        };
        sim.Control.SetTimer(2, timer);
        sim.ExecuteScanPass(10);

        // Sườn lên của In: Bắt đầu phát xung
        timer.In = true;
        sim.Control.SetTimer(2, timer);
        sim.ExecuteScanPass(40);
        var t = sim.Control.GetTimer(2);
        Assert.True(t.Q);
        Assert.True(t.Running);
        Assert.Equal(40u, t.ElapsedMs);

        // Ngay cả khi In rớt sớm, TP vẫn duy trì xung cho đủ Preset
        t.In = false;
        sim.Control.SetTimer(2, t);
        sim.ExecuteScanPass(40);
        t = sim.Control.GetTimer(2);
        Assert.True(t.Q);
        Assert.Equal(80u, t.ElapsedMs);

        // Đến 100ms -> Xung kết thúc
        sim.ExecuteScanPass(20);
        t = sim.Control.GetTimer(2);
        Assert.False(t.Q);
        Assert.False(t.Running);
        Assert.Equal(100u, t.ElapsedMs);
    }

    [Fact]
    public void Counter_CTU_IncrementsOnRisingEdgeOnly_NotOnHeldLevel()
    {
        var sim = new McuReferenceSimulator();
        var counter = new FbCounterRecordDto
        {
            Mode = SPLC_CounterMode.CTU,
            PresetValue = 5,
            CurrentValue = 0,
            Cu = false
        };
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);

        // Sườn lên 1: 0 -> 1
        counter.Cu = true;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);
        Assert.Equal(1, sim.Control.GetCounter(0).CurrentValue);

        // Mức giữ cao (Held High): 1 -> 1 (Không được tăng thêm!)
        sim.ExecuteScanPass(10);
        Assert.Equal(1, sim.Control.GetCounter(0).CurrentValue);

        // Rớt xuống 0
        counter = sim.Control.GetCounter(0);
        counter.Cu = false;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);
        Assert.Equal(1, sim.Control.GetCounter(0).CurrentValue);

        // Sườn lên 2: 0 -> 1 -> Phải tăng lên 2
        counter = sim.Control.GetCounter(0);
        counter.Cu = true;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);
        Assert.Equal(2, sim.Control.GetCounter(0).CurrentValue);
    }

    [Fact]
    public void Counter_CTU_TripsQWhenReachingPV_AndResetsOnResetPin()
    {
        var sim = new McuReferenceSimulator();
        var counter = new FbCounterRecordDto
        {
            Mode = SPLC_CounterMode.CTU,
            PresetValue = 2,
            CurrentValue = 0
        };
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);

        // Xung 1
        counter.Cu = true;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);

        counter = sim.Control.GetCounter(0);
        counter.Cu = false;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);
        Assert.False(sim.Control.GetCounter(0).Q);

        // Xung 2 -> Đạt PV=2 -> Q = true
        counter = sim.Control.GetCounter(0);
        counter.Cu = true;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);
        Assert.Equal(2, sim.Control.GetCounter(0).CurrentValue);
        Assert.True(sim.Control.GetCounter(0).Q);

        // Kích sườn lên của Reset -> CV về 0, Q về false
        counter = sim.Control.GetCounter(0);
        counter.Reset = true;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);

        var cAfter = sim.Control.GetCounter(0);
        Assert.Equal(0, cAfter.CurrentValue);
        Assert.False(cAfter.Q);
    }

    [Fact]
    public void Counter_Synchronizes_With_VREG_RETAIN()
    {
        var sim = new McuReferenceSimulator();
        var counter = new FbCounterRecordDto
        {
            Mode = SPLC_CounterMode.CTU,
            PresetValue = 100,
            CurrentValue = 0,
            RetainTagIndex = 84 // VREG_RETAIN0
        };
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);

        // Xung 1
        counter.Cu = true;
        sim.Control.SetCounter(0, counter);
        sim.ExecuteScanPass(10);

        // Kiểm tra xem TagIndex 84 (VREG_RETAIN0) có tự động mang giá trị 1 không
        int vregValue = sim.Control.GetTagValue(84);
        Assert.Equal(1, vregValue);
        Assert.Equal(1, sim.Control.GetCounter(0).CurrentValue);
    }
}
