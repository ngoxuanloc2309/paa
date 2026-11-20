namespace SimplePLC.Application.Mapping;

using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Chuyển đổi hai chiều giữa mô hình miền Rule và DTO giao thức Modbus RuleRecordDto (32-byte).
/// </summary>
public static class RuleMapper
{
    public static RuleRecordDto ToDto(Rule domainRule)
    {
        ArgumentNullException.ThrowIfNull(domainRule);

        byte flags = (byte)SPLC_RuleFlags.NONE;
        if (domainRule.Trigger.IsFloat) flags |= (byte)SPLC_RuleFlags.TRIGGER_FLOAT;
        if (domainRule.Action.IsFloat)  flags |= (byte)SPLC_RuleFlags.ACTION_FLOAT;

        // SCALE_TAG thăng hạng kiểu: nguồn số nguyên sang đích Float32
        if (domainRule.Action.Type == ActionKind.ScaleTag &&
            domainRule.Trigger.Tag.DataType != TagDataType.Float32 &&
            domainRule.Action.IsFloat)
        {
            flags |= (byte)(SPLC_RuleFlags.TRIGGER_FLOAT | SPLC_RuleFlags.ACTION_FLOAT | SPLC_RuleFlags.INT_TO_FLOAT);
        }

        bool isMathOp = domainRule.Action.Type == ActionKind.MathOp;

        int thresholdLo = isMathOp ? 0 : (domainRule.Trigger.IsFloat
            ? BitConverter.SingleToInt32Bits((float)domainRule.Trigger.ThresholdLo)
            : (int)Math.Round(domainRule.Trigger.ThresholdLo));

        int thresholdHi = isMathOp ? 0 : (domainRule.Trigger.IsFloat
            ? BitConverter.SingleToInt32Bits((float)domainRule.Trigger.ThresholdHi)
            : (int)Math.Round(domainRule.Trigger.ThresholdHi));

        uint forMs = isMathOp ? 0 : domainRule.Trigger.ForMs;

        // Đối với MATH_OP: Reg 6..7 (ActionParam) bắt buộc ghi 0 theo chuẩn SPLC-SPEC-MATH-007
        int actionParam = isMathOp ? 0 : (domainRule.Action.IsFloat
            ? BitConverter.SingleToInt32Bits((float)domainRule.Action.Parameter)
            : (int)Math.Round(domainRule.Action.Parameter));

        int auxParam = domainRule.Action.IsFloat
            ? BitConverter.SingleToInt32Bits((float)domainRule.Action.AuxParameter)
            : (int)Math.Round(domainRule.Action.AuxParameter);

        var dto = new RuleRecordDto
        {
            ThresholdLo = thresholdLo,
            ThresholdHi = thresholdHi,
            ForMs = forMs,
            ActionParam = actionParam,
            AuxParam = auxParam,
            TriggerTag = domainRule.Trigger.Tag.TagIndex,
            ActionTag = domainRule.Action.TargetTag.TagIndex,
            Enabled = domainRule.Enabled,
            TriggerType = (SPLC_TriggerType)domainRule.Trigger.Type,
            CompareOp = (SPLC_CompareOp)domainRule.Trigger.CompareOp,
            ActionType = (SPLC_ActionType)domainRule.Action.Type,
            Flags = flags
        };

        if (domainRule.Guard.HasGuard && domainRule.Guard.Tag != null)
        {
            ushort guardTag = (ushort)((domainRule.Guard.Tag.TagIndex & ModbusRegisterMap.GuardTagIndexMask) |
                                      (domainRule.Guard.Negated ? ModbusRegisterMap.GuardTagNegateMask : 0));
            dto.GuardTag = guardTag;
            if (isMathOp)
            {
                // Khi Op2 là tag động, AuxParam phải là 0
                dto.AuxParam = 0;
            }
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

        bool isTriggerFloat = dto.IsTriggerFloat;
        bool isActionFloat = dto.IsActionFloat || (dto.IsTriggerFloat && actionTag.DataType == TagDataType.Float32);

        var trigger = new TriggerModel(triggerTag, (TriggerKind)dto.TriggerType)
        {
            CompareOp = (CompareOperator)dto.CompareOp,
            ThresholdLo = isTriggerFloat ? dto.ThresholdLoFloat : dto.ThresholdLo,
            ThresholdHi = isTriggerFloat ? dto.ThresholdHiFloat : dto.ThresholdHi,
            IsFloat = isTriggerFloat,
            ForMs = dto.ForMs
        };

        bool isMathOp = (ActionKind)dto.ActionType == ActionKind.MathOp;
        double auxParam = isActionFloat
            ? (dto.AuxParam != 0 || dto.ThresholdHi == 0 ? dto.AuxParamFloat : dto.ThresholdHiFloat)
            : (dto.AuxParam != 0 || dto.ThresholdHi == 0 ? dto.AuxParam : dto.ThresholdHi);

        double actionParamVal;
        if (isMathOp)
        {
            // Đối với MathOp: nếu không có guard tag (0x7FFF), hằng số Op2 nằm ở auxParam
            actionParamVal = !dto.HasGuard ? auxParam : 0.0;
        }
        else
        {
            actionParamVal = isActionFloat ? dto.ActionParamFloat : dto.ActionParam;
        }

        var action = new ActionModel(
            actionTag,
            (ActionKind)dto.ActionType,
            actionParamVal,
            isMathOp ? actionParamVal : auxParam)
        {
            IsFloat = isActionFloat
        };

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
