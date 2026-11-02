namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Chức năng hoạt động của cổng RS-485 trên thiết bị DTU.
/// </summary>
public enum DtuSerialMode : ushort
{
    /// <summary>
    /// Chế độ truyền trong suốt: Toàn bộ byte nhị phân được chuyển tiếp trực tiếp hai chiều.
    /// </summary>
    Transference = 0,

    /// <summary>
    /// Chế độ Modbus RTU Master: DTU chủ động polling thiết bị/cảm biến ngoại vi.
    /// </summary>
    ModbusMaster = 1,

    /// <summary>
    /// Chế độ Modbus RTU Slave: DTU đóng vai trò trạm phụ mở bộ đệm cho SCADA/PLC khác đọc/ghi.
    /// </summary>
    ModbusSlave = 2
}

/// <summary>
/// Chế độ đẩy dữ liệu mạng (Uplink Forwarding Mode) của DTU.
/// </summary>
public enum DtuUplinkMode : ushort
{
    /// <summary>
    /// Không đẩy dữ liệu ra mạng (chỉ xử lý cục bộ).
    /// </summary>
    None = 0,

    /// <summary>
    /// Đẩy dữ liệu lên MQTT(s) Broker.
    /// </summary>
    Mqtt = 1,

    /// <summary>
    /// Đẩy dữ liệu lên HTTP(s) REST API Webhook (POST / PUT).
    /// </summary>
    Http = 2,

    /// <summary>
    /// Kết nối Socket lên TCP Server trung tâm (DTU làm Client).
    /// </summary>
    TcpClient = 3,

    /// <summary>
    /// Mở cổng Listening Socket cục bộ (DTU làm Server).
    /// </summary>
    TcpServer = 4
}

/// <summary>
/// Phương thức HTTP Request khi gửi dữ liệu viễn trắc.
/// </summary>
public enum DtuHttpMethod : ushort
{
    Post = 1,
    Put = 2
}

/// <summary>
/// Chế độ hoạt động Socket TCP của DTU.
/// </summary>
public enum DtuTcpSocketMode : ushort
{
    Client = 0,
    Server = 1
}

/// <summary>
/// Chế độ kích hoạt gửi bản tin HTTP REST của DTU (IETF RFC 9110).
/// </summary>
public enum DtuHttpTriggerMode : ushort
{
    /// <summary>
    /// Bắn HTTP POST/PUT ngay khi nhận xong khung dữ liệu RS-485.
    /// </summary>
    OnDataReceived = 0,

    /// <summary>
    /// Gửi HTTP theo chu kỳ thời gian (giây) độc lập.
    /// </summary>
    Periodic = 1
}

/// <summary>
/// Kiểu gói tin định danh / báo danh qua TCP Socket của DTU (IEC 62443-4-2).
/// </summary>
public enum DtuTcpRegistrationType : ushort
{
    /// <summary>
    /// Không gửi gói tin báo danh.
    /// </summary>
    None = 0,

    /// <summary>
    /// Chỉ gửi gói tin báo danh một lần duy nhất ngay sau khi thiết lập kết nối TCP.
    /// </summary>
    OnConnect = 1,

    /// <summary>
    /// Đính kèm tiền tố báo danh vào trước tất cả các gói tin dữ liệu gửi lên.
    /// </summary>
    PrependToEveryPacket = 2
}

/// <summary>
/// Chế độ định dạng dữ liệu payload chuyển tiếp của DTU (ISO/IEC 21778, ISA-95).
/// </summary>
public enum DtuPayloadMode : ushort
{
    /// <summary>
    /// Định dạng tự động chuẩn IEC/ISA-95 JSON có cấu trúc.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Định dạng JSON tự do theo mẫu template và bộ macro công nghiệp.
    /// </summary>
    Custom = 1,

    /// <summary>
    /// Chuyển tiếp dòng byte thô trong suốt (không bọc JSON).
    /// </summary>
    Raw = 2
}
