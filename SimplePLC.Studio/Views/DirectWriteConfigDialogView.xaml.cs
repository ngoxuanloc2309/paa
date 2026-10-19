using System;
using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class DirectWriteConfigDialogView : Window
{
    public DirectWriteConfigDialogView()
    {
        InitializeComponent();
    }

    public DirectWriteConfigDialogView(DirectWriteConfigDialogViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.RequestClose += success =>
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
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is DirectWriteConfigDialogViewModel vm)
        {
            vm.Apply();
        }

        try
        {
            DialogResult = true;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            DialogResult = false;
        }
        catch (InvalidOperationException)
        {
            Close();
        }
    }
}
