using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Water;

public sealed record WaterDay(string Label, int Count, double BarHeight, bool MetGoal, bool IsToday);

/// <summary>Nhắc uống nước trong giờ làm, điểm danh "Đã uống" và thống kê 7 ngày.</summary>
public sealed partial class WaterModule : IslandModule
{
    private const double MaxBarHeight = 36;
    private static readonly TimeSpan RepeatEvery = TimeSpan.FromMinutes(15);
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly WaterLog _log = new();
    private DateTime _countFrom;
    private DateTime _nextPeek;
    private DateTime _today;

    [ObservableProperty] private int _todayCount;
    [ObservableProperty] private string _todayText = "";
    [ObservableProperty] private double _todayProgress;
    [ObservableProperty] private string _streakText = "";
    [ObservableProperty] private string _nextText = "";
    [ObservableProperty] private bool _isDue;

    public override string Id => "water";
    public override string Title => "Uống nước";
    public override string Glyph => "\uEB42";
    public override int LivePriority => 30;
    public override bool CanPinCompact => true;

    public ObservableCollection<WaterDay> Week { get; } = [];

    private AppSettings S => Context.Settings.Current;
    private TimeSpan Interval => TimeSpan.FromMinutes(S.WaterReminderMinutes);
    private int Goal => Math.Max(1, S.WaterGoalPerDay);

    protected override Task OnInitializeAsync()
    {
        _log.Load();
        _today = DateTime.Today;
        _countFrom = _log.LastToday() ?? DateTime.Now;
        _nextPeek = _countFrom + Interval;
        RefreshStats();

        if (S.WaterReminderMinutes > 0)
        {
            _poll.Tick += (_, _) => Update();
            _poll.Start();
        }
        Update();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new WaterView();

    protected override FrameworkElement CreateCompactView() => new WaterCompactView();

    private void Update()
    {
        var now = DateTime.Now;
        if (now.Date != _today)
        {
            _today = now.Date;
            RefreshStats();
        }

        if (S.WaterReminderMinutes <= 0)
        {
            NextText = "Đã tắt nhắc trong cấu hình";
            return;
        }

        var (start, end) = WorkHours();
        var inWork = now.TimeOfDay >= start && now.TimeOfDay <= end;
        if (!inWork)
        {
            IsDue = IsLive = false;
            NextText = $"Chỉ nhắc trong giờ làm {S.WorkStart}–{S.WorkEnd}";
            return;
        }

        // Đầu giờ làm thì tính từ lúc vào làm, không nhắc dồn từ hôm trước.
        var from = _countFrom.Date == now.Date && _countFrom.TimeOfDay >= start ? _countFrom : now.Date + start;
        var due = from + Interval;
        IsDue = now >= due;
        IsLive = IsDue;
        NextText = IsDue ? "Đến giờ uống nước rồi" : $"Lần nhắc tới: {due:HH:mm}";

        if (IsDue && now >= _nextPeek)
        {
            Peek("\uEB42", "Uống nước thôi", $"Hôm nay {TodayCount}/{Goal} lần · bấm vào đảo để điểm danh", seconds: 6, playSound: true);
            _nextPeek = now + RepeatEvery;
        }
    }

    private (TimeSpan Start, TimeSpan End) WorkHours()
    {
        var start = TimeSpan.TryParse(S.WorkStart, CultureInfo.InvariantCulture, out var a) ? a : new TimeSpan(8, 0, 0);
        var end = TimeSpan.TryParse(S.WorkEnd, CultureInfo.InvariantCulture, out var b) ? b : new TimeSpan(17, 30, 0);
        return (start, end);
    }

    [RelayCommand]
    private void Drink()
    {
        var now = DateTime.Now;
        _log.Add(now);
        _countFrom = now;
        _nextPeek = now + Interval;
        RefreshStats();
        Update();
        if (TodayCount == Goal) Peek("\uEB42", "Đạt mục tiêu hôm nay", $"{Goal}/{Goal} lần, giỏi lắm!", seconds: 4);
    }

    [RelayCommand]
    private void Undo()
    {
        if (!_log.RemoveLastToday()) return;
        _countFrom = _log.LastToday() ?? DateTime.Now;
        RefreshStats();
        Update();
    }

    private void RefreshStats()
    {
        TodayCount = _log.CountOn(DateTime.Today);
        TodayText = $"{TodayCount}/{Goal}";
        TodayProgress = Math.Min(1, TodayCount / (double)Goal);

        Week.Clear();
        var days = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i - 6)).ToList();
        var top = Math.Max(Goal, days.Max(_log.CountOn));
        foreach (var day in days)
        {
            var count = _log.CountOn(day);
            var label = day == DateTime.Today ? "Nay" : Vietnamese.TextInfo.ToTitleCase(day.ToString("ddd", Vietnamese));
            Week.Add(new WaterDay(label, count, Math.Max(3, MaxBarHeight * count / top), count >= Goal, day == DateTime.Today));
        }

        // Chuỗi ngày liên tiếp đạt mục tiêu (hôm nay chưa đạt thì tính tới hôm qua).
        var streak = 0;
        var cursor = _log.CountOn(DateTime.Today) >= Goal ? DateTime.Today : DateTime.Today.AddDays(-1);
        while (_log.CountOn(cursor) >= Goal)
        {
            streak++;
            cursor = cursor.AddDays(-1);
        }
        StreakText = streak > 0 ? $"Chuỗi {streak} ngày đạt mục tiêu" : "Chưa có chuỗi ngày đạt mục tiêu";
    }

    public override void Dispose() => _poll.Stop();
}
