using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Adversarial stress test harness for Milestone 2:
/// - IEC Macro Expansion (TON, TOF, TP, CTU, CTD, SCALE) with extreme boundary values.
/// - Tool calling execution (configure_scale with missing optional fields).
/// - Non-destructive graph reconstruction (ReconstructGraphFromRule with multiple existing nodes).
/// - Ghost engine lifecycle and safety validation invariants.
/// Designed by teamwork_preview_challenger_m2_1.
/// </summary>
public class AiMacroAndPipelineStressTests
{
    private static List<TagModel> CreateTagCatalog()
    {
        return new List<TagModel>
        {
            new() { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput, Alias = "Start NO" },
            new() { Index = 1, Name = "DI1", Kind = TagKind.DiscreteInput, Alias = "Stop NC" },
            new() { Index = 2, Name = "DI2", Kind = TagKind.DiscreteInput, Alias = "Proximity Sensor" },
            new() { Index = 3, Name = "DI3", Kind = TagKind.DiscreteInput, Alias = "Pulse Trigger" },
            new() { Index = 4, Name = "DI4", Kind = TagKind.DiscreteInput, Alias = "Reset Button" },
            new() { Index = 8, Name = "DO0", Kind = TagKind.DiscreteOutput, Alias = "Motor Contactor" },
            new() { Index = 9, Name = "DO1", Kind = TagKind.DiscreteOutput, Alias = "Solenoid Valve" },
            new() { Index = 10, Name = "DO2", Kind = TagKind.DiscreteOutput, Alias = "Strobe Light" },
            new() { Index = 16, Name = "AI0", Kind = TagKind.AnalogInput, Alias = "Pressure Transmitter 4-20mA" },
            new() { Index = 17, Name = "AI1", Kind = TagKind.AnalogInput, Alias = "Flow Rate 0-10V" },
            new() { Index = 20, Name = "VFLAG0", Kind = TagKind.VirtualFlag, Alias = "Aux Run Interlock" },
            new() { Index = 32, Name = "VREG0", Kind = TagKind.VirtualRegister, Alias = "Scaled Pressure" },
            new() { Index = 33, Name = "VREG1", Kind = TagKind.VirtualRegister, Alias = "Counter CV" }
        };
    }

    // =========================================================================
    // 1. IEC MACRO RULE GENERATION: TIMER (TON, TOF, TP) BOUNDARIES
    // =========================================================================

    [Theory]
    [InlineData(0, 1000u)]              // 0 ms -> fallback default 1000 ms
    [InlineData(-100, 1000u)]           // Negative ms -> fallback default 1000 ms
    [InlineData(1, 1u)]                 // Minimal positive 1 ms
    [InlineData(50, 50u)]               // Standard debounce duration
    [InlineData(60000, 60000u)]         // 1 minute (60,000 ms)
    [InlineData(2147483647, 2147483647u)] // int.MaxValue boundary
    public void MacroRuleGeneration_TON_ExtremeBoundaries(int presetValue, uint expectedForMs)
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ton_boundary",
            NodeType = "TIMER",
            Mode = "TON",
            PresetValue = presetValue,
            InputTagName = "DI0",
            OutputTagName = "DO0"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(2, rules.Count);
        // Rule 0: OnRise / ForMs = expected
        Assert.Equal("DI0", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal(expectedForMs, rules[0].ForMs);
        Assert.Equal("DO0", rules[0].ActionTag);
        Assert.Equal(1, rules[0].Param);

        // Rule 1: OnFall / ForMs = 0
        Assert.Equal("DI0", rules[1].InputTag);
        Assert.Equal("ON_FALL", rules[1].Trigger);
        Assert.Equal(0u, rules[1].ForMs);
        Assert.Equal("DO0", rules[1].ActionTag);
        Assert.Equal(0, rules[1].Param);
    }

