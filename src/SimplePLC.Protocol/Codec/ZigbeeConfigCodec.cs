namespace SimplePLC.Protocol.Codec;

using System;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Bộ mã hóa và giải mã khung nhị phân Modbus Holding Registers cho phân hệ Zigbee (0x1700 - 0x173F).
/// Tuân thủ nghiêm ngặt chuẩn SPLC-SPEC-ZIGBEE-006 và Wire Contract V2.0.
/// </summary>
public static class ZigbeeConfigCodec
{
    /// <summary>
    /// Đóng gói đối tượng cấu hình ZigbeeConfigDto thành mảng thanh ghi Modbus Holding Registers (Big-Endian).
    /// </summary>
    /// <param name="config">Đối tượng cấu hình nguồn.</param>
    /// <param name="destination">Vùng đệm thanh ghi đích (tối thiểu 13 thanh ghi, thông thường 64 thanh ghi).</param>
    public static void Encode(ZigbeeConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (destination.Length < 13)
        {
            throw new ArgumentException("Destination span must be at least 13 registers (for offsets +0..+12).", nameof(destination));
        }

        // Xóa sạch vùng đệm đích để đảm bảo các bit reserved đều bằng 0
        destination.Clear();

        // 1. Offset +0: CONTROL_FLAGS (16-bit)
        ushort control = 0;
        if (config.Enabled) control |= ModbusRegisterMap.ZigbeeControlBitEnabled;
        if (config.Role == ZigbeeRole.Slave) control |= ModbusRegisterMap.ZigbeeControlBitRoleSlave;
        if (config.Framing == ZigbeeFraming.Ascii) control |= ModbusRegisterMap.ZigbeeControlBitFramingAscii;
        if (config.SecurityEnabled) control |= ModbusRegisterMap.ZigbeeControlBitSecurityEnabled;
        if (config.Antenna == ZigbeeAntenna.External) control |= ModbusRegisterMap.ZigbeeControlBitAntennaExternal;
        if (config.PreserveKey) control |= ModbusRegisterMap.ZigbeeControlBitPreserveKey;

        // Mask bỏ tất cả các bit ngoài bit 0..5 (Bit 6..15 bắt buộc = 0)
        control &= 0x003F;
        destination[0] = control;

        // 2. Offset +1: SLAVE_ID (1..247)
        destination[1] = config.SlaveId;

        // 3. Offset +2: PAN_ID (0x0001..0xFFFE)
        destination[2] = config.PanId;

        // 4. Offset +3: CHANNEL (11..26)
        destination[3] = config.Channel;

        // 5. Offset +4: RESERVED_CFG (Mặc định 0)
        destination[4] = 0;

        // 6. Offset +5..+12: NET_KEY (16 bytes = 8 registers, Big-Endian: byte cao trước)
        if (config.NetKey != null && config.NetKey.Length >= ModbusRegisterMap.ZigbeeKeyBytes)
        {
            for (int i = 0; i < ModbusRegisterMap.ZigbeeKeyRegisters; i++)
            {
                byte highByte = config.NetKey[i * 2];
                byte lowByte = config.NetKey[i * 2 + 1];
                destination[5 + i] = (ushort)((highByte << 8) | lowByte);
            }
        }
        else
        {
            for (int i = 0; i < ModbusRegisterMap.ZigbeeKeyRegisters; i++)
            {
                destination[5 + i] = 0;
            }
        }
    }

    /// <summary>
    /// Giải mã mảng thanh ghi Modbus Holding Registers thành đối tượng cấu hình ZigbeeConfigDto.
    /// </summary>
    /// <param name="source">Vùng đệm thanh ghi đọc về từ thiết bị (bắt đầu từ offset 0x1700).</param>
    /// <returns>Đối tượng ZigbeeConfigDto chứa toàn bộ cấu hình đang chạy và viễn trắc.</returns>
    public static ZigbeeConfigDto Decode(ReadOnlySpan<ushort> source)
    {
        if (source.IsEmpty)
        {
            throw new ArgumentException("Source span cannot be empty.", nameof(source));
        }

        var config = new ZigbeeConfigDto();

        // 1. Offset +0: CONTROL_FLAGS
        ushort control = source[0];
        config.Enabled = (control & ModbusRegisterMap.ZigbeeControlBitEnabled) != 0;
        config.Role = (control & ModbusRegisterMap.ZigbeeControlBitRoleSlave) != 0 ? ZigbeeRole.Slave : ZigbeeRole.Master;
        config.Framing = (control & ModbusRegisterMap.ZigbeeControlBitFramingAscii) != 0 ? ZigbeeFraming.Ascii : ZigbeeFraming.Rtu;
        config.SecurityEnabled = (control & ModbusRegisterMap.ZigbeeControlBitSecurityEnabled) != 0;
        config.Antenna = (control & ModbusRegisterMap.ZigbeeControlBitAntennaExternal) != 0 ? ZigbeeAntenna.External : ZigbeeAntenna.Pcb;
        config.PreserveKey = (control & ModbusRegisterMap.ZigbeeControlBitPreserveKey) != 0;

        // 2. Offset +1: SLAVE_ID
        if (source.Length > 1)
        {
            config.SlaveId = (byte)(source[1] & 0xFF);
        }

        // 3. Offset +2: PAN_ID
        if (source.Length > 2)
        {
            config.PanId = source[2];
        }

        // 4. Offset +3: CHANNEL
        if (source.Length > 3)
        {
            config.Channel = source[3];
        }

        // 5. Offset +5..+12: NET_KEY (Mặc dù đọc về luôn trả 0, vẫn giải mã byte-for-byte chuẩn)
        if (source.Length >= 13)
        {
            config.NetKey = new byte[ModbusRegisterMap.ZigbeeKeyBytes];
            for (int i = 0; i < ModbusRegisterMap.ZigbeeKeyRegisters; i++)
            {
                ushort reg = source[5 + i];
                config.NetKey[i * 2] = (byte)((reg >> 8) & 0xFF);
                config.NetKey[i * 2 + 1] = (byte)(reg & 0xFF);
            }
        }

        // 6. VIỄN TRẮC (Offset +32 .. +36)
        if (source.Length > 32)
        {
            config.LinkState = (ZigbeeLinkState)source[32];
        }

        if (source.Length > 33)
        {
            config.RawErrorCode = source[33];
        }

        if (source.Length > 34)
        {
            config.NodeCount = source[34];
        }

        if (source.Length > 35)
        {
            config.Rssi = (short)source[35];
        }

        if (source.Length > 36)
        {
            config.IsKeySet = (source[36] & ModbusRegisterMap.ZigbeeStatusBitKeySet) != 0;
        }

        return config;
    }
}
