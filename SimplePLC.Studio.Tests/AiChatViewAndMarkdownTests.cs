using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using SimplePLC.Studio.Controls;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử xác thực toàn diện Milestone 3 (M3):
/// 1. Bộ phân tích và hiển thị Markdown chuẩn công nghiệp (MarkdownTextBlock)
/// 2. Cơ chế sinh chip gợi ý ngữ cảnh thời gian thực (Dynamic Contextual Suggestions)
/// 3. Khung nhập liệu đa dòng (Multiline Prompt) và mô hình AiSuggestionChip
/// </summary>
public class AiChatViewAndMarkdownTests
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

    #region 1. Markdown Parsing Tests (Zero-Dependency Parser)

    [Fact]
    public void MarkdownTextBlock_ParseBlocks_HeadingsLevels123_ExtractedCorrectly()
    {
        string md = "# Tiêu đề Cấp 1\n## Tiêu đề Cấp 2\n### Tiêu đề Cấp 3";
        var blocks = MarkdownTextBlock.ParseBlocks(md);

        Assert.Equal(3, blocks.Count);

        Assert.Equal(MarkdownBlockType.Header, blocks[0].Type);
        Assert.Equal(1, blocks[0].HeaderLevel);
        Assert.Equal("Tiêu đề Cấp 1", blocks[0].RawContent);

        Assert.Equal(MarkdownBlockType.Header, blocks[1].Type);
        Assert.Equal(2, blocks[1].HeaderLevel);
        Assert.Equal("Tiêu đề Cấp 2", blocks[1].RawContent);

        Assert.Equal(MarkdownBlockType.Header, blocks[2].Type);
        Assert.Equal(3, blocks[2].HeaderLevel);
        Assert.Equal("Tiêu đề Cấp 3", blocks[2].RawContent);
    }

    [Fact]
    public void MarkdownTextBlock_ParseBlocks_BulletLists_ExtractedCorrectly()
    {
        string md = "• Khởi động hệ thống\n- Kiểm tra an toàn E-Stop\n* Đóng van xả DO0\n+ Cấp nguồn biến tần";
        var blocks = MarkdownTextBlock.ParseBlocks(md);

        Assert.Equal(4, blocks.Count);
        foreach (var block in blocks)
        {
            Assert.Equal(MarkdownBlockType.BulletItem, block.Type);
        }

        Assert.Equal("Khởi động hệ thống", blocks[0].RawContent);
        Assert.Equal("Kiểm tra an toàn E-Stop", blocks[1].RawContent);
        Assert.Equal("Đóng van xả DO0", blocks[2].RawContent);
        Assert.Equal("Cấp nguồn biến tần", blocks[3].RawContent);
    }

    [Fact]
    public void MarkdownTextBlock_ParseBlocks_Callouts_ExtractedCorrectly()
    {
        string md = "> ⚠️ Cảnh báo an toàn: Cần ngắt nguồn trước khi bảo trì!\n> Lưu ý kỹ thuật điều khiển";
        var blocks = MarkdownTextBlock.ParseBlocks(md);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(MarkdownBlockType.Callout, blocks[0].Type);
        Assert.Contains("⚠️ Cảnh báo an toàn", blocks[0].RawContent);

        Assert.Equal(MarkdownBlockType.Callout, blocks[1].Type);
        Assert.Equal("Lưu ý kỹ thuật điều khiển", blocks[1].RawContent);
    }

    [Fact]
    public void MarkdownTextBlock_ParseBlocks_CodeBlocks_ExtractedWithLanguageAndContent()
    {
        string md = "```iec\nTON(IN := DI0, PT := T#5s);\n```\n\n```json\n[{\"tag\": \"DO0\"}]\n```";
        var blocks = MarkdownTextBlock.ParseBlocks(md);

        Assert.Equal(2, blocks.Count);

        Assert.Equal(MarkdownBlockType.CodeBlock, blocks[0].Type);
        Assert.Equal("iec", blocks[0].Language);
        Assert.Equal("TON(IN := DI0, PT := T#5s);", blocks[0].RawContent);

        Assert.Equal(MarkdownBlockType.CodeBlock, blocks[1].Type);
        Assert.Equal("json", blocks[1].Language);
        Assert.Equal("[{\"tag\": \"DO0\"}]", blocks[1].RawContent);
    }

    [Fact]
    public void MarkdownTextBlock_ParseInlines_BoldAndItalic_ParsedCorrectly()
    {
        string text = "Hệ thống đang **BẬT** và ở chế độ *Tự động*.";
        var tokens = MarkdownTextBlock.ParseInlines(text);

        Assert.Contains(tokens, t => t.Type == MarkdownInlineType.Bold && t.Text == "BẬT");
        Assert.Contains(tokens, t => t.Type == MarkdownInlineType.Italic && t.Text == "Tự động");
        Assert.Contains(tokens, t => t.Type == MarkdownInlineType.PlainText && t.Text.Contains("Hệ thống đang "));
    }

    [Fact]
    public void MarkdownTextBlock_ParseInlines_InlineCode_ParsedCorrectly()
    {
        string text = "Sử dụng lệnh `TON.PT := 5000` để đặt thời gian.";
        var tokens = MarkdownTextBlock.ParseInlines(text);

        var codeToken = Assert.Single(tokens.Where(t => t.Type == MarkdownInlineType.InlineCode));
        Assert.Equal("TON.PT := 5000", codeToken.Text);
    }

    [Fact]
    public void MarkdownTextBlock_ParseInlines_PlcTagBadges_RecognizesAllStandardTags()
    {
        string text = "Ngõ vào DI0, DI7 nối tới DO0 và DO7. Cảm biến AI0, AI3. Biến cờ VFLAG0, VFLAG31. Biến nhớ VREG0, VREG_RETAIN0.";
        var tokens = MarkdownTextBlock.ParseInlines(text);

        var tagTokens = tokens.Where(t => t.Type == MarkdownInlineType.TagBadge).Select(t => t.Text).ToList();

        Assert.Contains("DI0", tagTokens);
        Assert.Contains("DI7", tagTokens);
        Assert.Contains("DO0", tagTokens);
        Assert.Contains("DO7", tagTokens);
        Assert.Contains("AI0", tagTokens);
        Assert.Contains("AI3", tagTokens);
        Assert.Contains("VFLAG0", tagTokens);
        Assert.Contains("VFLAG31", tagTokens);
        Assert.Contains("VREG0", tagTokens);
        Assert.Contains("VREG_RETAIN0", tagTokens);
    }

    [Fact]
    public void MarkdownTextBlock_ParseInlines_TagInsideBold_RecognizedAsTagBadge()
    {
        string text = "Kích hoạt ngõ ra **DO0** khi nút nhấn **DI0** bật.";
        var tokens = MarkdownTextBlock.ParseInlines(text);

        var tagTokens = tokens.Where(t => t.Type == MarkdownInlineType.TagBadge).Select(t => t.Text).ToList();
        Assert.Contains("DO0", tagTokens);
        Assert.Contains("DI0", tagTokens);
    }

    [Fact]
    public void MarkdownTextBlock_ParseInlines_ComplexProse_SequencePreserved()
    {
        string text = "Khi nhấn nút DI0 ➔ Bật ngõ ra **DO0** và kiểm tra `TON.Q`.";
        var tokens = MarkdownTextBlock.ParseInlines(text);

        Assert.NotEmpty(tokens);
        Assert.Equal(MarkdownInlineType.PlainText, tokens[0].Type);
        Assert.Equal("Khi nhấn nút ", tokens[0].Text);

        Assert.Equal(MarkdownInlineType.TagBadge, tokens[1].Type);
        Assert.Equal("DI0", tokens[1].Text);

        Assert.Equal(MarkdownInlineType.PlainText, tokens[2].Type);

        Assert.Equal(MarkdownInlineType.TagBadge, tokens[3].Type);
        Assert.Equal("DO0", tokens[3].Text);

        Assert.Equal(MarkdownInlineType.PlainText, tokens[4].Type);

        Assert.Equal(MarkdownInlineType.InlineCode, tokens[5].Type);
        Assert.Equal("TON.Q", tokens[5].Text);
    }

    #endregion

    #region 2. WPF Markdown Rendering Tests

    [Fact]
    public void MarkdownTextBlock_WpfRendering_PopulatesRichInlines()
    {
        RunOnSta(() =>
        {
            var control = new MarkdownTextBlock();
            string md = "### 1. Nguyên lý hoạt động\n• Khi nhấn DI0 thì bật **DO0**\n`TON.PT := 5000`\n> ⚠️ Chú ý an toàn";

            control.Markdown = md;

            Assert.NotEmpty(control.Inlines);
            Assert.Contains(control.Inlines, i => i is Span);
            Assert.Contains(control.Inlines, i => i is InlineUIContainer);
        });
    }

    [Fact]
    public void MarkdownTextBlock_WpfRendering_EmptyOrNull_ClearsInlines()
    {
        RunOnSta(() =>
        {
            var control = new MarkdownTextBlock();
            control.Markdown = "### Test";
            Assert.NotEmpty(control.Inlines);

            control.Markdown = string.Empty;
            Assert.Empty(control.Inlines);

            control.Markdown = null!;
            Assert.Empty(control.Inlines);
        });
    }

    [Fact]
    public void MarkdownTextBlock_AttachedProperty_PopulatesStandardTextBlock()
    {
        RunOnSta(() =>
        {
            var tb = new TextBlock();
            MarkdownTextBlock.SetMarkdownText(tb, "# Tiêu đề chính\n• Mục 1");

            Assert.NotEmpty(tb.Inlines);
            Assert.Equal("# Tiêu đề chính\n• Mục 1", MarkdownTextBlock.GetMarkdownText(tb));
        });
    }

    #endregion

    #region 3. Dynamic Contextual Suggestions Tests

    [Fact]
    public void AiChatViewModel_ContextualSuggestions_EmptyCanvas_ProducesStarterChips()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        chatVM.UpdateContextualSuggestions();

        Assert.NotEmpty(chatVM.SuggestionChips);
        Assert.Contains(chatVM.SuggestionChips, c => c.Category == "Starter");
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Start / Stop"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Đếm sản lượng"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Scale 4-20mA"));
    }

    [Fact]
    public void AiChatViewModel_ContextualSuggestions_TimerSelected_ProducesTimerChips()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        // Thêm khối Timer và chọn khối
        var timerNode = new TimerNodeViewModel { Title = "Timer TON (T1)", TimerMode = "TON" };
        logicVM.Nodes.Add(timerNode);
        logicVM.SelectedNode = timerNode;

        chatVM.UpdateContextualSuggestions();

        Assert.NotEmpty(chatVM.SuggestionChips);
        Assert.All(chatVM.SuggestionChips, c => Assert.Equal("Timer", c.Category));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Cấu hình trễ"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("TON / TOF"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Kiểm tra dây nối Q"));

        Assert.True(chatVM.HasActiveContextSelection);
        Assert.Contains("Timer TON", chatVM.ActiveContextSelectionText);
    }

    [Fact]
    public void AiChatViewModel_ContextualSuggestions_ScaleSelected_ProducesScaleChips()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        // Thêm khối Scale và chọn khối
        var scaleNode = new ScaleNodeViewModel { Title = "Scale Analog (SC1)" };
        logicVM.Nodes.Add(scaleNode);
        logicVM.SelectedNode = scaleNode;

        chatVM.UpdateContextualSuggestions();

        Assert.NotEmpty(chatVM.SuggestionChips);
        Assert.All(chatVM.SuggestionChips, c => Assert.Equal("Scale", c.Category));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Calib 4-20mA"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Đổi đơn vị"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Clamp"));

        Assert.True(chatVM.HasActiveContextSelection);
        Assert.Contains("Scale Analog", chatVM.ActiveContextSelectionText);
    }

    [Fact]
    public void AiChatViewModel_ContextualSuggestions_CounterSelected_ProducesCounterChips()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        var counterNode = new CounterNodeViewModel { Title = "Counter CTU (C1)" };
        logicVM.Nodes.Add(counterNode);
        logicVM.SelectedNode = counterNode;

        chatVM.UpdateContextualSuggestions();

        Assert.NotEmpty(chatVM.SuggestionChips);
        Assert.All(chatVM.SuggestionChips, c => Assert.Equal("Counter", c.Category));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Cấu hình đếm"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("CTU / CTD"));

        Assert.True(chatVM.HasActiveContextSelection);
    }

    [Fact]
    public void AiChatViewModel_ContextualSuggestions_CompilerErrors_PrioritizesDiagnosticChips()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        // Đặt trạng thái biên dịch lỗi
        logicVM.CompileState = CompileState.Invalid;

        chatVM.UpdateContextualSuggestions();

        Assert.NotEmpty(chatVM.SuggestionChips);
        Assert.All(chatVM.SuggestionChips, c => Assert.Equal("Diagnostic", c.Category));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Khắc phục lỗi biên dịch"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Giải thích cổng thiếu dây"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Kiểm tra xung đột ngõ ra"));

        Assert.True(chatVM.ActiveContextCompileIsError);
        Assert.Equal("⚠️ Lỗi sơ đồ", chatVM.ActiveContextCompileStatusText);
    }

    [Fact]
    public void AiChatViewModel_ContextualSuggestions_SimulationActive_ProducesSimulationChips()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        // Kích hoạt chế độ mô phỏng
        logicVM.IsSimulationMode = true;

        chatVM.UpdateContextualSuggestions();

        Assert.NotEmpty(chatVM.SuggestionChips);
        Assert.All(chatVM.SuggestionChips, c => Assert.Equal("Simulation", c.Category));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Giám sát ngõ ra DO"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Thử nghiệm cảm biến AI0"));
        Assert.Contains(chatVM.SuggestionChips, c => c.Text.Contains("Kiểm tra thời gian đáp ứng"));
    }

    [Fact]
    public void AiChatViewModel_ActiveContextIndicators_ReflectsCanvasAndTagState()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        // Thêm 2 khối logic vào canvas
        var inNode = new InputNodeViewModel { Title = "Input DI0" };
        var actNode = new ActionNodeViewModel { Title = "Action DO0" };
        logicVM.Nodes.Add(inNode);
        logicVM.Nodes.Add(actNode);

        chatVM.UpdateContextualSuggestions();

        Assert.Contains("2 khối", chatVM.ActiveContextNodeCountText);
        Assert.Contains("Tags", chatVM.ActiveContextTagCountText);
    }

    #endregion

    #region 4. Multiline Input Prompt & SuggestionChip Model Tests

    [Fact]
    public async Task AiChatViewModel_MultilinePrompt_SendsCorrectlyAndClearsInput()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        string receivedPrompt = string.Empty;
        chatVM.ApiHandler = (settings, instruction, history, tools, ct) =>
        {
            var lastUser = history.LastOrDefault(h => h.role == "user");
            receivedPrompt = lastUser.text;
            return Task.FromResult(new GeminiToolResponse("### Phản hồi mô phỏng\nĐã tiếp nhận yêu cầu điều khiển.", new List<AiToolCall>()));
        };

        string multiline = "Yêu cầu kỹ thuật:\n1. Bật DO0 khi nhấn DI0\n2. Tắt DO0 khi nhấn DI1";
        chatVM.InputText = multiline;

        await chatVM.SendMessageAsync();

        Assert.Empty(chatVM.InputText);
        Assert.Contains(multiline, receivedPrompt);
        Assert.True(chatVM.Messages.Count >= 2);
    }

    [Fact]
    public async Task AiChatViewModel_UseQuickPrompt_ExecutesWithChipPrompt()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        string receivedPrompt = string.Empty;
        chatVM.ApiHandler = (settings, instruction, history, tools, ct) =>
        {
            var lastUser = history.LastOrDefault(h => h.role == "user");
            receivedPrompt = lastUser.text;
            return Task.FromResult(new GeminiToolResponse("Giải pháp cho mạch Start/Stop bơm.", new List<AiToolCall>()));
        };

        string prompt = "Mạch Start/Stop bật tắt bơm DO0 với nút nhấn DI0 (Start) và DI1 (Stop)";
        await chatVM.UseQuickPromptCommand.ExecuteAsync(prompt);

        Assert.Contains(prompt, receivedPrompt);
        Assert.Empty(chatVM.InputText);
    }

    [Fact]
    public void AiSuggestionChip_ModelProperties_InitializedCorrectly()
    {
        var chip = new AiSuggestionChip(
            "Start / Stop", "⚡",
            "Mạch bật tắt bơm DO0",
            null,
            "Tooltip hướng dẫn",
            "Starter");

        Assert.Equal("Start / Stop", chip.Text);
        Assert.Equal("⚡", chip.Icon);
        Assert.Equal("Mạch bật tắt bơm DO0", chip.Prompt);
        Assert.Equal("Tooltip hướng dẫn", chip.Tooltip);
        Assert.Equal("Starter", chip.Category);
        Assert.Equal("⚡ Start / Stop", chip.DisplayText);

        var chipNoIcon = new AiSuggestionChip { Text = "Kiểm tra lỗi" };
        Assert.Equal("Kiểm tra lỗi", chipNoIcon.DisplayText);
    }

    #endregion
}
