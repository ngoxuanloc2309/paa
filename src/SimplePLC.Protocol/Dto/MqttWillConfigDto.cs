namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO chứa cấu hình Last Will & Testament (Will Message) của MQTT Client (0x0E60..0x0E7F, 32 registers = 64 bytes).
/// Tuân thủ tiêu chuẩn SPLC-SPEC-MQTT-004.
/// </summary>
public sealed class MqttWillConfigDto
{
    /// <summary>
    /// Cho biết tính năng Last Will & Testament có được kích hoạt hay không (Bit 0 của WILL_CONTROL).
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Chủ đề đăng ký phát tán di chúc khẩn cấp (tối đa 32 ký tự ASCII).
    /// </summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>
    /// Nội dung thông điệp báo sự cố ngoại tuyến (tối đa 30 ký tự ASCII, không chứa dấu " và \).
    /// </summary>
    public string Payload { get; set; } = "offline";

    /// <summary>
    /// Mức chất lượng dịch vụ MQTT Will QoS (0, 1, 2).
    /// </summary>
    public byte Qos { get; set; } = 1;

    /// <summary>
    /// Cờ lưu giữ thông điệp Will Retain trên Broker (Bit 1 của WILL_CONTROL).
    /// </summary>
    public bool Retain { get; set; } = true;
}
