using System.Text.Json;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class CounterMacroPersistenceAndAttributionTests
{
    private readonly TagCatalogViewModel _catalog = new();
    private readonly RuleCompiler _studioCompiler = new();

    [Fact]
    public void CounterMacro_ProjectPersistence_SaveAndReload_PreservesAllProperties()
    {
        var di2 = _catalog.AllTags.First(t => t.Name == "DI2");
        var di3 = _catalog.AllTags.First(t => t.Name == "DI3");
        var vregR0 = _catalog.AllTags.First(t => t.Name == "VREG_RETAIN0");
        var do2 = _catalog.AllTags.First(t => t.Name == "DO2");

        var originalNode = new CounterNodeViewModel("CTU", di2, vregR0, do2, di3, presetValue: 12)
        {
            Id = "CTU_TEST_99",
            CustomLabel = "BatchPacker",
            Location = new System.Windows.Point(150, 300)
        };

        // 1. Export
        var pNode = LogicEditorViewModel.ExportSingleNodeData(originalNode);
        Assert.Equal("Counter", pNode.Type);
        Assert.Equal("CTU", pNode.CounterMode);
        Assert.Equal(12, pNode.PresetValue);
        Assert.Equal("DI2", pNode.TagName);
        Assert.Equal("VREG_RETAIN0", pNode.CvTagName);
        Assert.Equal("DO2", pNode.OutputTagName);
        Assert.Equal("DI3", pNode.ResetTagName);
        Assert.Equal("BatchPacker", pNode.CustomLabel);
        Assert.Equal(150, pNode.LocationX);
        Assert.Equal(300, pNode.LocationY);

        // 2. JSON Roundtrip
        string json = JsonSerializer.Serialize(pNode);
        var deserializedPNode = JsonSerializer.Deserialize<ProjectNodeData>(json)!;

        // 3. Import
        var editorVm = new LogicEditorViewModel(_catalog);
        var reloadedNode = editorVm.CreateNodeFromData(deserializedPNode) as CounterNodeViewModel;

        Assert.NotNull(reloadedNode);
        Assert.Equal("CTU", reloadedNode.CounterMode);
        Assert.Equal(12, reloadedNode.PresetValue);
        Assert.Equal("DI2", reloadedNode.InputTag?.Name);
        Assert.Equal("VREG_RETAIN0", reloadedNode.CvTag?.Name);
        Assert.Equal("DO2", reloadedNode.OutputTag?.Name);
        Assert.Equal("DI3", reloadedNode.ResetTag?.Name);
        Assert.Equal("BatchPacker", reloadedNode.CustomLabel);
    }

    [Fact]
    public void CounterMacro_RuleCompiler_AssignsCorrectMacroAttributionAndBadgeColors()
    {
        var di2 = _catalog.AllTags.First(t => t.Name == "DI2");
        var di3 = _catalog.AllTags.First(t => t.Name == "DI3");
        var vregR0 = _catalog.AllTags.First(t => t.Name == "VREG_RETAIN0");
        var do2 = _catalog.AllTags.First(t => t.Name == "DO2");

        var counterNode = new CounterNodeViewModel("CTU", di2, vregR0, do2, di3, presetValue: 10)
        {
            Id = "CTU_BATCH",
            CustomLabel = "BoxFiller"
        };

        var compileResult = _studioCompiler.Compile(
            new[] { counterNode },
            Array.Empty<ConnectionViewModel>(),
            _catalog);

        Assert.True(compileResult.IsValid);
        Assert.Equal(4, compileResult.Rules.Count);

        for (int i = 0; i < 4; i++)
        {
            var rule = compileResult.Rules[i];
            Assert.Equal("CTU_BATCH", rule.DiagramId);
            Assert.Contains("CTU", rule.MacroAttribution);
            Assert.Contains("BoxFiller", rule.MacroAttribution);
            Assert.Contains($"{i + 1}/4", rule.MacroAttribution);

            // Dynamic deep teal badge colors
            Assert.Equal("#F0FDFA", rule.MacroBadgeBackground);
            Assert.Equal("#0F766E", rule.MacroBadgeBorderBrush);
            Assert.Equal("#0F766E", rule.MacroBadgeForeground);
        }
    }

    [Fact]
    public void CounterMacro_Ctd_NoReset_AssignsThreeRules()
    {
        var di1 = _catalog.AllTags.First(t => t.Name == "DI1");
        var vregR1 = _catalog.AllTags.First(t => t.Name == "VREG_RETAIN1");
        var do5 = _catalog.AllTags.First(t => t.Name == "DO5");

        var counterNode = new CounterNodeViewModel("CTD", di1, vregR1, do5, resetTag: null, presetValue: 5)
        {
            Id = "CTD_FEEDER",
            CustomLabel = "FeederCount"
        };

        var compileResult = _studioCompiler.Compile(
            new[] { counterNode },
            Array.Empty<ConnectionViewModel>(),
            _catalog);

        Assert.True(compileResult.IsValid);
        Assert.Equal(3, compileResult.Rules.Count);

        for (int i = 0; i < 3; i++)
        {
            var rule = compileResult.Rules[i];
            Assert.Contains("CTD", rule.MacroAttribution);
            Assert.Contains($"{i + 1}/3", rule.MacroAttribution);
            Assert.Equal("#F0FDFA", rule.MacroBadgeBackground);
        }
    }

    [Fact]
    public void CounterMacro_CanvasWiredFbd_ResolvesCuResetAndActionQ()
    {
        var di2 = _catalog.AllTags.First(t => t.Name == "DI2");
        var di3 = _catalog.AllTags.First(t => t.Name == "DI3");
        var do2 = _catalog.AllTags.First(t => t.Name == "DO2");
        var vregR0 = _catalog.AllTags.First(t => t.Name == "VREG_RETAIN0");

        var inpCount = new InputNodeViewModel(di2) { Id = "INP_1" };
        var inpReset = new InputNodeViewModel(di3) { Id = "INP_2" };
        var counter = new CounterNodeViewModel("CTU", inTag: null, cvTag: vregR0, qTag: null, resetTag: null, presetValue: 15)
        {
            Id = "CTU_FBD_1",
            CustomLabel = "LineCounter"
        };
        var actOut = new ActionNodeViewModel(do2) { Id = "ACT_1" };

        var connCount = new ConnectionViewModel(inpCount.OutputConnectors[0], counter.InputConnectors[0]);
        var connReset = new ConnectionViewModel(inpReset.OutputConnectors[0], counter.InputConnectors[1]);
        var connQ = new ConnectionViewModel(counter.OutputConnectors[0], actOut.InputConnectors[0]);

        var nodes = new GraphNodeViewModel[] { inpCount, inpReset, counter, actOut };
        var conns = new[] { connCount, connReset, connQ };

        var compileResult = _studioCompiler.Compile(nodes, conns, _catalog);

        Assert.True(compileResult.IsValid, $"Compile failed: {string.Join(", ", compileResult.Diagnostics.Select(d => d.Message))}");
        Assert.Equal(4, compileResult.Rules.Count);

        // Verify Rule 0 trigger is DI2
        Assert.Equal("DI2", compileResult.Rules[0].TriggerTag?.Name);
        // Verify Rule 1 trigger is DI3 (Reset)
        Assert.Equal("DI3", compileResult.Rules[1].TriggerTag?.Name);
        // Verify Rule 2 and 3 act on DO2
        Assert.Equal("DO2", compileResult.Rules[2].ActionTag?.Name);
        Assert.Equal("DO2", compileResult.Rules[3].ActionTag?.Name);
    }
}
