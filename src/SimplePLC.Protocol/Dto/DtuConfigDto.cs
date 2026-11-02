namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO chứa cấu hình tổng thể của phân hệ DTU Gateway (Routing, Serial Mode, Uplink Mode, Payload Formatter).
/// Phân vùng 0x1680..0x16BF (64 registers = 128 bytes).
/// Tuân thủ quy chuẩn SPLC-SPEC-DTU-001.
/// </summary>
public sealed class DtuConfigDto
{
    /// <summary>
    /// Trạng thái kích hoạt tổng thể phân hệ DTU.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Chỉ số cổng RS-485 áp dụng (0..3).
    /// </summary>
    public byte PortIndex { get; set; } = 0;

    /// <summary>
    /// Chế độ hoạt động cổng nối tiếp (Transference, ModbusMaster, ModbusSlave).
    /// </summary>
    public DtuSerialMode SerialMode { get; set; } = DtuSerialMode.Transference;

    /// <summary>
    /// Chế độ đẩy dữ liệu mạng Uplink (Mqtt, Http, TcpClient, TcpServer).
    /// </summary>
    public DtuUplinkMode UplinkMode { get; set; } = DtuUplinkMode.Mqtt;
    
    /// <summary>
    /// Thời gian ngắt khung ranh giới gói tin nối tiếp (ms) theo chuẩn t3.5.
    /// </summary>
    public ushort FrameTimeoutDelayMs { get; set; } = 20;

    /// <summary>
    /// Kích thước tối đa bộ đệm gói tin nối tiếp (Bytes).
    /// </summary>
    public ushort MaxPacketSize { get; set; } = 512;

    /// <summary>
    /// Chế độ định dạng dữ liệu chuyển tiếp (Standard, Custom, Raw).
    /// </summary>
    public DtuPayloadMode PayloadMode { get; set; } = DtuPayloadMode.Standard;

    /// <summary>
    /// Mặt nạ bit các trường dữ liệu tùy chọn đính kèm trong chế độ Standard.
    /// </summary>
    public byte PayloadFlags { get; set; } = 0x0F;

    /// <summary>
    /// Cấu hình phân hệ con HTTP Client.
    /// </summary>
    public DtuHttpConfigDto HttpConfig { get; set; } = new();

    /// <summary>
    /// Cấu hình phân hệ con TCP Socket.
    /// </summary>
    public DtuTcpConfigDto TcpConfig { get; set; } = new();

    /// <summary>
    /// Cấu hình phân hệ con MQTT Will (LWT).
    /// </summary>
    public MqttWillConfigDto MqttWillConfig { get; set; } = new();
}
