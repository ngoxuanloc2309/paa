using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Tests;

public class RuleCompilerTests
{
    [Fact]
    public void IncompleteAction_IsDiagnosticAndDoesNotUseFallbackTags()
    {
        var catalog = new TagCatalogViewModel();
        var nodes = new GraphNodeViewModel[] { new ActionNodeViewModel() };
        var result = new RuleCompiler().Compile(nodes, Array.Empty<ConnectionViewModel>(), catalog);

        Assert.Empty(result.Rules);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void CompleteInputTriggerAction_ProducesOneRule()
    {
        var catalog = new TagCatalogViewModel();
        var input = new InputNodeViewModel(catalog.AllTags.First(t => t.Name == "DI1"));
        var trigger = new TriggerNodeViewModel();
        var action = new ActionNodeViewModel(catalog.AllTags.First(t => t.Name == "DO1"));
        var connections = new[]
        {
            new ConnectionViewModel(input.OutputConnectors[0], trigger.InputConnectors[0]),
            new ConnectionViewModel(trigger.OutputConnectors[0], action.InputConnectors[0])
        };

        var result = new RuleCompiler().Compile(new GraphNodeViewModel[] { input, trigger, action }, connections, catalog);

        Assert.True(result.IsValid);
        Assert.Single(result.Rules);
        Assert.Equal(1, result.Rules[0].TriggerTag?.Index);
        Assert.Equal(9, result.Rules[0].ActionTag?.Index);
    }

    [Fact]
    public void NoActionNodes_ProducesLocalizedDiagnostic_InVietnameseAndEnglish()
    {
        var catalog = new TagCatalogViewModel();
        var guard = new GuardNodeViewModel();
        var nodes = new GraphNodeViewModel[] { guard };
        var compiler = new RuleCompiler();

        try
        {
            // Vietnamese
            LocalizationService.Instance.CurrentLanguage = "vi";
            var resultVi = compiler.Compile(nodes, Array.Empty<ConnectionViewModel>(), catalog);
            Assert.Contains(resultVi.Diagnostics, d => d.Message.Contains("Sơ đồ logic chưa có khối Hành động"));

            // English
            LocalizationService.Instance.CurrentLanguage = "en";
            var resultEn = compiler.Compile(nodes, Array.Empty<ConnectionViewModel>(), catalog);
            Assert.Contains(resultEn.Diagnostics, d => d.Message.Contains("Graph does not contain any Action"));
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = "en";
        }
    }

    [Fact]
    public void GuardNode_NarrativePreview_IsProperlyLocalized()
    {
        var tag = new TagModel { Name = "VFLAG2", Kind = TagKind.VirtualFlag, Index = 34 };
        var guard = new GuardNodeViewModel(tag);
        var vm = new LogicEditorViewModel(new TagCatalogViewModel());
        vm.SelectedNode = guard;

        try
        {
            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.Contains("Chốt chặn điều kiện (Khóa liên động)", vm.NarrativePreview);
            Assert.Contains("BẬT (1)", vm.NarrativePreview);
            Assert.DoesNotContain("Boolean Guard", vm.NarrativePreview);

            LocalizationService.Instance.CurrentLanguage = "en";
            Assert.Contains("Interlock Guard (Boolean)", vm.NarrativePreview);
            Assert.Contains("ON (1)", vm.NarrativePreview);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = "en";
        }
    }

    [Fact]
    public void UserDiagram_ThreeChains_VFlagInterlock_And_SetReset_CompilesValidInStudio()
    {
        var catalog = new TagCatalogViewModel();
        var di1 = catalog.AllTags.First(t => t.Name == "DI1");
        var vflag0 = catalog.AllTags.First(t => t.Name == "VFLAG0");
        var do0 = catalog.AllTags.First(t => t.Name == "DO0");

        // Chain 1: DI1 (50ms) -> VFLAG0 = 1
        var in1 = new InputNodeViewModel(di1);
        var tr1 = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE, ForMs = 50 };
        var act1 = new ActionNodeViewModel(vflag0) { ActionType = ActionType.SET_TAG, ActionParam = 1 };

        // Chain 2: VFLAG0 -> DO0 = 1
        var in2 = new InputNodeViewModel(vflag0);
        var tr2 = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE };
        var act2 = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1 };

        // Chain 3: DI1 -> Guard NOT VFLAG0 -> DO0 = 0
        var in3 = new InputNodeViewModel(di1);
        var tr3 = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE };
        var gd3 = new GuardNodeViewModel(vflag0) { Negate = true };
        var act3 = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 0 };

        var nodes = new GraphNodeViewModel[] { in1, tr1, act1, in2, tr2, act2, in3, tr3, gd3, act3 };
        var connections = new[]
        {
            new ConnectionViewModel(in1.OutputConnectors[0], tr1.InputConnectors[0]),
            new ConnectionViewModel(tr1.OutputConnectors[0], act1.InputConnectors[0]),

            new ConnectionViewModel(in2.OutputConnectors[0], tr2.InputConnectors[0]),
            new ConnectionViewModel(tr2.OutputConnectors[0], act2.InputConnectors[0]),

            new ConnectionViewModel(in3.OutputConnectors[0], tr3.InputConnectors[0]),
            new ConnectionViewModel(tr3.OutputConnectors[0], gd3.InputConnectors[0]),
            new ConnectionViewModel(gd3.OutputConnectors[0], act3.InputConnectors[0]),
        };

        var result = new RuleCompiler().Compile(nodes, connections, catalog);

        Assert.True(result.IsValid, $"Expected Valid, got: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.Equal(3, result.Rules.Count);
    }
}
