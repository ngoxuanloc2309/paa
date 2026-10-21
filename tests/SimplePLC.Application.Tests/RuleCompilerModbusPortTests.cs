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
/// Bộ kiểm thử độc lập cho tính năng biên dịch đa cổng RS-485 Modbus Master (Component 2).
/// Kiểm chứng ánh xạ PortIndex từ các node FBD trên Canvas vào CompiledProgram DTOs.
/// </summary>
public sealed class RuleCompilerModbusPortTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void ModbusRead_CompilesWithCustomPortIndex()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_EN", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateModbusRead(
            id: "MB_READ_1",
            enTag: di0.TagIndex,
            slaveId: 2,
            functionCode: ModbusMasterFunctionCode.ReadHoldingRegisters,
            startAddress: 100,
            regCount: 4,
            pollIntervalMs: 250,
            timeoutMs: 300,
            pins: new List<ModbusMasterPinBindingDto>
            {
                new() { Id = "P0", Label = "Temp", RegOffset = 0, DataType = ModbusDataType.Int16, TargetTagIndex = vreg0.TagIndex }
            },
            portIndex: 1)); // RS-485 Port 2

        graph.Edges.Add(new LogicEdge("IN_EN", "MB_READ_1", "Out", "EN"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.ModbusMasterConfigs);

        var config = result.Program.ModbusMasterConfigs[0];
        Assert.Equal("MB_READ_1", config.NodeId);
        Assert.Equal((byte)1, config.PortIndex); // Port 2
        Assert.Equal((byte)2, config.SlaveId);
        Assert.Equal((ushort)100, config.StartAddress);
    }

    [Fact]
    public void ModbusWrite_CompilesWithCustomPortIndex()
    {
        var di1 = _product.FindTagByName("DI1")!;
        var vreg1 = _product.FindTagByName("VREG1")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_TRIG", di1.TagIndex));
        graph.Nodes.Add(LogicNode.CreateModbusWrite(
            id: "MB_WRITE_1",
            trigTag: di1.TagIndex,
            slaveId: 5,
            writeMode: ModbusWriteMode.ChannelIndexed,
            functionCode: ModbusMasterWriteFunctionCode.WriteSingleRegister,
            baseAddressCommand: 0x0100,
            baseAddressParam: 0x0120,
            channelCount: 9,
            isExclusiveChannel: true,
            triggerMode: ModbusWriteTriggerMode.OnTriggerRisingEdge,
            cyclicIntervalMs: 500,
            timeoutMs: 250,
            channelTag: vreg1.TagIndex,
            portIndex: 3)); // RS-485 Port 4

        graph.Edges.Add(new LogicEdge("IN_TRIG", "MB_WRITE_1", "Out", "Trig"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.ModbusMasterWriteConfigs);

        var config = result.Program.ModbusMasterWriteConfigs[0];
        Assert.Equal("MB_WRITE_1", config.NodeId);
        Assert.Equal((byte)3, config.PortIndex); // Port 4
        Assert.Equal((byte)5, config.SlaveId);
        Assert.Equal((ushort)0x0100, config.BaseAddressCommand);
    }

    [Fact]
    public void MultiPortConcurrent_CompilesDispatchedCorrectly()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var di1 = _product.FindTagByName("DI1")!;

        var graph = new LogicGraph();
        // Port 1 (index 0) Poller
        graph.Nodes.Add(LogicNode.CreateModbusRead("RD_PORT1", enTag: di0.TagIndex, slaveId: 1, portIndex: 0));
        // Port 2 (index 1) Poller
        graph.Nodes.Add(LogicNode.CreateModbusRead("RD_PORT2", enTag: di1.TagIndex, slaveId: 2, portIndex: 1));
        // Port 3 (index 2) Actuator
        graph.Nodes.Add(LogicNode.CreateModbusWrite("WR_PORT3", trigTag: di0.TagIndex, slaveId: 3, portIndex: 2));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.ModbusMasterConfigs.Count);
        Assert.Single(result.Program.ModbusMasterWriteConfigs);

        Assert.Equal((byte)0, result.Program.ModbusMasterConfigs[0].PortIndex);
        Assert.Equal((byte)1, result.Program.ModbusMasterConfigs[1].PortIndex);
        Assert.Equal((byte)2, result.Program.ModbusMasterWriteConfigs[0].PortIndex);
    }
}
