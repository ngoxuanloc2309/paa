using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;

namespace SimplePLC.Studio.ViewModels;

public partial class AiChatViewModel : ObservableObject
{
    private readonly Func<IEnumerable<TagModel>?> _tagsProvider;
    private readonly Func<IEnumerable<RuleItemModel>?> _rulesProvider;
    private readonly Func<LogicEditorViewModel?>? _logicEditorProvider;
    private readonly GeminiApiClient _apiClient = new();
    private CancellationTokenSource? _cts;

    public ObservableCollection<ChatMessageModel> Messages { get; } = new();

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    private double _savedPanelWidth = 380;

    [ObservableProperty]
    private bool _isPanelOpen = false;

    [ObservableProperty]
    private GridLength _panelWidthGridLength = new(0);

    partial void OnIsPanelOpenChanged(bool value)
    {
        if (value)
        {
            PanelWidthGridLength = new GridLength(_savedPanelWidth > 100 ? _savedPanelWidth : 380);
        }
        else
        {
            if (PanelWidthGridLength.Value > 100)
            {
                _savedPanelWidth = PanelWidthGridLength.Value;
            }
            PanelWidthGridLength = new GridLength(0);
        }
    }

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private string _apiKeyInput = string.Empty;

    [ObservableProperty]
    private string _selectedModel = "gemini-3.8-flash";

    [ObservableProperty]
    private bool _isConfigured;

    [ObservableProperty]
    private string _settingsStatusMessage = string.Empty;

    public IReadOnlyList<string> AvailableModels { get; } = new List<string>
    {
        "gemini-3.8-flash",
        "gemini-3.7-flash",
        "gemini-3.6-flash",
        "gemini-3.5-flash",
        "gemini-3.1-pro-preview",
        "gemini-3.1-flash-lite",
        "gemini-2.5-flash",
        "gemini-2.5-pro",
        "gemini-flash-latest",
        "gemini-pro-latest"
    };

    public AiSettingsModel Settings { get; private set; }

    public AiChatViewModel(
        Func<IEnumerable<TagModel>?> tagsProvider,
        Func<IEnumerable<RuleItemModel>?> rulesProvider,
        Func<LogicEditorViewModel?>? logicEditorProvider = null)
    {
        _tagsProvider = tagsProvider;
        _rulesProvider = rulesProvider;
        _logicEditorProvider = logicEditorProvider;

        Settings = AiSettingsModel.Load();
        ApiKeyInput = Settings.ApiKey;
        SelectedModel = string.IsNullOrWhiteSpace(Settings.ModelName) ? "gemini-3.8-flash" : Settings.ModelName;
        IsConfigured = Settings.IsConfigured;

        ResetWelcomeMessage();
    }

    public void ResetWelcomeMessage()
    {
        Messages.Clear();
        if (!IsConfigured)
        {
            Messages.Add(new ChatMessageModel("assistant",
                "Chào bạn! Tôi là **SynaptiX AI** — trợ lý thiết kế mạch và cấu hình logic PLC.\n\n" +
                "💡 Để bắt đầu, bạn hãy nhấn vào nút **[⚙️ Cài đặt]** ở góc trên để cấu hình **Gemini API Key** (hỗ trợ mô hình `gemini-3.8-flash` / `gemini-3.6-flash`)."));
        }
        else
        {
            Messages.Add(new ChatMessageModel("assistant",
                "Chào bạn! Tôi là **SynaptiX AI** — trợ lý thiết kế mạch và cấu hình logic PLC.\n\n" +
                "Bạn chỉ cần mô tả bài toán bằng ngôn ngữ tự nhiên, tôi sẽ tự động thiết kế sơ đồ khối và đề xuất giải pháp trực quan để bạn xem trước trước khi áp dụng:\n" +
                "• *'Mạch khởi động và dừng bơm DO0, có khóa an toàn nút dừng khẩn cấp DI2'*\n" +
                "• *'Hẹn giờ trễ ngắt quạt làm mát DO1 sau 10 giây khi máy dừng'*\n" +
                "• *'Cảm biến áp suất AI0 vượt quá 80 bar duy trì 5 giây thì đóng van xả DO0'*\n" +
                "• *'Đếm đủ 100 sản phẩm từ cảm biến DI0 thì bật còi báo DO2'*\n" +
                "• *'Quy đổi cảm biến áp suất analog AI0 (4-20mA) sang 0-10 bar'*\n\n" +
                "Hãy nhập yêu cầu của bạn bên dưới!"));
        }
    }

