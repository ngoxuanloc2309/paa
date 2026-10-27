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
