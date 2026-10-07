using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn 10 thanh ghi (20 bytes) của SPLC_DeviceHealth đọc tại địa chỉ 0x0800.
/// </summary>
public sealed class DeviceHealthDto
{
    public uint UptimeSeconds { get; set; }
    public SPLC_ResetReason ResetReason { get; set; }
    public SPLC_HealthFlags HealthFlags { get; set; }
    public ushort CpuLoadPercent { get; set; }
    public ushort RamUsagePercent { get; set; }
    public uint ScanTimeMs { get; set; }
    public uint MaxScanTimeMs { get; set; }
}
