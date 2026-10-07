using System.Text.Json;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class TimerMacroPersistenceAndAttributionTests
{
    private readonly TagCatalogViewModel _catalog = new();
    private readonly RuleCompiler _studioCompiler = new();

    [Fact]
    public void TimerMacro_ProjectPersistence_SaveAndReload_PreservesAllProperties()
    {
        var di0 = _catalog.AllTags.First(t => t.Name == "DI0");
        var vflag0 = _catalog.AllTags.First(t => t.Name == "VFLAG0");

        var originalNode = new TimerNodeViewModel("TON", di0, vflag0, presetMs: 4500)
        {
            Id = "TIMER_TEST_42",
            CustomLabel = "LubePumpDelay",
            Location = new System.Windows.Point(120, 240)
        };

        // 1. Export
        var pNode = LogicEditorViewModel.ExportSingleNodeData(originalNode);
        Assert.Equal("Timer", pNode.Type);
        Assert.Equal("TON", pNode.TimerMode);
        Assert.Equal(4500u, pNode.PresetMs);
        Assert.Equal("DI0", pNode.TagName);
        Assert.Equal("VFLAG0", pNode.OutputTagName);
        Assert.Equal("LubePumpDelay", pNode.CustomLabel);
        Assert.Equal(120, pNode.LocationX);
        Assert.Equal(240, pNode.LocationY);

        // 2. JSON Roundtrip
        string json = JsonSerializer.Serialize(pNode);
        var deserializedPNode = JsonSerializer.Deserialize<ProjectNodeData>(json)!;

        // 3. Import
        var editorVm = new LogicEditorViewModel(_catalog);
        var reloadedNode = editorVm.CreateNodeFromData(deserializedPNode) as TimerNodeViewModel;

        Assert.NotNull(reloadedNode);
        Assert.Equal("TON", reloadedNode.TimerMode);
        Assert.Equal(4500u, reloadedNode.PresetMs);
        Assert.Equal("DI0", reloadedNode.InputTag?.Name);
        Assert.Equal("VFLAG0", reloadedNode.OutputTag?.Name);
        Assert.Equal("LubePumpDelay", reloadedNode.CustomLabel);
    }

    [Fact]
    public void TimerMacro_RuleCompiler_AssignsCorrectMacroAttribution()
    {
        var di1 = _catalog.AllTags.First(t => t.Name == "DI1");
        var do0 = _catalog.AllTags.First(t => t.Name == "DO0");

        var timerNode = new TimerNodeViewModel("TOF", di1, do0, presetMs: 2500)
        {
            Id = "TOF_COOLING",
            CustomLabel = "CoolingFan"
        };

        var compileResult = _studioCompiler.Compile(
            new[] { timerNode },
            Array.Empty<ConnectionViewModel>(),
            _catalog);

        Assert.True(compileResult.IsValid);
        Assert.Equal(2, compileResult.Rules.Count);

        // Rule 1: TOF ON (1/2)
        var r1 = compileResult.Rules[0];
        Assert.Equal("TOF_COOLING", r1.DiagramId);
        Assert.Contains("TOF", r1.MacroAttribution);
        Assert.Contains("CoolingFan", r1.MacroAttribution);
        Assert.Contains("1/2", r1.MacroAttribution);

        // Rule 2: TOF OFF (2/2)
        var r2 = compileResult.Rules[1];
        Assert.Equal("TOF_COOLING", r2.DiagramId);
        Assert.Contains("TOF", r2.MacroAttribution);
        Assert.Contains("CoolingFan", r2.MacroAttribution);
        Assert.Contains("2/2", r2.MacroAttribution);
    }

    [Fact]
    public void TimerMacro_Roundtrip_SaveReloadCompile_ProducesIdenticalRules()
    {
        var di0 = _catalog.AllTags.First(t => t.Name == "DI0");
        var do1 = _catalog.AllTags.First(t => t.Name == "DO1");

        // Original compile
        var originalNode = new TimerNodeViewModel("TP", di0, do1, presetMs: 1200)
        {
            Id = "TP_VALVE",
            CustomLabel = "AirBlow"
        };

        var resultOriginal = _studioCompiler.Compile(
            new[] { originalNode },
            Array.Empty<ConnectionViewModel>(),
            _catalog);

        Assert.True(resultOriginal.IsValid);

        // Export -> JSON -> Import
        var pNode = LogicEditorViewModel.ExportSingleNodeData(originalNode);
        string json = JsonSerializer.Serialize(pNode);
        var deserializedPNode = JsonSerializer.Deserialize<ProjectNodeData>(json)!;

        var editorVm = new LogicEditorViewModel(_catalog);
        var reloadedNode = editorVm.CreateNodeFromData(deserializedPNode)!;

        // Compile reloaded node
        var resultReloaded = _studioCompiler.Compile(
            new[] { reloadedNode },
            Array.Empty<ConnectionViewModel>(),
            _catalog);

        Assert.True(resultReloaded.IsValid);
        Assert.Equal(resultOriginal.Rules.Count, resultReloaded.Rules.Count);

        for (int i = 0; i < resultOriginal.Rules.Count; i++)
        {
            var rOrig = resultOriginal.Rules[i];
            var rRel = resultReloaded.Rules[i];

            Assert.Equal(rOrig.TriggerType, rRel.TriggerType);
            Assert.Equal(rOrig.TriggerTag?.Name, rRel.TriggerTag?.Name);
            Assert.Equal(rOrig.ForMs, rRel.ForMs);
            Assert.Equal(rOrig.ActionType, rRel.ActionType);
            Assert.Equal(rOrig.ActionTag?.Name, rRel.ActionTag?.Name);
            Assert.Equal(rOrig.ActionParam, rRel.ActionParam);
            Assert.Equal(rOrig.MacroAttribution, rRel.MacroAttribution);
        }
    }

    [Fact]
    public void TimerMacro_OfflineSimulation_DerivesElapsedTimeAndState()
    {
        var editorVm = new LogicEditorViewModel(_catalog, uiDispatcher: action => action());
        var di0 = _catalog.AllTags.First(t => t.Name == "DI0");
        var do0 = _catalog.AllTags.First(t => t.Name == "DO0");

        var inputNode = new InputNodeViewModel(di0) { Id = "INP_DI0" };
        var timerNode = new TimerNodeViewModel("TON", di0, do0, presetMs: 2000) { Id = "TMR_TON" };

        editorVm.Nodes.Add(inputNode);
        editorVm.Nodes.Add(timerNode);

        var conn = new ConnectionViewModel(inputNode.OutputConnectors[0], timerNode.InputConnectors[0]);
        editorVm.Connections.Add(conn);

        // Compile
        editorVm.CompileAndSaveRules(isAutoSync: true);
        Assert.Equal(CompileState.Valid, editorVm.CompileState);
        Assert.NotNull(editorVm.CurrentProgram);

        // Initial sim setup
        editorVm.StepSimScan();
        Assert.True(editorVm.IsSimulationMode);
        Assert.False(timerNode.IsTiming);
        Assert.Equal(0u, timerNode.ElapsedMs);
        Assert.False(timerNode.IsLiveActive);

        // Turn DI0 = 1, start dwell at current timestamp
        di0.Value = 1;
        editorVm.RunSimScan(stepMs: 0, animate: false);

        // Advance clock by 1000ms
        editorVm.AdvanceSimClock(1000);
        editorVm.RunSimScan(stepMs: 1000, animate: false);

        // Verify dwell in-progress
        Assert.True(timerNode.IsTiming);
        Assert.Equal(1000u, timerNode.ElapsedMs);
        Assert.Equal(50.0, timerNode.ProgressPercent);
        Assert.False(timerNode.IsLiveActive);
        Assert.Contains("1000 / 2000", timerNode.SimulationProgressText);

        // Advance clock by another 1000ms (total 2000ms)
        editorVm.AdvanceSimClock(1000);
        editorVm.RunSimScan(stepMs: 1000, animate: false);

        // TON timed out -> DO0 = 1
        Assert.False(timerNode.IsTiming);
        Assert.Equal(2000u, timerNode.ElapsedMs);
        Assert.Equal(100.0, timerNode.ProgressPercent);
        Assert.True(timerNode.IsLiveActive);
        Assert.Equal(1, do0.Value);

        // Reset DI0 = 0 -> Q turns off
        di0.Value = 0;
        editorVm.AdvanceSimClock(100);
        editorVm.RunSimScan(stepMs: 100, animate: false);

        Assert.False(timerNode.IsLiveActive);
        Assert.False(timerNode.IsTiming);
        Assert.Equal(0u, timerNode.ElapsedMs);
        Assert.Equal(0, do0.Value);
    }

    [Fact]
    public void TimerMacro_CanvasWiredFbd_ResolvesInAndActionQ()
    {
        var di0 = _catalog.AllTags.First(t => t.Name == "DI0");
        var do0 = _catalog.AllTags.First(t => t.Name == "DO0");

        var inpNode = new InputNodeViewModel(di0) { Id = "INP_1" };
        var timerNode = new TimerNodeViewModel("TON", inTag: null, qTag: null, presetMs: 5000)
        {
            Id = "TON_FBD_1",
            CustomLabel = "ConveyorDelay"
        };
        var actNode = new ActionNodeViewModel(do0) { Id = "ACT_1" };

        var connIn = new ConnectionViewModel(inpNode.OutputConnectors[0], timerNode.InputConnectors[0]);
        var connQ = new ConnectionViewModel(timerNode.OutputConnectors[0], actNode.InputConnectors[0]);

        var nodes = new GraphNodeViewModel[] { inpNode, timerNode, actNode };
        var conns = new[] { connIn, connQ };

        var compileResult = _studioCompiler.Compile(nodes, conns, _catalog);

        Assert.True(compileResult.IsValid, $"Compile failed: {string.Join(", ", compileResult.Diagnostics.Select(d => d.Message))}");
        Assert.Equal(2, compileResult.Rules.Count);

        // Verify Rule 0 trigger is DI0
        Assert.Equal("DI0", compileResult.Rules[0].TriggerTag?.Name);
        // Verify Rule 0 and 1 action target is DO0
        Assert.Equal("DO0", compileResult.Rules[0].ActionTag?.Name);
        Assert.Equal("DO0", compileResult.Rules[1].ActionTag?.Name);
    }
}
