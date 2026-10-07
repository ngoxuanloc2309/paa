namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

public sealed record RuleAccessInfo(
    LogicNode ActionNode,
    TriggerModel Trigger,
    ActionModel Action,
    GuardModel Guard,
    string RuleName,
    IReadOnlySet<ushort> TriggerReads,
    IReadOnlySet<ushort> GuardReads,
    IReadOnlySet<ushort> NonCommutativeActionReads,
    IReadOnlySet<ushort> AllActionReads,
    IReadOnlySet<ushort> Writes,
    GeneratedRuleOrigin? Origin = null)
{
    public int StableOrder => ActionNode.ExecutionOrder;
    public string ActionNodeId => ActionNode.Id;

    /// <summary>
    /// Các Tag mà Rule này đọc để xác định quan hệ Producer -> Consumer.
    /// Gồm TriggerReads, GuardReads và NonCommutativeActionReads.
    /// Đối với INC_COUNTER cùng ghi target, target KHÔNG nằm trong NonCommutativeActionReads
    /// nhằm tránh tạo quan hệ phụ thuộc giả và chu trình giả giữa các accumulator độc lập.
    /// </summary>
    public IReadOnlySet<ushort> ProducerDependencyReads { get; } =
        new HashSet<ushort>(TriggerReads.Concat(GuardReads).Concat(NonCommutativeActionReads));
}

public static class RuleAccessAnalyzer
{
    public static RuleAccessInfo Analyze(
        LogicNode actionNode,
        TriggerModel trigger,
        ActionModel action,
        GuardModel guard,
        string ruleName,
        GeneratedRuleOrigin? origin = null)
    {
        var triggerReads = new HashSet<ushort>();
        if (trigger.Tag != null && trigger.Tag.Kind != TagKind.None &&
            trigger.Type is not TriggerKind.Interval)
        {
            triggerReads.Add(trigger.Tag.TagIndex);
        }

        var guardReads = new HashSet<ushort>();
        if (guard.HasGuard && guard.Tag != null && guard.Tag.Kind != TagKind.None)
        {
            guardReads.Add(guard.Tag.TagIndex);
        }

        var nonCommutativeActionReads = new HashSet<ushort>();
        var allActionReads = new HashSet<ushort>();
        var writes = new HashSet<ushort>();

        ushort targetTagIdx = action.TargetTag?.TagIndex ?? 0;

        switch (action.Type)
        {
            case ActionKind.SetTag:
                // SET_TAG ghi đè trực tiếp giá trị vào target, không đọc target
                writes.Add(targetTagIdx);
                break;

            case ActionKind.ToggleTag:
                // TOGGLE_TAG cần đọc giá trị hiện thời để đảo (0 <-> 1)
                allActionReads.Add(targetTagIdx);
                nonCommutativeActionReads.Add(targetTagIdx);
                writes.Add(targetTagIdx);
                break;

            case ActionKind.IncrementCounter:
                // INC_COUNTER là phép cộng giao hoán (X + A + B == X + B + A)
                // Đọc target nhưng là accumulator-compatible nên KHÔNG tạo edge phụ thuộc giữa các accumulator
                allActionReads.Add(targetTagIdx);
                writes.Add(targetTagIdx);
                break;

            case ActionKind.AddTag:
                // ADD_TAG: target += TriggerTag.Value
                if (trigger.Tag != null && trigger.Tag.Kind != TagKind.None)
                {
                    allActionReads.Add(trigger.Tag.TagIndex);
                    nonCommutativeActionReads.Add(trigger.Tag.TagIndex);
                }
                allActionReads.Add(targetTagIdx);
                nonCommutativeActionReads.Add(targetTagIdx);
                writes.Add(targetTagIdx);
                break;

            case ActionKind.ScaleTag:
                // SCALE_TAG phụ thuộc TriggerTag
                if (trigger.Tag != null && trigger.Tag.Kind != TagKind.None)
                {
                    allActionReads.Add(trigger.Tag.TagIndex);
                    nonCommutativeActionReads.Add(trigger.Tag.TagIndex);
                }
                writes.Add(targetTagIdx);
                break;

            case ActionKind.WriteRemote:
            case ActionKind.LogEvent:
            case ActionKind.SendAlarm:
                // External side effects: không ghi vào Local Tag Store
                break;
        }

        return new RuleAccessInfo(
            actionNode,
            trigger,
            action,
            guard,
            ruleName,
            triggerReads,
            guardReads,
            nonCommutativeActionReads,
            allActionReads,
            writes,
            origin);
    }
}
