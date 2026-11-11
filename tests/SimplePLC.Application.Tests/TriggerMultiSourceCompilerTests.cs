namespace SimplePLC.Application.Tests;

using System.Collections.Generic;
using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

/// <summary>
/// Unit tests asserting multi-source tag resolution for Trigger, Comparator, and Guard nodes
/// when connected to MB_READ, MQTT_SUB, SCALE, TIMER, or COUNTER.
/// </summary>
public class TriggerMultiSourceCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    /// <summary>
    /// [Happy Path]: MB_READ Pin (Total_kWh -> VREG0) kết nối vào Trigger (OnRise) -> Action (DO0 = 1).
    /// Compiler phải phân giải thành công tag VREG0 từ chân pin và biên dịch thành công 0 lỗi.
    /// </summary>
    [Fact]
    public void Compile_ModbusReadPin_ConnectedToTrigger_CompilesSuccessfullyWithTargetTag()
    {
        // Arrange
        var vreg0 = _product.FindTagByName("VREG0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();

        var mbNode = new LogicNode
        {
            Id = "node_mb_read",
            Kind = LogicNodeKind.ModbusRead,
            Label = "SELEC MFM383A-C",
            ModbusReadData = new ModbusReadNodeData
            {
                PortIndex = 0,
                SlaveId = 1,
                FunctionCode = ModbusMasterFunctionCode.ReadInputRegisters,
                StartAddress = 0,
                RegCount = 2,
                ReadMode = ModbusReadMode.Cyclic,
                PollIntervalMs = 1000,
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new()
                    {
                        Id = "pin_total_kwh",
                        Label = "Total_kWh",
                        RegOffset = 0,
                        DataType = ModbusDataType.Real,
                        TargetTagIndex = vreg0.TagIndex
                    }
                }
            }
        };

        var triggerNode = LogicNode.CreateTrigger(
            "node_trig_1",
            type: TriggerKind.OnRise,
            label: "Trigger");

        var actionNode = LogicNode.CreateAction(
            "node_act_1",
            targetTagIndex: do0.TagIndex,
            type: ActionKind.SetTag,
            parameter: 1,
            label: "Action");

        graph.Nodes.Add(mbNode);
        graph.Nodes.Add(triggerNode);
        graph.Nodes.Add(actionNode);

        // Edge 1: MB_READ.Total_kWh -> Trigger.In
        graph.Edges.Add(new LogicEdge("node_mb_read", "node_trig_1", "Total_kWh", "In"));
        // Edge 2: Trigger.Out -> Action.In
        graph.Edges.Add(new LogicEdge("node_trig_1", "node_act_1", "Out", "In"));

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Program);
        Assert.Contains(result.Program.Rules, r => r.Trigger.Tag.TagIndex == vreg0.TagIndex && r.Action.TargetTag.TagIndex == do0.TagIndex);
    }

    /// <summary>
    /// [Happy Path]: MQTT_SUB Pin kết nối vào Trigger -> Action biên dịch thành công tag đích của Pin.
    /// </summary>
    [Fact]
    public void Compile_MqttSubscribePin_ConnectedToTrigger_CompilesSuccessfully()
    {
        // Arrange
        var vreg1 = _product.FindTagByName("VREG1")!;
        var do0 = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();

        var mqttNode = new LogicNode
        {
            Id = "node_mqtt_sub",
            Kind = LogicNodeKind.MqttSubscribe,
            Label = "MQTT Inbound",
            MqttSubscribeData = new MqttSubscribeNodeData
            {
                Topic = "factory/sensor",
                Pins = new List<MqttPinBindingDto>
                {
                    new()
                    {
                        Id = "pin_temp",
                        Label = "Temperature",
                        DataType = MqttDataType.Int32,
                        TargetTagIndex = vreg1.TagIndex
                    }
                }
            }
        };

        var triggerNode = LogicNode.CreateTrigger("node_trig_mqtt", type: TriggerKind.OnChange, label: "TrigMQTT");
        var actionNode = LogicNode.CreateAction("node_act_mqtt", targetTagIndex: do0.TagIndex, type: ActionKind.SetTag, parameter: 1);

        graph.Nodes.Add(mqttNode);
        graph.Nodes.Add(triggerNode);
        graph.Nodes.Add(actionNode);

        graph.Edges.Add(new LogicEdge("node_mqtt_sub", "node_trig_mqtt", "Temperature", "In"));
        graph.Edges.Add(new LogicEdge("node_trig_mqtt", "node_act_mqtt", "Out", "In"));

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Program);
        Assert.Contains(result.Program.Rules, r => r.Trigger.Tag.TagIndex == vreg1.TagIndex && r.Action.TargetTag.TagIndex == do0.TagIndex);
    }

    /// <summary>
    /// [Happy Path]: MB_READ Pin kết nối vào Comparator -> Action biên dịch thành công.
    /// </summary>
    [Fact]
    public void Compile_ModbusReadPin_ConnectedToComparator_CompilesSuccessfully()
    {
        // Arrange
        var vreg0 = _product.FindTagByName("VREG0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();

        var mbNode = new LogicNode
        {
            Id = "node_mb_read_cmp",
            Kind = LogicNodeKind.ModbusRead,
            ModbusReadData = new ModbusReadNodeData
            {
                Pins = new List<ModbusMasterPinBindingDto>
                {
                    new()
                    {
                        Id = "pin_kwh",
                        Label = "Energy",
                        TargetTagIndex = vreg0.TagIndex
                    }
                }
            }
        };

        var compNode = LogicNode.CreateComparator("node_cmp", inTagIndex: vreg0.TagIndex, op: CompareOperator.GreaterThan, thresholdLo: 100);
        var actionNode = LogicNode.CreateAction("node_act_cmp", targetTagIndex: do0.TagIndex, type: ActionKind.SetTag, parameter: 1);

        graph.Nodes.Add(mbNode);
        graph.Nodes.Add(compNode);
        graph.Nodes.Add(actionNode);

        graph.Edges.Add(new LogicEdge("node_mb_read_cmp", "node_cmp", "Energy", "In"));
        graph.Edges.Add(new LogicEdge("node_cmp", "node_act_cmp", "Out", "In"));

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Program);
        Assert.Contains(result.Program.Rules, r => r.Trigger.Tag.TagIndex == vreg0.TagIndex && r.Trigger.CompareOp == CompareOperator.GreaterThan);
    }

    /// <summary>
    /// [Happy Path]: Trigger được cấu hình trực tiếp InputTagIndex (từ UI binding) không cần dây ngược dòng vẫn biên dịch chuẩn xác.
    /// </summary>
    [Fact]
    public void Compile_TriggerWithDirectInputTagIndex_WithoutUpstreamWire_CompilesSuccessfully()
    {
        // Arrange
        var vflag0 = _product.FindTagByName("VFLAG0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();

        var triggerNode = LogicNode.CreateTrigger(
            "node_direct_trig",
            type: TriggerKind.OnRise,
            inputTagIndex: vflag0.TagIndex);

        var actionNode = LogicNode.CreateAction("node_direct_act", targetTagIndex: do0.TagIndex, type: ActionKind.SetTag, parameter: 1);

        graph.Nodes.Add(triggerNode);
        graph.Nodes.Add(actionNode);

        graph.Edges.Add(new LogicEdge("node_direct_trig", "node_direct_act", "Out", "In"));

        // Act
        var result = _compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics));
        Assert.NotNull(result.Program);
        Assert.Contains(result.Program.Rules, r => r.Trigger.Tag.TagIndex == vflag0.TagIndex && r.Action.TargetTag.TagIndex == do0.TagIndex);
    }
}
