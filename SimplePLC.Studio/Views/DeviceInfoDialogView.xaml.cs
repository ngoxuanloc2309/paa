using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class DeviceInfoDialogView : Window
{
    public DeviceInfoDialogView(DeviceInfoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
