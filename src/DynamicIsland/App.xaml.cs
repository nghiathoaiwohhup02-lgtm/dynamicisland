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

        _singleInstance = new Mutex(true, @"Local\DynamicIsland.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Tác vụ nền lỗi", args.Exception);
            args.SetObserved();
        };

        _http.DefaultRequestHeaders.UserAgent.ParseAdd("DynamicIsland/0.1");

        var settings = SettingsStore.Load();
        var context = new IslandContext(settings, _http, Dispatcher);

        // Khởi tạo module trước khi hiện cửa sổ để giao diện không gắn vào module chưa sẵn sàng.
        _viewModel = new IslandViewModel(ModuleCatalog.CreateDefault());
        await _viewModel.InitializeAsync(context);

        _window = new IslandWindow(_viewModel, settings);
        _window.Show();
        _tray = new TrayIcon(_window.ToggleUserVisibility, _window.ResetPosition, Restart, Shutdown);
        Log.Info("Đã khởi động");
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
