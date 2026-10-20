using System;
using System.Collections.Generic;
using System.Net;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec mã hóa và giải mã phân hệ Wi-Fi ST67 (0x1300..0x13FF, 256 thanh ghi = 512 bytes).
/// Tuân thủ chuẩn SPLC-SPEC-WIFI-004-ICD.
/// </summary>
public static class WifiCodec
{
    // ==========================================
    // 1. WIFI CONFIG CODEC (0x1300, 64 REGISTERS)
    // ==========================================

    /// <summary>
    /// Mã hóa WifiConfigDto thành 64 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeConfig(WifiConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.WifiConfigRegisters)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.WifiConfigRegisters} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.WifiConfigRegisters).Fill(0);

        // 1. Offset +0: Control Flags
        ushort control = 0;
        if (config.Enabled) control |= ModbusRegisterMap.WifiControlBitEnable;
        control |= (ushort)((config.Mode << ModbusRegisterMap.WifiControlModeShift) & ModbusRegisterMap.WifiControlModeMask);
        if (config.IsDhcp) control |= ModbusRegisterMap.WifiControlBitDhcp;
        if (config.AutoReconnect) control |= ModbusRegisterMap.WifiControlBitAutoReconnect;
        destination[0] = control;

        // 2. Offset +1: Reserved (0x0000)
        destination[1] = 0;

        // 3. Offset +2..+17: SSID (32 bytes = 16 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.Ssid, destination.Slice(2, 16), ModbusRegisterMap.WifiMaxSsidChars);

        // 4. Offset +18..+49: Password (64 bytes = 32 regs)
        MqttTelemetryCodec.EncodePackedAscii(config.Password, destination.Slice(18, 32), ModbusRegisterMap.WifiMaxPasswordChars);

        // 5. Offset +50..+57: Static IP params (8 regs = 16 bytes)
        EncodeIpAddress(config.StaticIp, destination.Slice(50, 2));
        EncodeIpAddress(config.StaticSubnet, destination.Slice(52, 2));
        EncodeIpAddress(config.StaticGateway, destination.Slice(54, 2));
        EncodeIpAddress(config.StaticDns, destination.Slice(56, 2));

