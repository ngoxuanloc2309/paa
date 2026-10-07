using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class RuleTableProjectionTests
{
    [Fact]
    public void ProjectProgram_ProjectsDomainRulesIntoReadOnlyRuleItemsWithHexBreakdown()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);

        // Build a compiled program with 2 domain rules
        var diTag = new TagDefinition(0, "DI0", TagKind.DiscreteInput, TagDataType.Boolean, isReadOnly: true);
        var doTag = new TagDefinition(1, "DO0", TagKind.DiscreteOutput, TagDataType.Boolean, isReadOnly: false);

        var rule1 = new SimplePLC.Domain.Models.Rule(
            0, "R1",
            new TriggerModel(diTag, TriggerKind.OnRise) { ForMs = 200 },
            new ActionModel(doTag, ActionKind.SetTag, 1),
            GuardModel.Empty,
            true);

        var rule2 = new SimplePLC.Domain.Models.Rule(
            1, "R2",
            new TriggerModel(diTag, TriggerKind.OnFall),
            new ActionModel(doTag, ActionKind.SetTag, 0),
            GuardModel.Empty,
            true);

        var program = new CompiledProgram("TEST_PROG", new[] { rule1, rule2 }, DateTimeOffset.UtcNow);

        // Act: Project
        ruleTableVM.ProjectProgram(program);

        // Assert
        Assert.Equal(2, ruleTableVM.ActiveRuleCount);
        Assert.Equal(2, ruleTableVM.Rules.Count);
        Assert.Equal("R1", ruleTableVM.Rules[0].Id);
        Assert.Equal(200u, ruleTableVM.Rules[0].ForMs);
        Assert.False(string.IsNullOrWhiteSpace(ruleTableVM.Rules[0].RawHex));
        Assert.False(string.IsNullOrWhiteSpace(ruleTableVM.Rules[0].RawHexBreakdown));

        Assert.Equal("R2", ruleTableVM.Rules[1].Id);
        Assert.False(string.IsNullOrWhiteSpace(ruleTableVM.Rules[1].RawHex));
    }

    [Fact]
    public void ProjectProgram_NullOrEmpty_ClearsRules()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        ruleTableVM.LoadDefaultRules();
        Assert.NotEmpty(ruleTableVM.Rules);

        ruleTableVM.ProjectProgram(null);

        Assert.Empty(ruleTableVM.Rules);
        Assert.Equal(0, ruleTableVM.ActiveRuleCount);
    }
}
