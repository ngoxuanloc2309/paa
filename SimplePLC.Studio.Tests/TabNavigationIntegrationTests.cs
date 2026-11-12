using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử tích hợp điều hướng Tab trong SynaptiX IDE (IEC 61508-3 / TDD).
/// Bảo đảm 100% các thao tác chuyển tab từ các ViewModel con (Blueprints, RuleTable, LogicEditor, Deploy, AiChat, MainVM)
/// đều dẫn đến đúng Tab và View tương ứng theo chuẩn hằng số AppTabIndices.
/// </summary>
public class TabNavigationIntegrationTests
{
    [Fact]
    public void Blueprints_ApplyBlueprint_NavigatesToLogicGraphTab()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.SelectTab(AppTabIndices.Blueprints);
        Assert.Equal(AppTabIndices.Blueprints, mainVM.SelectedTabIndex);

        var firstBlueprint = mainVM.BlueprintsVM.Blueprints[0];

        // Act
        mainVM.BlueprintsVM.ApplyBlueprintCommand.Execute(firstBlueprint);

        // Assert: Phải quay về Sơ đồ Logic (Tab 1), KHÔNG được rơi vào Tab 0 (Thiết bị)
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);
    }

    [Fact]
    public void RuleTable_NavigationCommands_NavigateToCorrectTabs()
    {
        // Arrange
        var mainVM = new MainViewModel();

        // 1. EditInLogicGraph -> LogicGraph (1)
        mainVM.SelectTab(AppTabIndices.RuleTable);
        mainVM.RuleTableVM.EditInLogicGraphCommand.Execute(null);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);

        // 2. CreateNewRule -> LogicGraph (1)
        mainVM.SelectTab(AppTabIndices.RuleTable);
        mainVM.RuleTableVM.CreateNewRuleCommand.Execute(null);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);

        // 3. NavigateToBlueprints -> Blueprints (4)
        mainVM.SelectTab(AppTabIndices.RuleTable);
        mainVM.RuleTableVM.NavigateToBlueprintsCommand.Execute(null);
        Assert.Equal(AppTabIndices.Blueprints, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.BlueprintsVM, mainVM.CurrentView);
    }

    [Fact]
    public void LogicEditor_NavigationCommands_NavigateToCorrectTabs()
    {
        // Arrange
        var mainVM = new MainViewModel();

        // 1. ViewRuleTable -> RuleTable (3)
        mainVM.SelectTab(AppTabIndices.LogicGraph);
        mainVM.LogicEditorVM.ViewRuleTableCommand.Execute(null);
        Assert.Equal(AppTabIndices.RuleTable, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.RuleTableVM, mainVM.CurrentView);

        // 2. DeployNow -> Deploy (5)
        mainVM.SelectTab(AppTabIndices.LogicGraph);
        mainVM.LogicEditorVM.DeployNowCommand.Execute(null);
        Assert.Equal(AppTabIndices.Deploy, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.DeployVM, mainVM.CurrentView);

        // 3. LoadRuleToCanvas -> LogicGraph (1)
        mainVM.SelectTab(AppTabIndices.RuleTable);
        var dummyRule = new RuleItemModel { Id = "R01" };
        mainVM.LogicEditorVM.LoadRuleToCanvas(dummyRule);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);
    }

    [Fact]
    public void DeployViewModel_NavigationCommands_NavigateToCorrectTabs()
    {
        // Arrange
        var mainVM = new MainViewModel();

        // 1. NavigateToRules -> RuleTable (3)
        mainVM.SelectTab(AppTabIndices.Deploy);
        mainVM.DeployVM.NavigateToRulesCommand.Execute(null);
        Assert.Equal(AppTabIndices.RuleTable, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.RuleTableVM, mainVM.CurrentView);

        // 2. NavigateToLogicEditor -> LogicGraph (1)
        mainVM.SelectTab(AppTabIndices.Deploy);
        mainVM.DeployVM.NavigateToLogicEditorCommand.Execute(null);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);

        // 3. NavigateToLiveWatch -> LiveWatch (6)
        mainVM.SelectTab(AppTabIndices.Deploy);
        mainVM.DeployVM.NavigateToLiveWatchCommand.Execute(null);
        Assert.Equal(AppTabIndices.LiveWatch, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LiveWatchVM, mainVM.CurrentView);
    }

    [Fact]
    public void AiChatViewModel_ViewRuleTable_NavigatesToRuleTableTab()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.SelectTab(AppTabIndices.LogicGraph);

        // Act
        mainVM.AiChatVM.ViewRuleTableCommand.Execute(null);

        // Assert: Phải chuyển sang Tab 3 (Bảng quy tắc), không phải Tab 2 (Tag Catalog)
        Assert.Equal(AppTabIndices.RuleTable, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.RuleTableVM, mainVM.CurrentView);
    }

    [Fact]
    public void MainViewModel_DemoAndExampleLoaders_NavigateToLogicGraphTab()
    {
        // Arrange
        var mainVM = new MainViewModel();

        // 1. LoadDefaultDemo -> LogicGraph (1)
        mainVM.SelectTab(AppTabIndices.Device);
        mainVM.LoadDefaultDemoCommand.Execute(null);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);

        // 2. LoadExample5FloatAlarm -> LogicGraph (1)
        mainVM.SelectTab(AppTabIndices.Device);
        mainVM.LoadExample5FloatAlarmCommand.Execute(null);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);

        // 3. ToggleSimulation from Device tab -> switches to LogicGraph (1)
        mainVM.SelectTab(AppTabIndices.Device);
        mainVM.ToggleSimulationCommand.Execute(null);
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);
    }
}
