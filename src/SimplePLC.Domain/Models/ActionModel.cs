using SimplePLC.Domain.Enums;

namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô hình nghiệp vụ của hành động thực thi (Action) trong Rule.
/// </summary>
public sealed class ActionModel
{
    public TagDefinition TargetTag { get; set; }
    public ActionKind Type { get; set; }
    public int Parameter { get; set; }

    public ActionModel(TagDefinition targetTag, ActionKind type = ActionKind.SetTag, int parameter = 0)
    {
        TargetTag = targetTag ?? throw new ArgumentNullException(nameof(targetTag));
        Type = type;
        Parameter = parameter;
    }
}
