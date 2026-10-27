using DynamicIsland.Modules.Clipboard;
using DynamicIsland.Modules.EmailReport;
using DynamicIsland.Modules.Files;
using DynamicIsland.Modules.Media;
using DynamicIsland.Modules.Notes;
using DynamicIsland.Modules.Overview;
using DynamicIsland.Modules.Pomodoro;
using DynamicIsland.Modules.QuickActions;
using DynamicIsland.Modules.SyncReminder;
using DynamicIsland.Modules.SystemStatus;
using DynamicIsland.Modules.Vitamin;
using DynamicIsland.Modules.Water;

namespace DynamicIsland.Core;

public static class ModuleCatalog
{
    /// <summary>
    /// Các module theo thứ tự tab, trừ những module có Id nằm trong HiddenModules của cấu hình.
    /// Thêm module mới tại đây.
    /// </summary>
    public static IReadOnlyList<IIslandModule> Create(AppSettings settings)
    {
        // Tab bị ẩn thì không tạo module luôn: không chạy hẹn giờ, không đọc file, không tốn bộ nhớ.
        var hidden = new HashSet<string>(settings.HiddenModules, StringComparer.OrdinalIgnoreCase);
        return All.Where(entry => !hidden.Contains(entry.Id)).Select(entry => entry.Create()).ToList();
    }

    private static readonly (string Id, Func<IIslandModule> Create)[] All =
    [
        ("overview", () => new OverviewModule()),
        ("report", () => new EmailReportModule()),
        ("media", () => new MediaModule()),
        ("pomodoro", () => new PomodoroModule()),
        ("sync", () => new SyncReminderModule()),
        ("water", () => new WaterModule()),
        ("vitamin", () => new VitaminModule()),
        ("notes", () => new NotesModule()),
        ("files", () => new FilesModule()),
        ("clipboard", () => new ClipboardModule()),
        ("system", () => new SystemStatusModule()),
        ("quick", () => new QuickActionsModule()),
    ];
}
