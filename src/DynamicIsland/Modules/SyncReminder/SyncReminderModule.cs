using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.SyncReminder;

/// <summary>
/// Nhắc Sync with Central định kỳ khi Revit đang mở. Tự nhận biết đã sync qua journal (chỉ đọc),
/// hoặc người dùng bấm "Đã sync". Không tác động gì tới Revit hay file dự án.
/// </summary>
public sealed partial class SyncReminderModule : IslandModule
{
    private static readonly TimeSpan RepeatEvery = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan SnoozeFor = TimeSpan.FromMinutes(5);

    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(20) };
    private readonly RevitJournalWatcher _journal = new();
    private DateTime _nextPeek;
    private DateTime _sessionStart;
    private DateTime _snoozedUntil;

    [ObservableProperty] private bool _isRevitRunning;
    [ObservableProperty] private DateTime? _lastSync;
    [ObservableProperty] private string _status = "Revit chưa mở";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string _compactText = "";
    [ObservableProperty] private bool _isOverdue;

    public override string Id => "sync";
    public override string Title => "Nhắc Sync";
    public override string Glyph => "";
    public override int LivePriority => 40;

    private TimeSpan Interval => TimeSpan.FromMinutes(Context.Settings.Current.SyncReminderMinutes);

    protected override Task OnInitializeAsync()
    {
        if (Context.Settings.Current.SyncReminderMinutes <= 0)
        {
            Status = "Đã tắt nhắc Sync trong cấu hình";
            return Task.CompletedTask;
        }
        _poll.Tick += (_, _) => Update();
        _poll.Start();
        Update();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new SyncReminderView();

    protected override FrameworkElement CreateCompactView() => new SyncReminderCompactView();

    private void Update()
    {
        var running = IsRevitOpen();
        if (running && !IsRevitRunning)
        {
            // Mở Revit: tính giờ từ lúc này, trừ khi journal cho biết đã sync gần hơn.
            LastSync = null;
            _sessionStart = DateTime.Now;
            _nextPeek = DateTime.Now + Interval;
        }
        IsRevitRunning = running;

        if (!running)
        {
            Status = "Revit chưa mở";
            Detail = "Mở Revit là bắt đầu tính giờ";
            IsOverdue = IsLive = false;
            return;
        }

        try
        {
            if (_journal.Poll() is DateTime synced && synced > (LastSync ?? DateTime.MinValue))
                MarkSynced(synced, fromJournal: true);
        }
        catch (Exception ex)
        {
            // Không đọc được journal thì vẫn nhắc theo giờ, người dùng tự bấm "Đã sync".
            Log.Error("Đọc journal Revit lỗi", ex);
        }

        var since = DateTime.Now - (LastSync ?? _sessionStart);
        var minutes = (int)since.TotalMinutes;
        Status = minutes < 1 ? "Vừa sync xong" : $"{minutes} phút chưa sync";
        Detail = LastSync is DateTime t
            ? $"Lần cuối: {t:HH:mm} · nhắc mỗi {Interval.TotalMinutes:0} phút"
            : $"Tính từ lúc mở Revit {_sessionStart:HH:mm} · nhắc mỗi {Interval.TotalMinutes:0} phút";
        CompactText = $"{minutes}' chưa sync";

        IsOverdue = since >= Interval;
        IsLive = IsOverdue && DateTime.Now >= _snoozedUntil;

        if (IsOverdue && DateTime.Now >= _nextPeek)
        {
            Peek("", "Đến lúc Sync with Central", $"Đã {minutes} phút từ lần sync cuối", seconds: 6, playSound: true);
            _nextPeek = DateTime.Now + RepeatEvery;
        }
    }

    private static bool IsRevitOpen()
    {
        var processes = Process.GetProcessesByName("Revit");
        foreach (var p in processes) p.Dispose();
        return processes.Length > 0;
    }

    private void MarkSynced(DateTime when, bool fromJournal)
    {
        LastSync = when;
        _nextPeek = when + Interval;
        IsOverdue = IsLive = false;
        if (fromJournal) Log.Info($"Thấy Sync with Central trong journal lúc {when:HH:mm:ss}");
    }

    [RelayCommand]
    private void ConfirmSynced()
    {
        MarkSynced(DateTime.Now, fromJournal: false);
        Update();
    }

    [RelayCommand]
    private void Snooze()
    {
        _snoozedUntil = _nextPeek = DateTime.Now + SnoozeFor;
        IsLive = false;
    }

    public override void Dispose() => _poll.Stop();
}