    [Theory]
    [InlineData(0, 1000u)]
    [InlineData(-500, 1000u)]
    [InlineData(1, 1u)]
    [InlineData(86400000, 86400000u)] // 24 hours in ms
    public void MacroRuleGeneration_TOF_ExtremeBoundaries(int presetValue, uint expectedForMs)
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "tof_boundary",
            NodeType = "TIMER",
            Mode = "TOF",
            PresetValue = presetValue,
            InputTagName = "DI1",
            OutputTagName = "DO1"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(2, rules.Count);
        // Rule 0: OnRise / ForMs = 0 -> SET Q 1
        Assert.Equal("DI1", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal(0u, rules[0].ForMs);
        Assert.Equal("DO1", rules[0].ActionTag);
        Assert.Equal(1, rules[0].Param);

        // Rule 1: OnFall / ForMs = expected / Guard Q=1 -> SET Q 0
        Assert.Equal("DI1", rules[1].InputTag);
        Assert.Equal("ON_FALL", rules[1].Trigger);
        Assert.Equal(expectedForMs, rules[1].ForMs);
        Assert.Equal("DO1", rules[1].GuardTag);
        Assert.False(rules[1].GuardNegated);
        Assert.Equal("DO1", rules[1].ActionTag);
        Assert.Equal(0, rules[1].Param);
    }

    [Theory]
    [InlineData(0, 1000u)]
    [InlineData(-1, 1000u)]
    [InlineData(10, 10u)]
    [InlineData(5000, 5000u)]
    public void MacroRuleGeneration_TP_ExtremeBoundaries(int presetValue, uint expectedForMs)
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "tp_boundary",
            NodeType = "TIMER",
            Mode = "TP",
            PresetValue = presetValue,
            InputTagName = "DI2",
            OutputTagName = "DO2"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(2, rules.Count);
        // Rule 0: OnRise / Guard NOT Q -> SET Q 1
        Assert.Equal("DI2", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal(0u, rules[0].ForMs);
        Assert.Equal("DO2", rules[0].GuardTag);
        Assert.True(rules[0].GuardNegated);
        Assert.Equal("DO2", rules[0].ActionTag);
        Assert.Equal(1, rules[0].Param);

        // Rule 1: Q OnRise / ForMs = expected -> SET Q 0
        Assert.Equal("DO2", rules[1].InputTag);
        Assert.Equal("ON_RISE", rules[1].Trigger);
        Assert.Equal(expectedForMs, rules[1].ForMs);
        Assert.Equal("DO2", rules[1].ActionTag);
        Assert.Equal(0, rules[1].Param);
    }

    // =========================================================================
    // 2. IEC MACRO RULE GENERATION: COUNTER (CTU, CTD) BOUNDARIES
    // =========================================================================

    [Theory]
    [InlineData(0, 10)]             // 0 -> fallback default 10
    [InlineData(-20, 10)]           // Negative -> fallback default 10
    [InlineData(1, 1)]              // Minimum pulse count 1
    [InlineData(99999, 99999)]      // High preset
    public void MacroRuleGeneration_CTU_ExtremeBoundaries(int presetValue, int expectedPv)
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ctu_boundary",
            NodeType = "COUNTER",
            Mode = "CTU",
            PresetValue = presetValue,
            InputTagName = "DI3",
            ResetTagName = "DI4",
            CvTagName = "VREG1",
            OutputTagName = "DO0"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(4, rules.Count);
        // Rule 0: Pulse -> CV += 1
        Assert.Equal("DI3", rules[0].InputTag);
        Assert.Equal("VREG1", rules[0].ActionTag);
        Assert.Equal("INC_COUNTER", rules[0].ActionType);
        Assert.Equal(1, rules[0].Param);
        Assert.Equal("DI4", rules[0].GuardTag);
        Assert.True(rules[0].GuardNegated);

        // Rule 1: Reset -> SET CV 0
        Assert.Equal("DI4", rules[1].InputTag);
        Assert.Equal("VREG1", rules[1].ActionTag);
        Assert.Equal(0, rules[1].Param);

        // Rule 2: CV >= PV -> SET Q 1
        Assert.Equal("VREG1", rules[2].InputTag);
        Assert.Equal("GTE", rules[2].CompareOp);
        Assert.Equal(expectedPv, rules[2].ThresholdLo);
        Assert.Equal("DO0", rules[2].ActionTag);
        Assert.Equal(1, rules[2].Param);

