using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec đóng gói và giải mã các khối MQTT Telemetry Publish theo đặc tả SPLC-SPEC-MQTT-001.
/// Mỗi khối chiếm đúng 96 thanh ghi (192 bytes):
/// - 32 thanh ghi đầu: Header, cờ điều khiển, chu kỳ, ngưỡng biến thiên, mảng Tag IDs và Topic string.
/// - 64 thanh ghi sau: Bộ đệm Custom Template (128 bytes ASCII).
/// </summary>
public static class MqttTelemetryCodec
{
    /// <summary>
    /// Mã hóa một MqttTelemetryConfigDto thành 96 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeBlock(MqttTelemetryConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.MqttRegistersPerBlock)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.MqttRegistersPerBlock} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.MqttRegistersPerBlock).Fill(0);

        // 1. Phân tích chế độ PublishMode (0=Event, 1=COV, 2=Periodic)
        ushort pubMode = 0;
        if (string.Equals(config.PublishMode, "COV", StringComparison.OrdinalIgnoreCase))
        {
            pubMode = 1;
        }
        else if (string.Equals(config.PublishMode, "Cyclic", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(config.PublishMode, "Periodic", StringComparison.OrdinalIgnoreCase))
        {
            pubMode = 2;
        }

        // 2. Phân tích chế độ PayloadMode (0=JSON, 1=Raw, 2=Custom)
        ushort payloadMode = 0;
        if (string.Equals(config.PayloadMode, "Raw", StringComparison.OrdinalIgnoreCase))
        {
            payloadMode = 1;
        }
        else if (string.Equals(config.PayloadMode, "Custom", StringComparison.OrdinalIgnoreCase))
        {
            payloadMode = 2;
        }

        // 3. Đóng gói CONTROL_FLAGS (Offset +0)
        ushort control = ModbusRegisterMap.MqttControlBitEnabled;
        control |= (ushort)((pubMode << ModbusRegisterMap.MqttControlModeShift) & ModbusRegisterMap.MqttControlModeMask);
        control |= (ushort)(((config.Qos & 0x03) << ModbusRegisterMap.MqttControlQosShift) & ModbusRegisterMap.MqttControlQosMask);
        if (config.Retain)
        {
            control |= ModbusRegisterMap.MqttControlBitRetain;
        }
        control |= (ushort)((payloadMode << ModbusRegisterMap.MqttControlPayloadModeShift) & ModbusRegisterMap.MqttControlPayloadModeMask);

        destination[0] = control;

        // Offset +1: EN_TAG_INDEX
        destination[1] = config.EnTagIndex ?? 0xFFFF;

        // Offset +2..+3: INTERVAL_MS (uint32)
        RegisterCodec.EncodeUInt32(config.IntervalMs, destination.Slice(2, 2));

        // Offset +4..+5: DEADBAND (float32)
        EncodeFloat((float)config.Deadband, destination.Slice(4, 2));

        // Offset +6..+14: TAG_COUNT & TAG_INDICES[8]
        var fields = config.Fields ?? new List<MqttPayloadFieldDto>();
        ushort tagCount = (ushort)Math.Clamp(fields.Count > 0 ? fields.Count : (config.DataTagIndex.HasValue ? 1 : 0), 0, ModbusRegisterMap.MqttMaxTagsPerBlock);
        destination[6] = tagCount;

        for (int i = 0; i < ModbusRegisterMap.MqttMaxTagsPerBlock; i++)
        {
            if (i < fields.Count)
            {
                destination[7 + i] = fields[i].TagIndex;
            }
            else if (i == 0 && config.DataTagIndex.HasValue)
            {
                destination[7 + i] = config.DataTagIndex.Value;
            }
            else
            {
                destination[7 + i] = 0xFFFF;
            }
        }

        // Offset +15: RESERVED
        destination[15] = 0;

        // Offset +16..+31: TOPIC_STRING (32 bytes ASCII = 16 thanh ghi)
        EncodePackedAscii(config.Topic, destination.Slice(16, 16), ModbusRegisterMap.MqttTopicMaxChars);

        // Offset +32..+95: CUSTOM_TEMPLATE (128 bytes ASCII = 64 thanh ghi)
        if (payloadMode == 2 && !string.IsNullOrEmpty(config.CustomTemplate))
        {
            EncodePackedAscii(config.CustomTemplate, destination.Slice(32, 64), ModbusRegisterMap.MqttTemplateMaxChars);
        }
    }

