using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class FunctionBlockGatewayTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai(wireProfile: 2);

    [Fact]
    public async Task WriteAndReadFunctionBlocks_Roundtrip_Succeeds()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var gateway = new FunctionBlockGateway(fakeClient);

        var timers = new List<FbTimerRecordDto>
        {
            new() { Mode = SPLC_TimerMode.TON, PresetMs = 5000, ElapsedMs = 1200, StatusBits = ModbusRegisterMap.FbTimerStatusBitRunning },
            new() { Mode = SPLC_TimerMode.TOF, PresetMs = 3000, ElapsedMs = 0, StatusBits = ModbusRegisterMap.FbTimerStatusBitQ }
        };

        var counters = new List<FbCounterRecordDto>
        {
            new() { Mode = SPLC_CounterMode.CTU, PresetValue = 50, CurrentValue = 18, RetainTagIndex = 84, StatusBits = ModbusRegisterMap.FbCounterStatusBitCu }
        };

        // Act: Write
        await gateway.WriteFunctionBlocksAsync(1, timers, counters);

        // Act: Read back
        var (readTimers, readCounters) = await gateway.ReadFunctionBlocksAsync(1);

        // Assert: 8 timers and 8 counters returned (full FB map)
        Assert.Equal(8, readTimers.Count);
        Assert.Equal(8, readCounters.Count);

        // Timer 0
        Assert.Equal(SPLC_TimerMode.TON, readTimers[0].Mode);
        Assert.Equal(5000u, readTimers[0].PresetMs);
        Assert.Equal(1200u, readTimers[0].ElapsedMs);
        Assert.True(readTimers[0].Running);

        // Timer 1
        Assert.Equal(SPLC_TimerMode.TOF, readTimers[1].Mode);
        Assert.Equal(3000u, readTimers[1].PresetMs);
        Assert.True(readTimers[1].Q);

        // Counter 0
        Assert.Equal(SPLC_CounterMode.CTU, readCounters[0].Mode);
        Assert.Equal(50, readCounters[0].PresetValue);
        Assert.Equal(18, readCounters[0].CurrentValue);
        Assert.Equal(84, readCounters[0].RetainTagIndex);
        Assert.True(readCounters[0].Cu);
    }

    [Fact]
    public async Task DeployRulesUseCase_WithFunctionBlocks_DeploysFBsAndRulesToMCU()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var writer = new RuleTableWriter(fakeClient);
        var useCase = new DeployRulesUseCase(writer);

        var table = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        table.AddRule(new Rule(0, "R1", new TriggerModel(di0, TriggerKind.OnRise), new ActionModel(do0, ActionKind.SetTag, 1)));

        var timers = new List<FbTimerRecordDto>
        {
            new() { Mode = SPLC_TimerMode.TON, PresetMs = 4000 }
        };

        var counters = new List<FbCounterRecordDto>
        {
            new() { Mode = SPLC_CounterMode.CTU, PresetValue = 100, RetainTagIndex = 85 }
        };

        // Act
        var result = await useCase.ExecuteAsync(table, timers, counters, slaveId: 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.DeployedRuleCount);

        // Verify Timer 0 written at 0x0B00
        var timerRegs = await fakeClient.ReadHoldingRegistersAsync(1, ModbusRegisterMap.FbTimerTableBaseAddress, 8);
        Assert.Equal(1, timerRegs[1]); // TON
        Assert.Equal(4000u, (uint)((timerRegs[2] << 16) | timerRegs[3])); // PresetMs = 4000

        // Verify Counter 0 written at 0x0B40
        var counterRegs = await fakeClient.ReadHoldingRegistersAsync(1, ModbusRegisterMap.FbCounterTableBaseAddress, 8);
        Assert.Equal(1, counterRegs[1]); // CTU
        Assert.Equal(100, (counterRegs[2] << 16) | counterRegs[3]); // PV = 100
        Assert.Equal(85, counterRegs[6]); // RetainTagIndex = 85
    }

    [Fact]
    public async Task DeployRulesUseCase_WithMqttConfigsAndBroker_DeploysMqttAndRulesToMCU()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var writer = new RuleTableWriter(fakeClient);
        var useCase = new DeployRulesUseCase(writer);

        var table = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        table.AddRule(new Rule(0, "R1", new TriggerModel(di0, TriggerKind.OnRise), new ActionModel(do0, ActionKind.SetTag, 1)));

        var mqttConfigs = new List<MqttTelemetryConfigDto>
        {
            new()
            {
                Topic = "plant/furnace/telemetry",
                PublishMode = "COV",
                IntervalMs = 2500,
                Deadband = 1.25,
                Qos = 1,
                Retain = true,
                PayloadMode = "JSON",
                Fields = new List<MqttPayloadFieldDto>
                {
                    new() { Key = "di0", TagIndex = di0.TagIndex }
                }
            }
        };

        var brokerConfig = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "192.168.1.150",
            Port = 1883,
            UseTls = false,
            ClientId = "SPLC-UNIT-01",
            Username = "operator",
            Password = "secret"
        };

        // Act
        var result = await useCase.ExecuteAsync(table, timers: null, counters: null, mqttConfigs, brokerConfig, slaveId: 1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.DeployedRuleCount);

        // Verify MQTT Telemetry Block 0 written at 0x0C00 (96 registers)
        var telemetryRegs = await fakeClient.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttTelemetryTableBaseAddress, ModbusRegisterMap.MqttRegistersPerBlock);
        var decodedBlock = SimplePLC.Protocol.Codec.MqttTelemetryCodec.DecodeBlock(telemetryRegs);
        Assert.Equal("plant/furnace/telemetry", decodedBlock.Topic);
        Assert.Equal(2500u, decodedBlock.IntervalMs);
        Assert.Equal(1.25, decodedBlock.Deadband, precision: 2);
        Assert.Equal(1, decodedBlock.Qos);
        Assert.True(decodedBlock.Retain);
        Assert.Equal("COV", decodedBlock.PublishMode);

        // Verify MQTT Broker Config written at 0x0E00 (96 registers)
        var brokerRegs = await fakeClient.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttBrokerConfigBaseAddress, ModbusRegisterMap.MqttBrokerConfigRegisters);
        var decodedBroker = SimplePLC.Protocol.Codec.MqttBrokerConfigCodec.Decode(brokerRegs);
        Assert.True(decodedBroker.Enabled);
        Assert.Equal("192.168.1.150", decodedBroker.Host);
        Assert.Equal(1883, decodedBroker.Port);
        Assert.Equal("SPLC-UNIT-01", decodedBroker.ClientId);
        Assert.Equal("operator", decodedBroker.Username);
        Assert.Equal("secret", decodedBroker.Password);
    }
}
