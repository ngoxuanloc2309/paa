using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class TimerMacroFeasibilityTests
{
    private readonly TagCatalogViewModel _catalog;
    private readonly TagModel _tagIn;
    private readonly TagModel _tagQ;

    public TimerMacroFeasibilityTests()
    {
        _catalog = new TagCatalogViewModel();
        _tagIn = _catalog.AllTags.First(t => t.Name == "DI0");
        _tagQ = _catalog.AllTags.First(t => t.Name == "DO0");
    }

    #region 1. TON (Timer On-Delay) Macro Feasibility Tests

    [Fact]
    public void Ton_HoldsForPresetTime_TurnsOutputOn()
    {
        // Arrange
        // R0: IF IN (ON_RISE) for 3000ms -> SET Q = 1
        // R1: IF IN (ON_FALL) -> SET Q = 0
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TON_RUN",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 3000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TON_RESET",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // Scan 1: t=0, IN=0 -> Q=0
        _tagIn.Value = 0;
        var snap1 = engine.Scan(rules, 0);
        Assert.Equal(0, _tagQ.Value);

        // Scan 2: t=1000, IN rises to 1 -> TON arms dwell (WaitingDwell)
        _tagIn.Value = 1;
        var snap2 = engine.Scan(rules, 1000);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap2.Evaluations[0].Status);

        // Scan 3: t=2500, IN still 1 (elapsed 1500ms < 3000ms) -> Still WaitingDwell
        var snap3 = engine.Scan(rules, 2500);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap3.Evaluations[0].Status);

        // Scan 4: t=4000, IN still 1 (elapsed 3000ms == 3000ms) -> Fires! Q = 1
        var snap4 = engine.Scan(rules, 4000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snap4.Evaluations[0].Status);

        // Scan 5: t=5000, IN still 1 -> R0 does not fire again (no new rising edge), Q stays 1
        var snap5 = engine.Scan(rules, 5000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Skip, snap5.Evaluations[0].Status);
    }

    [Fact]
    public void Ton_DropsBeforePresetTime_CancelsTimer_OutputRemainsOff()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TON_RUN",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 3000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TON_RESET",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // Scan 1: t=1000, IN rises to 1 -> dwell starts
        _tagIn.Value = 1;
        var snap1 = engine.Scan(rules, 1000);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap1.Evaluations[0].Status);

        // Scan 2: t=2500, IN drops back to 0 at 1500ms (< 3000ms)
        _tagIn.Value = 0;
        var snap2 = engine.Scan(rules, 2500);
        // Dwell cancelled immediately! R1 resets Q to 0
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Skip, snap2.Evaluations[0].Status);

        // Scan 3: t=4500, IN still 0 -> Q remains 0, dwell was not retained
        var snap3 = engine.Scan(rules, 4500);
        Assert.Equal(0, _tagQ.Value);
    }

    [Fact]
    public void Ton_ReassertsInputAfterCancel_StartsTimingFromZero()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TON_RUN",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 2000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TON_RESET",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // Start dwell at t=1000
        _tagIn.Value = 1;
        engine.Scan(rules, 1000);

        // Drop at t=2000 (1000ms elapsed < 2000ms required) -> cancelled
        _tagIn.Value = 0;
        engine.Scan(rules, 2000);

        // Reassert at t=3000 -> fresh dwell starts from 0
        _tagIn.Value = 1;
        var snap3 = engine.Scan(rules, 3000);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap3.Evaluations[0].Status);

        // At t=4000 (elapsed 1000ms from t=3000) -> must still be waiting!
        var snap4 = engine.Scan(rules, 4000);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap4.Evaluations[0].Status);

        // At t=5000 (elapsed 2000ms from t=3000) -> Fires!
        var snap5 = engine.Scan(rules, 5000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snap5.Evaluations[0].Status);
    }

    [Fact]
    public void Ton_InputDropsAfterOutputOn_TurnsOutputOffImmediately()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TON_RUN",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 1000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TON_RESET",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // t=1000 -> Rise
        _tagIn.Value = 1;
        engine.Scan(rules, 1000);

        // t=2000 -> Fires, Q = 1
        var snap2 = engine.Scan(rules, 2000);
        Assert.Equal(1, _tagQ.Value);

        // t=3000 -> IN drops to 0 -> Q turns off immediately
        _tagIn.Value = 0;
        var snap3 = engine.Scan(rules, 3000);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snap3.Evaluations[1].Status);
    }

    #endregion

    #region 2. TOF (Timer Off-Delay) Macro Feasibility Tests

    [Fact]
    public void Tof_InputRises_TurnsOutputOnImmediately()
    {
        // R0: IF IN (ON_RISE) -> SET Q = 1 immediately
        // R1: IF IN (ON_FALL) for 2000ms (Guard Q==1) -> SET Q = 0
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TOF_ON",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TOF_OFF",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 2000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0,
                GuardTag = _tagQ,
                GuardNegated = false
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // Startup: IN=0, Q=0
        _tagIn.Value = 0;
        var snap1 = engine.Scan(rules, 0);
        Assert.Equal(0, _tagQ.Value);

        // IN rises to 1 -> Q turns on immediately
        _tagIn.Value = 1;
        var snap2 = engine.Scan(rules, 1000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snap2.Evaluations[0].Status);
    }

    [Fact]
    public void Tof_InputDrops_HoldsOutputForPresetTime_ThenTurnsOff()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TOF_ON",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TOF_OFF",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 2000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0,
                GuardTag = _tagQ,
                GuardNegated = false
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // t=1000, IN=1 -> Q=1
        _tagIn.Value = 1;
        engine.Scan(rules, 1000);

        // t=2000, IN falls to 0 -> R_TOF_OFF arms dwell for 2000ms. Q MUST STAY 1!
        _tagIn.Value = 0;
        var snap2 = engine.Scan(rules, 2000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap2.Evaluations[1].Status);

        // t=3000, IN still 0 (1000ms < 2000ms) -> Q MUST STILL STAY 1!
        var snap3 = engine.Scan(rules, 3000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap3.Evaluations[1].Status);

        // t=4000, IN still 0 (2000ms >= 2000ms) -> R_TOF_OFF fires! Q turns 0
        var snap4 = engine.Scan(rules, 4000);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snap4.Evaluations[1].Status);
    }

    [Fact]
    public void Tof_InputReassertsDuringOffDelay_CancelsOffDelay_OutputRemainsOn()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TOF_ON",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TOF_OFF",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 2000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0,
                GuardTag = _tagQ,
                GuardNegated = false
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // Turn Q on
        _tagIn.Value = 1;
        engine.Scan(rules, 1000);

        // Drop IN to 0 at t=2000 -> dwell started
        _tagIn.Value = 0;
        var snap2 = engine.Scan(rules, 2000);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap2.Evaluations[1].Status);

        // At t=3000 (1000ms < 2000ms), IN reasserts to 1! Off-delay cancelled immediately!
        _tagIn.Value = 1;
        var snap3 = engine.Scan(rules, 3000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Skip, snap3.Evaluations[1].Status); // Dwell cancelled!

        // At t=5000, IN still 1 -> Q stays 1
        var snap4 = engine.Scan(rules, 5000);
        Assert.Equal(1, _tagQ.Value);
    }

    [Fact]
    public void Tof_StartupWithInputOff_OutputRemainsDeterministicOff()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TOF_ON",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1
            },
            new()
            {
                Index = 1,
                Id = "R_TOF_OFF",
                TriggerType = TriggerType.ON_FALL,
                TriggerTag = _tagIn,
                ForMs = 2000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0,
                GuardTag = _tagQ,
                GuardNegated = false
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // Startup at t=0 with IN=0
        _tagIn.Value = 0;
        var snap1 = engine.Scan(rules, 0);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Skip, snap1.Evaluations[0].Status);
        Assert.Equal(RuleEvaluationStatus.Skip, snap1.Evaluations[1].Status);

        // Later scan at t=3000 with IN=0 -> still OFF
        var snap2 = engine.Scan(rules, 3000);
        Assert.Equal(0, _tagQ.Value);
    }

    #endregion

    #region 3. TP (Pulse Timer) Macro Feasibility Tests

    [Fact]
    public void Tp_RisingEdge_TurnsOutputOnImmediately_AndTurnsOffAfterExactPt()
    {
        // R0: IF IN (ON_RISE) Guard(Q==0) -> SET Q = 1
        // R1: IF Q (ON_RISE) for 1500ms -> SET Q = 0
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TP_START",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1,
                GuardTag = _tagQ,
                GuardNegated = true // Guard: Q == 0
            },
            new()
            {
                Index = 1,
                Id = "R_TP_EXPIRE",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagQ,
                ForMs = 1500,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // t=1000: IN rises -> R0 fires (Q becomes 1), R1 sees Q rise and starts dwell!
        _tagIn.Value = 1;
        var snap1 = engine.Scan(rules, 1000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snap1.Evaluations[0].Status);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap1.Evaluations[1].Status);

        // t=1800 (800ms < 1500ms): Q must still be 1!
        var snap2 = engine.Scan(rules, 1800);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snap2.Evaluations[1].Status);

        // t=2500 (1500ms == 1500ms): R1 fires -> Q turns OFF!
        var snap3 = engine.Scan(rules, 2500);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snap3.Evaluations[1].Status);
    }

    [Fact]
    public void Tp_InputHeldLongerThanPt_OutputDoesNotRetrigger()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TP_START",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1,
                GuardTag = _tagQ,
                GuardNegated = true
            },
            new()
            {
                Index = 1,
                Id = "R_TP_EXPIRE",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagQ,
                ForMs = 1000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // t=1000: IN rises -> Q turns 1
        _tagIn.Value = 1;
        engine.Scan(rules, 1000);

        // t=2000: Pulse expires -> Q turns 0
        var snap2 = engine.Scan(rules, 2000);
        Assert.Equal(0, _tagQ.Value);

        // t=3000: IN is STILL held at 1. Since IN has no new rising edge, R0 MUST NOT fire!
        var snap3 = engine.Scan(rules, 3000);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Skip, snap3.Evaluations[0].Status);

        // t=4000: Still held at 1 -> still 0
        var snap4 = engine.Scan(rules, 4000);
        Assert.Equal(0, _tagQ.Value);
    }

    [Fact]
    public void Tp_InputDropsAndRisesAfterPulse_FiresNewPulse()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TP_START",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1,
                GuardTag = _tagQ,
                GuardNegated = true
            },
            new()
            {
                Index = 1,
                Id = "R_TP_EXPIRE",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagQ,
                ForMs = 1000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // First pulse: t=1000 -> t=2000
        _tagIn.Value = 1;
        engine.Scan(rules, 1000);
        engine.Scan(rules, 2000);

        // IN drops at t=3000
        _tagIn.Value = 0;
        engine.Scan(rules, 3000);

        // IN rises again at t=4000 -> Fresh rising edge fires a new pulse!
        _tagIn.Value = 1;
        var snapNew = engine.Scan(rules, 4000);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snapNew.Evaluations[0].Status);
    }

    [Fact]
    public void Tp_RetriggerDuringActivePulse_IsIgnoredByGuard()
    {
        var rules = new List<RuleItemModel>
        {
            new()
            {
                Index = 0,
                Id = "R_TP_START",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagIn,
                ForMs = 0,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 1,
                GuardTag = _tagQ,
                GuardNegated = true // Guard: Q == 0
            },
            new()
            {
                Index = 1,
                Id = "R_TP_EXPIRE",
                TriggerType = TriggerType.ON_RISE,
                TriggerTag = _tagQ,
                ForMs = 2000,
                ActionType = ActionType.SET_TAG,
                ActionTag = _tagQ,
                ActionParam = 0
            }
        };

        var engine = new RuntimeEngine(_catalog.AllTags.ToList());

        // t=1000: IN rises -> pulse starts, Q=1, expires at t=3000
        _tagIn.Value = 1;
        engine.Scan(rules, 1000);

        // t=1500: IN drops quickly
        _tagIn.Value = 0;
        engine.Scan(rules, 1500);

        // t=1800: IN rises AGAIN while pulse is still active (Q is still 1)!
        // R0 MUST BE BLOCKED BY GUARD (Q == 0)!
        _tagIn.Value = 1;
        var snapRetrig = engine.Scan(rules, 1800);
        Assert.Equal(1, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.BlockedByGuard, snapRetrig.Evaluations[0].Status);
        // Pulse timer must NOT be restarted, still timing from t=1000!
        Assert.Equal(RuleEvaluationStatus.WaitingDwell, snapRetrig.Evaluations[1].Status);

        // t=3000: Pulse expires on schedule at original t=1000 + 2000ms = 3000ms!
        var snapExpire = engine.Scan(rules, 3000);
        Assert.Equal(0, _tagQ.Value);
        Assert.Equal(RuleEvaluationStatus.Pass, snapExpire.Evaluations[1].Status);
    }

    #endregion
}
