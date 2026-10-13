using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class AiMacroAndPipelineTests
{
    private static List<TagModel> CreateTagCatalog()
    {
        return new List<TagModel>
        {
            new() { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput, Alias = "Start" },
            new() { Index = 1, Name = "DI1", Kind = TagKind.DiscreteInput, Alias = "Stop NC" },
            new() { Index = 2, Name = "DI2", Kind = TagKind.DiscreteInput, Alias = "Sensor A" },
            new() { Index = 3, Name = "DI3", Kind = TagKind.DiscreteInput, Alias = "Pulse Count" },
            new() { Index = 4, Name = "DI4", Kind = TagKind.DiscreteInput, Alias = "Reset Btn" },
            new() { Index = 5, Name = "DI5", Kind = TagKind.DiscreteInput, Alias = "Down Pulse" },
            new() { Index = 6, Name = "DI6", Kind = TagKind.DiscreteInput, Alias = "Load Btn" },
            new() { Index = 8, Name = "DO0", Kind = TagKind.DiscreteOutput, Alias = "Pump 1" },
            new() { Index = 9, Name = "DO1", Kind = TagKind.DiscreteOutput, Alias = "Valve 1" },
            new() { Index = 10, Name = "DO2", Kind = TagKind.DiscreteOutput, Alias = "Horn" },
            new() { Index = 11, Name = "DO3", Kind = TagKind.DiscreteOutput, Alias = "Batch Done" },
            new() { Index = 12, Name = "DO4", Kind = TagKind.DiscreteOutput, Alias = "Empty Alert" },
            new() { Index = 16, Name = "AI0", Kind = TagKind.AnalogInput, Alias = "Pressure 4-20mA" },
            new() { Index = 21, Name = "VFLAG0", Kind = TagKind.VirtualFlag, Alias = "Auto Mode" },
            new() { Index = 32, Name = "VREG0", Kind = TagKind.VirtualRegister, Alias = "Scaler Out" },
            new() { Index = 33, Name = "VREG1", Kind = TagKind.VirtualRegister, Alias = "CV Counter 1" },
            new() { Index = 34, Name = "VREG2", Kind = TagKind.VirtualRegister, Alias = "CV Counter 2" },
            new() { Index = 35, Name = "VREG3", Kind = TagKind.VirtualRegister, Alias = "Scaled Pressure" }
        };
    }

    [Fact]
    public void DraftGraphTransaction_ConfigureScale_UpdatesTargetNodeAttributes()
    {
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "scale_node_1",
            ["node_type"] = "SCALE",
            ["custom_label"] = "Pressure Scaler",
            ["position_x"] = 120,
            ["position_y"] = 240,
            ["tag_name"] = "NONE",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        tx.ApplyToolCall(new AiToolCall("configure_scale", new JsonObject
        {
            ["node_id"] = "scale_node_1",
            ["gain"] = 0.05,
            ["offset"] = 10.5,
            ["unit"] = "bar",
            ["decimal_places"] = 2,
            ["is_clamped"] = true,
            ["clamp_min"] = 0.0,
            ["clamp_max"] = 100.0,
            ["input_tag_name"] = "AI0",
            ["output_tag_name"] = "VREG0"
        }));

        var targetNode = tx.AddedNodes.FirstOrDefault(n => n.Id == "scale_node_1");
        Assert.NotNull(targetNode);
        Assert.Equal(0.05, targetNode.Gain);
        Assert.Equal(10.5, targetNode.Offset);
        Assert.Equal("bar", targetNode.Unit);
        Assert.Equal(2, targetNode.DecimalPlaces);
        Assert.True(targetNode.IsClamped);
        Assert.Equal(0.0, targetNode.ClampMin);
        Assert.Equal(100.0, targetNode.ClampMax);
        Assert.Equal("AI0", targetNode.InputTagName);
        Assert.Equal("VREG0", targetNode.OutputTagName);
    }

    [Fact]
    public void DraftGraphTransaction_RenameTag_RecordsInTransaction()
    {
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("rename_tag", new JsonObject
        {
            ["tag_name"] = "DI0",
            ["new_alias"] = "Start Pushbutton NO",
            ["description"] = "Industrial Panel 24VDC"
        }));

        Assert.Single(tx.RenamedTags);
        Assert.Equal("DI0", tx.RenamedTags[0].TagName);
        Assert.Equal("Start Pushbutton NO", tx.RenamedTags[0].NewAlias);
        Assert.Equal("Industrial Panel 24VDC", tx.RenamedTags[0].Description);
    }

    [Fact]
    public void DraftGraphTransaction_DisconnectWire_RemovesWireFromAddedWires()
    {
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "wire_temp_1",
            ["source_node_id"] = "node_1",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "node_2",
            ["target_connector_title"] = "In"
        }));

        Assert.Single(tx.AddedWires);

        tx.ApplyToolCall(new AiToolCall("disconnect_wire", new JsonObject
        {
            ["connection_id"] = "wire_temp_1",
            ["source_node_id"] = "node_1",
            ["target_node_id"] = "node_2"
        }));

        Assert.Empty(tx.AddedWires);
        Assert.Contains("wire_temp_1", tx.DisconnectedWireIds);
    }

    [Fact]
    public void DraftGraphTransaction_AutoLayoutAndDiagnose_RecordsRequests()
    {
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "in_1",
            ["node_type"] = "INPUT",
            ["custom_label"] = "Start",
            ["position_x"] = 0,
            ["position_y"] = 0,
            ["tag_name"] = "DI0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        tx.ApplyToolCall(new AiToolCall("auto_layout_graph", new JsonObject
        {
            ["diagram_id"] = "MAIN",
            ["horizontal_spacing"] = 280,
            ["vertical_spacing"] = 140
        }));

        Assert.True(tx.IsAutoLayoutRequested);
        Assert.NotNull(tx.LayoutRequest);
        Assert.Equal(280, tx.LayoutRequest.HorizontalSpacing);
        Assert.Equal(140, tx.LayoutRequest.VerticalSpacing);
        Assert.Equal(60, tx.AddedNodes[0].PositionX); // Column 0 for INPUT

        tx.ApplyToolCall(new AiToolCall("diagnose_logic_bugs", new JsonObject
        {
            ["check_write_conflicts"] = true,
            ["check_cyclic_loops"] = true,
            ["auto_propose_fixes"] = false
        }));

        Assert.True(tx.IsDiagnosticRequested);
        Assert.NotNull(tx.DiagnosticCheck);
        Assert.True(tx.DiagnosticCheck.CheckWriteConflicts);
        Assert.True(tx.DiagnosticCheck.CheckCyclicLoops);
        Assert.False(tx.DiagnosticCheck.AutoProposeFixes);
    }

    [Fact]
    public void DraftNode_ScaleProperties_PreserveDefaultsAndCustomValues()
    {
        var node = new DraftNode();
        Assert.Equal(0.01, node.Gain);
        Assert.Equal(0.0, node.Offset);
        Assert.Equal("bar", node.Unit);
        Assert.Equal(1, node.DecimalPlaces);
        Assert.True(node.IsClamped);
        Assert.Equal(0.0, node.ClampMin);
        Assert.Equal(100.0, node.ClampMax);

        node.Gain = 2.5;
        node.Offset = -10.0;
        node.Unit = "°C";
        node.DecimalPlaces = 3;
        node.IsClamped = false;

        Assert.Equal(2.5, node.Gain);
        Assert.Equal(-10.0, node.Offset);
        Assert.Equal("°C", node.Unit);
        Assert.Equal(3, node.DecimalPlaces);
        Assert.False(node.IsClamped);
    }

    [Fact]
    public void AiExplanationSynthesizer_SynthesizesTonMacroRules()
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ton_1",
            NodeType = "TIMER",
            Mode = "TON",
            PresetValue = 3000,
            InputTagName = "DI0",
            OutputTagName = "DO0"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(2, rules.Count);
        // Rule 0: OnRise / for_ms=PT -> SET Q 1
        Assert.Equal("DI0", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal((uint)3000, rules[0].ForMs);
        Assert.Equal("DO0", rules[0].ActionTag);
        Assert.Equal("SET_TAG", rules[0].ActionType);
        Assert.Equal(1, rules[0].Param);

        // Rule 1: OnFall / for_ms=0 -> SET Q 0
        Assert.Equal("DI0", rules[1].InputTag);
        Assert.Equal("ON_FALL", rules[1].Trigger);
        Assert.Equal((uint)0, rules[1].ForMs);
        Assert.Equal("DO0", rules[1].ActionTag);
        Assert.Equal("SET_TAG", rules[1].ActionType);
        Assert.Equal(0, rules[1].Param);
    }

    [Fact]
    public void AiExplanationSynthesizer_SynthesizesTofMacroRules()
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "tof_1",
            NodeType = "TIMER",
            Mode = "TOF",
            PresetValue = 2500,
            InputTagName = "DI1",
            OutputTagName = "DO1"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(2, rules.Count);
        // Rule 0: OnRise / for_ms=0 -> SET Q 1
        Assert.Equal("DI1", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal((uint)0, rules[0].ForMs);
        Assert.Equal("DO1", rules[0].ActionTag);
        Assert.Equal("SET_TAG", rules[0].ActionType);
        Assert.Equal(1, rules[0].Param);

        // Rule 1: OnFall / for_ms=PT / Guard Q=1 -> SET Q 0
        Assert.Equal("DI1", rules[1].InputTag);
        Assert.Equal("ON_FALL", rules[1].Trigger);
        Assert.Equal((uint)2500, rules[1].ForMs);
        Assert.Equal("DO1", rules[1].GuardTag);
        Assert.False(rules[1].GuardNegated);
        Assert.Equal("DO1", rules[1].ActionTag);
        Assert.Equal("SET_TAG", rules[1].ActionType);
        Assert.Equal(0, rules[1].Param);
    }

    [Fact]
    public void AiExplanationSynthesizer_SynthesizesTpMacroRules()
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "tp_1",
            NodeType = "TIMER",
            Mode = "TP",
            PresetValue = 1500,
            InputTagName = "DI2",
            OutputTagName = "DO2"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(2, rules.Count);
        // Rule 0: OnRise / Guard NOT Q -> SET Q 1
        Assert.Equal("DI2", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal((uint)0, rules[0].ForMs);
        Assert.Equal("DO2", rules[0].GuardTag);
        Assert.True(rules[0].GuardNegated);
        Assert.Equal("DO2", rules[0].ActionTag);
        Assert.Equal(1, rules[0].Param);

        // Rule 1: Q OnRise / for_ms=PT -> SET Q 0
        Assert.Equal("DO2", rules[1].InputTag);
        Assert.Equal("ON_RISE", rules[1].Trigger);
        Assert.Equal((uint)1500, rules[1].ForMs);
        Assert.Equal("DO2", rules[1].ActionTag);
        Assert.Equal(0, rules[1].Param);
    }

    [Fact]
    public void AiExplanationSynthesizer_SynthesizesCtuMacroRules()
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ctu_1",
            NodeType = "COUNTER",
            Mode = "CTU",
            PresetValue = 10,
            InputTagName = "DI3",
            ResetTagName = "DI4",
            CvTagName = "VREG1",
            OutputTagName = "DO3"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(4, rules.Count);
        // Rule 0: CU OnRise / Guard NOT Reset -> CV += 1
        Assert.Equal("DI3", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal("DI4", rules[0].GuardTag);
        Assert.True(rules[0].GuardNegated);
        Assert.Equal("VREG1", rules[0].ActionTag);
        Assert.Equal("INC_COUNTER", rules[0].ActionType);
        Assert.Equal(1, rules[0].Param);

        // Rule 1: Reset OnRise -> SET CV 0
        Assert.Equal("DI4", rules[1].InputTag);
        Assert.Equal("ON_RISE", rules[1].Trigger);
        Assert.Equal("VREG1", rules[1].ActionTag);
        Assert.Equal("SET_TAG", rules[1].ActionType);
        Assert.Equal(0, rules[1].Param);

        // Rule 2: CV >= PV -> SET Q 1
        Assert.Equal("VREG1", rules[2].InputTag);
        Assert.Equal("ON_CHANGE", rules[2].Trigger);
        Assert.Equal("GTE", rules[2].CompareOp);
        Assert.Equal(10, rules[2].ThresholdLo);
        Assert.Equal("DO3", rules[2].ActionTag);
        Assert.Equal(1, rules[2].Param);

        // Rule 3: CV < PV -> SET Q 0
        Assert.Equal("VREG1", rules[3].InputTag);
        Assert.Equal("ON_CHANGE", rules[3].Trigger);
        Assert.Equal("LT", rules[3].CompareOp);
        Assert.Equal(10, rules[3].ThresholdLo);
        Assert.Equal("DO3", rules[3].ActionTag);
        Assert.Equal(0, rules[3].Param);
    }

    [Fact]
    public void AiExplanationSynthesizer_SynthesizesCtdMacroRules()
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "ctd_1",
            NodeType = "COUNTER",
            Mode = "CTD",
            PresetValue = 5,
            InputTagName = "DI5",
            ResetTagName = "DI6",
            CvTagName = "VREG2",
            OutputTagName = "DO4"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Equal(4, rules.Count);
        // Rule 0: CD OnRise / Guard NOT Reset -> CV -= 1
        Assert.Equal("DI5", rules[0].InputTag);
        Assert.Equal("ON_RISE", rules[0].Trigger);
        Assert.Equal("DI6", rules[0].GuardTag);
        Assert.True(rules[0].GuardNegated);
        Assert.Equal("VREG2", rules[0].ActionTag);
        Assert.Equal("INC_COUNTER", rules[0].ActionType);
        Assert.Equal(-1, rules[0].Param);

        // Rule 1: Reset OnRise -> SET CV PV (5)
        Assert.Equal("DI6", rules[1].InputTag);
        Assert.Equal("ON_RISE", rules[1].Trigger);
        Assert.Equal("VREG2", rules[1].ActionTag);
        Assert.Equal("SET_TAG", rules[1].ActionType);
        Assert.Equal(5, rules[1].Param);

        // Rule 2: CV <= 0 -> SET Q 1
        Assert.Equal("VREG2", rules[2].InputTag);
        Assert.Equal("ON_CHANGE", rules[2].Trigger);
        Assert.Equal("LTE", rules[2].CompareOp);
        Assert.Equal(0, rules[2].ThresholdLo);
        Assert.Equal("DO4", rules[2].ActionTag);
        Assert.Equal(1, rules[2].Param);

        // Rule 3: CV > 0 -> SET Q 0
        Assert.Equal("VREG2", rules[3].InputTag);
        Assert.Equal("ON_CHANGE", rules[3].Trigger);
        Assert.Equal("GT", rules[3].CompareOp);
        Assert.Equal(0, rules[3].ThresholdLo);
        Assert.Equal("DO4", rules[3].ActionTag);
        Assert.Equal(0, rules[3].Param);
    }

    [Fact]
    public void AiExplanationSynthesizer_SynthesizesScaleMacroRule()
    {
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "scale_1",
            NodeType = "SCALE",
            Gain = 0.02,
            Offset = 5.0,
            InputTagName = "AI0",
            OutputTagName = "VREG3"
        });

        var rules = AiExplanationSynthesizer.GenerateJsonRules(tx);

        Assert.Single(rules);
        Assert.Equal("AI0", rules[0].InputTag);
        Assert.Equal("ON_CHANGE", rules[0].Trigger);
        Assert.Equal(5, rules[0].ThresholdHi);
        Assert.Equal("VREG3", rules[0].ActionTag);
        Assert.Equal("SCALE_TAG", rules[0].ActionType);
        Assert.Equal(20, rules[0].Param); // 0.02 * 1000 = 20
    }

    [Fact]
    public void LogicEditorViewModel_ApplyAiProposalToCanvas_MapsTimerInputTag()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "tm_auto",
            NodeType = "TIMER",
            Label = "Test Timer",
            PositionX = 200,
            PositionY = 150,
            TagName = "NONE",
            Mode = "TON",
            PresetValue = 3000,
            InputTagName = "DI0",
            OutputTagName = "DO0"
        });

        vm.ApplyAiProposalToCanvas(tx);

        Assert.Single(vm.Nodes);
        var timerNode = Assert.IsType<TimerNodeViewModel>(vm.Nodes[0]);
        Assert.True(timerNode.IsGhost);
        Assert.NotNull(timerNode.InputTag);
        Assert.Equal("DI0", timerNode.InputTag.Name);
        Assert.NotNull(timerNode.OutputTag);
        Assert.Equal("DO0", timerNode.OutputTag.Name);
        Assert.Equal((uint)3000, timerNode.PresetMs);
    }

    [Fact]
    public void LogicEditorViewModel_ApplyAiProposalToCanvas_MapsScaleProperties()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "sc_auto",
            NodeType = "SCALE",
            Label = "Scale Pressure",
            PositionX = 200,
            PositionY = 150,
            TagName = "NONE",
            InputTagName = "AI0",
            OutputTagName = "VREG0",
            Gain = 0.05,
            Offset = 12.0,
            Unit = "bar",
            DecimalPlaces = 2,
            IsClamped = true,
            ClampMin = 0.0,
            ClampMax = 100.0
        });

        vm.ApplyAiProposalToCanvas(tx);

        Assert.Single(vm.Nodes);
        var scaleNode = Assert.IsType<ScaleNodeViewModel>(vm.Nodes[0]);
        Assert.True(scaleNode.IsGhost);
        Assert.NotNull(scaleNode.InputTag);
        Assert.Equal("AI0", scaleNode.InputTag.Name);
        Assert.Equal(0.05, scaleNode.Gain);
        Assert.Equal(12.0, scaleNode.Offset);
        Assert.Equal("bar", scaleNode.Unit);
        Assert.Equal(2, scaleNode.DecimalPlaces);
        Assert.True(scaleNode.IsClamped);
        Assert.Equal(0.0, scaleNode.ClampMin);
        Assert.Equal(100.0, scaleNode.ClampMax);
    }

    [Fact]
    public void LogicEditorViewModel_ReconstructGraphFromRule_IsNonDestructive()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        // Add pre-existing user canvas work
        var userNode = new InputNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "DI0"))
        {
            Location = new System.Windows.Point(50, 50)
        };
        vm.Nodes.Add(userNode);
        Assert.Single(vm.Nodes);

        var rule = new RuleItemModel
        {
            Id = "RULE_RECON_1",
            TriggerTag = tagCatalog.AllTags.First(t => t.Name == "DI1"),
            TriggerType = TriggerType.ON_RISE,
            ForMs = 50,
            ActionTag = tagCatalog.AllTags.First(t => t.Name == "DO0"),
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        };

        // Reconstruct should append without clearing existing userNode
        vm.ReconstructGraphFromRule(rule);

        Assert.True(vm.Nodes.Count > 1);
        Assert.Contains(userNode, vm.Nodes);
    }

    [Fact]
    public void AiSafetyGate_RejectsMacroOutputWritingToReadOnlyDI()
    {
        var tags = CreateTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.AddedNodes.Add(new DraftNode
        {
            Id = "illegal_timer",
            NodeType = "TIMER",
            InputTagName = "DI1",
            OutputTagName = "DI0" // Read-only!
        });

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-TAG-ERR-READONLY", result.ErrorCode);
    }

    [Theory]
    [InlineData("AI0")]
    [InlineData("DI0")]
    public void AiSafetyGate_RejectsCounterWhenCvTagNameTargetsReadOnlyInputTag(string readOnlyTag)
    {
        var tags = CreateTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.AddedNodes.Add(new DraftNode
        {
            Id = "illegal_counter",
            NodeType = "COUNTER",
            Mode = "CTU",
            InputTagName = "DI3",
            ResetTagName = "DI4",
            CvTagName = readOnlyTag, // Read-only hardware input!
            OutputTagName = "DO3"
        });

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-TAG-ERR-READONLY", result.ErrorCode);
        Assert.Contains(readOnlyTag, result.ErrorMessage);
    }

    [Fact]
    public void AiSafetyGate_AcceptsCounterWhenCvTagNameTargetsVirtualRegister()
    {
        var tags = CreateTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.AddedNodes.Add(new DraftNode
        {
            Id = "legal_counter",
            NodeType = "COUNTER",
            Mode = "CTU",
            InputTagName = "DI3",
            ResetTagName = "DI4",
            CvTagName = "VREG1", // Valid virtual register
            OutputTagName = "DO3"
        });

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void AiSafetyGate_DetectsWriteConflict_BetweenMultipleTimersDrivingSameOutputTag()
    {
        var tags = CreateTagCatalog();
        var tx = new DraftGraphTransaction();

        // Timer 1 driving DO0
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "timer_1",
            NodeType = "TIMER",
            Mode = "TON",
            InputTagName = "DI0",
            OutputTagName = "DO0"
        });

        // Timer 2 also driving DO0 (conflict!)
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "timer_2",
            NodeType = "TIMER",
            Mode = "TOF",
            InputTagName = "DI1",
            OutputTagName = "DO0"
        });

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-COMP-DEP-002", result.ErrorCode);
        Assert.Contains("DO0", result.ErrorMessage);
    }

    [Fact]
    public void AiSafetyGate_DetectsWriteConflict_BetweenMacroAndActionDrivingSameOutputTag()
    {
        var tags = CreateTagCatalog();
        var tx = new DraftGraphTransaction();

        // Timer driving DO0
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "timer_1",
            NodeType = "TIMER",
            Mode = "TON",
            InputTagName = "DI0",
            OutputTagName = "DO0"
        });

        // Action driving DO0 (conflict!)
        tx.AddedNodes.Add(new DraftNode
        {
            Id = "action_1",
            NodeType = "ACTION",
            TagName = "DO0",
            ActionType = "SET_TAG",
            ActionParam = 1
        });

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-COMP-DEP-002", result.ErrorCode);
        Assert.Contains("DO0", result.ErrorMessage);
    }

    [Fact]
    public void AiSafetyGate_AllowsIndependentMacroAndActionDrivingDifferentTags()
    {
        var tags = CreateTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.AddedNodes.Add(new DraftNode
        {
            Id = "timer_1",
            NodeType = "TIMER",
            Mode = "TON",
            InputTagName = "DI0",
            OutputTagName = "DO0"
        });

        tx.AddedNodes.Add(new DraftNode
        {
            Id = "action_1",
            NodeType = "ACTION",
            TagName = "DO1",
            ActionType = "SET_TAG",
            ActionParam = 1
        });

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public async Task AiChatViewModel_FallbackProposalFlow_RejectsProposalWritingToReadOnlyTag()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var chatVM = mainVM.AiChatVM;
        var logicVM = mainVM.LogicEditorVM;

        chatVM.ApiKeyInput = "valid-api-key";
        chatVM.SaveSettings();

        // LLM returns raw text with JSON rules (fallback flow) writing to read-only hardware input DI0
        string fallbackResponse = """
            Sau đây là cấu hình logic điều khiển:
            ```json:rules
            [
              {
                "input_tag": "DI1",
                "trigger": "ON_RISE",
                "guard_tag": "NONE",
                "guard_negated": false,
                "action_tag": "DI0",
                "action_type": "SET_TAG",
                "param": 1,
                "narrative": "Ghi đè giá trị ngõ vào vật lý DI0"
              }
            ]
            ```
            """;

        chatVM.ApiHandler = (settings, sysPrompt, history, tools, ct) =>
        {
            // Empty ToolCalls forces Fallback rule extraction
            return Task.FromResult(new GeminiToolResponse(fallbackResponse, new List<AiToolCall>()));
        };

        chatVM.InputText = "Tạo mạch điều khiển: kích hoạt DI1 thì bật DI0";
        await chatVM.SendMessageAsync();

        var lastMessage = chatVM.Messages.Last();
        Assert.False(lastMessage.HasProposal);
        Assert.Contains("Cổng Kiểm Duyệt An Toàn Từ Chối", lastMessage.Content);
        Assert.Contains("DI0", lastMessage.Content);
        Assert.False(logicVM.HasPendingAiProposal);
        Assert.DoesNotContain(logicVM.Nodes, n => n.IsGhost);
    }

    [Fact]
    public void LogicEditorViewModel_ReconstructGraphFromRule_SupportsUndoAndRedo()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog, uiDispatcher: a => a());

        // Pre-existing user node
        var userNode = new InputNodeViewModel(tagCatalog.AllTags.First(t => t.Name == "DI0"))
        {
            Location = new System.Windows.Point(50, 50)
        };
        vm.Nodes.Add(userNode);
        Assert.Single(vm.Nodes);

        var rule = new RuleItemModel
        {
            Id = "RULE_RECON_UNDO_TEST",
            TriggerTag = tagCatalog.AllTags.First(t => t.Name == "DI1"),
            TriggerType = TriggerType.ON_RISE,
            ForMs = 100,
            ActionTag = tagCatalog.AllTags.First(t => t.Name == "DO0"),
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        };

        // Reconstruct nodes
        vm.ReconstructGraphFromRule(rule);
        int nodeCountAfterReconstruct = vm.Nodes.Count;
        Assert.True(nodeCountAfterReconstruct > 1);

        // Undo snapshot must exist
        Assert.True(vm.CanUndo);
        vm.UndoCommand.Execute(null);

        // Canvas should revert back to original state with only userNode
        Assert.Single(vm.Nodes);
        Assert.Equal(userNode.Id, vm.Nodes[0].Id);
        Assert.Equal(userNode.DisplayTitle, vm.Nodes[0].DisplayTitle);

        // Redo should restore the reconstructed nodes
        Assert.True(vm.CanRedo);
        vm.RedoCommand.Execute(null);
        Assert.Equal(nodeCountAfterReconstruct, vm.Nodes.Count);
    }

    [Fact]
    public void AiSafetyGate_ValidateRulesOverload_RejectsReadOnlyAndWriteConflicts()
    {
        var tags = CreateTagCatalog();

        // 1. Read-only target rule
        var illegalRule = new List<AiRuleSpecModel>
        {
            new() { InputTag = "DI1", Trigger = "ON_RISE", ActionTag = "DI0", ActionType = "SET_TAG", Param = 1, Narrative = "Illegal DI0" }
        };
        var res1 = AiSafetyGate.Validate(illegalRule, tags);
        Assert.False(res1.IsValid);
        Assert.Equal("SPLC-TAG-ERR-READONLY", res1.ErrorCode);

        // 2. Write conflict (> 2 rules to same action tag)
        var conflictRules = new List<AiRuleSpecModel>
        {
            new() { InputTag = "DI1", Trigger = "ON_RISE", ActionTag = "DO0", ActionType = "SET_TAG", Param = 1, Narrative = "Rule 1" },
            new() { InputTag = "DI2", Trigger = "ON_RISE", ActionTag = "DO0", ActionType = "SET_TAG", Param = 0, Narrative = "Rule 2" },
            new() { InputTag = "DI3", Trigger = "ON_RISE", ActionTag = "DO0", ActionType = "SET_TAG", Param = 1, Narrative = "Rule 3" }
        };
        var res2 = AiSafetyGate.Validate(conflictRules, tags);
        Assert.False(res2.IsValid);
        Assert.Equal("SPLC-COMP-DEP-002", res2.ErrorCode);

        // 3. Valid rules
        var validRules = new List<AiRuleSpecModel>
        {
            new() { InputTag = "DI1", Trigger = "ON_RISE", ActionTag = "DO0", ActionType = "SET_TAG", Param = 1, Narrative = "Valid 1" }
        };
        var res3 = AiSafetyGate.Validate(validRules, tags);
        Assert.True(res3.IsValid);
    }
}
