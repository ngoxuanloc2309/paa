using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

public class RuleCompilerCycleTimerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void Compile_CycleTimer_StartWithOn_ExpandsIntoFourSafeRules()
    {
        // Arrange
        var graph = new LogicGraph();

        // IN: DI0 (tag 0), Q: DO0 (tag 8), onTime: 500ms, offTime: 1500ms, startWithOn: true
        var timerNode = LogicNode.CreateTimer(
            id: "CYCLE0",
            type: TimerMacroType.Cycle,
            inTagIndex: 0,
            presetMs: 500,
            qTagIndex: 8,
            executionOrder: 0,
            label: "CYCLE_TIMER",
            onTimeMs: 500,
            offTimeMs: 1500,
            startWithOn: true);

        graph.Nodes.Add(timerNode);

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);

        var cycleRules = result.Program!.Rules.Where(r => r.Name.Contains("CYCLE")).ToList();
        Assert.Equal(4, cycleRules.Count);

        // R0: Start ON
        var r0 = cycleRules.First(r => r.Name.EndsWith("_CYCLE_START"));
        Assert.Equal(0u, r0.Trigger.ForMs);
        Assert.Equal(TriggerKind.OnRise, r0.Trigger.Type);
        Assert.Equal(0, r0.Trigger.Tag.TagIndex);
        Assert.Equal(ActionKind.SetTag, r0.Action.Type);
        Assert.Equal(1, r0.Action.Parameter);
        Assert.True(r0.Guard.HasGuard);
        Assert.True(r0.Guard.Negated); // Guard Q == 0

        // R1: ON expired -> Turn OFF
        var r1 = cycleRules.First(r => r.Name.EndsWith("_CYCLE_ON_EXPIRE"));
        Assert.Equal(500u, r1.Trigger.ForMs);
        Assert.Equal(TriggerKind.OnRise, r1.Trigger.Type);
        Assert.Equal(8, r1.Trigger.Tag.TagIndex); // Q ON_RISE
        Assert.Equal(ActionKind.SetTag, r1.Action.Type);
        Assert.Equal(0, r1.Action.Parameter);

        // R2: OFF expired -> Turn ON
        var r2 = cycleRules.First(r => r.Name.EndsWith("_CYCLE_OFF_EXPIRE"));
        Assert.Equal(1500u, r2.Trigger.ForMs);
        Assert.Equal(TriggerKind.OnFall, r2.Trigger.Type);
        Assert.Equal(8, r2.Trigger.Tag.TagIndex); // Q ON_FALL
        Assert.Equal(ActionKind.SetTag, r2.Action.Type);
        Assert.Equal(1, r2.Action.Parameter);
        Assert.True(r2.Guard.HasGuard);
        Assert.False(r2.Guard.Negated); // Guard In == 1

        // R3: In dropped -> Fail-safe stop
        var r3 = cycleRules.First(r => r.Name.EndsWith("_CYCLE_STOP"));
        Assert.Equal(0u, r3.Trigger.ForMs);
        Assert.Equal(TriggerKind.OnFall, r3.Trigger.Type);
        Assert.Equal(0, r3.Trigger.Tag.TagIndex); // In ON_FALL
        Assert.Equal(ActionKind.SetTag, r3.Action.Type);
        Assert.Equal(0, r3.Action.Parameter);
    }

    [Fact]
    public void Compile_CycleTimer_StartWithOff_SetsInitialDelayToOffTime()
    {
        // Arrange
        var graph = new LogicGraph();

        var timerNode = LogicNode.CreateTimer(
            id: "CYCLE0",
            type: TimerMacroType.Cycle,
            inTagIndex: 0,
            presetMs: 1000,
            qTagIndex: 8,
            executionOrder: 0,
            label: "CYCLE_TIMER",
            onTimeMs: 200,
            offTimeMs: 800,
            startWithOn: false);

        graph.Nodes.Add(timerNode);

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess);
        var r0 = result.Program!.Rules.First(r => r.Name.EndsWith("_CYCLE_START"));
        Assert.Equal(800u, r0.Trigger.ForMs); // Waits OffTimeMs before turning ON
    }

    [Fact]
    public void Compile_CycleTimer_WriteConflictValidator_AllowsMultipleWritersFromSameMacro()
    {
        // Arrange
        var graph = new LogicGraph();
        var timerNode = LogicNode.CreateTimer("CYCLE1", TimerMacroType.Cycle, inTagIndex: 1, presetMs: 1000, qTagIndex: 9, onTimeMs: 300, offTimeMs: 700);
        graph.Nodes.Add(timerNode);

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert: No SPLC-COMP-006 (ErrMultiWriterConflict)
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Diagnostics, d => d.Code == "SPLC-COMP-006");
    }
}
