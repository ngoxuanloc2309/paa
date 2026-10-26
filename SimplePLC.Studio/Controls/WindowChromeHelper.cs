using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SimplePLC.Studio.Controls;

/// <summary>
/// Cung cấp hỗ trợ tùy chỉnh Chrome cửa sổ Win32 cho các hộp thoại Modal WPF.
/// Hỗ trợ 2 chế độ:
/// 1. EnableIndependentMinimize: Hiển thị đầy đủ 3 nút [—] [◻] [✕], cho phép thu nhỏ popup xuống Taskbar
///    một cách độc lập mà KHÔNG làm thu nhỏ cửa sổ chính MainWindow.
/// 2. DisableMinimize: Ẩn/vô hiệu hóa nút Thu nhỏ [—], chỉ cho phép Phóng to [◻] và Đóng [✕].
/// </summary>
public static class WindowChromeHelper
{
    private const int GWL_STYLE = -16;
    private const int GWLP_HWNDPARENT = -8;
    private const int WS_MINIMIZEBOX = 0x00020000;
    private const int WM_SYSCOMMAND = 0x0112;
    private const int WM_ACTIVATE = 0x0006;
    private const int SC_MINIMIZE = 0xF020;
    private const int SC_RESTORE = 0xF120;
    private const int SW_MINIMIZE = 6;
    private const int SW_RESTORE = 9;
    private const uint MF_BYCOMMAND = 0x00000000;
    private const uint GW_OWNER = 4;

    private static readonly ConcurrentDictionary<IntPtr, IntPtr> DetachedOwners = new();

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool EnableWindow(IntPtr hWnd, bool bEnable);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

    [DllImport("user32.dll")]
    private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

