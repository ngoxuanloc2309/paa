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

        if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
        {
            // Nếu không tìm thấy asset file tải trực tiếp, mở trang web GitHub Release
            try
            {
                string releaseWebUrl = $"https://github.com/{AppUpdateService.GitHubRepoOwner}/{AppUpdateService.GitHubRepoName}/releases/tag/v{_updateInfo.LatestVersion}";
                Process.Start(new ProcessStartInfo(releaseWebUrl) { UseShellExecute = true });
                Close();
            }
            catch (Exception ex)
            {
                TxtStatusMessage.Text = $"Lỗi: {ex.Message}";
            }
            return;
        }

        _isDownloading = true;
        BtnUpdateNow.IsEnabled = false;
        BtnCancel.IsEnabled = false;
        BtnUpdateNow.Content = LocalizationService.Instance.IsVietnamese ? "Đang cập nhật..." : "Updating...";
        ProgressPanel.Visibility = Visibility.Visible;
        TxtStatusMessage.Text = string.Empty;

        _downloadCts = new CancellationTokenSource();
        var progress = new Progress<double>(pct =>
        {
            Dispatcher.Invoke(() =>
            {
                PrgDownload.Value = pct;
                TxtProgressPercent.Text = $"{pct:F0}%";
                TxtProgressStatus.Text = LocalizationService.Instance.IsVietnamese
                    ? $"Đang tải bản cập nhật... ({pct:F0}%)"
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
            TxtStatusMessage.Text = LocalizationService.Instance.IsVietnamese ? "Đã hủy tải bản cập nhật." : "Download canceled.";
        }
        catch (Exception ex)
        {
            _isDownloading = false;
            BtnUpdateNow.IsEnabled = true;
            BtnCancel.IsEnabled = true;
            BtnUpdateNow.Content = LocalizationService.Instance["UpdateDialogBtnUpdate"];
            ProgressPanel.Visibility = Visibility.Collapsed;
            TxtStatusMessage.Text = $"Lỗi tải tệp: {ex.Message}";
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
