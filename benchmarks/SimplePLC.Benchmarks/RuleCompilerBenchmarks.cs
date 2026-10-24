using BenchmarkDotNet.Attributes;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

namespace SimplePLC.Benchmarks;

[MemoryDiagnoser]
public class RuleCompilerBenchmarks
{
    private ProductDefinition _product = null!;
    private RuleCompiler _compiler = null!;
    private LogicGraph _smallGraph = null!;
    private LogicGraph _largeGraph = null!;

    [GlobalSetup]
    public void Setup()
    {
        _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        _compiler = new RuleCompiler();

        // 1. Small Graph (5 independent linear paths: DI -> Trigger -> Action)
        _smallGraph = new LogicGraph();
        for (int i = 0; i < 5; i++)
        {
            var inId = $"IN_{i}";
            var trId = $"TR_{i}";
            var actId = $"ACT_{i}";

            _smallGraph.Nodes.Add(LogicNode.CreateInput(inId, (ushort)(i % 8)));
            _smallGraph.Nodes.Add(LogicNode.CreateTrigger(trId, TriggerKind.OnRise));
            _smallGraph.Nodes.Add(LogicNode.CreateAction(actId, (ushort)(8 + (i % 8)), ActionKind.SetTag, 1));

            _smallGraph.Edges.Add(new LogicEdge(inId, trId));
            _smallGraph.Edges.Add(new LogicEdge(trId, actId));
        }

        // 2. Large Complex Graph (Paths combining Timers, Scalers, Guards, and Actions)
        _largeGraph = new LogicGraph();
        for (int i = 0; i < 10; i++)
        {
            var inId = $"L_IN_{i}";
            var trId = $"L_TR_{i}";
            var grdId = $"L_GRD_{i}";
            var tmrId = $"L_TMR_{i}";
            var actId1 = $"L_ACTA_{i}";

            _largeGraph.Nodes.Add(LogicNode.CreateInput(inId, (ushort)(i % 8)));
            _largeGraph.Nodes.Add(LogicNode.CreateGuard(grdId, (ushort)(20 + (i % 16)), negated: i % 2 == 1));
            _largeGraph.Nodes.Add(LogicNode.CreateTrigger(trId, TriggerKind.OnRise));
            _largeGraph.Nodes.Add(LogicNode.CreateTimer(tmrId, TimerMacroType.Ton, inTagIndex: (ushort)(i % 8), presetMs: 1000, qTagIndex: (ushort)(8 + (i % 8))));
            _largeGraph.Nodes.Add(LogicNode.CreateAction(actId1, (ushort)(8 + (i % 8)), ActionKind.SetTag, 1));

            _largeGraph.Edges.Add(new LogicEdge(inId, trId));
            _largeGraph.Edges.Add(new LogicEdge(grdId, trId));
            _largeGraph.Edges.Add(new LogicEdge(trId, tmrId));
            _largeGraph.Edges.Add(new LogicEdge(tmrId, actId1));
        }

        // Add Scaler paths
        for (int i = 0; i < 5; i++)
        {
            var aiId = $"L_AI_{i}";
            var scId = $"L_SC_{i}";
            var vregId = $"L_VREG_{i}";

            _largeGraph.Nodes.Add(LogicNode.CreateInput(aiId, (ushort)(16 + (i % 4))));
            _largeGraph.Nodes.Add(LogicNode.CreateScale(scId, inTagIndex: (ushort)(16 + (i % 4)), outTagIndex: (ushort)(52 + i), gain: 2.5, offset: 10.0));
            _largeGraph.Nodes.Add(LogicNode.CreateAction(vregId, (ushort)(52 + i), ActionKind.SetTag, 0));

            _largeGraph.Edges.Add(new LogicEdge(aiId, scId));
            _largeGraph.Edges.Add(new LogicEdge(scId, vregId));
        }
    }

    [Benchmark(Description = "RuleCompiler: Compile Small FBD Graph (5 Paths)")]
    public CompileResult Compile_SmallGraph()
    {
        return _compiler.Compile(_smallGraph, _product);
    }

    [Benchmark(Description = "RuleCompiler: Compile Large FBD Graph (Timers, Scalers, Guards)")]
    public CompileResult Compile_LargeGraph()
    {
        return _compiler.Compile(_largeGraph, _product);
    }
}
