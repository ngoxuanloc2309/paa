using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Tests;

public class RuntimeEngineTests
{
    [Fact]
    public void RisingEdge_FiresActionOnce()
    {
        var tags = new TagCatalogViewModel();
        var input = tags.AllTags.First(t => t.Name == "DI1");
        var output = tags.AllTags.First(t => t.Name == "DO1");
        var rule = new RuleItemModel { Index = 0, Id = "R1", TriggerType = TriggerType.ON_RISE, TriggerTag = input, ActionType = ActionType.SET_TAG, ActionTag = output, ActionParam = 1 };
        var engine = new RuntimeEngine(tags.AllTags.ToList());

        input.Value = 1;
        var snapshot = engine.Scan(new[] { rule }, 10);

        Assert.Equal(1, output.Value);
        Assert.Single(snapshot.Events);
        engine.Scan(new[] { rule }, 20);
        Assert.Single(snapshot.Events);
    }

    [Fact]
    public void Dwell_RequiresContinuousCondition()
    {
        var tags = new TagCatalogViewModel();
        var input = tags.AllTags.First(t => t.Name == "DI0");
        input.Value = 1;
        var output = tags.AllTags.First(t => t.Name == "DO0");
        var rule = new RuleItemModel { Index = 0, TriggerType = TriggerType.ON_FALL, TriggerTag = input, ForMs = 100, ActionType = ActionType.SET_TAG, ActionTag = output, ActionParam = 1 };
        var engine = new RuntimeEngine(tags.AllTags.ToList());

        input.Value = 0;
        Assert.Empty(engine.Scan(new[] { rule }, 10).Events);
        Assert.Empty(engine.Scan(new[] { rule }, 90).Events);
        Assert.Single(engine.Scan(new[] { rule }, 110).Events);
        input.Value = 1;
        engine.Scan(new[] { rule }, 120);
        input.Value = 0;
        Assert.Empty(engine.Scan(new[] { rule }, 130).Events);
    }

    [Fact]
    public void GuardNegate_BlocksWhenGuardIsOn()
    {
        var tags = new TagCatalogViewModel();
        var input = tags.AllTags.First(t => t.Name == "DI1");
        var guard = tags.AllTags.First(t => t.Name == "DI0");
        var output = tags.AllTags.First(t => t.Name == "DO1");
        var rule = new RuleItemModel { Index = 0, TriggerType = TriggerType.ON_RISE, TriggerTag = input, GuardTag = guard, GuardNegated = true, ActionType = ActionType.SET_TAG, ActionTag = output, ActionParam = 1 };
        var engine = new RuntimeEngine(tags.AllTags.ToList());

        guard.Value = 1;
        input.Value = 1;
        Assert.Empty(engine.Scan(new[] { rule }, 10).Events);
    }

    [Fact]
    public void SameScan_RuleChainingViaVFLAG_ExecutesInOrder()
    {
        // Rule 1: DI0 ON_RISE -> SET VFLAG0 = 1
        // Rule 2: DI0 ON_RISE, Guard VFLAG0 == 1 -> SET DO0 = 1
        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Name == "DI0");
        var vflag0 = tags.AllTags.First(t => t.Name == "VFLAG0");
        var do0 = tags.AllTags.First(t => t.Name == "DO0");

        var rule1 = new RuleItemModel
        {
            Index = 0,
            Id = "R1",
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = di0,
            ActionType = ActionType.SET_TAG,
            ActionTag = vflag0,
            ActionParam = 1
        };

        var rule2 = new RuleItemModel
        {
            Index = 1,
            Id = "R2",
            TriggerType = TriggerType.ON_RISE,
            TriggerTag = di0,
            GuardTag = vflag0,
            GuardNegated = false,
            ActionType = ActionType.SET_TAG,
            ActionTag = do0,
            ActionParam = 1
        };

        di0.Value = 0;
        vflag0.Value = 0;
        do0.Value = 0;

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // Initial scan with DI0 = 0
        engine.Scan(new[] { rule1, rule2 }, 10);
        Assert.Equal(0, vflag0.Value);
        Assert.Equal(0, do0.Value);

        // Same scan with DI0 transitioning to 1
        di0.Value = 1;
        var snapshot = engine.Scan(new[] { rule1, rule2 }, 20);

        // Rule 1 sets VFLAG0 = 1, and in the SAME scan cycle, Rule 2 reads VFLAG0 = 1 and sets DO0 = 1!
        Assert.Equal(1, vflag0.Value);
        Assert.Equal(1, do0.Value);
        Assert.Equal(2, snapshot.Events.Count);
    }

    [Fact]
    public void Scan_WithCompiledProgramDomainRules_ExecutesCorrectly()
    {
        var product = SimplePLC.Domain.Models.ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var trigTag = product.FindTagByName("DI0")!;
        var actTag = product.FindTagByName("DO0")!;

        var domainRule = new SimplePLC.Domain.Models.Rule(
            ruleIndex: 0,
            name: "R1",
            trigger: new SimplePLC.Domain.Models.TriggerModel(trigTag, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            action: new SimplePLC.Domain.Models.ActionModel(actTag, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            guard: SimplePLC.Domain.Models.GuardModel.Empty,
            enabled: true);

        var program = new SimplePLC.Application.Logic.Compilation.CompiledProgram(
            "P1",
            new[] { domainRule },
            DateTimeOffset.UtcNow);

        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Name == "DI0");
        var do0 = tags.AllTags.First(t => t.Name == "DO0");
        var engine = new RuntimeEngine(tags.AllTags.ToList());

        di0.Value = 0;
        engine.Scan(program, 10);
        Assert.Equal(0, do0.Value);

        di0.Value = 1;
        var snap = engine.Scan(program, 20);
        Assert.Equal(1, do0.Value);
        Assert.Single(snap.Events);
    }
}
