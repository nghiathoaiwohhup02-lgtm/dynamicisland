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

    /// <summary>Ký tự Segoe MDL2 Assets, vd. "".</summary>
    public string Glyph { get; set; } = "";

    /// <summary>Đường dẫn chương trình, thư mục, URL hoặc URI (ms-settings:, ms-screenclip:...).</summary>
    public string Target { get; set; } = "";

    public string? Arguments { get; set; }

    public static List<QuickActionItem> Defaults() =>
    [
        new() { Name = "Chụp màn hình", Glyph = "", Target = "ms-screenclip:" },
        new() { Name = "Máy tính", Glyph = "", Target = "calc.exe" },
        new() { Name = "Tải về", Glyph = "", Target = "explorer.exe", Arguments = "shell:Downloads" },
        new() { Name = "Notepad", Glyph = "", Target = "notepad.exe" },
        new() { Name = "Task Manager", Glyph = "", Target = "taskmgr.exe" },
        new() { Name = "Cài đặt", Glyph = "", Target = "ms-settings:" },
        new() { Name = "Khóa máy", Glyph = "", Target = "rundll32.exe", Arguments = "user32.dll,LockWorkStation" },
    ];
}
