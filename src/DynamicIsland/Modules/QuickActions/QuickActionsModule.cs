using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using DynamicIsland.Core;
using DynamicIsland.Shell;

namespace DynamicIsland.Modules.QuickActions;

/// <summary>Lưới nút mở nhanh chương trình, thư mục, trang web. Danh sách nằm trong settings.json.</summary>
public sealed partial class QuickActionsModule : IslandModule
{
    public override string Id => "quick";
    public override string Title => "Nút nhanh";
    public override string Glyph => "";

    private const string EditTarget = "@edit";

    // Các nút người dùng cấu hình, cộng ô cuối "Sửa nút" để mở settings.json.
    public IReadOnlyList<QuickActionItem> Actions =>
    [
        .. Context.Settings.Current.QuickActions,
        new QuickActionItem { Name = "Sửa nút", Glyph = "\uE710", Target = EditTarget },
    ];

    protected override Task OnInitializeAsync()
    {
        OnPropertyChanged(nameof(Actions));
        return Task.CompletedTask;
    }

    protected override FrameworkElement CreateExpandedView() => new QuickActionsView();

    [RelayCommand]
    private void Run(QuickActionItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Target)) return;
        if (item.Target == EditTarget)
        {
            TrayIcon.OpenSettingsFile();
            return;
        }
        try
        {
            var start = new ProcessStartInfo(Environment.ExpandEnvironmentVariables(item.Target)) { UseShellExecute = true };
            if (!string.IsNullOrWhiteSpace(item.Arguments)) start.Arguments = Environment.ExpandEnvironmentVariables(item.Arguments);
            Process.Start(start);
        }
        catch (Exception ex)
        {
            Log.Error($"Không chạy được nút \"{item.Name}\"", ex);
            Peek("", $"Không mở được {item.Name}", item.Target, seconds: 4);
        }
    }
}
