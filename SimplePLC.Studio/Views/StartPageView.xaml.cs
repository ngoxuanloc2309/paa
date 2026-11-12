using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

/// <summary>
/// Màn hình khởi động Dashboard toàn màn hình (In-App Fullscreen Welcome Dashboard)
/// theo chuẩn quốc tế Visual Studio / VS Code và nguyên lý ANSI/ISA-101.
/// </summary>
public partial class StartPageView : UserControl
{
    /// <summary>
    /// Khởi tạo StartPageView.
    /// </summary>
    public StartPageView()
    {
        InitializeComponent();
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is StartupHubViewModel vm)
        {
            vm.SearchText = string.Empty;
        }
    }

    private void OnRecentItemClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is RecentProjectItemViewModel item)
        {
            item.OpenCommand.Execute(null);
        }
    }

    private void OnCreateNewProjectClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is StartupHubViewModel vm)
        {
            vm.CreateNewProjectCommand.Execute(null);
        }
    }

    private void OnOpenLocalProjectClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is StartupHubViewModel vm)
        {
            vm.OpenLocalProjectCommand.Execute(null);
        }
    }

    private void OnContinueBlankClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is StartupHubViewModel vm)
        {
            vm.ContinueBlankCommand.Execute(null);
        }
    }
}
