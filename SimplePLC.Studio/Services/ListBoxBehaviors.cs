using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Cung cấp các attached behavior cho WPF <see cref="ListBox"/> trong môi trường công nghiệp SynaptiX IDE:
/// - AutoSelectOnFocus: Tự động đồng bộ hóa việc chọn dòng (<see cref="ListBoxItem.IsSelected"/>)
///   ngay khi người vận hành nhấp chuột (click) hoặc chuyển tiêu điểm bàn phím (Tab/Focus)
///   vào bất kỳ điều khiển con nào bên trong hàng (TextBox, ComboBox, CheckBox...).
///   Đảm bảo nguyên lý Visual Truth: dòng đang nhập liệu luôn trùng khớp với dòng được highlight
///   và thông số hiển thị trên Inspector.
/// </summary>
public static class ListBoxBehaviors
{
    /// <summary>
    /// Attached property kích hoạt tính năng tự động chọn dòng khi bất kỳ phần tử con nào nhận focus hoặc click chuột.
    /// </summary>
    public static readonly DependencyProperty AutoSelectOnFocusProperty =
        DependencyProperty.RegisterAttached(
            "AutoSelectOnFocus",
            typeof(bool),
            typeof(ListBoxBehaviors),
            new UIPropertyMetadata(false, OnAutoSelectOnFocusChanged));

    /// <summary>
    /// Lấy giá trị của thuộc tính <see cref="AutoSelectOnFocusProperty"/>.
    /// </summary>
    /// <param name="obj">Đối tượng WPF.</param>
    /// <returns>True nếu tính năng được bật.</returns>
    public static bool GetAutoSelectOnFocus(DependencyObject obj) => (bool)obj.GetValue(AutoSelectOnFocusProperty);

    /// <summary>
    /// Gán giá trị cho thuộc tính <see cref="AutoSelectOnFocusProperty"/>.
    /// </summary>
    /// <param name="obj">Đối tượng WPF.</param>
    /// <param name="value">Trạng thái bật/tắt.</param>
    public static void SetAutoSelectOnFocus(DependencyObject obj, bool value) => obj.SetValue(AutoSelectOnFocusProperty, value);

    private static void OnAutoSelectOnFocusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListBox listBox)
        {
            if ((bool)e.NewValue)
            {
                listBox.AddHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(OnListBoxPreviewMouseDown), true);
                listBox.AddHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnListBoxGotKeyboardFocus), true);
            }
            else
            {
                listBox.RemoveHandler(UIElement.PreviewMouseDownEvent, new MouseButtonEventHandler(OnListBoxPreviewMouseDown));
                listBox.RemoveHandler(UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnListBoxGotKeyboardFocus));
            }
        }
    }

    private static void OnListBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            SynchronizeSelection(sender as ListBox, source);
        }
    }

    private static void OnListBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source)
        {
            SynchronizeSelection(sender as ListBox, source);
        }
    }

    private static void SynchronizeSelection(ListBox? listBox, DependencyObject source)
    {
        if (listBox == null) return;

        var item = FindAncestorOrSelf<ListBoxItem>(source);
        if (item == null) return;

        var owner = ItemsControl.ItemsControlFromItemContainer(item) as ListBox 
                    ?? FindAncestorOrSelf<ListBox>(item);

        if ((owner == null || owner == listBox) && !item.IsSelected)
        {
            item.IsSelected = true;
            if (item.DataContext != null && item.DataContext != item && listBox.SelectedItem != item.DataContext)
            {
                listBox.SelectedItem = item.DataContext;
            }
        }
    }

    private static T? FindAncestorOrSelf<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            DependencyObject? parent = null;
            if (current is Visual or System.Windows.Media.Media3D.Visual3D)
            {
                parent = VisualTreeHelper.GetParent(current);
            }

            current = parent ?? LogicalTreeHelper.GetParent(current);
        }
        return null;
    }
}
