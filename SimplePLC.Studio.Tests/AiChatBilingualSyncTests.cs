using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho tính năng Đồng bộ Song ngữ (Bilingual Synchronization):
/// 1. Tự động nhận diện ngôn ngữ người dùng (Language Mirroring: English vs Tiếng Việt).
/// 2. Cấu hình quyền ưu tiên ngôn ngữ (Auto, English, Vietnamese, FollowApp).
/// 3. Sinh System Instruction và Project Context Envelopes theo đúng ngôn ngữ tương ứng.
/// 4. Đảm bảo từ điển song ngữ trong LocalizationService đầy đủ các key giao diện AI Copilot.
/// 5. Phản ứng thời gian thực của ViewModel (chips, context badges, proposal status) khi chuyển đổi ngôn ngữ.
/// </summary>
public class AiChatBilingualSyncTests
{
    private static void RunOnSta(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        var thread = new Thread(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        tcs.Task.GetAwaiter().GetResult();
    }

    [Fact]
    public void DetectIsEnglish_WithExplicitSettings_HonorsUserPreference()
    {
        RunOnSta(() =>
        {
            var settings = new AiSettingsModel();
            var vm = new AiChatViewModel(
                () => Enumerable.Empty<TagModel>(),
                () => Enumerable.Empty<RuleItemModel>(),
                () => null,
                settings);

            // 1. Explicit English setting
            settings.ResponseLanguage = "English";
            Assert.True(vm.DetectIsEnglish("Mạch Start/Stop bật tắt bơm DO0"));
            Assert.True(vm.DetectIsEnglish("Create motor circuit"));

            // 2. Explicit Vietnamese setting
            settings.ResponseLanguage = "Vietnamese";
            Assert.False(vm.DetectIsEnglish("Create a Start/Stop circuit"));
            Assert.False(vm.DetectIsEnglish("Mạch Start/Stop"));

            // 3. Follow App Language setting
            settings.ResponseLanguage = "FollowApp";
            LocalizationService.Instance.CurrentLanguage = "en";
            Assert.True(vm.DetectIsEnglish("Mạch Start/Stop"));

            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.False(vm.DetectIsEnglish("Create motor circuit"));
        });
    }

    [Theory]
    [InlineData("Mạch Start/Stop bật tắt bơm DO0 với nút nhấn DI0 và DI1", false)]
    [InlineData("giup toi tao mach khoi dong dong co DO1", false)]
    [InlineData("giai thich giup toi tai sao can loc nhieu debounce 50ms", false)]
    [InlineData("kiem tra an toan so do", false)]
    [InlineData("Create a Start/Stop circuit for pump DO0 with pushbuttons DI0 (Start) and DI1 (Stop)", true)]
    [InlineData("Design motor control circuit for DO1 when DI0 is pressed", true)]
    [InlineData("Explain why we need a debounce filter for mechanical contact buttons", true)]
    [InlineData("How does the TON timer macro work in SimplePLC?", true)]
    public void DetectIsEnglish_WithAutoDetect_DetectsLanguageAccurately(string prompt, bool expectedIsEnglish)
    {
        RunOnSta(() =>
        {
            var settings = new AiSettingsModel { ResponseLanguage = "Auto" };
            var vm = new AiChatViewModel(
                () => Enumerable.Empty<TagModel>(),
                () => Enumerable.Empty<RuleItemModel>(),
                () => null,
                settings);

            bool isEnglish = vm.DetectIsEnglish(prompt);
            Assert.Equal(expectedIsEnglish, isEnglish);
        });
    }

    [Fact]
    public void AiPromptBuilder_BuildSystemInstruction_GeneratesEnglishTemplateWhenRequested()
    {
        // Act: English synthesis prompt
        string enPrompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis, isEnglish: true);
        string viPrompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis, isEnglish: false);

