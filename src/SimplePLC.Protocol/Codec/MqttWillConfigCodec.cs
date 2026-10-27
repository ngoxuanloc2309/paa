using System;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec mã hóa và giải mã cấu hình Last Will and Testament (LWT) của MQTT (0x0E60, 32 thanh ghi = 64 bytes).
/// Tuân thủ chuẩn SPLC-SPEC-MQTT-004.
/// </summary>
public static class MqttWillConfigCodec
{
    public const int MaxTopicChars = 32;
    public const int MaxPayloadChars = 30;

    /// <summary>
    /// Mã hóa cấu hình Will thành 32 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void Encode(MqttWillConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.MqttWillConfigRegisters)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.MqttWillConfigRegisters} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.MqttWillConfigRegisters).Fill(0);

        // 1. Offset +0: WILL_CONTROL
        ushort control = 0;
        if (config.Enabled) control |= ModbusRegisterMap.MqttWillControlBitEnabled;
        if (config.Retain) control |= ModbusRegisterMap.MqttWillControlBitRetain;
        control |= (ushort)(((config.Qos & 0x03) << ModbusRegisterMap.MqttWillControlQosShift) & ModbusRegisterMap.MqttWillControlQosMask);
        destination[0] = control;

        // 2. Offset +1..+16: WILL_TOPIC (32 bytes = 16 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.Topic, destination.Slice(1, 16), MaxTopicChars);

        // 3. Offset +17..+31: WILL_PAYLOAD (30 bytes = 15 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.Payload, destination.Slice(17, 15), MaxPayloadChars);
    }

    /// <summary>
    /// Giải mã 32 thanh ghi Modbus thành MqttWillConfigDto.
    /// </summary>
    public static MqttWillConfigDto Decode(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.MqttWillConfigRegisters)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.MqttWillConfigRegisters} registers.", nameof(source));

        ushort control = source[0];
        bool enabled = (control & ModbusRegisterMap.MqttWillControlBitEnabled) != 0;
        bool retain = (control & ModbusRegisterMap.MqttWillControlBitRetain) != 0;
        byte qos = (byte)((control & ModbusRegisterMap.MqttWillControlQosMask) >> ModbusRegisterMap.MqttWillControlQosShift);

        string topic = MqttTelemetryCodec.DecodePackedAscii(source.Slice(1, 16));
        string payload = MqttTelemetryCodec.DecodePackedAscii(source.Slice(17, 15));

        return new MqttWillConfigDto
        {
            Enabled = enabled,
            Retain = retain,
            Qos = qos,
            Topic = topic,
            Payload = payload
        };
    }
}
