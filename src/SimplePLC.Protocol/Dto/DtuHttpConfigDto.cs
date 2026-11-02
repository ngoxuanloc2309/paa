namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// DTO chứa cấu hình HTTP(s) REST API Client của DTU (0x1600..0x163F, 64 registers = 128 bytes).
/// Tuân thủ quy chuẩn IETF RFC 9110 và SPLC-SPEC-DTU-001.
/// </summary>
public sealed class DtuHttpConfigDto
{
    /// <summary>
    /// Trạng thái kích hoạt phân hệ HTTP Client.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Phương thức HTTP Request (POST / PUT).
    /// </summary>
    public DtuHttpMethod Method { get; set; } = DtuHttpMethod.Post;

    /// <summary>
    /// Thời gian chờ phản hồi tối đa (giây).
    /// </summary>
    public ushort TimeoutSec { get; set; } = 10;

    /// <summary>
    /// Chu kỳ gửi định kỳ (giây) khi chế độ TriggerMode là Periodic.
    /// </summary>
    public ushort PeriodicIntervalSec { get; set; } = 5;

    /// <summary>
    /// Chế độ kích hoạt gửi bản tin (OnDataReceived hoặc Periodic).
    /// </summary>
    public DtuHttpTriggerMode TriggerMode { get; set; } = DtuHttpTriggerMode.OnDataReceived;

    /// <summary>
    /// Địa chỉ Endpoint URL đầy đủ (tối đa 64 ký tự).
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Các tiêu đề HTTP Headers tùy chỉnh (tối đa 56 ký tự).
    /// </summary>
    public string CustomHeaders { get; set; } = "Content-Type: application/json";
}
