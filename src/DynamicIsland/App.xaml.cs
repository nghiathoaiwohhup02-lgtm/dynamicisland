using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using DynamicIsland.Core;
using DynamicIsland.Shell;

namespace DynamicIsland;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private IslandViewModel? _viewModel;
    private IslandWindow? _window;
    private TrayIcon? _tray;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Mở bản mới khi bản cũ đang chạy: tắt bản cũ rồi chạy tiếp, không phải vào Task Manager.
        if (!TakeOverFromRunningInstance())
        {
            Log.Error("Không tắt được bản Dynamic Island đang chạy");
            Shutdown();
            return;
        }
        ListenForQuitRequest();

        DispatcherUnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Tác vụ nền lỗi", args.Exception);
            args.SetObserved();
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd($"DynamicIsland/{TrayIcon.Version}");

        var settings = SettingsStore.Load();
        var context = new IslandContext(settings, _http, Dispatcher);

        // Khởi tạo module trước khi hiện cửa sổ để giao diện không gắn vào module chưa sẵn sàng.
        _viewModel = new IslandViewModel(ModuleCatalog.Create(settings.Current), settings);
        await _viewModel.InitializeAsync(context);

        _window = new IslandWindow(_viewModel, settings);
        _window.Show();
        _tray = new TrayIcon(_window.ToggleUserVisibility, _window.ResetPosition, Restart, Shutdown);
        AnnounceStart(settings);
        Log.Info($"Đã khởi động bản {TrayIcon.Version}");
    }

    // Báo trên đảo (và ở khay hệ thống) rằng đã cài/cập nhật thành công, kèm số phiên bản.
    private void AnnounceStart(SettingsStore settings)
    {
        var version = TrayIcon.Version;
        var previous = settings.Current.LastRunVersion;
        var (title, message) = previous is null
            ? ("Đã cài đặt thành công", $"Dynamic Island {version} đang chạy")
            : previous != version
                ? ($"Đã cập nhật lên bản {version}", $"Từ bản {previous}")
                : ("Dynamic Island đã khởi động", $"Bản {version}");

        if (previous != version)
        {
            settings.Current.LastRunVersion = version;
            settings.Save();
            _tray?.ShowBalloon(title, message);
        }
        _viewModel?.Announce(new PeekRequest("\uE930", title, message, TimeSpan.FromSeconds(5)));
    }

    private const string MutexName = @"Local\DynamicIsland.SingleInstance";
    private const string QuitEventName = @"Local\DynamicIsland.Quit";

    private bool TakeOverFromRunningInstance()
    {
        _singleInstance = new Mutex(false, MutexName);
        if (TryAcquire(TimeSpan.Zero)) return true;

        // Bản mới (từ 0.4) nghe tín hiệu này và tự thoát êm.
        using (var quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName))
            quit.Set();
        if (TryAcquire(TimeSpan.FromSeconds(3))) return true;

        // Bản cũ không nghe tín hiệu: buộc tắt tiến trình Dynamic Island khác đang chạy.
        var self = Environment.ProcessId;
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (process.Id == self || !process.ProcessName.StartsWith("DynamicIsland", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch (Exception ex)
                {
                    Log.Error($"Không tắt được tiến trình {process.ProcessName}", ex);
                }
            }
        }
        return TryAcquire(TimeSpan.FromSeconds(3));
    }

    private bool TryAcquire(TimeSpan timeout)
    {
        try
        {
            return _singleInstance!.WaitOne(timeout);
        }
        catch (AbandonedMutexException)
        {
            // Bản cũ bị tắt ngang vẫn giữ mutex: coi như đã lấy được.
            return true;
        }
    }

    private void ListenForQuitRequest()
    {
        var quit = new EventWaitHandle(false, EventResetMode.AutoReset, QuitEventName);
        // Bỏ tín hiệu còn sót lại (do chính lần khởi động này gửi cho bản cũ không nghe).
        quit.Reset();
        var thread = new Thread(() =>
        {
            quit.WaitOne();
            Dispatcher.BeginInvoke(Shutdown);
        })
        { IsBackground = true, Name = "DynamicIsland.Quit" };
        thread.Start();
    }

    private void Restart()
    {
        var path = Environment.ProcessPath;
        ReleaseSingleInstance();
        if (path is not null) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        Shutdown();
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Lỗi chưa xử lý", e.Exception);
        e.Handled = true;
    }

    private void ReleaseSingleInstance()
    {
        if (_singleInstance is null) return;
        try { _singleInstance.ReleaseMutex(); } catch (ApplicationException) { }
        _singleInstance.Dispose();
        _singleInstance = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _viewModel?.Dispose();
        _http.Dispose();
        ReleaseSingleInstance();
        base.OnExit(e);
    }
}
