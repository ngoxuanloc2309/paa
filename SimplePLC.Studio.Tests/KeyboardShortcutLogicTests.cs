using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit and Integration tests for Keyboard Shortcut Commands and Hotkey Routing Logic (IEC 61508-3 / TDD).
/// Validates that all shortcut commands (Ctrl+1..7, Ctrl+S, Ctrl+Alt+G, F4..F11, Alt alignments) execute faithfully.
/// </summary>
public class KeyboardShortcutLogicTests
{
    [Theory]
    [InlineData("0", AppTabIndices.Device)]
    [InlineData("1", AppTabIndices.LogicGraph)]
    [InlineData("2", AppTabIndices.TagCatalog)]
    [InlineData("3", AppTabIndices.RuleTable)]
    [InlineData("4", AppTabIndices.Blueprints)]
    [InlineData("5", AppTabIndices.Deploy)]
    [InlineData("6", AppTabIndices.LiveWatch)]
    [InlineData(0, AppTabIndices.Device)]
    [InlineData(1, AppTabIndices.LogicGraph)]
    public void SelectTabCommand_WithVariousParameters_NavigatesToExpectedTab(object param, int expectedIndex)
    {
        // Arrange
        var mainVM = new MainViewModel();

        // Act
        mainVM.SelectTabCommand.Execute(param);

        // Assert
        Assert.Equal(expectedIndex, mainVM.SelectedTabIndex);
        switch (expectedIndex)
        {
            case AppTabIndices.Device:
                Assert.Same(mainVM.DtuGatewayStudioVM, mainVM.CurrentView);
                break;
            case AppTabIndices.LogicGraph:
                Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);
                break;
            case AppTabIndices.TagCatalog:
                Assert.Same(mainVM.TagCatalogVM, mainVM.CurrentView);
                break;
            case AppTabIndices.RuleTable:
                Assert.Same(mainVM.RuleTableVM, mainVM.CurrentView);
                break;
            case AppTabIndices.Blueprints:
                Assert.Same(mainVM.BlueprintsVM, mainVM.CurrentView);
                break;
            case AppTabIndices.Deploy:
                Assert.Same(mainVM.DeployVM, mainVM.CurrentView);
                break;
            case AppTabIndices.LiveWatch:
                Assert.Same(mainVM.LiveWatchVM, mainVM.CurrentView);
                break;
        }
    }

    [Fact]
    public void LogicEditorVM_SaveAndCompile_TriggersRequestSaveProject_WhenWired()
    {
        // Arrange
        var mainVM = new MainViewModel();
        bool saveProjectInvoked = false;
        mainVM.LogicEditorVM.RequestSaveProject = () =>
        {
            saveProjectInvoked = true;
        };

        // Act (Simulate Ctrl+S on Logic Editor canvas)
        mainVM.LogicEditorVM.SaveAndCompileCommand.Execute(null);

        // Assert
        Assert.True(saveProjectInvoked, "Ctrl+S from Logic Editor Canvas must invoke RequestSaveProject to write to disk.");
    }

    [Fact]
    public void LogicEditorVM_SaveAndCompile_SafelyCompiles_WhenRequestSaveProjectIsNull()
    {
        // Arrange
        var logicEditorVM = new LogicEditorViewModel(new TagCatalogViewModel());
        logicEditorVM.RequestSaveProject = null;

        // Act
        logicEditorVM.SaveAndCompile();

        // Assert
        Assert.NotNull(logicEditorVM.SaveStatusMessage);
    }

    [Fact]
    public void ToggleGridCommand_TogglesIsGridVisible()
    {
        // Arrange
        var mainVM = new MainViewModel();
        bool initialVisible = mainVM.LogicEditorVM.IsGridVisible;

        // Act 1 (Simulate Ctrl+Alt+G)
        mainVM.ToggleGridCommand.Execute(null);

        // Assert 1
        Assert.Equal(!initialVisible, mainVM.LogicEditorVM.IsGridVisible);

        // Act 2
        mainVM.ToggleGridCommand.Execute(null);

        // Assert 2
        Assert.Equal(initialVisible, mainVM.LogicEditorVM.IsGridVisible);
    }

    [Fact]
    public void ToggleSimulationCommand_NavigatesToLogicGraph_WhenCalledFromOtherTab()
    {
        // Arrange
        var mainVM = new MainViewModel();
        mainVM.SelectTab(AppTabIndices.TagCatalog);
        Assert.Equal(AppTabIndices.TagCatalog, mainVM.SelectedTabIndex);

        // Act (Simulate F5)
        mainVM.ToggleSimulationCommand.Execute(null);

        // Assert
        Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        Assert.Same(mainVM.LogicEditorVM, mainVM.CurrentView);
    }

    [Fact]
    public void ToggleAiCopilotCommand_TogglesAiChatPanelOpen()
    {
        // Arrange
        var mainVM = new MainViewModel();
        bool initialOpen = mainVM.AiChatVM.IsPanelOpen;

        // Act 1 (Simulate Ctrl+Space)
        mainVM.ToggleAiCopilotCommand.Execute(null);

        // Assert 1
        Assert.Equal(!initialOpen, mainVM.AiChatVM.IsPanelOpen);

        // Act 2
        mainVM.ToggleAiCopilotCommand.Execute(null);

        // Assert 2
        Assert.Equal(initialOpen, mainVM.AiChatVM.IsPanelOpen);
    }

    [Fact]
    public void CanvasAlignmentCommands_ExecuteThroughMainVM_WithoutExceptions()
    {
        // Arrange
        var mainVM = new MainViewModel();
        var node1 = new ScaleNodeViewModel { Location = new Point(10.5, 20.3), IsSelected = true };
        var node2 = new ScaleNodeViewModel { Location = new Point(50.2, 80.7), IsSelected = true };
        mainVM.LogicEditorVM.Nodes.Add(node1);
        mainVM.LogicEditorVM.Nodes.Add(node2);
        mainVM.LogicEditorVM.SelectedNodes.Add(node1);
        mainVM.LogicEditorVM.SelectedNodes.Add(node2);

        // Act & Assert (Alt+Up, Alt+Down, Alt+Left, Alt+Right, Alt+C, Alt+M, Alt+Shift+H, Alt+Shift+V, Alt+G)
        mainVM.AlignTopCommand.Execute(null);
        mainVM.AlignBottomCommand.Execute(null);
        mainVM.AlignLeftCommand.Execute(null);
        mainVM.AlignRightCommand.Execute(null);
        mainVM.AlignCenterCommand.Execute(null);
        mainVM.AlignMiddleCommand.Execute(null);
        mainVM.DistributeNodesHorizontallyCommand.Execute(null);
        mainVM.DistributeNodesVerticallyCommand.Execute(null);
        mainVM.SnapAllNodesToGridCommand.Execute(null);

        // Assert snapping: coordinates should be multiples of 10
        Assert.True(node1.Location.X % 10 == 0);
        Assert.True(node1.Location.Y % 10 == 0);
    }
}
