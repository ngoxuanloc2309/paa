namespace SimplePLC.Application.Tests;

using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

/// <summary>
/// Executable Acceptance Tests based on Section 4.2 of SimplePLC Modbus Register Map & Hardware Profiles.
/// Đảm bảo tính khách quan (Clean Code): Kiểm tra hành vi biên dịch độc lập trên nhiều cấu hình phần cứng
/// (Standard 8DI/8DO và Compact 4DI/4DO), không hardcode chỉ số tĩnh.
/// </summary>
public class WorkedExamplesAcceptanceTests
{
    private readonly RuleCompiler _compiler = new();

    private static ProductDefinition CreateProduct(ushort diCount, ushort doCount, ushort aiCount)
    {
        var resources = new ProductResourceProfile(
            DigitalInputs: diCount,
            DigitalOutputs: doCount,
            AnalogInputs: aiCount,
            VirtualFlags: 16,
            VirtualRegisters: 16,
            RetentiveRegisters: 8,
            Counters: 4);

        return DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources);
    }

    [Theory]
    [InlineData(8, 8, 4, 8)]  // Standard Board: DO0 mang index = 8
    [InlineData(4, 4, 2, 4)]  // Compact Board:  DO0 mang index = 4
    [Trait("Category", "SpecificationAcceptance")]
    public void Example1_StartLamp_ProducesExactlyOneRule_OnAnyHardware(
        ushort diCount, ushort doCount, ushort aiCount, ushort expectedDo0Index)
    {
        var product = CreateProduct(diCount, doCount, aiCount);
        var di0Start = product.Tags.First(t => t.Name == "DI0");
        var do0Green = product.Tags.First(t => t.Name == "DO0");

        Assert.Equal(expectedDo0Index, do0Green.TagIndex);

        // IF Start (DI0) ON_RISE THEN GreenLight (DO0) = ON (1)
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_START", di0Start.TagIndex, "StartButton"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_START", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_GREEN", do0Green.TagIndex, ActionKind.SetTag, 1, executionOrder: 0, label: "TurnOnGreen"));

        graph.Edges.Add(new LogicEdge("IN_START", "TR_START"));
        graph.Edges.Add(new LogicEdge("TR_START", "ACT_GREEN"));

        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(di0Start.TagIndex, rule.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, rule.Trigger.Type);
        Assert.Equal(do0Green.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, rule.Action.Type);
        Assert.Equal(1, rule.Action.Parameter);
        Assert.False(rule.Guard.HasGuard);
    }

    [Theory]
    [InlineData(8, 8, 4)]
    [InlineData(4, 4, 2)]
    [Trait("Category", "SpecificationAcceptance")]
    public void Example2_ProductCounter_ProducesExactlyOneRule_OnAnyHardware(
        ushort diCount, ushort doCount, ushort aiCount)
    {
        var product = CreateProduct(diCount, doCount, aiCount);
        var di1Sensor = product.Tags.First(t => t.Name == "DI1");
        var counterTag = product.Tags.First(t => t.Kind == TagKind.Counter);

        // IF ProductSensor (DI1) ON_RISE THEN Counter0 += 1
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_SENSOR", di1Sensor.TagIndex, "ProductSensor"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_PULSE", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_INC", counterTag.TagIndex, ActionKind.IncrementCounter, 1, executionOrder: 0, label: "IncCounter"));

        graph.Edges.Add(new LogicEdge("IN_SENSOR", "TR_PULSE"));
        graph.Edges.Add(new LogicEdge("TR_PULSE", "ACT_INC"));

        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(di1Sensor.TagIndex, rule.Trigger.Tag.TagIndex);
        Assert.Equal(counterTag.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.IncrementCounter, rule.Action.Type);
        Assert.Equal(1, rule.Action.Parameter);
    }

    [Theory]
    [InlineData(8, 8, 4)]
    [InlineData(4, 4, 2)]
    [Trait("Category", "SpecificationAcceptance")]
    public void Example3_PersistentProductionTotal_ProducesExactlyOneRule_OnAnyHardware(
        ushort diCount, ushort doCount, ushort aiCount)
    {
        var product = CreateProduct(diCount, doCount, aiCount);
        var di1Sensor = product.Tags.First(t => t.Name == "DI1");
        var retainTag = product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain);

        // IF ProductSensor (DI1) ON_RISE THEN VREG_RETAIN0 += 1
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_SENSOR", di1Sensor.TagIndex, "ProductSensor"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_PULSE", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_RETAIN", retainTag.TagIndex, ActionKind.IncrementCounter, 1, executionOrder: 0, label: "IncRetainTotal"));

        graph.Edges.Add(new LogicEdge("IN_SENSOR", "TR_PULSE"));
        graph.Edges.Add(new LogicEdge("TR_PULSE", "ACT_RETAIN"));

        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(retainTag.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.IncrementCounter, rule.Action.Type);
    }

    [Theory]
    [InlineData(8, 8, 4, 8)]
    [InlineData(4, 4, 2, 4)]
    [Trait("Category", "SpecificationAcceptance")]
    public void Example4_MultiStepViaVirtualState_ProducesTwoRules_OnAnyHardware(
        ushort diCount, ushort doCount, ushort aiCount, ushort expectedDo0Index)
    {
        var product = CreateProduct(diCount, doCount, aiCount);
        var di0Start = product.Tags.First(t => t.Name == "DI0");
        var do0Green = product.Tags.First(t => t.Name == "DO0");
        var vflag0 = product.Tags.First(t => t.Kind == TagKind.VirtualFlag);

        Assert.Equal(expectedDo0Index, do0Green.TagIndex);

        var graph = new LogicGraph();
        // Chain 1: IF Start (DI0) ON_RISE THEN VFLAG0 = 1
        graph.Nodes.Add(LogicNode.CreateInput("IN_START", di0Start.TagIndex, "StartButton"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_START", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_VFLAG", vflag0.TagIndex, ActionKind.SetTag, 1, executionOrder: 0, label: "SetStep1Active"));

        graph.Edges.Add(new LogicEdge("IN_START", "TR_START"));
        graph.Edges.Add(new LogicEdge("TR_START", "ACT_VFLAG"));

        // Chain 2: IF VFLAG0 ON_RISE THEN DO0 = 1
        graph.Nodes.Add(LogicNode.CreateInput("IN_VFLAG", vflag0.TagIndex, "Step1State"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_VFLAG", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_GREEN", do0Green.TagIndex, ActionKind.SetTag, 1, executionOrder: 1, label: "TurnOnGreenLamp"));

        graph.Edges.Add(new LogicEdge("IN_VFLAG", "TR_VFLAG"));
        graph.Edges.Add(new LogicEdge("TR_VFLAG", "ACT_GREEN"));

        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.Rules.Count);

        var r0 = result.Program.Rules[0];
        Assert.Equal(0, r0.RuleIndex);
        Assert.Equal(di0Start.TagIndex, r0.Trigger.Tag.TagIndex);
        Assert.Equal(vflag0.TagIndex, r0.Action.TargetTag.TagIndex);

        var r1 = result.Program.Rules[1];
        Assert.Equal(1, r1.RuleIndex);
        Assert.Equal(vflag0.TagIndex, r1.Trigger.Tag.TagIndex);
        Assert.Equal(do0Green.TagIndex, r1.Action.TargetTag.TagIndex);
    }

    [Theory]
    [InlineData(8, 8, 4)]
    [InlineData(4, 4, 2)]
    [Trait("Category", "SpecificationAcceptance")]
    public void Example5_AnalogThresholdFloat_ProducesAccurateRule_OnAnyHardware(
        ushort diCount, ushort doCount, ushort aiCount)
    {
        var product = CreateProduct(diCount, doCount, aiCount);
        var ai0Temp = product.Tags.First(t => t.Name == "AI0");
        var do0Cooling = product.Tags.First(t => t.Name == "DO0");

        // IF Temperature (AI0) >= 75.5 THEN CoolingFan (DO0) = 1
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_TEMP", ai0Temp.TagIndex, "TempSensor"));
        graph.Nodes.Add(LogicNode.CreateComparator("COMP_HIGH", ai0Temp.TagIndex, CompareOperator.GreaterThanOrEqual, thresholdLo: 75.5, thresholdHi: 0));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_COOL", do0Cooling.TagIndex, ActionKind.SetTag, 1, executionOrder: 0, label: "EnableCoolingFan"));

        graph.Edges.Add(new LogicEdge("IN_TEMP", "COMP_HIGH"));
        graph.Edges.Add(new LogicEdge("COMP_HIGH", "ACT_COOL"));

        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(ai0Temp.TagIndex, rule.Trigger.Tag.TagIndex);
        Assert.Equal(CompareOperator.GreaterThanOrEqual, rule.Trigger.CompareOp);
        Assert.Equal(75.5, rule.Trigger.ThresholdLo, tolerance: 1e-4);
        Assert.True(rule.Trigger.IsFloat);
        Assert.Equal(do0Cooling.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(1, rule.Action.Parameter);
    }
}

