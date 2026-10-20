using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public sealed class WifiCodecTests
{
    [Fact]
    public void EncodeAndDecodeConfig_Roundtrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new WifiConfigDto
        {
            Enabled = true,
            Mode = 0, // STA
            IsDhcp = false,
            AutoReconnect = true,
            Ssid = "SynaptiX_Industrial_5G",
            Password = "FactorySecureKey!2026",
            StaticIp = "192.168.10.50",
            StaticSubnet = "255.255.255.0",
            StaticGateway = "192.168.10.1",
            StaticDns = "1.1.1.1"
        };

        var registers = new ushort[ModbusRegisterMap.WifiConfigRegisters];

        // Act
        WifiCodec.EncodeConfig(original, registers);
        var decoded = WifiCodec.DecodeConfig(registers);

        // Assert
        Assert.Equal(original.Enabled, decoded.Enabled);
        Assert.Equal(original.Mode, decoded.Mode);
        Assert.Equal(original.IsDhcp, decoded.IsDhcp);
        Assert.Equal(original.AutoReconnect, decoded.AutoReconnect);
        Assert.Equal(original.Ssid, decoded.Ssid);
        Assert.Equal(original.Password, decoded.Password);
        Assert.Equal(original.StaticIp, decoded.StaticIp);
        Assert.Equal(original.StaticSubnet, decoded.StaticSubnet);
        Assert.Equal(original.StaticGateway, decoded.StaticGateway);
        Assert.Equal(original.StaticDns, decoded.StaticDns);
    }

    [Fact]
    public void EncodeConfig_ControlFlags_ShouldPackBitsCorrectly()
    {
        // Arrange: Enabled=true (bit0: 1), Mode=1 (bit1..2: 1 << 1 = 2), IsDhcp=true (bit3: 8), AutoReconnect=true (bit4: 16)
        // Tổng: 1 + 2 + 8 + 16 = 27 = 0x001B
        var config = new WifiConfigDto
        {
            Enabled = true,
            Mode = 1,
            IsDhcp = true,
            AutoReconnect = true,
            Ssid = "TestAP"
        };

        var registers = new ushort[ModbusRegisterMap.WifiConfigRegisters];

        // Act
        WifiCodec.EncodeConfig(config, registers);

        // Assert
        Assert.Equal(0x001B, registers[0]);
    }

    [Fact]
    public void DecodeConfig_LiveTelemetry_ShouldParseCorrectly()
    {
        // Arrange
        var registers = new ushort[ModbusRegisterMap.WifiConfigRegisters];
        registers[0] = 0x0009; // Enabled + DHCP
        registers[58] = 3;      // conn_status = GOT_IP
        registers[59] = unchecked((ushort)(short)-65); // -65 dBm
        // IP: 192.168.1.150 -> 0xC0A8, 0x0196
        registers[60] = 0xC0A8;
        registers[61] = 0x0196;
        // MAC: 00:80:E1:12:34:56
        registers[62] = 0x0080;
        registers[63] = 0xE112;

        // Act
        var decoded = WifiCodec.DecodeConfig(registers);

        // Assert
        Assert.Equal(3, decoded.ConnectionStatus);
        Assert.Equal(-65, decoded.RssiDbm);
        Assert.Equal("192.168.1.150", decoded.CurrentIp);
    }

    [Fact]
    public void EncodeAndDecodeControl_Roundtrip_ShouldPreserveValues()
    {
        // Arrange
        var control = new WifiScanControlDto
        {
            Command = ModbusRegisterMap.WifiCmdStartScan,
            ScanState = ModbusRegisterMap.WifiScanStateScanning,
            ApFoundCount = 3,
            ErrorCode = 0
        };

        var registers = new ushort[ModbusRegisterMap.WifiControlRegisters];

        // Act
        WifiCodec.EncodeControl(control, registers);
        var decoded = WifiCodec.DecodeControl(registers);

        // Assert
        Assert.Equal(ModbusRegisterMap.WifiCmdStartScan, decoded.Command);
        Assert.Equal(ModbusRegisterMap.WifiScanStateScanning, decoded.ScanState);
        Assert.Equal(3, decoded.ApFoundCount);
        Assert.Equal(0, decoded.ErrorCode);
    }

    [Fact]
    public void DecodeScanResults_MultipleAps_ShouldParseCorrectly()
    {
        // Arrange: 2 APs in scan table
        var registers = new ushort[ModbusRegisterMap.WifiScanTableRegisters];

        // AP 0: "Factory_5G", RSSI=-55, Auth=WPA2(3), Chan=6
        MqttTelemetryCodec.EncodePackedAscii("Factory_5G", registers.AsSpan(0, 16), 32);
        registers[16] = unchecked((ushort)(short)-55);
        registers[17] = (3 << 8) | 6; // AuthMode=3, Channel=6
        // MAC: AA:BB:CC:DD:EE:FF
        registers[18] = 0xAABB;
        registers[19] = 0xCCDD;
        registers[20] = 0xEEFF;

        // AP 1: "Guest_WiFi", RSSI=-78, Auth=Open(0), Chan=11
        MqttTelemetryCodec.EncodePackedAscii("Guest_WiFi", registers.AsSpan(22, 16), 32);
        registers[22 + 16] = unchecked((ushort)(short)-78);
        registers[22 + 17] = (0 << 8) | 11;

        // Act
        var aps = WifiCodec.DecodeScanResults(registers, 2);

        // Assert
        Assert.Equal(2, aps.Count);

        Assert.Equal("Factory_5G", aps[0].Ssid);
        Assert.Equal(-55, aps[0].RssiDbm);
        Assert.Equal(3, aps[0].AuthMode);
        Assert.Equal("WPA2-PSK", aps[0].AuthModeDisplay);
        Assert.Equal(6, aps[0].Channel);
        Assert.Equal("AA:BB:CC:DD:EE:FF", aps[0].Bssid);

        Assert.Equal("Guest_WiFi", aps[1].Ssid);
        Assert.Equal(-78, aps[1].RssiDbm);
        Assert.Equal(0, aps[1].AuthMode);
        Assert.Equal("Open", aps[1].AuthModeDisplay);
        Assert.Equal(11, aps[1].Channel);
    }

    [Theory]
    [InlineData("192.168.1.1", 0xC0A8, 0x0101)]
    [InlineData("10.0.0.254", 0x0A00, 0x00FE)]
    [InlineData("255.255.255.0", 0xFFFF, 0xFF00)]
    public void EncodeAndDecodeIpAddress_ShouldConvertAccurately(string ip, ushort reg0, ushort reg1)
    {
        var regs = new ushort[2];
        WifiCodec.EncodeIpAddress(ip, regs);

        Assert.Equal(reg0, regs[0]);
        Assert.Equal(reg1, regs[1]);

        string decoded = WifiCodec.DecodeIpAddress(regs);
        Assert.Equal(ip, decoded);
    }
}
