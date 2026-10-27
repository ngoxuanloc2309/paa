namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO chứa cấu hình TCP Socket Client / Server của DTU (0x1640..0x167F, 64 registers = 128 bytes).
/// </summary>
public sealed class DtuTcpConfigDto
{
    public bool Enabled { get; set; } = false;
    public DtuTcpSocketMode SocketMode { get; set; } = DtuTcpSocketMode.Client;
    public string RemoteHost { get; set; } = string.Empty;
    public ushort RemotePort { get; set; } = 8888;
    public ushort LocalPort { get; set; } = 8888;
    public ushort ReconnectDelaySec { get; set; } = 5;
    public ushort HeartbeatIntervalSec { get; set; } = 30;
    public string HeartbeatPayload { get; set; } = string.Empty;
}
