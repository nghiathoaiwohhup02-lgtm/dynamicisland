using System.IO;

namespace DynamicIsland.Core;

/// <summary>Ghi lỗi ra %AppData%\DynamicIsland\log.txt để dễ gửi lại khi có sự cố.</summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string FilePath => Path.Combine(SettingsStore.Folder, "log.txt");

    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(SettingsStore.Folder);
                var info = new FileInfo(FilePath);
                if (info.Exists && info.Length > 1_000_000) info.Delete();
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Không để việc ghi log làm sập ứng dụng.
        }
    }
}
