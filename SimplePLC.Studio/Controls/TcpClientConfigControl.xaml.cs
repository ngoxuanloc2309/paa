using System.Windows;
using System.Windows.Controls;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// Interaction logic for TcpClientConfigControl.xaml
/// Cung cấp giao diện cấu hình chuyên sâu Socket TCP Client / Stream Passthrough
/// (Remote Host, Port, Reconnect delay, Registration packet, Heartbeat ping, Hex Stream Live Preview).
/// </summary>
public partial class TcpClientConfigControl : UserControl
{
    public TcpClientConfigControl()
    {
        InitializeComponent();
    }

    private void InsertToken_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string token && TxtCustomTemplate != null)
        {
            int index = TxtCustomTemplate.SelectionStart;
            string text = TxtCustomTemplate.Text ?? string.Empty;
            TxtCustomTemplate.Text = text.Insert(index, token);
            TxtCustomTemplate.SelectionStart = index + token.Length;
            TxtCustomTemplate.Focus();
        }
    }
}
