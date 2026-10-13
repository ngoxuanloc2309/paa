namespace SimplePLC.Protocol.Dto;

using System.Collections.Generic;

/// <summary>
/// Đại diện cho cấu hình một trường dữ liệu trong bản tin MQTT đa biến.
/// </summary>
public sealed class MqttPayloadFieldDto
{
    public string Key { get; set; } = string.Empty;
    public ushort TagIndex { get; set; }
    public string? Unit { get; set; }
}

/// <summary>
/// DTO lưu trữ cấu hình khối MQTT Telemetry Publish của SimplePLC.
/// Được đóng gói cùng CompiledProgram để triển khai xuống Runtime hoặc lưu trữ dự án.
/// </summary>
public sealed class MqttTelemetryConfigDto
{
    public string NodeId { get; set; } = string.Empty;
    public string Topic { get; set; } = "synaptix/telemetry/data";
    public string PublishMode { get; set; } = "Event"; // "Event", "COV", "Periodic"
    public double Deadband { get; set; } = 0.5;
    public uint IntervalMs { get; set; } = 1000;
    public int Qos { get; set; } = 0; // 0, 1, 2
    public bool Retain { get; set; } = false;
    public string PayloadMode { get; set; } = "JSON"; // "JSON", "Raw", "Custom"
    public ushort? EnTagIndex { get; set; }
    public ushort? DataTagIndex { get; set; }
    public List<MqttPayloadFieldDto> Fields { get; set; } = new();
    public string CustomTemplate { get; set; } = string.Empty;
    public bool IncludeDevice { get; set; } = true;
    public bool IncludeTimestamp { get; set; } = true;
}
