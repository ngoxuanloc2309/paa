using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public class DualModeCompilerV2Tests
{
    private readonly ProductDefinition _productV1 = ProductDefinition.CreateRemoteIo8Di8Do4Ai(wireProfile: 1);
    private readonly ProductDefinition _productV2 = ProductDefinition.CreateRemoteIo8Di8Do4Ai(wireProfile: 2);
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void TargetV2_WithTimerAndCounter_CompilesToDedicatedFunctionBlocks_WithZeroMacroRules()
    {
        // Arrange
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("T1", TimerMacroType.Ton, inTagIndex: 0, presetMs: 5000, qTagIndex: 8));
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctu, cuTagIndex: 1, resetTagIndex: 2, cvTagIndex: 84, presetValue: 20, qTagIndex: 9));

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        // Zero macro rules in RuleTable!
        Assert.Empty(result.Program.Rules);
        Assert.Equal(0, result.Program.RuleCount);

        // Dedicated FB lists populated
        Assert.Single(result.Program.FunctionBlockTimers);
        var timer = result.Program.FunctionBlockTimers[0];
        Assert.Equal(SPLC_TimerMode.TON, timer.Mode);
        Assert.Equal(5000u, timer.PresetMs);

        Assert.Single(result.Program.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(SPLC_CounterMode.CTU, counter.Mode);
        Assert.Equal(20, counter.PresetValue);
        Assert.Equal(84, counter.RetainTagIndex); // TagIndex 84 in VREG_RETAIN space
    }

    [Fact]
    public void TargetV1_WithTimerAndCounter_ExpandsMacroRules_ForBackwardCompatibility()
    {
        // Arrange
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("T1", TimerMacroType.Ton, inTagIndex: 0, presetMs: 5000, qTagIndex: 8));
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctu, cuTagIndex: 1, resetTagIndex: 2, cvTagIndex: 84, presetValue: 20, qTagIndex: 9));

        // Act
        var result = _compiler.Compile(graph, _productV1);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        // V1 macro expansion: TON generates 2 rules, CTU with reset generates 4 rules = 6 rules total
        Assert.Equal(6, result.Program.RuleCount);
        Assert.Empty(result.Program.FunctionBlockTimers);
        Assert.Empty(result.Program.FunctionBlockCounters);
    }

    [Fact]
    public void TargetV2_ExceedingTimerLimit_ReturnsErrCapacityExceeded()
    {
        // Arrange: 9 timers (limit is 8)
        var graph = new LogicGraph();
        for (int i = 0; i < 9; i++)
        {
            graph.Nodes.Add(LogicNode.CreateTimer($"T{i}", TimerMacroType.Ton, inTagIndex: 0, presetMs: 1000, qTagIndex: 8));
        }

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrCapacityExceeded && d.Message.Contains("Timer count"));
    }

    [Fact]
    public void TargetV2_ExceedingCounterLimit_ReturnsErrCapacityExceeded()
    {
        // Arrange: 9 counters (limit is 8)
        var graph = new LogicGraph();
        for (int i = 0; i < 9; i++)
        {
            graph.Nodes.Add(LogicNode.CreateCounter($"C{i}", CounterMacroType.Ctu, cuTagIndex: 1, resetTagIndex: null, cvTagIndex: 84, presetValue: 10, qTagIndex: 8));
        }

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrCapacityExceeded && d.Message.Contains("Counter count"));
    }

    [Fact]
    public void TargetV2_NonRetentiveCounter_SetsRetainSentinel()
    {
        // Arrange: Counter with CV on VREG (Tag 52) which is volatile RAM, not VREG_RETAIN (84..115)
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctd, cuTagIndex: 1, resetTagIndex: null, cvTagIndex: 52, presetValue: 15, qTagIndex: 8));

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(SPLC_CounterMode.CTD, counter.Mode);
        Assert.Equal(ModbusRegisterMap.FbCounterRetainNone, counter.RetainTagIndex); // 0xFFFF sentinel
    }
}
