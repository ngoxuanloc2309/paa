namespace SimplePLC.Application.Mapping;

using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

public static class RuleMapper
{
    public static RuleRecordDto ToDto(Rule domainRule)
    {
        ArgumentNullException.ThrowIfNull(domainRule);

        var dto = new RuleRecordDto
        {
            ThresholdLo = domainRule.Trigger.ThresholdLo,
            ThresholdHi = domainRule.Trigger.ThresholdHi,
            ForMs = domainRule.Trigger.ForMs,
            ActionParam = domainRule.Action.Parameter,
            TriggerTag = domainRule.Trigger.Tag.TagIndex,
            ActionTag = domainRule.Action.TargetTag.TagIndex,
            Enabled = domainRule.Enabled,
            TriggerType = (SPLC_TriggerType)domainRule.Trigger.Type,
            CompareOp = (SPLC_CompareOp)domainRule.Trigger.CompareOp,
            ActionType = (SPLC_ActionType)domainRule.Action.Type
        };

        if (domainRule.Guard.HasGuard && domainRule.Guard.Tag != null)
        {
            ushort guardTag = (ushort)((domainRule.Guard.Tag.TagIndex & ModbusRegisterMap.GuardTagIndexMask) |
                                      (domainRule.Guard.Negated ? ModbusRegisterMap.GuardTagNegateMask : 0));
            dto.GuardTag = guardTag;
        }
        else
        {
            dto.GuardTag = ModbusRegisterMap.GuardTagNone;
        }

        return dto;
    }

    public static Rule ToDomain(RuleRecordDto dto, ushort ruleIndex, ProductDefinition? product = null)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var triggerTag = product?.FindTagByIndex(dto.TriggerTag) ??
                         new TagDefinition(dto.TriggerTag, $"TAG_{dto.TriggerTag}", TagKind.DiscreteInput, TagDataType.Boolean, isReadOnly: true);

        var actionTag = product?.FindTagByIndex(dto.ActionTag) ??
                        new TagDefinition(dto.ActionTag, $"TAG_{dto.ActionTag}", TagKind.DiscreteOutput, TagDataType.Boolean, isReadOnly: false);

        var trigger = new TriggerModel(triggerTag, (TriggerKind)dto.TriggerType)
        {
            CompareOp = (CompareOperator)dto.CompareOp,
            ThresholdLo = dto.ThresholdLo,
            ThresholdHi = dto.ThresholdHi,
            ForMs = dto.ForMs
        };

        var action = new ActionModel(actionTag, (ActionKind)dto.ActionType, dto.ActionParam);

        GuardModel guard = GuardModel.Empty;
        if (dto.HasGuard)
        {
            var guardTag = product?.FindTagByIndex(dto.GuardTagIndex) ??
                           new TagDefinition(dto.GuardTagIndex, $"TAG_{dto.GuardTagIndex}", TagKind.DiscreteInput, TagDataType.Boolean, isReadOnly: true);
            guard = new GuardModel(guardTag, dto.GuardNegated);
        }

        var ruleName = $"Rule_{ruleIndex + 1}";
        return new Rule(ruleIndex, ruleName, trigger, action, guard, dto.Enabled);
    }

    public static RuleRecordDto[] ToDtoArray(RuleTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var dtos = new RuleRecordDto[table.Rules.Count];
        for (int i = 0; i < table.Rules.Count; i++)
        {
            dtos[i] = ToDto(table.Rules[i]);
        }
        return dtos;
    }

    public static RuleTable ToDomainTable(IEnumerable<RuleRecordDto> dtos, ProductDefinition? product = null)
    {
        ArgumentNullException.ThrowIfNull(dtos);
        var table = new RuleTable();
        ushort index = 0;
        foreach (var dto in dtos)
        {
            table.AddRule(ToDomain(dto, index++, product));
        }
        return table;
    }
}
