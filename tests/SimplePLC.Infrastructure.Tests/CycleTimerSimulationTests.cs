using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class CycleTimerSimulationTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    private static void CommitRules(McuReferenceSimulator sim, params RuleRecordDto[] rules)
    {
        sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.RuleCountStagedAddress, (ushort)rules.Length).GetAwaiter().GetResult();
        ushort[] stagedBuf = new ushort[rules.Length * ModbusRegisterMap.RegistersPerRule];
        RegisterCodec.EncodeRuleRecords(rules, stagedBuf);

        sim.WriteMultipleRegistersAsync(1, ModbusRegisterMap.StagingRuleTableBaseAddress, stagedBuf).GetAwaiter().GetResult();

        ushort crc = Crc16Modbus.ComputeFromRegisters(stagedBuf);
        sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.ExpectedCrc16Address, crc).GetAwaiter().GetResult();
        sim.WriteSingleRegisterAsync(1, ModbusRegisterMap.CommitCommandAddress, ModbusRegisterMap.CommitMagic).GetAwaiter().GetResult();
    }

    [Fact]
    public void CycleTimer_Simulation_OscillatesCorrectly_AndStopsImmediatelyOnInputDrop()
    {
        // Arrange: Cycle Timer with OnTime = 200ms, OffTime = 400ms, StartWithOn = true
        var graph = new LogicGraph();
        var timerNode = LogicNode.CreateTimer(
            id: "CYC1",
            type: TimerMacroType.Cycle,
            inTagIndex: 0, // DI0
            presetMs: 200,
            qTagIndex: 8,  // DO0
            executionOrder: 0,
            label: "CYC_TEST",
            onTimeMs: 200,
            offTimeMs: 400,
            startWithOn: true);

        graph.Nodes.Add(timerNode);

        var compileResult = _compiler.Compile(graph, _product);
        Assert.True(compileResult.IsSuccess);

        var sim = new McuReferenceSimulator();
        var dtos = compileResult.Program!.Rules.Select(r => RuleMapper.ToDto(r)).ToArray();
        CommitRules(sim, dtos);

        // Initial scan: DI0 = 0 -> DO0 must be 0
        sim.ExecuteScanPass(50);
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Step 1: Turn DI0 = 1
        sim.Control.SetTagValue(0, 1);
        sim.ExecuteScanPass(50);
        // ON First: Immediately DO0 = 1
        Assert.Equal(1, sim.Control.GetTagValue(8));

        // Advance 100ms (total 150ms of ON phase < 200ms) -> DO0 still 1
        sim.ExecuteScanPass(100);
        Assert.Equal(1, sim.Control.GetTagValue(8));

        // Advance 100ms (total 250ms >= 200ms OnTime) -> DO0 turns OFF (0)
        sim.ExecuteScanPass(100);
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Advance 200ms (in OFF phase, total 300ms < 400ms OffTime) -> DO0 still 0
        sim.ExecuteScanPass(200);
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Advance 150ms (total 450ms >= 400ms OffTime) -> DO0 must turn ON (1) again!
        sim.ExecuteScanPass(150);
        Assert.Equal(1, sim.Control.GetTagValue(8));

        // Step 2: Fail-safe test - while DO0 is ON, drop DI0 to 0
        sim.Control.SetTagValue(0, 0);
        sim.ExecuteScanPass(50);
        // Must immediately drop DO0 to 0!
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Advance time significantly while DI0 stays 0 -> DO0 must never turn ON again
        sim.ExecuteScanPass(500);
        sim.ExecuteScanPass(500);
        Assert.Equal(0, sim.Control.GetTagValue(8));
    }

    [Fact]
    public void CycleTimer_StartWithOff_WaitsOffDurationBeforeFirstOn()
    {
        // Arrange: StartWithOn = false, OffTime = 300ms, OnTime = 200ms
        var graph = new LogicGraph();
        var timerNode = LogicNode.CreateTimer(
            id: "CYC_OFF_FIRST",
            type: TimerMacroType.Cycle,
            inTagIndex: 0,
            presetMs: 200,
            qTagIndex: 8,
            onTimeMs: 200,
            offTimeMs: 300,
            startWithOn: false);

        graph.Nodes.Add(timerNode);

        var compileResult = _compiler.Compile(graph, _product);
        Assert.True(compileResult.IsSuccess);

        var sim = new McuReferenceSimulator();
        var dtos = compileResult.Program!.Rules.Select(r => RuleMapper.ToDto(r)).ToArray();
        CommitRules(sim, dtos);

        // Turn DI0 = 1
        sim.Control.SetTagValue(0, 1);
        sim.ExecuteScanPass(50);
        // Start With OFF: DO0 must still be 0!
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Advance 200ms (total 250ms of 300ms OffTime) -> still 0
        sim.ExecuteScanPass(200);
        Assert.Equal(0, sim.Control.GetTagValue(8));

        // Advance 100ms (total 350ms, crossed 300ms OffTime) -> DO0 turns ON (1)
        sim.ExecuteScanPass(100);
        Assert.Equal(1, sim.Control.GetTagValue(8));
    }

    [Fact]
    public void HardwareFB_CycleTimer_CountsAndTogglesPeriodically()
    {
        var sim = new McuReferenceSimulator();
        var timer = new FbTimerRecordDto
        {
            Mode = SPLC_TimerMode.CYCLE,
            PresetMs = 200, // 200ms toggle period
            In = true
        };
        sim.Control.SetTimer(0, timer);

        // Scan 1: 100ms -> Elapsed = 100ms, Q = false
        sim.ExecuteScanPass(100);
        var t = sim.Control.GetTimer(0);
        Assert.Equal(100u, t.ElapsedMs);
        Assert.False(t.Q);

        // Scan 2: 120ms (total 220ms >= 200ms) -> Elapsed = 0, Q toggles to true!
        sim.ExecuteScanPass(120);
        t = sim.Control.GetTimer(0);
        Assert.True(t.Q);

        // Scan 3: drop In = false -> resets immediately
        timer = sim.Control.GetTimer(0);
        timer.In = false;
        sim.Control.SetTimer(0, timer);
        sim.ExecuteScanPass(10);
        t = sim.Control.GetTimer(0);
        Assert.False(t.Q);
        Assert.Equal(0u, t.ElapsedMs);
    }
}
