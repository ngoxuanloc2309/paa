namespace SimplePLC.Application.Tests;

using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using Xunit;

/// <summary>
/// Bộ kiểm thử chốt an toàn giới hạn số điểm/kênh của khối MB_WRITE theo ICD SPLC-SPEC-MBM-002 và RTOS buffer MCU.
/// Direct Mode: 1..16 points (tương ứng 4 dynamic pins direct_pins[4] và buffer RX 64B của MCU).
/// Channel-Indexed Mode: 1..64 channels (Sort-to-Light).
/// </summary>
public sealed class RuleCompilerModbusWriteLimitsTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void ModbusWrite_DirectMode_PointCountWithin16_CompilesSuccessfully()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateModbusWrite(
            id: "MB_WRITE_DIRECT_16",
            writeMode: ModbusWriteMode.DirectRegisters,
            pointCount: 16));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.ModbusMasterWriteConfigs);
        Assert.Equal(16, result.Program.ModbusMasterWriteConfigs[0].PointCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(256)]
    public void ModbusWrite_DirectMode_PointCountOutOfRange_ProducesDiagnosticError(ushort invalidPointCount)
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateModbusWrite(
            id: "MB_WRITE_DIRECT_INVALID",
            writeMode: ModbusWriteMode.DirectRegisters,
            pointCount: invalidPointCount));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "ERR_MB_WRITE_DIRECT_LIMIT");
    }

    [Fact]
    public void ModbusWrite_ChannelIndexed_ChannelCountWithin64_CompilesSuccessfully()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateModbusWrite(
            id: "MB_WRITE_CHANNEL_64",
            writeMode: ModbusWriteMode.ChannelIndexed,
            channelCount: 64));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.ModbusMasterWriteConfigs);
        Assert.Equal(64, result.Program.ModbusMasterWriteConfigs[0].ChannelCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    [InlineData(128)]
    public void ModbusWrite_ChannelIndexed_ChannelCountOutOfRange_ProducesDiagnosticError(ushort invalidChannelCount)
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateModbusWrite(
            id: "MB_WRITE_CHANNEL_INVALID",
            writeMode: ModbusWriteMode.ChannelIndexed,
            channelCount: invalidChannelCount));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == "ERR_MB_WRITE_CHANNEL_LIMIT");
    }
}
