namespace SimplePLC.Application.Mapping;

using SimplePLC.Application.Models;
using SimplePLC.Protocol.Dto;

public static class DeviceHealthMapper
{
    public static DeviceHealthInfo ToInfo(DeviceHealthDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return new DeviceHealthInfo
        {
            UptimeSeconds = dto.UptimeSeconds,
            CpuLoadPercent = dto.CpuLoadPercent,
            RamUsagePercent = dto.RamUsagePercent,
            ScanTimeMs = dto.ScanTimeMs,
            MaxScanTimeMs = dto.MaxScanTimeMs,
            ResetReason = dto.ResetReason,
            HealthFlags = dto.HealthFlags,
            Timestamp = DateTime.UtcNow
        };
    }
}
