namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO chứa một khối dữ liệu phân mảnh (Chunk Struct) của chứng chỉ bảo mật TLS/SSL.
/// Kích thước đóng gói chuẩn: 128 bytes = 64 thanh ghi Modbus (8 bytes Header + 120 bytes Payload).
/// Tuân thủ chuẩn SPLC-SPEC-MQTT-003.
/// </summary>
public sealed class TlsCertChunkDto
{
    /// <summary>
    /// Loại chứng chỉ: 0x01: Root CA, 0x02: Client Cert, 0x03: Client Key.
    /// </summary>
    public byte CertType { get; set; } = 0x01;

    /// <summary>
    /// Chỉ số khối (0-based): 0, 1, 2, ... (TotalChunks - 1).
    /// </summary>
    public byte ChunkIndex { get; set; }

    /// <summary>
    /// Tổng số khối cần nạp (TotalChunks &lt;= 32).
    /// </summary>
    public byte TotalChunks { get; set; } = 1;

    /// <summary>
    /// Độ dài dữ liệu thực của khối này (1..120 bytes).
    /// </summary>
    public byte ChunkLen { get; set; }

    /// <summary>
    /// Tổng kích thước của toàn bộ tệp chứng chỉ (bytes).
    /// </summary>
    public ushort TotalCertSize { get; set; }

    /// <summary>
    /// Checksum CRC-16 (Modbus polynomial 0xA001) của riêng Payload khối này.
    /// </summary>
    public ushort ChunkCrc16 { get; set; }

    /// <summary>
    /// Dữ liệu byte của khối (tối đa 120 bytes).
    /// </summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}
