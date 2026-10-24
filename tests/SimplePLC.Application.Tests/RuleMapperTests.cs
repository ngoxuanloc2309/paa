using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public class RuleMapperTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public void ToDto_MapsAllFieldsAccurately()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var di1 = _product.FindTagByName("DI1")!;

        var trigger = new TriggerModel(di0, TriggerKind.OnRise)
        {
            CompareOp = CompareOperator.GreaterThan,
            ThresholdLo = 100,
            ThresholdHi = 500,
            ForMs = 250
        };

        var action = new ActionModel(do0, ActionKind.SetTag, 1);
        var guard = new GuardModel(di1, negated: true);

        var domainRule = new Rule(0, "TestRule", trigger, action, guard, enabled: true);

        // Act
        var dto = RuleMapper.ToDto(domainRule);

        // Assert
        Assert.Equal(100, dto.ThresholdLo);
        Assert.Equal(500, dto.ThresholdHi);
        Assert.Equal(250u, dto.ForMs);
        Assert.Equal(1, dto.ActionParam);
        Assert.Equal(di0.TagIndex, dto.TriggerTag);
        Assert.Equal(do0.TagIndex, dto.ActionTag);
        Assert.True(dto.Enabled);
        Assert.Equal(SPLC_TriggerType.ON_RISE, dto.TriggerType);
        Assert.Equal(SPLC_CompareOp.GT, dto.CompareOp);
        Assert.Equal(SPLC_ActionType.SET_TAG, dto.ActionType);

        // GuardTag must have bit 15 set (Negate)
        Assert.True(dto.GuardNegated);
        Assert.Equal(di1.TagIndex, dto.GuardTagIndex);
    }

    [Fact]
    public void ToDto_NoGuard_SetsGuardTagToSentinel0x7FFF()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var trigger = new TriggerModel(di0, TriggerKind.OnChange);
        var action = new ActionModel(do0, ActionKind.ToggleTag, 0);
        var domainRule = new Rule(0, "NoGuardRule", trigger, action, guard: GuardModel.Empty);

        // Act
        var dto = RuleMapper.ToDto(domainRule);

        // Assert
        Assert.Equal(ModbusRegisterMap.GuardTagNone, dto.GuardTag); // 0x7FFF
        Assert.False(dto.HasGuard);
        Assert.False(dto.GuardNegated);
        Assert.Equal(0x7FFF, dto.GuardTagIndex);

        // Verify roundtrip ToDomain preserves NoGuard
        var reconstructed = RuleMapper.ToDomain(dto, 0, _product);
        Assert.False(reconstructed.Guard.HasGuard);
    }

    [Fact]
    public void ToDto_GuardIsDI0_SetsGuardTagTo0_AndReconstructedCorrectly()
    {
        // Chân DI0 (index 0) làm Guard
        var di1 = _product.FindTagByName("DI1")!;
        var di0 = _product.FindTagByName("DI0")!; // Index = 0
        var do0 = _product.FindTagByName("DO0")!;

        var trigger = new TriggerModel(di1, TriggerKind.OnRise);
        var action = new ActionModel(do0, ActionKind.SetTag, 1);
        var guard = new GuardModel(di0, negated: false); // Guard trên DI0 không đảo
        var domainRule = new Rule(0, "DI0GuardRule", trigger, action, guard);

        // Act
        var dto = RuleMapper.ToDto(domainRule);

        // Assert
        Assert.Equal(0x0000, dto.GuardTag); // DI0 index = 0, not negated
        Assert.True(dto.HasGuard);
        Assert.False(dto.GuardNegated);
        Assert.Equal(0, dto.GuardTagIndex);

        // Verify roundtrip ToDomain correctly preserves DI0 as Guard!
        var reconstructed = RuleMapper.ToDomain(dto, 0, _product);
        Assert.True(reconstructed.Guard.HasGuard);
        Assert.Equal("DI0", reconstructed.Guard.Tag!.Name);
        Assert.Equal(0, reconstructed.Guard.Tag!.TagIndex);
    }

    [Fact]
    public void Roundtrip_DomainToDtoAndBack_PreservesAllSemantics()
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;
        var vflag0 = _product.FindTagByName("VFLAG0")!;

        var trigger = new TriggerModel(ai0, TriggerKind.TimeWindow)
        {
            CompareOp = CompareOperator.Between,
            ThresholdLo = 50,
            ThresholdHi = 200,
            ForMs = 1000
        };

        var action = new ActionModel(vreg0, ActionKind.IncrementCounter, 5);
        var guard = new GuardModel(vflag0, negated: false);

        var originalRule = new Rule(3, "ComplexRule", trigger, action, guard, enabled: true);

        // Act
        var dto = RuleMapper.ToDto(originalRule);
        var reconstructed = RuleMapper.ToDomain(dto, 3, _product);

        // Assert
        Assert.Equal(originalRule.RuleIndex, reconstructed.RuleIndex);
        Assert.Equal(originalRule.Enabled, reconstructed.Enabled);
        Assert.Equal(originalRule.Trigger.Type, reconstructed.Trigger.Type);
        Assert.Equal(originalRule.Trigger.CompareOp, reconstructed.Trigger.CompareOp);
        Assert.Equal(originalRule.Trigger.ThresholdLo, reconstructed.Trigger.ThresholdLo);
        Assert.Equal(originalRule.Trigger.ThresholdHi, reconstructed.Trigger.ThresholdHi);
        Assert.Equal(originalRule.Trigger.ForMs, reconstructed.Trigger.ForMs);
        Assert.Equal(originalRule.Trigger.Tag.TagIndex, reconstructed.Trigger.Tag.TagIndex);

        Assert.Equal(originalRule.Action.Type, reconstructed.Action.Type);
        Assert.Equal(originalRule.Action.Parameter, reconstructed.Action.Parameter);
        Assert.Equal(originalRule.Action.TargetTag.TagIndex, reconstructed.Action.TargetTag.TagIndex);

        Assert.True(reconstructed.Guard.HasGuard);
        Assert.Equal(originalRule.Guard.Tag!.TagIndex, reconstructed.Guard.Tag!.TagIndex);
        Assert.Equal(originalRule.Guard.Negated, reconstructed.Guard.Negated);
    }

    [Fact]
    public void TableMapping_Roundtrip_MaintainsOrderAndCount()
    {
        var table = new RuleTable();
        for (int i = 0; i < 10; i++)
        {
            var di = _product.FindTagByIndex((ushort)(i % 8))!;
            var doTag = _product.FindTagByIndex((ushort)(8 + (i % 8)))!;
            var trigger = new TriggerModel(di, TriggerKind.OnChange);
            var action = new ActionModel(doTag, ActionKind.SetTag, i);
            table.AddRule(new Rule(i, $"Rule_{i}", trigger, action));
        }

        // Act
        var dtos = RuleMapper.ToDtoArray(table);
        var restoredTable = RuleMapper.ToDomainTable(dtos, _product);

        // Assert
        Assert.Equal(10, dtos.Length);
        Assert.Equal(10, restoredTable.Count);
        for (int i = 0; i < 10; i++)
        {
            Assert.Equal(i, restoredTable.Rules[i].RuleIndex);
            Assert.Equal(table.Rules[i].Action.Parameter, restoredTable.Rules[i].Action.Parameter);
        }
    }

    [Fact]
    public void ToDto_FloatThresholds_MapsIeee754AndSetsFlag()
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var trigger = new TriggerModel(ai0, TriggerKind.OnChange, thresholdLo: 55.5, thresholdHi: 100.25)
        {
            CompareOp = CompareOperator.Between,
            IsFloat = true
        };
        var action = new ActionModel(do0, ActionKind.SetTag, parameter: 1.0)
        {
            IsFloat = true
        };

        var rule = new Rule(0, "FloatTempRule", trigger, action, GuardModel.Empty, enabled: true);

        // Act
        var dto = RuleMapper.ToDto(rule);

        // Assert
        Assert.True(dto.IsFloatData);
        Assert.Equal(55.5f, dto.ThresholdLoFloat, precision: 4);
        Assert.Equal(100.25f, dto.ThresholdHiFloat, precision: 4);
        Assert.Equal(1.0f, dto.ActionParamFloat, precision: 4);
    }

    [Fact]
    public void Roundtrip_FloatRule_PreservesAccurateValues()
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var trigger = new TriggerModel(ai0, TriggerKind.OnChange, thresholdLo: 23.45, thresholdHi: 89.12)
        {
            CompareOp = CompareOperator.Between,
            IsFloat = true
        };
        var action = new ActionModel(vreg0, ActionKind.ScaleTag, parameter: 0.5)
        {
            IsFloat = true
        };

        var rule = new Rule(1, "FloatScaleRule", trigger, action, GuardModel.Empty, enabled: true);

        // Act
        var dto = RuleMapper.ToDto(rule);
        var restored = RuleMapper.ToDomain(dto, 1, _product);

        // Assert
        Assert.True(restored.Trigger.IsFloat);
        Assert.True(restored.Action.IsFloat);
        Assert.Equal(23.45, restored.Trigger.ThresholdLo, tolerance: 1e-4);
        Assert.Equal(89.12, restored.Trigger.ThresholdHi, tolerance: 1e-4);
        Assert.Equal(0.5, restored.Action.Parameter, tolerance: 1e-4);
    }

    [Fact]
    public void ToDto_PureIntegerRule_FlagsRemainZeroAndDirectInt32()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var trigger = new TriggerModel(di0, TriggerKind.OnChange, thresholdLo: 1200, thresholdHi: 3400)
        {
            CompareOp = CompareOperator.Between,
            IsFloat = false
        };
        var action = new ActionModel(vreg0, ActionKind.SetTag, parameter: 42)
        {
            IsFloat = false
        };

        var rule = new Rule(2, "IntRule", trigger, action, GuardModel.Empty, enabled: true);

        // Act
        var dto = RuleMapper.ToDto(rule);

        // Assert
        Assert.False(dto.IsFloatData);
        Assert.Equal(1200, dto.ThresholdLo);
        Assert.Equal(3400, dto.ThresholdHi);
        Assert.Equal(42, dto.ActionParam);
    }
    [Fact]
    public void ToDto_MixedRule_IntegerTriggerAndFloatAction_SetsOnlyActionFloatFlag()
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        // AI0 nguyên > 80 -> SET VREG0 (REAL) = 2.5f
        var trigger = new TriggerModel(ai0, TriggerKind.OnChange, thresholdLo: 80, thresholdHi: 0)
        {
            CompareOp = CompareOperator.GreaterThan,
            IsFloat = false
        };
        var action = new ActionModel(vreg0, ActionKind.SetTag, parameter: 2.5)
        {
            IsFloat = true
        };

        var rule = new Rule(3, "MixedRule", trigger, action, GuardModel.Empty, enabled: true);

        // Act
        var dto = RuleMapper.ToDto(rule);

        // Assert
        Assert.Equal((byte)SPLC_RuleFlags.ACTION_FLOAT, dto.Flags); // 0x02
        Assert.False(dto.IsTriggerFloat);
        Assert.True(dto.IsActionFloat);
        Assert.Equal(80, dto.ThresholdLo); // Giữ nguyên số nguyên 80 thô, không bị ép thành float!
        Assert.Equal(2.5f, dto.ActionParamFloat, precision: 4);

        // Roundtrip to domain
        var restored = RuleMapper.ToDomain(dto, 3, _product);
        Assert.False(restored.Trigger.IsFloat);
        Assert.True(restored.Action.IsFloat);
        Assert.Equal(80, restored.Trigger.ThresholdLo);
        Assert.Equal(2.5, restored.Action.Parameter, tolerance: 1e-4);
    }
}
