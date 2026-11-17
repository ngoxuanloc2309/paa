using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

/// <summary>
/// Interaction logic for MathConfigDialogView.xaml
/// </summary>
public partial class MathConfigDialogView : Window
{
    /// <summary>
    /// Khởi tạo hộp thoại cấu hình khối tính toán số học Math ALU.
    /// </summary>
    /// <param name="viewModel">ViewModel quản lý trạng thái hộp thoại.</param>
    public MathConfigDialogView(MathConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Closed += (_, _) => viewModel.Cleanup();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MathConfigDialogViewModel vm)
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
