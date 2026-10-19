using System;
using System.Globalization;
using System.Text.RegularExpressions;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Bộ giải mã dữ liệu thanh ghi Modbus (Data Parser) chuẩn công nghiệp.
/// Hỗ trợ bóc tách kiểu dữ liệu Bool, Int16, UInt16, Int32, UInt32, Real (IEEE 754)
/// và xử lý 4 biến thể Endianness (ABCD, CDAB, BADC, DCBA) kèm hệ số tỉ lệ Multiplier.
/// </summary>
public static class ModbusMasterDataParser
{
    /// <summary>
    /// Bóc tách một giá trị đo lường từ mảng thanh ghi phản hồi.
    /// </summary>
    /// <param name="responseRegisters">Dữ liệu các thanh ghi đọc được từ thiết bị Slave</param>
    /// <param name="regOffset">Độ lệch thanh ghi tương đối so với StartAddress</param>
    /// <param name="dataType">Kiểu dữ liệu cần giải mã</param>
    /// <param name="endianness">Thứ tự Byte / Word</param>
    /// <param name="multiplier">Hệ số nhân tỉ lệ</param>
    /// <param name="parsedValue">Giá trị giải mã cuối cùng</param>
    /// <returns>True nếu giải mã thành công, False nếu thiếu dữ liệu thanh ghi</returns>
    public static bool TryParsePinValue(
        ReadOnlySpan<ushort> responseRegisters,
        ushort regOffset,
        ModbusDataType dataType,
        ModbusEndianness endianness,
        float multiplier,
        out double parsedValue)
    {
        parsedValue = 0.0;
        int requiredRegs = (dataType is ModbusDataType.Int32 or ModbusDataType.UInt32 or ModbusDataType.Real) ? 2 : 1;

        if (regOffset + requiredRegs > responseRegisters.Length)
        {
            return false;
        }

        float scale = multiplier == 0.0f ? 1.0f : multiplier;

        switch (dataType)
        {
            case ModbusDataType.Bool:
                ushort boolReg = responseRegisters[regOffset];
                parsedValue = (boolReg != 0) ? 1.0 : 0.0;
                return true;

            case ModbusDataType.Int16:
                short signed16 = (short)responseRegisters[regOffset];
                if (endianness == ModbusEndianness.ByteSwap_BADC || endianness == ModbusEndianness.LittleEndian_DCBA)
                {
                    signed16 = (short)SwapBytes(responseRegisters[regOffset]);
                }
                parsedValue = signed16 * scale;
                return true;

            case ModbusDataType.UInt16:
                ushort unsigned16 = responseRegisters[regOffset];
                if (endianness == ModbusEndianness.ByteSwap_BADC || endianness == ModbusEndianness.LittleEndian_DCBA)
                {
                    unsigned16 = SwapBytes(unsigned16);
                }
                parsedValue = unsigned16 * scale;
                return true;

            case ModbusDataType.Int32:
                uint rawInt32 = Decode32BitWord(responseRegisters[regOffset], responseRegisters[regOffset + 1], endianness);
                parsedValue = (int)rawInt32 * (double)scale;
                return true;

            case ModbusDataType.UInt32:
                uint rawUInt32 = Decode32BitWord(responseRegisters[regOffset], responseRegisters[regOffset + 1], endianness);
                parsedValue = rawUInt32 * (double)scale;
                return true;

            case ModbusDataType.Real:
                uint rawFloatBits = Decode32BitWord(responseRegisters[regOffset], responseRegisters[regOffset + 1], endianness);
                float floatVal = BitConverter.UInt32BitsToSingle(rawFloatBits);
                if (float.IsNaN(floatVal) || float.IsInfinity(floatVal))
                {
                    parsedValue = 0.0;
                    return false;
                }
                parsedValue = floatVal * scale;
                return true;

            default:
                parsedValue = responseRegisters[regOffset] * scale;
                return true;
        }
    }

