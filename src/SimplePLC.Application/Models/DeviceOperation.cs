namespace SimplePLC.Application.Models;

/// <summary>
/// Các tác vụ yêu cầu quyền truy cập độc quyền (Exclusive Access) vào thiết bị PLC.
/// Khi tác vụ độc quyền diễn ra, luồng giám sát chu kỳ (Runtime Monitor) sẽ tự động nhường quyền.
/// </summary>
public enum DeviceOperation
{
    DeployRules,
    SystemCommand,
    FactoryReset,
    Reboot,
    DiagnosticControl,
    DiagnosticHeartbeat
}
