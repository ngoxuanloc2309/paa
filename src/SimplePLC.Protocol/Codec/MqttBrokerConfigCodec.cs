using System;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec mã hóa và giải mã cấu hình kết nối MQTT Broker (0x0E00, 96 thanh ghi = 192 bytes).
/// Tuân thủ chuẩn SPLC-SPEC-MQTT-001.
/// </summary>
public static class MqttBrokerConfigCodec
{
    /// <summary>
    /// Mã hóa DTO cấu hình Broker thành 96 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void Encode(MqttBrokerConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.MqttBrokerConfigRegisters)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.MqttBrokerConfigRegisters} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.MqttBrokerConfigRegisters).Fill(0);

        // 1. Offset +0: BROKER_CONTROL
        ushort control = 0;
        if (config.Enabled) control |= ModbusRegisterMap.MqttBrokerControlBitEnabled;
        if (config.UseTls) control |= ModbusRegisterMap.MqttBrokerControlBitUseTls;
        if (config.CleanSession) control |= ModbusRegisterMap.MqttBrokerControlBitCleanSession;
        control |= (ushort)((config.ProtocolVersion << ModbusRegisterMap.MqttBrokerControlVersionShift) & ModbusRegisterMap.MqttBrokerControlVersionMask);
        destination[0] = control;

        // 2. Offset +1: BROKER_PORT
        destination[1] = config.Port;

        // 3. Offset +2: KEEPALIVE_SEC
        destination[2] = config.KeepAliveSec;

        // 4. Offset +3: RESERVED
        destination[3] = 0;

        // 5. Offset +4..+35: BROKER_HOST (64 bytes = 32 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.Host, destination.Slice(4, 32), ModbusRegisterMap.MqttBrokerHostMaxChars);

        // 6. Offset +36..+51: CLIENT_ID (32 bytes = 16 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.ClientId, destination.Slice(36, 16), ModbusRegisterMap.MqttBrokerCredentialsMaxChars);

        // 7. Offset +52..+67: USERNAME (32 bytes = 16 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.Username, destination.Slice(52, 16), ModbusRegisterMap.MqttBrokerCredentialsMaxChars);

        // 8. Offset +68..+83: PASSWORD (32 bytes = 16 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.Password, destination.Slice(68, 16), ModbusRegisterMap.MqttBrokerCredentialsMaxChars);

        // 9. Offset +84..+95: STATUS / RESERVED (12 regs) - Được điền sẵn 0x0000
    }

    /// <summary>
    /// Giải mã 96 thanh ghi Modbus thành MqttBrokerConfigDto.
    /// </summary>
    public static MqttBrokerConfigDto Decode(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.MqttBrokerConfigRegisters)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.MqttBrokerConfigRegisters} registers.", nameof(source));

        ushort control = source[0];
        bool enabled = (control & ModbusRegisterMap.MqttBrokerControlBitEnabled) != 0;
        bool useTls = (control & ModbusRegisterMap.MqttBrokerControlBitUseTls) != 0;
        bool cleanSession = (control & ModbusRegisterMap.MqttBrokerControlBitCleanSession) != 0;
        byte protocolVersion = (byte)((control & ModbusRegisterMap.MqttBrokerControlVersionMask) >> ModbusRegisterMap.MqttBrokerControlVersionShift);

        ushort port = source[1];
        ushort keepAlive = source[2];

        string host = MqttTelemetryCodec.DecodePackedAscii(source.Slice(4, 32));
        string clientId = MqttTelemetryCodec.DecodePackedAscii(source.Slice(36, 16));
        string username = MqttTelemetryCodec.DecodePackedAscii(source.Slice(52, 16));
        string password = MqttTelemetryCodec.DecodePackedAscii(source.Slice(68, 16));

        return new MqttBrokerConfigDto
        {
            Enabled = enabled,
            Host = host,
            Port = port,
            UseTls = useTls,
            CleanSession = cleanSession,
            ProtocolVersion = protocolVersion,
            KeepAliveSec = keepAlive,
            ClientId = clientId,
            Username = username,
            Password = password
        };
    }
}
