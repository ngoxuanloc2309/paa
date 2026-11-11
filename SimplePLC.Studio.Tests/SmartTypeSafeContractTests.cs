using SimplePLC.Application.Enums;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Comprehensive contract and integration tests for Smart Type-Safe Input & Data Normalization (Issue #12).
/// Enforces First Principles:
/// - Axiom 1: Hardware Ground Truth (IEC 61131-3 physical data types)
/// - Axiom 2: The Visual Truth (UI 1:1 reflection of memory state)
/// - Axiom 3: Input State Machine (Drafting vs. Committed Auto-Normalize / Auto-Revert)
/// </summary>
public class SmartTypeSafeContractTests
{
    // =========================================================================
    // 1. ComparatorNodeViewModel Tests
    // =========================================================================

    [Fact]
    public void ComparatorNodeViewModel_WhenInputIsInt_RevertsFloatOnCommit()
    {
        var tag = new TagModel { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Value = 50 };
        var node = new ComparatorNodeViewModel(tag, CompareOp.GT, lo: 50);

        Assert.True(node.IsInputInteger);
        Assert.False(node.IsInputBoolean);
        Assert.False(node.IsInputReal);
        Assert.Equal("INT32", node.InputTagDataTypeText);

        // Drafting phase: typing fractional value "25.5" into an integer comparator
        node.ThresholdLoText = "25.5";

        // Underlying model must NOT be corrupted by fractional value during drafting
        Assert.Equal(50, node.ThresholdLo);

        // Committed phase: User triggers LostFocus / Enter
        node.CommitThresholdLoCommand.Execute(null);

        // Auto-Revert: displayed text must revert to committed integer, never lingering as "25.5"
        Assert.Equal("50", node.ThresholdLoText);
        Assert.Equal(50, node.ThresholdLo);
    }

    [Fact]
    public void ComparatorNodeViewModel_WhenInputIsInt_NormalizesIntegerOnCommit()
    {
        var tag = new TagModel { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Value = 0 };
        var node = new ComparatorNodeViewModel(tag, CompareOp.GT, lo: 0);

        // Operator types "100.0"
        node.ThresholdLoText = "100.0";
        node.CommitThresholdLoCommand.Execute(null);

        // Normalization: "100.0" -> "100"
        Assert.Equal("100", node.ThresholdLoText);
        Assert.Equal(100, node.ThresholdLo);
    }

    [Fact]
    public void ComparatorNodeViewModel_WhenInputIsBool_ButtonsSetZeroAndOne()
    {
        var tag = new TagModel { Name = "DI0", Kind = TagKind.DiscreteInput, DataType = TagDataType.Boolean };
        var node = new ComparatorNodeViewModel(tag, CompareOp.EQ, lo: 0);

        Assert.True(node.IsInputBoolean);
        Assert.Equal("BOOL", node.InputTagDataTypeText);

        // Click [ 1 ] button
        node.SetThresholdBoolCommand.Execute(1);
        Assert.Equal(1, node.ThresholdLo);
        Assert.Equal("1", node.ThresholdLoText);

        // Click [ 0 ] button
        node.SetThresholdBoolCommand.Execute(0);
        Assert.Equal(0, node.ThresholdLo);
        Assert.Equal("0", node.ThresholdLoText);
    }

    [Fact]
    public void ComparatorNodeViewModel_WhenInputIsReal_NormalizesToRealFormat()
    {
        var tag = new TagModel { Name = "VREG_FLOAT", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var node = new ComparatorNodeViewModel(tag, CompareOp.GT, lo: 10.0);

        Assert.True(node.IsInputReal);
        Assert.Equal("REAL", node.InputTagDataTypeText);

        // Operator types integer "25"
        node.ThresholdLoText = "25";
        node.CommitThresholdLoCommand.Execute(null);

        // Real normalization: "25" -> "25.0"
        Assert.Equal("25.0", node.ThresholdLoText);
        Assert.Equal(25.0, node.ThresholdLo);

        // Operator types float "25.5"
        node.ThresholdLoText = "25.5";
        node.CommitThresholdLoCommand.Execute(null);
        Assert.Equal("25.5", node.ThresholdLoText);
        Assert.Equal(25.5, node.ThresholdLo);
    }

    [Fact]
    public void ComparatorNodeViewModel_WhenInputIsReal_RevertsInvalidText()
    {
        var tag = new TagModel { Name = "VREG_FLOAT", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var node = new ComparatorNodeViewModel(tag, CompareOp.GT, lo: 10.0);

        node.ThresholdLoText = "invalid_text";
        node.CommitThresholdLoCommand.Execute(null);

        Assert.Equal("10.0", node.ThresholdLoText);
        Assert.Equal(10.0, node.ThresholdLo);
    }

    [Fact]
    public void ComparatorNodeViewModel_WhenFormulaExpressionWithInt_DoesNotContainDecimals()
    {
        var tag = new TagModel { Name = "VREG1", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32 };
        var node = new ComparatorNodeViewModel(tag, CompareOp.GT, lo: 50);

        Assert.Equal("VREG1 > 50", node.FormulaExpression);
    }

    [Fact]
    public void ComparatorNodeViewModel_WhenFormulaExpressionWithReal_ContainsDecimal()
    {
        var tag = new TagModel { Name = "VREG_R", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var node = new ComparatorNodeViewModel(tag, CompareOp.GT, lo: 50.0);

        Assert.Equal("VREG_R > 50.0", node.FormulaExpression);
    }

    // =========================================================================
    // 2. ActionNodeViewModel Tests
    // =========================================================================

    [Fact]
    public void ActionNodeViewModel_WhenTargetIsInt_RevertsFloatOnCommit()
    {
        var tag = new TagModel { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Value = 10 };
        var actionNode = new ActionNodeViewModel(tag)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = 10
        };

        Assert.True(actionNode.IsTargetInteger);
        Assert.False(actionNode.IsTargetBoolean);
        Assert.False(actionNode.IsTargetReal);

        // Drafting phase: typing fractional value "25.5"
        actionNode.ActionParamText = "25.5";
        Assert.Equal(10, actionNode.ActionParam);

        // Committed phase: LostFocus / Enter
        actionNode.CommitActionParamCommand.Execute(null);

        // Auto-Revert to valid committed state
        Assert.Equal("10", actionNode.ActionParamText);
        Assert.Equal(10, actionNode.ActionParam);
    }

    [Fact]
    public void ActionNodeViewModel_WhenTargetIsInt_NormalizesIntegerOnCommit()
    {
        var tag = new TagModel { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Value = 0 };
        var actionNode = new ActionNodeViewModel(tag)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = 0
        };

        actionNode.ActionParamText = "100.0";
        actionNode.CommitActionParamCommand.Execute(null);

        Assert.Equal("100", actionNode.ActionParamText);
        Assert.Equal(100, actionNode.ActionParam);
    }

    [Fact]
    public void ActionNodeViewModel_WhenTargetIsReal_NormalizesIntegerToReal()
    {
        var tag = new TagModel { Name = "VREG_FLOAT", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var actionNode = new ActionNodeViewModel(tag)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = 0
        };

        Assert.True(actionNode.IsTargetReal);

        actionNode.ActionParamText = "25";
        actionNode.CommitActionParamCommand.Execute(null);

        Assert.Equal("25.0", actionNode.ActionParamText);
        Assert.Equal(BitConverter.SingleToInt32Bits(25.0f), actionNode.ActionParam);
    }

    [Fact]
    public void ActionNodeViewModel_WhenTargetIsReal_ActionExpressionUsesFloatDisplay()
    {
        var tag = new TagModel { Name = "VREG_FLOAT", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var actionNode = new ActionNodeViewModel(tag)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = BitConverter.SingleToInt32Bits(25.5f)
        };

        Assert.Equal("VREG_FLOAT = 25.5", actionNode.ActionExpression);
    }

    [Fact]
    public void ActionNodeViewModel_WhenTargetIsBool_NormalizesBooleans()
    {
        var tag = new TagModel { Name = "DO0", Kind = TagKind.DiscreteOutput, DataType = TagDataType.Boolean };
        var actionNode = new ActionNodeViewModel(tag)
        {
            ActionType = ActionType.SET_TAG,
            ActionParam = 0
        };

        Assert.True(actionNode.IsTargetBoolean);

        actionNode.SetActionParamBoolCommand.Execute("1");
        Assert.Equal(1, actionNode.ActionParam);
        Assert.Equal("1", actionNode.ActionParamText);

        actionNode.ActionParamText = "true";
        actionNode.CommitActionParamCommand.Execute(null);
        Assert.Equal(1, actionNode.ActionParam);
        Assert.Equal("1", actionNode.ActionParamText);
    }

    // =========================================================================
    // 3. InputNodeViewModel Tests
    // =========================================================================

    [Fact]
    public void InputNodeViewModel_WhenTagIsInt_RevertsFloatOnCommit()
    {
        var tag = new TagModel { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Value = 42 };
        var inputNode = new InputNodeViewModel(tag);

        Assert.Equal("42", inputNode.TagValueText);

        // Drafting invalid fractional value
        inputNode.TagValueText = "99.9";
        Assert.Equal(42, tag.Value);

        // Committed phase
        inputNode.CommitTagValueCommand.Execute(null);

        // Auto-revert
        Assert.Equal("42", inputNode.TagValueText);
        Assert.Equal(42, tag.Value);
    }

    [Fact]
    public void InputNodeViewModel_WhenTagIsInt_NormalizesIntegerOnCommit()
    {
        var tag = new TagModel { Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32, Value = 0 };
        var inputNode = new InputNodeViewModel(tag);

        inputNode.TagValueText = "100.0";
        inputNode.CommitTagValueCommand.Execute(null);

        Assert.Equal("100", inputNode.TagValueText);
        Assert.Equal(100, tag.Value);
    }

    [Fact]
    public void InputNodeViewModel_WhenTagIsReal_NormalizesIntegerToReal()
    {
        var tag = new TagModel { Name = "VREG_FLOAT", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32, Value = 0 };
        var inputNode = new InputNodeViewModel(tag);

        inputNode.TagValueText = "50";
        inputNode.CommitTagValueCommand.Execute(null);

        Assert.Equal("50.0", inputNode.TagValueText);
        Assert.Equal(BitConverter.SingleToInt32Bits(50.0f), tag.Value);
    }

    [Fact]
    public void InputNodeViewModel_WhenTagIsBool_SetsZeroAndOne()
    {
        var tag = new TagModel { Name = "DI0", Kind = TagKind.DiscreteInput, DataType = TagDataType.Boolean, Value = 0 };
        var inputNode = new InputNodeViewModel(tag);

        inputNode.SetTagValueBoolCommand.Execute(1);
        Assert.Equal(1, tag.Value);
        Assert.Equal("1", inputNode.ValueBadgeText);

        inputNode.SetTagValueBoolCommand.Execute(0);
        Assert.Equal(0, tag.Value);
        Assert.Equal("0", inputNode.ValueBadgeText);
    }

    // =========================================================================
    // 4. RuleItemModel Tests
    // =========================================================================

    [Fact]
    public void RuleItemModel_WhenActionIsReal_FormatsSummaryCorrectly()
    {
        var tag = new TagModel { Name = "FLOAT_OUT", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var rule = new RuleItemModel
        {
            ActionTag = tag,
            ActionType = ActionType.SET_TAG,
            ActionParam = BitConverter.SingleToInt32Bits(25.5f)
        };

        Assert.Equal("Set FLOAT_OUT=25.5", rule.ActionSummary);
    }

    [Fact]
    public void RuleItemModel_WhenGuardIsInt_FormatsSummaryWithoutDecimals()
    {
        var tag = new TagModel { Name = "V1", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32 };
        var rule = new RuleItemModel
        {
            GuardTag = tag,
            CompareOp = SimplePLC.Studio.Models.CompareOp.GT,
            ThresholdLo = 50.0
        };

        Assert.Equal("V1 > 50", rule.GuardSummary);
    }

    [Fact]
    public void RuleItemModel_WhenGuardIsReal_FormatsSummaryWithDecimals()
    {
        var tag = new TagModel { Name = "R1", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var rule = new RuleItemModel
        {
            GuardTag = tag,
            CompareOp = SimplePLC.Studio.Models.CompareOp.GT,
            ThresholdLo = 50.0
        };

        Assert.Equal("R1 > 50.0", rule.GuardSummary);
    }
}
