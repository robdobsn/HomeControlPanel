using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

public static class Win32Helper
{
    public static void ShowWindowNoActive(Window window)
    {
        var hwnd = (HwndSource.FromVisual(window) as HwndSource).Handle;
        ShowWindow(hwnd, ShowWindowCommands.SW_SHOWNOACTIVATE);
        SetWindowPos(hwnd.ToInt32(), HWND_TOPMOST,
                (int)window.Left, (int)window.Top, (int)window.Width, (int)window.Height,
                SWP_NOACTIVATE);
    }

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, ShowWindowCommands nCmdShow);

    private enum ShowWindowCommands : int
    {
        SW_SHOWNOACTIVATE = 4
    }

    private const int HWND_TOPMOST = -1;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
    static extern bool SetWindowPos(
     int hWnd,             // Window handle
     int hWndInsertAfter,  // Placement-order handle
     int X,                // Horizontal position
     int Y,                // Vertical position
     int cx,               // Width
     int cy,               // Height
     uint uFlags);         // Window positioning flags

}
