using System.Windows.Controls;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// Reusable Industrial MQTT Command (Subscribe) Control.
/// Dynamic Output Pins list and real-time Payload Test extraction simulator.
/// Shared between Tab 0 (Device Configuration) and MqttSubConfigDialogView (MQTT_SUB block).
/// </summary>
public partial class MqttCommandSubscribeControl : UserControl
{
    public MqttCommandSubscribeControl()
    {
        InitializeComponent();
    }
}
