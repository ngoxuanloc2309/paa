using SimplePLC.Domain.Enums;

namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô hình nghiệp vụ của hành động thực thi (Action) trong Rule.
/// </summary>
public sealed class ActionModel
{
    public TagDefinition TargetTag { get; set; }
    public ActionKind Type { get; set; }
    public double Parameter { get; set; }
    public bool IsFloat { get; set; }

    public ActionModel(TagDefinition targetTag, ActionKind type = ActionKind.SetTag, double parameter = 0)
    {
        TargetTag = targetTag ?? throw new ArgumentNullException(nameof(targetTag));
        Type = type;
        Parameter = parameter;
        IsFloat = Math.Abs(parameter - Math.Round(parameter)) > 1e-6;
    }
}
