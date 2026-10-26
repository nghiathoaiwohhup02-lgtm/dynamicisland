using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Pomodoro;

public enum PomodoroPhase
{
    Idle,
    Work,
    ShortBreak,
    LongBreak,
}

/// <summary>Hẹn giờ Pomodoro: làm 25 phút, nghỉ 5 phút, sau 4 vòng nghỉ dài (đổi được trong cấu hình).</summary>
public sealed partial class PomodoroModule : IslandModule
{
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private DateTime _endsAt;
    private TimeSpan _phaseLength;

    [ObservableProperty] private PomodoroPhase _phase = PomodoroPhase.Idle;
    [ObservableProperty] private TimeSpan _remaining;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private int _completedRounds;
    [ObservableProperty] private double _progress;

    public override string Id => "pomodoro";
    public override string Title => "Pomodoro";
    public override string Glyph => "\uE916";
    public override int LivePriority => 20;
    public override bool CanPinCompact => true;

    private AppSettings S => Context.Settings.Current;

    public string RemainingText => $"{(int)Remaining.TotalMinutes:00}:{Remaining.Seconds:00}";

    public string PhaseText => Phase switch
    {
        PomodoroPhase.Work => "Đang tập trung",
        PomodoroPhase.ShortBreak => "Nghỉ ngắn",
        PomodoroPhase.LongBreak => "Nghỉ dài",
        _ => "Sẵn sàng",
    };

    public string RoundText => $"Vòng {CompletedRounds % Math.Max(1, S.PomodoroRoundsBeforeLongBreak) + 1}/{S.PomodoroRoundsBeforeLongBreak}";

    public string StartPauseText => IsRunning ? "Tạm dừng" : Phase == PomodoroPhase.Idle ? "Bắt đầu" : "Tiếp tục";

    public bool IsBreak => Phase is PomodoroPhase.ShortBreak or PomodoroPhase.LongBreak;

    protected override Task OnInitializeAsync()
    {
        _tick.Tick += (_, _) => OnTick();
        Remaining = Length(PomodoroPhase.Work);
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new PomodoroView();

    protected override FrameworkElement CreateCompactView() => new PomodoroCompactView();

    partial void OnRemainingChanged(TimeSpan value) => OnPropertyChanged(nameof(RemainingText));

    partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(StartPauseText));

    partial void OnCompletedRoundsChanged(int value) => OnPropertyChanged(nameof(RoundText));

    partial void OnPhaseChanged(PomodoroPhase value)
    {
        OnPropertyChanged(nameof(PhaseText));
        OnPropertyChanged(nameof(StartPauseText));
        OnPropertyChanged(nameof(IsBreak));
        IsLive = value != PomodoroPhase.Idle;
    }

    private TimeSpan Length(PomodoroPhase phase) => TimeSpan.FromMinutes(Math.Max(1, phase switch
    {
        PomodoroPhase.ShortBreak => S.PomodoroShortBreakMinutes,
        PomodoroPhase.LongBreak => S.PomodoroLongBreakMinutes,
        _ => S.PomodoroWorkMinutes,
    }));

    [RelayCommand]
    private void StartPause()
    {
        if (IsRunning)
        {
            Remaining = _endsAt - DateTime.Now;
            IsRunning = false;
            _tick.Stop();
            return;
        }

        if (Phase == PomodoroPhase.Idle) BeginPhase(PomodoroPhase.Work, autoStart: true);
        else Resume();
    }

    [RelayCommand]
    private void Reset()
    {
        _tick.Stop();
        IsRunning = false;
        CompletedRounds = 0;
        Phase = PomodoroPhase.Idle;
        Remaining = Length(PomodoroPhase.Work);
        Progress = 0;
    }

    [RelayCommand]
    private void Skip()
    {
        if (Phase == PomodoroPhase.Idle) return;
        FinishPhase(announce: false);
    }

    private void BeginPhase(PomodoroPhase phase, bool autoStart)
    {
        Phase = phase;
        _phaseLength = Length(phase);
        Remaining = _phaseLength;
        Progress = 0;
        if (autoStart) Resume();
    }

    private void Resume()
    {
        _endsAt = DateTime.Now + Remaining;
        IsRunning = true;
        _tick.Start();
    }

    // Tính theo giờ kết thúc thay vì đếm tích tắc, nên máy ngủ hay giật vẫn đúng giờ.
    private void OnTick()
    {
        var left = _endsAt - DateTime.Now;
        if (left <= TimeSpan.Zero)
        {
            FinishPhase(announce: true);
            return;
        }
        Remaining = TimeSpan.FromSeconds(Math.Ceiling(left.TotalSeconds));
        Progress = _phaseLength.TotalSeconds > 0 ? 1 - left.TotalSeconds / _phaseLength.TotalSeconds : 0;
    }

    private void FinishPhase(bool announce)
    {
        _tick.Stop();
        IsRunning = false;

        if (Phase == PomodoroPhase.Work)
        {
            CompletedRounds++;
            var longBreak = CompletedRounds % Math.Max(1, S.PomodoroRoundsBeforeLongBreak) == 0;
            var next = longBreak ? PomodoroPhase.LongBreak : PomodoroPhase.ShortBreak;
            if (announce)
                Peek("\uE916", "Hết giờ tập trung", $"{(longBreak ? "Nghỉ dài" : "Nghỉ ngắn")} {Length(next).TotalMinutes:0} phút nhé", seconds: 6, playSound: true);
            // Giờ nghỉ tự chạy luôn.
            BeginPhase(next, autoStart: true);
        }
        else
        {
            if (announce)
                Peek("\uE916", "Hết giờ nghỉ", "Bấm Bắt đầu cho vòng tiếp theo", seconds: 6, playSound: true);
            // Vòng làm việc chờ người dùng bấm, tránh chạy khi đã rời bàn.
            BeginPhase(PomodoroPhase.Work, autoStart: false);
        }
    }

    public override void Dispose() => _tick.Stop();
}
