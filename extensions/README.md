# WCP Extensions

Bản rút gọn của WCP Tools, **chỉ có tính năng Extensions**: người dùng tự làm tool Revit (nhờ Claude viết mã),
build xong bấm **Reload** trong Revit là có nút mới, không cần tắt Revit. Dùng để học cách làm tool.

- Độc lập hoàn toàn với WCP Tools: không cần OneDrive, ổ mạng hay Loader của WCP.
- Revit **2024, 2025, 2026, 2027**. Cài cho riêng người dùng, **không cần quyền admin**.
- Máy đã có WCP Tools đầy đủ (vốn đã có Extensions) thì WCP Extensions tự ẩn, trừ khi lúc cài
  tick ô *Show WCP Extensions alongside WCP Tools*.

## Lấy file cài đặt

GitHub → tab **Actions** → workflow **extensions** → lần chạy mới nhất → mục **Artifacts** →
tải `WCP-Extensions-Setup-<phiên bản>` (bên trong là `WCP Extensions Setup <phiên bản>.exe`).

Muốn có trang tải cố định: đẩy tag `ext-v<phiên bản>` (ví dụ `ext-v0.1.0`), workflow tự tạo trang Release kèm file.

Artifact `ui-pictures` là ảnh chụp các cửa sổ (Toolkits, hộp thoại, icon ribbon, Setup), chụp tự động ngoài Revit.

## Cài

1. Đóng Revit, chạy `WCP Extensions Setup <phiên bản>.exe` (Windows hỏi "Windows protected your PC" → *More info* → *Run anyway*).
2. Chọn các bản Revit (máy có bản nào thì tự tick bản đó) → **Install**.
3. Mở Revit → lần đầu Revit hỏi về add-in lạ → **Always Load**.

Gỡ: *Settings → Apps* của Windows → *WCP Extensions* → Uninstall (hoặc chạy lại Setup → **Uninstall**).
Bộ tool của người dùng được giữ lại.

Dòng lệnh: `/quiet` (cài im lặng cho mọi bản Revit tìm thấy), `/uninstall [/quiet]`.

## Dùng lần đầu

