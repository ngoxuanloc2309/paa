using System;
using System.ComponentModel;
using System.Windows;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Views;

public partial class WifiConfigDialogView : Window
{
    private readonly WifiConfigViewModel _viewModel;
    private bool _isUpdatingPassword;

    public WifiConfigDialogView(WifiConfigViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;

        if (!string.IsNullOrEmpty(_viewModel.Password))
        {
            PasswordBoxControl.Password = _viewModel.Password;
        }

        _viewModel.RequestClose += () =>
        {
            DialogResult = _viewModel.DialogResult;
            Close();
        };

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WifiConfigViewModel.Password) && !_isUpdatingPassword)
        {
            _isUpdatingPassword = true;
            PasswordBoxControl.Password = _viewModel.Password;
            _isUpdatingPassword = false;
        }
    }

    private void PasswordBoxControl_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_isUpdatingPassword)
        {
            _isUpdatingPassword = true;
            _viewModel.Password = PasswordBoxControl.Password;
            _isUpdatingPassword = false;
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _viewModel.ApplySelectedApAndReturnCommand.Execute(null);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _viewModel.Dispose();
    }
}
