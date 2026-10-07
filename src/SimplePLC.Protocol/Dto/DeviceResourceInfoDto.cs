namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn 10 thanh ghi (20 bytes) của SPLC_DeviceResourceInfo đọc tại địa chỉ 0x0020 theo Contract V1.9.
/// Thuần túy phản ánh dữ liệu wire-level do MCU khai báo, không chứa logic nghiệp vụ (business semantics).
/// Các năng lực nghiệp vụ (HasRuleEngine, HasRetentiveMemory) thuộc sở hữu của ProductDefinition trong Domain.
/// </summary>
public sealed class DeviceResourceInfoDto
{
    public ushort WireProfile { get; set; } = 1;
    public ushort MaxRules { get; set; }
    public ushort RuntimeTagCount { get; set; }

    public ushort DigitalInputCount { get; set; }
    public ushort DigitalOutputCount { get; set; }
    public ushort AnalogInputCount { get; set; }

    public ushort VirtualFlagCount { get; set; }
    public ushort VirtualRegisterCount { get; set; }
    public ushort RetentiveRegisterCount { get; set; }
    public ushort CounterCount { get; set; }

    /// <summary>
    /// Tổng số tag thực tế được khai báo bởi các nhóm tài nguyên (Wire-level checksum).
    /// </summary>
    public int TotalDeclaredTags =>
        DigitalInputCount +
        DigitalOutputCount +
        AnalogInputCount +
        VirtualFlagCount +
        VirtualRegisterCount +
        RetentiveRegisterCount +
        CounterCount;

    /// <summary>
    /// Kiểm tra tính nhất quán toán học giữa RuntimeTagCount và tổng số tag các nhóm khai báo.
    /// </summary>
    public bool IsValidDeclaredCount => RuntimeTagCount == TotalDeclaredTags;

    /// <summary>
    /// Tạo cấu hình chuẩn của Remote IO V1 (8DI / 8DO / 4AI / 32VFLAG / 32VREG / 32RETAIN / 8COUNTER = 124 tags).
    /// Dùng cho test harness và khởi tạo simulator mặc định.
    /// </summary>
    public static DeviceResourceInfoDto CreateRemoteIo8Di8Do4Ai() => new()
    {
        WireProfile = 1,
        MaxRules = 100,
        RuntimeTagCount = 124,
        DigitalInputCount = 8,
        DigitalOutputCount = 8,
        AnalogInputCount = 4,
        VirtualFlagCount = 32,
        VirtualRegisterCount = 32,
        RetentiveRegisterCount = 32,
        CounterCount = 8
    };
}
