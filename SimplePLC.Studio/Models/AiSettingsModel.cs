using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Mẫu cấu hình lưu trữ tham số kết nối dịch vụ AI (Google Gemini & Groq Cloud).
/// Hỗ trợ nạp, lưu tự động và phân tách API Key/Model giữa các nhà cung cấp.
/// </summary>
public class AiSettingsModel
{
    private static readonly string DefaultSettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SynaptiX",
        "ai_settings.json");

    /// <summary>
    /// Đường dẫn file cấu hình tùy biến (dùng cho môi trường Test hoặc Custom Profile).
    /// </summary>
    public static string? CustomSettingsFilePath { get; set; }

    /// <summary>
    /// Đường dẫn file cấu hình thực tế được sử dụng.
    /// </summary>
    public static string SettingsFilePath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(CustomSettingsFilePath))
                return CustomSettingsFilePath;

            // Đảm bảo test runner không ghi đè / phá hỏng file cấu hình thực tế của người dùng
            if (AppDomain.CurrentDomain.FriendlyName.Contains("testhost", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(Path.GetTempPath(), "synaptix_test_ai_settings.json");
            }

            return DefaultSettingsFilePath;
        }
    }

    /// <summary>
    /// Nhà cung cấp AI đang hoạt động ("Gemini" hoặc "Groq"). Mặc định: "Gemini".
    /// </summary>
    public string Provider { get; set; } = "Gemini";

    /// <summary>
    /// Khóa API Key kế thừa (dùng cho tính tương thích ngược với bản cũ).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Khóa API Key của Google Gemini.
    /// </summary>
    public string GeminiApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Khóa API Key của Groq Cloud.
    /// </summary>
    public string GroqApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Tên mô hình của Google Gemini (Mặc định: "gemini-3.8-flash").
    /// </summary>
    public string ModelName { get; set; } = "gemini-3.8-flash";

    /// <summary>
    /// Tên mô hình của Groq Cloud (Mặc định: "openai/gpt-oss-120b").
    /// </summary>
    public string GroqModel { get; set; } = "openai/gpt-oss-120b";

    /// <summary>
    /// Ngôn ngữ phản hồi của AI ("Auto", "English", "Vietnamese", "FollowApp"). Mặc định: "Auto".
    /// </summary>
    public string ResponseLanguage { get; set; } = "Auto";

    /// <summary>
    /// Nhiệt độ lấy mẫu sáng tạo (0.0 đến 1.0, mặc định 0.2 cho tư duy kỹ thuật chính xác).
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Trả về API Key của Google Gemini (ưu tiên GeminiApiKey, fallback sang ApiKey cũ).
    /// </summary>
    [JsonIgnore]
    public string EffectiveGeminiApiKey => !string.IsNullOrWhiteSpace(GeminiApiKey) ? GeminiApiKey : ApiKey;

    /// <summary>
    /// Trả về API Key của Groq Cloud.
    /// </summary>
    [JsonIgnore]
    public string EffectiveGroqApiKey => GroqApiKey;

    /// <summary>
    /// Trả về API Key tương ứng với Provider hiện tại đang được kích hoạt.
    /// </summary>
    [JsonIgnore]
    public string CurrentApiKey => Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)
        ? EffectiveGroqApiKey
        : EffectiveGeminiApiKey;

    /// <summary>
    /// Trả về Model tương ứng với Provider hiện tại đang được kích hoạt.
    /// </summary>
    [JsonIgnore]
    public string CurrentModel => Provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)
        ? (string.IsNullOrWhiteSpace(GroqModel) ? "openai/gpt-oss-120b" : GroqModel)
        : (string.IsNullOrWhiteSpace(ModelName) ? "gemini-3.8-flash" : ModelName);

    /// <summary>
    /// Kiểm tra cấu hình đã hợp lệ (có API Key cho provider hiện tại) hay chưa.
    /// </summary>
    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CurrentApiKey);

    /// <summary>
    /// Nạp cấu hình từ đĩa. Tự động di chuyển dữ liệu tương thích ngược nếu cần.
    /// </summary>
    /// <returns>Đối tượng AiSettingsModel hợp lệ.</returns>
    public static AiSettingsModel Load()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = File.ReadAllText(SettingsFilePath);
                var settings = JsonSerializer.Deserialize<AiSettingsModel>(json);
                if (settings != null)
                {
                    // Lọc bỏ giá trị test mẫu nếu từng bị nhiễm vào file cấu hình
                    if (settings.ApiKey == "valid-api-key")
                    {
                        settings.ApiKey = string.Empty;
                    }
                    if (settings.GeminiApiKey == "valid-api-key")
                    {
                        settings.GeminiApiKey = string.Empty;
                    }
                    if (settings.GroqApiKey == "valid-api-key")
                    {
                        settings.GroqApiKey = string.Empty;
                    }

                    // Đồng bộ tương thích ngược giữa ApiKey và GeminiApiKey
                    if (string.IsNullOrWhiteSpace(settings.GeminiApiKey) && !string.IsNullOrWhiteSpace(settings.ApiKey))
                    {
                        settings.GeminiApiKey = settings.ApiKey;
                    }
                    else if (!string.IsNullOrWhiteSpace(settings.GeminiApiKey) && string.IsNullOrWhiteSpace(settings.ApiKey))
                    {
                        settings.ApiKey = settings.GeminiApiKey;
                    }

                    if (string.IsNullOrWhiteSpace(settings.ModelName))
                    {
                        settings.ModelName = "gemini-3.8-flash";
                    }

                    if (string.IsNullOrWhiteSpace(settings.GroqModel) ||
                        settings.GroqModel == "llama-3.3-70b-versatile" ||
                        settings.GroqModel == "llama-3.1-8b-instant")
                    {
                        settings.GroqModel = "openai/gpt-oss-120b";
                    }

                    if (string.IsNullOrWhiteSpace(settings.Provider))
                    {
                        settings.Provider = "Gemini";
                    }

                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load AI settings: {ex.Message}");
        }

        return new AiSettingsModel();
    }

    /// <summary>
    /// Lưu cấu hình hiện tại xuống đĩa dưới định dạng JSON.
    /// </summary>
    public void Save()
    {
        try
        {
            var path = SettingsFilePath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save AI settings: {ex.Message}");
        }
    }
}
