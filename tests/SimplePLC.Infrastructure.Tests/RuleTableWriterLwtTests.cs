using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

/// <summary>
/// Kiểm thử hợp đồng giao tiếp MQTT Last Will & Testament (LWT, 0x0E60) của RuleTableWriter.
/// Tuân thủ chuẩn SPLC-SPEC-MQTT-004.
/// </summary>
public class RuleTableWriterLwtTests
{
    private static RuleRecordDto CreateTestRule()
    {
        return new RuleRecordDto
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
    }

    [Fact]
    public async Task RuleTableWriter_WhenLwtEnabled_Writes32RegistersTo0x0E60()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "192.168.1.100",
            Port = 1883
        };

        var willConfig = new MqttWillConfigDto
        {
            Enabled = true,
            Topic = "factory/node1/status",
            Payload = "offline",
            Qos = 1,
            Retain = true
        };

        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: null,
            directWriteConfigs: null,
            modbusPortConfigs: null,
            slaveMapConfigs: null,
            tagDataTypes: null,
            dtuConfig: null,
            mqttWillConfig: willConfig,
            tlsCertificates: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}");
        Assert.Contains(logs, l => l.Contains("[MQTT LWT] Writing 32 registers to 0x0E60") && l.Contains("Will Topic: factory/node1/status"));
    }

    [Fact]
    public async Task RuleTableWriter_WhenLwtDisabled_WritesZeroRegistersTo0x0E60()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "192.168.1.100",
            Port = 1883
        };

        // LWT is explicitly disabled
        var willConfig = new MqttWillConfigDto
        {
            Enabled = false,
            Topic = "factory/node1/status",
            Payload = "offline",
            Qos = 1,
            Retain = true
        };

        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: null,
            directWriteConfigs: null,
            modbusPortConfigs: null,
            slaveMapConfigs: null,
            tagDataTypes: null,
            dtuConfig: null,
            mqttWillConfig: willConfig,
            tlsCertificates: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}");
        // Must log safe disable clearing 32 registers at 0x0E60
        Assert.Contains(logs, l => l.Contains("[MQTT LWT] Disabling LWT: clearing 32 registers at 0x0E60"));
    }
}
