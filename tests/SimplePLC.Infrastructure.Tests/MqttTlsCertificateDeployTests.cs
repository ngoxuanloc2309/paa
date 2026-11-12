using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Bộ kiểm thử nạp chứng chỉ bảo mật MQTT TLS và mTLS xuống thiết bị PLC (SPLC-SPEC-MQTT-003).
/// </summary>
public class MqttTlsCertificateDeployTests
{
    private static RuleRecordDto CreateTestRule() => new()
    {
        ThresholdLo = 10,
        ThresholdHi = 100,
        ForMs = 0,
        ActionParam = 1,
        TriggerTag = 0,
        ActionTag = 8,
        GuardTag = 0xFFFF,
        Enabled = true,
        TriggerType = SPLC_TriggerType.ON_RISE,
        CompareOp = SPLC_CompareOp.GT,
        ActionType = SPLC_ActionType.SET_TAG
    };

    private const string SampleRootCaPem =
        "-----BEGIN CERTIFICATE-----\n" +
        "MIIB/zCCAaSgAwIBAgIUQ1234567890ABCDEFGHIKLMNOPQ==\n" +
        "MIIB/zCCAaSgAwIBAgIUQ1234567890ABCDEFGHIKLMNOPQ==\n" +
        "-----END CERTIFICATE-----\n";

    private const string SampleClientCertPem =
        "-----BEGIN CERTIFICATE-----\n" +
        "MIICADCCAYigAwIBAgIUX9876543210ZYXWVUTSRQPONML==\n" +
        "MIICADCCAYigAwIBAgIUX9876543210ZYXWVUTSRQPONML==\n" +
        "-----END CERTIFICATE-----\n";

    private const string SampleClientKeyPem =
        "-----BEGIN PRIVATE KEY-----\n" +
        "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg...\n" +
        "MIGHAgEAMBMGByqGSM49AgEGCCqGSM49AwEHBG0wawIBAQQg...\n" +
        "-----END PRIVATE KEY-----\n";

