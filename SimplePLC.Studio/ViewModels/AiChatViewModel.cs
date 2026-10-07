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

    [RelayCommand]
    public void ApplyRules(ChatMessageModel? message)
    {
        if (message == null || !message.HasExtractedRules || message.IsApplied)
            return;

        OnApplyRulesRequested?.Invoke(message.ExtractedRules, message);
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

                    var allTags = _tagsProvider() ?? Enumerable.Empty<TagModel>();
                    var validation = AiSafetyGate.Validate(tx, allTags);

                    if (!validation.IsValid)
                    {
                        assistantMsg.Content = $"⚠️ **Cổng Kiểm Duyệt An Toàn Từ Chối:** {validation.ErrorMessage}\n\n*Khuyến nghị:* {validation.Guidance}";
                        assistantMsg.IsLoading = false;
                        return;
                    }

                    // Áp dụng Ghost Preview lên Canvas thật mà không xóa sơ đồ cũ
                    logicVM.ApplyAiProposalToCanvas(tx, $"Đề xuất AI: {response.Explanation.Split('\n')[0]}");

                    string toolSummary = $"\n\n✨ **Đã tạo bản vẽ đề xuất trực quan trên sơ đồ** ({tx.AddedNodes.Count} khối, {tx.AddedWires.Count} đường nối). Bạn hãy xem trước trên sơ đồ và nhấn **[✓ Chấp nhận]** để áp dụng vào dự án!";
                    assistantMsg.Content = response.Explanation + toolSummary;
                    assistantMsg.IsLoading = false;
                    return;
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
}
