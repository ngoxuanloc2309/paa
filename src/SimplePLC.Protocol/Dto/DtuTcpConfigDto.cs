namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO chứa cấu hình TCP Socket Client / Server của DTU (0x1640..0x167F, 64 registers = 128 bytes).
/// Tuân thủ quy chuẩn IEC 62443-4-2 và SPLC-SPEC-DTU-001.
/// </summary>
public sealed class DtuTcpConfigDto
{
    /// <summary>
    /// Trạng thái kích hoạt kết nối Socket TCP.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Vai trò kết nối Socket (Client hoặc Server).
    /// </summary>
    public DtuTcpSocketMode SocketMode { get; set; } = DtuTcpSocketMode.Client;

    /// <summary>
    /// Tên miền hoặc địa chỉ IP máy chủ từ xa (khi ở chế độ Client).
    /// </summary>
    public string RemoteHost { get; set; } = string.Empty;

    /// <summary>
    /// Cổng kết nối máy chủ từ xa.
    /// </summary>
    public ushort RemotePort { get; set; } = 8888;

    /// <summary>
    /// Cổng lắng nghe cục bộ (khi ở chế độ Server).
    /// </summary>
    public ushort LocalPort { get; set; } = 8888;

    /// <summary>
    /// Thời gian chờ thử lại kết nối khi socket bị ngắt (giây).
    /// </summary>
    public ushort ReconnectDelaySec { get; set; } = 5;

    /// <summary>
    /// Chu kỳ gửi gói tin giữ phiên Heartbeat PING (giây).
    /// </summary>
    public ushort HeartbeatIntervalSec { get; set; } = 30;

    /// <summary>
    /// Nội dung tải trọng gói tin Heartbeat PING.
    /// </summary>
    public string HeartbeatPayload { get; set; } = string.Empty;

    /// <summary>
    /// Kiểu gói tin định danh / báo danh (None, OnConnect, PrependToEveryPacket).
    /// </summary>
    public DtuTcpRegistrationType RegistrationType { get; set; } = DtuTcpRegistrationType.None;

    /// <summary>
    /// Nội dung tải trọng gói tin định danh / báo danh (tối đa 48 ký tự).
    /// </summary>
    public string RegistrationPayload { get; set; } = string.Empty;
}
