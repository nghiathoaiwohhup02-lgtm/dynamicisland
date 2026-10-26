using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Outlook;

public sealed record MailRow(MailInfo Mail, string Line, string Time);

/// <summary>Cuộc họp kế tiếp (nhắc trước vài phút, nút vào Teams) và thư chưa đọc từ Outlook.</summary>
public sealed partial class OutlookModule : IslandModule
{
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly DispatcherTimer _tick = new() { Interval = TimeSpan.FromSeconds(15) };
    private readonly HashSet<string> _remindedMeetings = [];
    private int _lastUnread = -1;
    private bool _busy;

    [ObservableProperty] private MeetingInfo? _nextMeeting;
    [ObservableProperty] private string _meetingTitle = "Không còn cuộc họp hôm nay";
    [ObservableProperty] private string _meetingTime = "";
    [ObservableProperty] private string _meetingCountdown = "";
    [ObservableProperty] private bool _hasTeamsLink;
    [ObservableProperty] private int _unreadCount;
    [ObservableProperty] private string _status = "";

    public override string Id => "outlook";
    public override string Title => "Outlook";
    public override string Glyph => "\uE715";
    public override int LivePriority => 45;

    public ObservableCollection<MailRow> Mails { get; } = [];

    protected override Task OnInitializeAsync()
    {
        _poll.Tick += async (_, _) => await RefreshAsync();
        _tick.Tick += (_, _) => UpdateCountdown();
        _poll.Start();
        _tick.Start();
        _ = RefreshAsync();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new OutlookView();

    protected override FrameworkElement CreateCompactView() => new OutlookCompactView();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_busy) return;
        if (!OutlookReader.IsOutlookRunning())
        {
            Status = "Mở Outlook để xem lịch họp và thư";
            NextMeeting = null;
            UpdateCountdown();
            return;
        }

        _busy = true;
        try
        {
            var snapshot = await StaTask.Run(() => OutlookReader.Read(mailCount: 3));
            Status = "";
            var now = DateTime.Now;
            NextMeeting = snapshot.Meetings.FirstOrDefault(m => m.End > now);

            if (_lastUnread >= 0 && snapshot.UnreadCount > _lastUnread && snapshot.Unread.FirstOrDefault() is MailInfo newest)
                Peek("\uE715", newest.Sender, newest.Subject, seconds: 4, activate: () => Display(newest.EntryId));
            _lastUnread = UnreadCount = snapshot.UnreadCount;

            Mails.Clear();
            foreach (var mail in snapshot.Unread)
                Mails.Add(new MailRow(mail, $"{mail.Sender}: {mail.Subject}", mail.Received.Date == now.Date ? mail.Received.ToString("HH:mm") : mail.Received.ToString("dd/MM")));

            UpdateCountdown();
        }
        catch (Exception ex)
        {
            Log.Error("Đọc Outlook lỗi", ex);
            Status = "Không đọc được Outlook (chỉ hỗ trợ Outlook bản cổ điển)";
        }
        finally
        {
            _busy = false;
        }
    }

    private void UpdateCountdown()
    {
        var m = NextMeeting;
        if (m is null)
        {
            MeetingTitle = "Không còn cuộc họp hôm nay";
            MeetingTime = MeetingCountdown = "";
            HasTeamsLink = false;
            IsLive = false;
            return;
        }

        var now = DateTime.Now;
        MeetingTitle = m.Subject;
        MeetingTime = $"{m.Start:HH:mm}–{m.End:HH:mm}" + (string.IsNullOrWhiteSpace(m.Location) ? "" : $" · {m.Location}");
        HasTeamsLink = m.TeamsUrl is not null;

        var until = m.Start - now;
        MeetingCountdown = now >= m.Start ? "Đang diễn ra" : until.TotalMinutes < 60 ? $"Còn {Math.Ceiling(until.TotalMinutes)} phút" : $"Lúc {m.Start:HH:mm}";

        // Hiện ở dạng thu gọn từ 15 phút trước giờ họp tới khi họp xong.
        IsLive = until <= TimeSpan.FromMinutes(15) && now < m.End;

        var remind = TimeSpan.FromMinutes(Context.Settings.Current.MeetingReminderMinutes);
        if (until <= remind && until > TimeSpan.FromMinutes(-2) && _remindedMeetings.Add($"{m.EntryId}|{m.Start:O}"))
        {
            Peek("\uE787", m.Subject, $"{MeetingCountdown} · {(HasTeamsLink ? "bấm để vào Teams" : "bấm để mở")}",
                 seconds: 15, playSound: true, activate: Join);
        }
    }

    [RelayCommand]
    private void Join()
    {
        if (NextMeeting is not { } m) return;
        if (m.TeamsUrl is not null)
        {
            // msteams: mở thẳng ứng dụng Teams thay vì trình duyệt.
            var url = m.TeamsUrl.Replace("https://", "msteams://", StringComparison.OrdinalIgnoreCase);
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch { Process.Start(new ProcessStartInfo(m.TeamsUrl) { UseShellExecute = true }); }
        }
        else
        {
            Display(m.EntryId);
        }
    }

    [RelayCommand]
    private void OpenMeeting()
    {
        if (NextMeeting is { } m) Display(m.EntryId);
    }

    [RelayCommand]
    private void OpenMail(MailRow? row)
    {
        if (row is not null) Display(row.Mail.EntryId);
    }

    [RelayCommand]
    private void OpenInbox()
    {
        try { Process.Start(new ProcessStartInfo("outlook.exe", "/select outlook:Inbox") { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error("Không mở được Outlook", ex); }
    }

    private static void Display(string entryId) =>
        StaTask.Run(() => OutlookReader.Display(entryId))
               .ContinueWith(t => Log.Error("Không mở được mục Outlook", t.Exception), TaskContinuationOptions.OnlyOnFaulted);

    public override void Dispose()
    {
        _poll.Stop();
        _tick.Stop();
    }
}
