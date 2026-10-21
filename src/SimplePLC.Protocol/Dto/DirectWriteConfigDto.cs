namespace SimplePLC.Protocol.Dto;

using SimplePLC.Protocol.Enums;

/// <summary>
/// Data Transfer Object chứa cấu hình biên dịch của khối DIRECT_WRITE (Xuất lệnh chấp hành trực tiếp).
/// </summary>
public sealed class DirectWriteConfigDto
{
    /// <summary>
    /// Định danh duy nhất của khối trong sơ đồ (Node ID).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Chân ngõ ra số vật lý mục tiêu trên bo điều khiển (DO0..DO7).
    /// </summary>
    public DirectWriteTargetPin TargetPin { get; set; } = DirectWriteTargetPin.Do0;

    /// <summary>
    /// Chế độ xuất dữ liệu (DigitalOutput, PwmDuty, PulseTrain, AddressableBus).
    /// </summary>
    public DirectWriteMode Mode { get; set; } = DirectWriteMode.DigitalOutput;

    /// <summary>
    /// Tổng số kênh trong chế độ Channel-Indexed (Ví dụ: 18 ô chia chọn).
    /// </summary>
    public ushort ChannelCount { get; set; } = 18;

    /// <summary>
    /// Cờ độc quyền kênh: Khi bật, chỉ 1 kênh duy nhất được tích cực, các kênh khác tự động tắt.
    /// </summary>
    public bool IsExclusiveChannel { get; set; } = true;

    /// <summary>
    /// Độ rộng xung phát ra (tính bằng micro-giây, dùng cho chế độ PulseTrain / Timing).
    /// </summary>
    public uint PulseWidthUs { get; set; } = 50;

    /// <summary>
    /// Tần số PWM (Hz) cho chế độ PwmDuty.
    /// </summary>
    public uint PwmFrequencyHz { get; set; } = 1000;

    /// <summary>
    /// Chế độ kích hoạt thực thi (Sườn lên Trig hoặc liên tục).
    /// </summary>
    public DirectWriteTriggerMode TriggerMode { get; set; } = DirectWriteTriggerMode.OnTriggerRisingEdge;

    /// <summary>
    /// Thời gian chờ hết hạn xử lý (Timeout tính bằng ms).
    /// </summary>
    public uint TimeoutMs { get; set; } = 300;

    /// <summary>
    /// Tag ngõ vào cung cấp giá trị kênh (Channel Index Tag, ví dụ VREG0).
    /// </summary>
    public ushort? ChannelTagIndex { get; set; }

    /// <summary>
    /// Tag ngõ vào cung cấp tham số bổ sung (Mã màu RGB / Duty / Parameter Tag, ví dụ VREG1).
    /// </summary>
    public ushort? ParamTagIndex { get; set; }

    /// <summary>
    /// Mã màu RGB mặc định nếu chân Param không được đấu dây (Ví dụ: 0x00FF00 = Xanh lục).
    /// </summary>
    public uint DefaultColorRgb { get; set; } = 0x00FF00;

    /// <summary>
    /// Ngưỡng thay đổi COV (Change-of-Value Deadband). Chỉ áp dụng khi TriggerMode = OnChangeOfValue.
    /// Giá trị thay đổi nhỏ hơn ngưỡng này sẽ không kích hoạt ghi Modbus, ngăn chặn bão hòa bus RS485.
    /// Ví dụ: 0.5 có nghĩa là chỉ ghi khi giá trị thay đổi ≥ 0.5 đơn vị.
    /// </summary>
    public float Deadband { get; set; } = 0.5f;
}
