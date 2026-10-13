using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Adversarial stress test harness for AiIntentClassifier and ConversationMemoryEngine (Milestone 1).
/// Designed by teamwork_preview_challenger_m1_1.
/// </summary>
public class AiIntentAndMemoryStressTests
{
    // =========================================================================
    // 1. TRICKY PROMPTS MIXING THEORY AND ACTION
    // =========================================================================

    [Theory]
    [InlineData("Giải thích tại sao tôi không nên thêm timer TON ở mạch này?")]
    [InlineData("Tại sao tôi không nên thêm timer TON ở mạch này?")]
    [InlineData("Tôi có nên thêm timer TON ở mạch này không?")]
    [InlineData("So sánh sự khác nhau giữa DI0 và DO0 khi cấp điện 24V")]
    [InlineData("Giải thích nguyên lý tạo mạch tự giữ và vì sao cần nút Stop thường đóng NC")]
    [InlineData("Cho tôi hỏi nguyên lý mạch sao tam giác hoạt động như thế nào?")]
    [InlineData("Tại sao DI0 và DO0 không nên dùng chung nguồn 24VDC với tải công suất lớn?")]
    [InlineData("Timer TON 5 giây hoạt động như thế nào?")]
    [InlineData("Tính toán thời gian trễ của timer TON dựa trên tần số quét scan cycle")]
    [InlineData("Phân tích tại sao mạch tự giữ lại cần khóa chéo phần mềm")]
    public void IntentClassifier_MixedTheoryAndAction_QuestionsClassifiedAsConsultative(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: true, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.ConsultativeQnA, intent);
    }

    [Theory]
    [InlineData("Thêm timer TON ở mạch này và giải thích tại sao?")]
    [InlineData("Tạo mạch tự giữ và giải thích nguyên lý hoạt động")]
    [InlineData("Vẽ sơ đồ mạch sao tam giác và giải thích nguyên tắc chuyển đổi")]
    [InlineData("Thiết kế mạch bơm DO0 khi nhấn DI0, kèm thuyết minh nguyên lý")]
    [InlineData("Cấu hình timer TON 5s cho van DO3")]
    public void IntentClassifier_MixedTheoryAndAction_ActionFirstClassifiedAsSynthesis(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: true, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.FbdSynthesis, intent);
    }

    [Theory]
    [InlineData("Mạch đang chạy thì bị dừng đột ngột, kiểm tra lỗi giúp tôi")]
    [InlineData("Tại sao bơm DO0 không chạy khi nhấn DI0?")]
    [InlineData("Sơ đồ đang báo lỗi gì mà đèn đỏ nhấp nháy?")]
    [InlineData("Kiểm tra xung đột ghi giữa rule 1 và rule 2")]
    public void IntentClassifier_DiagnosticPrompts_ClassifiedAsProjectDiagnose(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: true, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.ProjectDiagnose, intent);
    }

    // =========================================================================
    // 2. BOUNDARY CONDITIONS, CASE INSENSITIVITY & PUNCTUATION STRESS
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void IntentClassifier_NullOrEmptyInputs_DoNotThrowAndReturnSafeDefault(string? prompt)
    {
        var intentWithoutErrors = AiIntentClassifier.Classify(prompt!, hasCanvasElements: false, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.ConsultativeQnA, intentWithoutErrors);

        var intentWithErrors = AiIntentClassifier.Classify(prompt!, hasCanvasElements: false, hasLinterErrors: true);
        Assert.Equal(AiUserIntent.ProjectDiagnose, intentWithErrors);
    }

    [Theory]
    [InlineData("GIẢI THÍCH TẠI SAO TÔI KHÔNG NÊN THÊM TIMER TON Ở MẠCH NÀY?")]
    [InlineData("SO SÁNH SỰ KHÁC NHAU GIỮA DI0 VÀ DO0 KHI CẤP ĐIỆN 24V")]
    [InlineData("TẠI SAO NÚT DỪNG KHẨN CẤP LUÔN DÙNG TIẾP ĐIỂM THƯỜNG ĐÓNG NC?")]
    public void IntentClassifier_UppercasePrompts_ClassifiedAccurately(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: false, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.ConsultativeQnA, intent);
    }

    [Theory]
    [InlineData("TẠO MẠCH KHỞI ĐỘNG VÀ DỪNG BƠM DO0 KHI NHẤN NÚT DI0")]
    [InlineData("THÊM TIMER TON 5 GIÂY CHO VAN DO3")]
    public void IntentClassifier_UppercaseSynthesisPrompts_ClassifiedAccurately(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: true, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.FbdSynthesis, intent);
    }

    [Theory]
    [InlineData("   Giải thích    tại sao tôi    không nên thêm timer TON?   \r\n\t  ")]
    [InlineData("¿¿¿ Giải thích tại sao tôi không nên thêm timer TON ở mạch này ???!!!")]
    [InlineData("[Hỏi đáp] Giải thích tại sao tôi không nên thêm timer TON ở mạch này?")]
    public void IntentClassifier_TrickyPunctuationAndWhitespace_DoesNotCrash(string prompt)
    {
        var intent = AiIntentClassifier.Classify(prompt, hasCanvasElements: false, hasLinterErrors: false);
        Assert.Equal(AiUserIntent.ConsultativeQnA, intent);
    }

    [Fact]
    public void IntentClassifier_LargeInput_DoesNotCauseReDoSOrHang()
    {
        var sb = new StringBuilder();
        sb.Append("Giải thích tại sao ");
        for (int i = 0; i < 5000; i++)
        {
            sb.Append("rất ");
        }
        sb.Append("quan trọng khi dùng tiếp điểm NC cho E-Stop?");
        string hugePrompt = sb.ToString();

        var sw = Stopwatch.StartNew();
        var intent = AiIntentClassifier.Classify(hugePrompt);
        sw.Stop();

        Assert.Equal(AiUserIntent.ConsultativeQnA, intent);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Classification took too long: {sw.ElapsedMilliseconds} ms");
    }

    // =========================================================================
    // 3. SANITIZE HISTORY MESSAGE ADVERSARIAL STRESS TESTS
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void SanitizeHistoryMessage_NullOrEmpty_ReturnsEmpty(string? input)
    {
        string result = AiChatViewModel.SanitizeHistoryMessage(input!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void SanitizeHistoryMessage_NormalMarkdownWithoutRules_IsPreserved()
    {
        string markdown = "Chào bạn! Trong tiêu chuẩn IEC 60204-1, mạch dừng khẩn cấp phải dùng tiếp điểm NC.";
        string result = AiChatViewModel.SanitizeHistoryMessage(markdown);
        Assert.Equal(markdown, result);
    }

    [Fact]
    public void SanitizeHistoryMessage_MultipleJsonBlocks_AreAllStripped()
    {
        string textWithMultipleBlocks =
            "Nguyên lý khối 1:\n" +
            "```json:rules\n" +
            "[\n" +
            "  {\"narrative\": \"Khối 1\", \"input_tag\": \"DI0\"}\n" +
            "]\n" +
            "```\n" +
            "Nguyên lý khối 2:\n" +
            "```json:rules\n" +
            "[\n" +
            "  {\"narrative\": \"Khối 2\", \"input_tag\": \"DI1\"}\n" +
            "]\n" +
            "```\n" +
            "Kết luận và hướng dẫn vận hành an toàn.";

        string sanitized = AiChatViewModel.SanitizeHistoryMessage(textWithMultipleBlocks);

        Assert.DoesNotContain("```json:rules", sanitized);
        Assert.DoesNotContain("\"narrative\": \"Khối 1\"", sanitized);
        Assert.DoesNotContain("\"narrative\": \"Khối 2\"", sanitized);
        Assert.Contains("Nguyên lý khối 1:", sanitized);
        Assert.Contains("Nguyên lý khối 2:", sanitized);
        Assert.Contains("Kết luận và hướng dẫn vận hành an toàn.", sanitized);
    }

    [Fact]
    public void SanitizeHistoryMessage_NonRuleJsonObject_IsNotStripped()
    {
        string contentWithRegularJson =
            "Dưới đây là cấu hình tham số động cơ:\n" +
            "```json\n" +
            "{\n" +
            "  \"rated_power_kw\": 7.5,\n" +
            "  \"nominal_current_a\": 15.2\n" +
            "}\n" +
            "```\n" +
            "Hãy đảm bảo cài đặt đúng bảo vệ quá tải.";

        string sanitized = AiChatViewModel.SanitizeHistoryMessage(contentWithRegularJson);

        Assert.Contains("rated_power_kw", sanitized);
        Assert.Contains("nominal_current_a", sanitized);
    }

    [Fact]
    public void SanitizeHistoryMessage_CSharpCodeBlock_IsNotStripped()
    {
        string csharpCode =
            "Mẫu code kiểm tra trạng thái:\n" +
            "```csharp\n" +
            "var isRunning = tagService.GetBool(\"DO0\");\n" +
            "```\n" +
            "Chú ý chỉ đọc không ghi đè.";

        string sanitized = AiChatViewModel.SanitizeHistoryMessage(csharpCode);

        Assert.Contains("var isRunning = tagService.GetBool(\"DO0\");", sanitized);
        Assert.Contains("```csharp", sanitized);
    }

    [Fact]
    public void SanitizeHistoryMessage_OrphanedFenceAtEnd_IsCleaned()
    {
        string orphaned = "Phần giải thích kết thúc tại đây.\n```json:rules";
        string sanitized = AiChatViewModel.SanitizeHistoryMessage(orphaned);
        Assert.DoesNotContain("```json:rules", sanitized);
        Assert.Equal("Phần giải thích kết thúc tại đây.", sanitized);
    }

    [Fact]
    public void SanitizeHistoryMessage_LargeMessageWithRules_PerformsUnder100ms()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Hướng dẫn kỹ thuật hệ thống SimplePLC");
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"- Bước {i}: Đảm bảo điện áp cấp 24VDC chuẩn công nghiệp.");
        }
        sb.AppendLine("```json:rules");
        sb.AppendLine("[");
        for (int i = 0; i < 20; i++)
        {
            sb.AppendLine($"  {{\"narrative\": \"Rule {i}\", \"input_tag\": \"DI{i % 8}\", \"action_tag\": \"DO{i % 8}\"}}{(i < 19 ? "," : "")}");
        }
        sb.AppendLine("]");
        sb.AppendLine("```");
        sb.AppendLine("Chúc hệ thống vận hành tin cậy!");

        string raw = sb.ToString();

        var sw = Stopwatch.StartNew();
        string sanitized = AiChatViewModel.SanitizeHistoryMessage(raw);
        sw.Stop();

        Assert.DoesNotContain("```json:rules", sanitized);
        Assert.DoesNotContain("\"narrative\": \"Rule 0\"", sanitized);
        Assert.Contains("Chúc hệ thống vận hành tin cậy!", sanitized);
        Assert.True(sw.ElapsedMilliseconds < 100, $"Sanitization took too long: {sw.ElapsedMilliseconds} ms");
    }

    // =========================================================================
    // 4. UNICODE NORMALIZATION FORM D VS FORM C EVALUATION
    // =========================================================================

    [Fact]
    public void IntentClassifier_UnicodeFormD_DoesNotThrow()
    {
        // Decomposed Unicode (Form D)
        string composed = "Giải thích tại sao tôi không nên thêm timer TON ở mạch này?";
        string decomposed = composed.Normalize(NormalizationForm.FormD);

        // Verify that decomposing changes the underlying code points
        Assert.NotEqual(composed, decomposed);

        // Execute classifier - ensure no crash
        var intent = AiIntentClassifier.Classify(decomposed);
        
        // Note: Default fallback is ConsultativeQnA if string matching doesn't match decomposed diacritics
        Assert.True(intent == AiUserIntent.ConsultativeQnA || intent == AiUserIntent.FbdSynthesis);
    }

    // =========================================================================
    // 5. EMPIRICALLY CONFIRMED EDGE CASE FINDINGS
    // =========================================================================

    [Fact]
    public void IntentClassifier_CompareCircuitVersusRelay_ExposesMissingStarterKeywordBug()
    {
        // EMPIRICAL FINDING: "so sánh" is in ConsultativeKeywords, but omitted from questionStarters.
        // When combined with "mạch tự giữ" (in SynthesisKeywords), StartsOrFramedAsConsultative fails,
        // misclassifying this consultative comparison as FbdSynthesis.
        string prompt = "So sánh mạch tự giữ và rơ le thời gian";
        var intent = AiIntentClassifier.Classify(prompt);

        Assert.Equal(AiUserIntent.FbdSynthesis, intent);
    }

    [Fact]
    public void IntentClassifier_ConsecutiveSpacesInCommand_ExposesSubstringMatchingVulnerability()
    {
        // EMPIRICAL FINDING: String.Contains without whitespace normalization fails when user enters multiple spaces.
        // Falls back to ConsultativeQnA instead of detecting synthesis.
        string prompt = "Tạo   mạch   tự   giữ";
        var intent = AiIntentClassifier.Classify(prompt);

        Assert.Equal(AiUserIntent.ConsultativeQnA, intent);
    }

    [Fact]
    public void IntentClassifier_MarkdownBoldFormatting_ExposesPrefixMatchingLimitation()
    {
        // EMPIRICAL FINDING: Markdown tokens like ** at prompt start prevent StartsWith("giải thích") from matching.
        // Misclassifies a consultative question as FbdSynthesis.
        string prompt = "**Giải thích** nguyên lý tạo mạch tự giữ";
        var intent = AiIntentClassifier.Classify(prompt);

        Assert.Equal(AiUserIntent.FbdSynthesis, intent);
    }

    [Fact]
    public void IntentClassifier_UnicodeFormD_SynthesisCommand_FailsMatchingDueToOrdinalComparison()
    {
        // EMPIRICAL FINDING: In Form D, "Tạo mạch" has decomposed combining diacritics,
        // so Ordinal comparison against NFC literal in code fails, falling back to ConsultativeQnA.
        string composed = "Tạo mạch tự giữ";
        string decomposed = composed.Normalize(NormalizationForm.FormD);

        var intentDecomposed = AiIntentClassifier.Classify(decomposed);
        Assert.Equal(AiUserIntent.ConsultativeQnA, intentDecomposed);
    }
}