    /// <summary>
    /// Giải mã 96 thanh ghi Modbus thành MqttTelemetryConfigDto.
    /// </summary>
    public static MqttTelemetryConfigDto DecodeBlock(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.MqttRegistersPerBlock)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.MqttRegistersPerBlock} registers.", nameof(source));

        ushort control = source[0];
        ushort pubModeVal = (ushort)((control & ModbusRegisterMap.MqttControlModeMask) >> ModbusRegisterMap.MqttControlModeShift);
        int qosVal = (control & ModbusRegisterMap.MqttControlQosMask) >> ModbusRegisterMap.MqttControlQosShift;
        bool retainVal = (control & ModbusRegisterMap.MqttControlBitRetain) != 0;
        ushort payloadModeVal = (ushort)((control & ModbusRegisterMap.MqttControlPayloadModeMask) >> ModbusRegisterMap.MqttControlPayloadModeShift);

        string pubMode = pubModeVal switch
        {
            1 => "COV",
            2 => "Periodic",
            _ => "Event"
        };

        string payloadMode = payloadModeVal switch
        {
            1 => "Raw",
            2 => "Custom",
            _ => "JSON"
        };

        ushort enTag = source[1];
        uint intervalMs = RegisterCodec.DecodeUInt32(source.Slice(2, 2));
        float deadband = DecodeFloat(source.Slice(4, 2));

        ushort tagCount = Math.Min(source[6], (ushort)ModbusRegisterMap.MqttMaxTagsPerBlock);
        var fields = new List<MqttPayloadFieldDto>(tagCount);
        for (int i = 0; i < tagCount; i++)
        {
            ushort tIdx = source[7 + i];
            if (tIdx != 0xFFFF)
            {
                fields.Add(new MqttPayloadFieldDto
                {
                    Key = $"tag_{tIdx}",
                    TagIndex = tIdx
                });
            }
        }

        string topic = DecodePackedAscii(source.Slice(16, 16));
        string customTemplate = payloadModeVal == 2 ? DecodePackedAscii(source.Slice(32, 64)) : string.Empty;

        return new MqttTelemetryConfigDto
        {
            Topic = topic,
            PublishMode = pubMode,
            Qos = qosVal,
            Retain = retainVal,
            PayloadMode = payloadMode,
            IntervalMs = intervalMs,
            Deadband = deadband,
            EnTagIndex = enTag != 0xFFFF ? enTag : null,
            DataTagIndex = fields.Count > 0 ? fields[0].TagIndex : null,
            Fields = fields,
            CustomTemplate = customTemplate
        };
    }

    /// <summary>
    /// Mã hóa chuỗi ASCII thành các thanh ghi 16-bit Big-Endian (2 ký tự / thanh ghi).
    /// </summary>
    public static void EncodePackedAscii(string? text, Span<ushort> destination, int maxChars)
    {
        destination.Fill(0);
        if (string.IsNullOrEmpty(text)) return;

        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(text);
        int maxCapacity = Math.Min(maxChars, destination.Length * 2);
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

    /// <summary>
    /// Giải mã các thanh ghi 16-bit Big-Endian thành chuỗi ASCII.
    /// </summary>
    public static string DecodePackedAscii(ReadOnlySpan<ushort> source)
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
        return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
    }

    /// <summary>
    /// Mã hóa Float32 IEEE 754 thành 2 thanh ghi (High Word -> Low Word).
    /// </summary>
    public static void EncodeFloat(float value, Span<ushort> destination)
    {
        uint bits = BitConverter.SingleToUInt32Bits(value);
        destination[0] = (ushort)(bits >> 16);
        destination[1] = (ushort)(bits & 0xFFFF);
    }

    /// <summary>
    /// Giải mã 2 thanh ghi thành Float32 IEEE 754.
    /// </summary>
    public static float DecodeFloat(ReadOnlySpan<ushort> source)
    {
        uint bits = ((uint)source[0] << 16) | source[1];
        return BitConverter.UInt32BitsToSingle(bits);
    }
}
