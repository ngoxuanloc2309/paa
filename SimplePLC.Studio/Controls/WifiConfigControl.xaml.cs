using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// Reusable industrial Wi-Fi configuration and wireless telemetry control.
/// Shared between Tab 0 (Device Configuration) and WifiConfigDialogView.
/// </summary>
public partial class WifiConfigControl : UserControl
{
    private bool _isUpdatingPassword;

    public WifiConfigControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is WifiConfigViewModel oldVm)
        {
            oldVm.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (e.NewValue is WifiConfigViewModel newVm)
        {
            newVm.PropertyChanged += ViewModel_PropertyChanged;
            if (!string.IsNullOrEmpty(newVm.Password))
            {
                _isUpdatingPassword = true;
                PasswordBoxControl.Password = newVm.Password;
                _isUpdatingPassword = false;
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (DataContext is not WifiConfigViewModel vm) return;

        if (e.PropertyName == nameof(WifiConfigViewModel.Password) && !_isUpdatingPassword)
        {
            _isUpdatingPassword = true;
            PasswordBoxControl.Password = vm.Password;
            _isUpdatingPassword = false;
        }
    }

    private void PasswordBoxControl_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WifiConfigViewModel vm) return;

        if (!_isUpdatingPassword)
        {
            _isUpdatingPassword = true;
            vm.Password = PasswordBoxControl.Password;
            _isUpdatingPassword = false;
        }
    }

    private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DataContext is WifiConfigViewModel vm)
        {
            vm.ApplySelectedApAndReturnCommand.Execute(null);
        }
    }
}
