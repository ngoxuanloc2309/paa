namespace SimplePLC.Domain.Enums;

/// <summary>
/// Platform TagKind Contract V1 (Nguồn chân lý duy nhất cho phân loại Tag).
/// Giá trị số tương thích 100% với tài liệu đặc tả ban đầu (Spec Mục 3.2).
/// </summary>
public enum TagKind : byte
{
    None = 0,

    DiscreteInput = 1,          // TAG_DI: digital input nội bộ (vật lý)
    DiscreteOutput = 2,         // TAG_DO: digital output nội bộ (vật lý)
    AnalogInput = 3,            // TAG_AI: analog input nội bộ (vật lý)

    VirtualFlag = 4,            // TAG_VFLAG: cờ logic nội bộ (1 bit, boolean)
    VirtualRegister = 5,        // TAG_VREG: thanh ghi số học nội bộ (int32, volatile)

    ModbusCoil = 6,             // TAG_MB_COIL: coil trên thiết bị Modbus khác (remote)
    ModbusHolding = 7,          // TAG_MB_HOLDING: holding register trên thiết bị Modbus khác (remote)

    VirtualRegisterRetain = 8,  // TAG_VREG_RETAIN: thanh ghi lưu giữ sau mất nguồn (int32, non-volatile)
    Counter = 9                 // TAG_COUNTER: bộ đếm nội bộ (int32)
}

/// <summary>
/// Kiểu dữ liệu nghiệp vụ của Tag.
/// </summary>
public enum TagDataType
{
    Boolean = 1,
    Int32 = 2
}
