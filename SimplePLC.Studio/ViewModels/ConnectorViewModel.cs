using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

namespace SimplePLC.Studio.ViewModels;

public partial class ConnectorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isInput;

    [ObservableProperty]
    private bool _isEvent;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private Point _anchor;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public void ClearDiagnostics()
    {
        HasError = false;
        ErrorMessage = string.Empty;
    }

    public GraphNodeViewModel Node { get; set; } = null!;
}
