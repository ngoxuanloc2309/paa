using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class NewProjectDialogView : Window
{
    public NewProjectViewModel ViewModel { get; }

    public NewProjectDialogView(NewProjectViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.Validate(out string error))
        {
            MessageBox.Show(this, error, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
