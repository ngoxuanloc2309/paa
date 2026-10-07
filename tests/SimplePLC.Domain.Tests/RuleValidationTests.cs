using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Domain.Validation;
using Xunit;

namespace SimplePLC.Domain.Tests;

public class RuleValidationTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public void ValidRule_PassesValidation()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var vflag0 = _product.FindTagByName("VFLAG0")!;

        var rule = new Rule(
            ruleIndex: 0,
            name: "TestRule",
            trigger: new TriggerModel(di0, TriggerKind.OnRise),
            action: new ActionModel(do0, ActionKind.SetTag, 1),
            guard: new GuardModel(vflag0, negated: false)
        );

        var result = RuleValidator.Validate(rule);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ActionTargetTag_IsReadOnly_FailsWithActionTagReadOnlyError()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var di1 = _product.FindTagByName("DI1")!; // DI1 là Read-Only!

        var rule = new Rule(
            ruleIndex: 0,
            name: "InvalidActionTarget",
            trigger: new TriggerModel(di0, TriggerKind.OnRise),
            action: new ActionModel(di1, ActionKind.SetTag, 1) // Cấm ghi vào DI
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == RuleValidator.ErrorActionTagReadOnly);
    }

    [Fact]
    public void CompareOp_Between_ThresholdLoGreaterThanHi_FailsValidation()
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var trigger = new TriggerModel(ai0, TriggerKind.OnChange)
        {
            CompareOp = CompareOperator.Between,
            ThresholdLo = 100,
            ThresholdHi = 50 // Sai: Lo (100) > Hi (50)
        };

        var rule = new Rule(
            ruleIndex: 0,
            name: "InvalidBetween",
            trigger: trigger,
            action: new ActionModel(do0, ActionKind.SetTag, 1)
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == RuleValidator.ErrorBetweenThresholdsInvalid);
    }

    [Fact]
    public void Trigger_Interval_WithZeroForMs_FailsValidation()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var trigger = new TriggerModel(di0, TriggerKind.Interval)
        {
            ForMs = 0 // Sai: Interval bắt buộc ForMs > 0
        };

        var rule = new Rule(
            ruleIndex: 0,
            name: "InvalidInterval",
            trigger: trigger,
            action: new ActionModel(do0, ActionKind.SetTag, 1)
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == RuleValidator.ErrorIntervalForMsZero);
    }

    [Fact]
    public void Guard_WithAnalogTag_FailsValidation()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var ai0 = _product.FindTagByName("AI0")!; // AI0 là Analog (Int32), không phải Boolean!

        var rule = new Rule(
            ruleIndex: 0,
            name: "InvalidGuard",
            trigger: new TriggerModel(di0, TriggerKind.OnRise),
            action: new ActionModel(do0, ActionKind.SetTag, 1),
            guard: new GuardModel(ai0)
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == RuleValidator.ErrorGuardTagNotBoolean);
    }

    [Fact]
    public void Action_ToggleTag_OnIntegerTag_FailsValidation()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!; // VREG0 là Int32, không thể Toggle!

        var rule = new Rule(
            ruleIndex: 0,
            name: "InvalidToggle",
            trigger: new TriggerModel(di0, TriggerKind.OnRise),
            action: new ActionModel(vreg0, ActionKind.ToggleTag)
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == RuleValidator.ErrorToggleTargetNotBoolean);
    }

    [Fact]
    public void Action_IncrementCounter_OnBooleanTag_FailsValidation()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!; // DO0 là Boolean, không thể Increment!

        var rule = new Rule(
            ruleIndex: 0,
            name: "InvalidIncrement",
            trigger: new TriggerModel(di0, TriggerKind.OnRise),
            action: new ActionModel(do0, ActionKind.IncrementCounter, 1)
        );

        var result = RuleValidator.Validate(rule);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == RuleValidator.ErrorIncrementTargetNotInteger);
    }
}
