using System.Threading.Tasks;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public sealed class WifiClientTests
{
    [Fact]
    public async Task WriteAndReadWifiConfig_Roundtrip_ShouldSucceed()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1");
        var wifiClient = new WifiClient(fakeClient);

        var config = new WifiConfigDto
        {
            Enabled = true,
            Mode = 0,
            IsDhcp = false,
            AutoReconnect = true,
            Ssid = "SmartFactory_ST67",
            Password = "SecurePassword2026",
            StaticIp = "192.168.1.88",
            StaticSubnet = "255.255.255.0",
            StaticGateway = "192.168.1.1",
            StaticDns = "8.8.8.8"
        };

        // Act
        await wifiClient.WriteWifiConfigAsync(1, config);
        var readConfig = await wifiClient.ReadWifiConfigAsync(1);

        // Assert
        Assert.True(readConfig.Enabled);
        Assert.Equal(0, readConfig.Mode);
        Assert.False(readConfig.IsDhcp);
        Assert.True(readConfig.AutoReconnect);
        Assert.Equal("SmartFactory_ST67", readConfig.Ssid);
        Assert.Equal("SecurePassword2026", readConfig.Password);
        Assert.Equal("192.168.1.88", readConfig.StaticIp);
        Assert.Equal("255.255.255.0", readConfig.StaticSubnet);
        Assert.Equal("192.168.1.1", readConfig.StaticGateway);
        Assert.Equal("8.8.8.8", readConfig.StaticDns);
    }

    [Fact]
    public async Task TriggerWifiCommand_ShouldWriteSingleRegister()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1");
        var wifiClient = new WifiClient(fakeClient);

        // Act
        await wifiClient.TriggerWifiCommandAsync(1, ModbusRegisterMap.WifiCmdStartScan);

        // Assert
        var status = await wifiClient.ReadWifiScanStatusAsync(1);
        Assert.Equal(ModbusRegisterMap.WifiCmdStartScan, status.Command);
    }

    [Fact]
    public async Task ReadWifiScanResults_ShouldDecodeFromSimulator()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1");
        var wifiClient = new WifiClient(fakeClient);

        // Ghi giả lập 1 AP vào bảng scan 0x1350 của Simulator qua internal memory của MCU
        var apRegisters = new ushort[ModbusRegisterMap.WifiRegistersPerAp];
        SimplePLC.Protocol.Codec.MqttTelemetryCodec.EncodePackedAscii("ST67_Demo_AP", apRegisters.AsSpan(0, 16), 32);
        apRegisters[16] = unchecked((ushort)(short)-50); // -50 dBm
        apRegisters[17] = (3 << 8) | 6; // WPA2-PSK, Ch 6
        apRegisters[18] = 0x0080;
        apRegisters[19] = 0xE101;
        apRegisters[20] = 0x0203;

        apRegisters.CopyTo(fakeClient.Simulator.Memory.InternalMutableSpan(ModbusRegisterMap.WifiScanTableBaseAddress, ModbusRegisterMap.WifiRegistersPerAp));

        // Act
        var results = await wifiClient.ReadWifiScanResultsAsync(1, 1);

        // Assert
        Assert.Single(results);
        Assert.Equal("ST67_Demo_AP", results[0].Ssid);
        Assert.Equal(-50, results[0].RssiDbm);
        Assert.Equal(3, results[0].AuthMode);
        Assert.Equal("WPA2-PSK", results[0].AuthModeDisplay);
        Assert.Equal(6, results[0].Channel);
        Assert.Equal("00:80:E1:01:02:03", results[0].Bssid);
    }
}
