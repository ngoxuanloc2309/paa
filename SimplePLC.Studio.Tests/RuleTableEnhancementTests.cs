using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class RuleTableEnhancementTests
{
    [Fact]
    public void CleanStartup_StartsEmptyWithZeroRules()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);

        Assert.Equal(0, vm.TotalRuleCount);
        Assert.Equal(0, vm.ActiveRuleCount);
        Assert.Equal(0, vm.DisabledRuleCount);
        Assert.Equal(0, vm.MemoryUsageBytes);
        Assert.Equal(0, vm.MemoryUsagePercent);
        Assert.False(vm.HasRules);
        Assert.Empty(vm.Rules);
        Assert.Empty(vm.FilteredRules);
        Assert.Null(vm.SelectedRule);
    }

    [Fact]
    public void MemoryQuota_CalculatesAccuratelyFor32ByteRules()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);

        vm.LoadDefaultRules(); // Loads 10 rules

        Assert.Equal(10, vm.TotalRuleCount);
        Assert.Equal(10, vm.ActiveRuleCount);
        Assert.Equal(0, vm.DisabledRuleCount);
        Assert.True(vm.HasRules);

        // 10 rules * 32 bytes = 320 bytes (10% of 3200 bytes quota)
        Assert.Equal(320, vm.MemoryUsageBytes);
        Assert.Equal(10, vm.MemoryUsagePercent);
    }

    [Fact]
    public void StatusFilter_FiltersActiveAndDisabledRules()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);
        vm.LoadDefaultRules();

        // Disable rule R2 (index 1)
        vm.Rules[1].Enabled = false;

        Assert.Equal(10, vm.TotalRuleCount);
        Assert.Equal(9, vm.ActiveRuleCount);
        Assert.Equal(1, vm.DisabledRuleCount);

        // Filter ACTIVE
        vm.SetStatusFilter("ACTIVE");
        Assert.True(vm.IsFilterActive);
        Assert.Equal(9, vm.FilteredRules.Count);
        Assert.DoesNotContain(vm.FilteredRules, r => r.Id == "R2");

        // Filter DISABLED
        vm.SetStatusFilter("DISABLED");
        Assert.True(vm.IsFilterDisabled);
        Assert.Single(vm.FilteredRules);
        Assert.Equal("R2", vm.FilteredRules[0].Id);

        // Filter ALL
        vm.SetStatusFilter("ALL");
        Assert.True(vm.IsFilterAll);
        Assert.Equal(10, vm.FilteredRules.Count);
    }

    [Fact]
    public void SearchFilter_FiltersByMultipleFields()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);
        vm.LoadDefaultRules();

        // Search by Id "R7"
        vm.SearchFilter = "R7";
        Assert.Single(vm.FilteredRules);
        Assert.Equal("R7", vm.FilteredRules[0].Id);

        // Search by Tag "DO3"
        vm.SearchFilter = "DO3";
        Assert.Single(vm.FilteredRules);
        Assert.Equal("R7", vm.FilteredRules[0].Id);

        // Search by Action "INC_COUNTER"
        vm.SearchFilter = "INC_COUNTER";
        Assert.Equal(4, vm.FilteredRules.Count); // R3, R4, R5, R6

        // Clear search
        vm.ClearSearch();
        Assert.Equal(10, vm.FilteredRules.Count);
    }

    [Fact]
    public void DisassemblyInspector_BreakdownAvailableForSelectedRule()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);
        vm.LoadDefaultRules();

        var r1 = vm.Rules[0];
        vm.SelectedRule = r1;

        Assert.NotNull(vm.SelectedRule);
        Assert.Equal("R1", vm.SelectedRule.Id);
        Assert.False(string.IsNullOrWhiteSpace(vm.SelectedRule.RawHex));
        Assert.False(string.IsNullOrWhiteSpace(vm.SelectedRule.RawHexBreakdown));
        Assert.Contains("|", vm.SelectedRule.RawHexBreakdown);
        // R1 has ForMs = 15000 (0x00003A98)
        Assert.Contains("00003A98", vm.SelectedRule.RawHexBreakdown);
    }

    [Fact]
    public void LogicFlowPipeline_PropertiesGenerateCleanUserCentricData()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);
        vm.LoadDefaultRules();

        // R1 has Trigger DI0 (ON_FALL, ForMs 15000), Guard VFLAG0, Action DO0 (SET_TAG 1)
        var r1 = vm.Rules[0];
        Assert.True(r1.HasGuard);
        Assert.True(r1.HasDwell);
        Assert.Contains("15", r1.TriggerDwellText);
        Assert.NotEmpty(r1.TriggerEventText);
        Assert.NotEmpty(r1.GuardConditionText);
        Assert.NotEmpty(r1.ActionDetailText);
        Assert.Equal(3, r1.RelatedTags.Count()); // DI0, VFLAG0, DO0

        // Toggle Dev Payload in VM
        Assert.False(vm.IsDevPayloadVisible);
        vm.ToggleDevPayload();
        Assert.True(vm.IsDevPayloadVisible);
        vm.ToggleDevPayload();
        Assert.False(vm.IsDevPayloadVisible);
    }

    [Fact]
    public void Localization_AllRuleTableKeysExistAndSwitchCorrectly()
    {
        var loc = LocalizationService.Instance;

        // Vietnamese
        loc.CurrentLanguage = "vi";
        Assert.Equal("DANH SÁCH QUY TẮC ĐIỀU KHIỂN", loc["RuleTableTitle"]);
        Assert.Equal("Tổng hợp các kịch bản tự động hóa được biên dịch từ đồ thị logic để thực thi trên thiết bị", loc["RuleTableDesc"]);
        Assert.Equal("Tín hiệu kích hoạt", loc["ColRuleTrigger"]);
        Assert.Equal("Điều kiện an toàn", loc["ColRuleGuard"]);
        Assert.Equal("Hành động thực thi", loc["ColRuleAction"]);
        Assert.Equal("Chưa có quy tắc logic nào được tạo", loc["RuleEmptyTitle"]);
        Assert.Equal("⚡ Mở Đồ Thị Logic", loc["RuleEmptyBtnGraph"]);
        Assert.Equal("🧩 Mở Thư Viện Mẫu", loc["RuleEmptyBtnBlueprints"]);
        Assert.Equal("Đồng bộ từ Đồ thị Logic", loc["RuleTableAutoSyncBadge"]);
        Assert.Equal("CHI TIẾT LUỒNG HOẠT ĐỘNG", loc["RuleFlowInspectorTitle"]);

        // English
        loc.CurrentLanguage = "en";
        Assert.Equal("CONTROL LOGIC RULES", loc["RuleTableTitle"]);
        Assert.Equal("Summary of automation logic rules compiled from the logic graph for device execution", loc["RuleTableDesc"]);
        Assert.Equal("Trigger Signal", loc["ColRuleTrigger"]);
        Assert.Equal("Safety Condition", loc["ColRuleGuard"]);
        Assert.Equal("Action Execution", loc["ColRuleAction"]);
        Assert.Equal("No logic rules created yet", loc["RuleEmptyTitle"]);
        Assert.Equal("⚡ Open Logic Graph", loc["RuleEmptyBtnGraph"]);
        Assert.Equal("🧩 Open Blueprints", loc["RuleEmptyBtnBlueprints"]);
        Assert.Equal("Synced from Logic Graph", loc["RuleTableAutoSyncBadge"]);
        Assert.Equal("LOGIC FLOW DETAILS", loc["RuleFlowInspectorTitle"]);

        // Restore to en
        loc.CurrentLanguage = "en";
    }

    [Fact]
    public void AddOrUpdateCompiledRules_AccumulatesRules_WithoutClearingExisting()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);

        Assert.Empty(vm.Rules);

        var di0 = new SimplePLC.Domain.Models.TagDefinition(0, "DI0", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
        var do0 = new SimplePLC.Domain.Models.TagDefinition(1, "DO0", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);
        var di1 = new SimplePLC.Domain.Models.TagDefinition(2, "DI1", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
        var do1 = new SimplePLC.Domain.Models.TagDefinition(3, "DO1", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);

        var domainRule1 = new SimplePLC.Domain.Models.Rule(
            0, "R1",
            new SimplePLC.Domain.Models.TriggerModel(di0, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            new SimplePLC.Domain.Models.ActionModel(do0, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            SimplePLC.Domain.Models.GuardModel.Empty,
            true);

        var added1 = vm.AddOrUpdateCompiledRules(new[] { domainRule1 }, null, null, null);
        Assert.Single(added1);
        Assert.Single(vm.Rules);
        Assert.Equal("R1", vm.Rules[0].Id);

        // Add second rule
        var domainRule2 = new SimplePLC.Domain.Models.Rule(
            1, "R2",
            new SimplePLC.Domain.Models.TriggerModel(di1, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            new SimplePLC.Domain.Models.ActionModel(do1, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            SimplePLC.Domain.Models.GuardModel.Empty,
            true);

        var added2 = vm.AddOrUpdateCompiledRules(new[] { domainRule2 }, null, null, null);
        Assert.Single(added2);
        Assert.Equal(2, vm.Rules.Count);
        Assert.Equal("R1", vm.Rules[0].Id);
        Assert.Equal("R2", vm.Rules[1].Id);
    }

    [Fact]
    public void AddOrUpdateCompiledRules_UpdatesExistingRuleInPlace_WhenTargetIdProvided()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);

        var di0 = new SimplePLC.Domain.Models.TagDefinition(0, "DI0", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
        var do0 = new SimplePLC.Domain.Models.TagDefinition(8, "DO0", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);
        var di1 = new SimplePLC.Domain.Models.TagDefinition(1, "DI1", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
        var do1 = new SimplePLC.Domain.Models.TagDefinition(9, "DO1", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);
        var do3 = new SimplePLC.Domain.Models.TagDefinition(11, "DO3", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);

        var r1 = new SimplePLC.Domain.Models.Rule(
            0, "R1",
            new SimplePLC.Domain.Models.TriggerModel(di0, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            new SimplePLC.Domain.Models.ActionModel(do0, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            SimplePLC.Domain.Models.GuardModel.Empty,
            true);

        var r2 = new SimplePLC.Domain.Models.Rule(
            1, "R2",
            new SimplePLC.Domain.Models.TriggerModel(di1, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            new SimplePLC.Domain.Models.ActionModel(do1, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            SimplePLC.Domain.Models.GuardModel.Empty,
            true);

        vm.AddOrUpdateCompiledRules(new[] { r1 }, null, null, null);
        vm.AddOrUpdateCompiledRules(new[] { r2 }, null, null, null);
        Assert.Equal(2, vm.Rules.Count);

        // Update R1 to target DO3
        var r1Updated = new SimplePLC.Domain.Models.Rule(
            0, "R1",
            new SimplePLC.Domain.Models.TriggerModel(di0, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            new SimplePLC.Domain.Models.ActionModel(do3, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            SimplePLC.Domain.Models.GuardModel.Empty,
            true);

        var updated = vm.AddOrUpdateCompiledRules(new[] { r1Updated }, null, null, "R1");
        Assert.Single(updated);
        Assert.Equal(2, vm.Rules.Count);
        Assert.Equal("R1", vm.Rules[0].Id);
        Assert.Contains("DO3", vm.Rules[0].ActionSummary);
    }

    [Fact]
    public void DeleteRule_RemovesRuleAndReindexes()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);

        var di0 = new SimplePLC.Domain.Models.TagDefinition(0, "DI0", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
        var do0 = new SimplePLC.Domain.Models.TagDefinition(8, "DO0", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);
        var di1 = new SimplePLC.Domain.Models.TagDefinition(1, "DI1", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
        var do1 = new SimplePLC.Domain.Models.TagDefinition(9, "DO1", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);

        var r1 = new SimplePLC.Domain.Models.Rule(
            0, "R1",
            new SimplePLC.Domain.Models.TriggerModel(di0, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            new SimplePLC.Domain.Models.ActionModel(do0, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            SimplePLC.Domain.Models.GuardModel.Empty,
            true);

        var r2 = new SimplePLC.Domain.Models.Rule(
            1, "R2",
            new SimplePLC.Domain.Models.TriggerModel(di1, SimplePLC.Domain.Enums.TriggerKind.OnRise),
            new SimplePLC.Domain.Models.ActionModel(do1, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
            SimplePLC.Domain.Models.GuardModel.Empty,
            true);

        vm.AddOrUpdateCompiledRules(new[] { r1 }, null, null, null);
        vm.AddOrUpdateCompiledRules(new[] { r2 }, null, null, null);
        Assert.Equal(2, vm.Rules.Count);

        // Delete first rule (R1)
        vm.DeleteRule(vm.Rules[0]);

        Assert.Single(vm.Rules);
        Assert.Equal("R1", vm.Rules[0].Id);
        Assert.Equal(0, vm.Rules[0].Index);
        Assert.Contains("DO1", vm.Rules[0].ActionSummary);
    }

    [Fact]
    public void CanvasDecoupling_ClearingCanvasDoesNotWipeRuleTable()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());

        var di0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");

        // Draw a rule on canvas: Input -> Trigger -> Action
        var inputNode = new InputNodeViewModel { Tag = di0 };
        var trigNode = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE };
        var actNode = new ActionNodeViewModel { ActionType = ActionType.SET_TAG, TargetTag = do0 };

        logicEditor.Nodes.Add(inputNode);
        logicEditor.Nodes.Add(trigNode);
        logicEditor.Nodes.Add(actNode);

        logicEditor.Connections.Add(new ConnectionViewModel(inputNode.OutputConnectors[0], trigNode.InputConnectors[0]));
        logicEditor.Connections.Add(new ConnectionViewModel(trigNode.OutputConnectors[0], actNode.InputConnectors[0]));

        // Explicit compile commits to RuleTable
        logicEditor.CompileNow();

        Assert.Single(ruleTable.Rules);
        Assert.Equal("R1", ruleTable.Rules[0].Id);

        // Now user calls NewRuleCanvas() (clearing canvas)
        logicEditor.NewRuleCanvas();

        Assert.Empty(logicEditor.Nodes);
        Assert.Empty(logicEditor.Connections);
        Assert.Null(logicEditor.EditingRuleId);

        // Trigger auto-sync on empty canvas
        logicEditor.CompileAndSaveRules(isAutoSync: true);

        // RULE TABLE MUST REMAIN INTACT WITH 1 RULE
        Assert.Single(ruleTable.Rules);
        Assert.Equal("R1", ruleTable.Rules[0].Id);
    }

    [Fact]
    public void RuleTable_CanGrowBeyond3Rules_ToAnyCapacity()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var di0 = new SimplePLC.Domain.Models.TagDefinition(0, "DI0", SimplePLC.Domain.Enums.TagKind.DiscreteInput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: true);
        var do0 = new SimplePLC.Domain.Models.TagDefinition(8, "DO0", SimplePLC.Domain.Enums.TagKind.DiscreteOutput, SimplePLC.Domain.Enums.TagDataType.Boolean, isReadOnly: false);

        // Add 15 rules sequentially
        for (int i = 0; i < 15; i++)
        {
            var r = new SimplePLC.Domain.Models.Rule(
                i, $"R{i + 1}",
                new SimplePLC.Domain.Models.TriggerModel(di0, SimplePLC.Domain.Enums.TriggerKind.OnRise),
                new SimplePLC.Domain.Models.ActionModel(do0, SimplePLC.Domain.Enums.ActionKind.SetTag, 1),
                SimplePLC.Domain.Models.GuardModel.Empty,
                true);
            ruleTable.AddOrUpdateCompiledRules(new[] { r }, null, null, null);
        }

        Assert.Equal(15, ruleTable.TotalRuleCount);
        Assert.Equal(15, ruleTable.FilteredRules.Count);
        Assert.Equal("R15", ruleTable.Rules[14].Id);
        Assert.Equal(15, ruleTable.MemoryUsagePercent);
    }

    [Fact]
    public void SaveAsNewRule_AddsNewRuleInsteadOfOverwritingExisting()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());

        var di0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");
        var do1 = tagCatalog.AllTags.First(t => t.Name == "DO1");

        // Rule 1: DI0 -> DO0
        var in1 = new InputNodeViewModel { Tag = di0 };
        var tr1 = new TriggerNodeViewModel { TriggerType = TriggerType.ON_RISE };
        var ac1 = new ActionNodeViewModel { ActionType = ActionType.SET_TAG, TargetTag = do0 };
        logicEditor.Nodes.Add(in1);
        logicEditor.Nodes.Add(tr1);
        logicEditor.Nodes.Add(ac1);
        logicEditor.Connections.Add(new ConnectionViewModel(in1.OutputConnectors[0], tr1.InputConnectors[0]));
        logicEditor.Connections.Add(new ConnectionViewModel(tr1.OutputConnectors[0], ac1.InputConnectors[0]));
        logicEditor.CompileNow();

        Assert.Single(ruleTable.Rules);
        Assert.Equal("R1", ruleTable.Rules[0].Id);
        Assert.Equal("R1", logicEditor.EditingRuleId);

        // Now user edits node on canvas to point to DO1, and clicks SaveAsNewRule
        ac1.TargetTag = do1;
        logicEditor.SaveAsNewRule();

        // Must now have 2 rules (R1 and R2), NOT overwrite R1!
        Assert.Equal(2, ruleTable.Rules.Count);
        Assert.Equal("R1", ruleTable.Rules[0].Id);
        Assert.Equal("R2", ruleTable.Rules[1].Id);
        Assert.Equal("DO0", ruleTable.Rules[0].ActionTag?.Name);
        Assert.Equal("DO1", ruleTable.Rules[1].ActionTag?.Name);
    }

    [Fact]
    public void SequentialBlueprints_AddIncrementallyWithoutOverwritingOldRules()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        // 1. Apply Blueprint: andon (1 rule)
        var bpAndon = blueprints.Blueprints.First(b => b.Id == "andon");
        blueprints.ApplyBlueprint(bpAndon);
        logicEditor.CompileNow();
        Assert.Single(ruleTable.Rules);

        // 2. Apply Blueprint: counter (2 rules)
        var bpCounter = blueprints.Blueprints.First(b => b.Id == "counter");
        blueprints.ApplyBlueprint(bpCounter);
        logicEditor.CompileNow();
        Assert.Equal(3, ruleTable.Rules.Count); // 1 + 2 = 3 rules

        // 3. Apply Blueprint: feed (1 rule)
        var bpFeed = blueprints.Blueprints.First(b => b.Id == "feed");
        blueprints.ApplyBlueprint(bpFeed);
        logicEditor.CompileNow();
        // MUST BE 4 RULES, NOT OVERWRITE R2 OR R3!
        Assert.Equal(4, ruleTable.Rules.Count);
        Assert.Equal("R4", ruleTable.Rules[3].Id);

        // 4. Apply Blueprint: overheat (1 rule)
        var bpOverheat = blueprints.Blueprints.First(b => b.Id == "overheat");
        blueprints.ApplyBlueprint(bpOverheat);
        logicEditor.CompileNow();
        // MUST BE 5 RULES!
        Assert.Equal(5, ruleTable.Rules.Count);
        Assert.Equal("R5", ruleTable.Rules[4].Id);
    }

    [Fact]
    public void FlowInspector_ToggleExpandsAndCollapses()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);
        vm.LoadDefaultRules();

        Assert.True(vm.IsFlowInspectorExpanded);
        Assert.Equal("▲", vm.FlowInspectorToggleIcon);

        vm.ToggleFlowInspector();
        Assert.False(vm.IsFlowInspectorExpanded);
        Assert.Equal("▼", vm.FlowInspectorToggleIcon);

        vm.ToggleFlowInspector();
        Assert.True(vm.IsFlowInspectorExpanded);
    }

    [Fact]
    public void MultiRuleDiagram_RepeatedCompiles_UpdateInPlace_WithoutDuplication()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        // Apply counter blueprint (2 rules)
        var bpCounter = blueprints.Blueprints.First(b => b.Id == "counter");
        blueprints.ApplyBlueprint(bpCounter);

        // First compile: adds R1 and R2
        logicEditor.CompileNow();
        Assert.Equal(2, ruleTable.Rules.Count);
        Assert.Equal("R1", ruleTable.Rules[0].Id);
        Assert.Equal("R2", ruleTable.Rules[1].Id);

        // Repeated compiles on the same canvas: MUST NOT CREATE R3, R4, R5...
        for (int i = 0; i < 5; i++)
        {
            logicEditor.CompileNow();
        }

        Assert.Equal(2, ruleTable.Rules.Count);
        Assert.Equal("R1", ruleTable.Rules[0].Id);
        Assert.Equal("R2", ruleTable.Rules[1].Id);
    }

    [Fact]
    public void DeduplicateRules_RemovesExactLogicDuplicates_AndReindexes()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new RuleTableViewModel(tagCatalog);

        var di0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var do0 = tagCatalog.AllTags.First(t => t.Name == "DO0");
        var do1 = tagCatalog.AllTags.First(t => t.Name == "DO1");

        // Add R1
        vm.Rules.Add(new RuleItemModel { Id = "R1", Index = 0, TriggerType = TriggerType.ON_RISE, TriggerTag = di0, ActionType = ActionType.SET_TAG, ActionTag = do0, ActionParam = 1 });
        // Add R2 (duplicate of R1)
        vm.Rules.Add(new RuleItemModel { Id = "R2", Index = 1, TriggerType = TriggerType.ON_RISE, TriggerTag = di0, ActionType = ActionType.SET_TAG, ActionTag = do0, ActionParam = 1 });
        // Add R3 (unique: do1)
        vm.Rules.Add(new RuleItemModel { Id = "R3", Index = 2, TriggerType = TriggerType.ON_RISE, TriggerTag = di0, ActionType = ActionType.SET_TAG, ActionTag = do1, ActionParam = 1 });
        // Add R4 (duplicate of R1)
        vm.Rules.Add(new RuleItemModel { Id = "R4", Index = 3, TriggerType = TriggerType.ON_RISE, TriggerTag = di0, ActionType = ActionType.SET_TAG, ActionTag = do0, ActionParam = 1 });
        // Add R5 (duplicate of R3)
        vm.Rules.Add(new RuleItemModel { Id = "R5", Index = 4, TriggerType = TriggerType.ON_RISE, TriggerTag = di0, ActionType = ActionType.SET_TAG, ActionTag = do1, ActionParam = 1 });

        Assert.Equal(5, vm.Rules.Count);

        int removed = vm.DeduplicateRulesCore();
        Assert.Equal(3, removed);
        Assert.Equal(2, vm.Rules.Count);

        Assert.Equal("R1", vm.Rules[0].Id);
        Assert.Equal(0, vm.Rules[0].Index);
        Assert.Equal("DO0", vm.Rules[0].ActionTag?.Name);

        Assert.Equal("R2", vm.Rules[1].Id);
        Assert.Equal(1, vm.Rules[1].Index);
        Assert.Equal("DO1", vm.Rules[1].ActionTag?.Name);
    }

    [Fact]
    public void Blueprints_Counter_AssignsVirtualRegisterRetainTags()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog);
        var logicEditor = new LogicEditorViewModel(tagCatalog, ruleTable: ruleTable, uiDispatcher: act => act());
        var blueprints = new BlueprintsViewModel(logicEditor, () => { });

        var bpCounter = blueprints.Blueprints.First(b => b.Id == "counter");
        blueprints.ApplyBlueprint(bpCounter);

        var actionNodes = logicEditor.Nodes.OfType<ActionNodeViewModel>().ToList();
        Assert.Equal(2, actionNodes.Count);

        // Tags must be VirtualRegisterRetain, not VirtualRegister (Timer 1)
        Assert.Equal(TagKind.VirtualRegisterRetain, actionNodes[0].TargetTag?.Kind);
        Assert.Equal(TagKind.VirtualRegisterRetain, actionNodes[1].TargetTag?.Kind);
        Assert.Equal("VREG_RETAIN0", actionNodes[0].TargetTag?.Name);
        Assert.Equal("VREG_RETAIN2", actionNodes[1].TargetTag?.Name);
    }
}
