namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO chứa cấu hình HTTP(s) REST API Client của DTU (0x1600..0x163F, 64 registers = 128 bytes).
/// </summary>
public sealed class DtuHttpConfigDto
{
    public bool Enabled { get; set; } = false;
    public DtuHttpMethod Method { get; set; } = DtuHttpMethod.Post;
    public ushort TimeoutSec { get; set; } = 10;
    public ushort PeriodicIntervalSec { get; set; } = 5;
    public string Url { get; set; } = string.Empty;
    public string CustomHeaders { get; set; } = "Content-Type: application/json";
}