    /// <summary>
    /// Ghép và đổi thứ tự Byte / Word của 2 thanh ghi 16-bit thành 1 từ 32-bit.
    /// </summary>
    public static uint Decode32BitWord(ushort reg0, ushort reg1, ModbusEndianness endianness)
    {
        byte b0 = (byte)(reg0 >> 8);   // Byte 0 (High byte của Reg 0)
        byte b1 = (byte)(reg0 & 0xFF); // Byte 1 (Low byte của Reg 0)
        byte b2 = (byte)(reg1 >> 8);   // Byte 2 (High byte của Reg 1)
        byte b3 = (byte)(reg1 & 0xFF); // Byte 3 (Low byte của Reg 1)

        return endianness switch
        {
            // Big-Endian chuẩn (High word first, High byte first): [B0, B1, B2, B3]
            ModbusEndianness.BigEndian_ABCD => ((uint)b0 << 24) | ((uint)b1 << 16) | ((uint)b2 << 8) | b3,

            // Mid-Big / Word-Swap (Low word first, High byte first): [B2, B3, B0, B1]
            ModbusEndianness.MidBig_CDAB => ((uint)b2 << 24) | ((uint)b3 << 16) | ((uint)b0 << 8) | b1,

            // Byte-Swap (High word first, Low byte first): [B1, B0, B3, B2]
            ModbusEndianness.ByteSwap_BADC => ((uint)b1 << 24) | ((uint)b0 << 16) | ((uint)b3 << 8) | b2,

            // Little-Endian thuần (Low word first, Low byte first): [B3, B2, B1, B0]
            ModbusEndianness.LittleEndian_DCBA => ((uint)b3 << 24) | ((uint)b2 << 16) | ((uint)b1 << 8) | b0,

            _ => ((uint)b0 << 24) | ((uint)b1 << 16) | ((uint)b2 << 8) | b3
        };
    }

    public static ushort SwapBytes(ushort value)
    {
        return (ushort)((value >> 8) | (value << 8));
    }

    /// <summary>
    /// Phân tích chuỗi Hex người dùng nhập (ví dụ: "01 03 04 01 19 02 8A" hoặc "01 19 02 8A")
    /// thành danh sách thanh ghi ushort 16-bit phục vụ kiểm thử trực tiếp trên Studio UI.
    /// Tự động loại bỏ phần header nếu người dùng dán nguyên khung Modbus RTU response (Slave + FC + ByteCount).
    /// </summary>
    public static bool TryParseHexToRegisters(string hexString, out ushort[] registers, out string error)
    {
        registers = Array.Empty<ushort>();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(hexString))
        {
            error = "Hex string is empty.";
            return false;
        }

        // Loại bỏ tiền tố 0x, khoảng trắng, dấu phẩy, dấu gạch nối
        string cleanHex = Regex.Replace(hexString, @"(?i)0x", "");
        cleanHex = Regex.Replace(cleanHex, @"[\s,\-_:]", "");

        if (cleanHex.Length % 2 != 0)
        {
            error = "Hex string must have an even number of hex characters.";
            return false;
        }

        int byteCount = cleanHex.Length / 2;
        byte[] bytes = new byte[byteCount];
        for (int i = 0; i < byteCount; i++)
        {
            if (!byte.TryParse(cleanHex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bytes[i]))
            {
                error = $"Invalid hex character at byte position {i}.";
                return false;
            }
        }

        // Tự động nhận diện khung Modbus RTU Response:
        // [SlaveID] [FC] [ByteCount N] [Data N bytes] [CRC_Lo] [CRC_Hi]
        // Nếu byte thứ 1 là FC (0x01..0x04) và byte thứ 2 là ByteCount đúng với độ dài payload
        int dataStart = 0;
        int dataLength = byteCount;

        if (byteCount >= 5 && bytes[1] >= 1 && bytes[1] <= 4 && bytes[2] == byteCount - 5)
        {
            // Bỏ 3 bytes header (Slave, FC, ByteCount) và 2 bytes CRC đuôi
            dataStart = 3;
            dataLength = bytes[2];
        }
        else if (byteCount >= 3 && bytes[1] >= 1 && bytes[1] <= 4 && bytes[2] == byteCount - 3)
        {
            // Khung không có CRC
            dataStart = 3;
            dataLength = bytes[2];
        }

        if (dataLength % 2 != 0)
        {
            error = "Extracted register payload length must be multiple of 2 bytes.";
            return false;
        }

        int regCount = dataLength / 2;
        registers = new ushort[regCount];
        for (int i = 0; i < regCount; i++)
        {
            int bIdx = dataStart + (i * 2);
            registers[i] = (ushort)((bytes[bIdx] << 8) | bytes[bIdx + 1]);
        }

        return true;
    }
}
