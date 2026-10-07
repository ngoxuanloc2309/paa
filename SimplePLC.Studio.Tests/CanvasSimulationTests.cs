using SimplePLC.Application.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class CanvasSimulationTests
{
    private LogicEditorViewModel CreateFixture()
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
    public void RuleActionNodeId_IsPopulatedDuringCompilation()
    {
        var logicVM = CreateFixture();
        Assert.NotNull(logicVM.CurrentProgram);
        Assert.NotEmpty(logicVM.CurrentProgram.Rules);

        var actionNodeIds = logicVM.Nodes.OfType<ActionNodeViewModel>().Select(a => a.Id).ToHashSet();
        foreach (var rule in logicVM.CurrentProgram.Rules)
        {
            Assert.False(string.IsNullOrEmpty(rule.ActionNodeId));
            Assert.Contains(rule.ActionNodeId, actionNodeIds);
        }
    }

    [Fact]
    public void CanvasSimulation_StartAndStop_ManagesModeAndVisuals()
    {
        var logicVM = CreateFixture();

        Assert.False(logicVM.IsSimulationMode);
        Assert.False(logicVM.IsSimRunning);

        // Act: Start simulation (manual-first: starts in paused/ready state)
        logicVM.StartSimulationCommand.Execute(null);

        Assert.True(logicVM.IsSimulationMode);
        Assert.False(logicVM.IsSimRunning); // Manual-first default is paused
        Assert.Empty(logicVM.SimGateWarning);

        // Can toggle to running
        logicVM.ToggleSimPlayPauseCommand.Execute(null);
        Assert.True(logicVM.IsSimRunning);

        // Act: Stop simulation
        logicVM.StopSimulationCommand.Execute(null);

        Assert.False(logicVM.IsSimulationMode);
        Assert.False(logicVM.IsSimRunning);
        Assert.All(logicVM.Connections, c => Assert.False(c.IsActive));
        Assert.All(logicVM.Nodes, n => Assert.False(n.IsSimActive));
    }

    [Fact]
    public void CanvasSimulation_StepScan_AdvancesScanCountAndEvaluates()
    {
        var logicVM = CreateFixture();

        // Start in step mode
        logicVM.StepSimScanCommand.Execute(null);

        Assert.True(logicVM.IsSimulationMode);
        Assert.False(logicVM.IsSimRunning); // Step mode should be paused
        Assert.Equal(1, logicVM.SimScanNumber);

        // Step again
        logicVM.StepSimScanCommand.Execute(null);
        Assert.Equal(2, logicVM.SimScanNumber);
    }

    [Fact]
    public void CanvasSimulation_InputForcing_TogglesDigitalTagOnCanvas()
    {
        var logicVM = CreateFixture();
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Kind == TagKind.DiscreteInput);

        int initialVal = inputNode.Tag!.Value;

        // Act: Click input node on canvas to toggle
        inputNode.ToggleSimInputCommand.Execute(null);

        Assert.NotEqual(initialVal, inputNode.Tag.Value);
        Assert.Equal(inputNode.Tag.Value != 0, inputNode.IsSimActive);

        // Toggle back
        inputNode.ToggleSimInputCommand.Execute(null);
        Assert.Equal(initialVal, inputNode.Tag.Value);
    }

    [Fact]
    public void CanvasSimulation_PowerFlow_HighlightsWiresAndNodes_WhenTriggerFires()
    {
        var logicVM = CreateFixture();

        // Rule 2 in Default Demo: AI0 (Analog) -> Trigger (ON_CHANGE > 85, ForMs = 0) -> Action (SET DO1 = 1)
        var inputNode2 = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "AI0");
        var trigNode2 = logicVM.Nodes.OfType<TriggerNodeViewModel>().First(n => n.CompareOp == CompareOp.GT);
        var actNode2 = logicVM.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO1");

        var inToTrigConn = logicVM.Connections.First(c => c.Source?.Node == inputNode2 && c.Target?.Node == trigNode2);
        var trigToActConn = logicVM.Connections.First(c => c.Source?.Node == trigNode2 && c.Target?.Node == actNode2);

        logicVM.StartSimulationCommand.Execute(null);
        logicVM.IsSimRunning = false; // Step manually

        // Step 1: Initially AI0 = 20 (<= 85)
        inputNode2.Tag!.Value = 20;
        logicVM.RunSimScan();

        Assert.False(trigNode2.IsSimTriggered);
        Assert.False(trigToActConn.IsActive);
        Assert.False(actNode2.IsSimFired);

        // Step 2: AI0 rises to 90 (> 85)
        inputNode2.Tag.Value = 90;
        logicVM.RunSimScan();

        // Power flow must be active
        Assert.True(inToTrigConn.IsActive, "Input wire should be active when AI0 has non-zero value");
        Assert.True(trigNode2.IsSimTriggered, "Trigger node should be fired when AI0 > 85");
        Assert.True(trigToActConn.IsActive, "Outgoing wire from Trigger to Action should be active");
        Assert.True(actNode2.IsSimFired, "Action node should fire");
    }

    [Fact]
    public void CanvasSimulation_GuardBlock_PreventsPowerFlowToDownstreamAction()
    {
        var logicVM = CreateFixture();

        // Rule 1 in Default Demo: DI0 -> Trigger (ON_FALL, set ForMs = 0 for instant test) -> Guard (VFLAG0 == 1) -> Action DO0
        var trigNode = logicVM.Nodes.OfType<TriggerNodeViewModel>().First(n => n.TriggerType == TriggerType.ON_FALL);
        trigNode.ForMs = 0; // Test immediate fall
        logicVM.CompileNow(); // Recompile to apply ForMs = 0

        var guardNode = logicVM.Nodes.OfType<GuardNodeViewModel>().First();
        var guardConn = logicVM.Connections.First(c => c.Source?.Node == guardNode);
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "DI0");
        var actNode = logicVM.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO0");

        logicVM.StartSimulationCommand.Execute(null);
        logicVM.IsSimRunning = false; // Step manually

        // Step 1: Set DI0 = 1, VFLAG0 = 0 (Guard condition not met: VFLAG0 is false)
        inputNode.Tag!.Value = 1;
        guardNode.GuardTag!.Value = 0;
        logicVM.RunSimScan();

        // Step 2: Falling edge on DI0: 1 -> 0
        inputNode.Tag.Value = 0;
        logicVM.RunSimScan();

        // Guard must be blocked, and wire from Guard to Action must NOT be active
        Assert.True(guardNode.IsSimBlocked, "Guard node should show BLOCKED when VFLAG0 == 0");
        Assert.False(guardNode.IsSimPassed, "Guard node should NOT show PASS");
        Assert.False(guardConn.IsActive, "Wire downstream of blocked Guard must be inactive");
        Assert.False(actNode.IsSimFired, "Action must not fire when guard is blocked");
    }

    [Fact]
    public void CanvasSimulation_SafetyGate_PausesWhenGraphModified()
    {
        var logicVM = CreateFixture();
        logicVM.StartSimulationCommand.Execute(null);
        logicVM.ToggleSimPlayPauseCommand.Execute(null); // start running to test pause on edit

        Assert.True(logicVM.IsSimulationMode);
        Assert.True(logicVM.IsSimRunning);

        // Act: Modify graph
        var trig = logicVM.Nodes.OfType<TriggerNodeViewModel>().First();
        trig.ForMs = 9999;

        // Assert: Safety gate triggers immediately
        Assert.False(logicVM.IsSimRunning);
        Assert.False(string.IsNullOrEmpty(logicVM.SimGateWarning));
        Assert.All(logicVM.Connections, c => Assert.False(c.IsActive));
    }

    [Fact]
    public void CanvasSimulation_Diagnostics_ShowsTrueFalseAndReason()
    {
        var logicVM = CreateFixture();
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "AI0");
        var trigNode = logicVM.Nodes.OfType<TriggerNodeViewModel>().First(n => n.CompareOp == CompareOp.GT);
        var actNode = logicVM.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO1");

        logicVM.StartSimulationCommand.Execute(null);
        logicVM.IsSimRunning = false;

        // When AI0 = 20 <= 85
        inputNode.Tag!.Value = 20;
        logicVM.RunSimScan();

        Assert.True(trigNode.IsSimEvaluated);
        Assert.False(trigNode.IsSimTriggered);
        Assert.Contains("20", trigNode.SimDiagText);
        Assert.Contains("✗", trigNode.SimDiagText);
        Assert.False(actNode.IsSimFired);

        // When AI0 rises to 90 > 85
        inputNode.Tag.Value = 90;
        logicVM.RunSimScan();

        Assert.True(trigNode.IsSimTriggered);
        Assert.True(trigNode.SimDiagText == "ĐẠT" || trigNode.SimDiagText == "TRUE");
        Assert.True(actNode.IsSimFired);
        Assert.True(actNode.IsSimTargetOn);
    }

    [Fact]
    public void CanvasSimulation_InputToggleWhileSelected_DoesNotExitSimulationOrStaleGraph()
    {
        var logicVM = CreateFixture();
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "DI0");
        
        // Select the input node as in the inspector panel
        logicVM.SelectedNode = inputNode;
        logicVM.StartSimulationCommand.Execute(null);
        logicVM.ToggleSimPlayPauseCommand.Execute(null); // Auto run active

        Assert.True(logicVM.IsSimulationMode);
        Assert.True(logicVM.IsSimRunning);
        Assert.Equal(CompileState.Valid, logicVM.CompileState);
        Assert.NotNull(logicVM.CurrentProgram);
        Assert.Empty(logicVM.SimGateWarning);

        // Act: User toggles input to create a pulse (e.g. DI0: 0 -> 1)
        logicVM.ToggleInputNode(inputNode);

        // Assert: Graph remains valid, simulation remains running, no safety gate warning
        Assert.True(logicVM.IsSimulationMode);
        Assert.True(logicVM.IsSimRunning);
        Assert.Equal(CompileState.Valid, logicVM.CompileState);
        Assert.NotNull(logicVM.CurrentProgram);
        Assert.Empty(logicVM.SimGateWarning);

        // Act: User toggles input again (DI0: 1 -> 0)
        logicVM.ToggleInputNode(inputNode);

        Assert.True(logicVM.IsSimulationMode);
        Assert.True(logicVM.IsSimRunning);
        Assert.Equal(CompileState.Valid, logicVM.CompileState);
        Assert.NotNull(logicVM.CurrentProgram);
        Assert.Empty(logicVM.SimGateWarning);
    }

    [Fact]
    public void CanvasSimulation_AutoRun_ProcessesPulseWhenInputToggled()
    {
        var logicVM = CreateFixture();
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "DI0");
        var trigNode = logicVM.Nodes.OfType<TriggerNodeViewModel>().First(n => n.TriggerType == TriggerType.ON_FALL);
        var guardNode = logicVM.Nodes.OfType<GuardNodeViewModel>().First();
        var actNode = logicVM.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO0");

        // Allow guard to pass and set ForMs = 0 for immediate edge test without dwell
        trigNode.ForMs = 0;
        guardNode.GuardTag!.Value = 1;

        logicVM.SelectedNode = inputNode;
        logicVM.StartSimulationCommand.Execute(null);

        // Initial scan at DI0 = 1
        inputNode.Tag!.Value = 1;
        logicVM.StepSimScan();
        Assert.False(trigNode.IsSimTriggered);
        Assert.False(actNode.IsSimFired);

        // Toggle to 0 to create falling pulse (1 -> 0)
        logicVM.ToggleInputNode(inputNode);
        Assert.Equal(0, inputNode.Tag.Value);
        Assert.Equal(CompileState.Valid, logicVM.CompileState);

        // Next scan executes falling pulse
        logicVM.StepSimScan();
        Assert.True(trigNode.IsSimTriggered);
        Assert.True(actNode.IsSimFired);
        Assert.Empty(logicVM.SimGateWarning);
    }

    [Fact]
    public async Task CanvasSimulation_SequentialPropagation_ActivatesStagesInOrder()
    {
        var logicVM = CreateFixture();
        // Use AI0 (> 85) -> DO1 rule for clear analog trigger test
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "AI0");
        var trigNode = logicVM.Nodes.OfType<TriggerNodeViewModel>().First(n => n.CompareOp == CompareOp.GT);
        var actNode = logicVM.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO1");
        var inToTrigConn = logicVM.Connections.First(c => c.Source?.Node == inputNode && c.Target?.Node == trigNode);
        var trigToActConn = logicVM.Connections.First(c => c.Source?.Node == trigNode && c.Target?.Node == actNode);

        logicVM.SimPropagationDelayMs = 60; // 60ms between stages
        logicVM.StartSimulationCommand.Execute(null);

        // AI0 = 90 (> 85)
        inputNode.Tag!.Value = 90;

        // Act: Run scan with animation enabled
        logicVM.RunSimScan(100, animate: true);

        // Stage 1 (immediate): Input is active, wire 1 is active, but Trigger has not yet evaluated
        Assert.True(inputNode.IsSimActive);
        Assert.True(inToTrigConn.IsActive);
        Assert.False(trigNode.IsSimTriggered, "Trigger node must not be active in stage 1");
        Assert.False(trigToActConn.IsActive, "Wire 2 must not be active in stage 1");
        Assert.False(actNode.IsSimFired, "Action node must not be active in stage 1");

        // Wait for animation to finish all stages
        Assert.NotNull(logicVM.LastAnimationTask);
        await logicVM.LastAnimationTask;

        // After full propagation: Trigger and Action are active, wire 2 is active
        Assert.True(inputNode.IsSimActive);
        Assert.True(inToTrigConn.IsActive);
        Assert.True(trigNode.IsSimTriggered);
        Assert.True(trigToActConn.IsActive);
        Assert.True(actNode.IsSimFired);
    }

    [Fact]
    public async Task CanvasSimulation_SequentialPropagation_StopsWhenTriggerFails()
    {
        var logicVM = CreateFixture();
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "AI0");
        var trigNode = logicVM.Nodes.OfType<TriggerNodeViewModel>().First(n => n.CompareOp == CompareOp.GT);
        var actNode = logicVM.Nodes.OfType<ActionNodeViewModel>().First(n => n.TargetTag?.Name == "DO1");
        var inToTrigConn = logicVM.Connections.First(c => c.Source?.Node == inputNode && c.Target?.Node == trigNode);
        var trigToActConn = logicVM.Connections.First(c => c.Source?.Node == trigNode && c.Target?.Node == actNode);

        logicVM.SimPropagationDelayMs = 40;
        logicVM.StartSimulationCommand.Execute(null);

        // AI0 = 50 (<= 85 -> condition NOT met)
        inputNode.Tag!.Value = 50;

        logicVM.RunSimScan(100, animate: true);
        Assert.NotNull(logicVM.LastAnimationTask);
        await logicVM.LastAnimationTask;

        // Input wire active because AI0 > 0, but Trigger FALSE -> downstream wire and Action are dark
        Assert.True(inputNode.IsSimActive);
        Assert.True(inToTrigConn.IsActive);
        Assert.False(trigNode.IsSimTriggered);
        Assert.False(trigToActConn.IsActive, "Downstream wire must not activate when trigger fails");
        Assert.False(actNode.IsSimFired, "Action must not fire when trigger fails");
    }

    [Fact]
    public void CanvasSimulation_SequentialPropagation_CancelsOnResetSim()
    {
        var logicVM = CreateFixture();
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First(n => n.Tag?.Name == "AI0");
        logicVM.SimPropagationDelayMs = 200;
        logicVM.StartSimulationCommand.Execute(null);

        inputNode.Tag!.Value = 90;
        logicVM.RunSimScan(100, animate: true);
        var task = logicVM.LastAnimationTask;
        Assert.NotNull(task);

        // Reset simulation during animation
        logicVM.ResetSimCommand.Execute(null);

        // All visuals are immediately cleared
        Assert.All(logicVM.Connections, c => Assert.False(c.IsActive));
        Assert.All(logicVM.Nodes, n => Assert.False(n.IsSimActive));
    }

    [Fact]
    public void CanvasSimulation_ScanHistory_ScanDisplay_AndPurity()
    {
        var logicVM = CreateFixture();
        logicVM.StartSimulationCommand.Execute(null);

        // Step scan to record items
        logicVM.StepSimScanCommand.Execute(null);

        Assert.NotEmpty(logicVM.SimScanHistory);
        var firstItem = logicVM.SimScanHistory[0];

        // RuleIndex is set and ScanDisplay reflects it
        Assert.True(firstItem.RuleIndex > 0);
        Assert.Contains($"(R{firstItem.RuleIndex})", firstItem.ScanDisplay);

        // Purity test: ActivityBadgeText in Vietnamese mode
        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.DoesNotContain("(INPUTS)", firstItem.ActivityBadgeText);
        Assert.DoesNotContain("(TRIGGER)", firstItem.ActivityBadgeText);
        Assert.DoesNotContain("(GUARD)", firstItem.ActivityBadgeText);
        Assert.DoesNotContain("(ACTION)", firstItem.ActivityBadgeText);

        // ActivityBadgeText in English mode
        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
        Assert.DoesNotContain("(INPUTS)", firstItem.ActivityBadgeText);
        Assert.DoesNotContain("(TRIGGER)", firstItem.ActivityBadgeText);
        Assert.DoesNotContain("(GUARD)", firstItem.ActivityBadgeText);
        Assert.DoesNotContain("(ACTION)", firstItem.ActivityBadgeText);

        // Reset to default
        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
    }

    [Fact]
    public void CanvasSimulation_ToggleSimTag_UpdatesTagAndInputNodes()
    {
        var logicVM = CreateFixture();
        var diTag = logicVM.TagCatalog.AllTags.First(t => t.Kind == TagKind.DiscreteInput);
        int initialVal = diTag.Value;

        // Toggle to opposite
        logicVM.ToggleSimTagCommand.Execute(diTag);
        Assert.Equal(initialVal == 0 ? 1 : 0, diTag.Value);

        var matchingNode = logicVM.Nodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Index == diTag.Index);
        if (matchingNode != null)
        {
            Assert.Equal(diTag.Value != 0, matchingNode.IsSimActive);
            Assert.Equal(diTag.Value != 0, matchingNode.IsLiveActive);
        }

        // Toggle back
        logicVM.ToggleSimTagCommand.Execute(diTag);
        Assert.Equal(initialVal, diTag.Value);
        if (matchingNode != null)
        {
            Assert.Equal(diTag.Value != 0, matchingNode.IsSimActive);
            Assert.Equal(diTag.Value != 0, matchingNode.IsLiveActive);
        }
    }

    [Fact]
    public void CanvasSimulation_ResetSim_ClearsOutputsAndCounters()
    {
        var logicVM = CreateFixture();
        logicVM.StartSimulationCommand.Execute(null);

        // Set DO and Counter tags
        var doTag = logicVM.TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput);
        if (doTag != null) doTag.Value = 1;

        var counterTag = logicVM.TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.Counter);
        if (counterTag != null) counterTag.Value = 42;

        logicVM.StepSimScanCommand.Execute(null);
        Assert.True(logicVM.SimScanNumber > 0);

        // Act: Reset simulation
        logicVM.ResetSimCommand.Execute(null);

        // Assert
        Assert.Equal(0, logicVM.SimScanNumber);
        Assert.Equal(0, logicVM.SimElapsedMs);
        Assert.Empty(logicVM.SimScanHistory);
        if (doTag != null) Assert.Equal(0, doTag.Value);
        if (counterTag != null) Assert.Equal(0, counterTag.Value);
    }

    [Fact]
    public void CanvasSimulation_ContextualNodeSelection_SupportsDirectSimulationInteraction()
    {
        var logicVM = CreateFixture();
        logicVM.StartSimulationCommand.Execute(null);
        Assert.True(logicVM.IsSimulationMode);

        // State 1: No node selected
        logicVM.SelectedNode = null;
        Assert.Null(logicVM.SelectedNode);
        Assert.NotEmpty(logicVM.CanvasInputNodes);
        Assert.NotEmpty(logicVM.CanvasActionNodes);

        // State 2: Select InputNode
        var inputNode = logicVM.Nodes.OfType<InputNodeViewModel>().First();
        logicVM.SelectedNode = inputNode;
        Assert.Same(inputNode, logicVM.SelectedInputNode);

        var initialVal = inputNode.Tag?.Value ?? 0;
        logicVM.ToggleInputNodeCommand.Execute(inputNode);
        Assert.Equal(initialVal == 0 ? 1 : 0, inputNode.Tag?.Value);

        // State 3: Select GuardNode
        var guardNode = logicVM.Nodes.OfType<GuardNodeViewModel>().First();
        logicVM.SelectedNode = guardNode;
        Assert.Same(guardNode, logicVM.SelectedGuardNode);

        if (guardNode.GuardTag != null)
        {
            var initialGuardVal = guardNode.GuardTag.Value;
            logicVM.ToggleSimTagCommand.Execute(guardNode.GuardTag);
            Assert.Equal(initialGuardVal == 0 ? 1 : 0, guardNode.GuardTag.Value);
        }

        // State 4: Select ActionNode
        var actionNode = logicVM.Nodes.OfType<ActionNodeViewModel>().First();
        logicVM.SelectedNode = actionNode;
        Assert.Same(actionNode, logicVM.SelectedActionNode);
        Assert.NotNull(actionNode.TargetTag);
    }

    [Fact]
    public void CanvasSimulation_TonTimerGraph_ToggleInputBehavior()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.Nodes.Clear();
        logicVM.Connections.Clear();

        var di0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");

        var inNode = new InputNodeViewModel(di0);
        var timerNode = new TimerNodeViewModel("TON", di0, do0, 5000);
        var actNode = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1 };

        logicVM.Nodes.Add(inNode);
        logicVM.Nodes.Add(timerNode);
        logicVM.Nodes.Add(actNode);

        logicVM.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], timerNode.InputConnectors[0]));
        logicVM.Connections.Add(new ConnectionViewModel(timerNode.OutputConnectors[0], actNode.InputConnectors[0]));

        logicVM.CompileNow();
        Assert.Equal(CompileState.Valid, logicVM.CompileState);

        logicVM.StartSimulation();
        logicVM.SelectedNode = inNode;

        var inConn = logicVM.Connections[0];

        // Act 1: Toggle input ON - connection wire must light up immediately
        logicVM.ToggleInputNode(inNode);
        Assert.Equal(1, inNode.Tag!.Value);
        Assert.True(inNode.IsSimActive);
        Assert.True(inConn.IsActive);

        // Step scan 1
        logicVM.StepSimScan();
        Assert.Equal(1, inNode.Tag.Value);
        Assert.True(inNode.IsSimActive);
        Assert.True(inConn.IsActive);

        // Run multiple scans to reach 5000ms preset (each step is 100ms)
        for (int i = 0; i < 60; i++)
        {
            logicVM.StepSimScan();
            Assert.Equal(1, inNode.Tag.Value);
        }
        Assert.Equal(1, do0.Value);

        // Act 2: Toggling input OFF resets wire and allows falling edge rule to execute
        logicVM.ToggleInputNode(inNode);
        Assert.Equal(0, inNode.Tag.Value);
        Assert.False(inNode.IsSimActive);
        Assert.False(inConn.IsActive);
        logicVM.StepSimScan();
        Assert.Equal(0, do0.Value);
    }

    [Fact]
    public void CanvasSimulation_HardwarePolling_DoesNotOverwriteSimulationTags()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTableVM = new RuleTableViewModel(tagCatalog);
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTableVM, uiDispatcher: action => action());
        logicVM.Nodes.Clear();
        logicVM.Connections.Clear();

        var di0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");
        var inNode = new InputNodeViewModel(di0);
        var timerNode = new TimerNodeViewModel("TON", di0, do0, 1000);
        var actNode = new ActionNodeViewModel(do0) { ActionType = ActionType.SET_TAG, ActionParam = 1 };

        logicVM.Nodes.Add(inNode);
        logicVM.Nodes.Add(timerNode);
        logicVM.Nodes.Add(actNode);
        logicVM.Connections.Add(new ConnectionViewModel(inNode.OutputConnectors[0], timerNode.InputConnectors[0]));
        logicVM.Connections.Add(new ConnectionViewModel(timerNode.OutputConnectors[0], actNode.InputConnectors[0]));

        logicVM.CompileNow();
        logicVM.StartSimulation();
        logicVM.ToggleInputNode(inNode);

        Assert.Equal(1, di0.Value);
        Assert.True(logicVM.IsSimulationMode);

        // Simulate incoming hardware snapshot from device state store (DI0 = 0 from physical port)
        var snapshot = new SimplePLC.Application.Models.RuntimeDeviceSnapshot(
            Endpoint: null,
            ConnectionStatus: SimplePLC.Application.Models.ConnectionStatus.Connected,
            Health: null,
            Tags: new[] { new SimplePLC.Application.Models.RuntimeTagSnapshot(0, "DI0", null, SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, 0, SimplePLC.Application.Enums.TagQuality.Good, System.DateTimeOffset.UtcNow) },
            Timestamp: System.DateTimeOffset.UtcNow,
            Timers: System.Array.Empty<SimplePLC.Protocol.Dto.FbTimerRecordDto>(),
            Counters: System.Array.Empty<SimplePLC.Protocol.Dto.FbCounterRecordDto>());

        logicVM.ApplySnapshotDiff(snapshot);

        // In simulation mode, ApplySnapshotDiff must NOT overwrite the simulated input state
        Assert.Equal(1, di0.Value);
        Assert.True(inNode.IsSimActive);
    }
}
