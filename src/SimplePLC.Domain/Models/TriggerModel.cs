using SimplePLC.Domain.Enums;

namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô hình nghiệp vụ của thành phần Trigger trong Rule.
/// </summary>
public sealed class TriggerModel
{
    public TagDefinition Tag { get; set; }
    public TriggerKind Type { get; set; }
    public int ThresholdLo { get; set; }
    public int ThresholdHi { get; set; }
    public uint ForMs { get; set; }
    public CompareOperator CompareOp { get; set; }

    public TriggerModel(TagDefinition tag, TriggerKind type = TriggerKind.OnChange)
    {
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        Type = type;
        CompareOp = CompareOperator.None;
    }
}
