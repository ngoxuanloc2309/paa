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

public class RuleTableWriterMqttFailSafeTests
{
    private static RuleRecordDto CreateTestRule(int index = 0)
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
    public async Task DeployRulesAsync_WithMqttAndValidCapabilities_DeploysSuccessfullyAndLogsFrames()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var mqttConfigs = new List<MqttTelemetryConfigDto>
        {
            new()
            {
                Topic = "spx/telemetry",
                IntervalMs = 1000,
                CustomTemplate = "{\"temp\":{VREG0}}"
            }
        };

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "192.168.1.100",
            Port = 1883
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: mqttConfigs,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert
        Assert.True(result.IsSuccess, $"Deploy failed: {result.ErrorCode} - {result.ErrorMessage}. Logs: {string.Join(" | ", logs)}");
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);
        Assert.True(result.ActiveVersion >= 1);

        // Verify wire logs generated
        Assert.Contains(logs, l => l.Contains("[CAPS] Reading hardware capabilities at 0x0A08"));
        Assert.Contains(logs, l => l.Contains("[CAPS] MCU capabilities: 0x") && l.Contains("MQTT_PUB: True"));
        Assert.Contains(logs, l => l.Contains("[MQTT PUB] Writing 96 registers to 0x0C00"));
        Assert.Contains(logs, l => l.Contains("[MQTT Broker] Writing 96 registers to 0x0E00"));
    }

    [Fact]
    public async Task DeployRulesAsync_WithMqttAndCapabilitiesTimeout_AbortsImmediatelyWithUnsupported()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Cấy lỗi: Timeout khi đọc địa chỉ 0x0A08
        client.Simulator.Control.Faults.TimeoutOnAddress = ModbusRegisterMap.HardwareCapabilitiesAddress;

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var mqttConfigs = new List<MqttTelemetryConfigDto>
        {
            new() { Topic = "spx/telemetry", IntervalMs = 1000 }
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: mqttConfigs,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert - Fail Fast
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.UNSUPPORTED, result.ErrorCode);
        Assert.Contains("0x0A08", result.ErrorMessage);
        Assert.Contains("Deployment aborted for safety", result.ErrorMessage);

        // Verify that staging was NOT committed
        Assert.Equal(0, client.Simulator.Memory.InternalRead(ModbusRegisterMap.ActiveRuleVersionAddress));
        Assert.Contains(logs, l => l.Contains("[CAPS WARNING] Error reading 0x0A08"));
    }

    [Fact]
    public async Task DeployRulesAsync_WithMqttAndMissingCapabilities_AbortsImmediatelyWithUnsupported()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Ghi đè caps = 0x00000001 (chỉ hỗ trợ BASE_LOGIC, không có MQTT_PUB)
        client.Simulator.Memory.InternalWrite(ModbusRegisterMap.HardwareCapabilitiesAddress, 0);
        client.Simulator.Memory.InternalWrite((ushort)(ModbusRegisterMap.HardwareCapabilitiesAddress + 1), 1);

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var mqttConfigs = new List<MqttTelemetryConfigDto>
        {
            new() { Topic = "spx/telemetry", IntervalMs = 1000 }
        };

        // Act
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: mqttConfigs,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert - Fail Fast
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.UNSUPPORTED, result.ErrorCode);
        Assert.Contains("missing SPLC_CAP_MQTT_PUB at 0x0A08", result.ErrorMessage);
        Assert.Contains("Deployment aborted for safety", result.ErrorMessage);

        // Verify that staging was NOT committed
        Assert.Equal(0, client.Simulator.Memory.InternalRead(ModbusRegisterMap.ActiveRuleVersionAddress));
    }

    [Fact]
    public async Task DeployRulesAsync_WithoutMqttAndCapabilitiesTimeout_SucceedsNormally()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Cấy lỗi: Timeout khi đọc 0x0A08
        client.Simulator.Control.Faults.TimeoutOnAddress = ModbusRegisterMap.HardwareCapabilitiesAddress;

        var writer = new RuleTableWriter(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        // Act - Không cấu hình MQTT hay Broker
        var result = await writer.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert - Vẫn deploy thành công bình thường cho logic thuần túy
        Assert.True(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);
        Assert.True(result.ActiveVersion >= 1);
    }
}
