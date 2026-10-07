using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.ViewModels;

public partial class ConnectionViewModel : ObservableObject
{
    [ObservableProperty]
    private ConnectorViewModel _source;

    [ObservableProperty]
    private ConnectorViewModel _target;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isGhost;

    [ObservableProperty]
    private DiffState _diffStatus = DiffState.None;

    public ConnectionViewModel(ConnectorViewModel source, ConnectorViewModel target)
    {
        _source = source;
        _target = target;
        source.IsConnected = true;
        target.IsConnected = true;
    }
}
