namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Device class: family cấp cao của sản phẩm SimplePLC.
/// </summary>
public enum SPLC_DeviceClass : ushort
{
    UNKNOWN = 0,
    REMOTE_IO = 1,
    DATALOGGER = 2,
    GATEWAY = 3,
    CONTROLLER = 4
}

/// <summary>
/// Remote I/O variant: chỉ dùng khi device_class = REMOTE_IO.
/// </summary>
public enum SPLC_RemoteIoVariant : ushort
{
    UNKNOWN = 0,
    VARIANT_8DI_8DO_4AI = 1,
    VARIANT_16DI_16DO = 2
}

/// <summary>
/// Datalogger variant: chỉ dùng khi device_class = DATALOGGER.
/// </summary>
public enum SPLC_DataloggerVariant : ushort
{
    UNKNOWN = 0,
    VARIANT_8AI = 1
}

/// <summary>
/// Gateway variant: chỉ dùng khi device_class = GATEWAY.
/// </summary>
public enum SPLC_GatewayVariant : ushort
{
    UNKNOWN = 0,
    VARIANT_RS485_ETH = 1
}

/// <summary>
/// Nguyên nhân chính của lần reset gần nhất.
/// </summary>
public enum SPLC_ResetReason : ushort
{
    UNKNOWN = 0,
    POWER_ON = 1,
    SOFTWARE = 2,
    WATCHDOG = 3,
    BROWNOUT = 4,
    EXTERNAL = 5
}

/// <summary>
/// Health bitmask: có thể OR nhiều cờ đồng thời.
/// </summary>
[Flags]
public enum SPLC_HealthFlags : ushort
{
    NONE = 0,
    CPU_HIGH = 1 << 0,
    RAM_HIGH = 1 << 1,
    SCAN_OVERRUN = 1 << 2
}

/// <summary>
/// Bitmask năng lực phần cứng do MCU khai báo tại địa chỉ 0x0A08 (32-bit uint32, Big-Endian).
/// </summary>
[Flags]
public enum SPLC_HardwareCapabilities : uint
{
    NONE         = 0x0000,
    BASE_LOGIC   = 0x0001, // Bit 0: Hỗ trợ Logic Engine FBD cơ bản (Rule Engine)
    RETENTIVE    = 0x0002, // Bit 1: Hỗ trợ lưu trữ Retentive Memory (Flash/NVRAM/FRAM)
    RTC          = 0x0004, // Bit 2: Hỗ trợ Real-Time Clock phần cứng (DS3231/LSE)
    ETHERNET     = 0x0008, // Bit 3: Có cổng truyền thông mạng vật lý (Ethernet/WiFi)
    MQTT_PUB     = 0x0010, // Bit 4: Hỗ trợ phân hệ xuất bản viễn trắc MQTT PUB (0x0C00)
    MQTT_SUB     = 0x0020, // Bit 5: Hỗ trợ phân hệ nhận bản tin điều khiển MQTT SUB
    SD_DATALOG   = 0x0040  // Bit 6: Hỗ trợ ghi chép dữ liệu cục bộ qua thẻ nhớ MicroSD
}

/// <summary>
/// Lệnh maintenance cấp hệ thống do App gửi xuống MCU.
/// </summary>
public enum SPLC_SystemCommand : ushort
{
    NONE = 0,
    REBOOT = 1,
    FACTORY_RESET = 2,
    CLEAR_RULES = 3,
    CLEAR_RETAIN = 4
}

/// <summary>
/// Trạng thái thực thi System Command (0x0A01).
/// </summary>
public enum SPLC_CommandStatus : ushort
{
    IDLE = 0,
    ACCEPTED = 1,
    BUSY = 2,
    DONE = 3,
    ERROR = 4
}

/// <summary>
/// Trạng thái Staging & Commit Rule Table tại thanh ghi CONFIG_STATUS (0x9000).
/// </summary>
public enum SPLC_ConfigStatus : ushort
{
    IDLE = 0,
    RECEIVING = 1,
    VERIFYING = 2,
    READY = 3,
    ERROR = 4
}

/// <summary>
/// Mã lỗi chung cho command/config operation giữa App và MCU.
/// </summary>
public enum SPLC_ErrorCode : ushort
{
    NONE = 0,
    INVALID_COMMAND = 1,
    INVALID_PARAMETER = 2,
    BUSY = 3,
    CRC_MISMATCH = 4,
    UNSUPPORTED = 5,
    FLASH = 6
}
