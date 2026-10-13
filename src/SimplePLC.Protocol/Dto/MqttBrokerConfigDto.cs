namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO chứa thông tin cấu hình kết nối MQTT Broker cấp hệ thống / dự án.
/// Được đóng gói và triển khai xuống MCU tại phân vùng Modbus Holding Registers 0x0E00.
/// </summary>
public sealed class MqttBrokerConfigDto
{
    /// <summary>
    /// Kích hoạt module MQTT Client trên MCU (Bit 0).
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Địa chỉ máy chủ Broker (IP hoặc Domain, tối đa 64 ký tự).
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Cổng kết nối (Mặc định 1883 cho Plain TCP, 8883 cho MQTTS TLS).
    /// </summary>
    public ushort Port { get; set; } = 1883;

    /// <summary>
    /// Sử dụng mã hóa an toàn TLS/SSL (Bit 1).
    /// </summary>
    public bool UseTls { get; set; } = false;

    /// <summary>
    /// Xóa phiên làm việc cũ khi kết nối (Clean Session, Bit 2).
    /// </summary>
    public bool CleanSession { get; set; } = true;

    /// <summary>
    /// Phiên bản giao thức MQTT (0 = v3.1.1, 1 = v5.0, Bits 3..4).
    /// </summary>
    public byte ProtocolVersion { get; set; } = 0;

    /// <summary>
    /// Chu kỳ nhịp tim giữ kết nối (giây).
    /// </summary>
    public ushort KeepAliveSec { get; set; } = 60;

    /// <summary>
    /// Mã định danh trạm Client ID (tối đa 32 ký tự).
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Tên tài khoản đăng nhập (tối đa 32 ký tự, để trống nếu không xác thực).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Mật khẩu đăng nhập (tối đa 32 ký tự).
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
