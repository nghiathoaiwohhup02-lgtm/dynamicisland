using DynamicIsland.Modules.Media;
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
    /// Danh sách module theo thứ tự tab. Thêm module mới (vd. Outlook/Teams ở bản 0.2) tại đây.
    /// </summary>
    public static IReadOnlyList<IIslandModule> CreateDefault() =>
    [
        new OverviewModule(),
        new MediaModule(),
        new PomodoroModule(),
        new SyncReminderModule(),
        new WaterModule(),
        new SystemStatusModule(),
        new QuickActionsModule(),
    ];
}
