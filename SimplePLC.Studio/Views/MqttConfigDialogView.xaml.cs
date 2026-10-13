using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class MqttConfigDialogView : Window
{
    public MqttConfigDialogView(MqttConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
