using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace DynamicIsland.Core;

/// <summary>Báo khi nội dung clipboard thay đổi (AddClipboardFormatListener trên cửa sổ đảo).</summary>
public static class ClipboardMonitor
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;
    private static HwndSource? _source;
    private static IntPtr _hwnd;

    public static event Action? Changed;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    public static void Attach(IntPtr hwnd)
    {
        _hwnd = hwnd;
        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(Hook);
        AddClipboardFormatListener(hwnd);
    }

    public static void Detach()
    {
        if (_hwnd == IntPtr.Zero) return;
        RemoveClipboardFormatListener(_hwnd);
        _source?.RemoveHook(Hook);
        _hwnd = IntPtr.Zero;
    }

    private static IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE) Changed?.Invoke();
        return IntPtr.Zero;
    }
}
