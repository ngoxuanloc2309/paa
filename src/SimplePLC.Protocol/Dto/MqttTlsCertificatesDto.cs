namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO chứa dữ liệu nhị phân hoặc chuỗi PEM của bộ chứng chỉ bảo mật MQTT TLS / mTLS (SPLC-SPEC-MQTT-003).
/// Được cung cấp từ ProjectMetadata khi tiến hành Deploy xuống thiết bị vi điều khiển PLC.
/// </summary>
public sealed class MqttTlsCertificatesDto
{
    /// <summary>
    /// Nội dung byte của chứng chỉ máy chủ Server Root CA (cert_type = 0x01).
    /// </summary>
    public byte[]? RootCaBytes { get; set; }

    /// <summary>
    /// Nội dung byte của chứng chỉ máy trạm Client Certificate (mTLS, cert_type = 0x02).
    /// </summary>
    public byte[]? ClientCertBytes { get; set; }

    /// <summary>
    /// Nội dung byte của khóa bí mật Client Private Key (mTLS, cert_type = 0x03, định dạng raw PKCS#1 / PKCS#8).
    /// </summary>
    public byte[]? ClientKeyBytes { get; set; }

    /// <summary>
    /// Kiểm tra xem có bất kỳ dữ liệu chứng chỉ nào cần nạp hay không.
    /// </summary>
    public bool HasAnyCertificates =>
        (RootCaBytes != null && RootCaBytes.Length > 0) ||
        (ClientCertBytes != null && ClientCertBytes.Length > 0) ||
        (ClientKeyBytes != null && ClientKeyBytes.Length > 0);
}
