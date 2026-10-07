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
}