        // Offset +58..+63: Live Feedback (chỉ đọc từ MCU, để 0 khi Host ghi)
    }

    /// <summary>
    /// Giải mã 64 thanh ghi Modbus thành WifiConfigDto.
    /// </summary>
    public static WifiConfigDto DecodeConfig(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.WifiConfigRegisters)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.WifiConfigRegisters} registers.", nameof(source));

        ushort control = source[0];
        bool enabled = (control & ModbusRegisterMap.WifiControlBitEnable) != 0;
        byte mode = (byte)((control & ModbusRegisterMap.WifiControlModeMask) >> ModbusRegisterMap.WifiControlModeShift);
        bool isDhcp = (control & ModbusRegisterMap.WifiControlBitDhcp) != 0;
        bool autoReconnect = (control & ModbusRegisterMap.WifiControlBitAutoReconnect) != 0;

        string ssid = MqttTelemetryCodec.DecodePackedAscii(source.Slice(2, 16));
        string password = MqttTelemetryCodec.DecodePackedAscii(source.Slice(18, 32));

        string staticIp = DecodeIpAddress(source.Slice(50, 2));
        string staticSubnet = DecodeIpAddress(source.Slice(52, 2));
        string staticGateway = DecodeIpAddress(source.Slice(54, 2));
        string staticDns = DecodeIpAddress(source.Slice(56, 2));

        ushort connStatus = source[58];
        short rssiDbm = (short)source[59];
        string currentIp = DecodeIpAddress(source.Slice(60, 2));
        string mac = DecodeMacAddress(source.Slice(62, 2));

        return new WifiConfigDto
        {
            Enabled = enabled,
            Mode = mode,
            IsDhcp = isDhcp,
            AutoReconnect = autoReconnect,
            Ssid = ssid,
            Password = password,
            StaticIp = staticIp,
            StaticSubnet = staticSubnet,
            StaticGateway = staticGateway,
            StaticDns = staticDns,
            ConnectionStatus = connStatus,
            RssiDbm = rssiDbm,
            CurrentIp = currentIp,
            MacAddress = mac
        };
    }

    // ==========================================
    // 2. WIFI SCAN CONTROL (0x1340, 16 REGISTERS)
    // ==========================================

    public static void EncodeControl(WifiScanControlDto control, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (destination.Length < ModbusRegisterMap.WifiControlRegisters)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.WifiControlRegisters} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.WifiControlRegisters).Fill(0);
        destination[0] = control.Command;
        destination[1] = control.ScanState;
        destination[2] = control.ApFoundCount;
        destination[3] = control.ErrorCode;
    }

    public static WifiScanControlDto DecodeControl(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.WifiControlRegisters)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.WifiControlRegisters} registers.", nameof(source));

        return new WifiScanControlDto
        {
            Command = source[0],
            ScanState = source[1],
            ApFoundCount = source[2],
            ErrorCode = source[3]
        };
    }

    // ==========================================
    // 3. WIFI SCAN RESULTS (0x1350, 8 x 22 REGISTERS)
    // ==========================================

    /// <summary>
    /// Giải mã danh sách tối đa count Access Points từ vùng nhớ scan kết quả.
    /// </summary>
    public static IReadOnlyList<WifiApInfoDto> DecodeScanResults(ReadOnlySpan<ushort> source, int count)
    {
        int actualCount = Math.Clamp(count, 0, ModbusRegisterMap.WifiMaxScanAps);
        var list = new List<WifiApInfoDto>(actualCount);

        for (int i = 0; i < actualCount; i++)
        {
            int offset = i * ModbusRegisterMap.WifiRegistersPerAp;
            if (offset + ModbusRegisterMap.WifiRegistersPerAp > source.Length) break;

            var apSlice = source.Slice(offset, ModbusRegisterMap.WifiRegistersPerAp);
            string ssid = MqttTelemetryCodec.DecodePackedAscii(apSlice.Slice(0, 16));
            if (string.IsNullOrWhiteSpace(ssid)) continue;

            short rssi = (short)apSlice[16];
            ushort authChan = apSlice[17];
            byte authMode = (byte)(authChan >> 8);
            byte channel = (byte)(authChan & 0xFF);
            string bssid = DecodeMacAddress(apSlice.Slice(18, 3));

            list.Add(new WifiApInfoDto
            {
                Ssid = ssid,
                RssiDbm = rssi,
                AuthMode = authMode,
                Channel = channel,
                Bssid = bssid
            });
        }

        return list;
    }

    // ==========================================
    // 4. IP & MAC HELPERS
    // ==========================================

    public static void EncodeIpAddress(string? ipString, Span<ushort> destination)
    {
        if (destination.Length < 2)
            throw new ArgumentException("Destination span must be at least 2 registers for IPv4.", nameof(destination));

        if (IPAddress.TryParse(ipString, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            byte[] bytes = ip.GetAddressBytes();
            destination[0] = (ushort)((bytes[0] << 8) | bytes[1]);
            destination[1] = (ushort)((bytes[2] << 8) | bytes[3]);
        }
        else
        {
            destination[0] = 0;
            destination[1] = 0;
        }
    }

    public static string DecodeIpAddress(ReadOnlySpan<ushort> source)
    {
        if (source.Length < 2) return "0.0.0.0";
        byte b0 = (byte)(source[0] >> 8);
        byte b1 = (byte)(source[0] & 0xFF);
        byte b2 = (byte)(source[1] >> 8);
        byte b3 = (byte)(source[1] & 0xFF);
        return $"{b0}.{b1}.{b2}.{b3}";
    }

    public static string DecodeMacAddress(ReadOnlySpan<ushort> source)
    {
        if (source.Length < 2) return "00:00:00:00:00:00";
        if (source.Length >= 3)
        {
            byte b0 = (byte)(source[0] >> 8);
            byte b1 = (byte)(source[0] & 0xFF);
            byte b2 = (byte)(source[1] >> 8);
            byte b3 = (byte)(source[1] & 0xFF);
            byte b4 = (byte)(source[2] >> 8);
            byte b5 = (byte)(source[2] & 0xFF);
            return $"{b0:X2}:{b1:X2}:{b2:X2}:{b3:X2}:{b4:X2}:{b5:X2}";
        }
        else
        {
            byte b0 = (byte)(source[0] >> 8);
            byte b1 = (byte)(source[0] & 0xFF);
            byte b2 = (byte)(source[1] >> 8);
            byte b3 = (byte)(source[1] & 0xFF);
            return $"{b0:X2}:{b1:X2}:{b2:X2}:{b3:X2}";
        }
    }
}
