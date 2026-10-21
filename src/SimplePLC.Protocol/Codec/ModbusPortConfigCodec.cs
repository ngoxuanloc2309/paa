namespace SimplePLC.Protocol.Codec;

using System;
using System.Collections.Generic;
using System.Text;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

/// <summary>
/// Codec đóng gói và giải mã phân vùng cấu hình Cổng RS-485 Modbus Master (0x1400 - 0x143F).
/// Tuân thủ quy chuẩn SPLC-SPEC-MBM-003:
/// - Tối đa 4 cổng RS-485, mỗi cổng chiếm đúng 16 thanh ghi (32 bytes).
/// - Offset +0: ENABLED (bit 8 = 0x0100) | PORT_NUMBER (bit 0..7 = 1-based, 1..4)
/// - Offset +1..+2: BAUD_RATE (uint32 Big-Endian)
/// - Offset +3: PARITY (0 = None, 1 = Even, 2 = Odd)
/// - Offset +4: DATA_BITS (bit 8..15) | STOP_BITS (bit 0..7)
/// - Offset +5: RESPONSE_TIMEOUT_MS (uint16)
/// - Offset +6: INTER_FRAME_DELAY_MS (uint16)
/// - Offset +7: RETRY_COUNT (uint16)
/// - Offset +8..+13: PORT_NAME (12 bytes ASCII packed string = 6 thanh ghi)
/// - Offset +14..+15: RESERVED (luôn ghi 0x0000)
/// </summary>
public static class ModbusPortConfigCodec
{
    /// <summary>
    /// Mã hóa 1 cấu hình cổng ModbusPortConfigDto thành 16 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodePort(ModbusPortConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.ModbusPortConfigRegistersPerPort)
        {
            throw new ArgumentException(
                $"Destination span must have at least {ModbusRegisterMap.ModbusPortConfigRegistersPerPort} registers.",
                nameof(destination));
        }

        destination.Slice(0, ModbusRegisterMap.ModbusPortConfigRegistersPerPort).Fill(0);

        // +0: ENABLED (bit 8) | PORT_NUMBER (1..4)
        ushort control = (ushort)((config.Enabled ? 0x0100 : 0x0000) | ((config.PortIndex + 1) & 0xFF));
        destination[0] = control;

        // +1..+2: BAUD_RATE (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.BaudRate, destination.Slice(1, 2));

        // +3: PARITY
        destination[3] = (ushort)config.Parity;

        // +4: DATA_BITS (High) | STOP_BITS (Low)
        byte dataBits = config.DataBits == 0 ? (byte)8 : config.DataBits;
        byte stopBits = config.StopBits == 0 ? (byte)1 : config.StopBits;
        destination[4] = (ushort)((dataBits << 8) | stopBits);

        // +5: RESPONSE_TIMEOUT_MS
        destination[5] = config.ResponseTimeoutMs;

        // +6: INTER_FRAME_DELAY_MS
        destination[6] = config.InterFrameDelayMs;

        // +7: RETRY_COUNT
        destination[7] = config.RetryCount;

        // +8..+13: PORT_NAME (12 bytes ASCII = 6 registers)
        EncodePackedAscii(config.PortName ?? string.Empty, destination.Slice(8, 6), ModbusRegisterMap.ModbusPortNameMaxChars);

