using System;
using System.Net;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO cấu hình và phản hồi trạng thái Wi-Fi ST67 (0x1300..0x133F, 64 thanh ghi = 128 Bytes).
/// SPLC-SPEC-WIFI-004-ICD.
/// </summary>
public sealed class WifiConfigDto
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Mode: 0 = Station (STA), 1 = Access Point (AP), 2 = STA + AP.
    /// </summary>
    public byte Mode { get; set; } = 0;

    /// <summary>
    /// DHCP: true = Tự động nhận IP từ router; false = Dùng Static IP.
    /// </summary>
    public bool IsDhcp { get; set; } = true;

    /// <summary>
    /// Tự động kết nối lại khi mất sóng.
    /// </summary>
    public bool AutoReconnect { get; set; } = true;

    /// <summary>
    /// Tên mạng Wi-Fi (Tối đa 31 ký tự ASCII).
    /// </summary>
    public string Ssid { get; set; } = string.Empty;

    /// <summary>
    /// Mật khẩu Wi-Fi (Tối đa 63 ký tự ASCII).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    // Static IP params (khi IsDhcp == false)
    public string StaticIp { get; set; } = "192.168.1.100";
    public string StaticSubnet { get; set; } = "255.255.255.0";
    public string StaticGateway { get; set; } = "192.168.1.1";
    public string StaticDns { get; set; } = "8.8.8.8";

    // Live Telemetry phản hồi từ ST67 (RO)
    /// <summary>
    /// Trạng thái kết nối: 0=Disconnected, 1=Connecting, 2=Connected, 3=GotIp, 4=AuthFailed, 5=SsidNotFound, 6=ApMode.
    /// </summary>
    public ushort ConnectionStatus { get; set; } = 0;

    /// <summary>
    /// Cường độ sóng Wi-Fi thực tế (-100 dBm .. 0 dBm).
    /// </summary>
    public short RssiDbm { get; set; } = -100;

    /// <summary>
    /// Địa chỉ IP thực tế MCU đang sử dụng.
    /// </summary>
    public string CurrentIp { get; set; } = "0.0.0.0";

    /// <summary>
    /// Địa chỉ MAC phần cứng của module ST67.
    /// </summary>
    public string MacAddress { get; set; } = "00:00:00:00:00:00";
}

/// <summary>
/// DTO lệnh và trạng thái quét mạng Wi-Fi (0x1340..0x134F, 16 thanh ghi = 32 Bytes).
/// </summary>
public sealed class WifiScanControlDto
{
    public ushort Command { get; set; }
    public ushort ScanState { get; set; }
    public ushort ApFoundCount { get; set; }
    public ushort ErrorCode { get; set; }
}

/// <summary>
/// DTO thông tin 1 Access Point quét được (0x1350 + idx * 22, 22 thanh ghi = 44 Bytes).
/// </summary>
public sealed class WifiApInfoDto
{
    public string Ssid { get; set; } = string.Empty;
    public short RssiDbm { get; set; }
    public byte AuthMode { get; set; }
    public byte Channel { get; set; }
    public string Bssid { get; set; } = string.Empty;

    public string AuthModeDisplay => AuthMode switch
    {
        0 => "Open",
        1 => "WEP",
        2 => "WPA-PSK",
        3 => "WPA2-PSK",
        4 => "WPA/WPA2",
        5 => "WPA2-Enterprise",
        6 => "WPA3-PSK",
        _ => "Unknown"
    };

    public int SignalQualityPercent
    {
        get
        {
            if (RssiDbm <= -100) return 0;
            if (RssiDbm >= -50) return 100;
            return 2 * (RssiDbm + 100);
        }
    }
}
