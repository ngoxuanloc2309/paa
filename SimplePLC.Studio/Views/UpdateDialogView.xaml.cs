using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.Views;

public partial class UpdateDialogView : Window
{
    private readonly UpdateInfo _updateInfo;
    private readonly AppUpdateService _updateService;
    private CancellationTokenSource? _downloadCts;
    private bool _isDownloading;

    public UpdateDialogView(UpdateInfo updateInfo, AppUpdateService updateService)
    {
        InitializeComponent();
        _updateInfo = updateInfo;
        _updateService = updateService;

        TxtCurrentVersion.Text = $"v{updateInfo.CurrentVersion}";
        TxtLatestVersion.Text = $"v{updateInfo.LatestVersion}";

        RenderChangelog(string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
            ? (updateInfo.ReleaseTitle ?? "Bản cập nhật ổn định SynaptiX IDE.")
            : updateInfo.ReleaseNotes);

        if (!string.IsNullOrEmpty(updateInfo.AssetSizeFormatted))
        {
            TxtAssetSize.Text = updateInfo.AssetSizeFormatted;
        }
        else
        {
            TxtAssetSize.Text = "69.0 MB";
        }

        // Bản địa hóa
        bool isVi = LocalizationService.Instance.IsVietnamese;
        BtnCancel.Content = LocalizationService.Instance["UpdateDialogBtnLater"];
        BtnUpdateNow.Content = LocalizationService.Instance["UpdateDialogBtnUpdate"];
    }

    private void RenderChangelog(string rawText)
    {
        ChangelogContentPanel.Children.Clear();
        if (string.IsNullOrWhiteSpace(rawText))
        {
            var fallback = new TextBlock
            {
                Text = "Bản phát hành chính thức của SynaptiX IDE.",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                Margin = new Thickness(0, 4, 0, 4)
            };
            ChangelogContentPanel.Children.Add(fallback);
            return;
        }

        var lines = rawText.Replace("\r", "").Split('\n');
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line)) continue;

            if (line.StartsWith("##") || line.StartsWith("#"))
            {
                string headerText = line.TrimStart('#').Trim();
                var h = new TextBlock
                {
                    Text = headerText,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                    Margin = new Thickness(0, 6, 0, 4)
                };
                ChangelogContentPanel.Children.Add(h);
            }
            else if (line.StartsWith("-") || line.StartsWith("*"))
            {
                string itemText = line.TrimStart('-', '*').Trim();
                var itemBlock = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(6, 3, 0, 3),
                    LineHeight = 19
                };

                // Technical Bullet (Square symbol or solid dark bullet)
                itemBlock.Inlines.Add(new Run("▪ ")
                {
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69))
                });

                // Parse **bold** syntax
                var tokens = itemText.Split(new[] { "**" }, StringSplitOptions.None);
                for (int i = 0; i < tokens.Length; i++)
                {
                    string t = tokens[i].Replace("`", "");
                    if (string.IsNullOrEmpty(t)) continue;

                    if (i % 2 == 1)
                    {
                        // Inside **...** -> Bold
                        itemBlock.Inlines.Add(new Run(t)
                        {
                            FontWeight = FontWeights.SemiBold,
                            Foreground = new SolidColorBrush(Color.FromRgb(0x0F, 0x17, 0x2A))
                        });
                    }
                    else
                    {
                        // Outside **...** -> Regular
                        itemBlock.Inlines.Add(new Run(t)
                        {
                            Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55))
                        });
                    }
                }
                ChangelogContentPanel.Children.Add(itemBlock);
            }
            else
            {
                var desc = new TextBlock
                {
                    Text = line.Replace("`", ""),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)),
                    Margin = new Thickness(0, 2, 0, 4),
                    LineHeight = 18
                };
                ChangelogContentPanel.Children.Add(desc);
            }
        }
    }

    private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading) return;

        // Reset error banner
        ErrorAlertPanel.Visibility = Visibility.Collapsed;
        TxtErrorMessage.Text = string.Empty;

        if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
        {
            // Nếu không tìm thấy asset file tải trực tiếp, mở trang web GitHub Release
            OpenReleaseInBrowser();
            Close();
            return;
        }

        _isDownloading = true;
        BtnUpdateNow.IsEnabled = false;
        BtnCancel.IsEnabled = false;
        BtnUpdateNow.Content = LocalizationService.Instance.IsVietnamese ? "Đang tải..." : "Downloading...";
        ProgressPanel.Visibility = Visibility.Visible;

        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<double>(pct =>
        {
            Dispatcher.Invoke(() =>
            {
                PrgDownload.Value = pct;
                TxtProgressPercent.Text = $"{pct:F0}%";
                TxtProgressStatus.Text = LocalizationService.Instance.IsVietnamese
                    ? $"Đang tải gói cập nhật... ({pct:F0}%)"
                    : $"Downloading update... ({pct:F0}%)";
            });
        });

        try
        {
            string tempFile = await _updateService.DownloadUpdateAsync(_updateInfo.DownloadUrl, progress, _downloadCts.Token);

            TxtProgressStatus.Text = LocalizationService.Instance.IsVietnamese
                ? "Tải xong! Đang khởi động lại ứng dụng..."
                : "Download complete! Restarting application...";
            PrgDownload.Value = 100;
            TxtProgressPercent.Text = "100%";

            await Task.Delay(1000);

            // Hoán đổi và khởi động lại
            _updateService.ApplyUpdateAndRestart(tempFile);
        }
        catch (OperationCanceledException)
        {
            _isDownloading = false;
            BtnUpdateNow.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            BtnUpdateNow.Content = LocalizationService.Instance["UpdateDialogBtnUpdate"];
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            BtnUpdateNow.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            BtnUpdateNow.Content = LocalizationService.Instance["UpdateDialogBtnRetry"];
            ProgressPanel.Visibility = Visibility.Collapsed;

            ShowErrorAlert(ex);
        }
    }

    private void ShowErrorAlert(Exception ex)
    {
        ErrorAlertPanel.Visibility = Visibility.Visible;
        TxtErrorHeading.Text = LocalizationService.Instance["UpdateDialogErrHeading"];

        string message = ex.Message;
        if (message.Contains("404") || message.Contains("Not Found", StringComparison.OrdinalIgnoreCase))
        {
            TxtErrorMessage.Text = LocalizationService.Instance["UpdateDialogErr404"];
        }
        else
        {
            string prefix = LocalizationService.Instance.IsVietnamese ? "Không thể tải tệp cập nhật: " : "Failed to download update: ";
            TxtErrorMessage.Text = $"{prefix}{message}";
        }
    }

    private void BtnOpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        OpenReleaseInBrowser();
    }

    private void OpenReleaseInBrowser()
    {
        try
        {
            string tag = string.IsNullOrWhiteSpace(_updateInfo.LatestVersion) ? "" : $"/tag/v{_updateInfo.LatestVersion}";
            string releaseWebUrl = $"https://github.com/{AppUpdateService.GitHubRepoOwner}/{AppUpdateService.GitHubRepoName}/releases{tag}";
            Process.Start(new ProcessStartInfo(releaseWebUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở trình duyệt: {ex.Message}", "SynaptiX IDE", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_isDownloading)
        {
            _downloadCts?.Cancel();
        }
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _downloadCts?.Cancel();
        _downloadCts?.Dispose();
    }
}
