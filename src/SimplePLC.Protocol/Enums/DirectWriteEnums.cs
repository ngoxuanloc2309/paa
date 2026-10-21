namespace SimplePLC.Protocol.Enums;

/// <summary>
/// Danh sách chân ngõ ra số vật lý (Digital Output - DO) của thiết bị PLC.
/// </summary>
public enum DirectWriteTargetPin : byte
{
    Do0 = 0,
    Do1 = 1,
    Do2 = 2,
    Do3 = 3,
    Do4 = 4,
    Do5 = 5,
    Do6 = 6,
    Do7 = 7
}

/// <summary>
/// Chế độ hoạt động của cọc ngõ ra vật lý trực tiếp.
/// </summary>
public enum DirectWriteMode : byte
{
    /// <summary>
    /// Ngõ ra số logic trực tiếp 0/1 (DO: Rơ-le, van từ, còi, đèn báo ON/OFF).
    /// </summary>
    DigitalOutput = 0,

    /// <summary>
    /// Điều chế độ rộng xung công suất (PWM Duty Cycle 0..100%: Điều tốc động cơ, van tỉ lệ, độ sáng).
    /// </summary>
    PwmDuty = 1,

    /// <summary>
    /// Phát chuỗi xung bước định lượng (Pulse Train Output - PTO cho Stepper / Servo / Đo lường).
    /// </summary>
    PulseTrain = 2,

    /// <summary>
    /// Cơ cấu chấp hành định địa chỉ nối tiếp (Addressable Actuator Bus: Dải LED thông minh, cụm van số 1 dây).
    /// </summary>
    AddressableBus = 3
}

/// <summary>
/// Chế độ kích hoạt xuất dữ liệu của khối DIRECT_WRITE.
/// </summary>
public enum DirectWriteTriggerMode : byte
{
    /// <summary>
    /// Kích hoạt khi phát hiện xung sườn lên tại chân Trig (Khuyên dùng cho Event / Xung).
    /// </summary>
    OnTriggerRisingEdge = 0,

    /// <summary>
    /// Xuất liên tục theo chu kỳ quét PLC (Continuous Cyclic).
    /// </summary>
    Continuous = 1,

    /// <summary>
    /// Kích hoạt khi giá trị thay đổi vượt ngưỡng Deadband (COV — Change-of-Value).
    /// Ngăn chặn bão hòa bus RS485 do nhiễu ADC liên tục. Deadband được cấu hình trong Inspector.
    /// </summary>
    OnChangeOfValue = 2
}
