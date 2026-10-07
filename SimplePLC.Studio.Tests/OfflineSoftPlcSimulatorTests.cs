using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using System.Linq;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class OfflineSoftPlcSimulatorTests
{
    private static LogicEditorViewModel CreateFixture()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.LoadDefaultDemoGraph();
        logicVM.CompileNow();
        logicVM.SimPropagationDelayMs = 0;
        return logicVM;
    }

    [Fact]
    public void ToggleSimPlayPause_AutoRunsImmediately_WhenNotSimulating()
    {
        var logicVM = CreateFixture();
        Assert.False(logicVM.IsSimulationMode);
        Assert.False(logicVM.IsSimRunning);

        // Act: Click Play / Press F5 (calls ToggleSimPlayPauseCommand)
        logicVM.ToggleSimPlayPauseCommand.Execute(null);

        // Assert: Enters simulation mode AND immediately starts auto-running
        Assert.True(logicVM.IsSimulationMode);
        Assert.True(logicVM.IsSimRunning);
        Assert.True(logicVM.IsVirtualIoBoardOpen);

        // Clean up
        logicVM.StopSimulationCommand.Execute(null);
    }

    [Fact]
    public void VirtualIoBoard_ToggleVirtualDi_TriggersRealTimeEvaluation()
    {
        var logicVM = CreateFixture();
        logicVM.ToggleSimPlayPauseCommand.Execute(null);

        var di0 = logicVM.VirtualDiTags.First(t => t.Name == "DI0");
        Assert.Equal(0, di0.Value);

        // Act: Toggle DI0 from the Virtual I/O Board
        logicVM.ToggleVirtualDiCommand.Execute(di0);

        Assert.Equal(1, di0.Value);

        // Stop simulation
        logicVM.StopSimulationCommand.Execute(null);
    }

    [Fact]
    public void VirtualIoBoard_SetAllVirtualDiZero_ResetsInputs()
    {
        var logicVM = CreateFixture();
        logicVM.ToggleSimPlayPauseCommand.Execute(null);

        var di0 = logicVM.VirtualDiTags.First(t => t.Name == "DI0");
        logicVM.ToggleVirtualDiCommand.Execute(di0);
        Assert.Equal(1, di0.Value);

        // Act: Set all DI to 0
        logicVM.SetAllVirtualDiCommand.Execute(0);

        Assert.All(logicVM.VirtualDiTags, tag => Assert.Equal(0, tag.Value));

        // Stop simulation
        logicVM.StopSimulationCommand.Execute(null);
    }

    [Fact]
    public void CanvasSimulation_TonTimer_ProgressPercentAndElapsedMsUpdateSmoothly()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.Nodes.Clear();
        logicVM.Connections.Clear();

        var di0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inNode = new InputNodeViewModel(di0);
        var timerNode = new TimerNodeViewModel("TON", di0, do0, 1000); // 1000ms preset
        var actNode = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1 };

        logicVM.Nodes.Add(inNode);
        logicVM.Nodes.Add(timerNode);
        logicVM.Nodes.Add(actNode);

        logicVM.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], timerNode.InputConnectors[0]));
        logicVM.Connections.Add(new ConnectionViewModel(timerNode.OutputConnectors[0], actNode.InputConnectors[0]));

        logicVM.CompileNow();
        logicVM.StartSimulation();

        // Turn DI0 = 1
        logicVM.ToggleVirtualDi(di0);

        // Step 1: 200ms -> 20% progress
        logicVM.AdvanceSimClock(200);
        logicVM.RunSimScan(200, animate: false);
        Assert.True(timerNode.IsTiming);
        Assert.True(timerNode.ProgressPercent >= 19.0 && timerNode.ProgressPercent <= 25.0);
        Assert.False(timerNode.IsLiveActive);
        Assert.Equal(0, do0.Value);

        // Step 2: 500ms more (total 700ms) -> 70% progress
        logicVM.AdvanceSimClock(500);
        logicVM.RunSimScan(500, animate: false);
        Assert.True(timerNode.IsTiming);
        Assert.True(timerNode.ProgressPercent >= 69.0 && timerNode.ProgressPercent <= 75.0);
        Assert.False(timerNode.IsLiveActive);
        Assert.Equal(0, do0.Value);

        // Step 3: 400ms more (total 1100ms >= 1000ms) -> Complete, DO0 turns ON
        logicVM.AdvanceSimClock(400);
        logicVM.RunSimScan(400, animate: false);
        Assert.False(timerNode.IsTiming);
        Assert.True(timerNode.IsLiveActive);
        Assert.Equal(100.0, timerNode.ProgressPercent);
        Assert.Equal(1, do0.Value);
        Assert.True(actNode.IsSimTargetOn);

        logicVM.StopSimulationCommand.Execute(null);
    }
}
