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
        var di0 = _productV2.FindTagByName("DI0")!;
        var di1 = _productV2.FindTagByName("DI1")!;
        var di2 = _productV2.FindTagByName("DI2")!;
        var do0 = _productV2.FindTagByName("DO0")!;
        var do1 = _productV2.FindTagByName("DO1")!;
        var retain0 = _productV2.FindTagByName("VREG_RETAIN0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("T1", TimerMacroType.Ton, inTagIndex: di0.TagIndex, presetMs: 5000, qTagIndex: do0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctu, cuTagIndex: di1.TagIndex, resetTagIndex: di2.TagIndex, cvTagIndex: retain0.TagIndex, presetValue: 20, qTagIndex: do1.TagIndex));

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
        Assert.True(timer.RuleRef >= 1 && timer.RuleRef <= result.Program.RuleCount);
        var timingRule = result.Program.Rules[timer.RuleRef - 1];
        Assert.Equal(5000u, timingRule.Trigger.ForMs);
        Assert.Equal(do0.TagIndex, timingRule.Action.TargetTag.TagIndex);
        Assert.Equal(1, timingRule.Action.Parameter);

        Assert.Single(result.Program.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(SPLC_CounterMode.CTU, counter.Mode);
        Assert.Equal(20, counter.PresetValue);
        Assert.Equal(retain0.TagIndex, counter.RetainTagIndex); // TagIndex in VREG_RETAIN space
    }

    [Fact]
    public void TargetV1_WithTimerAndCounter_ExpandsMacroRules_ForBackwardCompatibility()
    {
        // Arrange
        var di0 = _productV1.FindTagByName("DI0")!;
        var di1 = _productV1.FindTagByName("DI1")!;
        var di2 = _productV1.FindTagByName("DI2")!;
        var do0 = _productV1.FindTagByName("DO0")!;
        var do1 = _productV1.FindTagByName("DO1")!;
        var retain0 = _productV1.FindTagByName("VREG_RETAIN0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("T1", TimerMacroType.Ton, inTagIndex: di0.TagIndex, presetMs: 5000, qTagIndex: do0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctu, cuTagIndex: di1.TagIndex, resetTagIndex: di2.TagIndex, cvTagIndex: retain0.TagIndex, presetValue: 20, qTagIndex: do1.TagIndex));

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
        var di0 = _productV2.FindTagByName("DI0")!;
        var do0 = _productV2.FindTagByName("DO0")!;
        var graph = new LogicGraph();
        for (int i = 0; i < 9; i++)
        {
            graph.Nodes.Add(LogicNode.CreateTimer($"T{i}", TimerMacroType.Ton, inTagIndex: di0.TagIndex, presetMs: 1000, qTagIndex: do0.TagIndex));
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
        var di1 = _productV2.FindTagByName("DI1")!;
        var do0 = _productV2.FindTagByName("DO0")!;
        var retain0 = _productV2.FindTagByName("VREG_RETAIN0")!;
        var graph = new LogicGraph();
        for (int i = 0; i < 9; i++)
        {
            graph.Nodes.Add(LogicNode.CreateCounter($"C{i}", CounterMacroType.Ctu, cuTagIndex: di1.TagIndex, resetTagIndex: null, cvTagIndex: retain0.TagIndex, presetValue: 10, qTagIndex: do0.TagIndex));
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
        // Arrange: Counter with CV on VREG (e.g. VREG0) which is volatile RAM, should still bind VREG0 so MCU knows where CV is stored
        var di1 = _productV2.FindTagByName("DI1")!;
        var do0 = _productV2.FindTagByName("DO0")!;
        var vreg0 = _productV2.FindTagByName("VREG0")!;
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctd, cuTagIndex: di1.TagIndex, resetTagIndex: null, cvTagIndex: vreg0.TagIndex, presetValue: 15, qTagIndex: do0.TagIndex));

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(SPLC_CounterMode.CTD, counter.Mode);
        Assert.Equal(vreg0.TagIndex, counter.RetainTagIndex); // Successfully bound to VREG tag
    }

    [Fact]
    public void TargetV2_UnspecifiedCvTag_DefaultsToDedicatedCounterTag()
    {
        // Arrange: Counter with unspecified/unmapped CV tag (e.g. 999) -> falls back to COUNTER0 tag
        var di1 = _productV2.FindTagByName("DI1")!;
        var do0 = _productV2.FindTagByName("DO0")!;
        var counter0 = _productV2.FindTagByName("COUNTER0")!;
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("C1", CounterMacroType.Ctd, cuTagIndex: di1.TagIndex, resetTagIndex: null, cvTagIndex: 999, presetValue: 15, qTagIndex: do0.TagIndex));

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Program!.FunctionBlockCounters);
        var counter = result.Program.FunctionBlockCounters[0];
        Assert.Equal(SPLC_CounterMode.CTD, counter.Mode);
        Assert.Equal(counter0.TagIndex, counter.RetainTagIndex); // Automatically resolved to COUNTER0 tag index
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

    [Fact]
    public void TargetV2_AllTimerModes_HaveValidRuleRef_PointingToTimingRule()
    {
        // Arrange
        var di0 = _productV2.FindTagByName("DI0")!;
        var di1 = _productV2.FindTagByName("DI1")!;
        var di2 = _productV2.FindTagByName("DI2")!;
        var do0 = _productV2.FindTagByName("DO0")!;
        var do1 = _productV2.FindTagByName("DO1")!;
        var do2 = _productV2.FindTagByName("DO2")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTimer("TON_1", TimerMacroType.Ton, inTagIndex: di0.TagIndex, presetMs: 1500, qTagIndex: do0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTimer("TOF_1", TimerMacroType.Tof, inTagIndex: di1.TagIndex, presetMs: 2500, qTagIndex: do1.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTimer("TP_1", TimerMacroType.Tp, inTagIndex: di2.TagIndex, presetMs: 3500, qTagIndex: do2.TagIndex));

        // Act
        var result = _compiler.Compile(graph, _productV2);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.FunctionBlockTimers.Count);

        // 1. TON: rule đếm giờ là TON_ON (ForMs = 1500, action SET DO0 = 1)
        var tonFb = result.Program.FunctionBlockTimers[0];
        Assert.Equal(SPLC_TimerMode.TON, tonFb.Mode);
        Assert.True(tonFb.RuleRef >= 1 && tonFb.RuleRef <= result.Program.RuleCount);
        var tonTimingRule = result.Program.Rules[tonFb.RuleRef - 1];
        Assert.Equal(1500u, tonTimingRule.Trigger.ForMs);
        Assert.Equal(do0.TagIndex, tonTimingRule.Action.TargetTag.TagIndex);
        Assert.Equal(1, tonTimingRule.Action.Parameter);

        // 2. TOF: rule đếm giờ là TOF_OFF (ForMs = 2500, guard DO1 == 1, action SET DO1 = 0)
        var tofFb = result.Program.FunctionBlockTimers[1];
        Assert.Equal(SPLC_TimerMode.TOF, tofFb.Mode);
        Assert.True(tofFb.RuleRef >= 1 && tofFb.RuleRef <= result.Program.RuleCount);
        var tofTimingRule = result.Program.Rules[tofFb.RuleRef - 1];
        Assert.Equal(2500u, tofTimingRule.Trigger.ForMs);
        Assert.Equal(do1.TagIndex, tofTimingRule.Action.TargetTag.TagIndex);
        Assert.Equal(0, tofTimingRule.Action.Parameter);
        Assert.Equal(do1.TagIndex, tofTimingRule.Guard.Tag?.TagIndex);

        // 3. TP: rule đếm giờ là TP_EXPIRE (trigger DO2 ON_RISE, ForMs = 3500, action SET DO2 = 0)
        var tpFb = result.Program.FunctionBlockTimers[2];
        Assert.Equal(SPLC_TimerMode.TP, tpFb.Mode);
        Assert.True(tpFb.RuleRef >= 1 && tpFb.RuleRef <= result.Program.RuleCount);
        var tpTimingRule = result.Program.Rules[tpFb.RuleRef - 1];
        Assert.Equal(3500u, tpTimingRule.Trigger.ForMs);
        Assert.Equal(do2.TagIndex, tpTimingRule.Trigger.Tag.TagIndex);
        Assert.Equal(do2.TagIndex, tpTimingRule.Action.TargetTag.TagIndex);
        Assert.Equal(0, tpTimingRule.Action.Parameter);
    }
}
