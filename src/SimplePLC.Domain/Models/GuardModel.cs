namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô hình nghiệp vụ của điều kiện bảo vệ (Guard / Interlock) trong Rule.
/// </summary>
public sealed class GuardModel
{
    public TagDefinition? Tag { get; set; }
    public bool Negated { get; set; }

    public bool HasGuard => Tag != null && Tag.Kind != Enums.TagKind.None;

    public GuardModel(TagDefinition? tag = null, bool negated = false)
    {
        Tag = tag;
        Negated = negated;
    }

    public static GuardModel Empty => new(null, false);
}
