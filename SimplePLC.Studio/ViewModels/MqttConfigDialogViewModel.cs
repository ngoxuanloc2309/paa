using CommunityToolkit.Mvvm.ComponentModel;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.ViewModels;

/// <summary>
/// ViewModel phục vụ hộp thoại cấu hình chuyên sâu MQTT Telemetry & Visual Field Builder.
/// </summary>
public partial class MqttConfigDialogViewModel : ObservableObject
{
    public MqttPublishNodeViewModel Node { get; }
    public TagCatalogViewModel TagCatalog { get; }

    public MqttConfigDialogViewModel(MqttPublishNodeViewModel node, TagCatalogViewModel tagCatalog)
    {
        Node = node;
        TagCatalog = tagCatalog;
    }
}