        // Rule 3: CV < PV -> SET Q 0
        Assert.Equal("VREG1", rules[3].InputTag);
        Assert.Equal("LT", rules[3].CompareOp);
        Assert.Equal(expectedPv, rules[3].ThresholdLo);
        Assert.Equal("DO0", rules[3].ActionTag);
        Assert.Equal(0, rules[3].Param);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    [InlineData(1, 1)]
    [InlineData(500, 500)]
    public void MacroRuleGeneration_CTD_ExtremeBoundaries(int presetValue, int expectedPv)
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ctd_boundary",
            NodeType = "COUNTER",
            Mode = "CTD",
            PresetValue = presetValue,
            InputTagName = "DI3",
            ResetTagName = "DI4",
            CvTagName = "VREG1",
            OutputTagName = "DO1"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(4, rules.Count);
        // Rule 0: Pulse -> CV -= 1
        Assert.Equal("DI3", rules[0].InputTag);
        Assert.Equal("VREG1", rules[0].ActionTag);
        Assert.Equal("INC_COUNTER", rules[0].ActionType);
        Assert.Equal(-1, rules[0].Param);

        // Rule 1: Load -> SET CV PV
        Assert.Equal("DI4", rules[1].InputTag);
        Assert.Equal("VREG1", rules[1].ActionTag);
        Assert.Equal(expectedPv, rules[1].Param);

        // Rule 2: CV <= 0 -> SET Q 1
        Assert.Equal("VREG1", rules[2].InputTag);
        Assert.Equal("LTE", rules[2].CompareOp);
        Assert.Equal(0, rules[2].ThresholdLo);
        Assert.Equal("DO1", rules[2].ActionTag);
        Assert.Equal(1, rules[2].Param);

