using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
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
    private static readonly Regex RuleJsonBlockRegex = new(
        @"```(?:json:rules|json)?\s*(\[\s*\{.*?\}\s*\])(?:\s*```)?",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex FallbackArrayRegex = new(
        @"(\[\s*\{\s*""(?:narrative|input_tag)""[\s\S]*?\}\s*\])",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex OrphanedBlockRegex = new(
        @"```(?:json:rules|json)?\s*$",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private readonly Func<IEnumerable<TagModel>?> _tagsProvider;
    private readonly Func<IEnumerable<RuleItemModel>?> _rulesProvider;
    private readonly Func<LogicEditorViewModel?>? _logicEditorProvider;
    private readonly GeminiApiClient _apiClient = new();
    private CancellationTokenSource? _cts;

    /// <summary>
    /// Cho phép inject mock handler trong các bài unit tests tự động mà không gọi HTTP thực tế.
    /// </summary>
    public Func<AiSettingsModel, string, IEnumerable<(string role, string text)>, JsonArray?, CancellationToken, Task<GeminiToolResponse>>? ApiHandler { get; set; }

    public ObservableCollection<ChatMessageModel> Messages { get; } = new();

    /// <summary>
    /// Danh sách các chip gợi ý ngữ cảnh thời gian thực (Dynamic Contextual Suggestion Chips).
    /// </summary>
    public ObservableCollection<AiSuggestionChip> SuggestionChips { get; } = new();

    [ObservableProperty]
    private string _activeContextNodeCountText = "📍 0 khối";

    [ObservableProperty]
    private string _activeContextCompileStatusText = "⚪ Sẵn sàng";

    [ObservableProperty]
    private bool _activeContextCompileIsError;

    [ObservableProperty]
    private string _activeContextTagCountText = "🏷️ 0 Tags";

    [ObservableProperty]
    private string _activeContextSelectionText = string.Empty;

    [ObservableProperty]
    private bool _hasActiveContextSelection;

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
    };

    public AiSettingsModel Settings { get; private set; }

    public AiChatViewModel(
        Func<IEnumerable<TagModel>?> tagsProvider,
        Func<IEnumerable<RuleItemModel>?> rulesProvider,
        Func<LogicEditorViewModel?>? logicEditorProvider = null,
        AiSettingsModel? settings = null)
    {
        _tagsProvider = tagsProvider;
        _rulesProvider = rulesProvider;
        _logicEditorProvider = logicEditorProvider;

        Settings = settings ?? AiSettingsModel.Load();
        ApiKeyInput = Settings.ApiKey;
        SelectedModel = string.IsNullOrWhiteSpace(Settings.ModelName) ? "gemini-3.8-flash" : Settings.ModelName;
        IsConfigured = Settings.IsConfigured;

        ResetWelcomeMessage();
        UpdateContextualSuggestions();
        SubscribeToLogicEditor();
    }

    public void ResetWelcomeMessage()
    {
        Messages.Clear();
        if (!IsConfigured)
        {
            Messages.Add(new ChatMessageModel("assistant",
                "Chào bạn! Tôi là **SynaptiX AI** — trợ lý thiết kế mạch và cấu hình logic PLC.\n\n" +
                "💡 Để bắt đầu, bạn hãy nhấn vào nút **[⚙️ Cài đặt]** ở góc trên để cấu hình **Gemini API Key**."));
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
        UpdateContextualSuggestions();
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
        Settings.ApiKey = (ApiKeyInput ?? string.Empty).Trim();
        Settings.ModelName = string.IsNullOrWhiteSpace(SelectedModel) ? "gemini-3.8-flash" : SelectedModel.Trim();
        Settings.Save();

        IsConfigured = Settings.IsConfigured;
        IsSettingsOpen = false;
        SettingsStatusMessage = "Đã lưu cài đặt AI thành công!";

        if (IsConfigured)
        {
            Messages.Add(new ChatMessageModel("assistant",
                $"⚙️ **Đã lưu cấu hình AI thành công!**\n- **Mô hình:** `{Settings.ModelName}`\n- **Trạng thái:** Sẵn sàng nhận câu hỏi hoặc yêu cầu thiết kế mạch logic."));
        }
        else if (Messages.Count <= 1)
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

        if (!IsConfigured && ApiHandler == null)
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
            // 1. Phân loại ý định người dùng (Intent Classification - Layer 1)
            var logicVM = _logicEditorProvider?.Invoke();
            bool hasCanvasElements = logicVM?.Nodes.Any(n => !n.IsGhost) == true;
            bool hasLinterErrors = logicVM?.CompileState == CompileState.Invalid ||
                                   (logicVM?.Diagnostics != null && logicVM.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) ||
                                   logicVM?.HasSimGateWarning == true;

            var intent = AiIntentClassifier.Classify(userPrompt, hasCanvasElements, hasLinterErrors);

            // 2. Tổng hợp ngữ cảnh chuyên sâu (Layer 1 Context Aggregator)
            var aggregator = new AiContextAggregator(_tagsProvider, () => _logicEditorProvider?.Invoke());
            var contextJson = aggregator.BuildContextPayload();

            // 3. Quản lý bộ nhớ hội thoại đa lượt trượt (Sliding Window 10-12 messages) & Làm sạch JSON cũ
            var history = new List<(string role, string text)>();
            var contextMessages = Messages
                .Where(m => !m.IsLoading && !m.IsError && !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(12)
                .ToList();

            foreach (var m in contextMessages)
            {
                if (m == userMsg)
                {
                    string enrichedPrompt = $"[DỮ LIỆU NGỮ CẢNH DỰ ÁN]\n{contextJson.ToJsonString()}\n\nYêu cầu kỹ sư: {userPrompt}";
                    history.Add((m.Role, enrichedPrompt));
                }
                else if (m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    string sanitized = SanitizeHistoryMessage(m.Content);
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        history.Add((m.Role, sanitized));
                    }
                }
                else
                {
                    history.Add((m.Role, m.Content));
                }
            }

            // 4. Chọn System Prompt và Tool Declarations tương ứng với Intent
            string systemInstruction = AiPromptBuilder.BuildSystemInstruction(intent);
            var toolDeclarations = intent == AiUserIntent.FbdSynthesis
                ? AiToolDeclarations.GetGeminiFunctionDeclarations()
                : null;

            assistantMsg.Content = intent switch
            {
                AiUserIntent.ConsultativeQnA => "Đang phân tích và tư vấn kỹ thuật...",
                AiUserIntent.ProjectDiagnose => "Đang chẩn đoán và kiểm tra an toàn...",
                _ => "Đang thiết kế giải pháp điều khiển..."
            };

            // 5. Gọi LLM Engine
            GeminiToolResponse response;
            if (ApiHandler != null)
            {
                response = await ApiHandler(Settings, systemInstruction, history, toolDeclarations, _cts.Token);
            }
            else
            {
                response = await _apiClient.GenerateContentWithToolsAsync(
                    Settings,
                    systemInstruction,
                    history,
                    toolDeclarations,
                    _cts.Token);
            }

            // 6. Xử lý phản hồi theo Intent
            if (intent == AiUserIntent.ConsultativeQnA || intent == AiUserIntent.ProjectDiagnose)
            {
                // Khi là hỏi đáp tư vấn hoặc chẩn đoán:
                // - KHÔNG gọi tool execution hay vẽ Ghost Nodes
                // - KHÔNG chạy regex rule extraction
                // - KHÔNG ghi đè câu trả lời của LLM
                assistantMsg.Content = response.Explanation;
                assistantMsg.IsLoading = false;
                return;
            }

            // 7. Khi ý định là FbdSynthesis:
            // 7.1. Nếu AI phát ra Tool Calls, thẩm định qua AiSafetyGate và nạp Ghost Preview (Layer 3 & 4)
            if (response.ToolCalls.Count > 0 && _logicEditorProvider != null)
            {
                var logicVMInstance = _logicEditorProvider();
                if (logicVMInstance != null)
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
                        logicVMInstance.ApplyAiProposalToCanvas(tx, summary);

                        // Giữ nguyên phản hồi tự nhiên của LLM; chỉ synthesize khi LLM trả lời rỗng hoặc placeholder cụt ngủn
                        string explanation = response.Explanation;
                        if (string.IsNullOrWhiteSpace(explanation) ||
                            explanation.Trim() == "Đã xử lý cấu hình theo yêu cầu.")
                        {
                            explanation = AiExplanationSynthesizer.Synthesize(tx, userPrompt);
                        }

                        assistantMsg.HasProposal = true;
                        assistantMsg.ProposalSummaryText = summary;
                        assistantMsg.Content = explanation;
                        assistantMsg.IsLoading = false;
                        return;
                    }
                }
            }

            // 7.2. Fallback cho FbdSynthesis: trích xuất rule JSON hoặc regex từ text
            var extractedRules = AiRuleParser.ExtractRules(response.Explanation, out _);
            if (extractedRules.Count == 0 && intent == AiUserIntent.FbdSynthesis)
            {
                extractedRules = TryExtractRulesFromTextOrPrompt(response.Explanation, userPrompt);
            }

            if (extractedRules.Count > 0 && _logicEditorProvider != null)
            {
                var logicVMInstance = _logicEditorProvider();
                if (logicVMInstance != null)
                {
                    var allTags = _tagsProvider() ?? Enumerable.Empty<TagModel>();
                    var ruleValidation = AiSafetyGate.Validate(extractedRules, allTags);
                    if (!ruleValidation.IsValid)
                    {
                        assistantMsg.Content = $"⚠️ **Cổng Kiểm Duyệt An Toàn Từ Chối:** {ruleValidation.ErrorMessage}\n\n*Khuyến nghị:* {ruleValidation.Guidance}";
                        assistantMsg.IsLoading = false;
                        return;
                    }

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
                        var validation = AiSafetyGate.Validate(tx, allTags);
                        if (!validation.IsValid)
                        {
                            assistantMsg.Content = $"⚠️ **Cổng Kiểm Duyệt An Toàn Từ Chối:** {validation.ErrorMessage}\n\n*Khuyến nghị:* {validation.Guidance}";
                            assistantMsg.IsLoading = false;
                            return;
                        }

                        string summary = $"Đề xuất AI: +{tx.AddedNodes.Count} khối, +{tx.AddedWires.Count} đường nối";
                        logicVMInstance.ApplyAiProposalToCanvas(tx, summary);

                        assistantMsg.HasProposal = true;
                        assistantMsg.ProposalSummaryText = summary;

                        string explanation = response.Explanation;
                        if (string.IsNullOrWhiteSpace(explanation) ||
                            explanation.Trim() == "Đã xử lý cấu hình theo yêu cầu.")
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

    /// <summary>
    /// Làm sạch các khối code JSON máy đọc khỏi tin nhắn cũ khi tuần tự hóa vào lịch sử đa lượt,
    /// tránh gây ô nhiễm ngữ cảnh token ở các lượt trao đổi tiếp theo.
    /// </summary>
    public static string SanitizeHistoryMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return string.Empty;
        string cleaned = RuleJsonBlockRegex.Replace(content, "").TrimEnd();
        cleaned = FallbackArrayRegex.Replace(cleaned, "").TrimEnd();
        cleaned = OrphanedBlockRegex.Replace(cleaned, "").TrimEnd();
        return cleaned;
    }

    private static List<AiRuleSpecModel> TryExtractRulesFromTextOrPrompt(string explanation, string prompt)
    {
        var rules = new List<AiRuleSpecModel>();
        string combined = (explanation + " " + prompt).ToLowerInvariant();

        var diMatches = Regex.Matches(combined, @"\bdi[0-7]\b", RegexOptions.IgnoreCase);
        var doMatches = Regex.Matches(combined, @"\bdo[0-7]\b", RegexOptions.IgnoreCase);

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

    private LogicEditorViewModel? _subscribedLogicVM;

    public void SubscribeToLogicEditor()
    {
        var logicVM = _logicEditorProvider?.Invoke();
        if (logicVM == null || ReferenceEquals(logicVM, _subscribedLogicVM)) return;

        if (_subscribedLogicVM != null)
        {
            _subscribedLogicVM.PropertyChanged -= OnLogicEditorPropertyChanged;
            _subscribedLogicVM.Nodes.CollectionChanged -= OnLogicNodesChanged;
            _subscribedLogicVM.Connections.CollectionChanged -= OnLogicConnectionsChanged;
        }

        _subscribedLogicVM = logicVM;
        _subscribedLogicVM.PropertyChanged += OnLogicEditorPropertyChanged;
        _subscribedLogicVM.Nodes.CollectionChanged += OnLogicNodesChanged;
        _subscribedLogicVM.Connections.CollectionChanged += OnLogicConnectionsChanged;
    }

    private void OnLogicEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(LogicEditorViewModel.SelectedNode)
            or nameof(LogicEditorViewModel.CompileState)
            or nameof(LogicEditorViewModel.IsSimulationMode)
            or nameof(LogicEditorViewModel.IsSimRunning)
            or nameof(LogicEditorViewModel.HasSimGateWarning))
        {
            UpdateContextualSuggestions();
        }
    }

    private void OnLogicNodesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateContextualSuggestions();
    }

    private void OnLogicConnectionsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateContextualSuggestions();
    }

    /// <summary>
    /// Cập nhật các chỉ báo ngữ cảnh thời gian thực và danh sách chip gợi ý tương tác thông minh
    /// dựa theo trạng thái Canvas FBD (khối rỗng, khối được chọn, lỗi biên dịch, chế độ mô phỏng).
    /// </summary>
    public void UpdateContextualSuggestions()
    {
        SubscribeToLogicEditor();

        var logicVM = _logicEditorProvider?.Invoke();
        var allTags = _tagsProvider?.Invoke();
        int tagCount = allTags?.Count() ?? 0;

        // Cập nhật các chỉ báo ngữ cảnh thời gian thực (Active Context Status Indicators)
        if (logicVM != null)
        {
            int nodeCount = logicVM.Nodes.Count(n => !n.IsGhost);
            int wireCount = logicVM.Connections.Count(c => !c.IsGhost);
            ActiveContextNodeCountText = wireCount > 0 ? $"📍 {nodeCount} khối, {wireCount} dây" : $"📍 {nodeCount} khối";

            bool hasErrors = logicVM.CompileState == CompileState.Invalid ||
                             (logicVM.Diagnostics != null && logicVM.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) ||
                             logicVM.HasSimGateWarning;

            if (hasErrors)
            {
                ActiveContextCompileStatusText = "⚠️ Lỗi sơ đồ";
                ActiveContextCompileIsError = true;
            }
            else if (logicVM.CompileState == CompileState.Valid)
            {
                ActiveContextCompileStatusText = "🟢 Hợp lệ";
                ActiveContextCompileIsError = false;
            }
            else
            {
                ActiveContextCompileStatusText = "⚪ Chưa biên dịch";
                ActiveContextCompileIsError = false;
            }

            if (logicVM.SelectedNode != null)
            {
                HasActiveContextSelection = true;
                ActiveContextSelectionText = $"🎯 {logicVM.SelectedNode.Title}";
            }
            else
            {
                HasActiveContextSelection = false;
                ActiveContextSelectionText = string.Empty;
            }
        }
        else
        {
            ActiveContextNodeCountText = "📍 0 khối";
            ActiveContextCompileStatusText = "⚪ Sẵn sàng";
            ActiveContextCompileIsError = false;
            HasActiveContextSelection = false;
            ActiveContextSelectionText = string.Empty;
        }

        ActiveContextTagCountText = $"🏷️ {tagCount} Tags";

        // Cập nhật danh sách Chip gợi ý động theo mức độ ưu tiên ngữ cảnh
        SuggestionChips.Clear();

        bool hasLinterErrors = logicVM != null && (
            logicVM.CompileState == CompileState.Invalid ||
            (logicVM.Diagnostics != null && logicVM.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) ||
            logicVM.HasSimGateWarning);

        bool isSimActive = logicVM != null && (logicVM.IsSimulationMode || logicVM.IsSimRunning);
        var selectedNode = logicVM?.SelectedNode;

        if (hasLinterErrors)
        {
            // Trạng thái Trình biên dịch có lỗi: fix compiler error, explain missing wire
            SuggestionChips.Add(new AiSuggestionChip(
                "Khắc phục lỗi biên dịch", "⚠️",
                "Phân tích nguyên nhân và hướng dẫn khắc phục các lỗi biên dịch hiện tại trong sơ đồ",
                UseQuickPromptCommand, category: "Diagnostic"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Giải thích cổng thiếu dây", "🔍",
                "Giải thích các chân cổng còn thiếu dây nối và đề xuất cách kết nối an toàn",
                UseQuickPromptCommand, category: "Diagnostic"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Kiểm tra xung đột ngõ ra", "⚡",
                "Kiểm tra xem có nhiều Action cùng điều khiển một ngõ ra DO hoặc vi phạm quy tắc không",
                UseQuickPromptCommand, category: "Diagnostic"));
        }
        else if (isSimActive)
        {
            // Trạng thái Mô phỏng đang chạy: monitor DO states, force AI0 sensor test
            SuggestionChips.Add(new AiSuggestionChip(
                "Giám sát ngõ ra DO", "📊",
                "Hướng dẫn theo dõi trạng thái các ngõ ra DO0..DO7 trong chu kỳ quét mô phỏng",
                UseQuickPromptCommand, category: "Simulation"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Thử nghiệm cảm biến AI0", "⚡",
                "Hướng dẫn ép giá trị (Force) ngõ vào analog AI0 để kiểm tra ngưỡng kích hoạt",
                UseQuickPromptCommand, category: "Simulation"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Kiểm tra thời gian đáp ứng", "⏱️",
                "Đánh giá thời gian phản hồi tín hiệu và độ trễ debouncing trong mô phỏng",
                UseQuickPromptCommand, category: "Simulation"));
        }
        else if (selectedNode is TimerNodeViewModel timerNode)
        {
            // Khối Timer được chọn: configure delay, switch mode, check Q wire
            string title = timerNode.Title;
            SuggestionChips.Add(new AiSuggestionChip(
                $"Cấu hình trễ ({title})", "⏱️",
                $"Hướng dẫn cấu hình thời gian trễ 5000ms cho khối {title} và giải thích chân ngõ vào IN, ngõ ra Q",
                UseQuickPromptCommand, category: "Timer"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Chuyển chế độ TON / TOF", "🔄",
                $"So sánh và hướng dẫn chuyển đổi giữa trễ bật TON và trễ ngắt TOF cho khối {title}",
                UseQuickPromptCommand, category: "Timer"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Kiểm tra dây nối Q", "🔍",
                $"Kiểm tra xem ngõ ra Q của khối {title} đã được nối tới cuộn chấp hành DO nào chưa",
                UseQuickPromptCommand, category: "Timer"));
        }
        else if (selectedNode is ScaleNodeViewModel scaleNode)
        {
            // Khối Scale được chọn: calibrate 4-20mA, change engineering unit
            string title = scaleNode.Title;
            SuggestionChips.Add(new AiSuggestionChip(
                "Calib 4-20mA (0-10 bar)", "📐",
                $"Hướng dẫn calib tín hiệu dòng 4-20mA (2000-10000 mV) sang dải áp suất 0..10 bar cho khối {title}",
                UseQuickPromptCommand, category: "Scale"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Đổi đơn vị đo sang °C", "🌡️",
                $"Cấu hình khối {title} quy đổi cảm biến nhiệt độ PT100 sang đơn vị độ C (-20..150°C)",
                UseQuickPromptCommand, category: "Scale"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Giới hạn Clamp an toàn", "🛡️",
                $"Hướng dẫn thiết lập Clamp Min/Max cho khối {title} để bảo vệ dữ liệu không tràn dải",
                UseQuickPromptCommand, category: "Scale"));
        }
        else if (selectedNode is CounterNodeViewModel counterNode)
        {
            string title = counterNode.Title;
            SuggestionChips.Add(new AiSuggestionChip(
                $"Cấu hình đếm ({title})", "🔢",
                $"Hướng dẫn đặt giá trị Preset Value (PV=100) cho khối {title} và kết nối tín hiệu Reset",
                UseQuickPromptCommand, category: "Counter"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Chuyển chế độ CTU / CTD", "🔄",
                $"So sánh và hướng dẫn chuyển đổi đếm tiến CTU và đếm lùi CTD cho khối {title}",
                UseQuickPromptCommand, category: "Counter"));
        }
        else if (logicVM == null || !logicVM.Nodes.Any(n => !n.IsGhost))
        {
            // Canvas rỗng: starter circuits (Start/Stop, Đếm sản lượng, Scale 4-20mA)
            SuggestionChips.Add(new AiSuggestionChip(
                "Mạch Start / Stop", "⚡",
                "Mạch Start/Stop bật tắt bơm DO0 với nút nhấn DI0 (Start) và DI1 (Stop)",
                UseQuickPromptCommand, category: "Starter"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Đếm sản lượng", "📊",
                "Mạch đếm sản lượng từ cảm biến DI2 lưu an toàn vào biến Flash Retain VREG_RETAIN0",
                UseQuickPromptCommand, category: "Starter"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Scale 4-20mA", "📐",
                "Hướng dẫn dùng khối SCALE calib cảm biến áp suất 4-20mA (2000..10000 mV) sang 0..16 bar",
                UseQuickPromptCommand, category: "Starter"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Trễ bật TON", "⏱️",
                "Mạch trễ bật TON: nhấn giữ nút chạy DI0 liên tục 5s mới bật bơm bôi trơn DO0",
                UseQuickPromptCommand, category: "Starter"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Khóa chéo an toàn", "🛡️",
                "Mạch điều khiển 2 động cơ đảo chiều có khóa chéo liên động an toàn (Interlock)",
                UseQuickPromptCommand, category: "Starter"));
        }
        else
        {
            // Canvas có khối nhưng chưa chọn khối cụ thể
            SuggestionChips.Add(new AiSuggestionChip(
                "Kiểm tra an toàn sơ đồ", "🔍",
                "Rà soát và kiểm tra các Rule hiện tại trong dự án xem có xung đột ngõ ra hoặc thiếu an toàn không",
                UseQuickPromptCommand, category: "General"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Tối ưu hóa sơ đồ", "⚡",
                "Đề xuất tối ưu hóa sơ đồ logic FBD hiện tại để tăng độ tin cậy và tiết kiệm tài nguyên",
                UseQuickPromptCommand, category: "General"));
            SuggestionChips.Add(new AiSuggestionChip(
                "Thêm trễ an toàn", "⏱️",
                "Hướng dẫn bổ sung khối Timer để chống dội tín hiệu hoặc trễ ngắt bảo vệ động cơ",
                UseQuickPromptCommand, category: "General"));
        }
    }
}
