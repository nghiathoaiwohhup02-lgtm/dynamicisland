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
        var hidden = new HashSet<string>(settings.HiddenModules, StringComparer.OrdinalIgnoreCase);
        var all = new IIslandModule[]
        {
            new OverviewModule(),
            new EmailReportModule(),
            new MediaModule(),
            new PomodoroModule(),
            new SyncReminderModule(),
            new WaterModule(),
            new NotesModule(),
            new FilesModule(),
            new ClipboardModule(),
            new SystemStatusModule(),
            new QuickActionsModule(),
        };

        foreach (var module in all.Where(m => hidden.Contains(m.Id))) module.Dispose();
        return all.Where(m => !hidden.Contains(m.Id)).ToList();
    }
}
