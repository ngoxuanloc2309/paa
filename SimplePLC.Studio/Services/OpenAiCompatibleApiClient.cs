using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services.Ai;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Client kết nối tới các dịch vụ AI tương thích chuẩn OpenAI Chat Completions API v1 (đặc biệt là Groq Cloud LPU).
/// Hỗ trợ chuẩn hóa tin nhắn, truyền tải Function Calling và phân giải phản hồi kỹ thuật thời gian thực.
/// </summary>
public class OpenAiCompatibleApiClient
{
    private static HttpClient? _customHttpClient;
    private static readonly HttpClient DefaultHttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public static HttpClient HttpClient
    {
        get => _customHttpClient ?? DefaultHttpClient;
        set => _customHttpClient = value;
    }

    /// <summary>
    /// Điểm cuối mặc định cho Groq Cloud API.
    /// </summary>
    public const string DefaultGroqEndpoint = "https://api.groq.com/openai/v1/chat/completions";

    /// <summary>
    /// Gửi yêu cầu sinh nội dung văn bản thông thường không kèm công cụ.
    /// </summary>
    public async Task<string> GenerateContentAsync(
        AiSettingsModel settings,
        string systemInstruction,
        IEnumerable<(string role, string text)> conversationHistory,
        CancellationToken ct = default)
    {
        var result = await GenerateContentWithToolsAsync(settings, systemInstruction, conversationHistory, null, ct).ConfigureAwait(false);
        return result.Explanation;
    }

    /// <summary>
    /// Gửi yêu cầu sinh nội dung có kèm định nghĩa công cụ FBD (OpenAI Tools Schema).
    /// </summary>
    public async Task<GeminiToolResponse> GenerateContentWithToolsAsync(
        AiSettingsModel settings,
        string systemInstruction,
        IEnumerable<(string role, string text)> conversationHistory,
        JsonArray? tools = null,
        CancellationToken ct = default)
    {
        string apiKey = settings.EffectiveGroqApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình Groq API Key. Vui lòng vào Cài đặt (icon ⚙️) để nhập Groq API Key miễn phí từ console.groq.com.");
        }

        string model = string.IsNullOrWhiteSpace(settings.GroqModel) ? "openai/gpt-oss-120b" : settings.GroqModel.Trim();
        string url = DefaultGroqEndpoint;

        // Xây dựng Messages Array theo chuẩn OpenAI
        var messagesArray = new JsonArray();

