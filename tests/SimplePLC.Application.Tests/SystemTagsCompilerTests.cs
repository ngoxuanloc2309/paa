using System;
using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Bộ kiểm thử biên dịch System Tags và kết nối trực tiếp Input/System Tag vào Action/Guard
/// tuân thủ nghiêm ngặt chuẩn IEC 61508-3 và kiến trúc Clean Architecture.
/// </summary>
public sealed class SystemTagsCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    /// <summary>
    /// [Happy Path]: Kết nối trực tiếp System Tag (SYS_CLOCK_1S) vào Action DO0 được biên dịch thành công
    /// với trigger OnRise tự động sinh bởi RuleCompiler.
    /// </summary>
    [Fact]
    public void DirectInputToAction_WithSysClock1S_CompilesSuccessfullyWithOnRiseTrigger()
    {
        // Arrange
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("node_in", ModbusRegisterMap.SysIndexClock1S, "SYS_CLOCK_1S"));
        graph.Nodes.Add(LogicNode.CreateAction("node_act", 8, ActionKind.SetTag, 1, executionOrder: 1, "DO0 = 1"));
        graph.Edges.Add(new LogicEdge("node_in", "node_act"));

        var compiler = new RuleCompiler();

        // Act
        var result = compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        Assert.Single(result.Program!.Rules);
        var rule = result.Program.Rules[0];
        Assert.NotNull(rule.Trigger.Tag);
        Assert.Equal(ModbusRegisterMap.SysIndexClock1S, rule.Trigger.Tag!.TagIndex);
        Assert.Equal(TriggerKind.OnRise, rule.Trigger.Type);
        Assert.NotNull(rule.Action.TargetTag);
        Assert.Equal(8, rule.Action.TargetTag!.TagIndex);
    }

    /// <summary>
    /// [Happy Path]: Kết nối trực tiếp System Tag (SYS_ON) qua Guard vào Action DO1 được biên dịch thành công.
    /// </summary>
    [Fact]
    public void DirectInputToGuardToAction_WithSysAlwaysOn_CompilesSuccessfully()
    {
        // Arrange
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("node_in", ModbusRegisterMap.SysIndexAlwaysOn, "SYS_ON"));
        graph.Nodes.Add(LogicNode.CreateGuard("node_guard", 0, negated: false, "DI0 == 1")); // Guard check DI0
        graph.Nodes.Add(LogicNode.CreateAction("node_act", 9, ActionKind.SetTag, 1, executionOrder: 1, "DO1 = 1"));
        graph.Edges.Add(new LogicEdge("node_in", "node_guard"));
        graph.Edges.Add(new LogicEdge("node_guard", "node_act"));

        var compiler = new RuleCompiler();

        // Act
        var result = compiler.Compile(graph, _product);

        // Assert
        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        Assert.Single(result.Program!.Rules);
        var rule = result.Program.Rules[0];
        Assert.NotNull(rule.Trigger.Tag);
        Assert.Equal(ModbusRegisterMap.SysIndexAlwaysOn, rule.Trigger.Tag!.TagIndex);
        Assert.True(rule.Guard.HasGuard);
        Assert.Equal((ushort)0, rule.Guard.Tag?.TagIndex);
    }

    /// <summary>
    /// [Fault Injection]: Action cố gắng ghi đè vào System Tag (Read-Only) phải bị từ chối
    /// với mã lỗi SPLC-COMP-ACT-RO (ErrActionReadOnlyTarget).
    /// </summary>
    [Fact]
    public void ActionWritingToSystemTag_IsRejectedWithReadOnlyDiagnostic()
    {
        // Arrange
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("node_in", 0, "DI0"));
        graph.Nodes.Add(LogicNode.CreateTrigger("node_trig", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("node_act", ModbusRegisterMap.SysIndexAlwaysOn, ActionKind.SetTag, 1, executionOrder: 1, "Write to SYS_ON"));
        graph.Edges.Add(new LogicEdge("node_in", "node_trig"));
        graph.Edges.Add(new LogicEdge("node_trig", "node_act"));

        var compiler = new RuleCompiler();

        // Act
        var result = compiler.Compile(graph, _product);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrActionReadOnlyTarget);
    }

    /// <summary>
    /// [Boundary & Range]: ProductDefinition phân giải chính xác cả 8 System Tags từ 1000..1007
    /// và từ chối các chỉ số ngoài biên 999 hoặc 1008.
    /// </summary>
    [Fact]
    public void ProductDefinition_ResolvesSystemTags_WithinValidRange()
    {
        // Arrange & Act & Assert
        Assert.NotNull(_product.FindTagByIndex(1000));
        Assert.Equal("SYS_FIRST_SCAN", _product.FindTagByIndex(1000)?.Name);
        Assert.True(_product.FindTagByIndex(1000)?.IsReadOnly);

        Assert.NotNull(_product.FindTagByIndex(1001));
        Assert.Equal("SYS_ON", _product.FindTagByIndex(1001)?.Name);

        Assert.NotNull(_product.FindTagByIndex(1002));
        Assert.Equal("SYS_OFF", _product.FindTagByIndex(1002)?.Name);

        Assert.NotNull(_product.FindTagByIndex(1003));
        Assert.Equal("SYS_CLOCK_1S", _product.FindTagByIndex(1003)?.Name);

        Assert.NotNull(_product.FindTagByIndex(1004));
        Assert.Equal("SYS_CLOCK_100MS", _product.FindTagByIndex(1004)?.Name);

        Assert.NotNull(_product.FindTagByIndex(1005));
        Assert.Equal("SYS_RTC_HOUR", _product.FindTagByIndex(1005)?.Name);
        Assert.Equal(TagDataType.Int32, _product.FindTagByIndex(1005)?.DataType);

        Assert.NotNull(_product.FindTagByIndex(1006));
        Assert.Equal("SYS_RTC_MINUTE", _product.FindTagByIndex(1006)?.Name);

        Assert.NotNull(_product.FindTagByIndex(1007));
        Assert.Equal("SYS_COMM_OK", _product.FindTagByIndex(1007)?.Name);

        // Out of bounds
        Assert.Null(_product.FindTagByIndex(999));
        Assert.Null(_product.FindTagByIndex(1008));
    }
}
