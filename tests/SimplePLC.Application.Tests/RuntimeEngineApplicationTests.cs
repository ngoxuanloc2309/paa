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

    [Fact]
    public void Scan_FloatComparison_EvaluatesThresholdCorrectly()
    {
        // Tag 16 (Temperature), Tag 8 (Cooler Relay)
        // Rule: If Temperature > 55.5 -> Turn ON Cooler (Tag 8 = 1)
        int initialTempBits = BitConverter.SingleToInt32Bits(50.0f);
        var store = new InMemoryTagStore((16, initialTempBits), (8, 0));
        var engine = new RuntimeEngine(store);

        var tagIn = new TagDefinition(16, "Temperature", TagKind.AnalogInput, TagDataType.Float32, true);
        var tagOut = new TagDefinition(8, "Cooler", TagKind.DiscreteOutput, TagDataType.Boolean, false);

        var trigger = new TriggerModel(tagIn, TriggerKind.OnChange)
        {
            CompareOp = CompareOperator.GreaterThan,
            ThresholdLo = 55.5,
            IsFloat = true
        };

        var rule = new Rule(0, "R_Cooler_Control", trigger, new ActionModel(tagOut, ActionKind.SetTag, 1));

        // Scan 1: initial 50.0 <= 55.5 -> Skip
        var snap1 = engine.Scan(new[] { rule }, 0);
        Assert.Equal(RuleEvaluationStatus.Skip, snap1.Evaluations[0].Status);
        Assert.Equal(0, store.GetValue(8));

        // Temp rises to 55.4 (still <= 55.5) -> Skip
        store.SetValue(16, BitConverter.SingleToInt32Bits(55.4f));
        var snap2 = engine.Scan(new[] { rule }, 10);
        Assert.Equal(RuleEvaluationStatus.Skip, snap2.Evaluations[0].Status);
        Assert.Equal(0, store.GetValue(8));

        // Temp rises to 55.6 (> 55.5) -> Pass, Cooler turns ON!
        store.SetValue(16, BitConverter.SingleToInt32Bits(55.6f));
        var snap3 = engine.Scan(new[] { rule }, 20);
        Assert.Equal(RuleEvaluationStatus.Pass, snap3.Evaluations[0].Status);
        Assert.Equal(1, store.GetValue(8));
    }

    [Fact]
    public void Scan_FloatLinearScale_CalculatesIEEE754Correctly()
    {
        // Tag 16 (Raw Pressure), Tag 52 (Scaled Bar)
        // y = k * x + b where k = 0.5f, b = 10.5f, x = 100.0f
        // Expected y = 0.5 * 100.0 + 10.5 = 60.5f
        int initialRawBits = BitConverter.SingleToInt32Bits(0.0f);
        var store = new InMemoryTagStore((16, initialRawBits), (52, 0));
        var engine = new RuntimeEngine(store);

        var tagIn = new TagDefinition(16, "RawSensor", TagKind.AnalogInput, TagDataType.Float32, true);
        var tagOut = new TagDefinition(52, "ScaledBar", TagKind.VirtualRegister, TagDataType.Float32, false);

        var trigger = new TriggerModel(tagIn, TriggerKind.OnChange)
        {
            ThresholdHi = 10.5, // offset b
            IsFloat = true
        };

        var action = new ActionModel(tagOut, ActionKind.ScaleTag, 0.5)
        {
            IsFloat = true
        };

        var rule = new Rule(0, "R_Scale_Float", trigger, action);

        // Initial scan
        engine.Scan(new[] { rule }, 0);

        // Sensor updates to 100.0f
        store.SetValue(16, BitConverter.SingleToInt32Bits(100.0f));
        var snap = engine.Scan(new[] { rule }, 10);

        Assert.Equal(RuleEvaluationStatus.Pass, snap.Evaluations[0].Status);
        float scaledResult = BitConverter.Int32BitsToSingle(store.GetValue(52));
        Assert.Equal(60.5f, scaledResult, precision: 4);
    }
}
