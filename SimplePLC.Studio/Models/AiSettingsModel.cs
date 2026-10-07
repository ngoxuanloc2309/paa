using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SimplePLC.Studio.Models;

public class AiSettingsModel
{
    private static readonly string SettingsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimplePLC",
        "ai_settings.json");

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
                    if (string.IsNullOrWhiteSpace(settings.ModelName) ||
                        settings.ModelName == "gemini-2.0-flash" ||
                        settings.ModelName == "gemini-1.5-flash" ||
                        settings.ModelName == "gemini-1.5-pro")
                    {
                        settings.ModelName = "gemini-3.8-flash";
                    }
                    return settings;
                }
            }
        }
        catch
        {
            // Ignore and fallback to default
        }

        return new AiSettingsModel();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save AI settings: {ex.Message}");
        }
    }
}
