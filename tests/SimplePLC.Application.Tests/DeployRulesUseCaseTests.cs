using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public class DeployRulesUseCaseTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public async Task ExecuteAsync_ValidRules_DeploysSuccessfullyToMCU()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var writer = new RuleTableWriter(fakeClient);
        var useCase = new DeployRulesUseCase(writer);

        var table = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var trigger = new TriggerModel(di0, TriggerKind.OnRise);
        var action = new ActionModel(do0, ActionKind.SetTag, 1);
        table.AddRule(new Rule(0, "R1", trigger, action));

        // Act
        var result = await useCase.ExecuteAsync(table);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.DeployedRuleCount);
        Assert.Equal(SPLC_ErrorCode.NONE, result.ErrorCode);

        // Verify MCU Active Table count at 0x0010
        var activeCountReg = await fakeClient.ReadHoldingRegistersAsync(1, ModbusRegisterMap.RuleTableInfoAddress, 1);
        Assert.Equal(1, activeCountReg[0]);
    }

    [Fact]
    public async Task ExecuteAsync_DomainValidationFails_RejectsWithoutCallingHardware()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var writer = new RuleTableWriter(fakeClient);
        var useCase = new DeployRulesUseCase(writer);

        var table = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var di1 = _product.FindTagByName("DI1")!; // Read-Only!

        // Target tag is Read-Only DI1!
        var trigger = new TriggerModel(di0, TriggerKind.OnChange);
        var action = new ActionModel(di1, ActionKind.SetTag, 1);
        table.AddRule(new Rule(0, "InvalidRule", trigger, action));

        // Act
        var result = await useCase.ExecuteAsync(table);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotEmpty(result.ValidationErrors);
        Assert.Contains(result.ValidationErrors, e => e.Code == "ERR_ACTION_TAG_READONLY");

        // MCU should have 0 active rules
        var activeCountReg = await fakeClient.ReadHoldingRegistersAsync(1, ModbusRegisterMap.RuleTableInfoAddress, 1);
        Assert.Equal(0, activeCountReg[0]);
    }

    [Fact]
    public async Task ExecuteAsync_HardwareErrorInjected_ReturnsHardwareErrorResult()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        fakeClient.CorruptStagingCrcOnCommit = true;

        var writer = new RuleTableWriter(fakeClient);
        var useCase = new DeployRulesUseCase(writer);

        var table = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        table.AddRule(new Rule(0, "R1", new TriggerModel(di0, TriggerKind.OnChange), new ActionModel(do0, ActionKind.ToggleTag)));

        // Act
        var result = await useCase.ExecuteAsync(table);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(SPLC_ErrorCode.CRC_MISMATCH, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_ThrowsOperationCanceledException()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var writer = new RuleTableWriter(fakeClient);
        var useCase = new DeployRulesUseCase(writer);

        var table = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        table.AddRule(new Rule(0, "R1", new TriggerModel(di0, TriggerKind.OnChange), new ActionModel(do0, ActionKind.ToggleTag)));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(table, ct: cts.Token));
    }

    [Fact]
    public async Task ExecuteAsync_ThroughGateway_WithMqttAndTimers_DeploysAllHoldingRegisters()
    {
        // Arrange: Mô phỏng chính xác luồng Studio (DeployRulesUseCase -> RuleTableGateway -> RuleTableWriter)
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);

        var gateway = new SimplePLC.Infrastructure.Gateways.RuleTableGateway(fakeClient);
        var useCase = new DeployRulesUseCase(gateway);

        var table = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        table.AddRule(new Rule(0, "R1", new TriggerModel(di0, TriggerKind.OnRise), new ActionModel(do0, ActionKind.SetTag, 1)));

        var timers = new List<SimplePLC.Protocol.Dto.FbTimerRecordDto>
        {
            new() { Mode = SPLC_TimerMode.TON, PresetMs = 3000, RuleRef = 1 }
        };

        var mqttConfigs = new List<SimplePLC.Protocol.Dto.MqttTelemetryConfigDto>
        {
            new() { Topic = "factory/telemetry", IntervalMs = 500 }
        };

        var brokerConfig = new SimplePLC.Protocol.Dto.MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "10.0.0.1",
            Port = 1883
        };

        var logs = new List<string>();

        // Act
        var result = await useCase.ExecuteAsync(
            table,
            timers: timers,
            counters: null,
            mqttConfigs: mqttConfigs,
            brokerConfig: brokerConfig,
            mqttSubConfigs: null,
            modbusMasterConfigs: null,
            slaveId: 1,
            ct: default,
            progressLogger: logs.Add);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.DeployedRuleCount);

        // 1. Logs viễn trắc [CAPS] và [MQTT] phải được chuyển tiếp qua UseCase -> Gateway -> Writer
        Assert.Contains(logs, l => l.Contains("[CAPS] Reading hardware capabilities at 0x0A08"));
        Assert.Contains(logs, l => l.Contains("[MQTT PUB] Writing 96 registers to 0x0C00"));
        Assert.Contains(logs, l => l.Contains("[MQTT Broker] Writing 96 registers to 0x0E00"));

        // 2. Kiểm tra thanh ghi Timer 0x0B00 và MQTT 0x0C00 đã được ghi thực sự
        var timerModeReg = await fakeClient.ReadHoldingRegistersAsync(1, (ushort)(ModbusRegisterMap.FbTimerTableBaseAddress + 1), 1);
        Assert.Equal((ushort)SPLC_TimerMode.TON, timerModeReg[0]);

        var mqttControlReg = await fakeClient.ReadHoldingRegistersAsync(1, ModbusRegisterMap.MqttTelemetryTableBaseAddress, 1);
        Assert.NotEqual(0, mqttControlReg[0]);
    }
}

