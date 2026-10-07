using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class SimulatorGateTests
{
    private (SimulatorViewModel simVM, LogicEditorViewModel logicVM) CreateFixture()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.LoadDefaultDemoGraph();
        logicVM.CompileNow();
        var simVM = new SimulatorViewModel(tagCatalog, ruleTableVM, logicVM);

        return (simVM, logicVM);
    }

    [Fact]
    public void Simulator_WhenLogicGraphModifiedToStale_ImmediatelyPauses()
    {
        var (simVM, logicVM) = CreateFixture();

        Assert.True(simVM.IsRunning);
        Assert.Equal(CompileState.Valid, logicVM.CompileState);

        // Act: Edit node on canvas
        var trig = logicVM.Nodes.OfType<TriggerNodeViewModel>().First();
        trig.ForMs = 8888;

        // Assert: Simulator must auto-pause and report gate warning
        Assert.Equal(CompileState.Stale, logicVM.CompileState);
        Assert.False(simVM.IsRunning);
        Assert.Contains("Tạm dừng mô phỏng", simVM.GateWarning);
    }

    [Fact]
    public void Simulator_CannotBeResumed_WhileLogicIsStaleOrInvalid()
    {
        var (simVM, logicVM) = CreateFixture();

        // Cause Stale
        var trig = logicVM.Nodes.OfType<TriggerNodeViewModel>().First();
        trig.ForMs = 7777;

        Assert.False(simVM.IsRunning);

        // Act: User tries to start/resume simulation
        simVM.ToggleEngine();

        // Assert: Must remain stopped
        Assert.False(simVM.IsRunning);
        Assert.Contains("Không thể chạy mô phỏng", simVM.GateWarning);
    }

    [Fact]
    public void Simulator_CanResume_OnceCompiledProgramIsValid()
    {
        var (simVM, logicVM) = CreateFixture();

        // Cause Stale
        var trig = logicVM.Nodes.OfType<TriggerNodeViewModel>().First();
        trig.ForMs = 5000;
        Assert.False(simVM.IsRunning);

        // Compile logic
        logicVM.CompileNow();
        Assert.Equal(CompileState.Valid, logicVM.CompileState);

        // Act: User resumes simulation
        simVM.ToggleEngine();

        // Assert: Resumed successfully
        Assert.True(simVM.IsRunning);
    }
}
