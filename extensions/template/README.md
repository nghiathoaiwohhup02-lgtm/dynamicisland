# Bộ tool "__TOOLKIT_NAME__"

Thư mục này chứa các tool Revit của riêng bạn. Mỗi tool hiện thành một nút trên tab
**__TOOLKIT_NAME__** trong Revit.

## Làm một tool mới

1. Trong Revit: tab **WCP Extensions** → **Toolkits** → ở bộ tool này bấm **Copy prompt for Claude** → **New tool**.
2. Mở Claude Code ngay trong thư mục này, dán câu vừa chép, thay dòng
   `<describe the tool here…>` bằng mô tả tool bạn muốn (tiếng Việt được).
3. Claude viết mã và build. Khi Claude báo xong: trong Revit bấm **Reload**.
4. Nút mới hiện trên tab **__TOOLKIT_NAME__**. Không cần tắt Revit.

Muốn sửa tool: làm như trên, chọn **Change a tool**. Tool báo lỗi: chọn **Fix an error** rồi dán nội dung lỗi.

## Tự sửa mã

- Mỗi tool nằm trong `Tools\<Tên tool>\`. Hai tool mẫu: `Hello` (đơn giản nhất) và
  `SelectionInfo` (có cửa sổ).
- Sửa xong: trong Revit bấm **Build** trên tab **__TOOLKIT_NAME__** (hoặc nhấp đúp
  `Build.cmd`), rồi **Reload**.
- Đừng sửa các file của bộ khung: `_kit\`, `build.ps1`, `Build.cmd`, `Directory.Build.props`,
  `Directory.Build.targets`.

## Ghi chú

- Lần build đầu tải gói NuGet (và .NET SDK nếu máy chưa có, khoảng 250 MB), mất vài phút.
- Cửa sổ của tool đang mở vẫn chạy bản cũ tới khi đóng.
- Nút mới nằm cuối panel cho tới lần mở Revit sau; khi đó thứ tự theo `Order`.
- Xóa bộ tool: Toolkits → **Delete** (thư mục vào Thùng rác).