    private static IntPtr GetHwndParent(IntPtr hwnd) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, GWLP_HWNDPARENT) : new IntPtr(GetWindowLong32(hwnd, GWLP_HWNDPARENT));

    private static void SetHwndParent(IntPtr hwnd, IntPtr newParent)
    {
        if (IntPtr.Size == 8)
            SetWindowLongPtr64(hwnd, GWLP_HWNDPARENT, newParent);
        else
            SetWindowLong32(hwnd, GWLP_HWNDPARENT, newParent.ToInt32());
    }

    private static IntPtr GetOwnerHwnd(IntPtr hwnd)
    {
        var owner = GetWindow(hwnd, GW_OWNER);
        if (owner != IntPtr.Zero) return owner;

        owner = GetHwndParent(hwnd);
        if (owner != IntPtr.Zero) return owner;

        if (System.Windows.Application.Current?.MainWindow != null)
        {
            var mainHwnd = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;
            if (mainHwnd != IntPtr.Zero && mainHwnd != hwnd) return mainHwnd;
        }

        return IntPtr.Zero;
    }

    public static void CenterOverMainWindow(Window window)
    {
        try
        {
            if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
            {
                var main = System.Windows.Application.Current.MainWindow;
                double targetLeft = main.Left + (main.Width - window.Width) / 2;
                double targetTop = main.Top + (main.Height - window.Height) / 2;

                if (targetLeft < SystemParameters.VirtualScreenLeft) targetLeft = SystemParameters.VirtualScreenLeft;
                if (targetTop < SystemParameters.VirtualScreenTop) targetTop = SystemParameters.VirtualScreenTop;

                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Left = targetLeft;
                window.Top = targetTop;
            }
        }
        catch
        {
        }
    }

    // ==========================================
    // 1. EnableIndependentMinimize Attached Property
    // ==========================================
    public static readonly DependencyProperty EnableIndependentMinimizeProperty =
        DependencyProperty.RegisterAttached(
            "EnableIndependentMinimize",
            typeof(bool),
            typeof(WindowChromeHelper),
            new PropertyMetadata(false, OnEnableIndependentMinimizeChanged));

    public static bool GetEnableIndependentMinimize(DependencyObject obj) =>
        (bool)obj.GetValue(EnableIndependentMinimizeProperty);

    public static void SetEnableIndependentMinimize(DependencyObject obj, bool value) =>
        obj.SetValue(EnableIndependentMinimizeProperty, value);

    private static void OnEnableIndependentMinimizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window && (bool)e.NewValue)
        {
            if (window.IsLoaded)
            {
                ApplyIndependentMinimize(window);
            }
            else
            {
                window.SourceInitialized += (s, args) => ApplyIndependentMinimize(window);
            }
        }
    }

    private static void ApplyIndependentMinimize(Window window)
    {
        try
        {
            CenterOverMainWindow(window);

            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            window.StateChanged += (s, args) =>
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    var owner = GetOwnerHwnd(hwnd);
                    if (owner != IntPtr.Zero)
                    {
                        DetachedOwners[hwnd] = owner;
                        EnableWindow(owner, true);
                        SetForegroundWindow(owner);
                    }
                }
                else
                {
                    OnWindowRestored(hwnd);
                }
            };

            window.Closed += (s, args) =>
            {
                if (DetachedOwners.TryRemove(hwnd, out var owner) && owner != IntPtr.Zero)
                {
                    EnableWindow(owner, true);
                }
            };

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(IndependentMinimizeWndProc);
        }
        catch
        {
            // Bỏ qua nếu môi trường headless hoặc non-Win32
        }
    }

    private static IntPtr IndependentMinimizeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SYSCOMMAND)
        {
            int cmd = (int)wParam & 0xFFF0;
            if (cmd == SC_MINIMIZE)
            {
                var owner = GetOwnerHwnd(hwnd);
                if (owner != IntPtr.Zero)
                {
                    DetachedOwners[hwnd] = owner;
                    // Kích hoạt lại MainWindow trước khi minimize popup
                    // để Windows không kéo thu nhỏ cả ứng dụng hoặc đẩy MainWindow vào background
                    EnableWindow(owner, true);
                }

                ShowWindow(hwnd, SW_MINIMIZE);

                if (owner != IntPtr.Zero)
                {
                    SetForegroundWindow(owner);
                    SetActiveWindow(owner);
                }

                handled = true;
                return IntPtr.Zero;
            }
            else if (cmd == SC_RESTORE)
            {
                OnWindowRestored(hwnd);
            }
        }
        else if (msg == WM_ACTIVATE)
        {
            int state = (int)wParam & 0xFFFF;
            if (state != 0) // WA_ACTIVE (1) hoặc WA_CLICKACTIVE (2)
            {
                OnWindowRestored(hwnd);
            }
        }

        return IntPtr.Zero;
    }

    private static void OnWindowRestored(IntPtr hwnd)
    {
        if (DetachedOwners.TryGetValue(hwnd, out var owner) && owner != IntPtr.Zero)
        {
            // Khi popup được mở lại từ taskbar, vô hiệu hóa lại MainWindow để bảo toàn tính modal
            EnableWindow(owner, false);
        }
    }

    // ==========================================
    // 2. DisableMinimize Attached Property (Legacy / Option 2)
    // ==========================================
    public static readonly DependencyProperty DisableMinimizeProperty =
        DependencyProperty.RegisterAttached(
            "DisableMinimize",
            typeof(bool),
            typeof(WindowChromeHelper),
            new PropertyMetadata(false, OnDisableMinimizeChanged));

    public static bool GetDisableMinimize(DependencyObject obj) =>
        (bool)obj.GetValue(DisableMinimizeProperty);

    public static void SetDisableMinimize(DependencyObject obj, bool value) =>
        obj.SetValue(DisableMinimizeProperty, value);

    private static void OnDisableMinimizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Window window && (bool)e.NewValue)
        {
            if (window.IsLoaded)
            {
                ApplyDisableMinimize(window);
            }
            else
            {
                window.SourceInitialized += (s, args) => ApplyDisableMinimize(window);
            }
        }
    }

    private static void ApplyDisableMinimize(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;

            long style = IntPtr.Size == 8 
                ? GetWindowLongPtr64(hwnd, GWL_STYLE).ToInt64() 
                : GetWindowLong32(hwnd, GWL_STYLE);

            style &= ~WS_MINIMIZEBOX;

            if (IntPtr.Size == 8)
                SetWindowLongPtr64(hwnd, GWL_STYLE, new IntPtr(style));
            else
                SetWindowLong32(hwnd, GWL_STYLE, (int)style);

            IntPtr hMenu = GetSystemMenu(hwnd, false);
            if (hMenu != IntPtr.Zero)
            {
                DeleteMenu(hMenu, (uint)SC_MINIMIZE, MF_BYCOMMAND);
            }

            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(DisableMinimizeWndProc);
        }
        catch
        {
        }
    }

    private static IntPtr DisableMinimizeWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_SYSCOMMAND && ((int)wParam & 0xFFF0) == SC_MINIMIZE)
        {
            handled = true;
            return IntPtr.Zero;
        }
        return IntPtr.Zero;
    }
}
