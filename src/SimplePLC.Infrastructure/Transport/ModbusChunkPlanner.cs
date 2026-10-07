namespace SimplePLC.Infrastructure.Transport;

/// <summary>
/// Bộ lập kế hoạch chia nhỏ frame đọc/ghi Modbus (Chunking) để tránh tràn bộ đệm PDU vi điều khiển.
/// Mặc định: 16 thanh ghi / request (tương ứng đúng 1 rule 32-byte, tối ưu ghi Flash & atomic boundary STM32H5).
/// </summary>
public static class ModbusChunkPlanner
{
    public const int DefaultMaxChunkSize = 16;

    /// <summary>
    /// Lập kế hoạch đọc dải thanh ghi thành các đoạn chunk nhỏ an toàn.
    /// </summary>
    public static IReadOnlyList<(ushort Address, ushort Count)> PlanReadChunks(
        ushort startAddress,
        ushort totalCount,
        int maxChunkSize = DefaultMaxChunkSize)
    {
        if (maxChunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChunkSize), "Max chunk size must be greater than 0.");

        var chunks = new List<(ushort Address, ushort Count)>();
        ushort currentAddress = startAddress;
        ushort remaining = totalCount;

        while (remaining > 0)
        {
            ushort count = (ushort)Math.Min((int)remaining, maxChunkSize);
            chunks.Add((currentAddress, count));
            currentAddress = (ushort)(currentAddress + count);
            remaining = (ushort)(remaining - count);
        }

        return chunks;
    }

    /// <summary>
    /// Lập kế hoạch ghi mảng dữ liệu thành các đoạn chunk nhỏ an toàn.
    /// </summary>
    public static IReadOnlyList<(ushort Address, ReadOnlyMemory<T> Chunk)> PlanWriteChunks<T>(
        ushort startAddress,
        ReadOnlyMemory<T> data,
        int maxChunkSize = DefaultMaxChunkSize)
    {
        if (maxChunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxChunkSize), "Max chunk size must be greater than 0.");

        var chunks = new List<(ushort Address, ReadOnlyMemory<T> Chunk)>();
        ushort currentAddress = startAddress;
        int remaining = data.Length;
        int offset = 0;

        while (remaining > 0)
        {
            int count = Math.Min(remaining, maxChunkSize);
            chunks.Add((currentAddress, data.Slice(offset, count)));
            currentAddress = (ushort)(currentAddress + count);
            offset += count;
            remaining -= count;
        }

        return chunks;
    }
}
