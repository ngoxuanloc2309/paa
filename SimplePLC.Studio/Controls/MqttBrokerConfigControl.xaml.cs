using System.Windows.Controls;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// Reusable Industrial MQTT Broker Configuration Control.
/// Shared between Tab 0 (Device Configuration) and MqttBrokerConfigDialogView.
/// </summary>
public partial class MqttBrokerConfigControl : UserControl
{
    public MqttBrokerConfigControl()
    {
        InitializeComponent();
    }
}
