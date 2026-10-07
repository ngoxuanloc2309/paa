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
