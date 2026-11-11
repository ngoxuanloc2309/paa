using System.Windows;
using System.Windows.Controls;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// UserControl cấu hình chuyên sâu mạng truyền thông không dây Zigbee Modbus (SPLC-SPEC-ZIGBEE-006).
/// Tái sử dụng đồng nhất giữa màn hình Device Configuration và các hộp thoại FBD.
/// </summary>
public partial class ZigbeeConfigControl : UserControl
{
    public ZigbeeConfigControl()
    {
        InitializeComponent();
    }

    private void TxtStationId_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ZigbeeConfigViewModel vm)
        {
            vm.CommitStationIdCommand.Execute(null);
        }
    }

    private void TxtPanId_LostFocus(object sender, RoutedEventArgs e)
    {
        if (DataContext is ZigbeeConfigViewModel vm)
        {
            vm.CommitPanIdCommand.Execute(null);
        }
    }
}
