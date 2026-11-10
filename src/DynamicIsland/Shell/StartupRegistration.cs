using System.Diagnostics;
using System.IO;
using DynamicIsland.Core;
using Microsoft.Win32;

namespace DynamicIsland.Shell;

/// <summary>
/// Bật/tắt khởi động cùng Windows qua khóa Run của người dùng hiện tại (không cần quyền admin).
/// File tải về có số phiên bản trong tên (DynamicIsland-0.5.1.exe), nên mỗi lần mở bản mới
/// khóa Run được trỏ sang file đang chạy: máy khởi động lên luôn mở bản mới nhất.
/// </summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    // Tắt trong Task Manager → Startup (hoặc Settings → Apps → Startup) không xóa khóa Run mà ghi cờ vào đây.
    private const string ApprovedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    private const string ValueName = "DynamicIsland";

    /// <summary>Tham số Windows truyền vào khi mở đảo lúc đăng nhập.</summary>
    public const string AutostartArgument = "--autostart";

    /// <summary>Đã đăng ký và không bị tắt trong Task Manager.</summary>
    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string && !IsDisabledInTaskManager();
        }
    }

    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        key.SetValue(ValueName, CommandLine(CurrentPath()));
        // Người dùng chủ động bật lại: bỏ cờ "Disabled" mà Task Manager đã ghi, không thì Windows vẫn bỏ qua.
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey, writable: true);
        approved?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>
    /// Gọi mỗi lần mở đảo. Nếu đang bật khởi động cùng Windows mà khóa Run trỏ tới file khác
    /// (bản cũ, hoặc file đã bị xóa/dời chỗ) thì trỏ sang file đang chạy.
    /// Không trỏ về bản cũ hơn khi lỡ mở lại file cũ. Trả về true nếu đã sửa khóa Run.
    /// </summary>
    public static bool FollowCurrentExecutable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key?.GetValue(ValueName) is not string registered) return false;

        var current = CurrentPath();
        var expected = CommandLine(current);
        if (registered == expected) return false;

        var registeredPath = ExecutablePath(registered);
        if (registeredPath is not null && !SamePath(registeredPath, current)
            && File.Exists(registeredPath) && FileVersion(registeredPath) > RunningVersion)
            return false;

        key.SetValue(ValueName, expected);
        Log.Info($"Khởi động cùng Windows: đổi từ {registered} sang {expected}");
        return true;
    }

    private static Version RunningVersion { get; } =
        typeof(StartupRegistration).Assembly.GetName().Version is { } v ? new(v.Major, v.Minor, Math.Max(v.Build, 0)) : new();

    private static string CurrentPath() =>
        Environment.ProcessPath ?? throw new InvalidOperationException("Không biết đường dẫn file .exe đang chạy");

    private static string CommandLine(string path) => $"\"{path}\" {AutostartArgument}";

    // Lấy đường dẫn file từ dòng lệnh trong khóa Run: "C:\...\DynamicIsland.exe" --autostart.
    private static string? ExecutablePath(string commandLine)
    {
        var text = commandLine.Trim();
        if (text.StartsWith('"'))
        {
            var end = text.IndexOf('"', 1);
            return end > 1 ? text[1..end] : null;
        }
        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? text[..(exe + 4)] : text.Length > 0 ? text : null;
    }

    private static bool SamePath(string a, string b)
    {
        try
        {
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return false;
        }
    }

    // File không đọc được số phiên bản thì coi như bản cũ nhất.
    private static Version FileVersion(string path)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart);
        }
        catch (Exception)
        {
            return new Version();
        }
    }

    // Byte đầu của giá trị là số lẻ (03, 07...) nghĩa là đã tắt; không có giá trị nghĩa là đang bật.
    private static bool IsDisabledInTaskManager()
    {
        using var approved = Registry.CurrentUser.OpenSubKey(ApprovedKey);
        return approved?.GetValue(ValueName) is byte[] { Length: > 0 } flags && (flags[0] & 1) == 1;
    }
}
