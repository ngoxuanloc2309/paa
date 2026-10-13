using System.Windows.Input;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Đại diện cho một chip gợi ý tương tác thông minh (Contextual Suggestion Chip) trong AI Chatbox.
/// Gợi ý tự động thích ứng theo trạng thái sơ đồ FBD, khối được chọn, kết quả biên dịch và chế độ mô phỏng.
/// </summary>
public class AiSuggestionChip
{
    public string Text { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public ICommand? Command { get; set; }
    public string? Tooltip { get; set; }
    public string Category { get; set; } = "General";

    public string DisplayText => string.IsNullOrWhiteSpace(Icon) ? Text : $"{Icon} {Text}";

    public AiSuggestionChip() { }

    public AiSuggestionChip(
        string text,
        string icon,
        string prompt,
        ICommand? command = null,
        string? tooltip = null,
        string category = "General")
    {
        Text = text;
        Icon = icon;
        Prompt = prompt;
        Command = command;
        Tooltip = tooltip ?? prompt;
        Category = category;
    }
}
