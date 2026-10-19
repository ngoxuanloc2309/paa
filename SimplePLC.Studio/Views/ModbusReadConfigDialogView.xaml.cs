using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class ModbusReadConfigDialogView : Window
{
    public ModbusReadConfigDialogView(ModbusReadConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModbusReadConfigDialogViewModel vm)
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
