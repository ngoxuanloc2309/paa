namespace SimplePLC.Domain.Models;

/// <summary>
/// Mô tả hồ sơ tài nguyên của thiết bị theo Wire Profile V1 (Contract V1.9).
/// </summary>
public sealed record ProductResourceProfile(
    ushort DigitalInputs,
    ushort DigitalOutputs,
    ushort AnalogInputs,
    ushort VirtualFlags,
    ushort VirtualRegisters,
    ushort RetentiveRegisters,
    ushort Counters)
{
    /// <summary>
    /// Tổng số tag thực tế được khai báo.
    /// </summary>
    public int TotalTags =>
        DigitalInputs +
        DigitalOutputs +
        AnalogInputs +
        VirtualFlags +
        VirtualRegisters +
        RetentiveRegisters +
        Counters;

    /// <summary>
    /// Tạo profile mặc định của Remote IO V1 (8DI, 8DO, 4AI, 32VFLAG, 32VREG, 32RETAIN, 8COUNTER = 124 tags).
    /// </summary>
    public static ProductResourceProfile DefaultRemoteIo => new(
        DigitalInputs: 8,
        DigitalOutputs: 8,
        AnalogInputs: 4,
        VirtualFlags: 32,
        VirtualRegisters: 32,
        RetentiveRegisters: 32,
        Counters: 8);
}
