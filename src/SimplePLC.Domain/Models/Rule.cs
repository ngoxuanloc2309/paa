namespace SimplePLC.Domain.Models;

/// <summary>
/// Thực thể nghiệp vụ cốt lõi của một Rule trong SimplePLC (Transport-Agnostic, R9).
/// </summary>
public sealed class Rule
{
    /// <summary>
    /// Vị trí 0-based của rule trong bảng Rule Table theo quy chuẩn V1 (R9).
    /// </summary>
    public int RuleIndex { get; set; }

    public string Name { get; set; }
    public string Description { get; set; }
    public bool Enabled { get; set; }

    public TriggerModel Trigger { get; set; }
    public GuardModel Guard { get; set; }
    public ActionModel Action { get; set; }

    /// <summary>
    /// ID của Action Node tương ứng trên Logic Graph (dùng để đồng bộ trực quan Power Flow).
    /// </summary>
    public string? ActionNodeId { get; set; }

    public Rule(
        int ruleIndex,
        string name,
        TriggerModel trigger,
        ActionModel action,
        GuardModel? guard = null,
        bool enabled = true,
        string description = "")
    {
        RuleIndex = ruleIndex;
        Name = string.IsNullOrWhiteSpace(name) ? $"Rule_{ruleIndex + 1}" : name;
        Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Guard = guard ?? GuardModel.Empty;
        Enabled = enabled;
        Description = description ?? string.Empty;
    }
}
