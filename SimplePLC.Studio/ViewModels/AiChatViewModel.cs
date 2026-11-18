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

    private static readonly Regex VietnameseDiacriticsRegex = new(
        @"[àáảãạâầấẩẫậăằắẳẵặèéẻẽẹêềếểễệìíỉĩịòóỏõọôồốổỗộơờớởỡợùúủũụưừứửữựỳýỷỹỵđĐ]",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex VietnameseWordsRegex = new(
        @"\b(mach|bat|tat|bom|giup|cho toi|the nao|tai sao|kiem tra|giai thich|khi nao|nhan|nha|khoi dong|dung|dong co|van|so do|chuyen|tin hieu)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EnglishWordsRegex = new(
        @"\b(create|build|design|start|stop|circuit|explain|why|how|what|delay|timer|counter|scale|button|sensor|motor|pump|rule|safety|when|with|please|help|generate|make|turn|off|on|and|for)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Func<IEnumerable<TagModel>?> _tagsProvider;
    private readonly Func<IEnumerable<RuleItemModel>?> _rulesProvider;
    private readonly Func<LogicEditorViewModel?>? _logicEditorProvider;
    private readonly GeminiApiClient _apiClient = new();
    private readonly OpenAiCompatibleApiClient _groqClient = new();
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
    private string _selectedProvider = "Google Gemini";

    public IReadOnlyList<string> AvailableProviders { get; } = new List<string>
    {
        "Google Gemini",
        "Groq Cloud"
    };

    [ObservableProperty]
    private string _apiKeyInput = string.Empty;

    [ObservableProperty]
    private string _selectedModel = "gemini-3.8-flash";

    [ObservableProperty]
    private bool _isConfigured;

    [ObservableProperty]
    private string _settingsStatusMessage = string.Empty;

    [ObservableProperty]
    private string _apiKeyLabel = "Gemini API Key:";

    [ObservableProperty]
    private string _apiKeyHelpUrlText = "👉 Nhấp vào đây để lấy API Key miễn phí từ Google AI Studio";

    [ObservableProperty]
    private string _modelHelpText = "* Flash: Miễn phí 100% (cực nhanh).";

    [ObservableProperty]
    private string _onboardingBannerTitle = "Chưa kết nối Gemini API Key";

    [ObservableProperty]
    private string _onboardingBannerSubtitle = "Google cấp miễn phí 1.500 lượt hỏi/ngày. Hãy dán Key để bắt đầu sử dụng.";

    [ObservableProperty]
    private bool _isGroqSelected;

    [ObservableProperty]
    private IReadOnlyList<string> _availableModels = new List<string>
    {
        "gemini-3.8-flash",
        "gemini-2.0-flash",
        "gemini-1.5-flash"
    };

    [ObservableProperty]
    private string _selectedResponseLanguage = "Auto";

    public IReadOnlyList<string> AvailableResponseLanguages { get; } = new List<string>
    {
        "Auto",
        "English",
        "Vietnamese",
        "FollowApp"
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
        SelectedResponseLanguage = Settings.ResponseLanguage ?? "Auto";
        SelectedProvider = Settings.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)
            ? "Groq Cloud"
            : "Google Gemini";
        UpdateProviderState();
        IsConfigured = Settings.IsConfigured;

        ResetWelcomeMessage();
        UpdateContextualSuggestions();
        SubscribeToLogicEditor();

        // Đồng bộ song ngữ theo thời gian thực khi người dùng chuyển đổi ngôn ngữ ứng dụng
        LocalizationService.Instance.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged()
    {
        UpdateProviderState();
        UpdateContextualSuggestions();
        if (Messages.Count <= 1)
        {
            ResetWelcomeMessage();
        }
    }

    partial void OnSelectedProviderChanged(string value)
    {
        UpdateProviderState();
    }

    private void UpdateProviderState()
    {
        bool isGroq = SelectedProvider.Contains("Groq", StringComparison.OrdinalIgnoreCase);
        IsGroqSelected = isGroq;
        bool isVi = LocalizationService.Instance.IsVietnamese;

        if (isGroq)
        {
            AvailableModels = new List<string>
            {
                "openai/gpt-oss-120b",
                "openai/gpt-oss-20b",
                "qwen/qwen3.8-27b"
            };
            ApiKeyInput = Settings.EffectiveGroqApiKey;
            SelectedModel = string.IsNullOrWhiteSpace(Settings.GroqModel) ? "openai/gpt-oss-120b" : Settings.GroqModel;
            ApiKeyLabel = "Groq API Key:";
            ApiKeyHelpUrlText = isVi
                ? "👉 Nhấp vào đây để lấy API Key miễn phí từ Groq Console (console.groq.com)"
                : "👉 Click here to get a free API Key from Groq Console (console.groq.com)";
            ModelHelpText = isVi
                ? "* GPT-OSS 120B: Mô hình 120B siêu tốc (~0.2s) & tư duy logic FBD xuất sắc. Hoặc chọn GPT-OSS 20B / Qwen 3.8."
                : "* GPT-OSS 120B: Ultra-fast 120B model (~0.2s) with superior FBD reasoning. Or select GPT-OSS 20B / Qwen 3.8.";
            OnboardingBannerTitle = isVi ? "Chưa kết nối Groq API Key" : "Groq API Key Not Connected";
            OnboardingBannerSubtitle = isVi
                ? "Groq cấp miễn phí tốc độ siêu cao (LPU). Hãy dán Key để bắt đầu."
                : "Groq offers free high-speed LPU inference. Paste your API Key to get started.";
        }
        else
        {
            AvailableModels = new List<string>
            {
                "gemini-3.8-flash",
                "gemini-2.0-flash",
                "gemini-1.5-flash"
            };
            ApiKeyInput = Settings.EffectiveGeminiApiKey;
            SelectedModel = string.IsNullOrWhiteSpace(Settings.ModelName) ? "gemini-3.8-flash" : Settings.ModelName;
            ApiKeyLabel = "Gemini API Key:";
            ApiKeyHelpUrlText = isVi
                ? "👉 Nhấp vào đây để lấy API Key miễn phí từ Google AI Studio"
                : "👉 Click here to get a free API Key from Google AI Studio";
            ModelHelpText = isVi
                ? "* Flash: Miễn phí 100% (cực nhanh). Pro: Cho tài khoản có Billing."
                : "* Flash: 100% Free (ultra-fast). Pro: Requires billing account.";
            OnboardingBannerTitle = isVi ? "Chưa kết nối Gemini API Key" : "Gemini API Key Not Connected";
            OnboardingBannerSubtitle = isVi
                ? "Google cấp miễn phí 1.500 lượt hỏi/ngày. Hãy dán Key để bắt đầu sử dụng."
                : "Google provides 1,500 free queries/day. Paste your API Key to get started.";
        }
    }

    public void ResetWelcomeMessage()
    {
        Messages.Clear();
        bool isVi = LocalizationService.Instance.IsVietnamese;
        if (!IsConfigured)
        {
            bool isGroq = Settings.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase);
            string providerName = isGroq ? "Groq" : "Gemini";
            Messages.Add(new ChatMessageModel("assistant", isVi
                ? "Chào bạn! Tôi là **SynaptiX AI** — trợ lý thiết kế mạch và cấu hình logic PLC.\n\n" +
                  $"💡 Để bắt đầu, bạn hãy nhấn vào nút **[⚙️ Cài đặt]** ở góc trên để cấu hình **{providerName} API Key**."
                : "Hello! I am **SynaptiX AI** — your intelligent circuit design and PLC logic assistant.\n\n" +
                  $"💡 To get started, please click the **[⚙️ Settings]** button at the top to configure your **{providerName} API Key**."));
        }
        else
        {
            Messages.Add(new ChatMessageModel("assistant", isVi
                ? "Chào bạn! Tôi là **SynaptiX AI** — trợ lý thiết kế mạch và cấu hình logic PLC.\n\n" +
                  "Bạn chỉ cần mô tả bài toán bằng ngôn ngữ tự nhiên, tôi sẽ tự động thiết kế sơ đồ khối và đề xuất giải pháp trực quan để bạn xem trước trước khi áp dụng:\n" +
                  "• *'Mạch khởi động và dừng bơm DO0, có khóa an toàn nút dừng khẩn cấp DI2'*\n" +
                  "• *'Hẹn giờ trễ ngắt quạt làm mát DO1 sau 10 giây khi máy dừng'*\n" +
                  "• *'Cảm biến áp suất AI0 vượt quá 80 bar duy trì 5 giây thì đóng van xả DO0'*\n" +
                  "• *'Đếm đủ 100 sản phẩm từ cảm biến DI0 thì bật còi báo DO2'*\n" +
                  "• *'Quy đổi cảm biến áp suất analog AI0 (4-20mA) sang 0-10 bar'*\n\n" +
                  "Hãy nhập yêu cầu của bạn bên dưới!"
                : "Hello! I am **SynaptiX AI** — your industrial PLC logic synthesis and consulting copilot.\n\n" +
                  "Describe your automation requirement in natural language, and I will synthesize the FBD circuit and propose a visual draft preview for you to inspect and apply:\n" +
                  "• *'Start/Stop circuit for pump DO0 with emergency stop DI2 safety interlock'*\n" +
                  "• *'Off-delay timer for cooling fan DO1: keep running for 10 seconds after machine stops'*\n" +
                  "• *'Analog pressure sensor AI0 exceeds 80 bar for 5 seconds -> close discharge valve DO0'*\n" +
                  "• *'Count 100 items from sensor DI0 -> activate horn DO2'*\n" +
                  "• *'Calibrate analog 4-20mA pressure sensor AI0 to 0-10 bar'*\n\n" +
                  "Enter your request below!"));
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
            SelectedResponseLanguage = Settings.ResponseLanguage ?? "Auto";
            SelectedProvider = Settings.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)
                ? "Groq Cloud"
                : "Google Gemini";
            UpdateProviderState();
            SettingsStatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        bool isGroq = SelectedProvider.Contains("Groq", StringComparison.OrdinalIgnoreCase);
        if (isGroq)
        {
            Settings.Provider = "Groq";
            Settings.GroqApiKey = (ApiKeyInput ?? string.Empty).Trim();
            Settings.GroqModel = string.IsNullOrWhiteSpace(SelectedModel) ? "openai/gpt-oss-120b" : SelectedModel.Trim();
        }
        else
        {
            Settings.Provider = "Gemini";
            Settings.GeminiApiKey = (ApiKeyInput ?? string.Empty).Trim();
            Settings.ApiKey = Settings.GeminiApiKey;
            Settings.ModelName = string.IsNullOrWhiteSpace(SelectedModel) ? "gemini-3.8-flash" : SelectedModel.Trim();
        }

        Settings.ResponseLanguage = SelectedResponseLanguage;
        Settings.Save();

        IsConfigured = Settings.IsConfigured;
        IsSettingsOpen = false;
        bool isVi = LocalizationService.Instance.IsVietnamese;
        SettingsStatusMessage = isVi ? "Đã lưu cài đặt AI thành công!" : "AI settings saved successfully!";

        if (IsConfigured)
        {
            string providerDisplayName = isGroq ? "Groq Cloud" : "Google Gemini";
            string modelDisplayName = isGroq ? Settings.GroqModel : Settings.ModelName;
            Messages.Add(new ChatMessageModel("assistant", isVi
                ? $"⚙️ **Đã lưu cấu hình AI thành công!**\n- **Nhà cung cấp:** `{providerDisplayName}`\n- **Mô hình:** `{modelDisplayName}`\n- **Trạng thái:** Sẵn sàng nhận câu hỏi hoặc yêu cầu thiết kế mạch logic."
                : $"⚙️ **AI Configuration Saved Successfully!**\n- **Provider:** `{providerDisplayName}`\n- **Model:** `{modelDisplayName}`\n- **Status:** Ready for technical inquiries and logic circuit design."));
        }
        else if (Messages.Count <= 1)
        {
            ResetWelcomeMessage();
        }
    }

    [RelayCommand]
    public void OpenApiKeyPortal()
    {
        if (IsGroqSelected)
            OpenGroqConsole();
        else
            OpenGoogleAiStudio();
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
    public void OpenGroqConsole()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://console.groq.com/keys",
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
        OnNavigateToTab?.Invoke(AppTabIndices.RuleTable); // Chuyển sang Tab Bảng Quy Tắc (Rule Table)
    }

    [RelayCommand]
    public void ApplyRules(ChatMessageModel? message)
    {
        if (message == null || !message.HasExtractedRules || message.IsApplied)
            return;

        OnApplyRulesRequested?.Invoke(message.ExtractedRules, message);
        message.IsApplied = true;
        message.IsRejected = false;
        bool isVi = LocalizationService.Instance.IsVietnamese;
        message.AppliedStatusText = isVi
            ? $"✅ Đã đồng bộ thành công {message.ExtractedRules.Count} Rule vào Bảng Rule!"
            : $"✅ Synchronized {message.ExtractedRules.Count} Rules to Rule Table successfully!";
    }

    [RelayCommand]
    public void AcceptProposal(ChatMessageModel? message)
    {
        var logicVM = _logicEditorProvider?.Invoke();
        bool isVi = LocalizationService.Instance.IsVietnamese;
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
                string successFormat = LocalizationService.Instance.Get("AiChatAppliedSuccess");
                message.AppliedStatusText = totalRules > 0
                    ? $"✅ {string.Format(successFormat, totalRules)}"
                    : (isVi ? "✅ Đã áp dụng lên Canvas" : "✅ Applied to Canvas");
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
            bool isVi = LocalizationService.Instance.IsVietnamese;
            message.AppliedStatusText = isVi ? "✕ Đã hủy bỏ bản vẽ đề xuất nháp." : "✕ Cancelled draft proposal.";
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

        bool isVi = LocalizationService.Instance.IsVietnamese;
        if (!IsConfigured && ApiHandler == null)
        {
            IsSettingsOpen = true;
            SettingsStatusMessage = isVi
                ? "Vui lòng nhập API Key để bắt đầu chat."
                : "Please enter an API Key to start chatting.";
            return;
        }

        string userPrompt = InputText.Trim();
        InputText = string.Empty;

        // Phân loại ngôn ngữ giao tiếp của người dùng (Chat Space) và ngôn ngữ phần mềm (App Space)
        bool isEnglish = DetectIsEnglish(userPrompt);
        bool isAppEnglish = !LocalizationService.Instance.IsVietnamese;

        var userMsg = new ChatMessageModel("user", userPrompt);
        Messages.Add(userMsg);

        string loadingText = isEnglish
            ? "Aggregating project context and synthesizing solution..."
            : "Đang tổng hợp thông tin dự án...";
        var assistantMsg = new ChatMessageModel("assistant", loadingText, isLoading: true);
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

            // 3. Quản lý bộ nhớ hội thoại trượt tinh gọn (Tối đa 4 tin nhắn gần nhất để chống tràn Rate Limit)
            var history = new List<(string role, string text)>();
            var contextMessages = Messages
                .Where(m => !m.IsLoading && !m.IsError && !string.IsNullOrWhiteSpace(m.Content))
                .TakeLast(4)
                .ToList();

            foreach (var m in contextMessages)
            {
                if (m == userMsg)
                {
                    // Tối ưu hóa Token & Phản chiếu ngôn ngữ chuẩn:
                    // Với câu hỏi tư vấn / kiến thức kỹ thuật (ConsultativeQnA),
                    // KHÔNG chèn khối [DỮ LIỆU NGỮ CẢNH DỰ ÁN] nặng hàng nghìn tokens.
                    string enrichedPrompt;
                    if (intent == AiUserIntent.ConsultativeQnA)
                    {
                        enrichedPrompt = userPrompt;
                    }
                    else if (isEnglish)
                    {
                        enrichedPrompt = $"[PROJECT CONTEXT DATA]\n{contextJson.ToJsonString()}\n\nEngineer Request: {userPrompt}";
                    }
                    else
                    {
                        enrichedPrompt = $"[DỮ LIỆU NGỮ CẢNH DỰ ÁN]\n{contextJson.ToJsonString()}\n\nYêu cầu kỹ sư: {userPrompt}";
                    }
                    history.Add((m.Role, enrichedPrompt));
                }
                else if (m.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
                {
                    string sanitized = SanitizeHistoryMessage(m.Content);
                    if (!string.IsNullOrWhiteSpace(sanitized))
                    {
                        // Rút gọn tin nhắn cũ của assistant (tối đa 350 ký tự) để tránh nổ token khi hỏi các lượt tiếp theo
                        if (sanitized.Length > 350)
                        {
                            sanitized = isEnglish
                                ? sanitized.Substring(0, 350) + "... [Summarized]"
                                : sanitized.Substring(0, 350) + "... [Đã tóm lược]";
                        }
                        history.Add((m.Role, sanitized));
                    }
                }
                else
                {
                    string userContent = m.Content;
                    if (userContent.Length > 250)
                    {
                        userContent = userContent.Substring(0, 250) + "...";
                    }
                    history.Add((m.Role, userContent));
                }
            }

            // 4. Chọn System Prompt và Tool Declarations tương ứng với Intent & Provider
            string systemInstruction = AiPromptBuilder.BuildSystemInstruction(intent, isEnglish);
            bool isGroq = Settings.Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase);

            // Groq Cloud Free Tier có trần giới hạn 8,000 ITPM (Input Tokens Per Minute).
            // Mảng 10 tool schemas của OpenAI chiếm tới ~3,500 - 4,000 tokens khiến tổng payload vượt trần ITPM và báo lỗi HTTP 413.
            // Do đó với Groq, hệ thống tối ưu bằng cơ chế Structured JSON Rules Output qua AiRuleParser (chỉ ~1,000 tokens),
            // vừa đảm bảo độ chính xác FBD tuyệt đối, vừa sinh cực nhanh (<1s) và không bao giờ chạm trần Rate Limit.
            var toolDeclarations = (intent == AiUserIntent.FbdSynthesis && !isGroq)
                ? AiToolDeclarations.GetGeminiFunctionDeclarations()
                : null;

            assistantMsg.Content = isEnglish ? (intent switch
            {
                AiUserIntent.ConsultativeQnA => "Analyzing and formulating technical advice...",
                AiUserIntent.ProjectDiagnose => "Running diagnostics and safety checks...",
                _ => "Synthesizing control logic solution..."
            }) : (intent switch
            {
                AiUserIntent.ConsultativeQnA => "Đang phân tích và tư vấn kỹ thuật...",
                AiUserIntent.ProjectDiagnose => "Đang chẩn đoán và kiểm tra an toàn...",
                _ => "Đang thiết kế giải pháp điều khiển..."
            });

            // 5. Gọi LLM Engine theo Provider
            GeminiToolResponse response;
            if (ApiHandler != null)
            {
                response = await ApiHandler(Settings, systemInstruction, history, toolDeclarations, _cts.Token);
            }
            else if (isGroq)
            {
                response = await _groqClient.GenerateContentWithToolsAsync(
                    Settings,
                    systemInstruction,
                    history,
                    toolDeclarations,
                    _cts.Token);
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
                            assistantMsg.Content = isEnglish
                                ? $"⚠️ **Safety Gate Rejection:** {validation.ErrorMessage}\n\n*Guidance:* {validation.Guidance}"
                                : $"⚠️ **Cổng Kiểm Duyệt An Toàn Từ Chối:** {validation.ErrorMessage}\n\n*Khuyến nghị:* {validation.Guidance}";
                            assistantMsg.IsLoading = false;
                            return;
                        }

                        // Áp dụng Ghost Preview lên Canvas thật mà không xóa sơ đồ cũ
                        string summaryFormat = LocalizationService.Instance.Get("AiChatProposalSummary");
                        string summary = string.Format(summaryFormat, tx.AddedNodes.Count, tx.AddedWires.Count);
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
                        assistantMsg.Content = isEnglish
                            ? $"⚠️ **Safety Gate Rejection:** {ruleValidation.ErrorMessage}\n\n*Guidance:* {ruleValidation.Guidance}"
                            : $"⚠️ **Cổng Kiểm Duyệt An Toàn Từ Chối:** {ruleValidation.ErrorMessage}\n\n*Khuyến nghị:* {ruleValidation.Guidance}";
                        assistantMsg.IsLoading = false;
                        return;
                    }

                    var tx = new DraftGraphTransaction();
                    var existingNodes = logicVMInstance.Nodes;
                    double startY = existingNodes.Count > 0
                        ? existingNodes.Max(n => n.Location.Y) + 200
                        : 100;
                    double x = 60, y = startY;
                    int ruleIdx = 0;

                    foreach (var rule in extractedRules)
                    {
                        string suffix = Guid.NewGuid().ToString("N")[..6];
                        string inId = $"node_in_{ruleIdx}_{suffix}";
                        string trgId = $"node_trg_{ruleIdx}_{suffix}";
                        string cmpId = $"node_cmp_{ruleIdx}_{suffix}";
                        string actId = $"node_act_{ruleIdx}_{suffix}";
                        string grdId = $"node_grd_{ruleIdx}_{suffix}";

                        bool isTimeWindow = string.Equals(rule.Trigger, "TIME_WINDOW", StringComparison.OrdinalIgnoreCase);
                        bool isComparison = !isTimeWindow && !string.IsNullOrWhiteSpace(rule.CompareOp) &&
                                            !string.Equals(rule.CompareOp, "NONE", StringComparison.OrdinalIgnoreCase);

                        string lastId;
                        double currentX;

                        if (isTimeWindow)
                        {
                            // Archetype 2: Hẹn giờ RTC độc lập (phần cứng tự đọc đồng hồ, không sinh node Input)
                            string timeDesc = rule.ThresholdLo > 0
                                ? $"{(int)rule.ThresholdLo / 100:D2}:{(int)rule.ThresholdLo % 100:D2}"
                                : "RTC";
                            bool isRange = rule.ThresholdHi > rule.ThresholdLo && rule.ThresholdLo > 0;
                            tx.AddedNodes.Add(new DraftNode
                            {
                                Id = trgId,
                                NodeType = "Trigger",
                                Label = isAppEnglish ? $"Time Window ({timeDesc})" : $"Khung giờ ({timeDesc})",
                                PositionX = x,
                                PositionY = y,
                                TriggerType = "TIME_WINDOW",
                                CompareOp = isRange ? "BETWEEN" : "EQ",
                                ThresholdLo = rule.ThresholdLo,
                                ThresholdHi = rule.ThresholdHi > 0 ? rule.ThresholdHi : rule.ThresholdLo
                            });

                            lastId = trgId;
                            currentX = x;
                        }
                        else if (isComparison)
                        {
                            // Archetype 3: So sánh điều kiện / cảm biến độc lập qua khối Comparator (CMP)
                            tx.AddedNodes.Add(new DraftNode
                            {
                                Id = inId,
                                NodeType = "Input",
                                Label = isAppEnglish ? $"Input ({rule.InputTag})" : $"Ngõ vào ({rule.InputTag})",
                                PositionX = x,
                                PositionY = y,
                                TagName = rule.InputTag
                            });

                            tx.AddedNodes.Add(new DraftNode
                            {
                                Id = cmpId,
                                NodeType = "Comparator",
                                Label = isAppEnglish ? $"CMP ({rule.CompareOp} {rule.ThresholdLo})" : $"So sánh ({rule.CompareOp} {rule.ThresholdLo})",
                                PositionX = x + 240,
                                PositionY = y,
                                CompareOp = rule.CompareOp,
                                ThresholdLo = rule.ThresholdLo,
                                ThresholdHi = rule.ThresholdHi,
                                TagName = rule.InputTag
                            });

                            tx.AddedWires.Add(new DraftWire
                            {
                                SourceNodeId = inId,
                                SourcePort = "Out",
                                TargetNodeId = cmpId,
                                TargetPort = "In"
                            });

                            lastId = cmpId;
                            currentX = x + 240;
                        }
                        else
                        {
                            // Archetype 1: Luồng sự kiện số / sườn xung thông thường (ON_RISE, ON_FALL, INTERVAL)
                            tx.AddedNodes.Add(new DraftNode
                            {
                                Id = inId,
                                NodeType = "Input",
                                Label = isAppEnglish ? $"Input ({rule.InputTag})" : $"Ngõ vào ({rule.InputTag})",
                                PositionX = x,
                                PositionY = y,
                                TagName = rule.InputTag
                            });

                            tx.AddedNodes.Add(new DraftNode
                            {
                                Id = trgId,
                                NodeType = "Trigger",
                                Label = isAppEnglish ? $"Trigger ({rule.Trigger})" : $"Kích hoạt ({rule.Trigger})",
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

                            lastId = trgId;
                            currentX = x + 240;
                        }

                        if (!string.IsNullOrWhiteSpace(rule.GuardTag) && !string.Equals(rule.GuardTag, "NONE", StringComparison.OrdinalIgnoreCase))
                        {
                            currentX += 240;
                            tx.AddedNodes.Add(new DraftNode
                            {
                                Id = grdId,
                                NodeType = "Guard",
                                Label = isAppEnglish ? $"Guard ({rule.GuardTag})" : $"Khóa an toàn ({rule.GuardTag})",
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
                            Label = isAppEnglish ? $"Action ({rule.ActionTag})" : $"Tác vụ ({rule.ActionTag})",
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
                            assistantMsg.Content = isEnglish
                                ? $"⚠️ **Safety Gate Rejection:** {validation.ErrorMessage}\n\n*Guidance:* {validation.Guidance}"
                                : $"⚠️ **Cổng Kiểm Duyệt An Toàn Từ Chối:** {validation.ErrorMessage}\n\n*Khuyến nghị:* {validation.Guidance}";
                            assistantMsg.IsLoading = false;
                            return;
                        }

                        string summaryFormat = LocalizationService.Instance.Get("AiChatProposalSummary");
                        string summary = string.Format(summaryFormat, tx.AddedNodes.Count, tx.AddedWires.Count);
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
            assistantMsg.Content = isEnglish ? "*Request cancelled.*" : "*Đã hủy yêu cầu.*";
            assistantMsg.IsLoading = false;
        }
        catch (Exception ex)
        {
            assistantMsg.Content = isEnglish ? $"❌ **Error:** {ex.Message}" : $"❌ **Lỗi:** {ex.Message}";
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
    /// Phát hiện xem câu hỏi của người dùng hoặc cấu hình phản hồi nên sử dụng tiếng Anh (true) hay tiếng Việt (false).
    /// </summary>
    public bool DetectIsEnglish(string? prompt)
    {
        string pref = Settings.ResponseLanguage ?? "Auto";
        if (pref.Equals("English", StringComparison.OrdinalIgnoreCase))
            return true;
        if (pref.Equals("Vietnamese", StringComparison.OrdinalIgnoreCase))
            return false;
        if (pref.Equals("FollowApp", StringComparison.OrdinalIgnoreCase))
            return !LocalizationService.Instance.IsVietnamese;

        // "Auto" (Default): Phân tích nội dung prompt của kỹ sư
        if (string.IsNullOrWhiteSpace(prompt))
            return !LocalizationService.Instance.IsVietnamese;

        // 1. Nếu có dấu tiếng Việt -> 100% tiếng Việt
        if (VietnameseDiacriticsRegex.IsMatch(prompt))
            return false;

        // 2. Nếu có từ khóa tiếng Việt không dấu -> tiếng Việt
        if (VietnameseWordsRegex.IsMatch(prompt))
            return false;

        // 3. Nếu có từ khóa kỹ thuật tiếng Anh -> tiếng Anh
        if (EnglishWordsRegex.IsMatch(prompt))
            return true;

        // 4. Mặc định fallback theo ngôn ngữ đang kích hoạt của IDE
        return !LocalizationService.Instance.IsVietnamese;
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
    /// dựa theo trạng thái Canvas FBD (khối rỗng, khối được chọn, lỗi biên dịch, chế độ mô phỏng) và ngôn ngữ hiện tại.
    /// </summary>
    public void UpdateContextualSuggestions()
    {
        SubscribeToLogicEditor();

        var logicVM = _logicEditorProvider?.Invoke();
        var allTags = _tagsProvider?.Invoke();
        int tagCount = allTags?.Count() ?? 0;
        bool isVi = LocalizationService.Instance.IsVietnamese;

        // Cập nhật các chỉ báo ngữ cảnh thời gian thực (Active Context Status Indicators)
        if (logicVM != null)
        {
            int nodeCount = logicVM.Nodes.Count(n => !n.IsGhost);
            int wireCount = logicVM.Connections.Count(c => !c.IsGhost);
            string blocksWiresFormat = LocalizationService.Instance.Get("AiChatBlocksAndWires");
            ActiveContextNodeCountText = wireCount > 0
                ? $"📍 {string.Format(blocksWiresFormat, nodeCount, wireCount)}"
                : $"📍 {nodeCount} {(isVi ? "khối" : "blocks")}";

            bool hasErrors = logicVM.CompileState == CompileState.Invalid ||
                             (logicVM.Diagnostics != null && logicVM.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error)) ||
                             logicVM.HasSimGateWarning;

            if (hasErrors)
            {
                ActiveContextCompileStatusText = isVi ? "⚠️ Lỗi sơ đồ" : "⚠️ Diagram Error";
                ActiveContextCompileIsError = true;
            }
            else if (logicVM.CompileState == CompileState.Valid)
            {
                ActiveContextCompileStatusText = LocalizationService.Instance.Get("AiChatStatusValid");
                ActiveContextCompileIsError = false;
            }
            else
            {
                ActiveContextCompileStatusText = isVi ? "⚪ Chưa biên dịch" : "⚪ Uncompiled";
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
            ActiveContextNodeCountText = isVi ? "📍 0 khối" : "📍 0 blocks";
            ActiveContextCompileStatusText = LocalizationService.Instance.Get("AiChatStatusReady");
            ActiveContextCompileIsError = false;
            HasActiveContextSelection = false;
            ActiveContextSelectionText = string.Empty;
        }

        ActiveContextTagCountText = $"🏷️ {string.Format(LocalizationService.Instance.Get("AiChatTagsCount"), tagCount)}";

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
            if (isVi)
            {
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
            else
            {
                SuggestionChips.Add(new AiSuggestionChip(
                    "Fix compile errors", "⚠️",
                    "Analyze causes and guide how to resolve current compile errors in diagram",
                    UseQuickPromptCommand, category: "Diagnostic"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Explain missing wire", "🔍",
                    "Explain unconnected ports and suggest safe wire connections",
                    UseQuickPromptCommand, category: "Diagnostic"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Check output conflict", "⚡",
                    "Check if multiple Actions drive the same DO output or violate rules",
                    UseQuickPromptCommand, category: "Diagnostic"));
            }
        }
        else if (isSimActive)
        {
            if (isVi)
            {
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
            else
            {
                SuggestionChips.Add(new AiSuggestionChip(
                    "Monitor DO outputs", "📊",
                    "Guide how to monitor digital outputs DO0..DO7 during simulation scan cycle",
                    UseQuickPromptCommand, category: "Simulation"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Force AI0 sensor test", "⚡",
                    "Guide how to force analog input AI0 value to test trigger thresholds",
                    UseQuickPromptCommand, category: "Simulation"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Check response latency", "⏱️",
                    "Evaluate signal response time and debouncing latency under simulation",
                    UseQuickPromptCommand, category: "Simulation"));
            }
        }
        else if (selectedNode is TimerNodeViewModel timerNode)
        {
            string title = timerNode.Title;
            if (isVi)
            {
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
            else
            {
                SuggestionChips.Add(new AiSuggestionChip(
                    $"Configure delay ({title})", "⏱️",
                    $"Guide how to configure 5000ms delay for {title} block and explain IN, Q ports",
                    UseQuickPromptCommand, category: "Timer"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Switch TON / TOF mode", "🔄",
                    $"Compare and switch between TON on-delay and TOF off-delay for {title}",
                    UseQuickPromptCommand, category: "Timer"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Verify Q output wire", "🔍",
                    $"Verify whether Q output of {title} is connected to an actuator DO block",
                    UseQuickPromptCommand, category: "Timer"));
            }
        }
        else if (selectedNode is ScaleNodeViewModel scaleNode)
        {
            string title = scaleNode.Title;
            if (isVi)
            {
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
            else
            {
                SuggestionChips.Add(new AiSuggestionChip(
                    "Calibrate 4-20mA (0-10 bar)", "📐",
                    $"Guide how to calibrate 4-20mA current (2000-10000 mV) to 0..10 bar for {title}",
                    UseQuickPromptCommand, category: "Scale"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Change unit to °C", "🌡️",
                    $"Configure {title} to convert PT100 temperature sensor to °C (-20..150°C)",
                    UseQuickPromptCommand, category: "Scale"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Safety clamp limits", "🛡️",
                    $"Guide how to configure Clamp Min/Max for {title} to avoid value overflow",
                    UseQuickPromptCommand, category: "Scale"));
            }
        }
        else if (selectedNode is CounterNodeViewModel counterNode)
        {
            string title = counterNode.Title;
            if (isVi)
            {
                SuggestionChips.Add(new AiSuggestionChip(
                    $"Cấu hình đếm ({title})", "🔢",
                    $"Hướng dẫn đặt giá trị Preset Value (PV=100) cho khối {title} và kết nối tín hiệu Reset",
                    UseQuickPromptCommand, category: "Counter"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Chuyển chế độ CTU / CTD", "🔄",
                    $"So sánh và hướng dẫn chuyển đổi đếm tiến CTU và đếm lùi CTD cho khối {title}",
                    UseQuickPromptCommand, category: "Counter"));
            }
            else
            {
                SuggestionChips.Add(new AiSuggestionChip(
                    $"Configure counter ({title})", "🔢",
                    $"Guide how to set Preset Value (PV=100) for {title} and connect Reset signal",
                    UseQuickPromptCommand, category: "Counter"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Switch CTU / CTD mode", "🔄",
                    $"Compare and switch between CTU up-counter and CTD down-counter for {title}",
                    UseQuickPromptCommand, category: "Counter"));
            }
        }
        else if (logicVM == null || !logicVM.Nodes.Any(n => !n.IsGhost))
        {
            if (isVi)
            {
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
                SuggestionChips.Add(new AiSuggestionChip(
                    "Start / Stop Circuit", "⚡",
                    "Create a Start/Stop circuit for pump DO0 with pushbuttons DI0 (Start) and DI1 (Stop)",
                    UseQuickPromptCommand, category: "Starter"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Production Counter", "📊",
                    "Build a production counter from sensor DI2 saved safely to Flash Retain VREG_RETAIN0",
                    UseQuickPromptCommand, category: "Starter"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "4-20mA Scaler", "📐",
                    "Guide how to use SCALE block to calibrate 4-20mA pressure sensor (2000..10000 mV) to 0..16 bar",
                    UseQuickPromptCommand, category: "Starter"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "TON On-Delay", "⏱️",
                    "TON on-delay circuit: hold run button DI0 for 5s continuously to start lubrication pump DO0",
                    UseQuickPromptCommand, category: "Starter"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Safety Interlock", "🛡️",
                    "Reversing motor control circuit with safety mutual interlock",
                    UseQuickPromptCommand, category: "Starter"));
            }
        }
        else
        {
            if (isVi)
            {
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
            else
            {
                SuggestionChips.Add(new AiSuggestionChip(
                    "Safety Audit", "🔍",
                    "Audit current rules in project for output conflicts or missing safety interlocks",
                    UseQuickPromptCommand, category: "General"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Optimize Diagram", "⚡",
                    "Suggest optimizations for current FBD logic diagram to increase reliability",
                    UseQuickPromptCommand, category: "General"));
                SuggestionChips.Add(new AiSuggestionChip(
                    "Add Safety Delay", "⏱️",
                    "Guide how to add Timer block for contact debouncing or motor protection off-delay",
                    UseQuickPromptCommand, category: "General"));
            }
        }
    }
}
