using SimplePLC.Application.Abstractions;

namespace SimplePLC.Application.Models;

/// <summary>
/// Kết quả trả về của quá trình khởi tạo phiên kết nối vi điều khiển từ IDeviceConnectionFactory.
/// </summary>
public sealed record DeviceConnectionResult
{
    public bool IsSuccess => Session != null;
    public IDeviceSession? Session { get; init; }
    public CompatibilityResult Compatibility { get; init; } = CompatibilityResult.Ok();
    public string? FailureReason { get; init; }

    public static DeviceConnectionResult Success(IDeviceSession session) =>
        new()
        {
            Session = session,
            Compatibility = CompatibilityResult.Ok()
        };

    public static DeviceConnectionResult Incompatible(CompatibilityResult compatibility) =>
        new()
        {
            Compatibility = compatibility,
            FailureReason = compatibility.Reason
        };

    public static DeviceConnectionResult Failed(string reason) =>
        new()
        {
            Compatibility = CompatibilityResult.Incompatible(CompatibilityStatus.Unknown, reason),
            FailureReason = reason
        };
}
