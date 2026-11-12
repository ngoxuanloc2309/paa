namespace SimplePLC.Application.Logic.Compilation.MacroLowering;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

/// <summary>
/// Hạ cấp (Macro Lowering) các cờ trạng thái Modbus Master (Valid, Error) thành tập luật logic cơ sở.
/// </summary>
public static class ModbusStatusMacroLowering
{
    /// <summary>
    /// Thực hiện mở rộng các cờ trạng thái Modbus Read nodes thành các CandidateRule.
    /// </summary>
    public static void Lower(
        LogicGraph graph,
        ProductDefinition product,
        IReadOnlyList<LogicNode> modbusReadNodes,
        List<CandidateRule> candidateRules)
    {
        foreach (var mbNode in modbusReadNodes)
        {
            var mbData = mbNode.ModbusReadData;
            if (mbData == null) continue;

            string macroId = mbNode.Id;
            string labelPrefix = string.IsNullOrWhiteSpace(mbNode.Label) ? "MB_READ" : mbNode.Label;

            if (mbData.ValidTagIndex.HasValue)
            {
                var validTag = product.FindTagByIndex(mbData.ValidTagIndex.Value);
                if (validTag != null)
                {
                    var trigValid = new TriggerModel(validTag, TriggerKind.OnRise);
                    var actValid = new ActionModel(validTag, ActionKind.SetTag, 1);
                    var originValid = new GeneratedRuleOrigin(mbNode.Id, macroId, "MB_READ", 0, $"{labelPrefix} (Valid)");
                    candidateRules.Add(new CandidateRule(mbNode, trigValid, actValid, GuardModel.Empty, $"{labelPrefix}_VALID", originValid));
                }
            }

            if (mbData.ErrorTagIndex.HasValue)
            {
                var errorTag = product.FindTagByIndex(mbData.ErrorTagIndex.Value);
                if (errorTag != null)
                {
                    var trigError = new TriggerModel(errorTag, TriggerKind.OnRise);
                    var actError = new ActionModel(errorTag, ActionKind.SetTag, 1);
                    var originError = new GeneratedRuleOrigin(mbNode.Id, macroId, "MB_READ", 1, $"{labelPrefix} (Error)");
                    candidateRules.Add(new CandidateRule(mbNode, trigError, actError, GuardModel.Empty, $"{labelPrefix}_ERROR", originError));
                }
            }
        }
    }
}
