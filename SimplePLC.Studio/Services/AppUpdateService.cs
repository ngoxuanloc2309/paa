using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace SimplePLC.Studio.Services;

public class UpdateInfo
{
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = AppUpdateService.GetCurrentAppVersion();
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseTitle { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string AssetFileName { get; set; } = string.Empty;
    public long AssetSizeBytes { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;

    public string AssetSizeFormatted
    {
        get
        {
            if (AssetSizeBytes <= 0) return string.Empty;
            double mb = AssetSizeBytes / (1024.0 * 1024.0);
            return $"{mb:F1} MB";
        }
    }
}

public class AppUpdateService
{
    private readonly HttpClient _httpClient;
    public const string GitHubRepoOwner = "hoanv-synaptix";
    public const string GitHubRepoName = "SynaptiX-IDE-Releases";
    public const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";

    public string CurrentVersion { get; }

    public AppUpdateService(HttpClient? httpClient = null, string? currentVersionOverride = null)
    {
        CurrentVersion = currentVersionOverride ?? GetCurrentAppVersion();

        _httpClient = httpClient ?? new HttpClient();
        if (httpClient == null)
        {
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "SynaptiX-IDE-AutoUpdater");
            }
            _httpClient.Timeout = TimeSpan.FromSeconds(15);
        }
    }

