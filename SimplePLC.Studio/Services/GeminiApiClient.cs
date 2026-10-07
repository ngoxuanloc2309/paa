using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services.Ai;

namespace SimplePLC.Studio.Services;

public record GeminiToolResponse(string Explanation, List<AiToolCall> ToolCalls);

public class GeminiApiClient
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    public async Task<string> GenerateContentAsync(
        AiSettingsModel settings,
        string systemInstruction,
        IEnumerable<(string role, string text)> conversationHistory,
        CancellationToken ct = default)
    {
        var result = await GenerateContentWithToolsAsync(settings, systemInstruction, conversationHistory, null, ct).ConfigureAwait(false);
        return result.Explanation;
    }

    public async Task<GeminiToolResponse> GenerateContentWithToolsAsync(
        AiSettingsModel settings,
        string systemInstruction,
        IEnumerable<(string role, string text)> conversationHistory,
        JsonArray? tools = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            throw new InvalidOperationException("Chưa cấu hình API Key. Vui lòng vào Cài đặt (icon ⚙️) để nhập Gemini API Key miễn phí.");
        }

        string model = string.IsNullOrWhiteSpace(settings.ModelName) ? "gemini-2.0-flash" : settings.ModelName.Trim();
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={settings.ApiKey.Trim()}";

        // Build Payload
        var rootNode = new JsonObject();

        // System Instruction
        if (!string.IsNullOrWhiteSpace(systemInstruction))
        {
            var sysParts = new JsonArray { new JsonObject { ["text"] = systemInstruction } };
            rootNode["system_instruction"] = new JsonObject { ["parts"] = sysParts };
        }

        // Tools declaration if provided
        if (tools != null && tools.Count > 0)
        {
            rootNode["tools"] = new JsonArray
            {
                new JsonObject
                {
                    ["function_declarations"] = tools
                }
            };
        }

        // Contents
        var contentsArray = new JsonArray();
        foreach (var (role, text) in conversationHistory)
        {
            string geminiRole = role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ||
                                role.Equals("model", StringComparison.OrdinalIgnoreCase)
                ? "model"
                : "user";

            var parts = new JsonArray { new JsonObject { ["text"] = text } };
            contentsArray.Add(new JsonObject
            {
                ["role"] = geminiRole,
                ["parts"] = parts
            });
        }
        rootNode["contents"] = contentsArray;

        // Generation Config
        rootNode["generationConfig"] = new JsonObject
        {
            ["temperature"] = settings.Temperature,
            ["maxOutputTokens"] = 4096
        };

        string jsonPayload = rootNode.ToJsonString();
        using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await HttpClient.PostAsync(url, content, ct).ConfigureAwait(false);
            if ((int)response.StatusCode == 503)
            {
                await Task.Delay(1500, ct).ConfigureAwait(false);
                using var retryContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                response = await HttpClient.PostAsync(url, retryContent, ct).ConfigureAwait(false);
            }
        }
        catch (HttpRequestException ex)
        {
            throw new Exception($"Lỗi kết nối mạng: Không thể kết nối tới Google Gemini. Kiểm tra lại kết nối Internet ({ex.Message})", ex);
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException("Yêu cầu đã bị hủy bởi người dùng.", ct);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException("Hết thời gian chờ phản hồi từ Google Gemini (Timeout 30s).", ex);
        }

        string responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            string? errorMsg = null;
            try
            {
                var errorDoc = JsonNode.Parse(responseBody);
                errorMsg = errorDoc?["error"]?["message"]?.GetValue<string>();
            }
            catch
            {
                // ignore parsing failure
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
                response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                throw new Exception($"Google API Key không hợp lệ hoặc cấu hình chưa đúng: {errorMsg ?? response.ReasonPhrase}");
            }

            if ((int)response.StatusCode == 429)
            {
                throw new Exception("Hạn mức yêu cầu tạm thời đã vượt quá (Quota Exceeded). Vui lòng đợi vài giây và thử lại.");
            }

            if ((int)response.StatusCode == 503)
            {
                throw new Exception("Máy chủ Google Gemini đang trong thời điểm quá tải tạm thời (High demand spike). Vui lòng thử lại sau.");
            }

            throw new Exception($"Lỗi từ Google Gemini [{(int)response.StatusCode}]: {errorMsg ?? response.ReasonPhrase ?? responseBody}");
        }

        // Parse response
        try
        {
            var resDoc = JsonNode.Parse(responseBody);
            var candidates = resDoc?["candidates"]?.AsArray();
            var toolCalls = new List<AiToolCall>();
            var explanationSb = new StringBuilder();

            if (candidates != null && candidates.Count > 0)
            {
                var firstCandidate = candidates[0];
                var parts = firstCandidate?["content"]?["parts"]?.AsArray();
                if (parts != null)
                {
                    foreach (var part in parts)
                    {
                        var text = part?["text"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(text))
                        {
                            explanationSb.Append(text);
                        }

                        // Check for functionCall
                        var fnCall = part?["functionCall"]?.AsObject();
                        if (fnCall != null)
                        {
                            string? fnName = fnCall["name"]?.GetValue<string>();
                            var args = fnCall["args"]?.AsObject() ?? new JsonObject();
                            if (!string.IsNullOrEmpty(fnName))
                            {
                                toolCalls.Add(new AiToolCall(fnName, args));
                            }
                        }
                    }
                }
            }

            return new GeminiToolResponse(
                explanationSb.Length > 0 ? explanationSb.ToString() : "Đã xử lý cấu hình theo yêu cầu.",
                toolCalls);
        }
        catch (Exception ex)
        {
            throw new Exception($"Không thể đọc kết quả trả về từ Gemini: {ex.Message}", ex);
        }
    }
}
