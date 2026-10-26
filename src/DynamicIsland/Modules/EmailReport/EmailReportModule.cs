using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.EmailReport;

public sealed record ReportFile(string Label, string Path);

/// <summary>
/// Báo cáo email buổi sáng do tác vụ lịch của Claude tạo ra (file HTML mỗi ngày).
/// Khi file của hôm nay xuất hiện thì báo; bấm vào là mở trên trình duyệt. Chỉ đọc thư mục.
/// </summary>
public sealed partial class EmailReportModule : IslandModule
{
    private readonly DispatcherTimer _poll = new() { Interval = TimeSpan.FromMinutes(1) };
    private string? _announcedPath;

    [ObservableProperty] private string _status = "Đang tìm báo cáo...";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private string? _todayPath;

    public override string Id => "report";
    public override string Title => "Báo cáo email";
    public override string Glyph => "\uE8A5";
    public override int LivePriority => 35;

    public ObservableCollection<ReportFile> Recent { get; } = [];

    private string Folder => Environment.ExpandEnvironmentVariables(Context.Settings.Current.EmailReportFolder);

    protected override Task OnInitializeAsync()
    {
        _poll.Tick += (_, _) => Update();
        _poll.Start();
        Update();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new EmailReportView();

    protected override FrameworkElement CreateCompactView() => new EmailReportCompactView();

    private string PathFor(DateTime day) => Path.Combine(Folder, string.Format(Context.Settings.Current.EmailReportFilePattern, day));

    private void Update()
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                Status = "Chưa thấy thư mục báo cáo";
                Detail = Folder;
                TodayPath = null;
                IsLive = false;
                return;
            }

            var today = PathFor(DateTime.Today);
            TodayPath = File.Exists(today) ? today : null;
            RefreshRecent();

            if (TodayPath is null)
            {
                Status = "Chưa có báo cáo hôm nay";
                Detail = "Báo cáo thường có lúc khoảng 8:00 (Thứ 2 đến Thứ 7)";
                IsLive = false;
                return;
            }

            var opened = Context.Settings.Current.EmailReportOpenedOn == DateTime.Today;
            Status = opened ? "Đã xem báo cáo hôm nay" : "Có báo cáo email hôm nay";
            Detail = $"Tạo lúc {File.GetLastWriteTime(TodayPath):HH:mm} · bấm để mở";
            IsLive = !opened;

            if (!opened && _announcedPath != TodayPath)
            {
                _announcedPath = TodayPath;
                Peek("\uE8A5", "Báo cáo email hôm nay đã có", "Bấm vào đây để xem", seconds: 10, playSound: true, activate: OpenToday);
            }
        }
        catch (Exception ex)
        {
            Log.Error("Đọc thư mục báo cáo lỗi", ex);
        }
    }

    private void RefreshRecent()
    {
        Recent.Clear();
        foreach (var file in new DirectoryInfo(Folder).EnumerateFiles("*.html")
                     .OrderByDescending(f => f.LastWriteTime).Take(3))
            Recent.Add(new ReportFile($"{file.LastWriteTime:ddd dd/MM}", file.FullName));
    }

    [RelayCommand]
    private void OpenToday()
    {
        if (TodayPath is null) return;
        Open(TodayPath);
        Context.Settings.Current.EmailReportOpenedOn = DateTime.Today;
        Context.Settings.Save();
        Update();
    }

    [RelayCommand]
    private void OpenFile(ReportFile? file)
    {
        if (file is not null) Open(file.Path);
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (Directory.Exists(Folder)) Open(Folder);
    }

    private void Open(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error($"Không mở được {path}", ex); }
    }

    public override void Dispose() => _poll.Stop();
}
