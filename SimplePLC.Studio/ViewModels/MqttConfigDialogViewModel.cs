using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình chuyên sâu MQTT Telemetry & Visual Field Builder.
/// </summary>
public partial class MqttConfigDialogViewModel : ObservableObject
{
    public MqttPublishNodeViewModel Node { get; }
    public TagCatalogViewModel TagCatalog { get; }

    public Action? OnOpenDeviceBrokerSettings { get; set; }

    [RelayCommand]
    public void OpenDeviceBrokerSettings()
    {
        OnOpenDeviceBrokerSettings?.Invoke();
    }

    public MqttConfigDialogViewModel(MqttPublishNodeViewModel node, TagCatalogViewModel tagCatalog)
    {
        Node = node;
        TagCatalog = tagCatalog;
    }
}
