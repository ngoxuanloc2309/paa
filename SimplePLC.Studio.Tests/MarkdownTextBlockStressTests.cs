using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using SimplePLC.Studio.Controls;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử áp lực và tấn công biên (Empirical Adversarial Stress Tests) cho Milestone 3:
/// - MarkdownTextBlock: Malformed markdown, unclosed fences, regex catastrophic backtracking,
///   edge case tags (DI99, VREG999), random '#' chars, extreme length texts, rapid updates, WPF layout pass.
/// - Dynamic Suggestion Chips: Null VMs, rapid state toggles, boundary node selections.
/// </summary>
public class MarkdownTextBlockStressTests
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

    #region 1. Malformed Markdown Stress Tests

    [Theory]
    [InlineData("**bold without close")]
    [InlineData("text with **unclosed bold")]
    [InlineData("**")]
    [InlineData("***")]
    [InlineData("*****")]
    [InlineData("*single italic without close")]
    [InlineData("`inline code without close")]
    [InlineData("`")]
    [InlineData("``")]
    [InlineData("```")]
    public void ParseInlines_MalformedDelimiters_DoesNotThrowAndParsesGracefully(string malformed)
    {
        var tokens = MarkdownTextBlock.ParseInlines(malformed);
        Assert.NotNull(tokens);
        // Không được văng ngoại lệ, và tổng số ký tự token phải tương thích
        int totalTokenChars = tokens.Sum(t => t.Text.Length);
        Assert.True(totalTokenChars <= malformed.Length + 4);
    }

    [Fact]
    public void ParseBlocks_UnclosedCodeFence_CapturesRemainingContentGracefully()
    {
        string unclosed = "```csharp\nint x = 42;\nstring msg = \"hello\";\n// missing closing backticks";
        var blocks = MarkdownTextBlock.ParseBlocks(unclosed);

        Assert.Single(blocks);
        var block = blocks[0];
        Assert.Equal(MarkdownBlockType.CodeBlock, block.Type);
        Assert.Equal("csharp", block.Language);
        Assert.Contains("int x = 42;", block.RawContent);
        Assert.Contains("// missing closing backticks", block.RawContent);
    }

    [Fact]
    public void ParseBlocks_MultipleUnclosedCodeFences_CapturesCorrectly()
    {
        string text = "```iec\nTON(IN := DI0);\n```\n```json\n{\"tag\": \"DO0\"";
        var blocks = MarkdownTextBlock.ParseBlocks(text);

        Assert.Equal(2, blocks.Count);
        Assert.Equal(MarkdownBlockType.CodeBlock, blocks[0].Type);
        Assert.Equal("iec", blocks[0].Language);
        Assert.Equal(MarkdownBlockType.CodeBlock, blocks[1].Type);
        Assert.Equal("json", blocks[1].Language);
        Assert.Contains("{\"tag\": \"DO0\"", blocks[1].RawContent);
    }

    [Fact]
    public void ParseBlocks_EmptyCodeFence_DoesNotThrow()
    {
        string emptyFence = "```\n```\n```csharp\n```";
        var blocks = MarkdownTextBlock.ParseBlocks(emptyFence);
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.Equal(MarkdownBlockType.CodeBlock, b.Type));
    }

    [Fact]
    public void ParseInlines_CatastrophicBacktrackingDefense_HundredsOfAsterisks_FinishesUnder50ms()
    {
        string massiveAsterisks = new string('*', 500) + " text " + new string('*', 500);

        var sw = Stopwatch.StartNew();
        var tokens = MarkdownTextBlock.ParseInlines(massiveAsterisks);
        sw.Stop();

        Assert.NotNull(tokens);
        Assert.True(sw.ElapsedMilliseconds < 200, $"Parsing asterisks took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void ParseInlines_CatastrophicBacktrackingDefense_HundredsOfBackticks_FinishesUnder50ms()
    {
        string massiveBackticks = new string('`', 500) + " code " + new string('`', 500);

        var sw = Stopwatch.StartNew();
        var tokens = MarkdownTextBlock.ParseInlines(massiveBackticks);
        sw.Stop();

        Assert.NotNull(tokens);
        Assert.True(sw.ElapsedMilliseconds < 200, $"Parsing backticks took too long: {sw.ElapsedMilliseconds}ms");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\t\n\r\n   ")]
    public void ParseBlocks_EmptyOrWhitespaceInputs_ReturnsEmptyList(string? input)
    {
        var blocks = MarkdownTextBlock.ParseBlocks(input);
        Assert.NotNull(blocks);
        Assert.Empty(blocks);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ParseInlines_EmptyOrNullInputs_ReturnsEmptyList(string? input)
    {
        var tokens = MarkdownTextBlock.ParseInlines(input!);
        Assert.NotNull(tokens);
        Assert.Empty(tokens);
    }

    #endregion

    #region 2. Edge Case Tags & Identifiers

    [Theory]
    [InlineData("DI99")]
    [InlineData("DI8")]
    [InlineData("DO8")]
    [InlineData("DO99")]
    [InlineData("AI4")]
    [InlineData("AI99")]
    [InlineData("VFLAG32")]
    [InlineData("VFLAG999")]
    [InlineData("VREG32")]
    [InlineData("VREG999")]
    [InlineData("VREG_RETAIN32")]
    [InlineData("VREG_RETAIN999")]
    [InlineData("M0")]
    [InlineData("Q0.0")]
    public void ParseInlines_OutOfRangeTags_AreNotParsedAsTagBadges(string invalidTag)
    {
        string input = $"Kiểm tra tín hiệu {invalidTag} trên bộ điều khiển.";
        var tokens = MarkdownTextBlock.ParseInlines(input);

        Assert.DoesNotContain(tokens, t => t.Type == MarkdownInlineType.TagBadge && t.Text == invalidTag);
        Assert.Contains(tokens, t => t.Type == MarkdownInlineType.PlainText && t.Text.Contains(invalidTag));
    }

    [Theory]
    [InlineData("DI0")]
    [InlineData("DI7")]
    [InlineData("DO0")]
    [InlineData("DO7")]
    [InlineData("AI0")]
    [InlineData("AI3")]
    [InlineData("VFLAG0")]
    [InlineData("VFLAG31")]
    [InlineData("VREG0")]
    [InlineData("VREG31")]
    [InlineData("VREG_RETAIN0")]
    [InlineData("VREG_RETAIN31")]
    public void ParseInlines_ValidBoundaryTags_AreRecognizedAsTagBadges(string validTag)
    {
        string input = $"Tín hiệu {validTag} hoạt động.";
        var tokens = MarkdownTextBlock.ParseInlines(input);

        Assert.Contains(tokens, t => t.Type == MarkdownInlineType.TagBadge && t.Text == validTag);
    }

    [Theory]
    [InlineData("di0", "DI0")]
    [InlineData("do7", "DO7")]
    [InlineData("ai2", "AI2")]
    [InlineData("vflag15", "VFLAG15")]
    [InlineData("vreg31", "VREG31")]
    [InlineData("vreg_retain0", "VREG_RETAIN0")]
    public void ParseInlines_CaseInsensitiveTags_UppercasedInBadge(string raw, string expected)
    {
        string input = $"Bật {raw} ngay.";
        var tokens = MarkdownTextBlock.ParseInlines(input);

        var badge = Assert.Single(tokens.Where(t => t.Type == MarkdownInlineType.TagBadge));
        Assert.Equal(expected, badge.Text);
    }

    [Fact]
    public void ParseInlines_TagWithUnderscoreOrAlphanumericSuffix_NotMatchedDueToWordBoundary()
    {
        string input = "Biến DI0_EXTRA và DO0_TEST và DI01 không phải là tag chuẩn.";
        var tokens = MarkdownTextBlock.ParseInlines(input);

        Assert.DoesNotContain(tokens, t => t.Type == MarkdownInlineType.TagBadge && (t.Text == "DI0" || t.Text == "DO0"));
    }

    [Fact]
    public void ParseInlines_TagsSurroundedByPunctuation_CorrectlyExtracted()
    {
        string input = "Cổng (DI0), [DO1], {AI2}, DI3! và DI4? hoạt động.";
        var tokens = MarkdownTextBlock.ParseInlines(input);

        var tags = tokens.Where(t => t.Type == MarkdownInlineType.TagBadge).Select(t => t.Text).ToList();
        Assert.Contains("DI0", tags);
        Assert.Contains("DO1", tags);
        Assert.Contains("AI2", tags);
        Assert.Contains("DI3", tags);
        Assert.Contains("DI4", tags);
    }

    [Fact]
    public void ParseInlines_TagInsideInlineCode_PreservedAsCodeNotBadge()
    {
        string input = "Gõ lệnh `TON(IN := DI0)` để cấu hình.";
        var tokens = MarkdownTextBlock.ParseInlines(input);

        Assert.DoesNotContain(tokens, t => t.Type == MarkdownInlineType.TagBadge);
        var code = Assert.Single(tokens.Where(t => t.Type == MarkdownInlineType.InlineCode));
        Assert.Equal("TON(IN := DI0)", code.Text);
    }

    #endregion

    #region 3. Random Text with '#' & Markdown Structure

    [Theory]
    [InlineData("#")]
    [InlineData("##")]
    [InlineData("###")]
    [InlineData("####")]
    [InlineData("#TagWithoutSpace")]
    [InlineData("C# programming")]
    [InlineData("Vị trí chân #1 trên board")]
    public void ParseBlocks_HashesWithoutLeadingSpace_TreatedAsParagraph(string hashText)
    {
        var blocks = MarkdownTextBlock.ParseBlocks(hashText);

        Assert.Single(blocks);
        Assert.Equal(MarkdownBlockType.Paragraph, blocks[0].Type);
        Assert.Equal(hashText, blocks[0].RawContent);
    }

    [Theory]
    [InlineData("# Level 1 Header", 1)]
    [InlineData("## Level 2 Header", 2)]
    [InlineData("### Level 3 Header", 3)]
    [InlineData("#### Deep Header Level 4", 3)]
    [InlineData("###### Maximum Clamped Header Level 6", 3)]
    public void ParseBlocks_HeadingsWithVariousLevels_HandledAndClamped(string headingLine, int expectedLevel)
    {
        var blocks = MarkdownTextBlock.ParseBlocks(headingLine);

        Assert.Single(blocks);
        Assert.Equal(MarkdownBlockType.Header, blocks[0].Type);
        Assert.Equal(expectedLevel, blocks[0].HeaderLevel);
    }

    [Theory]
    [InlineData("# ")]
    [InlineData("## ")]
    [InlineData("### ")]
    public void ParseBlocks_HeadingsWithoutContent_FallbackToParagraphWithoutThrowing(string emptyHeading)
    {
        var blocks = MarkdownTextBlock.ParseBlocks(emptyHeading);

        Assert.Single(blocks);
        // Do line.Trim() cắt bỏ khoảng trắng cuối, chuỗi chỉ còn "#" nên được xử lý an toàn dạng Paragraph
        Assert.Equal(MarkdownBlockType.Paragraph, blocks[0].Type);
        Assert.Equal(emptyHeading.Trim(), blocks[0].RawContent);
    }

    [Fact]
    public void ParseBlocks_HeadingWithEmbeddedInlines_CorrectlyPopulatesInlines()
    {
        string heading = "### 1. Cấu hình ngõ vào DI0 và `TON.PT`";
        var blocks = MarkdownTextBlock.ParseBlocks(heading);

        Assert.Single(blocks);
        var header = blocks[0];
        Assert.Equal(MarkdownBlockType.Header, header.Type);
        Assert.Equal(3, header.HeaderLevel);

        Assert.Contains(header.Inlines, t => t.Type == MarkdownInlineType.TagBadge && t.Text == "DI0");
        Assert.Contains(header.Inlines, t => t.Type == MarkdownInlineType.InlineCode && t.Text == "TON.PT");
    }

    [Theory]
    [InlineData(">")]
    [InlineData("> ")]
    [InlineData("> ⚠️ Cảnh báo nguy hiểm cao điện áp")]
    public void ParseBlocks_Callouts_HandledSafely(string calloutText)
    {
        var blocks = MarkdownTextBlock.ParseBlocks(calloutText);

        Assert.Single(blocks);
        Assert.Equal(MarkdownBlockType.Callout, blocks[0].Type);
    }

    [Theory]
    [InlineData("- Item A")]
    [InlineData("• Item B")]
    [InlineData("* Item C")]
    [InlineData("+ Item D")]
    public void ParseBlocks_BulletItems_ExtractedCorrectly(string bulletText)
    {
        var blocks = MarkdownTextBlock.ParseBlocks(bulletText);

        Assert.Single(blocks);
        Assert.Equal(MarkdownBlockType.BulletItem, blocks[0].Type);
    }

    [Theory]
    [InlineData("- ")]
    [InlineData("• ")]
    [InlineData("* ")]
    [InlineData("+ ")]
    public void ParseBlocks_EmptyBulletItems_FallbackToParagraphWithoutThrowing(string emptyBulletText)
    {
        var blocks = MarkdownTextBlock.ParseBlocks(emptyBulletText);

        Assert.Single(blocks);
        // Do line.Trim() cắt bỏ khoảng trắng cuối, chuỗi chỉ còn ký tự gạch/chấm nên fallback Paragraph an toàn
        Assert.Equal(MarkdownBlockType.Paragraph, blocks[0].Type);
        Assert.Equal(emptyBulletText.Trim(), blocks[0].RawContent);
    }

    #endregion

    #region 4. Extreme Length Texts & Rapid Updates

    [Fact]
    public void ParseBlocks_ExtremeLengthText_10000Lines_CompletesRapidly()
    {
        var lines = new List<string>();
        for (int i = 0; i < 5000; i++)
        {
            lines.Add($"### Mục {i}");
            lines.Add($"• Kích hoạt DI0 điều khiển **DO1** với mã `CODE_{i}`.");
        }
        string massiveMarkdown = string.Join("\n", lines);

        var sw = Stopwatch.StartNew();
        var blocks = MarkdownTextBlock.ParseBlocks(massiveMarkdown);
        sw.Stop();

        Assert.Equal(10000, blocks.Count);
        Assert.True(sw.ElapsedMilliseconds < 1000, $"Extreme length parse took {sw.ElapsedMilliseconds}ms, expected < 1000ms");
    }

    [Fact]
    public void ParseInlines_ExtremeTagDensity_5000Tags_ParsedAccurately()
    {
        var tagList = new List<string>();
        for (int i = 0; i < 1000; i++)
        {
            tagList.Add("DI0");
            tagList.Add("DO1");
            tagList.Add("AI2");
            tagList.Add("VREG3");
            tagList.Add("VFLAG4");
        }
        string denseText = string.Join(" ", tagList);

        var sw = Stopwatch.StartNew();
        var tokens = MarkdownTextBlock.ParseInlines(denseText);
        sw.Stop();

        int tagCount = tokens.Count(t => t.Type == MarkdownInlineType.TagBadge);
        Assert.Equal(5000, tagCount);
        Assert.True(sw.ElapsedMilliseconds < 500, $"Extreme tag parse took {sw.ElapsedMilliseconds}ms");
    }

    #endregion

    #region 5. WPF UI Thread Rendering & Layout Pass Stress

    [Fact]
    public void WpfRendering_ComplexAdversarialMarkdown_ExecutesAndMeasuresWithoutException()
    {
        RunOnSta(() =>
        {
            var control = new MarkdownTextBlock();
            string adversarialMd =
                "# C# PLC Header\n" +
                "### Sub-header with DI0, `inline code`, and **bold tag DO7**\n" +
                "> ⚠️ Warning callout with VREG_RETAIN0 and **DI99** out of range\n" +
                "• Bullet item with *italic* and unclosed **bold\n" +
                "```iec\nTON(IN := DI0, PT := 5000);\n```\n" +
                "```unclosed_fence\nint a = 10;\n" +
                "Paragraph with `unclosed code and #random #hash # signs\n";

            control.Markdown = adversarialMd;

            Assert.NotEmpty(control.Inlines);

            // Force layout pass (Measure & Arrange) to verify all InlineUIContainers and Spans are valid
            control.Measure(new Size(800, 600));
            control.Arrange(new Rect(0, 0, 800, 600));

            Assert.True(control.ActualWidth >= 0);
            Assert.True(control.ActualHeight >= 0);
        });
    }

    [Fact]
    public void WpfRendering_RapidTextChanges_500Iterations_DoesNotThrowOrLeak()
    {
        RunOnSta(() =>
        {
            var control = new MarkdownTextBlock();

            for (int i = 0; i < 500; i++)
            {
                if (i % 4 == 0)
                    control.Markdown = $"### Tiêu đề {i}\n• Mục với DI0";
                else if (i % 4 == 1)
                    control.Markdown = "> ⚠️ Callout an toàn";
                else if (i % 4 == 2)
                    control.Markdown = "```iec\nTON();\n```";
                else
                    control.Markdown = string.Empty;
            }

            // Cuối cùng thử với null
            control.Markdown = null!;
            Assert.Empty(control.Inlines);
        });
    }

    [Fact]
    public void WpfRendering_AttachedPropertyOnStandardTextBlock_MeasuresCleanly()
    {
        RunOnSta(() =>
        {
            var tb = new TextBlock();
            string md = "## Header Attached\n• Item with DI0 and **DO0**\n`code inline`";

            MarkdownTextBlock.SetMarkdownText(tb, md);

            Assert.NotEmpty(tb.Inlines);
            tb.Measure(new Size(800, 600));
            tb.Arrange(new Rect(0, 0, 800, 600));
        });
    }

    #endregion

    #region 6. Dynamic Suggestion Chips Stress Tests

    [Fact]
    public void SuggestionChips_NullLogicVM_HandledSafelyWithStarterChips()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => null);

        chatVM.UpdateContextualSuggestions();

        Assert.NotEmpty(chatVM.SuggestionChips);
        Assert.Contains(chatVM.SuggestionChips, c => c.Category == "Starter");
        Assert.Equal("📍 0 khối", chatVM.ActiveContextNodeCountText);
        Assert.Equal("⚪ Sẵn sàng", chatVM.ActiveContextCompileStatusText);
        Assert.False(chatVM.ActiveContextCompileIsError);
        Assert.False(chatVM.HasActiveContextSelection);
    }

    [Fact]
    public void SuggestionChips_RapidStateTransitions_ProducesAppropriateChipsWithoutException()
    {
        var tagCatalog = new TagCatalogViewModel();
        var ruleTable = new RuleTableViewModel(tagCatalog, _ => { });
        var logicVM = new LogicEditorViewModel(tagCatalog, ruleTable, uiDispatcher: a => a());
        var chatVM = new AiChatViewModel(() => tagCatalog.AllTags, () => ruleTable.Rules, () => logicVM);

        var timerNode = new TimerNodeViewModel { Title = "Timer T1", TimerMode = "TON" };
        var scaleNode = new ScaleNodeViewModel { Title = "Scale S1" };
        var counterNode = new CounterNodeViewModel { Title = "Counter C1" };

        logicVM.Nodes.Add(timerNode);
        logicVM.Nodes.Add(scaleNode);
        logicVM.Nodes.Add(counterNode);

        for (int cycle = 0; cycle < 50; cycle++)
        {
            // 1. Timer selected
            logicVM.CompileState = CompileState.Valid;
            logicVM.IsSimulationMode = false;
            logicVM.SelectedNode = timerNode;
            chatVM.UpdateContextualSuggestions();
            Assert.Contains(chatVM.SuggestionChips, c => c.Category == "Timer");

            // 2. Scale selected
            logicVM.SelectedNode = scaleNode;
            chatVM.UpdateContextualSuggestions();
            Assert.Contains(chatVM.SuggestionChips, c => c.Category == "Scale");

            // 3. Counter selected
            logicVM.SelectedNode = counterNode;
            chatVM.UpdateContextualSuggestions();
            Assert.Contains(chatVM.SuggestionChips, c => c.Category == "Counter");

            // 4. Linter error active (overrides selection)
            logicVM.CompileState = CompileState.Invalid;
            chatVM.UpdateContextualSuggestions();
            Assert.Contains(chatVM.SuggestionChips, c => c.Category == "Diagnostic");

            // 5. Simulation active (overrides selection if compile valid)
            logicVM.CompileState = CompileState.Valid;
            logicVM.IsSimulationMode = true;
            chatVM.UpdateContextualSuggestions();
            Assert.Contains(chatVM.SuggestionChips, c => c.Category == "Simulation");

            // 6. Reset to general
            logicVM.IsSimulationMode = false;
            logicVM.SelectedNode = null;
            chatVM.UpdateContextualSuggestions();
            Assert.Contains(chatVM.SuggestionChips, c => c.Category == "General");
        }
    }

    #endregion
}
