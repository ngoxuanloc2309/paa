namespace SimplePLC.Application.Models;

public sealed record RuntimeTagValue
{
    public ushort TagIndex { get; init; }
    public string TagName { get; init; } = string.Empty;
    public int Value { get; init; }
    public ushort Quality { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
