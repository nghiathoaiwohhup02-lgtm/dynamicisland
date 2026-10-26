using System.Text;
using System.Windows.Threading;

namespace DynamicIsland.Shell;

/// <summary>Theo dõi cửa sổ phía trước có đang chiếm trọn màn hình không (trình chiếu, video, game).</summary>
internal sealed class FullscreenWatcher : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private readonly IntPtr _ownWindow;
    private bool _isFullscreen;

    public FullscreenWatcher(IntPtr ownWindow)
    {
        _ownWindow = ownWindow;
        _timer.Tick += (_, _) => Check();
    }

    public event EventHandler<bool>? FullscreenChanged;

    public void Start() => _timer.Start();

    private void Check()
    {
        var now = IsForegroundFullscreen();
        if (now == _isFullscreen) return;
        _isFullscreen = now;
        FullscreenChanged?.Invoke(this, now);
    }

    private bool IsForegroundFullscreen()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero || hwnd == _ownWindow ||
            hwnd == NativeMethods.GetShellWindow() || hwnd == NativeMethods.GetDesktopWindow())
            return false;

        // Màn hình nền (Progman/WorkerW) cũng phủ kín màn hình nhưng không phải toàn màn hình.
        var className = new StringBuilder(64);
        NativeMethods.GetClassName(hwnd, className, className.Capacity);
        if (className.ToString() is "Progman" or "WorkerW") return false;

        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return false;
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info)) return false;

        var m = info.rcMonitor;
        return rect.Left <= m.Left && rect.Top <= m.Top && rect.Right >= m.Right && rect.Bottom >= m.Bottom;
    }

    public void Dispose() => _timer.Stop();
}
