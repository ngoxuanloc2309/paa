using System;
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
        viewModel.RequestCloseDialog = (success) =>
        {
            try
            {
                DialogResult = success;
            }
            catch (InvalidOperationException)
            {
                Close();
            }
        };
        viewModel.RequestClose += () =>
        {
            try
            {
                if (DialogResult == null)
                {
                    DialogResult = true;
                }
            }
            catch (InvalidOperationException)
            {
                Close();
            }
        };
    }
}
