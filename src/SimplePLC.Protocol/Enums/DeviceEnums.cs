namespace SimplePLC.Protocol.Enums;

using SPLC_HardwareCapabilities = SimplePLC.Domain.Enums.SPLC_HardwareCapabilities;
using SPLC_DeviceClass = SimplePLC.Domain.Enums.SPLC_DeviceClass;
using SPLC_RemoteIoVariant = SimplePLC.Domain.Enums.SPLC_RemoteIoVariant;
using SPLC_DataloggerVariant = SimplePLC.Domain.Enums.SPLC_DataloggerVariant;
using SPLC_GatewayVariant = SimplePLC.Domain.Enums.SPLC_GatewayVariant;
using SPLC_DtuVariant = SimplePLC.Domain.Enums.SPLC_DtuVariant;

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
    FLASH = 6,
    SLAVE_PORT_CONFLICT = 7
}


/// <summary>
/// IEC 61131-3 Tag Data Types Contract (Bảng Tag DataType Registry 0x0080..0x00BF).
/// </summary>
public enum SPLC_TagDataType : byte
{
    NONE = 0,
    BOOL = 1,
    INT32 = 2,
    REAL = 3
}

/// <summary>
/// Cờ đặc tính của Rule trong struct SPLC_RuleRecord_t (Byte 13 High).
/// Phân tách trực giao kiểu Float của Trigger và Action để tránh ép kiểu nhầm IEEE-754.
/// </summary>
[Flags]
public enum SPLC_RuleFlags : byte
{
    NONE = 0x00,
    TRIGGER_FLOAT = 0x01, // Bit 0: Ngưỡng Trigger là số thực Float32 IEEE-754
    ACTION_FLOAT = 0x02   // Bit 1: Tham số Action là số thực Float32 IEEE-754
}