        // Assert EN
        Assert.Contains("You are SynaptiX Industrial PLC AI Assistant", enPrompt);
        Assert.Contains("1 INPUT ➔ 1 OUTPUT (1 RULE = 1 ACTION)", enPrompt);
        Assert.Contains("Operating Principle & Pipeline Diagram", enPrompt);
        Assert.Contains("Tag Catalog & Hardware Configuration", enPrompt);
        Assert.Contains("Industrial Safety & Fail-Safe Mechanisms", enPrompt);
        Assert.Contains("json:rules", enPrompt);

        // Assert VI
        Assert.Contains("Bạn là SynaptiX Industrial PLC AI Assistant", viPrompt);
        Assert.Contains("Nguyên lý hoạt động & Sơ đồ Hộp Pipeline", viPrompt);
        Assert.Contains("Danh mục Tag & Cấu hình thiết bị", viPrompt);
        Assert.Contains("Cơ chế an toàn công nghiệp (Fail-Safe)", viPrompt);
    }

    [Fact]
    public void AiPromptBuilder_BuildConsultativeInstruction_SupportsBilingual()
    {
        string enPrompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.ConsultativeQnA, isEnglish: true);
        string viPrompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.ConsultativeQnA, isEnglish: false);

        Assert.Contains("You are SynaptiX Senior Industrial Automation Consultant", enPrompt);
        Assert.Contains("On-Demand Explanation", enPrompt);
        Assert.Contains("Debounce Filter", enPrompt);

        Assert.Contains("Bạn là SynaptiX Senior Industrial Automation Consultant", viPrompt);
        Assert.Contains("Lọc rung phím (Debounce 30–50ms)", viPrompt);
    }

    [Fact]
    public void AiPromptBuilder_BuildContextPrompt_BilingualEnvelopes()
    {
        var tags = new List<TagModel>
        {
            new() { Name = "DI0", Alias = "BTN_START", Kind = TagKind.DiscreteInput, Channel = 0 }
        };
        var rules = new List<RuleItemModel>
        {
            new() { Index = 0, Narrative = "Start pump", Enabled = true }
        };

        // Act
        string enContext = AiPromptBuilder.BuildContextPrompt(tags, rules, "Build circuit", isEnglish: true);
        string viContext = AiPromptBuilder.BuildContextPrompt(tags, rules, "Tạo mạch", isEnglish: false);

        // Assert EN
        Assert.Contains("=== CURRENT PROJECT CONTEXT DATA (SYNAPTIX CONTEXT) ===", enContext);
        Assert.Contains("[Configured Tags in Project]:", enContext);
        Assert.Contains("Engineer Request: Build circuit", enContext);

        // Assert VI
        Assert.Contains("=== DỮ LIỆU NGỮ CẢNH DỰ ÁN HIỆN TẠI (SYNAPTIX CONTEXT) ===", viContext);
        Assert.Contains("[Danh bạ Tag đã được người dùng đặt tên trong dự án]:", viContext);
        Assert.Contains("Yêu cầu của người dùng: Tạo mạch", viContext);
    }

    [Fact]
    public void LocalizationService_ContainsAllRequiredAiChatKeys_InBothLanguages()
    {
        var loc = LocalizationService.Instance;
        var requiredKeys = new[]
        {
            "AiChatTitle",
            "AiChatWatermark",
            "AiChatBtnApply",
            "AiChatBtnReject",
            "AiChatBtnRejected",
            "AiChatAppliedSuccess",
            "AiChatCopySuggestion",
            "AiChatChipStartStop",
            "AiChatChipCounter",
            "AiChatChipScale",
            "AiChatChipSafetyDelay",
            "AiChatChipSafetyCheck",
            "AiChatChipOptimize",
            "AiChatSettingsTitle",
            "AiChatSettingsProvider",
            "AiChatSettingsModel",
            "AiChatSettingsResponseLang",
            "AiChatSettingsSave",
            "AiChatTooltipSettings",
            "AiChatTooltipClear",
            "AiChatTooltipCollapse",
            "AiChatTooltipSend",
            "AiChatTooltipCancel",
            "AiChatStatusReady",
            "AiChatStatusValid",
            "AiChatStatusError",
            "AiChatStatusWarning",
            "AiChatProposalSummary",
            "AiChatBlocksAndWires",
            "AiChatTagsCount"
        };

        foreach (var key in requiredKeys)
        {
            string viVal = loc.GetVi(key);
            string enVal = loc.GetEn(key);

            Assert.NotEqual(key, viVal);
            Assert.False(string.IsNullOrWhiteSpace(viVal), $"Missing Vietnamese localization for {key}");

            Assert.NotEqual(key, enVal);
            Assert.False(string.IsNullOrWhiteSpace(enVal), $"Missing English localization for {key}");
        }
    }

    [Fact]
    public void AiChatViewModel_OnLanguageChanged_DynamicallyUpdatesChipsAndContextText()
    {
        RunOnSta(() =>
        {
            var settings = new AiSettingsModel { GeminiApiKey = "test-key" };
            var vm = new AiChatViewModel(
                () => Enumerable.Empty<TagModel>(),
                () => Enumerable.Empty<RuleItemModel>(),
                () => null,
                settings);

            // 1. Switch to English
            LocalizationService.Instance.CurrentLanguage = "en";

            Assert.Contains("0 blocks", vm.ActiveContextNodeCountText);
            Assert.Contains("0 Tags", vm.ActiveContextTagCountText);
            Assert.Contains(vm.SuggestionChips, c => c.DisplayText.Contains("Start / Stop Circuit"));
            Assert.Contains(vm.SuggestionChips, c => c.DisplayText.Contains("Production Counter"));

            // 2. Switch to Vietnamese
            LocalizationService.Instance.CurrentLanguage = "vi";

            Assert.Contains("0 khối", vm.ActiveContextNodeCountText);
            Assert.Contains("0 Tags", vm.ActiveContextTagCountText);
            Assert.Contains(vm.SuggestionChips, c => c.DisplayText.Contains("Mạch Start / Stop"));
            Assert.Contains(vm.SuggestionChips, c => c.DisplayText.Contains("Đếm sản lượng"));
        });
    }

    [Fact]
    public async Task AiChatViewModel_SendMessage_EnrichesPromptWithEnglishEnvelope_WhenPromptIsEnglish()
    {
        var settings = new AiSettingsModel
        {
            Provider = "Gemini",
            GeminiApiKey = "test-key",
            ResponseLanguage = "Auto"
        };

        string capturedSys = string.Empty;
        List<(string role, string text)> capturedHistory = new();

        AiChatViewModel? vm = null;
        RunOnSta(() =>
        {
            vm = new AiChatViewModel(
                () => Enumerable.Empty<TagModel>(),
                () => Enumerable.Empty<RuleItemModel>(),
                () => null,
                settings);

            vm.ApiHandler = (cfg, sys, history, tools, ct) =>
            {
                capturedSys = sys;
                capturedHistory = new List<(string role, string text)>(history);
                return Task.FromResult(new GeminiToolResponse(
                    "Operating principle:\nTurn on pump DO0 when DI0 is pressed.",
                    new List<AiToolCall>()));
            };

            vm.InputText = "Create a Start/Stop circuit for pump DO0 with pushbuttons DI0 (Start) and DI1 (Stop)";
        });

        Assert.NotNull(vm);
        await vm.SendMessageAsync();

        // Assert: System prompt is in English
        Assert.Contains("You are SynaptiX Industrial PLC AI Assistant", capturedSys);

        // Assert: Enriched prompt uses English context headers
        var lastUserMsg = capturedHistory.Last(h => h.role == "user");
        Assert.Contains("[PROJECT CONTEXT DATA]", lastUserMsg.text);
        Assert.Contains("Engineer Request:", lastUserMsg.text);
    }

    [Fact]
    public void DraftNodeGeneration_WhenAppIsEnglishAndUserChatsVietnamese_ProducesEnglishNodeLabels()
    {
        var settings = new AiSettingsModel
        {
            Provider = "Groq",
            GroqApiKey = "test-key",
            ResponseLanguage = "Auto"
        };

        DraftGraphTransaction? capturedTx = null;

        RunOnSta(() =>
        {
            LocalizationService.Instance.CurrentLanguage = "en-US";

            var tagCatalog = new TagCatalogViewModel();
            var logicVM = new LogicEditorViewModel(tagCatalog);
            var vm = new AiChatViewModel(
                () => tagCatalog.AllTags,
                () => Enumerable.Empty<RuleItemModel>(),
                () => logicVM,
                settings);

            vm.ApiHandler = (cfg, sys, history, tools, ct) =>
            {
                string jsonResponse = """
                ```json:rules
                [
                  {
                    "narrative": "Bật DO0 lúc 12:00 trưa theo lịch RTC",
                    "input_tag": "NONE",
                    "trigger": "TIME_WINDOW",
                    "compare_op": "NONE",
                    "threshold_lo": 1200,
                    "threshold_hi": 1200,
                    "for_ms": 0,
                    "guard_tag": "NONE",
                    "guard_negated": false,
                    "action_tag": "DO0",
                    "action_type": "SET_TAG",
                    "param": 1
                  }
                ]
                ```
                """;
                return Task.FromResult(new GeminiToolResponse(jsonResponse, new List<AiToolCall>()));
            };

            vm.InputText = "sinh rule này trên canva cho tôi";
            vm.SendMessageCommand.Execute(null);

            capturedTx = logicVM.CurrentAiTransaction;
        });

        Assert.NotNull(capturedTx);
        Assert.Equal(2, capturedTx.AddedNodes.Count);

        var trgNode = capturedTx.AddedNodes.First(n => n.NodeType == "Trigger");
        var actNode = capturedTx.AddedNodes.First(n => n.NodeType == "Action");

        // Khi App để tiếng Anh (en-US), dù chat bằng tiếng Việt, nhãn node Canvas vẫn phải là tiếng Anh
        Assert.Contains("Time Window (12:00)", trgNode.Label);
        Assert.Contains("Action (DO0)", actNode.Label);
        Assert.DoesNotContain("Khung giờ", trgNode.Label);
        Assert.DoesNotContain("Tác vụ", actNode.Label);
    }

    [Fact]
    public void DraftNodeGeneration_WhenAppIsVietnameseAndUserChatsEnglish_ProducesVietnameseNodeLabels()
    {
        var settings = new AiSettingsModel
        {
            Provider = "Groq",
            GroqApiKey = "test-key",
            ResponseLanguage = "Auto"
        };

        DraftGraphTransaction? capturedTx = null;

        RunOnSta(() =>
        {
            LocalizationService.Instance.CurrentLanguage = "vi-VN";

            var tagCatalog = new TagCatalogViewModel();
            var logicVM = new LogicEditorViewModel(tagCatalog);
            var vm = new AiChatViewModel(
                () => tagCatalog.AllTags,
                () => Enumerable.Empty<RuleItemModel>(),
                () => logicVM,
                settings);

            vm.ApiHandler = (cfg, sys, history, tools, ct) =>
            {
                string jsonResponse = """
                ```json:rules
                [
                  {
                    "narrative": "Start DO0 at 12:00 via RTC schedule",
                    "input_tag": "NONE",
                    "trigger": "TIME_WINDOW",
                    "compare_op": "NONE",
                    "threshold_lo": 1200,
                    "threshold_hi": 1200,
                    "for_ms": 0,
                    "guard_tag": "NONE",
                    "guard_negated": false,
                    "action_tag": "DO0",
                    "action_type": "SET_TAG",
                    "param": 1
                  }
                ]
                ```
                """;
                return Task.FromResult(new GeminiToolResponse(jsonResponse, new List<AiToolCall>()));
            };

            vm.InputText = "generate circuit on canvas for me";
            vm.SendMessageCommand.Execute(null);

            capturedTx = logicVM.CurrentAiTransaction;
        });

        Assert.NotNull(capturedTx);
        Assert.Equal(2, capturedTx.AddedNodes.Count);

        var trgNode = capturedTx.AddedNodes.First(n => n.NodeType == "Trigger");
        var actNode = capturedTx.AddedNodes.First(n => n.NodeType == "Action");

        // Khi App để tiếng Việt (vi-VN), dù chat bằng tiếng Anh, nhãn node Canvas vẫn là tiếng Việt
        Assert.Contains("Khung giờ (12:00)", trgNode.Label);
        Assert.Contains("Tác vụ (DO0)", actNode.Label);
        Assert.DoesNotContain("Time Window", trgNode.Label);
        Assert.DoesNotContain("Action (DO0)", actNode.Label);
    }

    [Fact]
    public void GraphNode_OnLanguageChanged_DynamicallyTranslatesAiNodeLabels()
    {
        RunOnSta(() =>
        {
            LocalizationService.Instance.CurrentLanguage = "en-US";
            var trg = new TriggerNodeViewModel { CustomLabel = "Time Window (12:00)" };
            var act = new ActionNodeViewModel { CustomLabel = "Action (DO0)" };
            var cmp = new ComparatorNodeViewModel { CustomLabel = "CMP (GT 3000)" };

            Assert.Equal("Time Window (12:00)", trg.DisplayTitle);
            Assert.Equal("Action (DO0)", act.DisplayTitle);
            Assert.Equal("CMP (GT 3000)", cmp.DisplayTitle);

            // Chuyển sang Tiếng Việt -> Tự động dịch nhãn AI trên Canvas
            LocalizationService.Instance.CurrentLanguage = "vi-VN";
            Assert.Equal("Khung giờ (12:00)", trg.DisplayTitle);
            Assert.Equal("Tác vụ (DO0)", act.DisplayTitle);
            Assert.Equal("So sánh (GT 3000)", cmp.DisplayTitle);

            // Chuyển lại Tiếng Anh -> Tự động dịch ngược lại
            LocalizationService.Instance.CurrentLanguage = "en-US";
            Assert.Equal("Time Window (12:00)", trg.DisplayTitle);
            Assert.Equal("Action (DO0)", act.DisplayTitle);
            Assert.Equal("CMP (GT 3000)", cmp.DisplayTitle);
        });
    }

    [Fact]
    public void RuleTable_NarrativeGeneration_FollowsAppLanguage()
    {
        var rule = new RuleItemModel
        {
            Index = 0,
            Id = "R1",
            TriggerType = TriggerType.TIME_WINDOW,
            ThresholdLo = 1200,
            ThresholdHi = 1200,
            ActionTag = new TagModel { Name = "DO0" },
            ActionType = ActionType.SET_TAG,
            ActionParam = 1
        };

        // App Language = English
        LocalizationService.Instance.CurrentLanguage = "en-US";
        rule.UpdateNarrative();
        Assert.StartsWith("During shift", rule.Narrative);
        Assert.Contains("THEN set", rule.Narrative);

        // App Language = Vietnamese
        LocalizationService.Instance.CurrentLanguage = "vi-VN";
        rule.UpdateNarrative();
        Assert.StartsWith("Trong ca", rule.Narrative);
        Assert.Contains("THÌ đặt", rule.Narrative);
    }

    [Fact]
    public void MultiTurnAiRuleGeneration_ConsecutiveRules_HaveDistinctUniqueIdsAndDoNotCrash()
    {
        var settings = new AiSettingsModel
        {
            Provider = "Groq",
            GroqApiKey = "test-key",
            ResponseLanguage = "Auto"
        };

        RunOnSta(() =>
        {
            var tagCatalog = new TagCatalogViewModel();
            var logicVM = new LogicEditorViewModel(tagCatalog);
            var vm = new AiChatViewModel(
                () => tagCatalog.AllTags,
                () => Enumerable.Empty<RuleItemModel>(),
                () => logicVM,
                settings);

            int callCount = 0;
            vm.ApiHandler = (cfg, sys, history, tools, ct) =>
            {
                callCount++;
                string jsonResponse = callCount == 1
                    ? """
                    ```json:rules
                    [
                      {
                        "narrative": "Rule 1: Bật DO0 lúc 12:00",
                        "input_tag": "NONE",
                        "trigger": "TIME_WINDOW",
                        "threshold_lo": 1200,
                        "threshold_hi": 1200,
                        "guard_tag": "NONE",
                        "action_tag": "DO0",
                        "action_type": "SET_TAG",
                        "param": 1
                      }
                    ]
                    ```
                    """
                    : """
                    ```json:rules
                    [
                      {
                        "narrative": "Rule 2: Tắt DO0 lúc 18:00",
                        "input_tag": "NONE",
                        "trigger": "TIME_WINDOW",
                        "threshold_lo": 1800,
                        "threshold_hi": 1800,
                        "guard_tag": "NONE",
                        "action_tag": "DO0",
                        "action_type": "SET_TAG",
                        "param": 0
                      }
                    ]
                    ```
                    """;
                return Task.FromResult(new GeminiToolResponse(jsonResponse, new List<AiToolCall>()));
            };

            // Turn 1: Tạo rule 1
            vm.InputText = "create circuit: bật đèn lúc 12h";
            vm.SendMessageCommand.Execute(null);

            var tx1 = logicVM.CurrentAiTransaction;
            Assert.NotNull(tx1);
            Assert.Equal(2, tx1.AddedNodes.Count);

            // Chấp nhận rule 1
            var msg1 = vm.Messages.LastOrDefault(m => m.HasProposal);
            Assert.NotNull(msg1);
            vm.AcceptProposal(msg1);

            Assert.Equal(2, logicVM.Nodes.Count);
            Assert.All(logicVM.Nodes, n => Assert.False(n.IsGhost));

            // Turn 2: Yêu cầu sinh tiếp rule 2
            vm.InputText = "create circuit: tắt đèn lúc 18h";
            vm.SendMessageCommand.Execute(null);

            var tx2 = logicVM.CurrentAiTransaction;
            Assert.NotNull(tx2);
            Assert.Equal(2, tx2.AddedNodes.Count);

            // Chấp nhận rule 2 - Phải thành công tuyệt đối, KHÔNG crash vì trùng ID
            var msg2 = vm.Messages.LastOrDefault(m => m.HasProposal);
            Assert.NotNull(msg2);
            vm.AcceptProposal(msg2);

            Assert.Equal(4, logicVM.Nodes.Count);
            Assert.All(logicVM.Nodes, n => Assert.False(n.IsGhost));

            // Khẳng định 100% các node ID trên Canvas là duy nhất
            var allIds = logicVM.Nodes.Select(n => n.Id).ToList();
            Assert.Equal(allIds.Count, allIds.Distinct().Count());

            // Khẳng định Auto-layout: Vị trí Y của Rule 2 nằm bên dưới Rule 1
            var r1Nodes = logicVM.Nodes.Take(2).ToList();
            var r2Nodes = logicVM.Nodes.Skip(2).Take(2).ToList();
            double r1MaxY = r1Nodes.Max(n => n.Location.Y);
            double r2MinY = r2Nodes.Min(n => n.Location.Y);
            Assert.True(r2MinY > r1MaxY, $"Rule 2 Y ({r2MinY}) must be below Rule 1 Y ({r1MaxY})");
        });
    }

    [Fact]
    public void ApplyAiProposalToCanvas_WhenDraftNodesHaveCollidingIds_DefensivelyRemapsIdsAndPreservesWires()
    {
        RunOnSta(() =>
        {
            var tagCatalog = new TagCatalogViewModel();
            var logicVM = new LogicEditorViewModel(tagCatalog);

            // Cho sẵn 2 node có ID cố định trên Canvas
            var existingNode1 = new TriggerNodeViewModel { Id = "node_trg_0" };
            var existingNode2 = new ActionNodeViewModel { Id = "node_act_0" };
            logicVM.Nodes.Add(existingNode1);
            logicVM.Nodes.Add(existingNode2);

            // Đề xuất AI cố tình chứa ID trùng lặp với Canvas
            var collidingTx = new DraftGraphTransaction();
            collidingTx.AddedNodes.Add(new DraftNode { Id = "node_trg_0", NodeType = "Trigger", TriggerType = "TIME_WINDOW" });
            collidingTx.AddedNodes.Add(new DraftNode { Id = "node_act_0", NodeType = "Action", ActionType = "SET_TAG", TagName = "DO0" });
            collidingTx.AddedWires.Add(new DraftWire { SourceNodeId = "node_trg_0", SourcePort = "Out", TargetNodeId = "node_act_0", TargetPort = "In" });

            // Apply lên canvas -> Phòng thủ tầng sâu tự động remap ID
            logicVM.ApplyAiProposalToCanvas(collidingTx, "Test proposal with collision");

            Assert.Equal(4, logicVM.Nodes.Count);
            var distinctIds = logicVM.Nodes.Select(n => n.Id).Distinct().Count();
            Assert.Equal(4, distinctIds);

            // Xác nhận dây nối mới trỏ đúng vào các ID đã được remap
            var ghostConn = logicVM.Connections.FirstOrDefault(c => c.IsGhost);
            Assert.NotNull(ghostConn);
            Assert.NotEqual("node_trg_0", ghostConn.Source?.Node?.Id);
            Assert.NotEqual("node_act_0", ghostConn.Target?.Node?.Id);
            Assert.Equal(ghostConn.Source?.Node?.Id, collidingTx.AddedWires[0].SourceNodeId);
            Assert.Equal(ghostConn.Target?.Node?.Id, collidingTx.AddedWires[0].TargetNodeId);
        });
    }

    [Fact]
    public void ApplyAiRulesToTable_WhenInvoked_DoesNotSwitchAwayFromLogicGraphTab()
    {
        RunOnSta(() =>
        {
            var mainVM = new MainViewModel(uiDispatcher: a => a());
            mainVM.SelectTab(AppTabIndices.LogicGraph);
            Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);

            var spec = new AiRuleSpecModel
            {
                InputTag = "NONE",
                Trigger = "TIME_WINDOW",
                ThresholdLo = 1200,
                ThresholdHi = 1200,
                ActionTag = "DO0",
                ActionType = "SET_TAG",
                Param = 1,
                Narrative = "Rule test"
            };

            var msg = new ChatMessageModel("assistant", "Proposal");
            mainVM.ApplyAiRulesToTable(new List<AiRuleSpecModel> { spec }, msg);

            // Khẳng định tab vẫn giữ ở LogicGraph, KHÔNG bị nhảy sang RuleTable
            Assert.Equal(AppTabIndices.LogicGraph, mainVM.SelectedTabIndex);
        });
    }

    [Fact]
    public void AiPromptBuilder_BuildSystemInstruction_WhenAppIsEnglish_EnforcesEnglishToolLabels()
    {
        // Act: Kỹ sư chat bằng Tiếng Việt (isEnglish=false) nhưng App đang ở chế độ Tiếng Anh (isAppEnglish=true)
        string prompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis, isEnglish: false, isAppEnglish: true);

        // Assert
        Assert.Contains("App Presentation Language: English", prompt);
        Assert.Contains("custom_label", prompt);
        Assert.Contains("PHẢI VIẾT BẰNG TIẾNG ANH CHUẨN CÔNG NGHIỆP", prompt);
    }

    [Fact]
    public async Task AiChatViewModel_GeminiToolCalling_WhenAppIsEnglish_NormalizesVietnameseCustomLabels()
    {
        await Task.Run(() =>
        {
            RunOnSta(async () =>
            {
                // Arrange: Giao diện phần mềm đang là Tiếng Anh
                LocalizationService.Instance.CurrentLanguage = "en";

                var logicVM = new LogicEditorViewModel(new TagCatalogViewModel());
                var tags = new List<TagModel>
                {
                    new() { Name = "DI2", DataType = TagDataType.Boolean, Alias = "Alarm Sensor" },
                    new() { Name = "DO2", DataType = TagDataType.Boolean, Alias = "Door Actuator" }
                };

                var settings = new AiSettingsModel
                {
                    Provider = "Gemini",
                    GeminiApiKey = "test-gemini-key",
                    ModelName = "gemini-3.8-flash"
                };

                var chatVM = new AiChatViewModel(
                    () => tags,
                    () => Enumerable.Empty<RuleItemModel>(),
                    () => logicVM,
                    settings);

                // Giả lập Gemini gọi tool call với nhãn tiếng Việt lọt vào
                chatVM.ApiHandler = (st, sys, hist, tools, ct) =>
                {
                    var toolCalls = new List<AiToolCall>
                    {
                        new("add_node", new JsonObject
                        {
                            ["node_id"] = "node_in_test",
                            ["node_type"] = "INPUT",
                            ["custom_label"] = "Cảm Biến Báo Động",
                            ["position_x"] = 60,
                            ["position_y"] = 100,
                            ["tag_name"] = "DI2",
                            ["trigger_type"] = "NONE",
                            ["compare_op"] = "NONE",
                            ["threshold_lo"] = 0,
                            ["threshold_hi"] = 0,
                            ["debounce_ms"] = 0,
                            ["action_type"] = "NONE",
                            ["action_param"] = 0
                        }),
                        new("add_node", new JsonObject
                        {
                            ["node_id"] = "node_act_test",
                            ["node_type"] = "ACTION",
                            ["custom_label"] = "Kích hoạt Đóng Cửa",
                            ["position_x"] = 300,
                            ["position_y"] = 100,
                            ["tag_name"] = "DO2",
                            ["trigger_type"] = "NONE",
                            ["compare_op"] = "NONE",
                            ["threshold_lo"] = 0,
                            ["threshold_hi"] = 0,
                            ["debounce_ms"] = 0,
                            ["action_type"] = "SET_TAG",
                            ["action_param"] = 1
                        })
                    };

                    return Task.FromResult(new GeminiToolResponse("Đã tạo mạch điều khiển cửa.", toolCalls));
                };

                // Act
                chatVM.InputText = "tiến hành tạo rule khi mà cảm biến kêu thì đóng cửa";
                await chatVM.SendMessageCommand.ExecuteAsync(null);

                // Assert: Các khối Ghost trên Canvas phải được chuẩn hóa sang tiếng Anh công nghiệp
                Assert.Equal(2, logicVM.Nodes.Count);
                var inputNode = logicVM.Nodes.FirstOrDefault(n => n.Id == "node_in_test");
                var actionNode = logicVM.Nodes.FirstOrDefault(n => n.Id == "node_act_test");

                Assert.NotNull(inputNode);
                Assert.NotNull(actionNode);

                Assert.Equal("Input (DI2)", inputNode.CustomLabel);
                Assert.Equal("Turn ON (DO2)", actionNode.CustomLabel);
            });
        });
    }

    [Fact]
    public void GraphNodeViewModel_TryTranslateAiCustomLabel_TranslatesBilingualBidirectionally()
    {
        RunOnSta(() =>
        {
            // Đảm bảo trạng thái ban đầu là English
            LocalizationService.Instance.CurrentLanguage = "en";

            var node = new TriggerNodeViewModel
            {
                CustomLabel = "Rising Edge (50ms)"
            };

            // Switch to Vietnamese
            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.Equal("Sườn lên (50ms)", node.CustomLabel);

            // Switch back to English
            LocalizationService.Instance.CurrentLanguage = "en";
            Assert.Equal("Rising Edge (50ms)", node.CustomLabel);

            // Test Action node
            var actNode = new ActionNodeViewModel
            {
                CustomLabel = "Turn ON (DO0)"
            };
            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.Equal("Bật (DO0)", actNode.CustomLabel);

            LocalizationService.Instance.CurrentLanguage = "en";
            Assert.Equal("Turn ON (DO0)", actNode.CustomLabel);
        });
    }
}
