namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Kiểu dữ liệu hỗ trợ cho các chân động (Dynamic Pins) của khối MQTT IIoT.
/// </summary>
public enum MqttDataType
{
    Bool,    // Kiểu logic (hỗ trợ true/false, 1/0, "TRUE")
    Int16,   // Số nguyên 16-bit (-32768 .. 32767)
    Int32,   // Số nguyên 32-bit DINT
    Float,   // Số thực REAL (32-bit IEEE 754)
    String   // Chuỗi ký tự
}

/// <summary>
/// Chế độ xử lý hàng đợi tin nhắn nhận được từ MQTT Broker.
/// </summary>
public enum MqttQueueMode
{
    /// <summary>
    /// Xử lý tuần tự từng tin nhắn theo thứ tự đến (mỗi chu kỳ scan xử lý 1 tin).
    /// Chuẩn cho các sự kiện phân loại hàng (Sort-to-Light) để không bị nuốt đơn khi gửi dồn dập.
    /// </summary>
    Fifo,

    /// <summary>
    /// Chỉ lấy tin nhắn mới nhất trong chu kỳ scan, hủy bỏ các tin cũ.
    /// Chuẩn cho tín hiệu đo lường, nhiệt độ, áp suất, tốc độ đặt.
    /// </summary>
    LatestWins
}

/// <summary>
/// Hành vi an toàn (Fail-safe) khi mất kết nối Broker hoặc hết thời gian chờ Timeout.
/// </summary>
public enum MqttFailSafeAction
{
    /// <summary>
    /// Giữ nguyên giá trị nhận được gần nhất (0).
    /// </summary>
    KeepLastValue = 0,

    /// <summary>
    /// Đặt lại toàn bộ giá trị chân ngõ ra về 0 (1).
    /// </summary>
    ResetToZero = 1,

    /// <summary>
    /// Rơi về giá trị mặc định an toàn DefaultValue (2).
    /// </summary>
    ResetToDefault = 2
}

/// <summary>
/// Danh mục mã lỗi chuẩn hóa cho phân hệ MQTT Subscribe phục vụ chẩn đoán kỹ thuật.
/// </summary>
public enum MqttErrorCode : ushort
{
    None = 0,               // Hoạt động bình thường (OK)
    ConnectionLost = 1,     // Mất kết nối tới Broker
    JsonSyntaxError = 2,    // Payload không đúng chuẩn JSON
    MissingField = 3,       // Thiếu trường bắt buộc trong gói tin
    TypeMismatch = 4,       // Không thể ép kiểu dữ liệu
    OutOfRange = 5,         // Giá trị vượt dải Min / Max cho phép
    QueueOverflow = 6,      // Hàng đợi FIFO bị đầy (tin đến quá dồn dập)
    TimeoutExpired = 7      // Quá thời gian Timeout không nhận được tin mới
}
