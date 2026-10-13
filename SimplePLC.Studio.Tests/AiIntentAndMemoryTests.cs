using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class AiIntentAndMemoryTests
{
    // =========================================================================
    // 1. INTENT CLASSIFICATION TESTS
    // =========================================================================

    [Theory]
    [InlineData("Tại sao nút dừng khẩn cấp E-Stop luôn dùng tiếp điểm thường đóng NC thay vì NO?")]
    [InlineData("Sự khác biệt giữa Timer TON và TOF là gì?")]
    [InlineData("Làm thế nào để chống rung phím cơ khí cho DI0?")]
    [InlineData("Giải thích DI0 và DO0 trong hệ thống PLC")]
    [InlineData("Tại sao DI0 và DO0 không nên dùng chung nguồn 24VDC với tải công suất lớn?")]
    [InlineData("Tính toán hệ số k và b cho cảm biến áp suất 0-10V sang 0-10 bar như thế nào?")]
    [InlineData("Tiêu chuẩn an toàn IEC 60204-1 quy định gì về mạch dừng khẩn cấp?")]
    [InlineData("Nguyên lý hoạt động của relay nhiệt và cách đấu nối")]
    [InlineData("Giải thích nguyên lý tạo mạch tự giữ")]
    [InlineData("Khái niệm Fail-Safe trong điều khiển công nghiệp là gì?")]
    [InlineData("")]
    public void AiIntentClassifier_ConsultativeQueries_ClassifiedAsConsultativeQnA(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: false, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.ConsultativeQnA, intent);
    }

    [Theory]
    [InlineData("Tạo mạch khởi động và dừng bơm DO0, có khóa an toàn nút dừng khẩn cấp DI2")]
    [InlineData("Hẹn giờ trễ ngắt quạt làm mát DO1 sau 10 giây khi máy dừng")]
    [InlineData("Cảm biến áp suất AI0 vượt quá 80 bar duy trì 5 giây thì đóng van xả DO0")]
    [InlineData("Đếm đủ 100 sản phẩm từ cảm biến DI0 thì bật còi báo DO2")]
    [InlineData("Quy đổi cảm biến áp suất analog AI0 (4-20mA) sang 0-10 bar")]
    [InlineData("Thêm timer TON 5 giây cho van DO3")]
    [InlineData("Nối dây từ ngõ vào DI0 sang Trigger ON_RISE")]
    [InlineData("Cấu hình counter CTU đếm 50 sản phẩm")]
    [InlineData("Đổi tag DI1 thành EmergencyStop")]
    [InlineData("Tạo mạch tự giữ và giải thích nguyên lý hoạt động")]
    public void AiIntentClassifier_SynthesisQueries_ClassifiedAsFbdSynthesis(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: true, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.FbdSynthesis, intent);
    }

    [Theory]
    [InlineData("Kiểm tra lỗi cho sơ đồ hiện tại")]
    [InlineData("Rà soát xung đột ghi trên canvas")]
    [InlineData("Tại sao mạch của tôi không chạy?")]
    [InlineData("Mạch đang báo lỗi gì vậy?")]
    [InlineData("Check lỗi cú pháp và linter")]
    public void AiIntentClassifier_DiagnosticQueries_ClassifiedAsProjectDiagnose(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: true, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.ProjectDiagnose, intent);
    }

    [Fact]
    public void AiIntentClassifier_WhenLinterErrorsPresent_ClassifiesGenericCheckAsProjectDiagnose()
    {
        var intent = AiIntentClassifier.Classify("Kiểm tra giúp tôi mạch này với", hasCanvasElements: true, hasLinterErrors: true);
        Assert.Equal(AiUserIntent.ProjectDiagnose, intent);
    }

    // =========================================================================
    // 2. DUAL-MODE PROMPT BUILDER TESTS
    // =========================================================================

    [Fact]
    public void AiPromptBuilder_BuildSystemInstruction_ConsultativeMode_DoesNotContainRulesJsonBlock()
    {
        string instruction = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.ConsultativeQnA);

        Assert.NotNull(instruction);
        Assert.Contains("Senior Industrial Automation", instruction);
        Assert.Contains("IEC 60204-1", instruction);
        Assert.Contains("Fail-Safe", instruction);
        Assert.Contains("Debounce", instruction);
        Assert.Contains("TUYỆT ĐỐI KHÔNG sinh khối mã JSON máy đọc", instruction);
        Assert.DoesNotContain("```json:rules", instruction);
    }

    [Fact]
    public void AiPromptBuilder_BuildSystemInstruction_DiagnoseMode_FocusesOnSafetyAndDiagnostics()
    {
        string instruction = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.ProjectDiagnose);

        Assert.NotNull(instruction);
        Assert.Contains("Diagnostic", instruction);
        Assert.Contains("Write Conflict", instruction);
        Assert.Contains("IEC 60204-1", instruction);
        Assert.DoesNotContain("```json:rules", instruction);
    }

    [Fact]
    public void AiPromptBuilder_BuildSystemInstruction_FbdSynthesis_ConformsToConstraintsAndRequiredInstructions()
    {
        string instruction = AiPromptBuilder.BuildSystemInstruction(AiUserIntent.FbdSynthesis);

        Assert.NotNull(instruction);

        // 1. Length constraint check: System prompt must be rich but strictly bounded to prevent token exhaustion
        // Expected character length between 6,000 and 25,000 characters (~1,500 to 5,000 tokens)
        Assert.InRange(instruction.Length, 6000, 25000);

        // 2. Persona and 1 Input -> 1 Output Golden Rule
        Assert.Contains("SynaptiX Industrial PLC AI Assistant", instruction);
        Assert.Contains("1 INPUT ➔ 1 OUTPUT", instruction);
        Assert.Contains("CHỈ ĐIỀU KHIỂN DUY NHẤT 1 TARGET TAG", instruction);
        Assert.Contains("TÁCH THÀNH NHIỀU RULE ĐỘC LẬP", instruction);

        // 3. 4-Stage Pipeline architecture
        Assert.Contains("4-Stage Pipeline", instruction);
        Assert.Contains("Stage 1: Input Tag", instruction);
        Assert.Contains("Stage 2: Trigger", instruction);
        Assert.Contains("Stage 3: Guard", instruction);
        Assert.Contains("Stage 4: Action", instruction);

        // 4. IEC Macros & Scaler specifications
        Assert.Contains("TIMER MACROS (TON, TOF, TP)", instruction);
        Assert.Contains("TON", instruction);
        Assert.Contains("TOF", instruction);
        Assert.Contains("TP", instruction);
        Assert.Contains("COUNTER MACROS (CTU, CTD)", instruction);
        Assert.Contains("CTU", instruction);
        Assert.Contains("CTD", instruction);
        Assert.Contains("SCALE", instruction);
        Assert.Contains("y = k \\cdot x + b", instruction);

        // 5. Chat response structure (3 sections)
        Assert.Contains("QUY CHUẨN ĐỊNH DẠNG CÂU TRẢ LỜI CỦA AI TRONG CHATBOX", instruction);
        Assert.Contains("1. **Nguyên lý hoạt động", instruction);
        Assert.Contains("2. **Danh mục Tag", instruction);
        Assert.Contains("3. **Cơ chế an toàn công nghiệp", instruction);

        // 6. Tool calling rules & Machine-readable JSON block
        Assert.Contains("QUY TẮC BẮT BUỘC KHI GỌI TOOL CALLS", instruction);
        Assert.Contains("KHỐI CẤU TRÚC MÁY ĐỌC BẮT BUỘC (MACHINE-READABLE JSON)", instruction);
        Assert.Contains("```json:rules", instruction);
    }

    [Fact]
    public void AiPromptBuilder_BuildContextPrompt_StressInputs_EnforcesStrictCapsAndLengthBound()
    {
        // Stress harness: 500 tags with configured aliases and 100 enabled rules
        var tags = Enumerable.Range(0, 500).Select(i => new TagModel
        {
            Index = i,
            Name = $"TAG_{i}",
            Alias = $"Custom Alias {i}",
            Kind = TagKind.VirtualRegister
        }).ToList();

        var rules = Enumerable.Range(0, 100).Select(i => new RuleItemModel
        {
            Index = i,
            Id = $"R{i}",
            Enabled = true,
            Narrative = $"Stress test rule description number {i} with extensive details"
        }).ToList();

        string prompt = AiPromptBuilder.BuildContextPrompt(tags, rules, "Test prompt under stress");

        Assert.NotNull(prompt);
        // Prompt should be strictly bounded under 6,000 characters despite 500 tags and 100 rules
        Assert.True(prompt.Length < 6000, $"Prompt length was {prompt.Length}, expected < 6000 chars");

        // Verify that at most 40 tags and at most 15 rules are included in the prompt
        Assert.Contains("TAG_0", prompt);
        Assert.Contains("TAG_39", prompt);
        Assert.DoesNotContain("TAG_40", prompt);

        Assert.Contains("Rule 0 (R0)", prompt);
        Assert.Contains("Rule 14 (R14)", prompt);
        Assert.DoesNotContain("Rule 15 (R15)", prompt);
    }

    // =========================================================================
    // 3. CONTEXT AGGREGATOR TAG PRIORITIZATION TESTS
    // =========================================================================

    [Fact]
    public void AiContextAggregator_PrioritizesCanvasTagsAndAliases_ReducesTokenOverhead()
    {
        // 124 Tags giả lập
        var allTags = new List<TagModel>();
        for (int i = 0; i < 124; i++)
        {
            allTags.Add(new TagModel
            {
                Index = i,
                Name = $"TAG_{i}",
                Kind = TagKind.VirtualRegister,
                Alias = string.Empty
            });
        }

        // Đặt alias cho 2 tag
        allTags[10].Alias = "Nút khẩn cấp";
        allTags[20].Alias = "Cảm biến quang";

        // Thêm các tag core chuẩn
        allTags.Add(new TagModel { Index = 200, Name = "DI0", Kind = TagKind.DiscreteInput });
        allTags.Add(new TagModel { Index = 201, Name = "DO0", Kind = TagKind.DiscreteOutput });

        var aggregator = new AiContextAggregator(
            () => allTags,
            () => null);

        var payload = aggregator.BuildContextPayload();
        var catalog = payload["tag_catalog"]?.AsArray();

        Assert.NotNull(catalog);
        // Không gửi toàn bộ 126 tags thô
        Assert.True(catalog.Count < 50);

        // Các tag có alias và core phải xuất hiện
        var catalogNames = catalog.Select(x => x?["name"]?.ToString()).ToList();
        Assert.Contains("TAG_10", catalogNames);
        Assert.Contains("TAG_20", catalogNames);
        Assert.Contains("DI0", catalogNames);
        Assert.Contains("DO0", catalogNames);
    }

    // =========================================================================
    // 4. CONSULTATIVE Q&A ZERO GHOST / ZERO ROGUE REGEX TESTS
    // =========================================================================

    [Fact]
    public async Task AiChatViewModel_ConsultativeQuery_DoesNotProduceGhostRulesOrProposals()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var chatVM = mainVM.AiChatVM;
        var logicVM = mainVM.LogicEditorVM;

        chatVM.ApiKeyInput = "valid-api-key";
        chatVM.SaveSettings();

        string technicalExplanation = """
            Trong các hệ thống tự động hóa công nghiệp, việc dùng chung nguồn 24VDC giữa mạch tín hiệu I/O (DI0, DO0)
            và tải công suất lớn (như cuộn hút Contactor, van điện từ Solenoid) là điều tối kỵ vì:
            1. Hiện tượng sụt áp tạm thời (Voltage Dip) khi đóng cắt tải cảm ứng.
            2. Nhiễu điện từ cảm ứng (Back-EMF) do cuộn dây phóng ngược điện áp cao khi ngắt mạch.
            3. Rủi ro hỏng cách ly quang (Optocoupler) của PLC.
            """;

        chatVM.ApiHandler = (settings, sysPrompt, history, tools, ct) =>
        {
            // Kiểm tra intent ConsultativeQnA không gửi tool schemas
            Assert.Null(tools);
            Assert.Contains("Senior Industrial Automation", sysPrompt);
            return Task.FromResult(new GeminiToolResponse(technicalExplanation, new List<AiToolCall>()));
        };

        // Kỹ sư hỏi câu hỏi lý thuyết có nhắc đến DI0 và DO0
        chatVM.InputText = "Tại sao DI0 và DO0 không nên dùng chung nguồn 24VDC với tải công suất lớn?";
        await chatVM.SendMessageAsync();

        var lastMessage = chatVM.Messages.Last();
        Assert.False(lastMessage.HasProposal);
        Assert.False(lastMessage.HasExtractedRules);
        Assert.Empty(lastMessage.ExtractedRules);
        Assert.False(logicVM.HasPendingAiProposal);
        Assert.DoesNotContain(logicVM.Nodes, n => n.IsGhost);
        Assert.Equal(technicalExplanation, lastMessage.Content);
    }

    [Fact]
    public async Task AiChatViewModel_ConsultativeQuery_WithStartStopKeywords_DoesNotTriggerRogueRegex()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var chatVM = mainVM.AiChatVM;
        var logicVM = mainVM.LogicEditorVM;

        chatVM.ApiKeyInput = "valid-api-key";
        chatVM.SaveSettings();

        string answer = "Nút Start và Stop điều khiển DI0 và DO0 bằng cách cấp xung sườn lên ON_RISE để kích hoạt logic tự giữ.";

        chatVM.ApiHandler = (settings, sysPrompt, history, tools, ct) =>
        {
            return Task.FromResult(new GeminiToolResponse(answer, new List<AiToolCall>()));
        };

        // Câu hỏi chứa cả DI0, DO0, Start, Stop nhưng là câu hỏi tư vấn (có 'giải thích')
        chatVM.InputText = "Giải thích DI0 và DO0 khi bấm Start và Stop hoạt động thế nào";
        await chatVM.SendMessageAsync();

        var lastMessage = chatVM.Messages.Last();
        // Phải không kích hoạt rogue regex tạo rule
        Assert.False(lastMessage.HasProposal);
        Assert.False(lastMessage.HasExtractedRules);
        Assert.DoesNotContain(logicVM.Nodes, n => n.IsGhost);
    }

    // =========================================================================
    // 5. LLM RESPONSE PRESERVATION (NO "NGUYÊN LÝ" OVERWRITE) TESTS
    // =========================================================================

    [Fact]
    public async Task AiChatViewModel_PreservesLlmResponse_WithoutTheWordNguyenLy()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var chatVM = mainVM.AiChatVM;

        chatVM.ApiKeyInput = "valid-api-key";
        chatVM.SaveSettings();

        // Chuỗi văn bản trả lời kỹ thuật chuẩn mực của LLM nhưng KHÔNG chứa từ vựng "Nguyên lý"
        string naturalLlmText = """
            Mạch điều khiển trạm bơm đã sẵn sàng.
            Cơ chế kích hoạt: Tín hiệu nút bấm DI0 sẽ đưa bơm DO0 vào trạng thái hoạt động.
            Khi nút dừng DI1 được kích hoạt, hệ thống sẽ ngắt bơm ngay lập tức.
            Khóa chéo nút khẩn cấp DI2 đảm bảo máy luôn dừng an toàn.
            """;

        chatVM.ApiHandler = (settings, sysPrompt, history, tools, ct) =>
        {
            var calls = new List<AiToolCall>
            {
                new("add_node", new JsonObject { ["node_type"] = "Input", ["tag_name"] = "DI0" }),
                new("add_node", new JsonObject { ["node_type"] = "Trigger", ["trigger_type"] = "ON_RISE" }),
                new("add_node", new JsonObject { ["node_type"] = "Action", ["tag_name"] = "DO0", ["action_type"] = "SET_TAG", ["action_param"] = 1 })
            };
            return Task.FromResult(new GeminiToolResponse(naturalLlmText, calls));
        };

        chatVM.InputText = "Tạo mạch khởi động và dừng bơm DO0";
        await chatVM.SendMessageAsync();

        var lastMessage = chatVM.Messages.Last();
        Assert.True(lastMessage.HasProposal);
        // Tuyệt đối không bị ghi đè thành câu mẫu rập khuôn của AiExplanationSynthesizer
        Assert.Equal(naturalLlmText, lastMessage.Content);
        Assert.DoesNotContain("● Chống dội tín hiệu (Hardware Debounce)", lastMessage.Content);
    }

    // =========================================================================
    // 6. MULTI-TURN MEMORY & JSON SANITIZATION TESTS
    // =========================================================================

    [Fact]
    public void AiChatViewModel_SanitizeHistoryMessage_StripsJsonRulesBlock()
    {
        string rawMessage = """
            Dưới đây là sơ đồ điều khiển bơm:
            - Bật khi nhấn Start DI0
            - Tắt khi nhấn Stop DI1

            ```json:rules
            [
              {
                "narrative": "Bật DO0 khi DI0 ON_RISE",
                "input_tag": "DI0",
                "trigger": "ON_RISE",
                "action_tag": "DO0",
                "action_type": "SET_TAG",
                "param": 1
              }
            ]
            ```
            Bạn có thể xem trước sơ đồ trên Canvas.
            """;

        string sanitized = AiChatViewModel.SanitizeHistoryMessage(rawMessage);

        Assert.DoesNotContain("```json:rules", sanitized);
        Assert.DoesNotContain("\"narrative\": \"Bật DO0 khi DI0 ON_RISE\"", sanitized);
        Assert.Contains("Dưới đây là sơ đồ điều khiển bơm:", sanitized);
        Assert.Contains("Bạn có thể xem trước sơ đồ trên Canvas.", sanitized);
    }

    [Fact]
    public async Task AiChatViewModel_MultiTurnMemory_UsesSlidingWindowOf12AndSanitizesHistory()
    {
        var mainVM = new MainViewModel(uiDispatcher: a => a());
        var chatVM = mainVM.AiChatVM;

        chatVM.ApiKeyInput = "valid-api-key";
        chatVM.SaveSettings();

        // Tạo 14 tin nhắn lịch sử (7 lượt user/assistant)
        for (int i = 1; i <= 7; i++)
        {
            chatVM.Messages.Add(new ChatMessageModel("user", $"Câu hỏi số {i}"));
            chatVM.Messages.Add(new ChatMessageModel("assistant",
                $"Trả lời số {i}\n```json:rules\n[{{\"input_tag\": \"DI{i}\"}}]\n```"));
        }

        List<(string role, string text)>? capturedHistory = null;
        chatVM.ApiHandler = (settings, sysPrompt, history, tools, ct) =>
        {
            capturedHistory = history.ToList();
            return Task.FromResult(new GeminiToolResponse("Câu trả lời lượt 8", new List<AiToolCall>()));
        };

        chatVM.InputText = "Câu hỏi số 8";
        await chatVM.SendMessageAsync();

        Assert.NotNull(capturedHistory);
        // Trượt tối đa 12 tin nhắn
        Assert.True(capturedHistory.Count <= 12);

        // Tin nhắn của assistant trong lịch sử KHÔNG được chứa khối code ```json:rules
        foreach (var (role, text) in capturedHistory)
        {
            if (role == "assistant")
            {
                Assert.DoesNotContain("```json:rules", text);
            }
        }
    }
}
