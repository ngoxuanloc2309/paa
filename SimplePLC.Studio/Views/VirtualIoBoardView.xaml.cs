using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SimplePLC.Studio.Views;

/// <summary>
/// Interaction logic for VirtualIoBoardView.xaml
/// Hỗ trợ kéo thả tự do (Free Canvas Dragging), thu nhỏ/bung mở (Minimize/Expand),
/// và đặt lại vị trí mặc định (Reset Position).
/// </summary>
public partial class VirtualIoBoardView : UserControl
{
    private bool _isDragging;
    private Point _startMousePos;
    private Point _startTransform;
    private bool _isMinimized;

    public VirtualIoBoardView()
    {
        InitializeComponent();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMinimize();
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement header)
        {
            _isDragging = true;
            _startMousePos = e.GetPosition(Parent as UIElement ?? this);
            _startTransform = new Point(DragTransform.X, DragTransform.Y);
            header.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Header_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && sender is FrameworkElement)
        {
            Point curMousePos = e.GetPosition(Parent as UIElement ?? this);
            double deltaX = curMousePos.X - _startMousePos.X;
            double deltaY = curMousePos.Y - _startMousePos.Y;

            // Làm tròn pixel số nguyên để đảm bảo font chữ ClearType luôn sắc nét, không bị nhòe sub-pixel
            DragTransform.X = Math.Round(_startTransform.X + deltaX);
            DragTransform.Y = Math.Round(_startTransform.Y + deltaY);
            e.Handled = true;
        }
    }

    private void Header_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && sender is FrameworkElement header)
        {
            _isDragging = false;
            header.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void ToggleMinimize()
    {
        _isMinimized = !_isMinimized;
        ContentScrollViewer.Visibility = _isMinimized ? Visibility.Collapsed : Visibility.Visible;
        FooterBorder.Visibility = _isMinimized ? Visibility.Collapsed : Visibility.Visible;
        HeaderBorder.CornerRadius = _isMinimized ? new CornerRadius(3) : new CornerRadius(3, 3, 0, 0);
    }
}
