using System.Windows;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class TimeTriggerAndRtcStudioTests
{
    [Fact]
    public void TriggerNodeViewModel_TimePointMode_SetsThresholdsAndFormatting()
    {
        var node = new TriggerNodeViewModel();
        node.TriggerType = TriggerType.TIME_WINDOW;
        node.IsTimePointInTime = true; // Alarm / Point in time
        node.TimeStartHour = 7;
        node.TimeStartMinute = 30;

        Assert.True(node.IsTimeTrigger);
        Assert.Equal(CompareOp.EQ, node.CompareOp);
        Assert.Equal(730, node.ThresholdLo);
        Assert.Equal(730, node.ThresholdHi);
        Assert.Equal("07:30", node.TimeStartFormatted);
        Assert.True(node.CompactBadgeText == "07:30" || node.CompactBadgeText == "⏰ 07:30");
    }

    [Fact]
    public void TriggerNodeViewModel_TimeWindowMode_SetsRangeAndFormatting()
    {
        var node = new TriggerNodeViewModel();
        node.TriggerType = TriggerType.TIME_WINDOW;
        node.IsTimeWindowRange = true; // Range
        node.TimeStartHour = 18;
        node.TimeStartMinute = 0;
        node.TimeEndHour = 6;
        node.TimeEndMinute = 30;

        Assert.True(node.IsTimeTrigger);
        Assert.Equal(CompareOp.BETWEEN, node.CompareOp);
        Assert.Equal(1800, node.ThresholdLo);
        Assert.Equal(630, node.ThresholdHi);
        Assert.Equal("18:00", node.TimeStartFormatted);
        Assert.Equal("06:30", node.TimeEndFormatted);
        Assert.True(node.CompactBadgeText == "18:00 → 06:30" || node.CompactBadgeText == "⏰ 18:00 → 06:30");
    }

    [Fact]
    public void RuntimeEngine_TimeWindow_PointInTime_FiresOnlyAtTargetMinute()
    {
        var tags = new TagCatalogViewModel();
        var output = tags.AllTags.First(t => t.Name == "DO0");
        output.Value = 0;

        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R1",
            TriggerType = TriggerType.TIME_WINDOW,
            CompareOp = CompareOp.EQ,
            ThresholdLo = 700, // 07:00
            ThresholdHi = 700,
            ActionType = ActionType.SET_TAG,
            ActionTag = output,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // 1. Lúc 06:59 -> Không kích hoạt
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 6, 59, 0);
        var snap1 = engine.Scan(new[] { rule }, 100);
        Assert.Equal(RuleEvaluationStatus.Skip, snap1.Evaluations[0].Status);
        Assert.Equal(0, output.Value);

        // 2. Đúng 07:00 -> Kích hoạt PASS
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 7, 0, 15);
        var snap2 = engine.Scan(new[] { rule }, 200);
        Assert.Equal(RuleEvaluationStatus.Pass, snap2.Evaluations[0].Status);
        Assert.Equal(1, output.Value);

        // 3. Lúc 07:01 -> Không kích hoạt
        output.Value = 0;
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 7, 1, 0);
        var snap3 = engine.Scan(new[] { rule }, 300);
        Assert.Equal(RuleEvaluationStatus.Skip, snap3.Evaluations[0].Status);
        Assert.Equal(0, output.Value);
    }

    [Fact]
    public void RuntimeEngine_TimeWindow_NormalRange_ActiveInsideWindow()
    {
        var tags = new TagCatalogViewModel();
        var output = tags.AllTags.First(t => t.Name == "DO0");
        output.Value = 0;

        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R1",
            TriggerType = TriggerType.TIME_WINDOW,
            CompareOp = CompareOp.BETWEEN,
            ThresholdLo = 700,  // 07:00
            ThresholdHi = 1700, // 17:00
            ActionType = ActionType.SET_TAG,
            ActionTag = output,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // 06:59 -> Outside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 6, 59, 0);
        Assert.Equal(RuleEvaluationStatus.Skip, engine.Scan(new[] { rule }, 10).Evaluations[0].Status);

        // 07:00 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 7, 0, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 20).Evaluations[0].Status);

        // 12:30 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 12, 30, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 30).Evaluations[0].Status);

        // 17:00 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 17, 0, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 40).Evaluations[0].Status);

        // 17:01 -> Outside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 17, 1, 0);
        Assert.Equal(RuleEvaluationStatus.Skip, engine.Scan(new[] { rule }, 50).Evaluations[0].Status);
    }

    [Fact]
    public void RuntimeEngine_TimeWindow_CrossMidnight_ActiveDuringNight()
    {
        var tags = new TagCatalogViewModel();
        var output = tags.AllTags.First(t => t.Name == "DO0");
        output.Value = 0;

        // 18:00 to 06:00
        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R1",
            TriggerType = TriggerType.TIME_WINDOW,
            CompareOp = CompareOp.BETWEEN,
            ThresholdLo = 1800, // 18:00
            ThresholdHi = 600,  // 06:00
            ActionType = ActionType.SET_TAG,
            ActionTag = output,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // 17:59 -> Outside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 17, 59, 0);
        Assert.Equal(RuleEvaluationStatus.Skip, engine.Scan(new[] { rule }, 10).Evaluations[0].Status);

        // 18:00 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 18, 0, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 20).Evaluations[0].Status);

        // 23:45 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 23, 45, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 30).Evaluations[0].Status);

        // 00:00 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 0, 0, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 40).Evaluations[0].Status);

        // 05:59 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 5, 59, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 50).Evaluations[0].Status);

        // 06:00 -> Inside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 6, 0, 0);
        Assert.Equal(RuleEvaluationStatus.Pass, engine.Scan(new[] { rule }, 60).Evaluations[0].Status);

        // 06:01 -> Outside
        engine.TimeProvider = () => new DateTime(2026, 9, 30, 6, 1, 0);
        Assert.Equal(RuleEvaluationStatus.Skip, engine.Scan(new[] { rule }, 70).Evaluations[0].Status);
    }

    [Fact]
    public void RtcSyncViewModel_Initialization_PopulatesTimezonesAndLocalOptionFirst()
    {
        var fakeRtcClient = new FakeRtcClockClient();
        var vm = new RtcSyncViewModel(fakeRtcClient);

        Assert.NotEmpty(vm.AvailableTimezones);
        Assert.True(vm.AvailableTimezones.Count >= 10);

        // Mục đầu tiên phải là Giờ máy tính hiện tại (Local PC)
        Assert.NotNull(vm.SelectedTimezone);
        Assert.True(vm.SelectedTimezone.IsLocalPc);
        Assert.NotEmpty(vm.TargetPreviewTimeText);
        Assert.NotEmpty(vm.TargetModbusSummaryText);
    }

    [Fact]
    public void RtcSyncViewModel_TimezoneChange_UpdatesPreviewCorrectly()
    {
        var fakeRtcClient = new FakeRtcClockClient();
        var vm = new RtcSyncViewModel(fakeRtcClient);

        // Chuyển sang Eastern Standard Time (New York, UTC-5)
        var nyOpt = vm.AvailableTimezones.FirstOrDefault(t => t.TimeZoneInfo.Id == "Eastern Standard Time");
        Assert.NotNull(nyOpt);

        vm.SelectedTimezone = nyOpt;

        Assert.Contains("UTC-", vm.TargetPreviewTimeText);
        Assert.True(vm.TargetModbusSummaryText.Contains("-300") || vm.TargetModbusSummaryText.Contains("-240"));
    }

    [Fact]
    public async Task RtcSyncViewModel_SyncCommand_ExecutesAndNotifiesSuccess()
    {
        var fakeRtcClient = new FakeRtcClockClient();
        var vm = new RtcSyncViewModel(fakeRtcClient);

        bool? closedWithSuccess = null;
        vm.RequestClose = success => closedWithSuccess = success;

        var nyOpt = vm.AvailableTimezones.First(t => t.TimeZoneInfo.Id == "Eastern Standard Time");
        vm.SelectedTimezone = nyOpt;

        await vm.SyncAsync();

        Assert.True(closedWithSuccess);
        Assert.NotNull(vm.SyncedResult);
        Assert.True(vm.SyncedResult.IsSynced);
        Assert.True(vm.SyncedResult.TimezoneOffsetMinutes == -300 || vm.SyncedResult.TimezoneOffsetMinutes == -240);
    }

    [Fact]
    public async Task RtcSyncViewModel_ManualTimeOverride_CalculatesSpecificTime()
    {
        var fakeRtcClient = new FakeRtcClockClient();
        var vm = new RtcSyncViewModel(fakeRtcClient);

        bool? closedWithSuccess = null;
        vm.RequestClose = success => closedWithSuccess = success;

        var nyOpt = vm.AvailableTimezones.First(t => t.TimeZoneInfo.Id == "Eastern Standard Time");
        vm.SelectedTimezone = nyOpt;

        vm.IsManualTimeEnabled = true;
        vm.ManualDate = new DateTime(2026, 10, 15);
        vm.ManualHour = 8;
        vm.ManualMinute = 30;
        vm.ManualSecond = 0;

        await vm.SyncAsync();

        Assert.True(closedWithSuccess);
        Assert.NotNull(vm.SyncedResult);
        Assert.Equal(8, vm.SyncedResult.LocalDateTime.Hour);
        Assert.Equal(30, vm.SyncedResult.LocalDateTime.Minute);
    }

    [Fact]
    public void RtcSyncDialogView_XAML_InstantiatesWithoutResourceExceptions()
    {
        RunOnSta(() =>
        {
            var fakeRtcClient = new FakeRtcClockClient();
            var vm = new RtcSyncViewModel(fakeRtcClient);
            var dialog = new Views.RtcSyncDialogView(vm);
            Assert.NotNull(dialog);
            Assert.Same(vm, dialog.DataContext);
        });
    }

    private static void RunOnSta(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }

    private class FakeRtcClockClient : Application.Abstractions.IRtcClockClient
    {
        public Task<SimplePLC.Protocol.Dto.RtcClockDto> ReadRtcClockAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SimplePLC.Protocol.Dto.RtcClockDto
            {
                EpochUtcSeconds = 1774915200,
                TimezoneOffsetMinutes = 420,
                IsSynced = true,
                HasHardwareRtc = true,
                IsBatteryLow = false
            });
        }

        public Task WriteRtcClockAsync(SimplePLC.Protocol.Dto.RtcClockDto rtc, byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<SimplePLC.Protocol.Dto.RtcClockDto> SyncToNowAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SimplePLC.Protocol.Dto.RtcClockDto { IsSynced = true });
        }

        public Task<SimplePLC.Protocol.Dto.RtcClockDto> SyncSmartAsync(byte slaveId = 1, uint maxDriftSeconds = 2, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SimplePLC.Protocol.Dto.RtcClockDto { IsSynced = true });
        }

        public Task<SimplePLC.Protocol.Dto.RtcClockDto> SyncToCustomTimezoneAsync(
            TimeZoneInfo targetTimezone,
            DateTimeOffset? customLocalDateTime = null,
            byte slaveId = 1,
            CancellationToken cancellationToken = default)
        {
            DateTimeOffset target = customLocalDateTime ?? TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, targetTimezone);
            return Task.FromResult(new SimplePLC.Protocol.Dto.RtcClockDto
            {
                EpochUtcSeconds = (uint)target.ToUnixTimeSeconds(),
                TimezoneOffsetMinutes = (short)targetTimezone.GetUtcOffset(target).TotalMinutes,
                IsSynced = true,
                HasHardwareRtc = true,
                IsBatteryLow = false
            });
        }
    }
}
