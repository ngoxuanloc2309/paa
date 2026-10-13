using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace SimplePLC.Studio.Controls;

public enum MarkdownBlockType
{
    Paragraph,
    Header,
    BulletItem,
    CodeBlock,
    Callout
}

public class MarkdownBlock
{
    public MarkdownBlockType Type { get; set; }
    public int HeaderLevel { get; set; } = 1;
    public string Language { get; set; } = string.Empty;
    public string RawContent { get; set; } = string.Empty;
    public List<MarkdownInlineToken> Inlines { get; set; } = new();
}

public enum MarkdownInlineType
{
    PlainText,
    Bold,
    Italic,
    InlineCode,
    TagBadge
}

public class MarkdownInlineToken
{
    public MarkdownInlineType Type { get; set; }
    public string Text { get; set; } = string.Empty;

    public MarkdownInlineToken(MarkdownInlineType type, string text)
    {
        Type = type;
        Text = text;
    }

    public override string ToString() => $"[{Type}: {Text}]";
}

/// <summary>
/// WPF Control hiển thị văn bản Markdown kỹ thuật chuẩn công nghiệp, không phụ thuộc thư viện ngoài (Zero-Dependency).
/// Hỗ trợ: Headings (#, ##, ###), Bold (**), Italic (*), Inline Code (`...`), PLC Tag Badges (DI0..7, DO0..7, AI0..3, VFLAG, VREG),
/// Bullet lists (•, -, *), Code Blocks (```iec/json/csharp) và Callouts cảnh báo an toàn (> ⚠️).
/// </summary>
public class MarkdownTextBlock : TextBlock
{
    private static readonly Regex TagRegex = new(
        @"^(?:DI[0-7]|DO[0-7]|AI[0-3]|VFLAG(?:[0-9]|[12][0-9]|3[01])|VREG(?:[0-9]|[12][0-9]|3[01])|VREG_RETAIN(?:[0-9]|[12][0-9]|3[01]))$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex InlineCombinedRegex = new(
        @"(?<code>`[^`]+`)|(?<bold>\*\*[^*]+\*\*)|(?<italic>(?<!\*)\*[^*]+\*(?!\*))|(?<tag>\b(?:DI[0-7]|DO[0-7]|AI[0-3]|VFLAG(?:[0-9]|[12][0-9]|3[01])|VREG(?:[0-9]|[12][0-9]|3[01])|VREG_RETAIN(?:[0-9]|[12][0-9]|3[01]))\b)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static readonly DependencyProperty MarkdownProperty =
        DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownTextBlock),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender, OnMarkdownChanged));

    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.RegisterAttached(
            "MarkdownText",
            typeof(string),
            typeof(MarkdownTextBlock),
            new FrameworkPropertyMetadata(string.Empty, OnMarkdownTextChanged));

    public static string GetMarkdownText(DependencyObject obj) =>
        (string)obj.GetValue(MarkdownTextProperty);

    public static void SetMarkdownText(DependencyObject obj, string value) =>
        obj.SetValue(MarkdownTextProperty, value);

    private static void OnMarkdownChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownTextBlock control)
        {
            control.RenderMarkdown((string)e.NewValue);
        }
    }

    private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownTextBlock mtb)
        {
            mtb.Markdown = (string)e.NewValue;
        }
        else if (d is TextBlock tb)
        {
            RenderMarkdownToTextBlock(tb, (string)e.NewValue);
        }
    }

    public MarkdownTextBlock()
    {
        TextWrapping = TextWrapping.Wrap;
    }

    public void RenderMarkdown(string? markdown)
    {
        if (Dispatcher != null && !Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => RenderMarkdown(markdown));
            return;
        }

        RenderMarkdownToTextBlock(this, markdown);
    }

    public static void RenderMarkdownToTextBlock(TextBlock target, string? markdown)
    {
        target.Inlines.Clear();
        if (string.IsNullOrWhiteSpace(markdown)) return;

        var blocks = ParseBlocks(markdown);
        bool isFirst = true;

        foreach (var block in blocks)
        {
            switch (block.Type)
            {
                case MarkdownBlockType.Header:
                    if (!isFirst) target.Inlines.Add(new LineBreak());
                    var headerSpan = new Span
                    {
                        FontWeight = FontWeights.Bold,
                        FontSize = block.HeaderLevel switch
                        {
                            1 => 14,
                            2 => 13,
                            _ => 12
                        },
                        Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A))
                    };
                    PopulateInlines(headerSpan.Inlines, block.Inlines);
                    target.Inlines.Add(headerSpan);
                    target.Inlines.Add(new LineBreak());
                    break;

                case MarkdownBlockType.BulletItem:
                    var bulletSpan = new Span();
                    bulletSpan.Inlines.Add(new Run("  • ")
                    {
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
                    });
                    PopulateInlines(bulletSpan.Inlines, block.Inlines);
                    target.Inlines.Add(bulletSpan);
                    target.Inlines.Add(new LineBreak());
                    break;

                case MarkdownBlockType.Callout:
                    if (!isFirst) target.Inlines.Add(new LineBreak());
                    var calloutBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0xFF, 0xFB, 0xEB)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0xC0, 0x56, 0x21)),
                        BorderThickness = new Thickness(3, 0, 0, 0),
                        CornerRadius = new CornerRadius(0, 2, 2, 0),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(0, 4, 0, 4)
                    };
                    var calloutTb = new TextBlock
                    {
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x35, 0x0F)),
                        FontSize = 11.5
                    };
                    PopulateInlines(calloutTb.Inlines, block.Inlines);
                    calloutBorder.Child = calloutTb;
                    target.Inlines.Add(new InlineUIContainer(calloutBorder));
                    target.Inlines.Add(new LineBreak());
                    break;

                case MarkdownBlockType.CodeBlock:
                    if (!isFirst) target.Inlines.Add(new LineBreak());
                    var codeBorder = CreateCodeBlockUI(block.Language, block.RawContent);
                    target.Inlines.Add(new InlineUIContainer(codeBorder));
                    target.Inlines.Add(new LineBreak());
                    break;

                case MarkdownBlockType.Paragraph:
                default:
                    var paraSpan = new Span();
                    PopulateInlines(paraSpan.Inlines, block.Inlines);
                    target.Inlines.Add(paraSpan);
                    target.Inlines.Add(new LineBreak());
                    break;
            }
            isFirst = false;
        }

        // Loại bỏ LineBreak thừa ở cuối cùng để căn lề thẻ card gọn gàng
        if (target.Inlines.LastInline is LineBreak)
        {
            target.Inlines.Remove(target.Inlines.LastInline);
        }
    }

    private static Border CreateCodeBlockUI(string language, string code)
    {
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 4, 0, 4)
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        if (!string.IsNullOrWhiteSpace(language))
        {
            var headerDock = new DockPanel { Margin = new Thickness(0, 0, 0, 4), LastChildFill = false };
            var langLabel = new TextBlock
            {
                Text = language.ToUpperInvariant(),
                FontSize = 9.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8))
            };
            DockPanel.SetDock(langLabel, Dock.Left);
            headerDock.Children.Add(langLabel);

            var copyBtn = new TextBlock
            {
                Text = "📋 Copy",
                FontSize = 9.5,
                Foreground = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Sao chép mã vào Clipboard"
            };
            copyBtn.MouseDown += (s, e) =>
            {
                try { Clipboard.SetText(code); } catch { }
            };
            DockPanel.SetDock(copyBtn, Dock.Right);
            headerDock.Children.Add(copyBtn);

            Grid.SetRow(headerDock, 0);
            grid.Children.Add(headerDock);
        }

        var codeTb = new TextBlock
        {
            Text = code,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 16
        };
        Grid.SetRow(codeTb, 1);
        grid.Children.Add(codeTb);

        border.Child = grid;
        return border;
    }

    private static void PopulateInlines(InlineCollection target, List<MarkdownInlineToken> tokens)
    {
        foreach (var token in tokens)
        {
            switch (token.Type)
            {
                case MarkdownInlineType.Bold:
                    target.Add(new Bold(new Run(token.Text)));
                    break;

                case MarkdownInlineType.Italic:
                    target.Add(new Italic(new Run(token.Text)));
                    break;

                case MarkdownInlineType.InlineCode:
                    var codeBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(0xCB, 0xD5, 0xE1)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(3, 0, 3, 0),
                        Margin = new Thickness(1, 0, 1, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    codeBorder.Child = new TextBlock
                    {
                        Text = token.Text,
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    target.Add(new InlineUIContainer(codeBorder) { BaselineAlignment = BaselineAlignment.Center });
                    break;

                case MarkdownInlineType.TagBadge:
                    var tagBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x3A, 0x8A)), // Slate Navy #1E3A8A
                        CornerRadius = new CornerRadius(2),
                        Padding = new Thickness(4, 0, 4, 1),
                        Margin = new Thickness(2, 0, 2, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    tagBorder.Child = new TextBlock
                    {
                        Text = token.Text,
                        FontFamily = new FontFamily("Consolas, Segoe UI"),
                        FontWeight = FontWeights.SemiBold,
                        FontSize = 10.5,
                        Foreground = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    target.Add(new InlineUIContainer(tagBorder) { BaselineAlignment = BaselineAlignment.Center });
                    break;

                case MarkdownInlineType.PlainText:
                default:
                    target.Add(new Run(token.Text));
                    break;
            }
        }
    }

    /// <summary>
    /// Phân tích cú pháp chuỗi Markdown thành danh sách các khối logic (Headers, Lists, Code, Callouts, Paragraphs).
    /// </summary>
    public static List<MarkdownBlock> ParseBlocks(string? markdown)
    {
        var result = new List<MarkdownBlock>();
        if (string.IsNullOrWhiteSpace(markdown)) return result;

        string normalized = markdown.Replace("\r\n", "\n").Replace("\r", "\n");
        string[] lines = normalized.Split('\n');

        bool inCodeBlock = false;
        string currentCodeLang = string.Empty;
        var codeLines = new List<string>();

        foreach (string rawLine in lines)
        {
            string line = rawLine;

            if (inCodeBlock)
            {
                if (line.TrimStart().StartsWith("```"))
                {
                    inCodeBlock = false;
                    result.Add(new MarkdownBlock
                    {
                        Type = MarkdownBlockType.CodeBlock,
                        Language = currentCodeLang,
                        RawContent = string.Join("\n", codeLines)
                    });
                    codeLines.Clear();
                    currentCodeLang = string.Empty;
                }
                else
                {
                    codeLines.Add(line);
                }
                continue;
            }

            string trimmed = line.Trim();

            if (trimmed.StartsWith("```"))
            {
                inCodeBlock = true;
                currentCodeLang = trimmed.Substring(3).Trim();
                codeLines.Clear();
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            // Heading: #, ##, ###
            if (trimmed.StartsWith('#'))
            {
                int count = 0;
                while (count < trimmed.Length && trimmed[count] == '#') count++;
                if (count < trimmed.Length && trimmed[count] == ' ')
                {
                    string headerText = trimmed.Substring(count + 1).Trim();
                    result.Add(new MarkdownBlock
                    {
                        Type = MarkdownBlockType.Header,
                        HeaderLevel = Math.Clamp(count, 1, 3),
                        RawContent = headerText,
                        Inlines = ParseInlines(headerText)
                    });
                    continue;
                }
            }

            // Callout: >
            if (trimmed.StartsWith('>'))
            {
                string calloutText = trimmed.Substring(1).Trim();
                result.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.Callout,
                    RawContent = calloutText,
                    Inlines = ParseInlines(calloutText)
                });
                continue;
            }

            // Bullet list: •, -, *, +
            if (trimmed.StartsWith("• ") || trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
            {
                string itemText = trimmed.Substring(2).Trim();
                result.Add(new MarkdownBlock
                {
                    Type = MarkdownBlockType.BulletItem,
                    RawContent = itemText,
                    Inlines = ParseInlines(itemText)
                });
                continue;
            }

            // Paragraph
            result.Add(new MarkdownBlock
            {
                Type = MarkdownBlockType.Paragraph,
                RawContent = trimmed,
                Inlines = ParseInlines(trimmed)
            });
        }

        // Xử lý khối code chưa đóng thẻ
        if (inCodeBlock && codeLines.Count > 0)
        {
            result.Add(new MarkdownBlock
            {
                Type = MarkdownBlockType.CodeBlock,
                Language = currentCodeLang,
                RawContent = string.Join("\n", codeLines)
            });
        }

        return result;
    }

    /// <summary>
    /// Phân tích cú pháp nội dòng thành danh sách token: PlainText, Bold, Italic, InlineCode, TagBadge.
    /// </summary>
    public static List<MarkdownInlineToken> ParseInlines(string text)
    {
        var tokens = new List<MarkdownInlineToken>();
        if (string.IsNullOrEmpty(text)) return tokens;

        int lastIndex = 0;
        var matches = InlineCombinedRegex.Matches(text);

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                string plain = text.Substring(lastIndex, match.Index - lastIndex);
                tokens.Add(new MarkdownInlineToken(MarkdownInlineType.PlainText, plain));
            }

            if (match.Groups["code"].Success)
            {
                string raw = match.Groups["code"].Value;
                string inner = raw.Length >= 2 ? raw.Substring(1, raw.Length - 2) : raw;
                tokens.Add(new MarkdownInlineToken(MarkdownInlineType.InlineCode, inner));
            }
            else if (match.Groups["bold"].Success)
            {
                string raw = match.Groups["bold"].Value;
                string inner = raw.Length >= 4 ? raw.Substring(2, raw.Length - 4) : raw;
                if (IsTag(inner))
                {
                    tokens.Add(new MarkdownInlineToken(MarkdownInlineType.TagBadge, inner.ToUpperInvariant()));
                }
                else
                {
                    tokens.Add(new MarkdownInlineToken(MarkdownInlineType.Bold, inner));
                }
            }
            else if (match.Groups["italic"].Success)
            {
                string raw = match.Groups["italic"].Value;
                string inner = raw.Length >= 2 ? raw.Substring(1, raw.Length - 2) : raw;
                tokens.Add(new MarkdownInlineToken(MarkdownInlineType.Italic, inner));
            }
            else if (match.Groups["tag"].Success)
            {
                tokens.Add(new MarkdownInlineToken(MarkdownInlineType.TagBadge, match.Groups["tag"].Value.ToUpperInvariant()));
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < text.Length)
        {
            string remaining = text.Substring(lastIndex);
            tokens.Add(new MarkdownInlineToken(MarkdownInlineType.PlainText, remaining));
        }

        return tokens;
    }

    private static bool IsTag(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return TagRegex.IsMatch(text.Trim());
    }
}