        // System Instruction
        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            messagesArray.Add(new JsonObject
            {
                ["role"] = "system",
                ["content"] = systemInstruction
            });
        }

        // Conversation History
        foreach (var (role, text) in conversationHistory)
        {
            string openAiRole = role.Equals("model", StringComparison.OrdinalIgnoreCase) ||
                                role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
                ? "assistant"
                : "user";

            messagesArray.Add(new JsonObject
            {
                ["role"] = openAiRole,
                ["content"] = text
            });
        }

        // Build Root Payload
        var rootNode = new JsonObject
        {
            ["model"] = model,
            ["messages"] = messagesArray,
            ["temperature"] = settings.Temperature
        };

        // Tools declaration if provided
        if (tools != null && tools.Count > 0)
        {
            rootNode["tools"] = tools;
            rootNode["tool_choice"] = "auto";
        }

        string jsonPayload = rootNode.ToJsonString();
        HttpResponseMessage? response = null;
        string responseBody = string.Empty;
        const int maxRetries = 1;

        for (int retry = 0; retry <= maxRetries; retry++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
            request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                response = await HttpClient.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Lỗi kết nối mạng: Không thể kết nối tới Groq Cloud. Kiểm tra lại kết nối Internet ({ex.Message})", ex);
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException("Yêu cầu đã bị hủy bởi người dùng.", ct);
            }
            catch (TaskCanceledException ex)
            {
                throw new TimeoutException("Hết thời gian chờ phản hồi từ Groq Cloud (Timeout 60s).", ex);
            }

            responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                break;
            }

            // Xử lý Rate Limit 429: tự động thử lại 1 lần nếu còn lượt retry
            if ((int)response.StatusCode == 429 && retry < maxRetries)
            {
                string? errorMsg = null;
                try
                {
                    var errorDoc = JsonNode.Parse(responseBody);
                    errorMsg = errorDoc?["error"]?["message"]?.GetValue<string>();
                }
                catch
                {
                    /* Best-effort parse of error JSON payload; fallback to raw body if unparsable */
                }

                double waitSeconds = 2.0;
                if (!string.IsNullOrWhiteSpace(errorMsg))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(
                        errorMsg,
                        @"try again in ([0-9]+(\.[0-9]+)?)s",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedSec))
                    {
                        waitSeconds = parsedSec;
                    }
                }

                int delayMs = Math.Clamp((int)(waitSeconds * 1000) + 300, 1500, 5000);
                await Task.Delay(delayMs, ct).ConfigureAwait(false);
                continue;
            }

            // Nếu không phải 429 hoặc đã hết lượt retry -> ném lỗi
            string? failMsg = null;
            try
            {
                var errorDoc = JsonNode.Parse(responseBody);
                failMsg = errorDoc?["error"]?["message"]?.GetValue<string>();
            }
            catch
            {
                /* Best-effort parse of failure JSON payload; fallback to raw reason phrase if unparsable */
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                throw new Exception($"Groq API Key không hợp lệ hoặc đã hết hạn: {failMsg ?? response.ReasonPhrase}");
            }

            if ((int)response.StatusCode == 429)
            {
                throw new Exception($"Hạn mức yêu cầu Groq tạm thời đã vượt quá (Rate Limit Exceeded): {failMsg ?? response.ReasonPhrase}. Vui lòng đợi vài giây và thử lại.");
            }

            if ((int)response.StatusCode == 413)
            {
                throw new Exception($"Dung lượng yêu cầu vượt quá giới hạn token của Groq (Request Too Large): {failMsg ?? response.ReasonPhrase}. Hãy thử lại với câu hỏi ngắn gọn hơn.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"Mô hình '{model}' không khả dụng trên tài khoản Groq của bạn. Hãy đổi sang 'llama-3.1-8b-instant' hoặc 'openai/gpt-oss-120b' trong Cài đặt (icon ⚙️). ({failMsg ?? response.ReasonPhrase})");
            }

            throw new Exception($"Lỗi từ Groq Cloud [{(int)response.StatusCode}]: {failMsg ?? response.ReasonPhrase ?? responseBody}");
        }

        // Phân giải kết quả trả về từ Groq
        try
        {
            var resDoc = JsonNode.Parse(responseBody);
            var choices = resDoc?["choices"]?.AsArray();
            var toolCalls = new List<AiToolCall>();
            string explanation = string.Empty;

            if (choices != null && choices.Count > 0)
            {
                var firstChoice = choices[0];
                var message = firstChoice?["message"]?.AsObject();
                if (message != null)
                {
                    explanation = message["content"]?.GetValue<string?>() ??
                                  message["reasoning"]?.GetValue<string?>() ??
                                  string.Empty;

                    // Parse OpenAI tool_calls
                    var rawToolCalls = message["tool_calls"]?.AsArray();
                    if (rawToolCalls != null)
                    {
                        foreach (var tc in rawToolCalls)
                        {
                            var fn = tc?["function"]?.AsObject();
                            if (fn != null)
                            {
                                string? fnName = fn["name"]?.GetValue<string>();
                                string? argsJsonStr = fn["arguments"]?.GetValue<string>();

                                JsonObject argsObj;
                                try
                                {
                                    argsObj = !string.IsNullOrWhiteSpace(argsJsonStr)
                                        ? (JsonNode.Parse(argsJsonStr)?.AsObject() ?? new JsonObject())
                                        : new JsonObject();
                                }
                                catch
                                {
                                    argsObj = new JsonObject();
                                }

                                if (!string.IsNullOrEmpty(fnName))
                                {
                                    toolCalls.Add(new AiToolCall(fnName, argsObj));
                                }
                            }
                        }
                    }
                }
            }

            return new GeminiToolResponse(
                !string.IsNullOrWhiteSpace(explanation) ? explanation : "Đã xử lý cấu hình theo yêu cầu.",
                toolCalls);
        }
        catch (Exception ex)
        {
            throw new Exception($"Không thể đọc kết quả trả về từ Groq Cloud: {ex.Message}", ex);
        }
    }
}
