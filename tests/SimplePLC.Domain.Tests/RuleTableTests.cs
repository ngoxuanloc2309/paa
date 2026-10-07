using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Domain.Tests;

public class RuleTableTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    private Rule CreateDummyRule(int index = 0)
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        return new Rule(
            ruleIndex: index,
            name: $"Rule_{index}",
            trigger: new TriggerModel(di0, TriggerKind.OnRise),
            action: new ActionModel(do0, ActionKind.SetTag, 1)
        );
    }

    [Fact]
    public void AddRule_IncrementsCountAndAssignsContiguousIndex()
    {
        var table = new RuleTable();

        var r1 = CreateDummyRule(99); // index ban đầu bị lệch
        var r2 = CreateDummyRule(88);

        table.AddRule(r1);
        table.AddRule(r2);

        Assert.Equal(2, table.Count);
        Assert.Equal(0, r1.RuleIndex);
        Assert.Equal(1, r2.RuleIndex);
    }

    [Fact]
    public void AddRule_AtMaxCapacity100_Succeeds()
    {
        var table = new RuleTable();

        for (int i = 0; i < RuleTable.MaxCapacity; i++)
        {
            table.AddRule(CreateDummyRule(i));
        }

        Assert.Equal(100, table.Count);
        Assert.Equal(99, table[99].RuleIndex);
    }

    [Fact]
    public void AddRule_ExceedingMaxCapacity_ThrowsInvalidOperationException()
    {
        var table = new RuleTable();

        for (int i = 0; i < RuleTable.MaxCapacity; i++)
        {
            table.AddRule(CreateDummyRule(i));
        }

        // Rule thứ 101 phải ném ngoại lệ
        Assert.Throws<InvalidOperationException>(() => table.AddRule(CreateDummyRule(100)));
    }

    [Fact]
    public void RemoveRule_MiddleRule_ReindexesRemainingRules()
    {
        var table = new RuleTable();

        var r0 = CreateDummyRule();
        var r1 = CreateDummyRule();
        var r2 = CreateDummyRule();

        table.AddRule(r0);
        table.AddRule(r1);
        table.AddRule(r2);

        Assert.Equal(0, r0.RuleIndex);
        Assert.Equal(1, r1.RuleIndex);
        Assert.Equal(2, r2.RuleIndex);

        // Xóa rule ở vị trí 1
        bool removed = table.RemoveRule(1);

        Assert.True(removed);
        Assert.Equal(2, table.Count);
        Assert.Equal(0, table[0].RuleIndex);
        Assert.Equal(1, table[1].RuleIndex);
        Assert.Same(r2, table[1]);
    }
}