1. Revit → tab **WCP Extensions** → **Toolkits**.
2. Gõ tên bộ tool (ví dụ `Thoai Tools`) → **Create**. Bộ tool được tạo trong `%USERPROFILE%\WCP-Extensions\ThoaiTools\`
   và build ngay (lần đầu tải gói NuGet, và .NET SDK nếu máy chưa có — khoảng 250 MB, vài phút).
3. Xong: tab mới **Thoai Tools** có hai tool mẫu **Hello** và **Selection Info**.

## Làm tool với Claude

1. Toolkits → ở bộ tool bấm **Copy prompt for Claude** → **New tool** (hoặc *Change a tool*, *Fix an error*).
2. **Open folder** → mở Claude Code trong thư mục đó → dán → thay phần `<…>` bằng mô tả tool (tiếng Việt được).
3. Claude đọc `CLAUDE.md` của bộ tool, viết mã, build. Khi Claude báo xong → bấm **Reload** trong Revit.

Có bản build mới chưa nạp thì nút **Reload** hiện chấm đỏ. Tool báo lỗi thì hộp lỗi có nút **Copy for Claude**
(chép sẵn lỗi + đường dẫn bộ tool). Build lỗi thì *Fix an error* tự kèm các dòng lỗi.

Tự sửa mã cũng được: sửa trong `Tools\<Tên tool>\`, bấm **Build** trên tab của bộ tool (build rồi tự Reload).

## Cách Reload chạy được mà không tắt Revit

- Mỗi lần build, assembly mang **tên mới** (`ThoaiTools_b<yyMMddHHmmss>.dll`), nên bản mới luôn nạp được cạnh bản cũ;
  `bin\<năm>\current.txt` trỏ tới bản mới nhất.
- Trước khi nạp, bản build được **chép bóng** sang `%LOCALAPPDATA%\WCP-Extensions\shadow\` — Revit không bao giờ khóa
  file mà lần build sau cần ghi.
- Mỗi nút trên ribbon gắn với một lớp lệnh cố định trong add-in (`Slots.S000`…`S255`); bấm nút thì add-in tra xem lớp đó
  đang trỏ tới tool nào của bản build mới nhất. Revit không xóa được nút, nên tool bị xóa thì nút bị ẩn.
- Nút và panel lấy từ thuộc tính `[WcpButton("Tên", Panel = "...", Tooltip = "...")]` trên lớp lệnh.

## Nơi lưu trên máy

| Nơi | Gì |
|---|---|
| `%APPDATA%\Autodesk\Revit\Addins\<năm>\WcpExtensions.addin` + `WcpExtensions\` | Add-in |
| `%USERPROFILE%\WCP-Extensions\<Bộ tool>\` | Bộ tool của người dùng (ngoài OneDrive, cố ý) |
| `%APPDATA%\WCP-Extensions\settings.json` | Thư mục bộ tool, bộ tool đang tắt, `runAlongsideWcp` |
| `%LOCALAPPDATA%\WCP-Extensions\` | `log.txt`, `shadow\`, `dotnet\` (.NET SDK riêng nếu phải tự cài), `Setup.exe` (để gỡ) |

## Giới hạn đã biết

- Cửa sổ của tool đang mở vẫn chạy bản cũ tới khi đóng.
- Nút mới nằm cuối panel tới lần mở Revit sau (Revit không cho sắp lại); khi đó theo `Order`.
- Mỗi phiên Revit tối đa 256 nút tool (tính cả nút đã ẩn). Hết thì mở lại Revit.
- Bản build cũ vẫn nằm trong bộ nhớ tới khi tắt Revit (nhỏ, không ảnh hưởng khi học).
- Máy cần mạng ở lần build đầu (gói NuGet, .NET SDK).

## Phát triển

```
extensions/
  src/WcpExtensions/    add-in Revit (một project, build 4 lần: 2024 net48 · 2025/2026 net8 · 2027 net10)
  src/Setup/            Setup.exe (.NET Framework 4.8, nhúng add-in 4 năm dưới dạng payload.zip)
  template/             bộ khung bộ tool — nhúng vào add-in, "Create" ghi ra đĩa
  tests/UiSmoke/        mở mọi cửa sổ ngoài Revit + chụp ảnh (chạy trên CI)
  tests/RevitStubs/     RevitAPI/RevitAPIUI giả cho UiSmoke
  tools/                build-setup.ps1, new-toolkit.ps1, gen-slots.py
```

- Build add-in một năm: `dotnet build extensions/src/WcpExtensions -c Release -p:RevitVersion=2025`.
- Build Setup.exe đủ 4 năm: `pwsh extensions/tools/build-setup.ps1` (cần .NET 10 SDK) → `extensions/artifacts/`.
- Ra bản mới: tăng `<Version>` trong `extensions/Directory.Build.props`.
- Sửa bộ khung bộ tool: sửa trong `template/` (`__TOOLKIT_ID__`, `__TOOLKIT_NAME__` được thay lúc Create).
  CI tạo một bộ tool từ bộ khung và build đủ 4 năm Revit.

## Đã kiểm / chưa kiểm

Đã kiểm: add-in và bộ khung build 0 lỗi cho cả 4 năm Revit; tạo bộ tool (cả tên tiếng Việt), build, chép bóng,
nạp hai bản build cạnh nhau, đọc `[WcpButton]` — chạy thử ngoài Revit. CI kiểm thêm trên Windows: `build.ps1`,
mở các cửa sổ và chụp ảnh.

**Chưa thử trong Revit thật**: ribbon (tạo/ẩn nút, chấm đỏ), Reload, cửa sổ Toolkits bên trong Revit, Setup.exe trên máy
người dùng, đường Revit 2024 (.NET Framework). Cần một lượt thử tay trước khi phát cho người dùng.
