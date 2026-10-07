using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

public static class ProjectFileService
{
    public const string ProjectExtension = ".splc";
    public const string ProjectFilter = "SimplePLC Project (*.splc)|*.splc|JSON File (*.json)|*.json|All Files (*.*)|*.*";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string RecentProjectsFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SimplePLC",
        "recent_projects.json");

    public static void SaveProject(string filePath, ProjectModel project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        project.Metadata.LastModified = DateTime.UtcNow;

        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = filePath + ".tmp";
        string json = JsonSerializer.Serialize(project, JsonOptions);
        File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        File.Move(tempPath, filePath);

        AddRecentProject(filePath);
    }

    public static async Task SaveProjectAsync(string filePath, ProjectModel project)
    {
        ArgumentNullException.ThrowIfNull(project);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be empty.", nameof(filePath));

        project.Metadata.LastModified = DateTime.UtcNow;

        string dir = Path.GetDirectoryName(filePath) ?? string.Empty;
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = filePath + ".tmp";
        string json = JsonSerializer.Serialize(project, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, System.Text.Encoding.UTF8);

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        File.Move(tempPath, filePath);

        AddRecentProject(filePath);
    }

    public static ProjectModel LoadProject(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Project file not found: {filePath}", filePath);

        string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        var project = JsonSerializer.Deserialize<ProjectModel>(json, JsonOptions);

        if (project == null)
            throw new InvalidDataException("Project file format is invalid or empty.");

        AddRecentProject(filePath);
        return project;
    }

    public static async Task<ProjectModel> LoadProjectAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Project file not found: {filePath}", filePath);

        string json = await File.ReadAllTextAsync(filePath, System.Text.Encoding.UTF8);
        var project = JsonSerializer.Deserialize<ProjectModel>(json, JsonOptions);

        if (project == null)
            throw new InvalidDataException("Project file format is invalid or empty.");

        AddRecentProject(filePath);
        return project;
    }

    public static List<string> GetRecentProjects()
    {
        try
        {
            if (!File.Exists(RecentProjectsFilePath))
                return new List<string>();

            string json = File.ReadAllText(RecentProjectsFilePath);
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            return list.Where(File.Exists).Take(10).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    public static void AddRecentProject(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var list = GetRecentProjects();
            list.RemoveAll(p => string.Equals(p, fullPath, StringComparison.OrdinalIgnoreCase));
            list.Insert(0, fullPath);

            if (list.Count > 10) list = list.Take(10).ToList();

            string dir = Path.GetDirectoryName(RecentProjectsFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(RecentProjectsFilePath, JsonSerializer.Serialize(list, JsonOptions));
        }
        catch
        {
            // Silent catch for recent projects logging
        }
    }
}