    public static string GetCurrentAppVersion()
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var ver = asm.GetName().Version;
        if (ver != null)
        {
            return $"{ver.Major}.{ver.Minor}.{Math.Max(0, ver.Build)}";
        }
        return "2.0.0";
    }

    public const string WebLatestReleaseUrl = $"https://github.com/{GitHubRepoOwner}/{GitHubRepoName}/releases/latest";

    /// <summary>
    /// Kiểm tra phiên bản phát hành mới nhất trên GitHub Releases.
    /// Tự động fallback qua Web 302 Redirect khi GitHub API chạm giới hạn 60 requests/giờ (HTTP 403 Rate Limit).
    /// </summary>
    public async Task<UpdateInfo> CheckForUpdateAsync(CancellationToken ct = default)
    {
        var info = new UpdateInfo
        {
            CurrentVersion = CurrentVersion
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, ReleasesApiUrl);
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                req.Headers.Add("User-Agent", "SynaptiX-IDE-AutoUpdater");
            }

            using var resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return ParseReleaseJson(json, CurrentVersion);
            }

            // Nếu gặp lỗi (403 Rate Limit, 404 hoặc mạng), tự động fallback qua Web 302 Redirect không giới hạn
            var fallback = await CheckForUpdateViaWebFallbackAsync(CurrentVersion, ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(fallback.ErrorMessage))
            {
                return fallback;
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                info.ErrorMessage = "Không tìm thấy bản phát hành nào (Mã lỗi: 404).\nKho mã nguồn trên GitHub đang ở chế độ Riêng tư (Private) hoặc chưa có bản Release nào được tạo.";
            }
            else
            {
                info.ErrorMessage = $"GitHub API trả về mã lỗi: {(int)resp.StatusCode} ({resp.ReasonPhrase})";
            }
            return info;
        }
        catch (Exception ex)
        {
            try
            {
                var fallback = await CheckForUpdateViaWebFallbackAsync(CurrentVersion, ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(fallback.ErrorMessage))
                {
                    return fallback;
                }
            }
            catch
            {
                // Ignore fallback exception to return original message
            }

            info.ErrorMessage = ex.Message;
            return info;
        }
    }

    /// <summary>
    /// Cơ chế dự phòng: Kiểm tra phiên bản qua Header Location của HTTP 302 Redirect từ GitHub Web.
    /// Hoàn toàn KHÔNG bị áp đặt giới hạn 60 req/giờ của GitHub REST API.
    /// </summary>
    public async Task<UpdateInfo> CheckForUpdateViaWebFallbackAsync(string currentVersion, CancellationToken ct = default)
    {
        var info = new UpdateInfo
        {
            CurrentVersion = currentVersion
        };

        try
        {
            using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Add("User-Agent", "SynaptiX-IDE-AutoUpdater");

            using var req = new HttpRequestMessage(HttpMethod.Get, WebLatestReleaseUrl);
            using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);

            string? redirectUrl = resp.Headers.Location?.ToString();
            if (!string.IsNullOrEmpty(redirectUrl))
            {
                int tagIdx = redirectUrl.LastIndexOf("/tag/", StringComparison.OrdinalIgnoreCase);
                if (tagIdx >= 0)
                {
                    string rawTag = redirectUrl.Substring(tagIdx + 5).Trim('/');
                    info.LatestVersion = CleanVersionString(rawTag);
                    info.ReleaseTitle = $"SynaptiX IDE {rawTag}";
                    info.ReleaseNotes = $"Phiên bản chính thức {rawTag} của SynaptiX IDE.\n- Chuẩn phần cứng: Hỗ trợ SynaptiX Industrial Controller (SPLC-AF-003).\n- Tự động cập nhật và hoán đổi tệp an toàn.";
                    info.DownloadUrl = $"https://github.com/{GitHubRepoOwner}/{GitHubRepoName}/releases/download/{rawTag}/SimplePLC.Studio.exe";
                    info.AssetFileName = "SimplePLC.Studio.exe";
                    info.HasUpdate = IsNewerVersion(info.LatestVersion, currentVersion);
                    return info;
                }
            }

            info.ErrorMessage = "Không thể lấy thông tin phiên bản từ Web Redirect.";
            return info;
        }
        catch (Exception ex)
        {
            info.ErrorMessage = $"Lỗi kiểm tra cập nhật qua Web: {ex.Message}";
            return info;
        }
    }

    /// <summary>
    /// Xử lý chuỗi JSON từ GitHub Releases API và xác định xem có bản mới hơn không.
    /// </summary>
    public static UpdateInfo ParseReleaseJson(string json, string currentVersion)
    {
        var info = new UpdateInfo
        {
            CurrentVersion = currentVersion
        };

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? string.Empty : string.Empty;
            string releaseTitle = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : tagName;
            string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? string.Empty : string.Empty;
            DateTime? publishedAt = root.TryGetProperty("published_at", out var pubProp) && pubProp.TryGetDateTime(out var dt) ? dt : null;

            info.LatestVersion = CleanVersionString(tagName);
            info.ReleaseTitle = releaseTitle;
            info.ReleaseNotes = body;
            info.PublishedAt = publishedAt;

            // Tìm asset tệp nhị phân .exe (hoặc .zip)
            if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsProp.EnumerateArray())
                {
                    string assetName = asset.TryGetProperty("name", out var anProp) ? anProp.GetString() ?? string.Empty : string.Empty;
                    string downloadUrl = asset.TryGetProperty("browser_download_url", out var dlProp) ? dlProp.GetString() ?? string.Empty : string.Empty;
                    long size = asset.TryGetProperty("size", out var sProp) && sProp.TryGetInt64(out var sz) ? sz : 0;

                    if (assetName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        info.AssetFileName = assetName;
                        info.DownloadUrl = downloadUrl;
                        info.AssetSizeBytes = size;
                        break;
                    }
                    else if (string.IsNullOrEmpty(info.DownloadUrl) && assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        info.AssetFileName = assetName;
                        info.DownloadUrl = downloadUrl;
                        info.AssetSizeBytes = size;
                    }
                }
            }

            info.HasUpdate = IsNewerVersion(info.LatestVersion, currentVersion);
        }
        catch (Exception ex)
        {
            info.ErrorMessage = $"Lỗi phân tích cú pháp dữ liệu release: {ex.Message}";
        }

        return info;
    }

    public static string CleanVersionString(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "0.0.0";
        string clean = raw.Trim().TrimStart('v', 'V');
        int dashIdx = clean.IndexOf('-');
        if (dashIdx > 0)
        {
            clean = clean.Substring(0, dashIdx);
        }
        return clean;
    }

    /// <summary>
    /// So sánh xem phiên bản remote có lớn hơn phiên bản hiện tại hay không theo Semantic Versioning.
    /// </summary>
    public static bool IsNewerVersion(string latestVerStr, string currentVerStr)
    {
        string cleanLatest = CleanVersionString(latestVerStr);
        string cleanCurrent = CleanVersionString(currentVerStr);

        if (Version.TryParse(NormalizeVersion(cleanLatest), out var vLatest) &&
            Version.TryParse(NormalizeVersion(cleanCurrent), out var vCurrent))
        {
            return vLatest > vCurrent;
        }

        return string.Compare(cleanLatest, cleanCurrent, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static string NormalizeVersion(string ver)
    {
        var parts = ver.Split('.');
        if (parts.Length == 1) return $"{parts[0]}.0.0";
        if (parts.Length == 2) return $"{parts[0]}.{parts[1]}.0";
        return ver;
    }

    /// <summary>
    /// Tải tệp cập nhật về thư mục tạm với tiến trình phần trăm.
    /// </summary>
    public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double> progress, CancellationToken ct = default)
    {
        string tempDir = Path.GetTempPath();
        string tempFile = Path.Combine(tempDir, $"SynaptiX_Update_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.exe");

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
            totalRead += bytesRead;

            if (totalBytes.HasValue && totalBytes.Value > 0)
            {
                double pct = (double)totalRead / totalBytes.Value * 100.0;
                progress.Report(pct);
            }
        }

        return tempFile;
    }

    /// <summary>
    /// Áp dụng bản cập nhật: Khởi chạy script PowerShell ngầm để hoán đổi tệp .exe an toàn và khởi động lại SynaptiX IDE.
    /// </summary>
    public void ApplyUpdateAndRestart(string downloadedTempFile, string? targetExePath = null)
    {
        targetExePath ??= Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(targetExePath) || !File.Exists(targetExePath))
        {
            throw new InvalidOperationException("Không thể xác định đường dẫn tệp thực thi của ứng dụng hiện tại.");
        }

        int currentPid = Process.GetCurrentProcess().Id;
        string escapedTemp = downloadedTempFile.Replace("'", "''");
        string escapedTarget = targetExePath.Replace("'", "''");

        // PowerShell script: Chờ tiến trình app hiện tại thoát hẳn (tối đa 10s), vòng lặp retry ghi đè file .exe (tối đa 30 lần x 500ms = 15s)
        string script = $"$p = Get-Process -Id {currentPid} -ErrorAction SilentlyContinue; if ($p) {{ [void]$p.WaitForExit(10000); }}; Start-Sleep -Milliseconds 500; $copied = $false; for ($i = 0; $i -lt 30; $i++) {{ try {{ Copy-Item -LiteralPath '{escapedTemp}' -Destination '{escapedTarget}' -Force -ErrorAction Stop; $copied = $true; break; }} catch {{ Start-Sleep -Milliseconds 500; }} }}; if ($copied) {{ Remove-Item -LiteralPath '{escapedTemp}' -Force -ErrorAction SilentlyContinue; Start-Process -FilePath '{escapedTarget}'; }}";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -WindowStyle Hidden -Command \"{script}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process.Start(psi);

        // Tắt app hiện tại để giải phóng file lock
        try
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                System.Windows.Application.Current.Shutdown();
            });
        }
        catch
        {
            // Ignore dispatch errors during app teardown
        }

        Environment.Exit(0);
    }
}
