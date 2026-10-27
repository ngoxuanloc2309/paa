namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO chứa cấu hình tổng thể của phân hệ DTU Gateway (Routing, Serial Mode, Uplink Mode).
/// Phân vùng 0x1680..0x16BF (64 registers = 128 bytes).
/// </summary>
public sealed class DtuConfigDto
{
    public bool Enabled { get; set; } = false;
    public byte PortIndex { get; set; } = 0; // Cổng RS-485 áp dụng (0..3)
    public DtuSerialMode SerialMode { get; set; } = DtuSerialMode.Transference;
    public DtuUplinkMode UplinkMode { get; set; } = DtuUplinkMode.Mqtt;
    
    // Tham số cho Transparent / Passthrough
    public ushort FrameTimeoutDelayMs { get; set; } = 20;
    public ushort MaxPacketSize { get; set; } = 512;

    // Chi tiết phân hệ con
    public DtuHttpConfigDto HttpConfig { get; set; } = new();
    public DtuTcpConfigDto TcpConfig { get; set; } = new();
    public MqttWillConfigDto MqttWillConfig { get; set; } = new();
}
