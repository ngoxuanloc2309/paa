using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class RtcClockAndSchedulerTests
{
    [Fact]
    public async Task RtcClockClient_ReadAndWrite_SynchronizesWithMcu()
    {
        var simulator = new McuReferenceSimulator(wireProfile: 1);
        var client = new FakeModbusClient(simulator, isConnected: true);
        var rtcClient = new RtcClockClient(client);

        // Ban đầu chưa sync
        var initial = await rtcClient.ReadRtcClockAsync();
        Assert.False(initial.IsSynced);

        // Đồng bộ thời gian: 2026-09-30 07:00:00 UTC+7 (12:00 UTC)
        var utcDate = new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero); // 00:00 UTC = 07:00 UTC+7
        uint epochSeconds = (uint)utcDate.ToUnixTimeSeconds();

        var syncDto = new RtcClockDto
        {
            EpochUtcSeconds = epochSeconds,
            TimezoneOffsetMinutes = 420, // UTC+7
            IsSynced = true,
            HasHardwareRtc = false,
            IsBatteryLow = false
        };

        await rtcClient.WriteRtcClockAsync(syncDto);

        var readBack = await rtcClient.ReadRtcClockAsync();
        Assert.True(readBack.IsSynced);
        Assert.Equal(epochSeconds, readBack.EpochUtcSeconds);
        Assert.Equal(420, readBack.TimezoneOffsetMinutes);
        Assert.Equal(7, readBack.LocalDateTime.Hour);
        Assert.Equal(0, readBack.LocalDateTime.Minute);
        Assert.Equal(700, readBack.LocalHhmm);
    }

    [Fact]
    public async Task TimeWindowRule_PointInTimeAlarm_TriggersOnceAtTargetMinute()
    {
        var simulator = new McuReferenceSimulator(wireProfile: 1);
        var client = new FakeModbusClient(simulator, isConnected: true);
        var rtcClient = new RtcClockClient(client);

        // Đặt giờ MCU: 2026-09-30 06:59:50 UTC+7
        var startTime = new DateTimeOffset(2026, 9, 30, 6, 59, 50, TimeSpan.FromHours(7));
        await rtcClient.WriteRtcClockAsync(new RtcClockDto
        {
            EpochUtcSeconds = (uint)startTime.ToUnixTimeSeconds(),
            TimezoneOffsetMinutes = 420,
            IsSynced = true
        });

        // Nạp Rule: Lúc 07:00 (700) -> SetTag DO0 = 1
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.TIME_WINDOW,
            CompareOp = SPLC_CompareOp.EQ,
            ThresholdLo = 700, // 07:00
            ThresholdHi = 0,
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = ModbusRegisterMap.DoBaseIndex, // DO0 (tag 8)
            ActionParam = 1,
            GuardTag = ModbusRegisterMap.GuardTagNone
        };

        simulator.Control.SetActiveRule(0, rule);

        // DO0 ban đầu phải là 0
        Assert.Equal(0, simulator.Control.GetTagValue(ModbusRegisterMap.DoBaseIndex));

        // Scan 5 giây (chưa tới 7:00)
        for (int i = 0; i < 50; i++)
        {
            simulator.Control.ExecuteScanPass(deltaMs: 100);
        }
        Assert.Equal(0, simulator.Control.GetTagValue(ModbusRegisterMap.DoBaseIndex));

        // Scan thêm 6 giây (vượt qua mốc 7:00:00)
        for (int i = 0; i < 60; i++)
        {
            simulator.Control.ExecuteScanPass(deltaMs: 100);
        }

        // DO0 phải được kích hoạt bật lên 1!
        Assert.Equal(1, simulator.Control.GetTagValue(ModbusRegisterMap.DoBaseIndex));
    }

    [Fact]
    public async Task TimeWindowRule_CrossMidnightRange_TriggersCorrectly()
    {
        var simulator = new McuReferenceSimulator(wireProfile: 1);
        var client = new FakeModbusClient(simulator, isConnected: true);
        var rtcClient = new RtcClockClient(client);

        // Rule: Khung giờ qua đêm từ 18:00 (1800) đến 06:00 (600) -> SetTag DO1 = 1
        var rule = new RuleRecordDto
        {
            Enabled = true,
            TriggerType = SPLC_TriggerType.TIME_WINDOW,
            CompareOp = SPLC_CompareOp.BETWEEN,
            ThresholdLo = 1800, // 18:00
            ThresholdHi = 600,  // 06:00 sáng hôm sau
            ActionType = SPLC_ActionType.SET_TAG,
            ActionTag = (ushort)(ModbusRegisterMap.DoBaseIndex + 1), // DO1 (tag 9)
            ActionParam = 1,
            GuardTag = ModbusRegisterMap.GuardTagNone
        };

        simulator.Control.SetActiveRule(0, rule);

        ushort do1TagIndex = (ushort)(ModbusRegisterMap.DoBaseIndex + 1);

        // Test 1: Lúc 12:00 trưa (nằm ngoài khung giờ 18:00 -> 06:00) -> DO1 không bật
        var noon = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.FromHours(7));
        await rtcClient.WriteRtcClockAsync(new RtcClockDto
        {
            EpochUtcSeconds = (uint)noon.ToUnixTimeSeconds(),
            TimezoneOffsetMinutes = 420,
            IsSynced = true
        });

        simulator.Control.ExecuteScanPass(deltaMs: 10);
        Assert.Equal(0, simulator.Control.GetTagValue(do1TagIndex));

        // Test 2: Lúc 23:30 đêm (nằm trong khung giờ, trước nửa đêm) -> DO1 bật
        var night = new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.FromHours(7));
        await rtcClient.WriteRtcClockAsync(new RtcClockDto
        {
            EpochUtcSeconds = (uint)night.ToUnixTimeSeconds(),
            TimezoneOffsetMinutes = 420,
            IsSynced = true
        });

        simulator.Control.ExecuteScanPass(deltaMs: 10);
        Assert.Equal(1, simulator.Control.GetTagValue(do1TagIndex));

        // Đặt lại DO1 = 0 để test sau nửa đêm
        simulator.Control.SetTagValue(do1TagIndex, 0);
        Assert.Equal(0, simulator.Control.GetTagValue(do1TagIndex));

        // Test 3: Lúc 02:00 sáng (nằm trong khung giờ, sau nửa đêm) -> DO1 vẫn kích hoạt bật
        var earlyMorning = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.FromHours(7));
        await rtcClient.WriteRtcClockAsync(new RtcClockDto
        {
            EpochUtcSeconds = (uint)earlyMorning.ToUnixTimeSeconds(),
            TimezoneOffsetMinutes = 420,
            IsSynced = true
        });

        simulator.Control.ExecuteScanPass(deltaMs: 10);
        Assert.Equal(1, simulator.Control.GetTagValue(do1TagIndex));
    }

    [Fact]
    public async Task RtcClockClient_SyncSmart_PreservesHardwareAndBatteryFlags()
    {
        var simulator = new McuReferenceSimulator(wireProfile: 1);
        simulator.Control.SetHardwareRtcState(hasHardwareRtc: true, batteryLow: true);

        var client = new FakeModbusClient(simulator, isConnected: true);
        var rtcClient = new RtcClockClient(client);

        // Gọi SyncSmart: Đồng bộ giờ Host PC xuống nhưng phải BẢO TOÀN cờ phần cứng
        var synced = await rtcClient.SyncSmartAsync();

        Assert.True(synced.IsSynced);
        Assert.True(synced.HasHardwareRtc, "Hardware RTC flag must be preserved");
        Assert.True(synced.IsBatteryLow, "Battery low warning flag must be preserved");

        // Đọc lại từ simulator để kiểm tra chắc chắn
        var readBack = await rtcClient.ReadRtcClockAsync();
        Assert.True(readBack.IsSynced);
        Assert.True(readBack.HasHardwareRtc, "ReadBack: Hardware RTC flag must be preserved");
        Assert.True(readBack.IsBatteryLow, "ReadBack: Battery low warning flag must be preserved");
    }

    [Fact]
    public async Task RtcClockClient_SyncToNow_PreservesHardwareFlags()
    {
        var simulator = new McuReferenceSimulator(wireProfile: 1);
        simulator.Control.SetHardwareRtcState(hasHardwareRtc: true, batteryLow: false);

        var client = new FakeModbusClient(simulator, isConnected: true);
        var rtcClient = new RtcClockClient(client);

        // Gọi SyncToNow: Read-Before-Write bảo toàn cờ HasHardwareRtc
        var synced = await rtcClient.SyncToNowAsync();

        Assert.True(synced.IsSynced);
        Assert.True(synced.HasHardwareRtc, "SyncToNow must preserve HasHardwareRtc");
        Assert.False(synced.IsBatteryLow, "SyncToNow must preserve IsBatteryLow = false");

        var readBack = await rtcClient.ReadRtcClockAsync();
        Assert.True(readBack.HasHardwareRtc);
    }
}
