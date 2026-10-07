using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Application.Logic.Runtime;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

public class E2EWalkthroughStressTest
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

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
    public void SpecWalkthrough_AsWrittenInDoc_FailsGraphGrammarValidation()
    {
        // Construct the EXACT graph from Section 5.3 of NEXTGEN_AI_COPILOT_SPECIFICATION.md
        var graph = new LogicGraph();

        // 1. add_node node_in_start (INPUT, DI0 = Tag 0)
        var inStart = LogicNode.CreateInput("node_in_start", tagIndex: 0, label: "Nút Start");
        graph.Nodes.Add(inStart);

        // 2. add_node node_trg_start (TRIGGER, ON_RISE, debounce_ms=30)
        var trgStart = LogicNode.CreateTrigger(
            "node_trg_start",
            type: TriggerKind.OnRise,
            compareOp: CompareOperator.None,
            thresholdLo: 0,
            thresholdHi: 0,
            forMs: 30,
            label: "Bắt Sườn Lên Start");
        graph.Nodes.Add(trgStart);

        // 3. add_node node_grd_estop (GUARD, DI2 = Tag 2, negated=false for EQ 1)
        var grdEstop = LogicNode.CreateGuard(
            "node_grd_estop",
            tagIndex: 2,
            negated: false,
            label: "Khóa An Toàn E-Stop NC");
        graph.Nodes.Add(grdEstop);

        // 4. add_node node_act_latch (ACTION, VFLAG0 = Tag 20, SET_TAG 1)
        var actLatch = LogicNode.CreateAction(
            "node_act_latch",
            targetTagIndex: 20, // VFLAG0
            type: ActionKind.SetTag,
            parameter: 1,
            label: "Bật Cờ Tự Giữ Bơm");
        graph.Nodes.Add(actLatch);

        // 5. add_node tm_pump_tof (TIMER, TOF, Preset 5000, Q: DO0 = Tag 8)
        var tmTof = LogicNode.CreateTimer(
            "tm_pump_tof",
            type: TimerMacroType.Tof,
            inTagIndex: 20, // or 0
            presetMs: 5000,
            qTagIndex: 8, // DO0
            label: "Trễ Ngắt Bơm 5s");
        graph.Nodes.Add(tmTof);

        // Wire 1: node_in_start -> node_trg_start
        graph.Edges.Add(new LogicEdge { Id = "wire_1", SourceNodeId = "node_in_start", SourcePort = "Out", TargetNodeId = "node_trg_start", TargetPort = "In" });

        // Wire 2: node_trg_start -> node_grd_estop
        graph.Edges.Add(new LogicEdge { Id = "wire_2", SourceNodeId = "node_trg_start", SourcePort = "Out", TargetNodeId = "node_grd_estop", TargetPort = "In" });

        // Wire 3: node_grd_estop -> node_act_latch
        graph.Edges.Add(new LogicEdge { Id = "wire_3", SourceNodeId = "node_grd_estop", SourcePort = "Out", TargetNodeId = "node_act_latch", TargetPort = "In" });

        // Wire 4: node_act_latch -> tm_pump_tof (AS WRITTEN IN SPECIFICATION SECTION 5.3!)
        graph.Edges.Add(new LogicEdge { Id = "wire_4", SourceNodeId = "node_act_latch", SourcePort = "Out", TargetNodeId = "tm_pump_tof", TargetPort = "IN" });

        // EXECUTE EMPIRICAL TEST:
        var grammarDiagnostics = GraphGrammarV1.ValidateStructure(graph);

        // Assert that the specification claim of [PASSED] in Section 5.4 is FALSE:
        Assert.NotEmpty(grammarDiagnostics);
        Assert.Contains(grammarDiagnostics, d => d.Code == GraphGrammarV1.ErrActionSource);

        // And compile result MUST fail
        var compileResult = _compiler.Compile(graph, _product);
        Assert.False(compileResult.IsSuccess);
        Assert.Contains(compileResult.Diagnostics, d => d.Code == GraphGrammarV1.ErrActionSource);
    }

    [Fact]
    public void SpecWalkthrough_CompiledRules_FailIndustrialSafety_EStopCannotStopPump()
    {
        // Test the exact 3 compiled rules claimed in Section 5.6:
        // Rule 1: DI0 (Start) ON_RISE, Guard DI2 EQ 1 -> VFLAG0 = 1
        // Rule 2: VFLAG0 ON_RISE -> DO0 = 1
        // Rule 3: VFLAG0 ON_FALL (for 5000ms) -> DO0 = 0

        var di0 = new TagDefinition(0, "DI0", TagKind.DiscreteInput, TagDataType.Boolean, true);
        var di2 = new TagDefinition(2, "DI2", TagKind.DiscreteInput, TagDataType.Boolean, true);
        var vflag0 = new TagDefinition(20, "VFLAG0", TagKind.VirtualFlag, TagDataType.Boolean, false);
        var do0 = new TagDefinition(8, "DO0", TagKind.DiscreteOutput, TagDataType.Boolean, false);

        var rule1 = new Rule(
            0, "R1_Start_Latch",
            new TriggerModel(di0, TriggerKind.OnRise),
            new ActionModel(vflag0, ActionKind.SetTag, 1),
            new GuardModel(di2, negated: false)); // DI2 == 1 (Safe)

        var rule2 = new Rule(
            1, "R2_TOF_ON",
            new TriggerModel(vflag0, TriggerKind.OnRise),
            new ActionModel(do0, ActionKind.SetTag, 1));

        var rule3 = new Rule(
            2, "R3_TOF_OFF",
            new TriggerModel(vflag0, TriggerKind.OnFall) { ForMs = 5000 },
            new ActionModel(do0, ActionKind.SetTag, 0),
            new GuardModel(do0, negated: false));

        var rules = new[] { rule1, rule2, rule3 };

        // Initial condition: E-Stop is NC (safe, DI2=1), Start is 0, VFLAG0=0, DO0=0
        var store = new InMemoryTagStore((0, 0), (2, 1), (20, 0), (8, 0));
        var engine = new RuntimeEngine(store);

        // Scan 1: Idle
        engine.Scan(rules, 0);
        Assert.Equal(0, store.GetValue(8)); // Pump off

        // Scan 2: Operator presses Start (DI0 0 -> 1)
        store.SetValue(0, 1);
        engine.Scan(rules, 20);
        Assert.Equal(1, store.GetValue(20)); // VFLAG0 latched ON
        Assert.Equal(1, store.GetValue(8));  // Pump DO0 is ON!

        // Scan 3: Operator releases Start button (DI0 1 -> 0)
        store.SetValue(0, 0);
        engine.Scan(rules, 40);
        Assert.Equal(1, store.GetValue(20)); // VFLAG0 still ON
        Assert.Equal(1, store.GetValue(8));  // Pump still ON

        // Scan 4: EMERGENCY! Operator slams E-STOP button! (DI2 1 -> 0)
        store.SetValue(2, 0); // E-Stop engaged!
        engine.Scan(rules, 60);

        // VERIFY THE INDUSTRIAL SAFETY BUG:
        // Because there is NO rule resetting VFLAG0 upon E-Stop (DI2=0),
        // VFLAG0 remains 1, and the pump DO0 NEVER SHUTS DOWN!
        Assert.Equal(1, store.GetValue(8));  // BUG: Pump STILL RUNNING despite E-Stop!
        Assert.Equal(1, store.GetValue(20)); // BUG: Latch flag STILL SET!

        // Scan 5: 10 seconds later, with E-Stop still held at 0
        engine.Scan(rules, 10060);
        Assert.Equal(1, store.GetValue(8));  // BUG: 10s later, pump is STILL RUNNING!
    }
}