        // Rule 3: CV > 0 -> SET Q 0
        Assert.Equal("VREG1", rules[3].InputTag);
        Assert.Equal("GT", rules[3].CompareOp);
        Assert.Equal(0, rules[3].ThresholdLo);
        Assert.Equal("DO1", rules[3].ActionTag);
        Assert.Equal(0, rules[3].Param);
    }

    [Fact]
    public void MacroRuleGeneration_CounterWithoutResetTag_GeneratesThreeRules()
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ctu_no_reset",
            NodeType = "COUNTER",
            Mode = "CTU",
            PresetValue = 25,
            InputTagName = "DI3",
            ResetTagName = "NONE", // No reset tag
            CvTagName = "VREG1",
            OutputTagName = "DO0"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        // Without reset tag, reset rule is omitted -> exactly 3 rules
        Assert.Equal(3, rules.Count);
        Assert.Equal("INC_COUNTER", rules[0].ActionType);
        Assert.Equal("NONE", rules[0].GuardTag); // No guard since no reset
        Assert.False(rules[0].GuardNegated);
        Assert.Equal("GTE", rules[1].CompareOp);
        Assert.Equal("LT", rules[2].CompareOp);
    }

    // =========================================================================
    // 3. IEC MACRO RULE GENERATION: SCALE BOUNDARIES & MATHEMATICAL CLAMPING
    // =========================================================================

    [Theory]
    [InlineData(0.0, 0.0, 0, 0)]              // Zero gain, zero offset
    [InlineData(0.0001, 0.0, 1, 0)]           // Sub-threshold tiny gain clamped to 1
    [InlineData(-0.0001, 0.0, -1, 0)]         // Sub-threshold tiny negative gain clamped to -1
    [InlineData(0.05, 10.4, 50, 10)]          // Positive gain & rounded offset
    [InlineData(-0.05, -12.6, -50, -13)]      // Negative gain & negative rounded offset
    [InlineData(12.345, -100.2, 12345, -100)] // Large positive gain & negative offset
    [InlineData(-2.0, 50.0, -2000, 50)]       // Negative inversion scaler
    public void MacroRuleGeneration_SCALE_ExtremeBoundaries(double gain, double offset, int expectedParam, int expectedThresholdHi)
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "scale_stress",
            NodeType = "SCALE",
            Gain = gain,
            Offset = offset,
            InputTagName = "AI0",
            OutputTagName = "VREG0"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Single(rules);
        Assert.Equal("AI0", rules[0].InputTag);
        Assert.Equal("ON_CHANGE", rules[0].Trigger);
        Assert.Equal("VREG0", rules[0].ActionTag);
        Assert.Equal("SCALE_TAG", rules[0].ActionType);
        Assert.Equal(expectedParam, rules[0].Param);
        Assert.Equal(expectedThresholdHi, rules[0].ThresholdHi);
    }

    // =========================================================================
    // 4. TOOL CALL EXECUTION: CONFIGURE_SCALE WITH MISSING OPTIONAL FIELDS
    // =========================================================================

    [Fact]
    public void ConfigureScale_MissingOptionalFields_DoesNotThrowAndAppliesSafeDefaults()
    {
        var tx = new DraftGraphTransaction();

        // 1. Add base SCALE node with initial configuration
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "scale_partial",
            ["node_type"] = "SCALE",
            ["custom_label"] = "Analog Scaler",
            ["position_x"] = 100,
            ["position_y"] = 200,
            ["tag_name"] = "NONE",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        var targetNode = tx.AddedNodes.First(n => n.Id == "scale_partial");
        targetNode.InputTagName = "AI1";
        targetNode.OutputTagName = "VREG0";

        // 2. Call configure_scale with ONLY node_id, gain, and offset (omitting unit, decimal_places, is_clamped, clamp_min, clamp_max, input/output tags)
        var sparseArgs = new JsonObject
        {
            ["node_id"] = "scale_partial",
            ["gain"] = 0.08,
            ["offset"] = -5.5
        };

        // Must not throw NullReferenceException or KeyNotFoundException
        var exception = Record.Exception(() => tx.ApplyToolCall(new AiToolCall("configure_scale", sparseArgs)));
        Assert.Null(exception);

        // Verify values
        Assert.Equal(0.08, targetNode.Gain);
        Assert.Equal(-5.5, targetNode.Offset);
        Assert.Equal("bar", targetNode.Unit); // default unit fallback
        Assert.Equal(0, targetNode.DecimalPlaces); // missing int -> 0
        Assert.False(targetNode.IsClamped); // missing bool -> false
        Assert.Equal(0.0, targetNode.ClampMin); // missing double -> 0
        Assert.Equal(0.0, targetNode.ClampMax); // missing double -> 0
        // Input and Output tags must not be wiped out when omitted in configure_scale
        Assert.Equal("AI1", targetNode.InputTagName);
        Assert.Equal("VREG0", targetNode.OutputTagName);
    }

    [Fact]
    public void ConfigureScale_WithNonExistentNodeId_SafelyIgnored()
    {
        var tx = new DraftGraphTransaction();
        var call = new AiToolCall("configure_scale", new JsonObject
        {
            ["node_id"] = "ghost_that_does_not_exist",
            ["gain"] = 1.0,
            ["offset"] = 0.0
        });

        var ex = Record.Exception(() => tx.ApplyToolCall(call));
        Assert.Null(ex);
        Assert.Empty(tx.AddedNodes);
    }

    [Fact]
    public void ConfigureScale_StringFormattedNumbers_ParsedCorrectly()
    {
        var tx = new DraftGraphTransaction();
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "scale_str",
            ["node_type"] = "SCALE",
            ["custom_label"] = "Scale Str",
            ["position_x"] = 0,
            ["position_y"] = 0,
            ["tag_name"] = "NONE",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        // Pass numbers as string tokens (common LLM JSON formatting variance)
        tx.ApplyToolCall(new AiToolCall("configure_scale", new JsonObject
        {
            ["node_id"] = "scale_str",
            ["gain"] = "0.035",
            ["offset"] = "15.2",
            ["decimal_places"] = "3",
            ["clamp_min"] = "-10.0",
            ["clamp_max"] = "150.0"
        }));

        var node = tx.AddedNodes.First(n => n.Id == "scale_str");
        Assert.Equal(0.035, node.Gain, precision: 4);
        Assert.Equal(15.2, node.Offset, precision: 2);
        Assert.Equal(3, node.DecimalPlaces);
        Assert.Equal(-10.0, node.ClampMin, precision: 2);
        Assert.Equal(150.0, node.ClampMax, precision: 2);
    }

    // =========================================================================
    // 5. NON-DESTRUCTIVE RECONSTRUCTION WITH MULTIPLE EXISTING NODES
    // =========================================================================

    [Fact]
    public void ReconstructGraphFromRule_WithMultipleExistingNodes_PreservesAllAndPositionsSafely()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        // Setup 4 existing user nodes across diverse coordinates
        var userIn = new InputNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "DI0"))
        {
            Location = new Point(60, 80)
        };
        var userTrg = new TriggerNodeViewModel
        {
            Location = new Point(300, 80)
        };
        var userGrd = new GuardNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "VFLAG0"))
        {
            Location = new Point(540, 80)
        };
        var userAct = new ActionNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "DO0"))
        {
            Location = new Point(780, 240) // Max Y = 240
        };

        vm.Nodes.Add(userIn);
        vm.Nodes.Add(userTrg);
        vm.Nodes.Add(userGrd);
        vm.Nodes.Add(userAct);

        // Connect user nodes
        vm.Connect(userIn.OutputConnectors[0], userTrg.InputConnectors[0]);
        vm.Connect(userTrg.OutputConnectors[0], userGrd.InputConnectors[0]);
        vm.Connect(userGrd.OutputConnectors[0], userAct.InputConnectors[0]);

        int initialNodeCount = vm.Nodes.Count;
        int initialConnCount = vm.Connections.Count;
        Assert.Equal(4, initialNodeCount);
        Assert.Equal(3, initialConnCount);

        // Reconstruct Rule 1 (Trigger DI1, Guard DI0, Action DO1)
        var rule1 = new RuleItemModel
        {
            Id = "R_CONSTRUCT_1",
            TriggerTag = tagCatalog.AllTags.First(t => t.Name == "DI1"),
            TriggerType = TriggerType.ON_RISE,
            GuardTag = tagCatalog.AllTags.First(t => t.Name == "DI0"),
            GuardNegated = true,
            ActionTag = tagCatalog.AllTags.First(t => t.Name == "DO1"),
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        };

        vm.ReconstructGraphFromRule(rule1);

        // Verify that all original nodes and connections survived intact
        Assert.Contains(userIn, vm.Nodes);
        Assert.Contains(userTrg, vm.Nodes);
        Assert.Contains(userGrd, vm.Nodes);
        Assert.Contains(userAct, vm.Nodes);
        Assert.True(vm.Nodes.Count >= 7); // 4 initial + at least 3-4 reconstructed
        Assert.True(vm.Connections.Count >= 5); // 3 initial + 2-3 reconstructed

        // Verify non-destructive spatial layout: new nodes must be positioned below existing max Y (240 + 160 = 400)
        var newNodes = vm.Nodes.Except(new GraphNodeViewModel[] { userIn, userTrg, userGrd, userAct }).ToList();
        foreach (var node in newNodes)
        {
            Assert.True(node.Location.Y >= 400, $"Expected node {node.Id} Y >= 400, but got {node.Location.Y}");
        }

        // Reconstruct Rule 2 sequentially
        double maxYAfterRule1 = vm.Nodes.Max(n => n.Location.Y);
        var rule2 = new RuleItemModel
        {
            Id = "R_CONSTRUCT_2",
            TriggerTag = tagCatalog.AllTags.First(t => t.Name == "DI2"),
            TriggerType = TriggerType.ON_FALL,
            ActionTag = tagCatalog.AllTags.First(t => t.Name == "DO2"),
            ActionType = ActionType.SET_TAG,
            ActionParam = 0
        };

        vm.ReconstructGraphFromRule(rule2);

        // Verify rule 2 is placed even further below
        var rule2Nodes = vm.Nodes.Where(n => n.Location.Y > maxYAfterRule1).ToList();
        Assert.NotEmpty(rule2Nodes);
        foreach (var r2n in rule2Nodes)
        {
            Assert.True(r2n.Location.Y >= maxYAfterRule1 + 160);
        }
    }

    // =========================================================================
    // 6. GHOST ENGINE LIFECYCLE: PREVIEW, REJECT, ACCEPT & UNDO RESTORATION
    // =========================================================================

    [Fact]
    public void GhostEngine_ProposalLifecycle_RejectRestoresCanvasToOriginalState()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        var userNode = new InputNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "DI0"))
        {
            Location = new Point(100, 100)
        };
        vm.Nodes.Add(userNode);

        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ghost_timer_preview",
            NodeType = "TIMER",
            Label = "Ghost TON",
            PositionX = 300,
            PositionY = 100,
            Mode = "TON",
            PresetValue = 5000,
            InputTagName = "DI0",
            OutputTagName = "DO0"
        });

        // 1. Apply Proposal (Ghost nodes appear)
        vm.ApplyAiProposalToCanvas(tx);
        Assert.True(vm.HasPendingAiProposal);
        Assert.Equal(2, vm.Nodes.Count);

        var ghostTimer = vm.Nodes.FirstOrDefault(n => n.Id == "ghost_timer_preview");
        Assert.NotNull(ghostTimer);
        Assert.True(ghostTimer.IsGhost);
        Assert.False(userNode.IsGhost);

        // 2. Reject Proposal (Ghost nodes stripped)
        vm.RejectAiProposal();
        Assert.False(vm.HasPendingAiProposal);
        Assert.Single(vm.Nodes);
        Assert.Equal(userNode, vm.Nodes[0]);
    }

    [Fact]
    public void GhostEngine_ProposalLifecycle_AcceptAndUndo_RestoresPreCommitState()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        var userNode = new InputNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "DI0"))
        {
            Location = new Point(100, 100)
        };
        vm.Nodes.Add(userNode);

        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ghost_commit_node",
            NodeType = "ACTION",
            Label = "Ghost Act",
            PositionX = 400,
            PositionY = 100,
            TagName = "DO0",
            ActionType = "SET_TAG",
            ActionParam = 1
        });

        vm.ApplyAiProposalToCanvas(tx);
        Assert.True(vm.HasPendingAiProposal);

        // Accept
        vm.AcceptAiProposal();
        Assert.False(vm.HasPendingAiProposal);
        Assert.Equal(2, vm.Nodes.Count);
        Assert.All(vm.Nodes, n => Assert.False(n.IsGhost));
        Assert.True(vm.CanUndo);

        // Undo
        vm.Undo();
        Assert.Single(vm.Nodes);
        Assert.Equal("DI0", ((InputNodeViewModel)vm.Nodes[0]).Tag?.Name);
    }

    // =========================================================================
    // 7. SAFETY GATE HARDENING: MACRO OUTPUT READ-ONLY INVARIANTS
    // =========================================================================

    [Theory]
    [InlineData("SCALE", "AI0")]    // Writing analog scale output to analog input AI0 (Illegal)
    [InlineData("SCALE", "DI0")]    // Writing analog scale output to discrete input DI0 (Illegal)
    [InlineData("COUNTER", "DI1")]  // Writing counter Q output to DI1 (Illegal)
    [InlineData("TIMER", "AI1")]    // Writing timer Q output to AI1 (Illegal)
    public void AiSafetyGate_RejectsMacroOutputWritingToReadOnlyInputTags(string macroType, string illegalTarget)
    {
        var tags = CreateTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.AddedNodes.Add(new DraftNode
        {
            Id = "illegal_macro_node",
            NodeType = macroType,
            OutputTagName = illegalTarget
        });

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-TAG-ERR-READONLY", result.ErrorCode);
        Assert.Contains(illegalTarget, result.ErrorMessage);
    }

    [Fact]
    public void DraftGraphTransaction_ComplexMultiToolSequence_AppliesAtomically()
    {
        var tx = new DraftGraphTransaction();

        // 1. Add Input, Timer, Scale, Action nodes
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "n_in",
            ["node_type"] = "INPUT",
            ["custom_label"] = "Button Start",
            ["position_x"] = 0,
            ["position_y"] = 0,
            ["tag_name"] = "DI0"
        }));

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "n_tm",
            ["node_type"] = "TIMER",
            ["custom_label"] = "Delay Timer",
            ["position_x"] = 0,
            ["position_y"] = 0,
            ["tag_name"] = "NONE"
        }));

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "n_sc",
            ["node_type"] = "SCALE",
            ["custom_label"] = "Analog Scaler",
            ["position_x"] = 0,
            ["position_y"] = 0,
            ["tag_name"] = "NONE"
        }));

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "n_act",
            ["node_type"] = "ACTION",
            ["custom_label"] = "Pump Run",
            ["position_x"] = 0,
            ["position_y"] = 0,
            ["tag_name"] = "DO0",
            ["action_type"] = "SET_TAG",
            ["action_param"] = 1
        }));

        // 2. Configure Timer & Scale
        tx.ApplyToolCall(new AiToolCall("configure_timer_counter", new JsonObject
        {
            ["node_id"] = "n_tm",
            ["mode"] = "TON",
            ["preset_value"] = 3500,
            ["input_tag_name"] = "DI0",
            ["output_tag_name"] = "DO0"
        }));

        tx.ApplyToolCall(new AiToolCall("configure_scale", new JsonObject
        {
            ["node_id"] = "n_sc",
            ["gain"] = -0.05,
            ["offset"] = 25.0,
            ["unit"] = "°C",
            ["decimal_places"] = 2,
            ["is_clamped"] = true,
            ["clamp_min"] = -20.0,
            ["clamp_max"] = 120.0,
            ["input_tag_name"] = "AI0",
            ["output_tag_name"] = "VREG0"
        }));

        // 3. Connect Wires (n_in -> n_tm, and n_tm -> n_act)
        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "w1",
            ["source_node_id"] = "n_in",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "n_tm",
            ["target_connector_title"] = "In"
        }));

        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "w2",
            ["source_node_id"] = "n_tm",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "n_act",
            ["target_connector_title"] = "In"
        }));

        // 4. Rename Tag
        tx.ApplyToolCall(new AiToolCall("rename_tag", new JsonObject
        {
            ["tag_name"] = "DO0",
            ["new_alias"] = "Main Feed Pump",
            ["description"] = "Industrial High-Pressure Pump"
        }));

        // 5. Auto layout
        tx.ApplyToolCall(new AiToolCall("auto_layout_graph", new JsonObject
        {
            ["diagram_id"] = "MAIN",
            ["horizontal_spacing"] = 300,
            ["vertical_spacing"] = 150
        }));

        // 6. Diagnose
        tx.ApplyToolCall(new AiToolCall("diagnose_logic_bugs", new JsonObject
        {
            ["check_write_conflicts"] = true,
            ["check_cyclic_loops"] = true,
            ["auto_propose_fixes"] = true
        }));

        Assert.Equal(4, tx.AddedNodes.Count);
        Assert.Equal(2, tx.AddedWires.Count);
        Assert.Single(tx.RenamedTags);
        Assert.True(tx.IsAutoLayoutRequested);
        Assert.True(tx.IsDiagnosticRequested);

        // Verify layered coordinates:
        // Col 0: INPUT (X = 60 + 0*300 = 60)
        // Col 1: TIMER and SCALE (X = 60 + 1*300 = 360)
        // Col 3: ACTION (X = 60 + 3*300 = 960)
        var inNode = tx.AddedNodes.First(n => n.Id == "n_in");
        var tmNode = tx.AddedNodes.First(n => n.Id == "n_tm");
        var scNode = tx.AddedNodes.First(n => n.Id == "n_sc");
        var actNode = tx.AddedNodes.First(n => n.Id == "n_act");

        Assert.Equal(60, inNode.PositionX);
        Assert.Equal(360, tmNode.PositionX);
        Assert.Equal(360, scNode.PositionX);
        Assert.Equal(960, actNode.PositionX);

        // Rules synthesis
        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);
        // TON produces 2 rules, SCALE produces 1 rule -> total 3 rules
        // (Action node connected to/driven by macro doesn't create duplicate ECA chain)
        Assert.Equal(3, rules.Count);
    }

    [Fact]
    public void MacroRuleGeneration_TagResolutionFromWires_ResolvesWhenPropsUnspecified()
    {
        var tx = new DraftGraphTransaction();

        // Nodes with NONE tags initially
        tx.AddedNodes.Add(new DraftNode { Id = "in_1", NodeType = "INPUT", TagName = "DI2" });
        tx.AddedNodes.Add(new DraftNode { Id = "tm_1", NodeType = "TIMER", Mode = "TON", PresetValue = 2000, TagName = "NONE", InputTagName = "NONE", OutputTagName = "NONE" });
        tx.AddedNodes.Add(new DraftNode { Id = "act_1", NodeType = "ACTION", TagName = "DO2" });

        // Wire In -> Timer.In, Timer.Out -> Act.In
        tx.AddedWires.Add(new DraftWire { Id = "w1", SourceNodeId = "in_1", SourcePort = "Out", TargetNodeId = "tm_1", TargetPort = "In" });
        tx.AddedWires.Add(new DraftWire { Id = "w2", SourceNodeId = "tm_1", SourcePort = "Out", TargetNodeId = "act_1", TargetPort = "In" });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(2, rules.Count);
        Assert.Equal("DI2", rules[0].InputTag);
        Assert.Equal("DO2", rules[0].ActionTag);
    }

    [Fact]
    public void ReconstructGraphFromRule_NullOrEmptyRule_DoesNotThrowOrCorruptCanvas()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        var userNode = new InputNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "DI0"))
        {
            Location = new Point(50, 50)
        };
        vm.Nodes.Add(userNode);

        // Null rule
        var exNull = Record.Exception(() => vm.ReconstructGraphFromRule(null!));
        Assert.Null(exNull);
        Assert.Single(vm.Nodes);

        // Rule with no trigger tag or action tag
        var emptyRule = new RuleItemModel { Id = "EMPTY" };
        var exEmpty = Record.Exception(() => vm.ReconstructGraphFromRule(emptyRule));
        Assert.Null(exEmpty);
        Assert.Contains(userNode, vm.Nodes);
    }
}

