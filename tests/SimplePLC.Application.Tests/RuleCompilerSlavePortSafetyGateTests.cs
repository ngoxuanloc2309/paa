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

public sealed class RuleCompilerSlavePortSafetyGateTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void ModbusRead_OnMasterPort_CompilesSuccessfully()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var vreg0 = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_EN", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateModbusRead(
            id: "MB_READ_1",
            enTag: di0.TagIndex,
            slaveId: 2,
            portIndex: 0)); // Port 1 (Master)
        graph.Edges.Add(new LogicEdge("IN_EN", "MB_READ_1", "Out", "EN"));

        var ports = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Master }
        };

        var result = _compiler.Compile(graph, _product, "PROG1", ports, null);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.ModbusMasterConfigs);
    }

    [Fact]
    public void ModbusRead_OnSlavePort_EmitsCompilerError()
    {
        var di0 = _product.FindTagByName("DI0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_EN", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateModbusRead(
            id: "MB_READ_SLAVE",
            enTag: di0.TagIndex,
            slaveId: 2,
            portIndex: 1)); // Port 2 (Slave)
        graph.Edges.Add(new LogicEdge("IN_EN", "MB_READ_SLAVE", "Out", "EN"));

        var ports = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Master },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = ModbusPortRole.Slave, SlaveId = 5 }
        };

        var result = _compiler.Compile(graph, _product, "PROG1", ports, null);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrModbusPortSlaveConflict && d.Severity == DiagnosticSeverity.Error);
        var err = result.Diagnostics.First(d => d.Code == RuleCompiler.ErrModbusPortSlaveConflict);
        Assert.Contains("Modbus Slave", err.Message);
        Assert.Equal("MB_READ_SLAVE", err.NodeId);
    }

    [Fact]
    public void ModbusWrite_OnSlavePort_EmitsCompilerError()
    {
        var di1 = _product.FindTagByName("DI1")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_TRIG", di1.TagIndex));
        graph.Nodes.Add(LogicNode.CreateModbusWrite(
            id: "MB_WRITE_SLAVE",
            trigTag: di1.TagIndex,
            slaveId: 2,
            portIndex: 1)); // Port 2 (Slave)
        graph.Edges.Add(new LogicEdge("IN_TRIG", "MB_WRITE_SLAVE", "Out", "Trig"));

        var ports = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Master },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = ModbusPortRole.Slave, SlaveId = 5 }
        };

        var result = _compiler.Compile(graph, _product, "PROG1", ports, null);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrModbusPortSlaveConflict && d.Severity == DiagnosticSeverity.Error);
        var err = result.Diagnostics.First(d => d.Code == RuleCompiler.ErrModbusPortSlaveConflict);
        Assert.Contains("Modbus Slave", err.Message);
        Assert.Equal("MB_WRITE_SLAVE", err.NodeId);
    }
}