    [RelayCommand]
    public void TogglePanel()
    {
        IsPanelOpen = !IsPanelOpen;
    }

    [RelayCommand]
    public void ClosePanel()
    {
        IsPanelOpen = false;
    }

    [RelayCommand]
    public void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        if (IsSettingsOpen)
        {
            ApiKeyInput = Settings.ApiKey;
            SelectedModel = Settings.ModelName;
            SettingsStatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        Settings.ApiKey = ApiKeyInput.Trim();
        Settings.ModelName = SelectedModel;
        Settings.Save();

        IsConfigured = Settings.IsConfigured;
        IsSettingsOpen = false;
        SettingsStatusMessage = "Đã lưu cài đặt AI thành công!";

        if (Messages.Count <= 1)
        {
            ResetWelcomeMessage();
        }
    }

    [RelayCommand]
    public void OpenGoogleAiStudio()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://aistudio.google.com/app/apikey",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở trình duyệt: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    [RelayCommand]
    public void ClearChat()
    {
        CancelCurrentRequest();
        ResetWelcomeMessage();
    }

    [RelayCommand]
    public void CancelCurrentRequest()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }
        IsBusy = false;
    }

    [RelayCommand]
    public void CopyMessage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        try
        {
            Clipboard.SetText(text);
        }
        catch { }
    }

    public Action<List<AiRuleSpecModel>, ChatMessageModel>? OnApplyRulesRequested { get; set; }
    public Action<int>? OnNavigateToTab { get; set; }

    [RelayCommand]
    public void ViewRuleTable(ChatMessageModel? message = null)
    {
        OnNavigateToTab?.Invoke(2); // Chuyển sang Tab 2: Bảng Rule
    }

    [RelayCommand]
    public void ApplyRules(ChatMessageModel? message)
    {
        if (message == null || !message.HasExtractedRules || message.IsApplied)
            return;

        OnApplyRulesRequested?.Invoke(message.ExtractedRules, message);
        message.IsApplied = true;
        message.IsRejected = false;
        message.AppliedStatusText = $"✅ Đã đồng bộ thành công {message.ExtractedRules.Count} Rule vào Bảng Rule!";
    }

    [RelayCommand]
    public void AcceptProposal(ChatMessageModel? message)
    {
        var logicVM = _logicEditorProvider?.Invoke();
        if (logicVM != null)
        {
            if (logicVM.HasPendingAiProposal)
            {
                logicVM.AcceptAiProposal();
            }

            int ruleCount = logicVM.ValidRuleCount;
            if (ruleCount == 0 && message?.ExtractedRules != null && message.ExtractedRules.Count > 0)
            {
                OnApplyRulesRequested?.Invoke(message.ExtractedRules, message);
                ruleCount = message.ExtractedRules.Count;
            }

            var currentRules = _rulesProvider?.Invoke();
            int totalRules = currentRules?.Count() ?? ruleCount;

            if (message != null)
            {
                message.IsApplied = true;
                message.IsRejected = false;
                message.AppliedStatusText = totalRules > 0
                    ? $"✅ Đã áp dụng thành công ({totalRules} Rule vào Bảng Rule)"
                    : "✅ Đã áp dụng lên Canvas";
            }
        }
        else if (message?.HasExtractedRules == true)
        {
            ApplyRules(message);
        }
    }

    [RelayCommand]
    public void RejectProposal(ChatMessageModel? message)
    {
        var logicVM = _logicEditorProvider?.Invoke();
        if (logicVM != null && logicVM.HasPendingAiProposal)
        {
            logicVM.RejectAiProposal();
        }
        if (message != null)
        {
            message.HasProposal = false;
            message.IsRejected = true;
            message.AppliedStatusText = "✕ Đã hủy bỏ bản vẽ đề xuất nháp.";
        }
    }

    [RelayCommand]
    public async Task UseQuickPrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return;
        InputText = prompt;
        await SendMessageAsync();
    }

    [RelayCommand]
    public async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsBusy) return;

        if (!IsConfigured)
        {
            IsSettingsOpen = true;
            SettingsStatusMessage = "Vui lòng nhập API Key để bắt đầu chat.";
            return;
        }

        string userPrompt = InputText.Trim();
        InputText = string.Empty;

        var userMsg = new ChatMessageModel("user", userPrompt);
        Messages.Add(userMsg);

        var assistantMsg = new ChatMessageModel("assistant", "Đang tổng hợp thông tin dự án...", isLoading: true);
        Messages.Add(assistantMsg);

        IsBusy = true;
        _cts = new CancellationTokenSource();

        try
        {
            // 1. Tổng hợp ngữ cảnh chuyên sâu (Layer 1)
            var aggregator = new AiContextAggregator(_tagsProvider, () => _logicEditorProvider?.Invoke());
            var contextJson = aggregator.BuildContextPayload();

            var history = new List<(string role, string text)>();
            var contextMessages = Messages.Where(m => !m.IsLoading && !m.IsError && !string.IsNullOrWhiteSpace(m.Content)).TakeLast(6).ToList();
            foreach (var m in contextMessages)
            {
                if (m == userMsg)
                {
                    string enrichedPrompt = $"[DỮ LIỆU NGỮ CẢNH DỰ ÁN]\n{contextJson.ToJsonString()}\n\nYêu cầu kỹ sư: {userPrompt}";
                    history.Add((m.Role, enrichedPrompt));
                }
                else
                {
                    history.Add((m.Role, m.Content));
                }
            }

            string systemInstruction = AiPromptBuilder.BuildSystemInstruction();
            var toolDeclarations = AiToolDeclarations.GetGeminiFunctionDeclarations();

            // 2. Gọi LLM thiết kế logic
            assistantMsg.Content = "Đang thiết kế giải pháp điều khiển...";
            var response = await _apiClient.GenerateContentWithToolsAsync(
                Settings,
                systemInstruction,
                history,
                toolDeclarations,
                _cts.Token);

            // 3. Nếu AI phát ra Tool Calls, thẩm định qua AiSafetyGate và nạp Ghost Preview (Layer 3 & 4)
            if (response.ToolCalls.Count > 0 && _logicEditorProvider != null)
            {
                var logicVM = _logicEditorProvider();
                if (logicVM != null)
                {
                    var tx = new DraftGraphTransaction();
                    foreach (var tc in response.ToolCalls)
                    {
                        tx.ApplyToolCall(tc);
                    }

                    if (tx.AddedNodes.Count > 0)
                    {
                        var allTags = _tagsProvider() ?? Enumerable.Empty<TagModel>();
                        var validation = AiSafetyGate.Validate(tx, allTags);

                        if (!validation.IsValid)
                        {
                            assistantMsg.Content = $"⚠️ **Cổng Kiểm Duyệt An Toàn Từ Chối:** {validation.ErrorMessage}\n\n*Khuyến nghị:* {validation.Guidance}";
                            assistantMsg.IsLoading = false;
                            return;
                        }

                        // Áp dụng Ghost Preview lên Canvas thật mà không xóa sơ đồ cũ
                        string summary = $"Đề xuất AI: +{tx.AddedNodes.Count} khối, +{tx.AddedWires.Count} đường nối";
                        logicVM.ApplyAiProposalToCanvas(tx, summary);

                        // TỔNG HỢP THUYẾT MINH NẾU AI CHỈ TRẢ VỀ TOOL CALL MÀ THIẾU GIẢI THÍCH HOẶC CÂU CỤT NGỦN
                        string explanation = response.Explanation;
                        if (string.IsNullOrWhiteSpace(explanation) ||
                            explanation.Trim() == "Đã xử lý cấu hình theo yêu cầu." ||
                            !explanation.Contains("Nguyên lý"))
                        {
                            explanation = AiExplanationSynthesizer.Synthesize(tx, userPrompt);
                        }

                        assistantMsg.HasProposal = true;
                        assistantMsg.ProposalSummaryText = $"Đề xuất: +{tx.AddedNodes.Count} khối, +{tx.AddedWires.Count} đường nối";
                        assistantMsg.Content = explanation;
                        assistantMsg.IsLoading = false;
                        return;
                    }
                }
            }

            // Fallback: Nếu không có Tool Calls hoặc Tool Calls rỗng, kiểm tra xem có rule JSON trong text không
            var extractedRules = AiRuleParser.ExtractRules(response.Explanation, out _);
            if (extractedRules.Count == 0)
            {
                extractedRules = TryExtractRulesFromTextOrPrompt(response.Explanation, userPrompt);
            }

            if (extractedRules.Count > 0 && _logicEditorProvider != null)
            {
                var logicVM = _logicEditorProvider();
                if (logicVM != null)
                {
                    // Tự động chuyển đổi các rule thành DraftGraphTransaction để hiển thị Ghost Preview
                    var tx = new DraftGraphTransaction();
                    double x = 60, y = 100;
                    int ruleIdx = 0;

                    foreach (var rule in extractedRules)
                    {
                        string inId = $"node_in_{ruleIdx}";
                        string trgId = $"node_trg_{ruleIdx}";
                        string actId = $"node_act_{ruleIdx}";
                        string grdId = $"node_grd_{ruleIdx}";

                        tx.AddedNodes.Add(new DraftNode
                        {
                            Id = inId,
                            NodeType = "Input",
                            Label = $"Ngõ vào ({rule.InputTag})",
                            PositionX = x,
                            PositionY = y,
                            TagName = rule.InputTag
                        });

                        tx.AddedNodes.Add(new DraftNode
                        {
                            Id = trgId,
                            NodeType = "Trigger",
                            Label = $"Kích hoạt ({rule.Trigger})",
                            PositionX = x + 240,
                            PositionY = y,
                            TriggerType = rule.Trigger,
                            CompareOp = rule.CompareOp,
                            ThresholdLo = rule.ThresholdLo,
                            ThresholdHi = rule.ThresholdHi,
                            DebounceMs = (int)rule.ForMs
                        });

                        tx.AddedWires.Add(new DraftWire
                        {
                            SourceNodeId = inId,
                            SourcePort = "Out",
                            TargetNodeId = trgId,
                            TargetPort = "In"
                        });

                        string lastId = trgId;
                        double currentX = x + 240;

                        if (!string.IsNullOrWhiteSpace(rule.GuardTag) && !string.Equals(rule.GuardTag, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            currentX += 240;
                            tx.AddedNodes.Add(new DraftNode
                            {
                                Id = grdId,
                                NodeType = "Guard",
                                Label = $"Khóa an toàn ({rule.GuardTag})",
                                PositionX = currentX,
                                PositionY = y,
                                TagName = rule.GuardTag
                            });

                            tx.AddedWires.Add(new DraftWire
                            {
                                SourceNodeId = lastId,
                                SourcePort = "Out",
                                TargetNodeId = grdId,
                                TargetPort = "In"
                            });

                            lastId = grdId;
                        }

                        currentX += 240;
                        tx.AddedNodes.Add(new DraftNode
                        {
                            Id = actId,
                            NodeType = "Action",
                            Label = $"Tác vụ ({rule.ActionTag})",
                            PositionX = currentX,
                            PositionY = y,
                            TagName = rule.ActionTag,
                            ActionType = rule.ActionType,
                            ActionParam = rule.Param
                        });

                        tx.AddedWires.Add(new DraftWire
                        {
                            SourceNodeId = lastId,
                            SourcePort = "Out",
                            TargetNodeId = actId,
                            TargetPort = "In"
                        });

                        y += 180;
                        ruleIdx++;
                    }

                    if (tx.AddedNodes.Count > 0)
                    {
                        string summary = $"Đề xuất AI: +{tx.AddedNodes.Count} khối, +{tx.AddedWires.Count} đường nối";
                        logicVM.ApplyAiProposalToCanvas(tx, summary);

                        assistantMsg.HasProposal = true;
                        assistantMsg.ProposalSummaryText = $"Đề xuất: +{tx.AddedNodes.Count} khối, +{tx.AddedWires.Count} đường nối";

                        string explanation = response.Explanation;
                        if (string.IsNullOrWhiteSpace(explanation) ||
                            explanation.Trim() == "Đã xử lý cấu hình theo yêu cầu." ||
                            !explanation.Contains("Nguyên lý"))
                        {
                            explanation = AiExplanationSynthesizer.Synthesize(tx, userPrompt);
                        }

                        assistantMsg.Content = explanation;
                        assistantMsg.IsLoading = false;
                        return;
                    }
                }
            }

            assistantMsg.Content = response.Explanation;
            assistantMsg.IsLoading = false;
        }
        catch (OperationCanceledException)
        {
            assistantMsg.Content = "*Đã hủy yêu cầu.*";
            assistantMsg.IsLoading = false;
        }
        catch (Exception ex)
        {
            assistantMsg.Content = $"❌ **Lỗi:** {ex.Message}";
            assistantMsg.IsLoading = false;
            assistantMsg.IsError = true;
        }
        finally
        {
            IsBusy = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private static List<AiRuleSpecModel> TryExtractRulesFromTextOrPrompt(string explanation, string prompt)
    {
        var rules = new List<AiRuleSpecModel>();
        string combined = (explanation + " " + prompt).ToLowerInvariant();

        var diMatches = System.Text.RegularExpressions.Regex.Matches(combined, @"\bdi[0-7]\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var doMatches = System.Text.RegularExpressions.Regex.Matches(combined, @"\bdo[0-7]\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (diMatches.Count >= 2 && doMatches.Count >= 1 &&
            (combined.Contains("start") || combined.Contains("bật")) &&
            (combined.Contains("stop") || combined.Contains("tắt")))
        {
            string startTag = diMatches[0].Value.ToUpperInvariant();
            string stopTag = diMatches[1].Value.ToUpperInvariant();
            string outTag = doMatches[0].Value.ToUpperInvariant();

            rules.Add(new AiRuleSpecModel
            {
                Narrative = $"Bật {outTag} khi nhấn nút Start {startTag}",
                InputTag = startTag,
                Trigger = "ON_RISE",
                ForMs = 50,
                ActionTag = outTag,
                ActionType = "SET_TAG",
                Param = 1
            });

            rules.Add(new AiRuleSpecModel
            {
                Narrative = $"Tắt {outTag} khi nhấn nút Stop {stopTag}",
                InputTag = stopTag,
                Trigger = "ON_RISE",
                ForMs = 50,
                ActionTag = outTag,
                ActionType = "SET_TAG",
                Param = 0
            });
        }
        else if (diMatches.Count >= 1 && doMatches.Count >= 1)
        {
            string inTag = diMatches[0].Value.ToUpperInvariant();
            string outTag = doMatches[0].Value.ToUpperInvariant();
            bool isToggle = combined.Contains("toggle") || combined.Contains("đảo");
            rules.Add(new AiRuleSpecModel
            {
                Narrative = $"Điều khiển {outTag} từ ngõ vào {inTag}",
                InputTag = inTag,
                Trigger = "ON_RISE",
                ForMs = 50,
                ActionTag = outTag,
                ActionType = isToggle ? "TOGGLE_TAG" : "SET_TAG",
                Param = 1
            });
        }

        return rules;
    }
}
