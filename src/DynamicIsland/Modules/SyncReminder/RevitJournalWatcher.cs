using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace DynamicIsland.Modules.SyncReminder;

/// <summary>
/// Nhận biết lần Sync with Central gần nhất bằng cách ĐỌC file journal mà Revit tự ghi
/// (%LOCALAPPDATA%\Autodesk\Revit\Autodesk Revit 20xx\Journals). Chỉ đọc, mở với FileShare.ReadWrite
/// để không khóa file; không mở file .rvt, không gọi API Revit, không cần add-in.
/// </summary>
public sealed partial class RevitJournalWatcher
{
    private string? _file;
    private long _offset;
    private DateTime? _lastStamp;

    // Lệnh Sync trong journal: "Synchronize with Central , ID_FILE_SAVE_TO_MASTER" (kể cả bản _SHORTCUT).
    [GeneratedRegex(@"ID_FILE_SAVE_TO_MASTER|SynchronizeWithCentral", RegexOptions.IgnoreCase)]
    private static partial Regex SyncPattern();

    // Dòng mốc thời gian, vd. 'C 26-Oct-2026 11:43:12.123;
    [GeneratedRegex(@"^'[A-Z] (\d{2}-[A-Za-z]{3}-\d{4} \d{2}:\d{2}:\d{2})")]
    private static partial Regex StampPattern();

    /// <summary>Đọc phần mới của journal; trả về thời điểm sync mới nhất thấy được, nếu có.</summary>
    public DateTime? Poll()
    {
        var newest = FindNewestJournal();
        if (newest is null) return null;

        if (newest != _file)
        {
            _file = newest;
            _offset = 0;
            _lastStamp = null;
        }

        using var stream = new FileStream(_file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length < _offset) _offset = 0;
        stream.Seek(_offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();
        _offset = stream.Length;

        DateTime? found = null;
        foreach (var line in text.Split('\n'))
        {
            var stamp = StampPattern().Match(line);
            if (stamp.Success &&
                DateTime.TryParseExact(stamp.Groups[1].Value, "dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                _lastStamp = t;

            if (SyncPattern().IsMatch(line)) found = _lastStamp ?? DateTime.Now;
        }
        return found;
    }

    private static string? FindNewestJournal()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autodesk", "Revit");
        if (!Directory.Exists(root)) return null;

        return Directory.EnumerateDirectories(root, "Autodesk Revit *")
            .Select(dir => Path.Combine(dir, "Journals"))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "journal.*.txt"))
            .Select(path => new FileInfo(path))
            .Where(f => f.LastWriteTime > DateTime.Now.AddDays(-1))
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault()?.FullName;
    }
}
