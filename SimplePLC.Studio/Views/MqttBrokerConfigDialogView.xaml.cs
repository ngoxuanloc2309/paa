using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class MqttBrokerConfigDialogView : Window
{
    public MqttBrokerConfigDialogView(MqttBrokerConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += () =>
        {
            DialogResult = true;
            Close();
        };
    }
}
