using System;
using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

/// <summary>
/// Dialog window hosting <see cref="Controls.WifiConfigControl"/> with Apply/Cancel lifecycle.
/// </summary>
public partial class WifiConfigDialogView : Window
{
    private readonly WifiConfigViewModel _viewModel;

    public WifiConfigDialogView(WifiConfigViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        _viewModel.RequestClose += () =>
        {
            DialogResult = _viewModel.DialogResult;
            Close();
        };
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
