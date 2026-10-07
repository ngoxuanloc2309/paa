using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class BuildLifecycleTests
{
    private (LogicEditorViewModel vm, TagCatalogViewModel tags) CreateFixture()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: action => action());
        vm.LoadDefaultDemoGraph();
        vm.CompileNow();
        return (vm, tagCatalog);
    }

    [Fact]
    public void Startup_DemoGraph_HasValidCompileStateAndCurrentProgram()
    {
        var (vm, _) = CreateFixture();

        Assert.Equal(CompileState.Valid, vm.CompileState);
        Assert.NotNull(vm.CurrentProgram);
        Assert.NotNull(vm.LastSuccessfulProgram);
        Assert.True(vm.ValidRuleCount > 0);
        Assert.Equal(vm.ValidRuleCount, vm.CurrentProgram.RuleCount);
    }

    [Fact]
    public void GraphModified_ByAddingNode_ImmediatelyTransitionsToStaleAndClearsCurrentProgram()
    {
        LocalizationService.Instance.CurrentLanguage = "vi";
        try
        {
            var (vm, _) = CreateFixture();
            Assert.Equal(CompileState.Valid, vm.CompileState);

            // Act: Add a node
            vm.AddNode("Trigger", new Point(500, 500));

            // Assert: Immediately Stale, CurrentProgram cleared, but LastSuccessfulProgram preserved
            Assert.Equal(CompileState.Stale, vm.CompileState);
            Assert.Null(vm.CurrentProgram);
            Assert.NotNull(vm.LastSuccessfulProgram);
            Assert.False(vm.IsSaved);
            Assert.Contains("Cần biên dịch", vm.SaveStatusMessage);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = "en";
        }
    }

    [Fact]
    public void GraphModified_ByAlteringNodeProperty_ImmediatelyTransitionsToStale()
    {
        var (vm, _) = CreateFixture();
        var triggerNode = vm.Nodes.OfType<TriggerNodeViewModel>().First();

        // Act: Alter semantic property
        triggerNode.ForMs = 9999;

        // Assert
        Assert.Equal(CompileState.Stale, vm.CompileState);
        Assert.Null(vm.CurrentProgram);
    }

    [Fact]
    public async Task DebouncedAutoCompile_TransitionsFromStaleToValid()
    {
        var (vm, _) = CreateFixture();
        vm.DebounceDelayMs = 50; // Fast debounce for testing

        // Modify a property to trigger Stale
        var triggerNode = vm.Nodes.OfType<TriggerNodeViewModel>().First();
        triggerNode.ForMs = 2500;

        Assert.Equal(CompileState.Stale, vm.CompileState);
        Assert.Null(vm.CurrentProgram);

        // Wait for debounce delay
        await Task.Delay(100);

        // Assert: Auto-compile should have completed
        Assert.Equal(CompileState.Valid, vm.CompileState);
        Assert.NotNull(vm.CurrentProgram);
        Assert.True(vm.IsSaved);
    }

    [Fact]
    public void CompileNow_BypassesDebounce_CompilesImmediately()
    {
        var (vm, _) = CreateFixture();
        vm.DebounceDelayMs = 5000; // Long debounce

        var triggerNode = vm.Nodes.OfType<TriggerNodeViewModel>().First();
        triggerNode.ForMs = 3000;

        Assert.Equal(CompileState.Stale, vm.CompileState);
        Assert.Null(vm.CurrentProgram);

        // Act: User triggers manual compile (F7)
        vm.CompileNow();

        // Assert: Immediately Valid without waiting 5000ms
        Assert.Equal(CompileState.Valid, vm.CompileState);
        Assert.NotNull(vm.CurrentProgram);
        Assert.Equal(vm.CurrentProgram, vm.LastSuccessfulProgram);
    }

    [Fact]
    public void InvalidGraph_DisconnectedAction_CompilesToInvalid()
    {
        var (vm, _) = CreateFixture();
        vm.Nodes.Clear();
        vm.Connections.Clear();

        // An Action node with no incoming trigger/guard connection violates grammar (ErrActionNoIncoming)
        var act = new ActionNodeViewModel { Location = new Point(100, 100) };
        vm.Nodes.Add(act);

        vm.CompileNow();

        Assert.Equal(CompileState.Invalid, vm.CompileState);
        Assert.Null(vm.CurrentProgram);
        Assert.False(vm.IsSaved);
        Assert.NotEmpty(vm.Diagnostics);
        Assert.Contains(vm.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }
}
