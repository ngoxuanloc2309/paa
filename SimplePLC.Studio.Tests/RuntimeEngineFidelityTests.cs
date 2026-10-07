using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class RuntimeEngineFidelityTests
{
    [Fact]
    public void DwellTimer_ResetsImmediately_WhenAnalogConditionDrops()
    {
        // Scenario: AI0 > 80, ForMs = 3000 -> SET DO0 = 1
        var tags = new TagCatalogViewModel();
        var ai0 = tags.AllTags.First(t => t.Name == "AI0");
        var do0 = tags.AllTags.First(t => t.Name == "DO0");

        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R_AI0_HIGH",
            TriggerType = TriggerType.ON_CHANGE,
            TriggerTag = ai0,
            CompareOp = CompareOp.GT,
            ThresholdLo = 80,
            ForMs = 3000,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // Scan 1: AI0 = 85 (Condition True, starts dwell)
        ai0.Value = 85;
        var snap1 = engine.Scan(new[] { rule }, 1000);
        Assert.Single(snap1.Evaluations);
        var eval1 = snap1.Evaluations[0];
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, eval1.Status);
        Assert.Equal(0, eval1.DwellElapsedMs);
        Assert.Equal(3000, eval1.DwellRequiredMs);
        Assert.Equal(0, do0.Value);

        // Scan 2: AI0 = 85 at t=2000 (Condition True, 1000ms elapsed)
        var snap2 = engine.Scan(new[] { rule }, 2000);
        var eval2 = snap2.Evaluations[0];
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, eval2.Status);
        Assert.Equal(1000, eval2.DwellElapsedMs);
        Assert.Equal(0, do0.Value);

        // Scan 3: AI0 drops to 75 at t=2500 (Condition False -> Dwell timer immediately resets!)
        ai0.Value = 75;
        var snap3 = engine.Scan(new[] { rule }, 2500);
        var eval3 = snap3.Evaluations[0];
        Assert.Equal(RuleEvaluationStatus.Skip, eval3.Status);
        Assert.Equal(0, eval3.DwellElapsedMs);
        Assert.Equal(0, do0.Value);

        // Scan 4: AI0 rises back to 85 at t=3500 (Dwell restarted from 0, elapsed is 0, must NOT fire yet)
        ai0.Value = 85;
        var snap4 = engine.Scan(new[] { rule }, 3500);
        var eval4 = snap4.Evaluations[0];
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, eval4.Status);
        Assert.Equal(0, eval4.DwellElapsedMs);
        Assert.Equal(0, do0.Value);
    }

    [Fact]
    public void DwellTimer_Fires_WhenConditionHeldContinuously()
    {
        // Scenario: AI0 > 80 held for 3000ms -> SET DO0 = 1
        var tags = new TagCatalogViewModel();
        var ai0 = tags.AllTags.First(t => t.Name == "AI0");
        var do0 = tags.AllTags.First(t => t.Name == "DO0");

        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R_DWELL_PASS",
            TriggerType = TriggerType.ON_CHANGE,
            TriggerTag = ai0,
            CompareOp = CompareOp.GT,
            ThresholdLo = 80,
            ForMs = 3000,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // Scan 1: t=1000, AI0 = 90 -> WaitingDwell
        ai0.Value = 90;
        var snap1 = engine.Scan(new[] { rule }, 1000);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap1.Evaluations[0].Status);
        Assert.Equal(0, do0.Value);

        // Scan 2: t=4000 (3000ms elapsed >= ForMs) -> PASS! Action executes!
        var snap2 = engine.Scan(new[] { rule }, 4000);
        Assert.Equal(RuleEvaluationStatus.Pass, snap2.Evaluations[0].Status);
        Assert.Equal(1, do0.Value);
        Assert.NotNull(snap2.Evaluations[0].ActionDelta);
        Assert.Equal("DO0: 0 → 1 (SET)", snap2.Evaluations[0].ActionDelta!.FormattedDelta);
    }

    [Fact]
    public void ActionDelta_RecordsBeforeAndAfter_Accurately()
    {
        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Name == "DI0");
        var counter = tags.AllTags.First(t => t.Name == "COUNTER0");

        counter.Value = 15;
        di0.Value = 0;

        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R_INC",
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = di0,
            ActionType = ActionType.INC_COUNTER,
            ActionTag = counter,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        di0.Value = 1;
        var snap = engine.Scan(new[] { rule }, 100);

        Assert.Equal(16, counter.Value);
        Assert.Single(snap.Evaluations);
        var eval = snap.Evaluations[0];
        Assert.Equal(RuleEvaluationStatus.Pass, eval.Status);
        Assert.NotNull(eval.ActionDelta);
        Assert.Equal(15, eval.ActionDelta!.BeforeValue);
        Assert.Equal(16, eval.ActionDelta.AfterValue);
        Assert.Equal("COUNTER0: 15 → 16 (+1)", eval.ActionDelta.FormattedDelta);
    }

    [Fact]
    public void BlockedByGuard_IsReported_WhenTriggerAndDwellMet()
    {
        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Name == "DI0");
        var guardTag = tags.AllTags.First(t => t.Name == "VFLAG0");
        var do0 = tags.AllTags.First(t => t.Name == "DO0");

        // Guard requires VFLAG0 == 1. Currently VFLAG0 = 0.
        guardTag.Value = 0;
        di0.Value = 0;
        do0.Value = 0;

        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R_GUARD_BLOCKED",
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = di0,
            GuardTag = guardTag,
            GuardNegated = false,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        di0.Value = 1;
        var snap = engine.Scan(new[] { rule }, 100);

        var eval = snap.Evaluations[0];
        Assert.Equal(RuleEvaluationStatus.BlockedByGuard, eval.Status);
        Assert.Equal(0, do0.Value);
        Assert.Null(eval.ActionDelta);
        Assert.Contains("Blocked", eval.GuardSummary ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SameScanCausality_IsTrackedInEvaluationRecord()
    {
        // Rule 0: DI0 rise -> SET VFLAG0 = 1
        // Rule 1: VFLAG0 ON_CHANGE -> SET DO0 = 1
        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Name == "DI0");
        var vflag0 = tags.AllTags.First(t => t.Name == "VFLAG0");
        var do0 = tags.AllTags.First(t => t.Name == "DO0");

        di0.Value = 0;
        vflag0.Value = 0;
        do0.Value = 0;

        var rule0 = new RuleItemModel
        {
            Index = 0,
            Id = "R0",
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = di0,
            ActionType = ActionType.SET_TAG,
            ActionTag = vflag0,
            ActionParam = 1
        };

        var rule1 = new RuleItemModel
        {
            Index = 1,
            Id = "R1",
            TriggerType = TriggerType.ON_CHANGE,
            TriggerTag = vflag0,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 1
        };

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // Prime previous value
        engine.Scan(new[] { rule0, rule1 }, 0);

        // Rising edge on DI0
        di0.Value = 1;
        var snap = engine.Scan(new[] { rule0, rule1 }, 100);

        Assert.Equal(2, snap.Evaluations.Count);
        var eval0 = snap.Evaluations[0];
        var eval1 = snap.Evaluations[1];

        Assert.Equal(RuleEvaluationStatus.Pass, eval0.Status);
        Assert.False(eval0.IsChainedInSameScan);

        // Rule 1 consumed VFLAG0 which was written in the same scan by Rule 0!
        Assert.Equal(RuleEvaluationStatus.Pass, eval1.Status);
        Assert.True(eval1.IsChainedInSameScan);
        Assert.Equal(1, do0.Value);
    }

    [Fact]
    public void StepScan_AdvancesClockAndExecutesSingleCycle()
    {
        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Name == "DI0");
        var do0 = tags.AllTags.First(t => t.Name == "DO0");

        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R_STEP",
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = di0,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 1
        };

        var ruleTable = new RuleTableViewModel(tags);
        ruleTable.Rules.Clear();
        ruleTable.Rules.Add(rule);

        var vm = new SimulatorViewModel(tags, ruleTable);
        // Pause background timer
        vm.ToggleEngine();

        di0.Value = 0;
        Assert.Equal(0, vm.ScanNumber);

        // Prime scan
        vm.StepScanCommand.Execute(null);
        Assert.Equal(1, vm.ScanNumber);
        Assert.Equal(0, do0.Value);

        // Trigger rising edge
        di0.Value = 1;
        vm.StepScanCommand.Execute(null);
        Assert.Equal(2, vm.ScanNumber);

        var status = vm.RuleStatusList.FirstOrDefault();
        Assert.NotNull(status);
        Assert.True(status.Status == RuleEvaluationStatus.Pass, $"Expected Pass but got {status.Status}: {status.SummaryLine}");
        Assert.Equal(1, do0.Value);
    }
}
