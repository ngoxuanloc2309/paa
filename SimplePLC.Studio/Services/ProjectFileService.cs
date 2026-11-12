using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Dịch vụ lưu, nạp và quản lý danh sách dự án mở gần đây (Recent Projects) cho SynaptiX IDE.
/// </summary>
public static class ProjectFileService
{
    public const string ProjectExtension = ".synx";
    public const string ProjectFilter = "SynaptiX Project (*.synx)|*.synx|JSON File (*.json)|*.json|All Files (*.*)|*.*";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Đường dẫn tùy biến cho danh sách dự án gần đây (dành riêng cho kiểm thử hermetic isolation).
    /// </summary>
    public static string? CustomRecentProjectsFilePath { get; set; }

    /// <summary>
    /// Đường dẫn tệp lưu trữ danh sách dự án gần đây (mặc định trong %APPDATA%/SynaptiX).
    /// </summary>
    public static string RecentProjectsFilePath =>
        !string.IsNullOrEmpty(CustomRecentProjectsFilePath)
            ? CustomRecentProjectsFilePath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SynaptiX",
                "recent_projects.json");

    public static void SaveProject(string filePath, ProjectModel project, bool allowEmptyOverwrite = false)
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

        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            bool isProjectEmpty = (project.Nodes == null || project.Nodes.Count == 0) &&
                                  (project.Rules == null || project.Rules.Count == 0);
            if (fileInfo.Length > 512 && isProjectEmpty && !allowEmptyOverwrite)
            {
                throw new InvalidOperationException(
                    "Safety Gate Violation: Cannot overwrite existing project containing data with empty nodes and rules without explicit confirmation.");
            }

            try
            {
                string backupPath = filePath + ".bak";
                File.Copy(filePath, backupPath, overwrite: true);
            }
            catch
            {
                // Best-effort backup
            }
        }

        string tempPath = filePath + ".tmp";
        string json = JsonSerializer.Serialize(project, JsonOptions);
        File.WriteAllText(tempPath, json, System.Text.Encoding.UTF8);

        File.Move(tempPath, filePath, overwrite: true);

        AddRecentProject(filePath, project.Metadata?.ProjectName, project.Metadata?.TargetDevice);
    }

    public static async Task SaveProjectAsync(string filePath, ProjectModel project, bool allowEmptyOverwrite = false)
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

        if (File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            bool isProjectEmpty = (project.Nodes == null || project.Nodes.Count == 0) &&
                                  (project.Rules == null || project.Rules.Count == 0);
            if (fileInfo.Length > 512 && isProjectEmpty && !allowEmptyOverwrite)
            {
                throw new InvalidOperationException(
                    "Safety Gate Violation: Cannot overwrite existing project containing data with empty nodes and rules without explicit confirmation.");
            }

            try
            {
                string backupPath = filePath + ".bak";
                File.Copy(filePath, backupPath, overwrite: true);
            }
            catch
            {
                // Best-effort backup
            }
        }

        string tempPath = filePath + ".tmp";
        string json = JsonSerializer.Serialize(project, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, System.Text.Encoding.UTF8);

        File.Move(tempPath, filePath, overwrite: true);

        AddRecentProject(filePath, project.Metadata?.ProjectName, project.Metadata?.TargetDevice);
    }

    public static ProjectModel LoadProject(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Project file not found: {filePath}", filePath);

        string json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        var project = JsonSerializer.Deserialize<ProjectModel>(json, JsonOptions);

        if (project == null)
            throw new InvalidDataException("Project file format is invalid or empty.");

        AddRecentProject(filePath, project.Metadata?.ProjectName, project.Metadata?.TargetDevice);
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

        AddRecentProject(filePath, project.Metadata?.ProjectName, project.Metadata?.TargetDevice);
        return project;
    }

    public static List<string> GetRecentProjects()
    {
        return GetRecentProjectEntries().Select(e => e.FilePath).ToList();
    }

    public static List<RecentProjectEntry> GetRecentProjectEntries()
    {
        try
        {
            if (!File.Exists(RecentProjectsFilePath))
                return new List<RecentProjectEntry>();

            string json = File.ReadAllText(RecentProjectsFilePath);
            List<RecentProjectEntry>? entries = null;

            try
            {
                entries = JsonSerializer.Deserialize<List<RecentProjectEntry>>(json, JsonOptions);
            }
            catch
            {
                // Fallback: migrate legacy format List<string>
                var stringList = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                if (stringList != null)
                {
                    entries = stringList.Select(p => new RecentProjectEntry
                    {
                        FilePath = p,
                        ProjectName = Path.GetFileNameWithoutExtension(p),
                        HardwareBadge = "[SYNX]",
                        LastOpened = DateTime.UtcNow
                    }).ToList();
                }
            }

            if (entries == null)
                return new List<RecentProjectEntry>();

            // Sanitize legacy badges: auto-upgrade "[PLC]" to standardized SYNX badge
            foreach (var e in entries)
            {
                if (e.HardwareBadge == "[PLC]" || string.IsNullOrWhiteSpace(e.HardwareBadge))
                {
                    e.HardwareBadge = DeriveHardwareBadge(e.HardwareTarget);
                }
            }

            // Filter existing files only, and exclude temp test directory files unless in custom test mode
            string tempDir = Path.GetTempPath();
            entries = entries
                .Where(e => File.Exists(e.FilePath) && (CustomRecentProjectsFilePath != null || !e.FilePath.StartsWith(tempDir, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            // Sort: pinned first (descending by LastOpened), then unpinned (descending by LastOpened)
            return entries
                .OrderByDescending(e => e.IsPinned)
                .ThenByDescending(e => e.LastOpened)
                .Take(20)
                .ToList();
        }
        catch
        {
            return new List<RecentProjectEntry>();
        }
    }

    public static void AddRecentProject(string filePath, string? projectName = null, string? hardwareTarget = null, string? hardwareBadge = null)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);

            // Do not record temporary test files in production recent projects
            if (CustomRecentProjectsFilePath == null && fullPath.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var entries = GetRecentProjectEntries();
            var existing = entries.FirstOrDefault(p => string.Equals(p.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

            bool isPinned = existing?.IsPinned ?? false;
            entries.RemoveAll(p => string.Equals(p.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

            var entry = new RecentProjectEntry
            {
                FilePath = fullPath,
                ProjectName = !string.IsNullOrWhiteSpace(projectName) 
                    ? projectName 
                    : (existing?.ProjectName ?? Path.GetFileNameWithoutExtension(fullPath)),
                HardwareTarget = !string.IsNullOrWhiteSpace(hardwareTarget)
                    ? hardwareTarget
                    : (existing?.HardwareTarget ?? string.Empty),
                HardwareBadge = !string.IsNullOrWhiteSpace(hardwareBadge) && hardwareBadge != "[PLC]"
                    ? hardwareBadge
                    : (!string.IsNullOrWhiteSpace(existing?.HardwareBadge) && existing.HardwareBadge != "[PLC]"
                        ? existing.HardwareBadge
                        : DeriveHardwareBadge(hardwareTarget ?? existing?.HardwareTarget)),
                LastOpened = DateTime.UtcNow,
                IsPinned = isPinned
            };

            entries.Insert(0, entry);

            // Re-sort: pinned first, then unpinned
            entries = entries
                .OrderByDescending(e => e.IsPinned)
                .ThenByDescending(e => e.LastOpened)
                .Take(20)
                .ToList();

            string dir = Path.GetDirectoryName(RecentProjectsFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(RecentProjectsFilePath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch
        {
            // Silent catch for recent projects logging
        }
    }

    public static void RemoveRecentProject(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var entries = GetRecentProjectEntries();
            entries.RemoveAll(p => string.Equals(p.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

            string dir = Path.GetDirectoryName(RecentProjectsFilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(RecentProjectsFilePath, JsonSerializer.Serialize(entries, JsonOptions));
        }
        catch
        {
            // Best-effort
        }
    }

    public static void TogglePinRecentProject(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var entries = GetRecentProjectEntries();
            var target = entries.FirstOrDefault(p => string.Equals(p.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                target.IsPinned = !target.IsPinned;

                entries = entries
                    .OrderByDescending(e => e.IsPinned)
                    .ThenByDescending(e => e.LastOpened)
                    .Take(20)
                    .ToList();

                string dir = Path.GetDirectoryName(RecentProjectsFilePath) ?? string.Empty;
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(RecentProjectsFilePath, JsonSerializer.Serialize(entries, JsonOptions));
            }
        }
        catch
        {
            // Best-effort
        }
    }

    public static string DeriveHardwareBadge(string? hardwareTarget)
    {
        if (string.IsNullOrWhiteSpace(hardwareTarget)) return "[SYNX-RIO-884]";

        if (hardwareTarget.Contains("884", StringComparison.OrdinalIgnoreCase)) return "[SYNX-RIO-884]";
        if (hardwareTarget.Contains("440-WF", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("WF", StringComparison.OrdinalIgnoreCase)) return "[SYNX-RIO-440-WF]";
        if (hardwareTarget.Contains("440-ZB", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("ZB", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("Zigbee", StringComparison.OrdinalIgnoreCase)) return "[SYNX-RIO-440-ZB]";
        if (hardwareTarget.Contains("440", StringComparison.OrdinalIgnoreCase)) return "[SYNX-RIO-440]";
        if (hardwareTarget.Contains("880", StringComparison.OrdinalIgnoreCase)) return "[SYNX-RIO-880]";
        if (hardwareTarget.Contains("424", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("DL", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("DAT", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("Logger", StringComparison.OrdinalIgnoreCase)) return "[SYNX-DL-424]";
        if (hardwareTarget.Contains("220", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("GW", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("GTW", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("Gateway", StringComparison.OrdinalIgnoreCase)) return "[SYNX-GW-220]";
        if (hardwareTarget.Contains("DTU", StringComparison.OrdinalIgnoreCase) || hardwareTarget.Contains("485", StringComparison.OrdinalIgnoreCase)) return "[SYNX-DTU-485]";
        if (hardwareTarget.Contains("Controller", StringComparison.OrdinalIgnoreCase)) return "[SYNX-RIO-884]";

        return "[SYNX]";
    }
}
