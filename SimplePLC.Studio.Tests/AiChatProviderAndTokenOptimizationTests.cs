using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử đơn vị toàn diện cho tính năng Nâng cấp AI:
/// 1. Định dạng OpenAI / Groq Tool Declarations
/// 2. Cơ chế cắt tỉa ngữ cảnh tiết kiệm Token (Intent-Based Context Pruning)
/// 3. Cơ chế chuyển đổi Provider linh hoạt giữa Google Gemini và Groq Cloud
/// 4. Quản lý cấu hình AiSettingsModel đa nhà cung cấp
/// </summary>
public class AiChatProviderAndTokenOptimizationTests
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

    [Fact]
    public async Task GeminiApiClient_WithoutApiKey_ThrowsInvalidOperationException()
    {
        var client = new GeminiApiClient();
        var settings = new AiSettingsModel { GeminiApiKey = string.Empty, ApiKey = string.Empty };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GenerateContentAsync(settings, "Sys instruction", new List<(string role, string text)>()));
    }

    [Fact]
    public async Task OpenAiCompatibleApiClient_WithoutApiKey_ThrowsInvalidOperationException()
    {
        var client = new OpenAiCompatibleApiClient();
        var settings = new AiSettingsModel { GroqApiKey = string.Empty };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GenerateContentAsync(settings, "Sys instruction", new List<(string role, string text)>()));
    }

    [Fact]
    public void AiToolDeclarations_GetOpenAiFunctionDeclarations_ContainsFunctionTools()
    {
        // Act
        var openAiTools = AiToolDeclarations.GetOpenAiFunctionDeclarations();

        // Assert
        Assert.NotNull(openAiTools);
        Assert.Equal(11, openAiTools.Count);

        foreach (var node in openAiTools)
        {
            Assert.NotNull(node);
            var obj = node.AsObject();
            Assert.Equal("function", obj["type"]?.GetValue<string>());

            var fn = obj["function"]?.AsObject();
            Assert.NotNull(fn);
            Assert.False(string.IsNullOrWhiteSpace(fn["name"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(fn["description"]?.GetValue<string>()));
            Assert.NotNull(fn["parameters"]);
        }
    }

    [Fact]
    public void AiChatViewModel_ProviderSwitch_UpdatesAvailableModelsAndKey()
    {
        RunOnSta(() =>
        {
            // Arrange
            var settings = new AiSettingsModel
            {
                Provider = "Gemini",
                GeminiApiKey = "gemini-secret-key",
                GroqApiKey = "groq-secret-key",
                ModelName = "gemini-3.8-flash",
                GroqModel = "openai/gpt-oss-120b"
            };

            var vm = new AiChatViewModel(
                () => Enumerable.Empty<TagModel>(),
                () => Enumerable.Empty<RuleItemModel>(),
                () => null,
                settings);

            // Assert ban đầu là Gemini
            Assert.Contains("gemini-3.8-flash", vm.AvailableModels);
            Assert.Equal("gemini-secret-key", vm.ApiKeyInput);

            // Act: Chuyển sang Groq Cloud
            vm.SelectedProvider = "Groq Cloud";

            // Assert: Danh sách model và Key tự cập nhật sang Groq
            Assert.Contains("openai/gpt-oss-120b", vm.AvailableModels);
            Assert.Equal("groq-secret-key", vm.ApiKeyInput);
            Assert.Equal("openai/gpt-oss-120b", vm.SelectedModel);
            Assert.True(vm.IsGroqSelected);

            // Act: Chuyển lại Google Gemini
            vm.SelectedProvider = "Google Gemini";

            // Assert: Quay về Gemini
            Assert.Contains("gemini-3.8-flash", vm.AvailableModels);
            Assert.Equal("gemini-secret-key", vm.ApiKeyInput);
            Assert.False(vm.IsGroqSelected);
        });
    }

    [Fact]
    public void AiChatViewModel_SaveSettings_StoresProviderSpecificValues()
    {
        RunOnSta(() =>
        {
            var settings = new AiSettingsModel();
            var vm = new AiChatViewModel(
                () => Enumerable.Empty<TagModel>(),
                () => Enumerable.Empty<RuleItemModel>(),
                () => null,
                settings);

            // Chuyển sang Groq và lưu
            vm.SelectedProvider = "Groq Cloud";
            vm.ApiKeyInput = "gsk_test_groq_123456";
            vm.SelectedModel = "openai/gpt-oss-120b";
            vm.SaveSettings();

            Assert.Equal("Groq", settings.Provider);
            Assert.Equal("gsk_test_groq_123456", settings.GroqApiKey);
            Assert.Equal("openai/gpt-oss-120b", settings.GroqModel);
            Assert.True(settings.IsConfigured);

            // Chuyển sang Gemini và lưu
            vm.SelectedProvider = "Google Gemini";
            vm.ApiKeyInput = "AIzaSy_test_gemini_789";
            vm.SelectedModel = "gemini-2.0-flash";
            vm.SaveSettings();

            Assert.Equal("Gemini", settings.Provider);
            Assert.Equal("AIzaSy_test_gemini_789", settings.GeminiApiKey);
            Assert.Equal("gemini-2.0-flash", settings.ModelName);
            Assert.True(settings.IsConfigured);
        });
    }

    [Fact]
    public async Task AiChatViewModel_IntentPruning_ConsultativeQnA_OmitsContextJson()
    {
        await Task.Run(() =>
        {
            RunOnSta(async () =>
            {
                // Arrange
                var tags = new List<TagModel>
                {
                    new TagModel { Name = "DI0", DataType = TagDataType.Boolean, Kind = TagKind.DiscreteInput }
                };

                var settings = new AiSettingsModel
                {
                    GeminiApiKey = "test-key"
                };

                var vm = new AiChatViewModel(
                    () => tags,
                    () => Enumerable.Empty<RuleItemModel>(),
                    () => null,
                    settings);

                string receivedPrompt = string.Empty;
                JsonArray? receivedTools = null;

                vm.ApiHandler = (st, sys, hist, tools, ct) =>
                {
                    var lastUserMsg = hist.LastOrDefault(h => h.role == "user");
                    receivedPrompt = lastUserMsg.text;
                    receivedTools = tools;
                    return Task.FromResult(new GeminiToolResponse("Khối TON là bộ định thời trễ bật.", new List<AiToolCall>()));
                };

                // Act: Đặt câu hỏi tư vấn lý thuyết
                vm.InputText = "Khối TON hoạt động như thế nào trong tiêu chuẩn IEC 61131-3?";
                await vm.SendMessageCommand.ExecuteAsync(null);

                // Assert: Với ConsultativeQnA, [DỮ LIỆU NGỮ CẢNH DỰ ÁN] PHẢI BỊ CẮT BỎ để tiết kiệm token
                Assert.DoesNotContain("[DỮ LIỆU NGỮ CẢNH DỰ ÁN]", receivedPrompt);
                Assert.Equal("Khối TON hoạt động như thế nào trong tiêu chuẩn IEC 61131-3?", receivedPrompt);
                Assert.Null(receivedTools); // Tools cũng không cần gửi
            });
        });
    }

    [Fact]
    public async Task AiChatViewModel_IntentPruning_FbdSynthesis_IncludesContextJsonAndTools()
    {
        await Task.Run(() =>
        {
            RunOnSta(async () =>
            {
                var tags = new List<TagModel>
                {
                    new TagModel { Name = "DI0", DataType = TagDataType.Boolean, Kind = TagKind.DiscreteInput }
                };

                var settings = new AiSettingsModel
                {
                    GeminiApiKey = "test-key"
                };

                var vm = new AiChatViewModel(
                    () => tags,
                    () => Enumerable.Empty<RuleItemModel>(),
                    () => null,
                    settings);

                string receivedPrompt = string.Empty;
                JsonArray? receivedTools = null;

                vm.ApiHandler = (st, sys, hist, tools, ct) =>
                {
                    var lastUserMsg = hist.LastOrDefault(h => h.role == "user");
                    receivedPrompt = lastUserMsg.text;
                    receivedTools = tools;
                    return Task.FromResult(new GeminiToolResponse("Đã tạo mạch Start/Stop.", new List<AiToolCall>()));
                };

                // Act: Yêu cầu thiết kế mạch FBD
                vm.InputText = "Tạo mạch start stop bật bơm DO0 với nút nhấn DI0";
                await vm.SendMessageCommand.ExecuteAsync(null);

                // Assert: Với FbdSynthesis, ngữ cảnh dự án và tools PHẢI ĐƯỢC TRUYỀN ĐẦY ĐỦ
                Assert.Contains("[DỮ LIỆU NGỮ CẢNH DỰ ÁN]", receivedPrompt);
                Assert.NotNull(receivedTools);
                Assert.NotEmpty(receivedTools);
            });
        });
    }

    [Fact]
    public void AiSettingsModel_EffectiveKeys_FallbackGracefully()
    {
        // Kiểm tra tính tương thích ngược khi file cũ chỉ có ApiKey
        var settingsLegacy = new AiSettingsModel
        {
            ApiKey = "legacy-key-123"
        };

        Assert.Equal("legacy-key-123", settingsLegacy.EffectiveGeminiApiKey);
        Assert.Equal("legacy-key-123", settingsLegacy.CurrentApiKey);

        // Khi có GeminiApiKey riêng
        var settingsNew = new AiSettingsModel
        {
            ApiKey = "old-key",
            GeminiApiKey = "new-gemini-key",
            GroqApiKey = "groq-key"
        };

        settingsNew.Provider = "Gemini";
        Assert.Equal("new-gemini-key", settingsNew.CurrentApiKey);

        settingsNew.Provider = "Groq";
        Assert.Equal("groq-key", settingsNew.CurrentApiKey);
    }

    [Fact]
    public async Task AiChatViewModel_GroqProvider_FbdSynthesis_OmitsToolsToPreventRateLimit413()
    {
        await Task.Run(() =>
        {
            RunOnSta(async () =>
            {
                // Arrange: Cấu hình Provider là Groq
                var tags = new List<TagModel>
                {
                    new() { Name = "DI0", DataType = TagDataType.Boolean },
                    new() { Name = "DI1", DataType = TagDataType.Boolean },
                    new() { Name = "DO0", DataType = TagDataType.Boolean }
                };

                var settings = new AiSettingsModel
                {
                    Provider = "Groq",
                    GroqApiKey = "test-groq-key",
                    GroqModel = "openai/gpt-oss-120b"
                };

                var vm = new AiChatViewModel(
                    () => tags,
                    () => Enumerable.Empty<RuleItemModel>(),
                    () => null,
                    settings);

                JsonArray? receivedTools = null;

                vm.ApiHandler = (st, sys, hist, tools, ct) =>
                {
                    receivedTools = tools;
                    string resp = """
                        ┌── Rule 1: Bật Bơm ──┐
                        │ [DI0] -> [DO0=1]    │
                        └───┘
                        ```json:rules
                        [
                          { "narrative": "Bật bơm", "input_tag": "DI0", "trigger": "ON_RISE", "for_ms": 50, "guard_tag": "DI1", "action_tag": "DO0", "action_type": "SET_TAG", "action_param": 1 }
                        ]
                        ```
                        """;
                    return Task.FromResult(new GeminiToolResponse(resp, new List<AiToolCall>()));
                };

                // Act: Yêu cầu thiết kế mạch FBD
                vm.InputText = "Mạch Start/Stop bật tắt bơm DO0 với nút nhấn DI0 (Start) và DI1 (Stop)";
                await vm.SendMessageCommand.ExecuteAsync(null);

                // Assert: Với Groq, tools BẮT BUỘC LÀ NULL để không bị trần rate limit ITPM 8000 gây lỗi 413
                Assert.Null(receivedTools);
            });
        });
    }

    [Fact]
    public void AiContextAggregator_WhenUltraCompact_ReducesJsonPayloadSizeSignificantly()
    {
        // Arrange
        var tags = new List<TagModel>
        {
            new() { Name = "DI0", DataType = TagDataType.Boolean, Alias = "Start Button" },
            new() { Name = "DI1", DataType = TagDataType.Boolean, Alias = "Stop Button" },
            new() { Name = "DO0", DataType = TagDataType.Boolean, Alias = "Pump Motor" },
            new() { Name = "DO1", DataType = TagDataType.Boolean, Alias = "Valve Actuator" },
            new() { Name = "AI0", DataType = TagDataType.Float32, Alias = "Pressure" }
        };

        var logicVM = new LogicEditorViewModel(new TagCatalogViewModel());
        var node1 = new InputNodeViewModel { Id = "node1", Location = new System.Windows.Point(100, 200) };
        var node2 = new ActionNodeViewModel { Id = "node2", Location = new System.Windows.Point(340, 200) };
        logicVM.Nodes.Add(node1);
        logicVM.Nodes.Add(node2);

        var aggregator = new AiContextAggregator(() => tags, () => logicVM);

        // Act
        var normalPayload = aggregator.BuildContextPayload(isUltraCompact: false);
        var ultraCompactPayload = aggregator.BuildContextPayload(isUltraCompact: true);

        // Assert
        Assert.NotNull(normalPayload["hardware_profile"]);
        Assert.Null(ultraCompactPayload["hardware_profile"]);

        var ultraNodes = ultraCompactPayload["canvas_nodes"]?.AsArray();
        Assert.NotNull(ultraNodes);
        foreach (var n in ultraNodes)
        {
            var obj = n!.AsObject();
            Assert.False(obj.ContainsKey("pos_x"), "UltraCompact must omit pos_x");
            Assert.False(obj.ContainsKey("pos_y"), "UltraCompact must omit pos_y");
        }

        string normalStr = normalPayload.ToJsonString();
        string ultraStr = ultraCompactPayload.ToJsonString();
        Assert.True(ultraStr.Length < normalStr.Length, "UltraCompact JSON payload must be noticeably shorter");
    }

    private class Mock429ThenSuccessHandler : HttpMessageHandler
    {
        private int _callCount = 0;
        public int CallCount => _callCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _callCount++;
            if (_callCount == 1)
            {
                var errorResponse = new HttpResponseMessage((HttpStatusCode)429)
                {
                    Content = new StringContent(
                        "{\"error\": {\"message\": \"Rate limit reached for model openai/gpt-oss-120b on tokens per minute (TPM): Limit 8000, Used 2711, Requested 5532. Please try again in 0.05s.\"}}",
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
                return Task.FromResult(errorResponse);
            }

            var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"choices\": [{\"message\": {\"role\": \"assistant\", \"content\": \"Success after 429 auto-retry!\"}}]}",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(successResponse);
        }
    }

    [Fact]
    public async Task OpenAiCompatibleApiClient_WhenReceives429_RetriesAutomaticallyAndSucceeds()
    {
        // Arrange
        var mockHandler = new Mock429ThenSuccessHandler();
        var originalClient = OpenAiCompatibleApiClient.HttpClient;
        OpenAiCompatibleApiClient.HttpClient = new HttpClient(mockHandler);

        try
        {
            var client = new OpenAiCompatibleApiClient();
            var settings = new AiSettingsModel
            {
                Provider = "Groq",
                GroqApiKey = "gsk-mock-key-12345",
                GroqModel = "openai/gpt-oss-120b"
            };

            var history = new List<(string role, string text)>
            {
                ("user", "Test prompt triggering retry")
            };

            // Act
            var response = await client.GenerateContentAsync(settings, "System prompt", history);

            // Assert
            Assert.NotNull(response);
            Assert.Contains("Success after 429 auto-retry", response);
            Assert.Equal(2, mockHandler.CallCount);
        }
        finally
        {
            OpenAiCompatibleApiClient.HttpClient = originalClient;
        }
    }

    [Fact]
    public void AiChatViewModel_ContainsVietnamese_DetectsDiacriticsAccurately()
    {
        Assert.True(AiChatViewModel.ContainsVietnamese("Cảm Biến Báo Động"));
        Assert.True(AiChatViewModel.ContainsVietnamese("Kích hoạt Đóng Cửa"));
        Assert.True(AiChatViewModel.ContainsVietnamese("Chu kỳ 1 Giờ"));
        Assert.True(AiChatViewModel.ContainsVietnamese("Đổi trạng thái Còi"));
        Assert.True(AiChatViewModel.ContainsVietnamese("Đèn"));

        Assert.False(AiChatViewModel.ContainsVietnamese("Alarm Sensor"));
        Assert.False(AiChatViewModel.ContainsVietnamese("Door Close Action"));
        Assert.False(AiChatViewModel.ContainsVietnamese("Rising Edge (50ms)"));
        Assert.False(AiChatViewModel.ContainsVietnamese("Input (DI0)"));
        Assert.False(AiChatViewModel.ContainsVietnamese(""));
        Assert.False(AiChatViewModel.ContainsVietnamese(null));
    }

    [Fact]
    public void AiChatViewModel_NormalizeToEnglishLabel_ConvertsVietnameseToStandardEnglish()
    {
        var inputNode = new DraftNode { NodeType = "INPUT", TagName = "DI2", Label = "Cảm Biến Báo Động" };
        Assert.Equal("Input (DI2)", AiChatViewModel.NormalizeToEnglishLabel(inputNode));

        var trgNode = new DraftNode { NodeType = "TRIGGER", TriggerType = "ON_RISE", DebounceMs = 50, Label = "Phát hiện kích hoạt" };
        Assert.Equal("Rising Edge (50ms)", AiChatViewModel.NormalizeToEnglishLabel(trgNode));

        var actNode = new DraftNode { NodeType = "ACTION", TagName = "DO2", ActionType = "SET_TAG", ActionParam = 1, Label = "Kích hoạt Đóng Cửa" };
        Assert.Equal("Turn ON (DO2)", AiChatViewModel.NormalizeToEnglishLabel(actNode));

        var toggleNode = new DraftNode { NodeType = "ACTION", TagName = "DO1", ActionType = "TOGGLE_TAG", Label = "Đổi trạng thái Còi" };
        Assert.Equal("Toggle (DO1)", AiChatViewModel.NormalizeToEnglishLabel(toggleNode));

        var intervalNode = new DraftNode { NodeType = "TRIGGER", TriggerType = "INTERVAL", ThresholdLo = 3600, Label = "Chu kỳ 1 Giờ" };
        Assert.Equal("Interval (3600s)", AiChatViewModel.NormalizeToEnglishLabel(intervalNode));
    }
}
