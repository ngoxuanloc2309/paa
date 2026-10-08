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
    public void TargetV2_WithTimerAndCounter_GeneratesMacroRules_AndPopulatesDedicatedFunctionBlocks()
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
        // V2 generates macro rules (TON 2 rules + CTU with reset 4 rules = 6 rules) for MCU Rule Engine
        Assert.Equal(6, result.Program.RuleCount);

        // AND populates dedicated FB lists for hardware registers 0x0B00..0x0B7F
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
    public void TargetV2_VolatileCounter_BindsCvTagIndex()
    {
        // Arrange: Counter with CV on VREG (Tag 52) which is volatile RAM, should still bind 52 so MCU knows where CV is stored
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctd, cuTagIndex: 1, resetTagIndex: null, cvTagIndex: 52, presetValue: 15, qTagIndex: 8));

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(SPLC_CounterMode.CTD, counter.Mode);
        Assert.Equal(52, counter.RetainTagIndex); // Successfully bound to VREG tag 52
    }

    [Fact]
    public void TargetV2_UnspecifiedCvTag_DefaultsToDedicatedCounterTag()
    {
        // Arrange: Counter with unspecified/unmapped CV tag (e.g. 999) -> falls back to COUNTER0 (Tag 116)
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctd, cuTagIndex: 1, resetTagIndex: null, cvTagIndex: 999, presetValue: 15, qTagIndex: 8));

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(SPLC_CounterMode.CTD, counter.Mode);
        Assert.Equal(116, counter.RetainTagIndex); // Automatically resolved to COUNTER0 tag index
    }

    [Fact]
    public void TargetV2_DenseProfile_ResolvesRetainTagIndexFromDeviceResourceInfo()
    {
        // Arrange: Board Zigbee-IO with Dense Layout (DI=8, DO=8, AI=4, VFLAG=20, VREG=32 -> VREG_RETAIN 72..103, COUNTER 104..111)
        var denseResources = new ProductResourceProfile(
            DigitalInputs: 8,
            DigitalOutputs: 8,
            AnalogInputs: 4,
            VirtualFlags: 20,
            VirtualRegisters: 32,
            RetentiveRegisters: 32,
            Counters: 8);

        var denseTags = new List<TagDefinition>();
        // Add retain tags 72..103
        for (ushort i = 0; i < 32; i++)
        {
            denseTags.Add(new TagDefinition((ushort)(72 + i), $"VREG_RETAIN{i}", TagKind.VirtualRegisterRetain, TagDataType.Int32, false));
        }
        // Add DI, DO, Counter tags
        denseTags.Add(new TagDefinition(1, "DI1", TagKind.DiscreteInput, TagDataType.Boolean, true));
        denseTags.Add(new TagDefinition(8, "DO0", TagKind.DiscreteOutput, TagDataType.Boolean, false));
        denseTags.Add(new TagDefinition(104, "COUNTER0", TagKind.Counter, TagDataType.Int32, false));

        var denseProduct = new ProductDefinition("Zigbee-IO", 1, 1, 100, denseResources, denseTags, wireProfile: 2);

        var graph = new LogicGraph();
        // Counter with CV on VREG_RETAIN at tag 75
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctu, cuTagIndex: 1, resetTagIndex: null, cvTagIndex: 75, presetValue: 50, qTagIndex: 8));

        // Act
        var result = _compiler.Compile(graph, denseProduct);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(75, counter.RetainTagIndex); // Successfully resolved to 75 dynamically!
    }
}
