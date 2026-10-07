namespace SimplePLC.Infrastructure.Simulator;

/// <summary>
/// Đại diện cho phân vùng bộ nhớ không khả biến (Non-Volatile Flash) của MCU STM32.
/// Lưu trữ bền vững bảng Active Rule Table, Version, và CRC.
/// Dữ liệu trong Flash được bảo toàn tuyệt đối qua các chu kỳ khởi động lại (Reboot) hoặc mất nguồn (Power Cycle).
/// </summary>
public sealed class SimulatorFlash
{
    private readonly ushort[] _flashBuffer = new ushort[1600]; // 100 rules * 16 registers

    public ushort StoredRuleCount { get; private set; }
    public ushort StoredVersion { get; private set; } = 0;
    public ushort StoredCrc16 { get; private set; } = 0xFFFF;
    public bool HasCommittedData { get; private set; }

    /// <summary>
    /// Ghi cấu hình Active đã xác thực thành công vào bộ nhớ Flash.
    /// </summary>
    public void Commit(ushort ruleCount, ushort version, ushort crc16, ReadOnlySpan<ushort> ruleRegisters)
    {
        StoredRuleCount = ruleCount;
        StoredVersion = version;
        StoredCrc16 = crc16;

        int regCount = ruleCount * 16;
        ruleRegisters.Slice(0, regCount).CopyTo(_flashBuffer.AsSpan(0, regCount));
        HasCommittedData = true;
    }

    /// <summary>
    /// Tải cấu hình từ Flash vào bộ nhớ RAM khi MCU khởi động lại.
    /// </summary>
    public bool Load(out ushort ruleCount, out ushort version, out ushort crc16, Span<ushort> destination)
    {
        ruleCount = StoredRuleCount;
        version = StoredVersion;
        crc16 = StoredCrc16;

        if (!HasCommittedData)
        {
            return false;
        }

        int regCount = StoredRuleCount * 16;
        if (destination.Length < regCount)
        {
            throw new ArgumentException($"Destination buffer is too small for Flash content ({destination.Length} < {regCount}).", nameof(destination));
        }

        _flashBuffer.AsSpan(0, regCount).CopyTo(destination);
        return true;
    }

    /// <summary>
    /// Xóa trắng cấu hình trong Flash khi thực hiện Factory Reset.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_flashBuffer, 0, _flashBuffer.Length);
        StoredRuleCount = 0;
        StoredVersion = 0;
        StoredCrc16 = 0xFFFF;
        HasCommittedData = false;
    }
}
