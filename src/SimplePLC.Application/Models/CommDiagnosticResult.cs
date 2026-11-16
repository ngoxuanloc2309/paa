namespace SimplePLC.Application.Models;

/// <summary>
/// Phân loại nguyên nhân gốc của trạng thái truyền thông (Root Cause Category).
/// </summary>
public enum CommRootCauseCategory
{
    /// <summary>
    /// Đường truyền thông suốt, không có lỗi.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// Thiết bị hoạt động độc lập (Offline / Standalone) không có trạm chủ hoặc broker kết nối.
    /// </summary>
    OfflineStandalone = 1,

    /// <summary>
    /// Quá thời gian chờ gói tin Modbus từ trạm chủ (quá 3.0s không có request hợp lệ).
    /// </summary>
    ModbusTimeout = 2,

    /// <summary>
    /// Không tìm thấy điểm truy cập Wi-Fi (SSID Not Found / Router tắt / Ngoài vùng phủ sóng).
    /// </summary>
    WifiSsidNotFound = 3,

    /// <summary>
    /// Sai mật khẩu hoặc phương thức mã hóa Wi-Fi (Authentication Failed).
    /// </summary>
    WifiAuthFailed = 4,

    /// <summary>
    /// Wi-Fi bị ngắt kết nối (Disconnected).
    /// </summary>
    WifiDisconnected = 5,

    /// <summary>
    /// Cường độ tín hiệu Wi-Fi quá yếu (RSSI &lt; -80 dBm).
    /// </summary>
    WifiLowSignal = 6,

    /// <summary>
    /// Lỗi phân giải tên miền Broker MQTT (DNS Failed / Router mất kết nối Internet).
    /// </summary>
    MqttDnsFailed = 7,

    /// <summary>
    /// Không thể thiết lập kết nối TCP tới Broker MQTT (Cổng 1883/8883 bị chặn hoặc Broker tắt).
    /// </summary>
    MqttTcpConnectFailed = 8,

    /// <summary>
    /// Lỗi bắt tay bảo mật TLS/SSL với Broker MQTT (Chứng chỉ hết hạn hoặc không khớp).
    /// </summary>
    MqttTlsHandshakeFailed = 9,

    /// <summary>
    /// Broker MQTT từ chối thông tin xác thực (Username hoặc Password không đúng).
    /// </summary>
    MqttAuthRejected = 10,

    /// <summary>
    /// Mất phản hồi Ping / Keepalive từ Broker MQTT.
    /// </summary>
    MqttTimeout = 11,

    /// <summary>
    /// Nguyên nhân chưa xác định.
    /// </summary>
    Unknown = 12
}

/// <summary>
/// Kết quả chẩn đoán và phân tích nguyên nhân gốc của liên kết truyền thông thiết bị.
/// </summary>
public sealed record CommDiagnosticResult
{
    /// <summary>
    /// Trạng thái truyền thông có khỏe mạnh hay không (khớp với bit SYS_COMM_OK).
    /// </summary>
    public bool IsHealthy { get; init; }

    /// <summary>
    /// Phân loại nguyên nhân gốc.
    /// </summary>
    public CommRootCauseCategory Category { get; init; } = CommRootCauseCategory.Unknown;

    /// <summary>
    /// Mã lỗi định danh chuẩn đoán (Diagnostic Code).
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Thông điệp giải thích nguyên nhân bằng tiếng Anh.
    /// </summary>
    public string MessageEn { get; init; } = string.Empty;

    /// <summary>
    /// Thông điệp giải thích nguyên nhân bằng tiếng Việt.
    /// </summary>
    public string MessageVi { get; init; } = string.Empty;

    /// <summary>
    /// Hướng dẫn khuyến nghị khắc phục cho kỹ sư vận hành (tiếng Việt).
    /// </summary>
    public string RecommendationVi { get; init; } = string.Empty;

    /// <summary>
    /// Khởi tạo một đối tượng kết quả chẩn đoán truyền thông.
    /// </summary>
    /// <param name="isHealthy">Trạng thái truyền thông khỏe mạnh.</param>
    /// <param name="category">Phân loại nguyên nhân.</param>
    /// <param name="code">Mã lỗi định danh.</param>
    /// <param name="messageEn">Thông điệp tiếng Anh.</param>
    /// <param name="messageVi">Thông điệp tiếng Việt.</param>
    /// <param name="recommendationVi">Khuyến nghị khắc phục.</param>
    public CommDiagnosticResult(
        bool isHealthy,
        CommRootCauseCategory category,
        string code,
        string messageEn,
        string messageVi,
        string recommendationVi)
    {
        IsHealthy = isHealthy;
        Category = category;
        Code = code;
        MessageEn = messageEn;
        MessageVi = messageVi;
        RecommendationVi = recommendationVi;
    }
}
