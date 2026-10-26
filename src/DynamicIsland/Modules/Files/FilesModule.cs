using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;

namespace DynamicIsland.Modules.Files;

public sealed record TrayItem(string Path, string Name, string Glyph);

/// <summary>
/// Khay file: kéo file vào đảo để giữ tạm, kéo ra để thả vào email, Teams, thư mục khác...
/// Khay chỉ nhớ đường dẫn, không sao chép hay di chuyển file. Kèm trạng thái OneDrive.
/// </summary>
public sealed partial class FilesModule : IslandModule, IFileDropTarget
{
    private static readonly string TrayFile = System.IO.Path.Combine(SettingsStore.Folder, "tray.json");
    private readonly DispatcherTimer _oneDrivePoll = new() { Interval = TimeSpan.FromMinutes(15) };
    private List<string> _conflicts = [];

    [ObservableProperty] private string _oneDriveText = "Đang kiểm tra OneDrive...";
    [ObservableProperty] private bool _oneDriveOk;
    [ObservableProperty] private bool _hasConflicts;

    public override string Id => "files";
    public override string Title => "Khay file và OneDrive";
    public override string Glyph => "";

    public ObservableCollection<TrayItem> Items { get; } = [];

    protected override Task OnInitializeAsync()
    {
        LoadTray();
        _oneDrivePoll.Tick += async (_, _) => await CheckOneDriveAsync();
        _oneDrivePoll.Start();
        _ = CheckOneDriveAsync();
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new FilesView();

    public void AcceptFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            Items.Add(Create(path));
        }
        SaveTray();
    }

    private static TrayItem Create(string path) =>
        new(path, System.IO.Path.GetFileName(path.TrimEnd('\\')) is { Length: > 0 } name ? name : path,
            Directory.Exists(path) ? "" : "");

    [RelayCommand]
    private void Open(TrayItem? item)
    {
        if (item is null) return;
        try { Process.Start(new ProcessStartInfo(item.Path) { UseShellExecute = true }); }
        catch (Exception ex) { Log.Error($"Không mở được {item.Path}", ex); }
    }

    [RelayCommand]
    private void Remove(TrayItem? item)
    {
        if (item is null) return;
        Items.Remove(item);
        SaveTray();
    }

    [RelayCommand]
    private void Clear()
    {
        Items.Clear();
        SaveTray();
    }

    [RelayCommand]
    private void OpenOneDrive()
    {
        try
        {
            // Có xung đột thì mở thư mục chứa file xung đột đầu tiên và chọn sẵn file đó.
            if (_conflicts.FirstOrDefault() is string conflict)
                Process.Start("explorer.exe", $"/select,\"{conflict}\"");
            else if (OneDriveStatus.Accounts().FirstOrDefault() is { } account)
                Process.Start(new ProcessStartInfo(account.Folder) { UseShellExecute = true });
        }
        catch (Exception ex) { Log.Error("Không mở được OneDrive", ex); }
    }

    private async Task CheckOneDriveAsync()
    {
        try
        {
            var running = OneDriveStatus.IsRunning();
            var accounts = OneDriveStatus.Accounts();
            if (accounts.Count == 0)
            {
                OneDriveText = "Máy chưa đăng nhập OneDrive";
                OneDriveOk = HasConflicts = false;
                return;
            }

            var conflicts = await Task.Run(() => OneDriveStatus.FindConflicts(accounts.Select(a => a.Folder)));
            var isNew = conflicts.Count > _conflicts.Count;
            _conflicts = conflicts;
            HasConflicts = conflicts.Count > 0;
            OneDriveOk = running && !HasConflicts;

            var names = string.Join(", ", accounts.Select(a => a.Name));
            OneDriveText = !running ? $"OneDrive đang tắt, file không được đồng bộ ({names})"
                : HasConflicts ? $"{conflicts.Count} file xung đột cần xử lý · bấm để xem"
                : $"OneDrive đang chạy · {names}";

            if (!running)
                Peek("", "OneDrive đang tắt", "File sẽ không được đồng bộ", seconds: 5);
            else if (isNew)
                Peek("", "OneDrive có file xung đột", System.IO.Path.GetFileName(conflicts[0]), seconds: 6, activate: OpenOneDrive);
        }
        catch (Exception ex)
        {
            Log.Error("Kiểm tra OneDrive lỗi", ex);
            OneDriveText = "Không kiểm tra được OneDrive";
        }
    }

    private void LoadTray()
    {
        try
        {
            if (!File.Exists(TrayFile)) return;
            foreach (var path in JsonSerializer.Deserialize<List<string>>(File.ReadAllText(TrayFile)) ?? [])
                if (File.Exists(path) || Directory.Exists(path)) Items.Add(Create(path));
        }
        catch (Exception ex) { Log.Error("Đọc khay file lỗi", ex); }
    }

    private void SaveTray()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.Folder);
            File.WriteAllText(TrayFile, JsonSerializer.Serialize(Items.Select(i => i.Path).ToList()));
        }
        catch (Exception ex) { Log.Error("Lưu khay file lỗi", ex); }
    }

    public override void Dispose() => _oneDrivePoll.Stop();
}
