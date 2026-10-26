using System.Globalization;
using System.IO;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Water;

/// <summary>
/// Sổ điểm danh uống nước: mỗi lần bấm "Đã uống" ghi một dòng thời gian vào
/// %AppData%\DynamicIsland\water.csv. Mở bằng Excel để tự thống kê thêm.
/// </summary>
public sealed class WaterLog
{
    private const string Header = "time";
    private readonly List<DateTime> _entries = [];

    public static string FilePath { get; } = Path.Combine(SettingsStore.Folder, "water.csv");

    public IReadOnlyList<DateTime> Entries => _entries;

    public void Load()
    {
        _entries.Clear();
        if (!File.Exists(FilePath)) return;
        foreach (var line in File.ReadLines(FilePath))
        {
            if (DateTime.TryParse(line.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
                _entries.Add(t);
        }
        _entries.Sort();
    }

    public void Add(DateTime time)
    {
        _entries.Add(time);
        Rewrite();
    }

    public bool RemoveLastToday()
    {
        var index = _entries.FindLastIndex(t => t.Date == DateTime.Today);
        if (index < 0) return false;
        _entries.RemoveAt(index);
        Rewrite();
        return true;
    }

    public int CountOn(DateTime day) => _entries.Count(t => t.Date == day.Date);

    public DateTime? LastToday() => _entries.LastOrDefault(t => t.Date == DateTime.Today) is var t && t != default ? t : null;

    private void Rewrite()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllLines(FilePath, _entries.Select(t => t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Prepend(Header));
        }
        catch (Exception ex)
        {
            Log.Error("Không ghi được water.csv", ex);
        }
    }
}
