using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

/// <summary>
/// Interaction logic for ModbusPortConfigDialogView.xaml
/// </summary>
public partial class ModbusPortConfigDialogView : Window
{
    public ModbusPortConfigDialogView(ModbusPortConfigViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += () => DialogResult = true;
    }
}
