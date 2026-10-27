namespace DynamicIsland.Core;

/// <summary>Cấu hình lưu ở %AppData%\DynamicIsland\settings.json, sửa bằng tay được.</summary>
public sealed class AppSettings
{
    /// <summary>Tên thành phố cho thời tiết. Đổi tên là ứng dụng tự tra lại tọa độ.</summary>
    public string City { get; set; } = "Hà Nội";

    // Tọa độ đã tra cho City; ResolvedCity khác City thì tra lại.
    public string? ResolvedCity { get; set; } = "Hà Nội";
    public double Latitude { get; set; } = 21.0285;
    public double Longitude { get; set; } = 105.8542;

    public int PomodoroWorkMinutes { get; set; } = 25;
    public int PomodoroShortBreakMinutes { get; set; } = 5;
    public int PomodoroLongBreakMinutes { get; set; } = 15;
    public int PomodoroRoundsBeforeLongBreak { get; set; } = 4;

    /// <summary>Phiên bản chạy lần trước, để báo "đã cập nhật" khi mở bản mới.</summary>
    public string? LastRunVersion { get; set; }

    /// <summary>Id các tab muốn ẩn, vd. ["media", "water"]. Xem Id trong README.</summary>
    public List<string> HiddenModules { get; set; } = [];

    /// <summary>Tab đã chọn lần cuối có giao diện thu gọn (vd. "water"): thu gọn vẫn hiện tab đó.</summary>
    public string? PinnedModule { get; set; }

    /// <summary>Thư mục chứa báo cáo email buổi sáng (bao-cao-yyyy-MM-dd.html).</summary>
    public string EmailReportFolder { get; set; } = @"%USERPROFILE%\Bao-cao-email";
    public string EmailReportFilePattern { get; set; } = "bao-cao-{0:yyyy-MM-dd}.html";

    /// <summary>Ngày đã mở báo cáo, để không nhắc lại sau khi khởi động lại.</summary>
    public DateTime? EmailReportOpenedOn { get; set; }

    /// <summary>Nhắc Sync with Central khi Revit đang mở. 0 = tắt.</summary>
    public int SyncReminderMinutes { get; set; } = 30;

    /// <summary>Nhắc uống nước trong giờ làm. 0 = tắt.</summary>
    public int WaterReminderMinutes { get; set; } = 60;
    public int WaterGoalPerDay { get; set; } = 8;
    public string WorkStart { get; set; } = "08:00";
    public string WorkEnd { get; set; } = "17:30";

    /// <summary>Giờ nhắc uống vitamin mỗi sáng (HH:mm). Để trống = tắt.</summary>
    public string VitaminTime { get; set; } = "08:00";

    /// <summary>
    /// Vị trí đảo do người dùng kéo tới (đơn vị DIP, tọa độ màn hình): IslandX là tâm theo chiều ngang,
    /// IslandY là một điểm trên màn hình chứa đảo. Bỏ trống thì đảo nằm giữa màn hình chính.
    /// </summary>
    public double? IslandX { get; set; }
    public double? IslandY { get; set; }

    /// <summary>Ẩn đảo khi có ứng dụng toàn màn hình (trình chiếu, xem video).</summary>
    public bool HideWhenFullscreen { get; set; } = true;

    public List<QuickActionItem> QuickActions { get; set; } = QuickActionItem.Defaults();
}

public sealed class QuickActionItem
{
    public string Name { get; set; } = "";

    /// <summary>Ký tự Segoe MDL2 Assets, vd. "\uE8EF".</summary>
    public string Glyph { get; set; } = "\uE8A7";

    /// <summary>Đường dẫn chương trình, thư mục, URL hoặc URI (ms-settings:, ms-screenclip:...).</summary>
    public string Target { get; set; } = "";

    public string? Arguments { get; set; }

    public static List<QuickActionItem> Defaults() =>
    [
        new() { Name = "Chụp màn hình", Glyph = "\uE722", Target = "ms-screenclip:" },
        new() { Name = "Máy tính", Glyph = "\uE8EF", Target = "calc.exe" },
        new() { Name = "Tải về", Glyph = "\uE896", Target = "explorer.exe", Arguments = "shell:Downloads" },
        new() { Name = "Notepad", Glyph = "\uE70F", Target = "notepad.exe" },
        new() { Name = "Task Manager", Glyph = "\uE9D9", Target = "taskmgr.exe" },
        new() { Name = "Cài đặt", Glyph = "\uE713", Target = "ms-settings:" },
        new() { Name = "Khóa máy", Glyph = "\uE72E", Target = "rundll32.exe", Arguments = "user32.dll,LockWorkStation" },
    ];
}