        // +14..+15: RESERVED
        destination[14] = 0;
        destination[15] = 0;
    }

    /// <summary>
    /// Giải mã 16 thanh ghi Modbus thành ModbusPortConfigDto.
    /// </summary>
    public static ModbusPortConfigDto DecodePort(ReadOnlySpan<ushort> source, int defaultPortIndex = 0)
    {
        if (source.Length < ModbusRegisterMap.ModbusPortConfigRegistersPerPort)
        {
            throw new ArgumentException(
                $"Source span must have at least {ModbusRegisterMap.ModbusPortConfigRegistersPerPort} registers.",
                nameof(source));
        }

        ushort control = source[0];
        bool enabled = (control & 0x0100) != 0;
        byte rawPortNum = (byte)(control & 0xFF);
        byte portIndex = rawPortNum > 0 ? (byte)(rawPortNum - 1) : (byte)defaultPortIndex;

        uint baudRate = RegisterCodec.DecodeUInt32(source.Slice(1, 2));
        if (baudRate == 0) baudRate = 9600;

        byte parityRaw = (byte)source[3];
        var parity = Enum.IsDefined(typeof(ModbusParity), parityRaw) ? (ModbusParity)parityRaw : ModbusParity.None;

        ushort framing = source[4];
        byte dataBits = (byte)(framing >> 8);
        if (dataBits == 0) dataBits = 8;
        byte stopBits = (byte)(framing & 0xFF);
        if (stopBits == 0) stopBits = 1;

        ushort timeoutMs = source[5];
        if (timeoutMs == 0) timeoutMs = 200;

        ushort delayMs = source[6];
        ushort retryCount = source[7];

        string portName = DecodePackedAscii(source.Slice(8, 6)).Trim();
        if (string.IsNullOrWhiteSpace(portName))
        {
            portName = $"RS-485 #{portIndex + 1}";
        }

        return new ModbusPortConfigDto
        {
            PortIndex = portIndex,
            PortName = portName,
            Enabled = enabled,
            BaudRate = baudRate,
            DataBits = dataBits,
            Parity = parity,
            StopBits = stopBits,
            ResponseTimeoutMs = timeoutMs,
            InterFrameDelayMs = delayMs,
            RetryCount = retryCount
        };
    }

    /// <summary>
    /// Mã hóa danh sách các cổng thành toàn bộ 64 thanh ghi dải 0x1400..0x143F.
    /// </summary>
    public static void EncodeAllPorts(IReadOnlyList<ModbusPortConfigDto>? ports, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.ModbusPortConfigTableTotalLength)
        {
            throw new ArgumentException(
                $"Destination span must have at least {ModbusRegisterMap.ModbusPortConfigTableTotalLength} registers.",
                nameof(destination));
        }

        destination.Slice(0, ModbusRegisterMap.ModbusPortConfigTableTotalLength).Fill(0);

        int count = ports?.Count ?? 0;
        for (int i = 0; i < ModbusRegisterMap.ModbusPortConfigMaxPorts; i++)
        {
            int offset = i * ModbusRegisterMap.ModbusPortConfigRegistersPerPort;
            var portSpan = destination.Slice(offset, ModbusRegisterMap.ModbusPortConfigRegistersPerPort);

            if (ports != null && i < count && ports[i] != null)
            {
                EncodePort(ports[i], portSpan);
            }
            else
            {
                // Cổng mặc định chưa dùng
                var defaultDto = new ModbusPortConfigDto
                {
                    PortIndex = (byte)i,
                    PortName = $"RS-485 #{i + 1}",
                    Enabled = false
                };
                EncodePort(defaultDto, portSpan);
            }
        }
    }

    /// <summary>
    /// Giải mã 64 thanh ghi dải 0x1400..0x143F thành danh sách 4 cổng ModbusPortConfigDto.
    /// </summary>
    public static List<ModbusPortConfigDto> DecodeAllPorts(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.ModbusPortConfigTableTotalLength)
        {
            throw new ArgumentException(
                $"Source span must have at least {ModbusRegisterMap.ModbusPortConfigTableTotalLength} registers.",
                nameof(source));
        }

        var result = new List<ModbusPortConfigDto>(ModbusRegisterMap.ModbusPortConfigMaxPorts);
        for (int i = 0; i < ModbusRegisterMap.ModbusPortConfigMaxPorts; i++)
        {
            int offset = i * ModbusRegisterMap.ModbusPortConfigRegistersPerPort;
            var portSpan = source.Slice(offset, ModbusRegisterMap.ModbusPortConfigRegistersPerPort);
            result.Add(DecodePort(portSpan, i));
        }

        return result;
    }

    private static void EncodePackedAscii(string text, Span<ushort> destination, int maxCapacity)
    {
        destination.Fill(0);
        if (string.IsNullOrEmpty(text)) return;

        byte[] bytes = Encoding.ASCII.GetBytes(text);
        int len = Math.Min(bytes.Length, maxCapacity);
        int regCount = destination.Length;

        for (int i = 0; i < len; i += 2)
        {
            int regIdx = i / 2;
            if (regIdx >= regCount) break;

            byte high = bytes[i];
            byte low = (i + 1 < len) ? bytes[i + 1] : (byte)0;
            destination[regIdx] = (ushort)((high << 8) | low);
        }
    }

    private static string DecodePackedAscii(ReadOnlySpan<ushort> source)
    {
        var bytes = new List<byte>(source.Length * 2);
        foreach (var reg in source)
        {
            byte high = (byte)(reg >> 8);
            byte low = (byte)(reg & 0xFF);
            if (high == 0) break;
            bytes.Add(high);
            if (low == 0) break;
            bytes.Add(low);
        }
        return Encoding.ASCII.GetString(bytes.ToArray());
    }
}
