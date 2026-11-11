using System.Windows.Controls;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// Reusable Industrial MQTT Telemetry (Publish) Control.
/// Visual No-Code JSON Key to Tag mapping table with real-time Payload Preview.
/// Shared between Tab 0 (Device Configuration) and MqttConfigDialogView (MQTT_PUB block).
/// </summary>
public partial class MqttTelemetryPublishControl : UserControl
{
    public MqttTelemetryPublishControl()
    {
        InitializeComponent();
    }
}
