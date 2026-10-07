using SimplePLC.Application.UseCases;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using Xunit;

namespace SimplePLC.Application.Tests;

public class LoadRulesUseCaseTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public async Task ExecuteAsync_DeploysAndThenLoads_RestoresDomainRuleTable()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var writer = new RuleTableWriter(fakeClient);
        var reader = new RuleTableReader(fakeClient);
        var deployUseCase = new DeployRulesUseCase(writer);
        var loadUseCase = new LoadRulesUseCase(reader);

        var originalTable = new RuleTable();
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var di2 = _product.FindTagByName("DI2")!;

        var trigger = new TriggerModel(di0, TriggerKind.OnFall)
        {
            CompareOp = CompareOperator.Equal,
            ThresholdLo = 42
        };
        var action = new ActionModel(do0, ActionKind.SetTag, 1);
        var guard = new GuardModel(di2, negated: true);

        originalTable.AddRule(new Rule(0, "R0", trigger, action, guard));

        // Act 1: Deploy
        var deployResult = await deployUseCase.ExecuteAsync(originalTable);
        Assert.True(deployResult.IsSuccess);

        // Act 2: Load back
        var loadedTable = await loadUseCase.ExecuteAsync(_product);

        // Assert
        Assert.Equal(1, loadedTable.Count);
        var loadedRule = loadedTable.Rules[0];
        Assert.Equal(0, loadedRule.RuleIndex);
        Assert.Equal(TriggerKind.OnFall, loadedRule.Trigger.Type);
        Assert.Equal(CompareOperator.Equal, loadedRule.Trigger.CompareOp);
        Assert.Equal(42, loadedRule.Trigger.ThresholdLo);
        Assert.Equal(di0.TagIndex, loadedRule.Trigger.Tag.TagIndex);
        Assert.Equal(do0.TagIndex, loadedRule.Action.TargetTag.TagIndex);
        Assert.True(loadedRule.Guard.HasGuard);
        Assert.Equal(di2.TagIndex, loadedRule.Guard.Tag!.TagIndex);
        Assert.True(loadedRule.Guard.Negated);
    }
}
