namespace SimplePLC.Application.Models;

/// <summary>
/// Trạng thái tương thích của thiết bị kết nối.
/// </summary>
public enum CompatibilityStatus
{
    Compatible,
    UnsupportedDeviceClass,
    UnsupportedDeviceVariant,
    UnsupportedProtocolVersion,
    UnsupportedRuleFormatVersion,
    Unknown
}

/// <summary>
/// Kết quả thẩm định tương thích giữa firmware vi điều khiển và phần mềm SimplePLC.
/// </summary>
public sealed record CompatibilityResult
{
    public bool IsCompatible { get; init; }
    public CompatibilityStatus Status { get; init; }
    public string? Reason { get; init; }

    public static CompatibilityResult Ok() =>
        new() { IsCompatible = true, Status = CompatibilityStatus.Compatible };

    public static CompatibilityResult Incompatible(CompatibilityStatus status, string reason) =>
        new() { IsCompatible = false, Status = status, Reason = reason };
}
