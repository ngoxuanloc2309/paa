using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

public class TimerMacroCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void TON_ExpandsToExactlyTwoRules()
    {
        var graph = new LogicGraph();
        // IN: DI0 (tag 0), Q: DO0 (tag 8), PT: 3000ms
        graph.Nodes.Add(LogicNode.CreateTimer("TON_1", TimerMacroType.Ton, inTagIndex: 0, presetMs: 3000, qTagIndex: 8, label: "MotorStartDelay"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        var r0 = result.Program.Rules[0];
        Assert.Equal(0, r0.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, r0.Trigger.Type);
        Assert.Equal(3000u, r0.Trigger.ForMs);
        Assert.Equal(8, r0.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, r0.Action.Type);
        Assert.Equal(1, r0.Action.Parameter);
        Assert.False(r0.Guard.HasGuard);

        var r1 = result.Program.Rules[1];
        Assert.Equal(0, r1.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnFall, r1.Trigger.Type);
        Assert.Equal(0u, r1.Trigger.ForMs);
        Assert.Equal(8, r1.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, r1.Action.Type);
        Assert.Equal(0, r1.Action.Parameter);
        Assert.False(r1.Guard.HasGuard);
    }

    [Fact]
    public void TOF_ExpandsToExactlyTwoRules()
    {
        var graph = new LogicGraph();
        // IN: DI0 (tag 0), Q: DO0 (tag 8), PT: 2000ms
        graph.Nodes.Add(LogicNode.CreateTimer("TOF_1", TimerMacroType.Tof, inTagIndex: 0, presetMs: 2000, qTagIndex: 8, label: "FanRunout"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        var r0 = result.Program.Rules[0];
        Assert.Equal(0, r0.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, r0.Trigger.Type);
        Assert.Equal(0u, r0.Trigger.ForMs);
        Assert.Equal(8, r0.Action.TargetTag.TagIndex);
        Assert.Equal(1, r0.Action.Parameter);
        Assert.False(r0.Guard.HasGuard);

        var r1 = result.Program.Rules[1];
        Assert.Equal(0, r1.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnFall, r1.Trigger.Type);
        Assert.Equal(2000u, r1.Trigger.ForMs);
        Assert.Equal(8, r1.Action.TargetTag.TagIndex);
        Assert.Equal(0, r1.Action.Parameter);
        Assert.True(r1.Guard.HasGuard);
        Assert.Equal(8, r1.Guard.Tag!.TagIndex);
        Assert.False(r1.Guard.Negated); // Guard: Q == 1
    }

    [Fact]
    public void TP_ExpandsToExactlyTwoRules()
    {
        var graph = new LogicGraph();
        // IN: DI0 (tag 0), Q: DO0 (tag 8), PT: 1500ms
        graph.Nodes.Add(LogicNode.CreateTimer("TP_1", TimerMacroType.Tp, inTagIndex: 0, presetMs: 1500, qTagIndex: 8, label: "SolenoidPulse"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        var r0 = result.Program.Rules[0];
        Assert.Equal(0, r0.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, r0.Trigger.Type);
        Assert.Equal(0u, r0.Trigger.ForMs);
        Assert.True(r0.Guard.HasGuard);
        Assert.Equal(8, r0.Guard.Tag!.TagIndex);
        Assert.True(r0.Guard.Negated); // Guard: Q == 0 (locks out retriggering during pulse)
        Assert.Equal(8, r0.Action.TargetTag.TagIndex);
        Assert.Equal(1, r0.Action.Parameter);

        var r1 = result.Program.Rules[1];
        Assert.Equal(8, r1.Trigger.Tag.TagIndex); // Triggered by Q!
        Assert.Equal(TriggerKind.OnRise, r1.Trigger.Type);
        Assert.Equal(1500u, r1.Trigger.ForMs);
        Assert.Equal(8, r1.Action.TargetTag.TagIndex);
        Assert.Equal(0, r1.Action.Parameter);
        Assert.False(r1.Guard.HasGuard);
    }

    [Fact]
    public void TimerGeneratedPair_DoesNotTriggerFalseWriterConflict()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("TON_ALONE", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 8));

        var result = _compiler.Compile(graph, _product);

        // Controlled Multi-Writer Group must allow the complementary pair
        Assert.True(result.IsSuccess, $"Must succeed, but got: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == RuleCompiler.ErrConflictingWriters);
    }

    [Fact]
    public void TimerAndExternalWriterSameQ_IsRejected()
    {
        var graph = new LogicGraph();
        // Timer writes to DO0 (tag 8)
        graph.Nodes.Add(LogicNode.CreateTimer("TON_1", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 8));

        // External user rule also writes to DO0 (tag 8)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_ROGUE", 8, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_ROGUE", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        // Must reject conflict between Timer and external user rule
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrConflictingWriters);
    }

    [Fact]
    public void TwoTimersSameQ_AreRejected()
    {
        var graph = new LogicGraph();
        // Timer 1 writes to DO0 (tag 8)
        graph.Nodes.Add(LogicNode.CreateTimer("TON_1", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 8));
        // Timer 2 ALSO writes to DO0 (tag 8)
        graph.Nodes.Add(LogicNode.CreateTimer("TON_2", TimerMacroType.Ton, inTagIndex: 1, presetMs: 2000, qTagIndex: 8));

        var result = _compiler.Compile(graph, _product);

        // Must reject two different timers contesting the same output Q
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrConflictingWriters);
    }

    [Fact]
    public void FortyTimers_ConsumeEightyRuleSlots()
    {
        // Test constrained device: MaxRules = 50
        var tags = DeviceProfileBuilder.GenerateTags(ProductResourceProfile.DefaultRemoteIo);
        var product50 = new ProductDefinition(
            "ConstrainedPLC",
            deviceClass: 1,
            productVariant: 1,
            maxRules: 50,
            resources: ProductResourceProfile.DefaultRemoteIo,
            tags: tags);

        // 1. 20 timers -> 40 rules <= 50 -> SUCCESS
        var graph40 = new LogicGraph();
        for (int i = 0; i < 20; i++)
        {
            // Unique VFLAGs 20..39 for Q
            ushort qTag = (ushort)(20 + i);
            graph40.Nodes.Add(LogicNode.CreateTimer($"TON_{i}", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: qTag));
        }

        var result40 = _compiler.Compile(graph40, product50);
        Assert.True(result40.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result40.Diagnostics.Select(d => d.Message))}");
        Assert.Equal(40, result40.Program!.RuleCount);

        // 2. 30 timers -> 60 rules > 50 -> CAPACITY ERROR
        var graph60 = new LogicGraph();
        for (int i = 0; i < 30; i++)
        {
            // Unique VFLAGs 20..49 for Q
            ushort qTag = (ushort)(20 + i);
            graph60.Nodes.Add(LogicNode.CreateTimer($"TON_{i}", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: qTag));
        }

        var result60 = _compiler.Compile(graph60, product50);
        Assert.False(result60.IsSuccess);
        Assert.Contains(result60.Diagnostics, d => d.Code == RuleCompiler.ErrCapacityExceeded);
    }

    [Fact]
    public void ExpandedTimerRules_ParticipateInDependencyOrdering()
    {
        var graph = new LogicGraph();

        // Producer: TON writes to VFLAG0 (tag 20)
        graph.Nodes.Add(LogicNode.CreateTimer("TON_PRODUCER", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 20));

        // Consumer: Normal rule reads VFLAG0 (tag 20) and writes to DO0 (tag 8)
        graph.Nodes.Add(LogicNode.CreateInput("IN_VFLAG0", 20));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_VFLAG0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_DO0", 8, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_VFLAG0", "TR_VFLAG0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_VFLAG0", "ACT_DO0", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.RuleCount);

        // The Producer (TON rules writing VFLAG0) MUST precede the Consumer rule (reading VFLAG0)
        // Check that the rule writing DO0 is the LAST rule (Index 2)
        var do0Rule = result.Program.Rules.First(r => r.Action.TargetTag.TagIndex == 8);
        Assert.Equal(2, do0Rule.RuleIndex);

        // The first two rules must be the TON rules writing VFLAG0
        Assert.All(result.Program.Rules.Take(2), r => Assert.Equal(20, r.Action.TargetTag.TagIndex));
    }

    [Fact]
    public void SameGraphTimerCompile_IsDeterministic()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("TON_A", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 20));
        graph.Nodes.Add(LogicNode.CreateTimer("TOF_B", TimerMacroType.Tof, inTagIndex: 1, presetMs: 2000, qTagIndex: 21));
        graph.Nodes.Add(LogicNode.CreateTimer("TP_C", TimerMacroType.Tp, inTagIndex: 2, presetMs: 3000, qTagIndex: 22));

        var baseline = _compiler.Compile(graph, _product);
        Assert.True(baseline.IsSuccess);

        for (int iteration = 0; iteration < 5; iteration++)
        {
            var testRun = _compiler.Compile(graph, _product);
            Assert.True(testRun.IsSuccess);
            Assert.Equal(baseline.Program!.RuleCount, testRun.Program!.RuleCount);

            for (int i = 0; i < baseline.Program.RuleCount; i++)
            {
                var rBase = baseline.Program.Rules[i];
                var rTest = testRun.Program.Rules[i];
                Assert.Equal(rBase.Name, rTest.Name);
                Assert.Equal(rBase.Trigger.Tag.TagIndex, rTest.Trigger.Tag.TagIndex);
                Assert.Equal(rBase.Trigger.Type, rTest.Trigger.Type);
                Assert.Equal(rBase.Trigger.ForMs, rTest.Trigger.ForMs);
                Assert.Equal(rBase.Action.TargetTag.TagIndex, rTest.Action.TargetTag.TagIndex);
                Assert.Equal(rBase.Action.Parameter, rTest.Action.Parameter);
                Assert.Equal(rBase.Guard.HasGuard, rTest.Guard.HasGuard);
            }
        }
    }

    [Fact]
    public void TimerMacro_SourceMapSurvivesIntoCompiledProgram()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("TON_SRC", TimerMacroType.Ton, inTagIndex: 0, presetMs: 3000, qTagIndex: 8, label: "PumpDelay"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.SourceMap.Count);

        var sm0 = result.Program.SourceMap[0];
        Assert.Equal("TON_SRC", sm0.MacroInstanceId);
        Assert.Equal("TON_SRC", sm0.SourceNodeId);
        Assert.Equal("TON", sm0.MacroType);
        Assert.Equal(0, sm0.ExpansionIndex);
        Assert.Equal("PumpDelay", sm0.DisplayLabel);

        var sm1 = result.Program.SourceMap[1];
        Assert.Equal("TON_SRC", sm1.MacroInstanceId);
        Assert.Equal("TON_SRC", sm1.SourceNodeId);
        Assert.Equal("TON", sm1.MacroType);
        Assert.Equal(1, sm1.ExpansionIndex);
        Assert.Equal("PumpDelay", sm1.DisplayLabel);
    }

    [Fact]
    public void TimerMacro_InputCanBeWiredFromUpstreamInputNode()
    {
        var graph = new LogicGraph();

        // Upstream Input node: DI3 (Tag 3)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI3", 3));

        // Timer node: has default inTagIndex: 0, but wired from IN_DI3!
        graph.Nodes.Add(LogicNode.CreateTimer("TON_WIRED", TimerMacroType.Ton, inTagIndex: 0, presetMs: 2500, qTagIndex: 8));

        // Wire Input -> Timer
        graph.Edges.Add(new LogicEdge("IN_DI3", "TON_WIRED", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Must succeed, but got: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        // Triggers must resolve to Tag 3 (DI3) due to wiring!
        Assert.Equal(3, result.Program.Rules[0].Trigger.Tag.TagIndex);
        Assert.Equal(3, result.Program.Rules[1].Trigger.Tag.TagIndex);
        Assert.Equal(8, result.Program.Rules[0].Action.TargetTag.TagIndex);
    }

    [Fact]
    public void TimerMacro_InstanceId_IsDeterministic()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("TON_DET", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 8));

        var res1 = _compiler.Compile(graph, _product);
        var res2 = _compiler.Compile(graph, _product);

        Assert.True(res1.IsSuccess && res2.IsSuccess);
        Assert.Equal(res1.Program!.SourceMap[0].MacroInstanceId, res2.Program!.SourceMap[0].MacroInstanceId);
        Assert.Equal("TON_DET", res1.Program.SourceMap[0].MacroInstanceId);
    }

    [Fact]
    public void MalformedControlledWriterGroup_IsRejected()
    {
        var timerNode = LogicNode.CreateTimer("TON_BUGGY", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 8);
        var qTag = _product.FindTagByIndex(8)!;
        var inTag = _product.FindTagByIndex(0)!;

        // Malformed group: 2 writers both setting 1 (no reset rule!)
        var r0 = RuleAccessAnalyzer.Analyze(
            timerNode,
            new TriggerModel(inTag, TriggerKind.OnRise),
            new ActionModel(qTag, ActionKind.SetTag, 1),
            GuardModel.Empty,
            "R0",
            new GeneratedRuleOrigin("TON_BUGGY", "TON_BUGGY", "TON", 0));

        var r1 = RuleAccessAnalyzer.Analyze(
            timerNode,
            new TriggerModel(inTag, TriggerKind.OnFall),
            new ActionModel(qTag, ActionKind.SetTag, 1), // BUG: sets 1 instead of 0!
            GuardModel.Empty,
            "R1",
            new GeneratedRuleOrigin("TON_BUGGY", "TON_BUGGY", "TON", 1));

        var diagnostics = WriteConflictValidator.Validate(new[] { r0, r1 }, _product);

        Assert.NotEmpty(diagnostics);
        Assert.Contains(diagnostics, d => d.Code == RuleCompiler.ErrConflictingWriters);
        Assert.Contains("không đúng định dạng chuẩn", diagnostics[0].Message);
    }

    [Fact]
    public void Timer_FbdWiringResolution_WiresInAndQSuccessfully()
    {
        var graph = new LogicGraph();
        // Node 1: Input DI0 (Tag 0)
        graph.Nodes.Add(LogicNode.CreateInput("INP_1", 0));
        // Node 2: Timer TON (placeholders for in and q)
        graph.Nodes.Add(LogicNode.CreateTimer("TON_FBD", TimerMacroType.Ton, inTagIndex: 99, presetMs: 4000, qTagIndex: 99));
        // Node 3: Action DO0 (Tag 8)
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 8));

        // Connect INP_1 -> TON_FBD:In
        graph.Edges.Add(new LogicEdge("INP_1", "TON_FBD", "Out", "In"));
        // Connect TON_FBD:Q -> ACT_1:In
        graph.Edges.Add(new LogicEdge("TON_FBD", "ACT_1", "Q", "In"));

        // Structure check should pass grammar
        var structDiags = GraphGrammarV1.ValidateStructure(graph);
        Assert.Empty(structDiags);

        // Compile
        var result = _compiler.Compile(graph, _product);
        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        // Verify IN resolved to DI0 (Tag 0)
        Assert.Equal(0, result.Program.Rules[0].Trigger.Tag.TagIndex);
        Assert.Equal(0, result.Program.Rules[1].Trigger.Tag.TagIndex);

        // Verify Q resolved to DO0 (Tag 8)
        Assert.Equal(8, result.Program.Rules[0].Action.TargetTag.TagIndex);
        Assert.Equal(8, result.Program.Rules[1].Action.TargetTag.TagIndex);
    }
}
