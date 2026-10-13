using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimplePLC.Studio.Models;

public class AiSettingsModel
{
    private static readonly string DefaultSettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SynaptiX",
        "ai_settings.json");

    public static string? CustomSettingsFilePath { get; set; }

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

    public string ApiKey { get; set; } = string.Empty;
    public string ModelName { get; set; } = "gemini-3.8-flash";
    public double Temperature { get; set; } = 0.2;

    [JsonIgnore]
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

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

                    if (string.IsNullOrWhiteSpace(settings.ModelName))
                    {
                        settings.ModelName = "gemini-3.8-flash";
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
