using SimplePLC.Application.Logic.Runtime;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

public class RuntimeEngineApplicationTests
{
    private sealed class InMemoryTagStore : ITagValueStore
    {
        private readonly Dictionary<int, int> _values = new();

        public InMemoryTagStore(params (int Index, int InitialValue)[] initial)
        {
            foreach (var (idx, val) in initial)
                _values[idx] = val;
        }

        public int GetValue(int tagIndex) => _values.TryGetValue(tagIndex, out var v) ? v : 0;
        public void SetValue(int tagIndex, int value) => _values[tagIndex] = value;
        public bool ContainsTag(int tagIndex) => _values.ContainsKey(tagIndex);
        public IReadOnlyCollection<int> AllTagIndices => _values.Keys;
    }

    [Fact]
    public void Scan_OnRiseRule_ExecutesSetTagAction()
    {
        // Tag 0 (DI0), Tag 8 (DO0)
        var store = new InMemoryTagStore((0, 0), (8, 0));
        var engine = new RuntimeEngine(store);

        var tagIn = new TagDefinition(0, "DI0", TagKind.DiscreteInput, TagDataType.Boolean, true);
        var tagOut = new TagDefinition(8, "DO0", TagKind.DiscreteOutput, TagDataType.Boolean, false);

        var rule = new Rule(
            0,
            "R_Test_OnRise",
            new TriggerModel(tagIn, TriggerKind.OnRise),
            new ActionModel(tagOut, ActionKind.SetTag, 1));

        // Scan 1: DI0 = 0 -> DO0 = 0
        var snap1 = engine.Scan(new[] { rule }, 0);
        Assert.Equal(0, store.GetValue(8));
        Assert.Equal(RuleEvaluationStatus.Skip, snap1.Evaluations[0].Status);

        // Transition: DI0 0 -> 1
        store.SetValue(0, 1);
        var snap2 = engine.Scan(new[] { rule }, 20);
        Assert.Equal(1, store.GetValue(8));
        Assert.Equal(RuleEvaluationStatus.Pass, snap2.Evaluations[0].Status);
        Assert.Equal(1, snap2.Values[8]);
    }

    [Fact]
    public void Scan_LinearScaleRule_AppliesFormulaWithOffsetAndGain()
    {
        // Tag 16 (AI0 = 0 -> 5000 mV), Tag 52 (VREG0)
        // Gain = 0.015 (Parameter = 15), Offset = -50 (ThresholdHi = -50)
        // y = floor(5000 * 15 / 1000) + (-50) = 75 - 50 = 25
        var store = new InMemoryTagStore((16, 0), (52, 0));
        var engine = new RuntimeEngine(store);

        var tagIn = new TagDefinition(16, "AI0", TagKind.AnalogInput, TagDataType.Int32, true);
        var tagOut = new TagDefinition(52, "VREG0", TagKind.VirtualRegister, TagDataType.Int32, false);

        var scaleRule = new Rule(
            0,
            "R_Scale_Pressure",
            new TriggerModel(tagIn, TriggerKind.OnChange) { ThresholdHi = -50 },
            new ActionModel(tagOut, ActionKind.ScaleTag, 15));

        // Initial scan (AI0 = 0 -> previous = 0)
        engine.Scan(new[] { scaleRule }, 0);

        // Analog sensor updates to 5000 mV (OnChange fires!)
        store.SetValue(16, 5000);
        var snap = engine.Scan(new[] { scaleRule }, 10);
        Assert.Equal(RuleEvaluationStatus.Pass, snap.Evaluations[0].Status);
        Assert.Equal(25, store.GetValue(52));
    }


    [Fact]
    public void Scan_GuardCondition_BlocksRuleExecutionWhenClosed()
    {
        // DI0 (tag 0) OnRise -> DO0 (tag 8) = 1, GUARDED by DI1 (tag 1, must be 1)
        var store = new InMemoryTagStore((0, 0), (1, 0), (8, 0));
        var engine = new RuntimeEngine(store);

        var tagIn = new TagDefinition(0, "DI0", TagKind.DiscreteInput, TagDataType.Boolean, true);
        var tagGuard = new TagDefinition(1, "DI1", TagKind.DiscreteInput, TagDataType.Boolean, true);
        var tagOut = new TagDefinition(8, "DO0", TagKind.DiscreteOutput, TagDataType.Boolean, false);

        var rule = new Rule(
            0,
            "R_Guarded",
            new TriggerModel(tagIn, TriggerKind.OnRise),
            new ActionModel(tagOut, ActionKind.SetTag, 1),
            new GuardModel(tagGuard, negated: false));

        // Pulse DI0 = 1 while DI1 = 0 (Guard closed)
        store.SetValue(0, 1);
        var snap1 = engine.Scan(new[] { rule }, 10);
        Assert.Equal(RuleEvaluationStatus.BlockedByGuard, snap1.Evaluations[0].Status);
        Assert.Equal(0, store.GetValue(8));

        // Open guard DI1 = 1, and re-pulse DI0
        store.SetValue(1, 1);
        store.SetValue(0, 0);
        engine.Scan(new[] { rule }, 20); // transition 1 -> 0
        store.SetValue(0, 1);
        var snap2 = engine.Scan(new[] { rule }, 30); // transition 0 -> 1

        Assert.Equal(RuleEvaluationStatus.Pass, snap2.Evaluations[0].Status);
        Assert.Equal(1, store.GetValue(8));
    }
}
