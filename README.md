# Dynamic Island cho Windows 10

Thanh đen co giãn ở giữa mép trên màn hình, kiểu Dynamic Island của iPhone, gom các tiện ích dùng hằng ngày khi làm việc.

## Có gì

| Tab (Id) | Chức năng |
| --- | --- |
| Tổng quan (`overview`) | Đồng hồ, ngày, thời tiết hiện tại và 3 ngày tới (Open-Meteo, không cần API key). |
| Báo cáo email (`report`) | Khi file báo cáo buổi sáng của hôm nay (`bao-cao-yyyy-MM-dd.html`) xuất hiện trong thư mục `Bao-cao-email`, đảo báo; bấm vào là mở trên trình duyệt. |
| Nhạc (`media`) | Tên bài, ca sĩ, ảnh bìa, nút trước/phát/tiếp. Dùng được với Spotify, YouTube trên Edge/Chrome, VLC... |
| Pomodoro (`pomodoro`) | 25 phút tập trung, 5 phút nghỉ, sau 4 vòng nghỉ 15 phút. Có âm báo khi hết giờ. |
| Nhắc Sync (`sync`) | Khi Revit đang mở, cứ 30 phút chưa Sync with Central thì đảo báo và chuyển đỏ. Tự nhận biết lần sync bằng cách **chỉ đọc** file journal của Revit; không mở file .rvt, không cần add-in, không tác động gì tới Revit. |
| Uống nước (`water`) | Nhắc mỗi 60 phút trong giờ làm. Bấm "Đã uống" để điểm danh; thống kê hôm nay, 7 ngày, chuỗi ngày đạt mục tiêu. Dữ liệu ở `water.csv`. |
| Ghi chú (`notes`) | Ghi chú nhanh (tự lưu) và danh sách việc cần làm. Bấm vào ô chữ để gõ, Esc để đóng. |
| Khay file (`files`) | Kéo file từ Explorer vào đảo để giữ tạm, kéo ra để thả vào email/Teams/thư mục. Khay chỉ nhớ đường dẫn, không chép hay di chuyển file. Kèm trạng thái OneDrive: đang chạy hay tắt, và file xung đột. |
| Clipboard (`clipboard`) | 20 đoạn chữ copy gần nhất, bấm để chép lại. Chỉ giữ trong bộ nhớ, không ghi ra đĩa. |
| Pin và âm lượng (`system`) | Mức pin, thời gian còn lại, thanh kéo âm lượng, bật/tắt tiếng. |
| Nút nhanh (`quick`) | Chụp màn hình, máy tính, thư mục Tải về, Notepad, Task Manager, Cài đặt, khóa máy. Thêm/sửa được. |

Không dùng tab nào thì thêm Id của nó vào `HiddenModules` trong cấu hình.

Cách đảo hoạt động:

- **Dạng viên thuốc** như Dynamic Island của iPhone.
- **Kéo để dời chỗ**: nhấn giữ đảo rồi kéo tới bất cứ đâu, kể cả đặt lên taskbar. Đảo ở nửa dưới màn hình thì bảng mở rộng bung lên trên. Vị trí được nhớ cho lần sau; muốn về chỗ cũ thì chọn *Đưa đảo về giữa màn hình* ở khay hệ thống.
- **Cập nhật**: mở bản mới là bản cũ tự tắt; đảo báo "Đã cập nhật lên bản …".
- **Thu gọn**: hiện giờ và ngày. Khi đang phát nhạc hoặc đang chạy Pomodoro, đảo dài ra và hiện hoạt động đó (Pomodoro được ưu tiên).
- **Mở rộng**: rê chuột vào đảo (hoặc bấm). Bảng mở rộng gọn cỡ một cụm nút trên ribbon Revit. Rời chuột ra thì tự thu lại.
- **Báo tin**: đảo bung ra vài giây khi đổi bài, đổi âm lượng bằng phím, cắm/rút sạc, pin dưới 20% và 10%, hết giờ Pomodoro.
- Đảo không lấy focus khi bấm (đang gõ trong Revit vẫn giữ nguyên), không hiện trong Alt+Tab, và tự ẩn khi có ứng dụng toàn màn hình (trình chiếu PowerPoint, xem video).
- Biểu tượng ở khay hệ thống: ẩn/hiện đảo, đưa đảo về giữa, mở file cấu hình, khởi động lại, khởi động cùng Windows, thoát.

