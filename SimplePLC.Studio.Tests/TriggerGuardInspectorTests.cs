using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class TriggerGuardInspectorTests
{
    [Fact]
    public void TagCatalog_DigitalTags_ContainsOnlyDigitalAndNoneTags()
    {
        var tagCatalog = new TagCatalogViewModel();

        Assert.NotEmpty(tagCatalog.DigitalTags);

        // All items in DigitalTags must either be NONE or IsDigital == true
        foreach (var tag in tagCatalog.DigitalTags)
        {
            Assert.True(tag.Kind == TagKind.None || tag.IsDigital, 
                $"Tag '{tag.Name}' ({tag.Kind}) in DigitalTags is not a digital tag!");
        }

        // Must contain DI and VFLAG
        Assert.Contains(tagCatalog.DigitalTags, t => t.Name == "DI0");
        Assert.Contains(tagCatalog.DigitalTags, t => t.Name == "VFLAG0");
        Assert.Contains(tagCatalog.DigitalTags, t => t.Name == "NONE");

        // Must NOT contain AI, VREG, VREG_RETAIN, COUNTER
        Assert.DoesNotContain(tagCatalog.DigitalTags, t => t.Name.StartsWith("AI"));
        Assert.DoesNotContain(tagCatalog.DigitalTags, t => t.Name.StartsWith("VREG"));
        Assert.DoesNotContain(tagCatalog.DigitalTags, t => t.Name.StartsWith("COUNTER"));
    }

    [Fact]
    public void GuardNodeViewModel_IsInvalidTagType_IdentifiesNonBooleanTags()
    {
        var tagCatalog = new TagCatalogViewModel();
        var guardNode = new GuardNodeViewModel();

        // 1. Initial / null -> valid (not invalid)
        Assert.False(guardNode.IsInvalidTagType);

        // 2. NONE tag -> valid
        var tagNone = tagCatalog.AllTags.First(t => t.Kind == TagKind.None);
        guardNode.GuardTag = tagNone;
        Assert.False(guardNode.IsInvalidTagType);

        // 3. DI tag -> valid
        var tagDI0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        guardNode.GuardTag = tagDI0;
        Assert.False(guardNode.IsInvalidTagType);

        // 4. VFLAG tag -> valid
        var tagVFlag0 = tagCatalog.AllTags.First(t => t.Name == "VFLAG0");
        guardNode.GuardTag = tagVFlag0;
        Assert.False(guardNode.IsInvalidTagType);

        // 5. VREG_RETAIN tag -> INVALID!
        var tagRetain = tagCatalog.AllTags.First(t => t.Name.StartsWith("VREG_RETAIN"));
        guardNode.GuardTag = tagRetain;
        Assert.True(guardNode.IsInvalidTagType, "VREG_RETAIN should be flagged as invalid for Guard!");

        // 6. AI tag -> INVALID!
        var tagAI0 = tagCatalog.AllTags.First(t => t.Name == "AI0");
        guardNode.GuardTag = tagAI0;
        Assert.True(guardNode.IsInvalidTagType, "AI should be flagged as invalid for Guard!");
    }

    [Fact]
    public void TriggerNodeViewModel_ComparisonProperties_BindAndNotifyCorrectly()
    {
        var triggerNode = new TriggerNodeViewModel();

        // Default: CompareOp.NONE
        Assert.Equal(CompareOp.NONE, triggerNode.CompareOp);
        Assert.False(triggerNode.HasComparison);
        Assert.False(triggerNode.IsRangeComparison);

        // Set to GT with ThresholdLo = 50
        triggerNode.CompareOp = CompareOp.GT;
        triggerNode.ThresholdLo = 50;
        Assert.True(triggerNode.HasComparison);
        Assert.False(triggerNode.IsRangeComparison);
        Assert.Equal("> 50", triggerNode.ConditionExpression);

        // Set to BETWEEN with ThresholdLo = 10, ThresholdHi = 90
        triggerNode.CompareOp = CompareOp.BETWEEN;
        triggerNode.ThresholdLo = 10;
        triggerNode.ThresholdHi = 90;
        Assert.True(triggerNode.HasComparison);
        Assert.True(triggerNode.IsRangeComparison);
        Assert.Equal("in [10..90]", triggerNode.ConditionExpression);
    }

    [Fact]
    public void RuleItemModel_SeparatesTriggerComparison_FromGuardBooleanState()
    {
        var tagCatalog = new TagCatalogViewModel();
        var tagAI0 = tagCatalog.AllTags.First(t => t.Name == "AI0");
        var tagVFlag0 = tagCatalog.AllTags.First(t => t.Name == "VFLAG0");
        var tagDO0 = tagCatalog.AllTags.First(t => t.Name == "DO0");

        var rule = new RuleItemModel
        {
            Id = "R1",
            TriggerTag = tagAI0,
            TriggerType = TriggerType.ON_CHANGE,
            CompareOp = CompareOp.GT,
            ThresholdLo = 85,
            GuardTag = tagVFlag0,
            GuardNegated = false,
            ActionTag = tagDO0,
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        };

        rule.UpdateNarrative();

        // Trigger condition text should show comparison (> 85)
        Assert.Contains("85", rule.TriggerEventText);

        // Guard condition text should show boolean state, NOT comparison
        Assert.DoesNotContain("85", rule.GuardConditionText);
        Assert.Contains("VFLAG0", rule.GuardConditionText);

        // Narrative should have trigger comparison and guard IF state
        Assert.Contains("AI0", rule.Narrative);
        Assert.Contains("85", rule.Narrative);
        Assert.Contains("VFLAG0", rule.Narrative);
        Assert.Contains("DO0", rule.Narrative);
    }
}
