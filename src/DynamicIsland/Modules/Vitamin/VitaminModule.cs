using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Vitamin;

public sealed record VitaminDay(string Label, bool Taken, bool IsToday);

/// <summary>
/// Nhắc uống vitamin mỗi sáng: từ giờ VitaminTime tới khi bấm "Đã uống" thì đảo hiện nhắc,
/// 15 phút bung tin một lần. Mỗi lần điểm danh ghi vào %AppData%\DynamicIsland\vitamin.csv.
/// </summary>
public sealed partial class VitaminModule : IslandModule
{
    private static readonly TimeSpan RepeatEvery = TimeSpan.FromMinutes(15);
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly string FilePath = Path.Combine(SettingsStore.Folder, "vitamin.csv");

    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly List<DateTime> _entries = [];
    private DateTime _nextPeek;

    [ObservableProperty] private bool _takenToday;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _streakText = "";

    public override string Id => "vitamin";
    public override string Title => "Vitamin";
    public override string Glyph => "";
    public override int LivePriority => 38;

    public ObservableCollection<VitaminDay> Week { get; } = [];

    private string TimeSetting => Context.Settings.Current.VitaminTime;

    protected override Task OnInitializeAsync()
    {
        Load();
        _poll.Tick += (_, _) => Update();
        _poll.Start();
        Update();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new VitaminView();

    protected override FrameworkElement CreateCompactView() => new VitaminCompactView();

    private void Update()
    {
        var now = DateTime.Now;
        var taken = _entries.LastOrDefault(t => t.Date == now.Date);
        TakenToday = taken != default;
        RefreshWeek();

        if (string.IsNullOrWhiteSpace(TimeSetting) ||
            !TimeSpan.TryParse(TimeSetting, CultureInfo.InvariantCulture, out var at))
        {
            IsLive = false;
            StatusText = TakenToday ? $"Hôm nay đã uống lúc {taken:HH:mm}" : "Đã tắt nhắc trong cấu hình";
            return;
        }

        if (TakenToday)
        {
            IsLive = false;
            StatusText = $"Hôm nay đã uống lúc {taken:HH:mm}";
            return;
        }

        var due = now.TimeOfDay >= at;
        IsLive = due;
        StatusText = due ? "Đến giờ uống vitamin rồi" : $"Nhắc lúc {at:hh\\:mm} sáng nay";

        if (due && now >= _nextPeek)
        {
            Peek("", "Uống vitamin", "Bấm vào đảo, chọn \"Đã uống\" để điểm danh", seconds: 8, playSound: true);
            _nextPeek = now + RepeatEvery;
        }
    }

    [RelayCommand]
    private void Take()
    {
        if (_entries.Any(t => t.Date == DateTime.Today)) return;
        _entries.Add(DateTime.Now);
        Save();
        Update();
    }

    [RelayCommand]
    private void Undo()
    {
        var index = _entries.FindLastIndex(t => t.Date == DateTime.Today);
        if (index < 0) return;
        _entries.RemoveAt(index);
        Save();
        _nextPeek = DateTime.Now + RepeatEvery;
        Update();
    }

    private void RefreshWeek()
    {
        Week.Clear();
        for (var i = -6; i <= 0; i++)
        {
            var day = DateTime.Today.AddDays(i);
            var label = i == 0 ? "Nay" : Vietnamese.TextInfo.ToTitleCase(day.ToString("ddd", Vietnamese));
            Week.Add(new VitaminDay(label, _entries.Any(t => t.Date == day), i == 0));
        }

        var streak = 0;
        var cursor = TakenToday ? DateTime.Today : DateTime.Today.AddDays(-1);
        while (_entries.Any(t => t.Date == cursor))
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        StreakText = streak > 0 ? $"Chuỗi {streak} ngày liên tiếp" : "Chưa có chuỗi ngày liên tiếp";
    }

    private void Load()
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

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllLines(FilePath, _entries.Select(t => t.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Prepend("time"));
        }
        catch (Exception ex)
        {
            Log.Error("Không ghi được vitamin.csv", ex);
        }
    }

    public override void Dispose() => _poll.Stop();
}
