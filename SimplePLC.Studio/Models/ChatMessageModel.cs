using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SimplePLC.Studio.Models;

public partial class ChatMessageModel : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _role = "user"; // "user", "assistant", "system"

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private string _displayContent = string.Empty;

    [ObservableProperty]
    private List<AiRuleSpecModel> _extractedRules = new();

    [ObservableProperty]
    private bool _isApplied;

    [ObservableProperty]
    private bool _isRejected;

    [ObservableProperty]
    private bool _hasProposal;

    [ObservableProperty]
    private string _proposalSummaryText = string.Empty;

    [ObservableProperty]
    private string _appliedStatusText = string.Empty;

    [ObservableProperty]
    private int _proposedRuleCount;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isError;

    public bool IsUser => Role.Equals("user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => Role.Equals("assistant", StringComparison.OrdinalIgnoreCase);
    public bool IsSystem => Role.Equals("system", StringComparison.OrdinalIgnoreCase);

    public bool HasExtractedRules => ExtractedRules != null && ExtractedRules.Count > 0;
    public string ApplyButtonText => ExtractedRules != null && ExtractedRules.Count > 1
        ? $"✨ Nạp {ExtractedRules.Count} Rule vào Bảng Rule"
        : "✨ Nạp Rule vào Bảng Rule";

    public string FormattedTime => Timestamp.ToString("HH:mm");

    partial void OnContentChanged(string value)
    {
        if (IsAssistant && !string.IsNullOrWhiteSpace(value))
        {
            var rules = AiRuleParser.ExtractRules(value, out string clean);
            ExtractedRules = rules;
            DisplayContent = clean;
            ProposedRuleCount = rules.Count;
            if (rules.Count > 0 && string.IsNullOrWhiteSpace(ProposalSummaryText))
            {
                ProposalSummaryText = $"Đề xuất {rules.Count} quy tắc điều khiển";
            }
            OnPropertyChanged(nameof(HasExtractedRules));
            OnPropertyChanged(nameof(ApplyButtonText));
        }
        else
        {
            DisplayContent = value;
        }
    }

    public ChatMessageModel() { }

    public ChatMessageModel(string role, string content, bool isLoading = false, bool isError = false)
    {
        Role = role;
        Content = content;
        IsLoading = isLoading;
        IsError = isError;
        Timestamp = DateTime.Now;
    }
}
