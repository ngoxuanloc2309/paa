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

    [Fact]
    public void TimerNode_TimeUnitConversions_CalculatesAccurateHardwareMs()
    {
        var node = new TimerNodeViewModel("TON", presetMs: 3000);

        // Mặc định 3000ms -> 3 giây
        Assert.Equal("s", node.TimeUnit);
        Assert.Equal("3", node.DurationValueText);
        Assert.Equal(3000u, node.PresetMs);
        Assert.Contains("3,000 ms", node.CalculatedMsSummary);

        // Người dùng nhập 30 phút (min)
        node.TimeUnit = "min";
        node.DurationValueText = "30";
        Assert.Equal(1800000u, node.PresetMs); // 30 * 60 * 1000 = 1,800,000 ms
        Assert.Contains("1,800,000 ms", node.CalculatedMsSummary);
        Assert.Contains("30min", node.CalculatedMsSummary);

        // Người dùng nhập 1.5 giây
        node.TimeUnit = "s";
        node.DurationValueText = "1.5";
        Assert.Equal(1500u, node.PresetMs);
        Assert.Contains("1,500 ms", node.CalculatedMsSummary);

        // Người dùng nhập 2 giờ (h)
        node.TimeUnit = "h";
        node.DurationValueText = "2";
        Assert.Equal(7200000u, node.PresetMs); // 2 * 3600 * 1000 = 7,200,000 ms
        Assert.Contains("7,200,000 ms", node.CalculatedMsSummary);

        // Người dùng nhập 250 ms
        node.TimeUnit = "ms";
        node.DurationValueText = "250";
        Assert.Equal(250u, node.PresetMs);
        Assert.Contains("250 ms", node.CalculatedMsSummary);

        // Gán trực tiếp PresetMs (SyncFromPresetMs)
        node.PresetMs = 60000;
        Assert.Equal("min", node.TimeUnit);
        Assert.Equal("1", node.DurationValueText);
    }

    [Fact]
    public void TimerNode_Persistence_WithCustomUnit_RoundtripSuccessful()
    {
        var di0 = _catalog.AllTags.First(t => t.Name == "DI0");
        var do0 = _catalog.AllTags.First(t => t.Name == "DO0");

        var originalNode = new TimerNodeViewModel("TON", di0, do0);
        originalNode.SetDuration(45, "min"); // 45 phút = 2,700,000 ms
        Assert.Equal(2700000u, originalNode.PresetMs);

        // Export
        var pNode = LogicEditorViewModel.ExportSingleNodeData(originalNode);
        Assert.Equal(2700000u, pNode.PresetMs);
        Assert.Equal("min", pNode.TimeUnit);
        Assert.Equal(45.0, pNode.DurationValue);

        // Serialize & Deserialize
        string json = JsonSerializer.Serialize(pNode);
        var deserializedPNode = JsonSerializer.Deserialize<ProjectNodeData>(json)!;

        // Import
        var editorVm = new LogicEditorViewModel(_catalog);
        var reloadedNode = editorVm.CreateNodeFromData(deserializedPNode) as TimerNodeViewModel;

        Assert.NotNull(reloadedNode);
        Assert.Equal(2700000u, reloadedNode.PresetMs);
        Assert.Equal("min", reloadedNode.TimeUnit);
        Assert.Equal("45", reloadedNode.DurationValueText);
    }

    [Fact]
    public void TimerNode_TimeUnit_SynchronizesWithLocalizationService()
    {
        var originalLang = LocalizationService.Instance.CurrentLanguage;
        try
        {
            // Switch to English
            LocalizationService.Instance.CurrentLanguage = "en";
            var sUnitEn = TimerNodeViewModel.AvailableUnits.First(u => u.Key == "s");
            var minUnitEn = TimerNodeViewModel.AvailableUnits.First(u => u.Key == "min");
            var hUnitEn = TimerNodeViewModel.AvailableUnits.First(u => u.Key == "h");
            var msUnitEn = TimerNodeViewModel.AvailableUnits.First(u => u.Key == "ms");

            Assert.Equal("s (seconds)", sUnitEn.Label);
            Assert.Equal("min (minutes)", minUnitEn.Label);
            Assert.Equal("h (hours)", hUnitEn.Label);
            Assert.Equal("ms (milliseconds)", msUnitEn.Label);

            // Switch to Vietnamese
            bool propertyChangedFired = false;
            sUnitEn.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TimeUnitOption.Label))
                {
                    propertyChangedFired = true;
                }
            };

            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.True(propertyChangedFired);
            Assert.Equal("s (giây)", sUnitEn.Label);
            Assert.Equal("min (phút)", minUnitEn.Label);
            Assert.Equal("h (giờ)", hUnitEn.Label);
            Assert.Equal("ms (mili-giây)", msUnitEn.Label);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }
}

