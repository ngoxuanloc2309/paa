using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class RuleTableGatewayDeployTests
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
    public async Task DeployRulesAsync_ThroughGateway_WithTimersCountersMqttBroker_WritesAllHoldingRegistersAndLogsProgress()
    {
        // Arrange
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // RuleTableGateway bọc RuleTableWriter
        var gateway = new RuleTableGateway(client);

        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var timers = new List<FbTimerRecordDto>
        {
            new()
            {
                Mode = SPLC_TimerMode.TON,
                PresetMs = 5000,
                RuleRef = 1
            }
        };

        var counters = new List<FbCounterRecordDto>
        {
            new()
            {
                Mode = SPLC_CounterMode.CTU,
                PresetValue = 10
            }
        };

        var mqttConfigs = new List<MqttTelemetryConfigDto>
        {
            new()
            {
                Topic = "spx/hub/telemetry",
                IntervalMs = 1000,
                CustomTemplate = "{\"counter\":{VREG0}}"
            }
        };

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "192.168.1.50",
            Port = 1883,
            ClientId = "SPX_GATEWAY_01"
        };

        // Act: Nạp trực tiếp qua IRuleTableWriter (RuleTableGateway) với overload 10 tham số
        var result = await gateway.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: timers,
            counters: counters,
            mqttConfigs: mqttConfigs,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert
        Assert.True(result.IsSuccess, $"Deploy qua Gateway thất bại: {result.ErrorCode} - {result.ErrorMessage}. Logs: {string.Join(" | ", logs)}");
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);
        Assert.True(result.ActiveVersion >= 1);

        // 1. Kiểm tra log dây viễn trắc [CAPS] và [MQTT] phải được phát ra
        Assert.Contains(logs, l => l.Contains("[CAPS] Reading hardware capabilities at 0x0A08"));
        Assert.Contains(logs, l => l.Contains("[CAPS] MCU capabilities: 0x") && l.Contains("MQTT_PUB: True"));
        Assert.Contains(logs, l => l.Contains("[MQTT PUB] Writing 96 registers to 0x0C00"));
        Assert.Contains(logs, l => l.Contains("[MQTT Broker] Writing 96 registers to 0x0E00"));

        // 2. Kiểm tra bộ nhớ thanh ghi thực tế trong Simulator
        var mem = client.Simulator.Memory;

        // Timer tại 0x0B00: Chế độ TON (1) tại Offset +1
        ushort timerModeReg = mem.InternalRead((ushort)(ModbusRegisterMap.FbTimerTableBaseAddress + 1));
        Assert.Equal((ushort)SPLC_TimerMode.TON, timerModeReg);

        // Counter tại 0x0B40: Chế độ CTU (1) tại Offset +1
        ushort counterModeReg = mem.InternalRead((ushort)(ModbusRegisterMap.FbCounterTableBaseAddress + 1));
        Assert.Equal((ushort)SPLC_CounterMode.CTU, counterModeReg);

        // MQTT Telemetry tại 0x0C00: Control flags có cờ Enabled (0x0001) tại Offset +0
        ushort telemetryControl = mem.InternalRead(ModbusRegisterMap.MqttTelemetryTableBaseAddress);
        Assert.NotEqual(0, telemetryControl);
        Assert.True((telemetryControl & ModbusRegisterMap.MqttControlBitEnabled) != 0);

        // MQTT Broker tại 0x0E00: Host tại Offset +4
        ushort brokerHostFirstReg = mem.InternalRead((ushort)(ModbusRegisterMap.MqttBrokerConfigBaseAddress + 4));
        Assert.NotEqual(0, brokerHostFirstReg);

        // Port tại 0x0E01 = 1883 (Offset +1)
        ushort brokerPort = mem.InternalRead((ushort)(ModbusRegisterMap.MqttBrokerConfigBaseAddress + 1));
        Assert.Equal(1883, brokerPort);

        // Active Table 0x0010 có 1 active rule
        ushort activeRuleCount = mem.InternalRead(ModbusRegisterMap.ActiveRuleCountAddress);
        Assert.Equal(1, activeRuleCount);
    }

    [Fact]
    public async Task DeployRulesAsync_ThroughGateway_WhenMqttNotSupported_AbortsImmediatelyWithFailSafe()
    {
        // Arrange
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        // Giả lập MCU chỉ có BASE_LOGIC (không có cờ MQTT_PUB)
        client.Simulator.Memory.InternalWrite(
            ModbusRegisterMap.HardwareCapabilitiesAddress,
            (ushort)SPLC_HardwareCapabilities.BASE_LOGIC);
        client.Simulator.Memory.InternalWrite(
            (ushort)(ModbusRegisterMap.HardwareCapabilitiesAddress + 1),
            0x0000);

        var gateway = new RuleTableGateway(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var mqttConfigs = new List<MqttTelemetryConfigDto>
        {
            new() { Topic = "spx/telemetry", IntervalMs = 1000 }
        };

        // Act
        var result = await gateway.DeployRulesAsync(
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

        // Assert - Khóa an toàn Fail-Safe kích hoạt ngay qua Gateway
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.UNSUPPORTED, result.ErrorCode);
        Assert.Contains("SPLC_CAP_MQTT_PUB", result.ErrorMessage);
        Assert.Contains("Device does not support MQTT Telemetry PUB", result.ErrorMessage);
    }

    [Fact]
    public async Task DeployRulesAsync_WithModbusMasterWriteAndDirectWrite_DeploysToPartitions0x1100And0x1200()
    {
        // Arrange
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var gateway = new RuleTableGateway(client);
        var rules = new List<RuleRecordDto> { CreateTestRule() };
        var logs = new List<string>();

        var mbWriteConfigs = new List<ModbusMasterWriteConfigDto>
        {
            new()
            {
                Enabled = true,
                SlaveId = 2,
                FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
                StartAddress = 40001,
                PointCount = 1
            }
        };

        var directWriteConfigs = new List<DirectWriteConfigDto>
        {
            new()
            {
                TargetPin = DirectWriteTargetPin.Do3,
                Mode = DirectWriteMode.AddressableBus,
                ChannelCount = 18,
                IsExclusiveChannel = true
            }
        };

        // Act
        var result = await gateway.DeployRulesAsync(
            slaveId: 1,
            rules: rules,
            timers: null,
            counters: null,
            mqttConfigs: null,
            brokerConfig: null,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            modbusMasterWriteConfigs: mbWriteConfigs,
            directWriteConfigs: directWriteConfigs,
            cancellationToken: default,
            progressLogger: logs.Add);

        // Assert
        Assert.True(result.IsSuccess, $"Deploy qua Gateway thất bại: {result.ErrorCode} - {result.ErrorMessage}. Logs: {string.Join(" | ", logs)}");
        Assert.Contains(logs, l => l.Contains("[MB WRITE] Writing 64 registers to 0x1100"));
        Assert.Contains(logs, l => l.Contains("[PULSE_OUT] Writing 16 registers to 0x1200"));

        var mem = client.Simulator.Memory;
        ushort mbWriteControl = mem.InternalRead(ModbusRegisterMap.ModbusMasterWriteTableBaseAddress);
        Assert.Equal(1, mbWriteControl & 0x0001);

        ushort dwReg0 = mem.InternalRead(ModbusRegisterMap.DirectWriteTableBaseAddress);
        Assert.Equal(0x0303, dwReg0);
    }
}
