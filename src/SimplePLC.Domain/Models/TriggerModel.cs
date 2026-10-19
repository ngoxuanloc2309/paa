using SimplePLC.Domain.Enums;

namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô hình nghiệp vụ của thành phần Trigger trong Rule.
/// </summary>
public sealed class TriggerModel
{
    public TagDefinition Tag { get; set; }
    public TriggerKind Type { get; set; }
    public double ThresholdLo { get; set; }
    public double ThresholdHi { get; set; }
    public bool IsFloat { get; set; }
    public uint ForMs { get; set; }
    public CompareOperator CompareOp { get; set; }

    public TriggerModel(TagDefinition tag, TriggerKind type = TriggerKind.OnChange, double thresholdLo = 0, double thresholdHi = 0, uint forMs = 0)
    {
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        Type = type;
        ThresholdLo = thresholdLo;
        ThresholdHi = thresholdHi;
        ForMs = forMs;
        CompareOp = CompareOperator.None;
        IsFloat = Math.Abs(thresholdLo - Math.Round(thresholdLo)) > 1e-6 || Math.Abs(thresholdHi - Math.Round(thresholdHi)) > 1e-6;
    }
}