## Chạy thử

Cách nhanh nhất: vào tab **Actions** → lần build mới nhất → mục **Artifacts**, tải `DynamicIsland-<phiên bản>-win-x64` rồi chạy file `.exe` bên trong. File đã kèm .NET nên không cần cài gì thêm. Phiên bản đang chạy hiện ở dòng đầu menu khay hệ thống.

Phát hành bản mới: tăng `<Version>` trong `src/DynamicIsland/DynamicIsland.csproj`. Nếu tạo tag cùng số (vd. `v0.3.0`, qua trang Releases của GitHub), workflow tự gắn file `.exe` vào trang Release đó.

Tự build (cần [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) hoặc Visual Studio 2022):

```powershell
dotnet run --project src/DynamicIsland
```

Đóng gói một file `.exe`:

```powershell
dotnet publish src/DynamicIsland -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true -o publish
```

Yêu cầu Windows 10 bản 1809 trở lên (Windows 11 cũng chạy được).

## Cấu hình

File `%AppData%\DynamicIsland\settings.json` được tạo ở lần chạy đầu. Mở nhanh qua khay hệ thống → *Mở file cấu hình*, sửa xong chọn *Khởi động lại*.

```jsonc
{
  "City": "Hà Nội",                 // đổi tên thành phố là tự tra lại tọa độ
  "PomodoroWorkMinutes": 25,
  "PomodoroShortBreakMinutes": 5,
  "PomodoroLongBreakMinutes": 15,
  "PomodoroRoundsBeforeLongBreak": 4,
  "HiddenModules": [],               // vd. ["media", "system"]
  "EmailReportFolder": "%USERPROFILE%\\Bao-cao-email",
  "SyncReminderMinutes": 30,         // 0 = tắt nhắc Sync
  "WaterReminderMinutes": 60,        // 0 = tắt nhắc uống nước
  "WaterGoalPerDay": 8,
  "WorkStart": "08:00",
  "WorkEnd": "17:30",
  "HideWhenFullscreen": true,
  "QuickActions": [
    { "Name": "Dự án", "Glyph": "\uE8B7", "Target": "D:\\Projects" },
    { "Name": "Revit", "Glyph": "\uE7F4", "Target": "C:\\Program Files\\Autodesk\\Revit 2024\\Revit.exe" },
    { "Name": "Tải về", "Glyph": "\uE896", "Target": "explorer.exe", "Arguments": "shell:Downloads" }
  ]
}
```

`Target` có thể là chương trình, thư mục, trang web hoặc URI như `ms-settings:`. `Glyph` là ký tự của font [Segoe MDL2 Assets](https://learn.microsoft.com/windows/apps/design/style/segoe-ui-symbol-font).

Lỗi (nếu có) được ghi vào `%AppData%\DynamicIsland\log.txt`.

## Cấu trúc mã

```
src/DynamicIsland/
  App.xaml(.cs)          khởi động, chỉ cho chạy một bản, bắt lỗi chung
  Core/                  giao diện module, cấu hình, log
  Shell/                 cửa sổ đảo, hiệu ứng co giãn, khay hệ thống, phát hiện toàn màn hình
  Modules/
    Overview/            đồng hồ + thời tiết
    Media/               điều khiển nhạc
    Pomodoro/
    SystemStatus/        pin + âm lượng
    QuickActions/        nút nhanh
    SyncReminder/        nhắc Sync with Central (đọc journal Revit)
    Water/               nhắc uống nước + thống kê
    EmailReport/         báo cáo email buổi sáng
    Files/               khay file + OneDrive
    Clipboard/           lịch sử clipboard
    Notes/               ghi chú + việc cần làm
```

Mỗi tiện ích là một module độc lập. Hướng dẫn thêm module mới ở [docs/them-module.md](docs/them-module.md).

## Lộ trình

- **0.1**: thanh co giãn, đồng hồ/thời tiết, nhạc, pin/âm lượng, Pomodoro, nút nhanh.
- **0.2**: báo cáo email buổi sáng (phần Outlook/Teams đã bỏ theo yêu cầu).
- **0.3**: khay file, OneDrive, lịch sử clipboard, ghi chú, việc cần làm.
- Thêm: nhắc Sync with Central, nhắc uống nước.
