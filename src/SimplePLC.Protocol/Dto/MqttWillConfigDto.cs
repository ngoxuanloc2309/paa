namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO chứa cấu hình Last Will & Testament (Will Message) của MQTT Client (0x0E40..0x0E5F, 32 registers = 64 bytes).
/// </summary>
public sealed class MqttWillConfigDto
{
    public bool Enabled { get; set; } = false;
    public string Topic { get; set; } = string.Empty;
    public string Payload { get; set; } = "{\"status\":\"offline\"}";
    public byte Qos { get; set; } = 1;
    public bool Retain { get; set; } = true;
}
