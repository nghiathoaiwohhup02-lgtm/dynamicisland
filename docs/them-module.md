# Thêm một module mới

Ví dụ dưới đây là khung cho module Outlook/Teams của bản 0.2.

## 1. Lớp module

Tạo `src/DynamicIsland/Modules/Meetings/MeetingsModule.cs`, kế thừa `IslandModule`:

```csharp
public sealed partial class MeetingsModule : IslandModule
{
    [ObservableProperty] private string _nextMeeting = "";

    public override string Id => "meetings";
    public override string Title => "Lịch họp";
    public override string Glyph => "\uE787";      // Segoe MDL2: Calendar
    public override int LivePriority => 30;         // cao hơn Pomodoro (20) và nhạc (10)

    protected override async Task OnInitializeAsync()
    {
        // Context.Settings, Context.Http, Context.Dispatcher dùng chung cho mọi module.
        // Lấy lịch, đặt hẹn giờ kiểm tra định kỳ...
    }

    protected override FrameworkElement CreateExpandedView() => new MeetingsView();

    // Chỉ cần nếu module có "hoạt động" hiện ở dạng thu gọn (vd. họp sắp bắt đầu).
    protected override FrameworkElement? CreateCompactView() => new MeetingsCompactView();
}
```

Những gì lớp cơ sở lo sẵn:

- `IsLive = true` → đảo thu gọn hiện `CompactView` của module (module có `LivePriority` cao nhất thắng).
- `Peek(glyph, title, message, seconds, playSound)` → đảo bung ra vài giây để báo tin, vd. "Họp sau 5 phút".
- `OnUi(...)` → đưa việc về luồng giao diện khi sự kiện đến từ luồng khác.
- Giao diện được tạo một lần, `DataContext` là chính module nên XAML bind thẳng vào thuộc tính.
- Lỗi khi khởi tạo được ghi log và không làm hỏng các module khác.

## 2. Giao diện

Tạo `MeetingsView.xaml` (UserControl). Dùng các style có sẵn trong `Shell/Theme.xaml`: `Text`, `DimText`, `Glyph`, `IconButton`, `BigIconButton`, `PillButton`, `TileButton`, `ThinSlider`, `ThinProgress`.

## 3. Đăng ký

Thêm vào `Core/ModuleCatalog.cs`:

```csharp
new MeetingsModule(),
```

Thứ tự trong danh sách là thứ tự tab.

## 4. Cấu hình (nếu cần)

Thêm thuộc tính vào `Core/AppSettings.cs`; giá trị mặc định tự được ghi vào `settings.json` ở lần chạy sau.
