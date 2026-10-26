using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace DynamicIsland.Modules.Files;

public sealed record OneDriveAccount(string Name, string Folder);

/// <summary>
/// Trạng thái OneDrive ở mức đọc được an toàn: OneDrive có đang chạy, thư mục đồng bộ, và các bản
/// "xung đột" mà OneDrive tạo ra khi hai nơi cùng sửa một file (tên file kèm tên máy, vd. "A-PC01.xlsx").
/// Windows không có API công khai cho tiến độ đồng bộ, nên phần này không hiện phần trăm đồng bộ.
/// </summary>
public static class OneDriveStatus
{
    private const int MaxEntries = 30_000;

    public static bool IsRunning()
    {
        var processes = Process.GetProcessesByName("OneDrive");
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    public static List<OneDriveAccount> Accounts()
    {
        var result = new List<OneDriveAccount>();
        using var root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\OneDrive\Accounts");
        if (root is null) return result;
        foreach (var name in root.GetSubKeyNames())
        {
            using var key = root.OpenSubKey(name);
            if (key?.GetValue("UserFolder") is string folder && Directory.Exists(folder))
                result.Add(new OneDriveAccount(key.GetValue("DisplayName") as string ?? name, folder));
        }
        return result;
    }

    /// <summary>Tìm file xung đột sửa trong 30 ngày gần đây. Chỉ đọc tên file, không tải file về máy.</summary>
    public static List<string> FindConflicts(IEnumerable<string> folders)
    {
        var marker = "-" + Environment.MachineName;
        var since = DateTime.Now.AddDays(-30);
        var found = new List<string>();
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, MaxRecursionDepth = 8, AttributesToSkip = FileAttributes.System };
        foreach (var folder in folders)
        {
            var seen = 0;
            foreach (var path in Directory.EnumerateFiles(folder, "*", options))
            {
                if (++seen > MaxEntries) break;
                var name = Path.GetFileNameWithoutExtension(path);
                if (!name.EndsWith(marker, StringComparison.OrdinalIgnoreCase) &&
                    !name.Contains(marker + "-", StringComparison.OrdinalIgnoreCase)) continue;
                if (File.GetLastWriteTime(path) >= since) found.Add(path);
            }
        }
        return found;
    }
}
