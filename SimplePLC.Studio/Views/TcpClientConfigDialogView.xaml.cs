using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

/// <summary>
/// Interaction logic for TcpClientConfigDialogView.xaml
/// </summary>
public partial class TcpClientConfigDialogView : Window
{
    public TcpClientConfigDialogView(TcpClientConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is TcpClientConfigDialogViewModel vm)
        {
            vm.Apply();
            DialogResult = true;
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
