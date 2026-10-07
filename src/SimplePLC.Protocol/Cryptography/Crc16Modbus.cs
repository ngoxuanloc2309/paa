namespace SimplePLC.Protocol.Cryptography;

/// <summary>
/// Tính toán kiểm tra mã CRC-16/MODBUS chuẩn (Polynomial 0xA001, Init 0xFFFF).
/// Tuân thủ quy định tính toán CRC cho toàn bộ Rule Table (R3, R4, R8).
/// </summary>
public static class Crc16Modbus
{
    public const ushort InitialValue = 0xFFFF;
    public const ushort Polynomial = 0xA001;

    /// <summary>
    /// Bảng tra cứu CRC16 (Lookup table 256 phần tử) giúp tăng tốc xử lý throughput cao mà không cấp phát bộ nhớ.
    /// </summary>
    private static readonly ushort[] Table = GenerateTable();

    private static ushort[] GenerateTable()
    {
        var table = new ushort[256];
        for (ushort i = 0; i < 256; i++)
        {
            ushort value = i;
            for (int j = 0; j < 8; j++)
            {
                if ((value & 0x0001) != 0)
                {
                    value = (ushort)((value >> 1) ^ Polynomial);
                }
                else
                {
                    value >>= 1;
                }
            }
            table[i] = value;
        }
        return table;
    }

    /// <summary>
    /// Tính CRC-16/MODBUS trên mảng byte (zero allocation với ReadOnlySpan).
    /// </summary>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = InitialValue;
        for (int i = 0; i < data.Length; i++)
        {
            byte index = (byte)(crc ^ data[i]);
            crc = (ushort)((crc >> 8) ^ Table[index]);
        }
        return crc;
    }

    /// <summary>
    /// Tính CRC-16/MODBUS trực tiếp từ mảng thanh ghi Modbus (16-bit registers).
    /// Mỗi thanh ghi được giải nén theo thứ tự Big-Endian: High Byte trước, Low Byte sau.
    /// Hoàn toàn không cấp phát mảng byte trung gian (Zero Memory Allocation).
    /// </summary>
    public static ushort ComputeFromRegisters(ReadOnlySpan<ushort> registers)
    {
        ushort crc = InitialValue;
        for (int i = 0; i < registers.Length; i++)
        {
            ushort reg = registers[i];
            byte highByte = (byte)(reg >> 8);
            byte lowByte = (byte)(reg & 0xFF);

            // High byte trước
            byte idx1 = (byte)(crc ^ highByte);
            crc = (ushort)((crc >> 8) ^ Table[idx1]);

            // Low byte sau
            byte idx2 = (byte)(crc ^ lowByte);
            crc = (ushort)((crc >> 8) ^ Table[idx2]);
        }
        return crc;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của toàn bộ khung Modbus RTU bao gồm 2 byte CRC ở cuối (Little-Endian: CRC_Lo trước, CRC_Hi sau).
    /// </summary>
    public static bool Verify(ReadOnlySpan<byte> frameWithCrc)
    {
        if (frameWithCrc.Length < 3) return false;
        ushort expected = Compute(frameWithCrc[..^2]);
        ushort actual = (ushort)(frameWithCrc[^2] | (frameWithCrc[^1] << 8));
        return expected == actual;
    }
}
