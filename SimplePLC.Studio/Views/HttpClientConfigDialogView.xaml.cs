using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

/// <summary>
/// Interaction logic for HttpClientConfigDialogView.xaml
/// </summary>
public partial class HttpClientConfigDialogView : Window
{
    /// <summary>
    /// Khởi tạo hộp thoại cấu hình HTTP Client với ViewModel tương ứng.
    /// </summary>
    /// <param name="viewModel">ViewModel quản lý trạng thái hộp thoại.</param>
    public HttpClientConfigDialogView(HttpClientConfigDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is HttpClientConfigDialogViewModel vm)
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
