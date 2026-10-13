using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class RtcSyncDialogView : Window
{
    public RtcSyncDialogView(RtcSyncViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose = (success) =>
        {
            DialogResult = success;
            Close();
        };

        Loaded += async (s, e) =>
        {
            await viewModel.InitializeAsync();
        };
    }
}