    [Fact]
    public async Task Deploy_OneWayTls_UploadsRootCaAndCommits()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "broker.hivemq.com",
            Port = 8883,
            UseTls = true
        };

        var rootCaBytes = Encoding.UTF8.GetBytes(SampleRootCaPem);
        ushort expectedCrc = Crc16Modbus.Compute(rootCaBytes);

        var certs = new MqttTlsCertificatesDto
        {
            RootCaBytes = rootCaBytes
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            tlsCertificates: certs,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert
        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}. Logs: {string.Join(" | ", logs)}");
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);

        // Verify simulator memory state
        ushort activeMask = client.Simulator.Memory.InternalRead(ModbusRegisterMap.MqttCertActiveTypeMaskAddress);
        Assert.Equal(0x0001, activeMask & 0x0001); // Root CA bit set

        ushort storedCrc = client.Simulator.Memory.InternalRead(ModbusRegisterMap.MqttCertCrcRootCaAddress);
        Assert.Equal(expectedCrc, storedCrc);

        Assert.Contains(logs, l => l.Contains("[MQTT TLS] Slicing Root CA"));
        Assert.Contains(logs, l => l.Contains("[MQTT TLS OK] Root CA committed successfully"));
    }

    [Fact]
    public async Task Deploy_SmartSync_SkipsUploadWhenCrcMatches()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "broker.hivemq.com",
            Port = 8883,
            UseTls = true
        };

        var rootCaBytes = Encoding.UTF8.GetBytes(SampleRootCaPem);
        var certs = new MqttTlsCertificatesDto
        {
            RootCaBytes = rootCaBytes
        };

        // Act 1: Initial Deploy
        var result1 = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            tlsCertificates: certs,
            cancellationToken: default,
            progressLogger: logs.Add);
        Assert.True(result1.IsSuccess);

        // Act 2: Second Deploy with identical certificates
        var logs2 = new List<string>();
        var result2 = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            tlsCertificates: certs,
            cancellationToken: default,
            progressLogger: logs2.Add);

        // Assert 2
        Assert.True(result2.IsSuccess);
        Assert.Contains(logs2, l => l.Contains("Root CA is up-to-date on MCU (CRC-16 matched") && l.Contains("Skipping chunk upload"));
        Assert.DoesNotContain(logs2, l => l.Contains("Slicing Root CA"));
    }

    [Fact]
    public async Task Deploy_MutualTls_UploadsAllThreeCertificatesInOrder()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "broker.hivemq.com",
            Port = 8883,
            UseTls = true,
            UseMtls = true
        };

        var rootCaBytes = Encoding.UTF8.GetBytes(SampleRootCaPem);
        var clientCertBytes = Encoding.UTF8.GetBytes(SampleClientCertPem);
        var clientKeyBytes = Encoding.UTF8.GetBytes(SampleClientKeyPem);

        ushort rootCaCrc = Crc16Modbus.Compute(rootCaBytes);
        ushort clientCertCrc = Crc16Modbus.Compute(clientCertBytes);
        ushort clientKeyCrc = Crc16Modbus.Compute(clientKeyBytes);

        var certs = new MqttTlsCertificatesDto
        {
            RootCaBytes = rootCaBytes,
            ClientCertBytes = clientCertBytes,
            ClientKeyBytes = clientKeyBytes
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            tlsCertificates: certs,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert
        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}. Logs: {string.Join(" | ", logs)}");

        ushort activeMask = client.Simulator.Memory.InternalRead(ModbusRegisterMap.MqttCertActiveTypeMaskAddress);
        Assert.Equal(0x0007, activeMask & 0x0007); // Bit 0 (CA), Bit 1 (Cert), Bit 2 (Key) all set

        Assert.Equal(rootCaCrc, client.Simulator.Memory.InternalRead(ModbusRegisterMap.MqttCertCrcRootCaAddress));
        Assert.Equal(clientCertCrc, client.Simulator.Memory.InternalRead(ModbusRegisterMap.MqttCertCrcClientCertAddress));
        Assert.Equal(clientKeyCrc, client.Simulator.Memory.InternalRead(ModbusRegisterMap.MqttCertCrcClientKeyAddress));

        Assert.Contains(logs, l => l.Contains("[MQTT TLS OK] Root CA committed successfully"));
        Assert.Contains(logs, l => l.Contains("[MQTT TLS OK] Client Certificate committed successfully"));
        Assert.Contains(logs, l => l.Contains("[MQTT TLS OK] Client Private Key committed successfully"));
    }

    [Fact]
    public async Task Deploy_DeltaCertChange_OnlyUploadsChangedCertificate()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "broker.hivemq.com",
            Port = 8883,
            UseTls = true,
            UseMtls = true
        };

        var rootCaBytes = Encoding.UTF8.GetBytes(SampleRootCaPem);
        var clientCertBytes = Encoding.UTF8.GetBytes(SampleClientCertPem);
        var initialKeyBytes = Encoding.UTF8.GetBytes(SampleClientKeyPem);

        var certs = new MqttTlsCertificatesDto
        {
            RootCaBytes = rootCaBytes,
            ClientCertBytes = clientCertBytes,
            ClientKeyBytes = initialKeyBytes
        };

        // Deploy 1: All 3 uploaded
        var res1 = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            tlsCertificates: certs,
            cancellationToken: default);
        Assert.True(res1.IsSuccess);

        // Modify only Client Key
        var modifiedKeyBytes = Encoding.UTF8.GetBytes(SampleClientKeyPem + "\n# Modified Key Material");
        ushort newKeyCrc = Crc16Modbus.Compute(modifiedKeyBytes);

        var certs2 = new MqttTlsCertificatesDto
        {
            RootCaBytes = rootCaBytes,
            ClientCertBytes = clientCertBytes,
            ClientKeyBytes = modifiedKeyBytes
        };

        var logs2 = new List<string>();
        var res2 = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            tlsCertificates: certs2,
            cancellationToken: default,
            progressLogger: logs2.Add);

        // Assert
        Assert.True(res2.IsSuccess);
        Assert.Contains(logs2, l => l.Contains("Root CA is up-to-date on MCU") && l.Contains("Skipping chunk upload"));
        Assert.Contains(logs2, l => l.Contains("Client Certificate is up-to-date on MCU") && l.Contains("Skipping chunk upload"));
        Assert.Contains(logs2, l => l.Contains("Slicing Client Private Key"));
        Assert.Contains(logs2, l => l.Contains("[MQTT TLS OK] Client Private Key committed successfully"));
        Assert.Equal(newKeyCrc, client.Simulator.Memory.InternalRead(ModbusRegisterMap.MqttCertCrcClientKeyAddress));
    }

    [Fact]
    public async Task Deploy_MissingHardwareCapability_AbortsFailFast()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Clear MQTT_TLS_CERT bit (0x0080) from simulator capabilities (0x0A08)
        var originalCaps = (uint)RegisterCodec.DecodeHardwareCapabilities(new ushort[]
        {
            client.Simulator.Memory.InternalRead(ModbusRegisterMap.HardwareCapabilitiesAddress),
            client.Simulator.Memory.InternalRead(ModbusRegisterMap.HardwareCapabilitiesAddress + 1)
        });

        uint disabledTlsCaps = originalCaps & ~(uint)SPLC_HardwareCapabilities.MQTT_TLS_CERT;
        ushort[] rawCaps = new ushort[2];
        RegisterCodec.EncodeHardwareCapabilities((SPLC_HardwareCapabilities)disabledTlsCaps, rawCaps);
        client.Simulator.Memory.InternalWrite(ModbusRegisterMap.HardwareCapabilitiesAddress, rawCaps[0]);
        client.Simulator.Memory.InternalWrite(ModbusRegisterMap.HardwareCapabilitiesAddress + 1, rawCaps[1]);

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "broker.hivemq.com",
            Port = 8883,
            UseTls = true
        };

        var certs = new MqttTlsCertificatesDto
        {
            RootCaBytes = Encoding.UTF8.GetBytes(SampleRootCaPem)
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            tlsCertificates: certs,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert - Fail Fast
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.UNSUPPORTED, result.ErrorCode);
        Assert.Contains("SPLC_CAP_MQTT_TLS_CERT", result.ErrorMessage);
        Assert.Contains("Deployment aborted for safety", result.ErrorMessage);
    }
}
