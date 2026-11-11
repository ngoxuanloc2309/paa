using System.Windows.Controls;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// Interaction logic for HttpClientConfigControl.xaml
/// Cung cấp giao diện cấu hình chuyên sâu REST API HTTP Client / Webhook
/// (URL, Method, Headers, Timeout, Trigger Mode, Visual Field Builder, Live JSON Payload Preview).
/// </summary>
public partial class HttpClientConfigControl : UserControl
{
    public HttpClientConfigControl()
    {
        InitializeComponent();
    }
}
