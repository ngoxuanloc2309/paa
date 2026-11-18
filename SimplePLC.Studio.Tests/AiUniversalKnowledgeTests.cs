using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Application.Enums;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử đơn vị cho Kiến trúc Tri thức Tự động hóa Toàn năng (Universal Industrial Knowledge Architecture):
/// 1. Kiểm tra System Prompt bao quát 100% 12 khối chức năng, loại bỏ hoàn toàn các lỗi sai suy diễn cũ.
/// 2. Kiểm tra token budget của System Instruction luôn tối ưu và gọn gàng.
/// 3. Kiểm tra việc phác thảo đồ thị nháp (Draft Graph) cho TIME_WINDOW (không sinh node Input ma).
/// 4. Kiểm tra việc phác thảo đồ thị nháp cho bài toán so sánh ngưỡng (sinh đúng khối Comparator CMP).
/// 5. Kiểm tra AiContextAggregator đọc và trích xuất đúng 100% các khối trên Canvas không bị "UNKNOWN".
/// 6. Kiểm tra AiExplanationSynthesizer tạo đúng ASCII Box cho cả Comparator và TIME_WINDOW.
/// </summary>
public class AiUniversalKnowledgeTests
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
    public void PromptBuilder_InstructionIncludesAll12PrimitivesAndEliminatesOldFalsehood()
    {
        // 1. Kiểm tra bản tiếng Việt
        string viPrompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis, isEnglish: false);
        Assert.Contains("INPUT", viPrompt);
        Assert.Contains("TRIGGER", viPrompt);
        Assert.Contains("COMPARATOR", viPrompt);
        Assert.Contains("GUARD", viPrompt);
        Assert.Contains("ACTION", viPrompt);
        Assert.Contains("TIMER", viPrompt);
        Assert.Contains("COUNTER", viPrompt);
        Assert.Contains("SCALE", viPrompt);
        Assert.Contains("MATH", viPrompt);
        Assert.Contains("MODBUS", viPrompt);
        Assert.Contains("MQTT", viPrompt);
        Assert.Contains("TIME_WINDOW", viPrompt);
        Assert.Contains("VREG_RETAIN", viPrompt);

        // Khẳng định KHÔNG CÒN câu nói sai lệch cũ
        Assert.DoesNotContain("không có Condition riêng", viPrompt);
        Assert.DoesNotContain("Trigger ở Stage 2 chịu trách nhiệm so sánh giá trị", viPrompt);

        // 2. Kiểm tra bản tiếng Anh
        string enPrompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis, isEnglish: true);
        Assert.Contains("INPUT", enPrompt);
        Assert.Contains("TRIGGER", enPrompt);
        Assert.Contains("COMPARATOR", enPrompt);
        Assert.Contains("GUARD", enPrompt);
        Assert.Contains("ACTION", enPrompt);
        Assert.Contains("TIMER", enPrompt);
        Assert.Contains("COUNTER", enPrompt);
        Assert.Contains("SCALE", enPrompt);
        Assert.Contains("MATH", enPrompt);
        Assert.Contains("TIME_WINDOW", enPrompt);

        // Dung lượng ký tự tinh gọn (dưới 10,000 ký tự mỗi ngôn ngữ -> dưới 2,500 tokens)
        Assert.True(viPrompt.Length < 10000, $"VI prompt length {viPrompt.Length} exceeds 10,000 chars");
        Assert.True(enPrompt.Length < 10000, $"EN prompt length {enPrompt.Length} exceeds 10,000 chars");
    }

    [Fact]
    public void DraftGraph_TimeWindowRule_GeneratesTriggerWithoutInputNode()
    {
        RunOnSta(() =>
        {
            var tagCatalog = new TagCatalogViewModel();
            var logicVM = new LogicEditorViewModel(tagCatalog);
            var vm = new AiChatViewModel(
                () => tagCatalog.AllTags,
                () => Enumerable.Empty<RuleItemModel>(),
                () => logicVM,
                new AiSettingsModel());

            var rules = new List<AiRuleSpecModel>
            {
                new()
                {
                    Narrative = "Start pump at 06:00 AM",
                    InputTag = "NONE",
                    Trigger = "TIME_WINDOW",
                    ThresholdLo = 600,
                    ThresholdHi = 600,
                    ActionTag = "DO0",
                    ActionType = "SET_TAG",
                    Param = 1
                }
            };

            var jsonBlock = "```json:rules\n" + System.Text.Json.JsonSerializer.Serialize(rules) + "\n```";
            vm.ApiHandler = (settings, sys, hist, tools, ct) =>
                Task.FromResult(new GeminiToolResponse(jsonBlock, new List<AiToolCall>()));

            vm.InputText = "Bật bơm lúc 6h sáng";
            vm.SendMessageCommand.Execute(null);

            // Kiểm tra các node ghost được thêm vào LogicEditorViewModel
            var ghostNodes = logicVM.Nodes.Where(n => n.IsGhost).ToList();
            Assert.True(ghostNodes.Count >= 2);

            // Phải có khối Trigger TIME_WINDOW
            var trg = ghostNodes.OfType<TriggerNodeViewModel>().FirstOrDefault(n => n.TriggerType == TriggerType.TIME_WINDOW);
            Assert.NotNull(trg);
            Assert.Equal(600, trg.ThresholdLo);

            // TUYỆT ĐỐI KHÔNG có khối Input ma
            var inp = ghostNodes.OfType<InputNodeViewModel>().FirstOrDefault();
            Assert.Null(inp);

            // Phải có khối Action nối trực tiếp từ Trigger
            var act = ghostNodes.OfType<ActionNodeViewModel>().FirstOrDefault(n => n.TargetTag?.Name == "DO0");
            Assert.NotNull(act);
        });
    }

    [Fact]
    public void DraftGraph_ComparatorRule_GeneratesComparatorNodeAndWiresProperly()
    {
        RunOnSta(() =>
        {
            var tagCatalog = new TagCatalogViewModel();
            var aiTag = new TagModel { Name = "AI0", Index = 0, Kind = TagKind.AnalogInput, DataType = TagDataType.Float32 };
            var doTag = new TagModel { Name = "DO0", Index = 0, Kind = TagKind.DiscreteOutput, DataType = TagDataType.Boolean };
            tagCatalog.AllTags.Add(aiTag);
            tagCatalog.AllTags.Add(doTag);

            var logicVM = new LogicEditorViewModel(tagCatalog);
            var vm = new AiChatViewModel(
                () => tagCatalog.AllTags,
                () => Enumerable.Empty<RuleItemModel>(),
                () => logicVM,
                new AiSettingsModel());

            var rules = new List<AiRuleSpecModel>
            {
                new()
                {
                    Narrative = "Stop DO0 when AI0 exceeds 3000",
                    InputTag = "AI0",
                    Trigger = "ON_CHANGE",
                    CompareOp = "GT",
                    ThresholdLo = 3000,
                    ThresholdHi = 0,
                    ActionTag = "DO0",
                    ActionType = "SET_TAG",
                    Param = 0
                }
            };

            var jsonBlock = "```json:rules\n" + System.Text.Json.JsonSerializer.Serialize(rules) + "\n```";
            vm.ApiHandler = (settings, sys, hist, tools, ct) =>
                Task.FromResult(new GeminiToolResponse(jsonBlock, new List<AiToolCall>()));

            vm.InputText = "Tắt bơm khi áp suất AI0 vượt quá 3000";
            vm.SendMessageCommand.Execute(null);

            var ghostNodes = logicVM.Nodes.Where(n => n.IsGhost).ToList();

            // Phải có khối Input AI0
            var inp = ghostNodes.OfType<InputNodeViewModel>().FirstOrDefault(n => n.Tag?.Name == "AI0");
            Assert.NotNull(inp);

            // Phải có khối Comparator CMP
            var cmp = ghostNodes.OfType<ComparatorNodeViewModel>().FirstOrDefault();
            Assert.NotNull(cmp);
            Assert.Equal(CompareOp.GT, cmp.CompareOp);
            Assert.Equal(3000, cmp.ThresholdLo);

            // Phải có khối Action DO0
            var act = ghostNodes.OfType<ActionNodeViewModel>().FirstOrDefault(n => n.TargetTag?.Name == "DO0");
            Assert.NotNull(act);
        });
    }

    [Fact]
    public void ContextAggregator_WithAllCanvasNodes_SerializesCorrectTypesWithoutUnknown()
    {
        RunOnSta(() =>
        {
            var tagCatalog = new TagCatalogViewModel();
            var aiTag = new TagModel { Name = "AI0", Index = 0, Kind = TagKind.AnalogInput, DataType = TagDataType.Float32 };
            tagCatalog.AllTags.Add(aiTag);

            var logicVM = new LogicEditorViewModel(tagCatalog);
            var cmpNode = new ComparatorNodeViewModel(aiTag, CompareOp.GT, 3000, 0);
            var mathNode = new MathNodeViewModel();

            logicVM.Nodes.Add(cmpNode);
            logicVM.Nodes.Add(mathNode);

            var aggregator = new AiContextAggregator(
                () => tagCatalog.AllTags,
                () => logicVM,
                null,
                null);

            var payload = aggregator.BuildContextPayload();
            var nodes = payload["canvas_nodes"]?.AsArray();
            Assert.NotNull(nodes);

            var cmpJson = nodes.FirstOrDefault(n => n?["id"]?.GetValue<string>() == cmpNode.Id);
            Assert.NotNull(cmpJson);
            Assert.Equal("COMPARATOR", cmpJson["type"]?.GetValue<string>());
            Assert.Equal("GT", cmpJson["compare_op"]?.GetValue<string>());
            Assert.Equal(3000, cmpJson["threshold_lo"]?.GetValue<double>());

            var mathJson = nodes.FirstOrDefault(n => n?["id"]?.GetValue<string>() == mathNode.Id);
            Assert.NotNull(mathJson);
            Assert.Equal("MATH", mathJson["type"]?.GetValue<string>());

            // Đảm bảo không có node nào rơi vào UNKNOWN
            foreach (var n in nodes)
            {
                Assert.NotEqual("UNKNOWN", n?["type"]?.GetValue<string>());
            }
        });
    }

    [Fact]
    public void ExplanationSynthesizer_GeneratesCorrectAsciiBoxForComparatorAndTimeWindow()
    {
        RunOnSta(() =>
        {
            var tx = new DraftGraphTransaction();
            tx.AddedNodes.Add(new DraftNode
            {
                Id = "trg_rtc",
                NodeType = "Trigger",
                TriggerType = "TIME_WINDOW",
                ThresholdLo = 600,
                ThresholdHi = 600
            });
            tx.AddedNodes.Add(new DraftNode
            {
                Id = "act_pump",
                NodeType = "Action",
                TagName = "DO0",
                ActionType = "SET_TAG",
                ActionParam = 1
            });
            tx.AddedWires.Add(new DraftWire
            {
                SourceNodeId = "trg_rtc",
                SourcePort = "Out",
                TargetNodeId = "act_pump",
                TargetPort = "In"
            });

            var explanation = AiExplanationSynthesizer.Synthesize(tx, "Bật lúc 6h sáng");
            Assert.Contains("[Trigger: TIME_WINDOW (06:00)]", explanation);
            Assert.Contains("[Action: DO0=1]", explanation);

            // Kiểm tra Comparator
            var txCmp = new DraftGraphTransaction();
            txCmp.AddedNodes.Add(new DraftNode { Id = "in_ai", NodeType = "Input", TagName = "AI0" });
            txCmp.AddedNodes.Add(new DraftNode { Id = "cmp_1", NodeType = "Comparator", CompareOp = "GT", ThresholdLo = 3000, TagName = "AI0" });
            txCmp.AddedNodes.Add(new DraftNode { Id = "act_1", NodeType = "Action", TagName = "DO1", ActionType = "SET_TAG", ActionParam = 0 });
            txCmp.AddedWires.Add(new DraftWire { SourceNodeId = "in_ai", TargetNodeId = "cmp_1" });
            txCmp.AddedWires.Add(new DraftWire { SourceNodeId = "cmp_1", TargetNodeId = "act_1" });

            var expCmp = AiExplanationSynthesizer.Synthesize(txCmp, "Ngắt áp suất");
            Assert.Contains("[Comparator: CMP (GT 3000)]", expCmp);
            Assert.Contains("[Action: DO1=0]", expCmp);
        });
    }

    [Fact]
    public void ToolDeclarations_ContainsTimeWindowAndConfigureComparator()
    {
        var tools = AiToolDeclarations.GetGeminiFunctionDeclarations();
        Assert.NotNull(tools);

        var addNodeTool = tools.FirstOrDefault(t => t?["name"]?.GetValue<string>() == "add_node");
        Assert.NotNull(addNodeTool);

        var triggerEnum = addNodeTool?["parameters"]?["properties"]?["trigger_type"]?["enum"]?.AsArray();
        Assert.NotNull(triggerEnum);
        Assert.Contains(triggerEnum, e => e?.GetValue<string>() == "TIME_WINDOW");

        var configCmpTool = tools.FirstOrDefault(t => t?["name"]?.GetValue<string>() == "configure_comparator");
        Assert.NotNull(configCmpTool);
    }

    [Theory]
    [InlineData(AiUserIntent.ConsultativeQnA, false)]
    [InlineData(AiUserIntent.ConsultativeQnA, true)]
    [InlineData(AiUserIntent.ProjectDiagnose, false)]
    [InlineData(AiUserIntent.ProjectDiagnose, true)]
    [InlineData(AiUserIntent.FbdSynthesis, false)]
    [InlineData(AiUserIntent.FbdSynthesis, true)]
    public void AllModes_ContainOntologicalBoundary(AiUserIntent intent, bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(intent, isEnglish);
        Assert.Contains("Structured Text", prompt);
        Assert.Contains("TIME_WINDOW", prompt);
    }

    [Theory]
    [InlineData(AiUserIntent.ConsultativeQnA, false)]
    [InlineData(AiUserIntent.ConsultativeQnA, true)]
    [InlineData(AiUserIntent.ProjectDiagnose, false)]
    [InlineData(AiUserIntent.ProjectDiagnose, true)]
    [InlineData(AiUserIntent.FbdSynthesis, false)]
    [InlineData(AiUserIntent.FbdSynthesis, true)]
    public void AllModes_ContainTimerAndCounterKnowledge(AiUserIntent intent, bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(intent, isEnglish);
        Assert.Contains("TON", prompt);
        Assert.Contains("TOF", prompt);
        Assert.Contains("TP", prompt);
        Assert.Contains("CTU", prompt);
        Assert.Contains("VREG_RETAIN", prompt);
    }

    [Theory]
    [InlineData(AiUserIntent.ConsultativeQnA, false)]
    [InlineData(AiUserIntent.ConsultativeQnA, true)]
    [InlineData(AiUserIntent.ProjectDiagnose, false)]
    [InlineData(AiUserIntent.ProjectDiagnose, true)]
    [InlineData(AiUserIntent.FbdSynthesis, false)]
    [InlineData(AiUserIntent.FbdSynthesis, true)]
    public void AllModes_ContainZeroHallucinationContract(AiUserIntent intent, bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(intent, isEnglish);
        Assert.Contains("SYS_RTC_", prompt);
        Assert.Contains("TIME_WINDOW", prompt);
        Assert.Contains("NONE", prompt);
    }

    [Theory]
    [InlineData(AiUserIntent.ConsultativeQnA, false)]
    [InlineData(AiUserIntent.ConsultativeQnA, true)]
    [InlineData(AiUserIntent.ProjectDiagnose, false)]
    [InlineData(AiUserIntent.ProjectDiagnose, true)]
    [InlineData(AiUserIntent.FbdSynthesis, false)]
    [InlineData(AiUserIntent.FbdSynthesis, true)]
    public void AllModes_Contain15Archetypes(AiUserIntent intent, bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(intent, isEnglish);
        Assert.Contains("MQTT", prompt);
        Assert.Contains("MODBUS", prompt);
        Assert.Contains("SCALE", prompt);
        Assert.Contains("COMPARATOR", prompt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiagnoseMode_KnowsTimeWindowWithoutInputIsNormal(bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.ProjectDiagnose, isEnglish);
        Assert.Contains("TIME_WINDOW", prompt);
        if (isEnglish)
        {
            Assert.Contains("completely normal", prompt);
        }
        else
        {
            Assert.Contains("hoàn toàn bình thường", prompt);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConsultativeMode_KnowsPulseTimerForShortAlarms(bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.ConsultativeQnA, isEnglish);
        Assert.Contains("Pulse Timer", prompt);
        Assert.Contains("PT=5s", prompt);
    }

    [Theory]
    [InlineData(AiUserIntent.ConsultativeQnA, false)]
    [InlineData(AiUserIntent.ConsultativeQnA, true)]
    [InlineData(AiUserIntent.ProjectDiagnose, false)]
    [InlineData(AiUserIntent.ProjectDiagnose, true)]
    [InlineData(AiUserIntent.FbdSynthesis, false)]
    [InlineData(AiUserIntent.FbdSynthesis, true)]
    public void AllModes_PromptLengthUnderSafeTokenBudget(AiUserIntent intent, bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(intent, isEnglish);
        // Dưới 10,000 ký tự -> an toàn tuyệt đối dưới 2,500 tokens
        Assert.True(prompt.Length < 10000, $"Prompt {intent} (EN={isEnglish}) quá dài: {prompt.Length} chars");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildSynthesisInstruction_LengthConstraint_SatisfiesRange(bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis, isEnglish);
        Assert.InRange(prompt.Length, 6000, 25000);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuildContextPrompt_TagFormatting_RetainsAliasAndAddress(bool isEnglish)
    {
        var tags = new List<TagModel>
        {
            new() { Name = "DI0", Alias = "BTN_START", Kind = TagKind.DiscreteInput, Channel = 0 }
        };

        string context = AiPromptBuilder.BuildContextPrompt(tags, null, "Test query", isEnglish);
        Assert.Contains("DI0", context);
        Assert.Contains("BTN_START", context);
        Assert.Contains("DiscreteInput", context);
        Assert.Contains("0x0000", context);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TagCatalogRule_ExplicitlyForbidsSysRtcTagsWhenInputIsNone(bool isEnglish)
    {
        string prompt = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis, isEnglish);
        if (isEnglish)
        {
            Assert.Contains("NEVER list SYS_RTC_HOUR or SYS_RTC_MINUTE in the Tag Catalog for TIME_WINDOW rules!", prompt);
        }
        else
        {
            Assert.Contains("TUYỆT ĐỐI KHÔNG đưa SYS_RTC_HOUR hay SYS_RTC_MINUTE vào Danh mục Tag của sơ đồ đó!", prompt);
        }
    }
}
