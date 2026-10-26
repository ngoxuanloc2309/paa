namespace SimplePLC.Protocol.Codec;

using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Codec mã hóa và giải mã Bảng Ánh Xạ Dữ Liệu Modbus Slave (0x1500 - 0x157F).
/// Tuân thủ quy chuẩn SPLC-SPEC-SLAVE-001:
/// - 32 Entries x 4 thanh ghi = 128 thanh ghi (256 bytes).
/// - Offset +0: MODBUS_ADDRESS (uint16: 0..65535)
/// - Offset +1: TARGET_TAG_INDEX (uint16: 0..127)
/// - Offset +2: FLAGS (uint16):
///     Bit 0: IS_WRITABLE (0 = ReadOnly, 1 = ReadWrite)
///     Bit 1..3: DATA_TYPE (1 = BOOL, 2 = INT32, 3 = REAL, 4 = UINT16, 5 = INT16)
///     Bit 4..7: PORT_MASK (Bit 0 = Port 1, Bit 1 = Port 2...)
///     Bit 8: IS_ACTIVE (1 = Mục có hiệu lực, 0 = Mục trống)
/// - Offset +3: RESERVED (0x0000)
/// </summary>
public static class ModbusSlaveMapCodec
{
    public const ushort FlagBitWritable = 0x0001;
    public const ushort FlagDataTypeMask = 0x000E;
    public const int FlagDataTypeShift = 1;
    public const ushort FlagPortMask = 0x00F0;
    public const int FlagPortShift = 4;
    public const ushort FlagBitActive = 0x0100;

    /// <summary>
    /// Mã hóa danh sách các mục ánh xạ Modbus Slave thành 128 thanh ghi Modbus.
    /// </summary>
    public static void EncodeEntries(IReadOnlyList<ModbusSlaveMapEntryDto>? entries, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.ModbusSlaveMapTableTotalLength)
        {
            throw new ArgumentException(
                $"Destination span must have at least {ModbusRegisterMap.ModbusSlaveMapTableTotalLength} registers.",
                nameof(destination));
        }

        destination.Slice(0, ModbusRegisterMap.ModbusSlaveMapTableTotalLength).Fill(0);

        if (entries == null || entries.Count == 0)
            return;

        int count = Math.Min(entries.Count, ModbusRegisterMap.ModbusSlaveMapMaxEntries);
        for (int i = 0; i < count; i++)
        {
            var entry = entries[i];
            int offset = i * ModbusRegisterMap.ModbusSlaveMapRegistersPerEntry;

            // Offset +0: MODBUS_ADDRESS
            destination[offset + 0] = entry.ModbusAddress;

            // Offset +1: TARGET_TAG_INDEX
            destination[offset + 1] = entry.TargetTagIndex;

            // Offset +2: FLAGS
            ushort flags = FlagBitActive;
            if (entry.AccessRight == ModbusSlaveAccessRight.ReadWrite)
            {
                flags |= FlagBitWritable;
            }

            flags |= (ushort)((entry.DataType & 0x07) << FlagDataTypeShift);
            flags |= (ushort)((entry.PortMask & 0x0F) << FlagPortShift);

            destination[offset + 2] = flags;

            // Offset +3: RESERVED
            destination[offset + 3] = 0x0000;
        }
    }

    /// <summary>
    /// Giải mã 128 thanh ghi Modbus thành danh sách ModbusSlaveMapEntryDto.
    /// </summary>
    public static List<ModbusSlaveMapEntryDto> DecodeEntries(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.ModbusSlaveMapTableTotalLength)
        {
            throw new ArgumentException(
                $"Source span must have at least {ModbusRegisterMap.ModbusSlaveMapTableTotalLength} registers.",
                nameof(source));
        }

        var result = new List<ModbusSlaveMapEntryDto>();

        for (int i = 0; i < ModbusRegisterMap.ModbusSlaveMapMaxEntries; i++)
        {
            int offset = i * ModbusRegisterMap.ModbusSlaveMapRegistersPerEntry;
            ushort flags = source[offset + 2];

            // Nếu không bật cờ Active thì bỏ qua
            if ((flags & FlagBitActive) == 0)
                continue;

            ushort modbusAddr = source[offset + 0];
            ushort tagIndex = source[offset + 1];
            bool isWritable = (flags & FlagBitWritable) != 0;
            byte dataType = (byte)((flags & FlagDataTypeMask) >> FlagDataTypeShift);
            if (dataType == 0) dataType = 2; // Default INT32
            byte portMask = (byte)((flags & FlagPortMask) >> FlagPortShift);
            if (portMask == 0) portMask = 0x0F;

            result.Add(new ModbusSlaveMapEntryDto
            {
                ModbusAddress = modbusAddr,
                TargetTagIndex = tagIndex,
                AccessRight = isWritable ? ModbusSlaveAccessRight.ReadWrite : ModbusSlaveAccessRight.ReadOnly,
                DataType = dataType,
                PortMask = portMask,
                Description = $"SlaveReg_{modbusAddr}"
            });
        }

        return result;
    }
}
