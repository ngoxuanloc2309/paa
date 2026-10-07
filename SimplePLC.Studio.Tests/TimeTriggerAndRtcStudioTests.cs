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
        Assert.Equal("⏰ 07:30", node.CompactBadgeText);
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
        Assert.Equal("⏰ 18:00 → 06:30", node.CompactBadgeText);
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
}
