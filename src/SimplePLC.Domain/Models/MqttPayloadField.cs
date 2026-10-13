namespace SimplePLC.Domain.Models;

/// <summary>
/// Đại diện cho 1 trường dữ liệu trong bản tin Telemetry đa biến MQTT.
/// </summary>
public sealed record MqttPayloadField(
    string Key,
    ushort TagIndex,
    string? Unit = null
);
