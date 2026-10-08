using System.Collections.Generic;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class AiCopilotTests
{
    [Fact]
    public void AiPromptBuilder_BuildSystemInstruction_ContainsCoreArchitecture()
    {
        string instruction = AiPromptBuilder.BuildSystemInstruction();

        Assert.NotNull(instruction);
        Assert.Contains("SynaptiX Industrial PLC AI Assistant", instruction);
        Assert.Contains("4-Stage Pipeline", instruction);
        Assert.Contains("1 INPUT ➔ 1 OUTPUT", instruction);
        Assert.Contains("CHỈ ĐIỀU KHIỂN DUY NHẤT 1 TARGET TAG", instruction);
        Assert.Contains("TÁCH THÀNH NHIỀU RULE ĐỘC LẬP", instruction);
        Assert.Contains("ON_RISE", instruction);
        Assert.Contains("ON_FALL", instruction);
        Assert.Contains("for_ms", instruction);
        Assert.Contains("SET_TAG", instruction);
        Assert.Contains("VREG_RETAIN", instruction);
    }

    [Fact]
    public void AiPromptBuilder_BuildContextPrompt_IncludesCustomTagsAndActiveRules()
    {
        var tags = new List<TagModel>
        {
            new() { Index = 1, Name = "DI0", Alias = "Nút nhấn khẩn E-Stop", Kind = TagKind.DiscreteInput },
            new() { Index = 9, Name = "DO0", Alias = "Động cơ băng tải", Kind = TagKind.DiscreteOutput }
        };

        var rules = new List<RuleItemModel>
        {
            new() { Index = 0, Id = "R0", Enabled = true, Narrative = "Khi DI0 sườn lên thì ngắt DO0" }
        };

        string prompt = AiPromptBuilder.BuildContextPrompt(tags, rules, "Bật còi khi nhấn nút");

        Assert.Contains("Nút nhấn khẩn E-Stop", prompt);
        Assert.Contains("Động cơ băng tải", prompt);
        Assert.Contains("Rule 0 (R0)", prompt);
        Assert.Contains("Bật còi khi nhấn nút", prompt);
    }

    [Fact]
    public void AiSettingsModel_DefaultConfiguration_HasCorrectDefaults()
    {
        var settings = new AiSettingsModel();

        Assert.Equal("gemini-3.8-flash", settings.ModelName);
        Assert.False(settings.IsConfigured);
        Assert.Equal(0.2, settings.Temperature);

        settings.ApiKey = "AIzaSyTestKey123";
        Assert.True(settings.IsConfigured);
    }

    [Fact]
    public void AiChatViewModel_InitialState_WelcomeMessageConfigured()
    {
        var vm = new AiChatViewModel(
            () => new List<TagModel>(),
            () => new List<RuleItemModel>());

        Assert.NotEmpty(vm.Messages);
        Assert.False(vm.IsPanelOpen);

        // Toggle panel
        vm.TogglePanel();
        Assert.True(vm.IsPanelOpen);

        vm.ClosePanel();
        Assert.False(vm.IsPanelOpen);
    }

    [Fact]
    public void AiRuleParser_ExtractRules_ExtractsMultipleRulesAndCleansProse()
    {
        string rawAiResponse = """
            Bài toán cần 2 Rule độc lập (1 Input ➔ 1 Output):

            🔹 **Rule 1: Bật bơm DO0**
            `[Input: DI0] ➔ [Trigger: ON_RISE (50ms)] ➔ [Guard: NONE] ➔ [Action: DO0 = SET_TAG(1)]`

            🔹 **Rule 2: Tắt bơm DO0**
            `[Input: DI1] ➔ [Trigger: ON_RISE (50ms)] ➔ [Guard: NONE] ➔ [Action: DO0 = SET_TAG(0)]`

            ```json:rules
            [
              {
                "narrative": "Bật bơm DO0 khi nhấn Start DI0",
                "input_tag": "DI0",
                "trigger": "ON_RISE",
                "for_ms": 50,
                "guard_tag": "NONE",
                "guard_op": "NONE",
                "guard_val": 0,
                "action_tag": "DO0",
                "action_type": "SET_TAG",
                "param": 1
              },
              {
                "narrative": "Tắt bơm DO0 khi nhấn Stop DI1",
                "input_tag": "DI1",
                "trigger": "ON_RISE",
                "for_ms": 50,
                "guard_tag": "NONE",
                "guard_op": "NONE",
                "guard_val": 0,
                "action_tag": "DO0",
                "action_type": "SET_TAG",
                "param": 0
              }
            ]
            ```
            """;

        var rules = AiRuleParser.ExtractRules(rawAiResponse, out string cleanContent);

        Assert.Equal(2, rules.Count);
        Assert.Equal("DI0", rules[0].InputTag);
        Assert.Equal("DO0", rules[0].ActionTag);
        Assert.Equal(1, rules[0].Param);
        Assert.Equal("DI1", rules[1].InputTag);
        Assert.Equal(0, rules[1].Param);

        Assert.DoesNotContain("```json", cleanContent);
        Assert.Contains("Bài toán cần 2 Rule độc lập", cleanContent);
    }

    [Fact]
    public void ChatMessageModel_ContentChanged_AutomaticallyExtractsRules()
    {
        var msg = new ChatMessageModel("assistant", "Đang phân tích...", isLoading: true);
        Assert.False(msg.HasExtractedRules);

        msg.Content = """
            Giải pháp đề xuất:
            ```json:rules
            [
              {
                "narrative": "Đếm sản lượng",
                "input_tag": "DI2",
                "trigger": "ON_RISE",
                "for_ms": 30,
                "action_tag": "VREG_RETAIN0",
                "action_type": "INC_COUNTER",
                "param": 1
              }
            ]
            ```
            """;

        Assert.True(msg.HasExtractedRules);
        Assert.Single(msg.ExtractedRules);
        Assert.Equal("VREG_RETAIN0", msg.ExtractedRules[0].ActionTag);
        Assert.Equal("✨ Nạp Rule vào Bảng Rule", msg.ApplyButtonText);
        Assert.DoesNotContain("```json", msg.DisplayContent);
    }

    [Fact]
    public void ApplyAiRulesToTable_AnalogOverheatRule_MapsThresholdToTriggerAndNullsGuard()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var message = new ChatMessageModel("assistant", "Đề xuất mạch bảo vệ quá nhiệt");

        // Mô phỏng trường hợp AI cũ phân loại nhầm AI0 GT 3000 vào Guard
        var specs = new List<AiRuleSpecModel>
        {
            new()
            {
                Narrative = "Ngắt tải DO0 khi nhiệt độ AI0 vượt 3000",
                InputTag = "AI0",
                Trigger = "ON_CHANGE",
                ForMs = 100,
                GuardTag = "AI0",
                GuardOp = "GT",
                GuardVal = 3000,
                ActionTag = "DO0",
                ActionType = "SET_TAG",
                Param = 0
            }
        };

        mainVM.ApplyAiRulesToTable(specs, message);

        Assert.NotEmpty(mainVM.RuleTableVM.Rules);
        var rule = mainVM.RuleTableVM.Rules[^1];

        // Xác nhận CompareOp & Threshold được gán chuẩn cho Trigger
        Assert.Equal("AI0", rule.TriggerTag?.Name);
        Assert.Equal(TriggerType.ON_CHANGE, rule.TriggerType);
        Assert.Equal(CompareOp.GT, rule.CompareOp);
        Assert.Equal(3000, rule.ThresholdLo);
        Assert.Equal(100u, rule.ForMs);

        // Guard phải là null vì AI0 là cảm biến Analog, không phải Boolean interlock!
        Assert.Null(rule.GuardTag);
        Assert.Equal("DO0", rule.ActionTag?.Name);
        Assert.Equal(0, rule.ActionParam);
    }

    [Fact]
    public void ReconstructGraphFromRule_AnalogOverheatRule_ProducesValid3NodeGraphWithoutCompilationErrors()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var tagCatalog = mainVM.TagCatalogVM;
        var tagAi0 = tagCatalog.AllTags.First(t => t.Name == "AI0");
        var tagDo0 = tagCatalog.AllTags.First(t => t.Name == "DO0");

        var rule = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            Enabled = true,
            TriggerTag = tagAi0,
            TriggerType = TriggerType.ON_CHANGE,
            CompareOp = CompareOp.GT,
            ThresholdLo = 3000,
            ForMs = 100,
            GuardTag = null,
            ActionTag = tagDo0,
            ActionType = ActionType.SET_TAG,
            ActionParam = 0
        };

        mainVM.LogicEditorVM.ReconstructGraphFromRule(rule);

        // 1. Phải có đúng 3 khối: Input (AI0), Trigger (> 3000, 100ms), Action (DO0 = 0)
        Assert.Equal(3, mainVM.LogicEditorVM.Nodes.Count);
        Assert.Contains(mainVM.LogicEditorVM.Nodes, n => n is InputNodeViewModel inp && inp.Tag?.Name == "AI0");
        Assert.Contains(mainVM.LogicEditorVM.Nodes, n => n is TriggerNodeViewModel trg && trg.CompareOp == CompareOp.GT && trg.ThresholdLo == 3000 && trg.ForMs == 100);
        Assert.Contains(mainVM.LogicEditorVM.Nodes, n => n is ActionNodeViewModel act && act.TargetTag?.Name == "DO0" && act.ActionParam == 0);

        // 2. Không có khối Guard thừa
        Assert.DoesNotContain(mainVM.LogicEditorVM.Nodes, n => n is GuardNodeViewModel);

        // 3. Phải có đúng 2 đường nối: Input -> Trigger, Trigger -> Action
        Assert.Equal(2, mainVM.LogicEditorVM.Connections.Count);

        // 4. Biên dịch đồ thị phải VALID 100%, không có lỗi, không có warning cổng nhận 2 kết nối
        Assert.Equal(CompileState.Valid, mainVM.LogicEditorVM.CompileState);
        Assert.True(string.IsNullOrEmpty(mainVM.LogicEditorVM.SimGateWarning));
        Assert.Equal(1, mainVM.LogicEditorVM.ValidRuleCount);
    }

    [Fact]
    public void ReconstructGraphFromRule_BooleanInterlockRule_ProducesValid4NodeGraph()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var tagCatalog = mainVM.TagCatalogVM;
        var tagDi0 = tagCatalog.AllTags.First(t => t.Name == "DI0");
        var tagDi1 = tagCatalog.AllTags.First(t => t.Name == "DI1");
        var tagDo0 = tagCatalog.AllTags.First(t => t.Name == "DO0");

        var rule = new RuleItemModel
        {
            Id = "R1",
            Index = 0,
            Enabled = true,
            TriggerTag = tagDi0,
            TriggerType = TriggerType.ON_RISE,
            ForMs = 50,
            GuardTag = tagDi1,
            GuardNegated = false,
            ActionTag = tagDo0,
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        };

        mainVM.LogicEditorVM.ReconstructGraphFromRule(rule);

        // Phải có đúng 4 khối: Input (DI0), Trigger (ON_RISE), Guard (DI1), Action (DO0)
        Assert.Equal(4, mainVM.LogicEditorVM.Nodes.Count);
        Assert.Equal(3, mainVM.LogicEditorVM.Connections.Count);

        var guardNode = mainVM.LogicEditorVM.Nodes.OfType<GuardNodeViewModel>().FirstOrDefault();
        Assert.NotNull(guardNode);
        Assert.Equal("DI1", guardNode.GuardTag?.Name);

        // Biên dịch đồ thị thành công
        Assert.Equal(CompileState.Valid, mainVM.LogicEditorVM.CompileState);
        Assert.True(string.IsNullOrEmpty(mainVM.LogicEditorVM.SimGateWarning));
    }

    [Fact]
    public void AiExplanationSynthesizer_StartStopPump_Produces3SectionExplanationAndJsonRules()
    {
        // Giả lập giao dịch đồ thị từ tool call (Start/Stop bơm DO0 với DI0 và DI1)
        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode { Id = "in_start", NodeType = "Input", TagName = "DI0", Label = "Nút nhấn Start" });
        tx.AddedNodes.Add(new DraftNode { Id = "trg_start", NodeType = "Trigger", TriggerType = "ON_RISE", DebounceMs = 50 });
        tx.AddedNodes.Add(new DraftNode { Id = "act_start", NodeType = "Action", TagName = "DO0", ActionType = "SET_TAG", ActionParam = 1, Label = "Bơm chính" });

        tx.AddedWires.Add(new DraftWire { SourceNodeId = "in_start", TargetNodeId = "trg_start" });
        tx.AddedWires.Add(new DraftWire { SourceNodeId = "trg_start", TargetNodeId = "act_start" });

        tx.AddedNodes.Add(new DraftNode { Id = "in_stop", NodeType = "Input", TagName = "DI1", Label = "Nút nhấn Stop" });
        tx.AddedNodes.Add(new DraftNode { Id = "trg_stop", NodeType = "Trigger", TriggerType = "ON_RISE", DebounceMs = 50 });
        tx.AddedNodes.Add(new DraftNode { Id = "act_stop", NodeType = "Action", TagName = "DO0", ActionType = "SET_TAG", ActionParam = 0, Label = "Bơm chính" });

        tx.AddedWires.Add(new DraftWire { SourceNodeId = "in_stop", TargetNodeId = "trg_stop" });
        tx.AddedWires.Add(new DraftWire { SourceNodeId = "trg_stop", TargetNodeId = "act_stop" });

        string synthesized = AiExplanationSynthesizer.Synthesize(tx, "Mạch Start/Stop bật tắt bơm DO0 với nút nhấn DI0 (Start) và DI1 (Stop)");

        // 1. Phải có cấu trúc chuẩn 3 phần công nghiệp
        Assert.Contains("1. Nguyên lý hoạt động", synthesized);
        Assert.Contains("2. Danh mục Tag", synthesized);
        Assert.Contains("3. Cơ chế an toàn công nghiệp", synthesized);

        // 2. Phải có danh mục tag cụ thể
        Assert.Contains("DI0", synthesized);
        Assert.Contains("DI1", synthesized);
        Assert.Contains("DO0", synthesized);

        // 3. Phải có khối JSON rules để tự động nạp
        Assert.Contains("```json:rules", synthesized);
        var rules = AiRuleParser.ExtractRules(synthesized, out _);
        Assert.Equal(2, rules.Count);
        Assert.Equal("DI0", rules[0].InputTag);
        Assert.Equal(1, rules[0].Param);
        Assert.Equal("DI1", rules[1].InputTag);
        Assert.Equal(0, rules[1].Param);
    }

    [Fact]
    public void AcceptAiProposal_WhenAccepted_CommitsRulesIntoRuleTableAndMarksOfficial()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var logicVM = mainVM.LogicEditorVM;

        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode { Id = "in1", NodeType = "Input", TagName = "DI0", PositionX = 100, PositionY = 100 });
        tx.AddedNodes.Add(new DraftNode { Id = "trg1", NodeType = "Trigger", TriggerType = "ON_RISE", PositionX = 340, PositionY = 100 });
        tx.AddedNodes.Add(new DraftNode { Id = "act1", NodeType = "Action", TagName = "DO0", ActionType = "SET_TAG", ActionParam = 1, PositionX = 580, PositionY = 100 });

        tx.AddedWires.Add(new DraftWire { SourceNodeId = "in1", SourcePort = "Out", TargetNodeId = "trg1", TargetPort = "In" });
        tx.AddedWires.Add(new DraftWire { SourceNodeId = "trg1", SourcePort = "Out", TargetNodeId = "act1", TargetPort = "In" });

        logicVM.ApplyAiProposalToCanvas(tx, "Test proposal");
        Assert.True(logicVM.HasPendingAiProposal);
        Assert.True(logicVM.Nodes.All(n => n.IsGhost));

        // Người dùng ấn Accept
        logicVM.AcceptAiProposal();

        // 1. Ghost phải chuyển thành official
        Assert.False(logicVM.HasPendingAiProposal);
        Assert.False(logicVM.Nodes.Any(n => n.IsGhost));

        // 2. Quan trọng nhất: Bảng Rule (RuleTableVM) PHẢI CÓ RULE ĐƯỢC BIÊN DỊCH VÀ NẠP VÀO!
        Assert.NotEmpty(mainVM.RuleTableVM.Rules);
        var rule = mainVM.RuleTableVM.Rules[0];
        Assert.Equal("DI0", rule.TriggerTag?.Name);
        Assert.Equal("DO0", rule.ActionTag?.Name);
        Assert.Equal(1, rule.ActionParam);
    }

    [Fact]
    public void AiChatViewModel_AcceptProposal_SetsAppliedStatusTextAndSyncsRules()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var chatVM = mainVM.AiChatVM;
        var logicVM = mainVM.LogicEditorVM;

        var message = new ChatMessageModel("assistant", "Đề xuất mạch Start/Stop");
        message.HasProposal = true;

        var tx = new DraftGraphTransaction();
        tx.AddedNodes.Add(new DraftNode { Id = "in1", NodeType = "Input", TagName = "DI0" });
        tx.AddedNodes.Add(new DraftNode { Id = "trg1", NodeType = "Trigger", TriggerType = "ON_RISE" });
        tx.AddedNodes.Add(new DraftNode { Id = "act1", NodeType = "Action", TagName = "DO0", ActionType = "SET_TAG", ActionParam = 1 });
        tx.AddedWires.Add(new DraftWire { SourceNodeId = "in1", TargetNodeId = "trg1" });
        tx.AddedWires.Add(new DraftWire { SourceNodeId = "trg1", TargetNodeId = "act1" });

        logicVM.ApplyAiProposalToCanvas(tx, "Test proposal");

        chatVM.AcceptProposal(message);

        // Đã áp dụng, thông báo trạng thái rõ ràng ngay trong tin nhắn
        Assert.True(message.IsApplied);
        Assert.False(message.IsRejected);
        Assert.Contains("Đã áp dụng thành công", message.AppliedStatusText);
        Assert.Contains("Rule vào Bảng Rule", message.AppliedStatusText);
        Assert.NotEmpty(mainVM.RuleTableVM.Rules);
    }
}
