using System.Windows.Controls;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// UserControl điều khiển giao diện cấu hình đa cổng RS-485 Modbus Master & Slave chuẩn công nghiệp.
/// Tái sử dụng đồng nhất giữa Tab Thiết bị và Hộp thoại cấu hình độc lập.
/// </summary>
public partial class ModbusPortConfigControl : UserControl
{
    public ModbusPortConfigControl()
    {
        InitializeComponent();
    }
}
