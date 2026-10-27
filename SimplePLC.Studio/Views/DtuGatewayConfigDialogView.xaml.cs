using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class DtuGatewayConfigDialogView : Window
{
    public DtuGatewayStudioViewModel ViewModel => (DtuGatewayStudioViewModel)DataContext;

    public DtuGatewayConfigDialogView(DtuGatewayStudioViewModel viewModel)
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
