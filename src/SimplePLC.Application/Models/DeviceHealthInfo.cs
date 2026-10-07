namespace SimplePLC.Application.Models;

using SimplePLC.Protocol.Enums;

public sealed record DeviceHealthInfo
{
    public uint UptimeSeconds { get; init; }
    public ushort CpuLoadPercent { get; init; }
    public ushort RamUsagePercent { get; init; }
    public uint ScanTimeMs { get; init; }
    public uint MaxScanTimeMs { get; init; }
    public SPLC_ResetReason ResetReason { get; init; } = SPLC_ResetReason.UNKNOWN;
    public SPLC_HealthFlags HealthFlags { get; init; } = SPLC_HealthFlags.NONE;
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
