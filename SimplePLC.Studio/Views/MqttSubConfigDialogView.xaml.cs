using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class MqttSubConfigDialogView : Window
{
    public MqttSubConfigDialogView(MqttSubConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
