using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class ModbusWriteConfigDialogView : Window
{
    public ModbusWriteConfigDialogView(ModbusWriteConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModbusWriteConfigDialogViewModel vm)
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
