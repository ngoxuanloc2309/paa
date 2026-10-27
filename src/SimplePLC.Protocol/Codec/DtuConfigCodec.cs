using System;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec mã hóa và giải mã cấu hình toàn diện DTU Gateway (HTTP 0x1600, TCP 0x1640, Routing 0x1680).
/// Tuân thủ quy chuẩn đặc tả SPLC-SPEC-DTU-001.
/// </summary>
public static class DtuConfigCodec
{
    public const ushort HttpControlBitEnabled = 0x0001;
    public const ushort HttpControlMethodMask = 0x0006;
    public const int HttpControlMethodShift = 1;

    public const ushort TcpControlBitEnabled = 0x0001;
    public const ushort TcpControlSocketModeMask = 0x0002;
    public const int TcpControlSocketModeShift = 1;

    public const ushort RoutingControlBitEnabled = 0x0001;
    public const ushort RoutingControlSerialModeMask = 0x000E;
    public const int RoutingControlSerialModeShift = 1;
    public const ushort RoutingControlUplinkModeMask = 0x00F0;
    public const int RoutingControlUplinkModeShift = 4;
    public const ushort RoutingControlPortIndexMask = 0xFF00;
    public const int RoutingControlPortIndexShift = 8;

    // --- HTTP Config (64 regs = 128 bytes) ---
    public static void EncodeHttp(DtuHttpConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (destination.Length < ModbusRegisterMap.DtuHttpConfigRegisters)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.DtuHttpConfigRegisters} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.DtuHttpConfigRegisters).Fill(0);

        ushort control = 0;
        if (config.Enabled) control |= HttpControlBitEnabled;
        control |= (ushort)(((int)config.Method << HttpControlMethodShift) & HttpControlMethodMask);
        destination[0] = control;

        destination[1] = config.TimeoutSec;
        destination[2] = config.PeriodicIntervalSec;
        destination[3] = 0; // Reserved

        MqttTelemetryCodec.EncodePackedAscii(config.Url, destination.Slice(4, 32), ModbusRegisterMap.DtuHttpUrlMaxChars);
        MqttTelemetryCodec.EncodePackedAscii(config.CustomHeaders, destination.Slice(36, 28), ModbusRegisterMap.DtuHttpHeadersMaxChars);
    }

    public static DtuHttpConfigDto DecodeHttp(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.DtuHttpConfigRegisters)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.DtuHttpConfigRegisters} registers.", nameof(source));

        ushort control = source[0];
        bool enabled = (control & HttpControlBitEnabled) != 0;
        ushort methodVal = (ushort)((control & HttpControlMethodMask) >> HttpControlMethodShift);
        var method = Enum.IsDefined(typeof(DtuHttpMethod), methodVal) ? (DtuHttpMethod)methodVal : DtuHttpMethod.Post;

        ushort timeout = source[1];
        ushort periodic = source[2];

        string url = MqttTelemetryCodec.DecodePackedAscii(source.Slice(4, 32));
        string headers = MqttTelemetryCodec.DecodePackedAscii(source.Slice(36, 28));

        return new DtuHttpConfigDto
        {
            Enabled = enabled,
            Method = method,
            TimeoutSec = timeout,
            PeriodicIntervalSec = periodic,
            Url = url,
            CustomHeaders = headers
        };
    }

    // --- TCP Config (64 regs = 128 bytes) ---
    public static void EncodeTcp(DtuTcpConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (destination.Length < ModbusRegisterMap.DtuTcpConfigRegisters)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.DtuTcpConfigRegisters} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.DtuTcpConfigRegisters).Fill(0);

        ushort control = 0;
        if (config.Enabled) control |= TcpControlBitEnabled;
        control |= (ushort)(((int)config.SocketMode << TcpControlSocketModeShift) & TcpControlSocketModeMask);
        destination[0] = control;

        destination[1] = config.RemotePort;
        destination[2] = config.LocalPort;
        destination[3] = config.ReconnectDelaySec;
        destination[4] = config.HeartbeatIntervalSec;
        destination[5] = 0;
        destination[6] = 0;
        destination[7] = 0;

        MqttTelemetryCodec.EncodePackedAscii(config.RemoteHost, destination.Slice(8, 32), ModbusRegisterMap.DtuTcpHostMaxChars);
        MqttTelemetryCodec.EncodePackedAscii(config.HeartbeatPayload, destination.Slice(40, 24), ModbusRegisterMap.DtuTcpHeartbeatMaxChars);
    }

    public static DtuTcpConfigDto DecodeTcp(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.DtuTcpConfigRegisters)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.DtuTcpConfigRegisters} registers.", nameof(source));

        ushort control = source[0];
        bool enabled = (control & TcpControlBitEnabled) != 0;
        ushort modeVal = (ushort)((control & TcpControlSocketModeMask) >> TcpControlSocketModeShift);
        var socketMode = Enum.IsDefined(typeof(DtuTcpSocketMode), modeVal) ? (DtuTcpSocketMode)modeVal : DtuTcpSocketMode.Client;

        ushort remotePort = source[1];
        ushort localPort = source[2];
        ushort reconnectDelay = source[3];
        ushort heartbeatInterval = source[4];

        string host = MqttTelemetryCodec.DecodePackedAscii(source.Slice(8, 32));
        string heartbeat = MqttTelemetryCodec.DecodePackedAscii(source.Slice(40, 24));

        return new DtuTcpConfigDto
        {
            Enabled = enabled,
            SocketMode = socketMode,
            RemotePort = remotePort,
            LocalPort = localPort,
            ReconnectDelaySec = reconnectDelay,
            HeartbeatIntervalSec = heartbeatInterval,
            RemoteHost = host,
            HeartbeatPayload = heartbeat
        };
    }

    // --- Routing Config (64 regs = 128 bytes) ---
    public static void EncodeRouting(DtuConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (destination.Length < ModbusRegisterMap.DtuRoutingConfigRegisters)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.DtuRoutingConfigRegisters} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.DtuRoutingConfigRegisters).Fill(0);

        ushort control = 0;
        if (config.Enabled) control |= RoutingControlBitEnabled;
        control |= (ushort)(((int)config.SerialMode << RoutingControlSerialModeShift) & RoutingControlSerialModeMask);
        control |= (ushort)(((int)config.UplinkMode << RoutingControlUplinkModeShift) & RoutingControlUplinkModeMask);
        control |= (ushort)(((int)config.PortIndex << RoutingControlPortIndexShift) & RoutingControlPortIndexMask);
        destination[0] = control;

        destination[1] = config.FrameTimeoutDelayMs;
        destination[2] = config.MaxPacketSize;
    }

    public static void DecodeRouting(ReadOnlySpan<ushort> source, DtuConfigDto target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (source.Length < ModbusRegisterMap.DtuRoutingConfigRegisters)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.DtuRoutingConfigRegisters} registers.", nameof(source));

        ushort control = source[0];
        target.Enabled = (control & RoutingControlBitEnabled) != 0;
        
        ushort serialModeVal = (ushort)((control & RoutingControlSerialModeMask) >> RoutingControlSerialModeShift);
        target.SerialMode = Enum.IsDefined(typeof(DtuSerialMode), serialModeVal) ? (DtuSerialMode)serialModeVal : DtuSerialMode.Transference;

        ushort uplinkModeVal = (ushort)((control & RoutingControlUplinkModeMask) >> RoutingControlUplinkModeShift);
        target.UplinkMode = Enum.IsDefined(typeof(DtuUplinkMode), uplinkModeVal) ? (DtuUplinkMode)uplinkModeVal : DtuUplinkMode.None;

        target.PortIndex = (byte)((control & RoutingControlPortIndexMask) >> RoutingControlPortIndexShift);
        target.FrameTimeoutDelayMs = source[1];
        target.MaxPacketSize = source[2];
    }
}
