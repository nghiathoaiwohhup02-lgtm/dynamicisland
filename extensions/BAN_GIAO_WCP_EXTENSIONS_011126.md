# BÀN GIAO — WCP EXTENSIONS (bộ cài chỉ có Extensions) — 01/11/2026

> Tổng hợp phiên cloud ngày 01/11/2026 để **một phiên Claude Code trên máy local (Windows, có Revit)** làm tiếp
> mà không phải hỏi lại. Văn bản là ảnh chụp một thời điểm: khi khác với mã nguồn thì **mã nguồn thắng**.
> Hướng dẫn chi tiết cho người đọc: `extensions/README.md`. Luật cho Claude trong mỗi bộ tool: `extensions/template/CLAUDE.md`.

---

## 0. Phiên local: làm gì trước

1. Lấy mã **ngoài OneDrive** (luật WCP: không sửa/build trong OneDrive), ví dụ `C:\dev\dynamicisland`:
   ```powershell
   git clone https://github.com/nghiathoaiwohhup02-lgtm/dynamicisland.git C:\dev\dynamicisland
   cd C:\dev\dynamicisland
   git checkout claude/wcp-extensions-installer
   ```
   (đã có bản clone thì `git fetch origin claude/wcp-extensions-installer` rồi `git checkout` nhánh đó.)
2. Đọc: văn bản này → `extensions/README.md` → `extensions/template/CLAUDE.md`.
3. Kiểm máy có .NET SDK 10: `dotnet --list-sdks` (cần 10.x để build cả Revit 2027; build 2024–2026 thì 8.x đủ).
4. Việc chính còn lại: **thử trong Revit thật** (mục 9). Trao đổi tiếng Việt, ngắn; giao diện tool tiếng Anh.

Luật chủ dự án vẫn áp dụng: Claude **không tự mở/đóng Revit**, không tự chạy bộ cài / sửa `.addin` trên máy chủ dự án
khi chưa được phép từng lần; hỏi bằng AskUserQuestion có đáp án bấm sẵn (đề xuất đứng đầu, câu cuối "Ghi chú / lưu ý chung?").

---

## 1. Yêu cầu và quyết định đã chốt

Yêu cầu gốc của chủ dự án (Trần Nghĩa Thoại): *"Từ ý tưởng của hệ thống [WCP] này, làm 1 file cài đặt chỉ có tính năng
extension. Mục đích: cho người dùng tự tạo tool theo yêu cầu và có nút reload để học dùng."*

Chủ dự án đã chọn (AskUserQuestion, 01/11):

| Câu hỏi | Chọn |
|---|---|
| Gắn với WCP tới mức nào | **Độc lập hoàn toàn** — bộ cài riêng, tab riêng, không cần OneDrive/ổ mạng/Loader WCP, không đụng WCP. Máy đã có WCP đầy đủ thì bản này tự ẩn (để không ra hai thứ "Extensions"). |
| Cách người dùng tạo tool | **Project C# + build như WCP** (không phải kiểu dịch file .cs trong Revit). Máy người dùng cần .NET SDK — `build.ps1` tự cài SDK riêng nếu thiếu. |
| Để mã ở đâu | **Thư mục mới `extensions/` trong repo `dynamicisland`**, GitHub Actions build ra Setup.exe (giống cách Dynamic Island đang làm). Không đụng mã Dynamic Island. |
| Ghi chú | Mặc định (2025/2026/2027, UI tiếng Anh, không cần admin, có gỡ cài đặt) **+ thêm Revit 2024**. |

Mã WCP gốc nằm trên OneDrive nên phiên cloud **không đọc được** → phần Extensions được **viết lại từ đầu**, không chung
định dạng với bộ tool `WCP-<Tên>` của WCP đầy đủ.

---

## 2. Mã ở đâu, trạng thái

| Mục | Giá trị |
|---|---|
| Repo | `nghiathoaiwohhup02-lgtm/dynamicisland` |
| Nhánh | `claude/wcp-extensions-installer` (chưa gộp vào `main`, **chưa có PR** — chủ dự án chưa yêu cầu) |
| Commit | `bd32ed8` (toàn bộ extensions) · `7ac90b4` (sửa trigger workflow) |
| Phiên bản | `0.1.0` (`extensions/Directory.Build.props` → `<Version>`) |
| CI | workflow `extensions` — run [#1 41285037095](https://github.com/nghiathoaiwohhup02-lgtm/dynamicisland/actions/runs/41285037095): **xanh toàn bộ** |
| Artifact | `WCP-Extensions-Setup-0.1.0` (Setup.exe ~360 KB) · `ui-pictures` (ảnh cửa sổ chụp ngoài Revit: toolkits.png, reload-report.png, error.png, delete.png, ribbon-icons.png, setup.png) |

Phiên cloud **không xem được** ảnh `ui-pictures` (proxy chặn blob storage) → giao diện chưa ai soát bằng mắt.

---

## 3. Kiến trúc

```
Revit ─▶ %APPDATA%\Autodesk\Revit\Addins\<năm>\WcpExtensions.addin   (Assembly = WcpExtensions\WcpExtensions.dll, đường dẫn tương đối)
          └─▶ WcpExtensions.dll  (WcpExtensions.App : IExternalApplication, AddInId 4F051520-A552-4FDF-87EE-11C8DF846ADF)
               ├─ có WCP đầy đủ (WCP_Loader.addin / .addin chứa L00_Stub|L00_Loader) và settings.runAlongsideWcp=false → không làm gì
               ├─ tab "WCP Extensions" › panel "Extensions": [Toolkits] [Reload]
               ├─ quét %USERPROFILE%\WCP-Extensions\<BộTool>\ (thư mục có toolkit.json + đúng 1 .csproj)
               ├─ nạp bản build hiện hành của mỗi bộ tool (bin\<năm>\current.txt) → tab riêng cho mỗi bộ tool
               │     tab bộ tool: panel "Develop" [Reload] [Build] + các panel tool
               └─ Idling + FileSystemWatcher(current.txt) → chấm đỏ trên mọi nút Reload khi có build mới chưa nạp
```

**Vì sao Reload chạy được mà không tắt Revit (4 ý):**

1. **Tên assembly mới mỗi lần build**: `<Project>_b<yyMMddHHmmss>.dll` (`AssemblyName = $(MSBuildProjectName)_b$(BuildStamp)`,
   `build.ps1` truyền `-p:BuildStamp`). Bản mới luôn nạp được cạnh bản cũ, kể cả trên .NET Framework (Revit 2024).
   Đường dẫn XAML sinh ra (`/<Asm>_b…;component/...`) đi theo đúng tên đó → không lẫn tài nguyên WPF giữa các bản.
   `dotnet build` trơn (không stamp) **cố ý báo lỗi** (Directory.Build.targets).
2. **Chép bóng**: trước khi nạp, `bin\<năm>\` được chép sang `%LOCALAPPDATA%\WCP-Extensions\shadow\<năm>\<PID>\<BộTool>\<bản>\`
   → Revit không khóa file mà lần build sau cần ghi. Thư mục của PID đã tắt được dọn lúc khởi động.
3. **Nạp**: Revit 2025–2027 (.NET 8/10) → một `ToolkitLoadContext` (AssemblyLoadContext, **không collectible**) mỗi bản build;
   RevitAPI/RevitAPIUI/AdWindows… luôn lấy từ Revit, phụ thuộc khác lấy từ thư mục chép bóng.
   Revit 2024 (.NET 4.8) → `Assembly.LoadFrom` + `AssemblyResolve` dự phòng.
4. **Nút cố định**: mỗi nút tool trỏ vào một lớp lệnh cố định trong add-in `WcpExtensions.Slots.S000…S255`
   (`Slots.g.cs`, sinh bằng `tools/gen-slots.py`). Bấm nút → `ToolkitHost.RunSlot` tra slot đang trỏ tới lớp nào của bản build
   mới nhất → `Activator.CreateInstance` → `Execute`. Revit không xóa được nút → tool bị xóa thì **ẩn** nút (`Visible=false`);
   tab không còn bộ tool → ẩn qua AdWindows (`RibbonTab.IsVisible`).

**Nút của tool** = thuộc tính `[WcpButton("Text", Panel=, Tooltip=, Icon=, Order=, NeedsDocument=)]` đặt trên lớp
`public … : IExternalCommand`. Lớp thuộc tính nằm **trong chính bộ tool** (`_kit\WcpButtonAttribute.cs`); add-in đọc bằng
`CustomAttributeData` theo **tên** `WcpButtonAttribute` → không cần DLL dùng chung giữa add-in và bộ tool.
Lớp lệnh public không có `[WcpButton]` được liệt kê trong báo cáo Reload. Icon: PNG 32×32 theo đường dẫn tương đối thư mục
bộ tool; không có thì add-in vẽ ô chữ cái đầu (màu theo hash ổn định của tên).

**Bộ tool đọc thư mục của mình**: `Kit.ToolkitFolder` (đọc `AppDomain.GetData("WcpExtensions.Folder:<asm>")` do add-in đặt) —
**không** dùng `Assembly.Location` (trỏ vào bản chép bóng).

---

## 4. Cấu trúc `extensions/`

| Đường dẫn | Vai trò |
|---|---|
| `Directory.Build.props` | Version chung; tách `obj\<năm>\` cho project WcpExtensions |
| `.gitattributes` | `*.cmd`, `*.ps1` luôn CRLF |
| `src/WcpExtensions/WcpExtensions.csproj` | Add-in, build 4 lần: `-p:RevitVersion=2024` (net48) · 2025/2026 (net8.0-windows) · 2027 (net10.0-windows). Gói Nice3point.Revit.Api.RevitAPI/RevitAPIUI/AdWindows ghim `2024.0.0 / 2025.0.2 / 2026.0.4 / 2027.0.0` (ExcludeAssets=runtime). Nhúng `template/**` làm resource `template/<đường dẫn>` |
| `App.cs` | Khởi động, phát hiện WCP đầy đủ, Idling → chấm đỏ |
| `Commands.cs` | `ReloadCmd`, `ManagerCmd` (Toolkits), `BuildCmd` (build bộ tool của tab đang mở), `AlwaysAvailable`, `DocumentAvailable` |
| `ToolkitHost.cs` | Reload (quét, nạp bản mới, giữ bản cũ nếu bản mới lỗi), `RunSlot` + hộp lỗi "Copy for Claude", watcher, `ReloadReport` |
| `RibbonManager.cs` | `RibbonApi` (bọc UIControlledApplication/UIApplication), tạo tab/panel/nút, ẩn nút/panel/tab, chấm đỏ Reload |
| `BuildLoader.cs` | Chép bóng, nạp (ALC / LoadFrom), đọc `[WcpButton]`, dọn bóng cũ |
| `Toolkit.cs` | Model bộ tool, `Scan`, `current.txt`; `LoadedBuild`, `ToolDef` |
| `TemplateWriter.cs` | "Create": kiểm tên (chặn tên tab Revit, tab WCP, trùng), `MakeId` (bỏ dấu tiếng Việt → PascalCase), ghi bộ khung (CRLF; BOM cho .cs/.xaml/.ps1, **không** BOM cho .cmd) |
| `BuildRunner.cs` | Chạy `powershell -ExecutionPolicy Bypass -File build.ps1 -Revit <năm>` ẩn, đọc output; dừng bằng `taskkill /T` |
| `DotnetSdk.cs`, `Shell.cs`, `Settings.cs`, `MiniJson.cs`, `Log.cs`, `Paths.cs` (có lớp `Host.RevitYear`), `Icons.cs`, `Properties.cs` (InternalsVisibleTo UiSmoke) | Phụ trợ |
| `UI/Styles.xaml` | Theme kiểu iOS: brush khóa `Wx.B.*`, style khóa `Wx.*` (**không được trùng khóa** — xem bẫy 11.2) |
| `UI/ManagerWindow.xaml(.cs)` | Cửa sổ Toolkits (modal, nên gọi Reload thẳng được): Create, Copy prompt for Claude ▾ (New tool / Change a tool / Fix an error), Build & Reload, Open folder, Switch off/on, Delete (vào Thùng rác), log build, Reload, Close |
| `UI/Dialog.cs` | Hộp thoại tự vẽ (Info/Confirm/Error có "Copy for Claude"/báo cáo Reload); `Build()` tách khỏi `Show()` để thử khói |
| `src/Setup/` | `WcpExtensionsSetup.exe` (net48 WinForms, x64, asInvoker): chọn năm Revit, Install/Update/Uninstall, ô "Show alongside WCP Tools"; `/quiet`, `/uninstall [/quiet]`, `/screenshot <png>`. Nhúng `payload.zip` (`<năm>/WcpExtensions.dll|pdb` + `version.txt`). Tự chép vào `%LOCALAPPDATA%\WCP-Extensions\Setup.exe` + khóa gỡ cài đặt `HKCU\…\Uninstall\WcpExtensions` |
| `template/` | Bộ khung bộ tool (xem mục 5) |
| `tests/UiSmoke/` | Mở mọi cửa sổ ngoài Revit, chụp PNG (CI) |
| `tests/RevitStubs/` | RevitAPI/RevitAPIUI giả (v25.0.0.0) cho UiSmoke — không bao giờ phát hành |
| `tools/build-setup.ps1` | Build add-in 4 năm → `payload.zip` → Setup.exe → `extensions/artifacts/WCP Extensions Setup <ver>.exe` |
| `tools/new-toolkit.ps1` | Tạo bộ tool từ `template/` giống "Create" (CI dùng) |
| `tools/gen-slots.py` | Sinh lại `Slots.g.cs` |
| `README.md` | Hướng dẫn tiếng Việt (người dùng + phát triển) |
| `../.github/workflows/extensions.yml` | CI riêng (mục 7) |

---

## 5. Bộ khung bộ tool (`template/` → `%USERPROFILE%\WCP-Extensions\<Id>\`)

Placeholder thay lúc Create: `__TOOLKIT_ID__` (tên thư mục / .csproj / namespace), `__TOOLKIT_NAME__` (tên hiện + tên tab),
`__AUTHOR__`, `__DATE__`.

| File | Ghi chú |
|---|---|
| `__TOOLKIT_ID__.csproj` | Gần như trống — chỗ duy nhất thêm NuGet (hỏi người dùng trước) |
| `Directory.Build.props` | TFM theo `RevitVersion`, gói Revit API ghim như add-in, `AssemblyName=<Id>_b$(BuildStamp)`, output `bin\<năm>\`, `obj\<năm>\`, `CopyLocalLockFileAssemblies` |
| `Directory.Build.targets` | Báo lỗi khi thiếu BuildStamp / năm sai; sau build: xóa bản cũ, ghi `current.txt` |
| `build.ps1` | Năm Revit: tham số `-Revit 2024,2025` hoặc tự dò (năm đã cài WCP Extensions → năm có Revit → 2025). Tìm SDK (riêng → Program Files → PATH), thiếu thì cài .NET 10 vào `%LOCALAPPDATA%\WCP-Extensions\dotnet` bằng dotnet-install.ps1. **`$ErrorActionPreference='Continue'`** (xem bẫy 11.5). In `BUILD OK` / `BUILD FAILED` |
| `Build.cmd` | Nhấp đúp để build |
| `_kit\WcpButtonAttribute.cs`, `_kit\Kit.cs` | Thuộc tính nút; `Kit.ToolkitFolder/ToolkitName/FilePath/Resource/ShowDialog/Info/Confirm` |
| `Tools\Hello\HelloCmd.cs` | Tool nhỏ nhất (TaskDialog) |
| `Tools\SelectionInfo\…` | Tool có cửa sổ WPF: đếm theo category, chọn lại |
| `CLAUDE.md` | Luật cho Claude: cách thêm tool, build, tương thích 2024–2027 (cấm `record`, `init`, `^1`, `Math.Clamp`…), Revit API, những điều làm hỏng Reload, giao diện, ranh giới |
| `README.md` | Hướng dẫn ngắn tiếng Việt cho người dùng |

---

## 6. Trên máy người dùng

| Nơi | Gì |
|---|---|
| `%APPDATA%\Autodesk\Revit\Addins\<năm>\WcpExtensions.addin` + `WcpExtensions\` | Add-in |
| `%USERPROFILE%\WCP-Extensions\<Id>\` | Bộ tool (ngoài OneDrive, cố ý) |
| `%APPDATA%\WCP-Extensions\settings.json` | `toolkitsRoot`, `disabled` (tên thư mục bộ tool), `runAlongsideWcp` |
| `%LOCALAPPDATA%\WCP-Extensions\` | `log.txt` (nguồn bằng chứng số 1), `shadow\`, `dotnet\`, `Setup.exe` |

Gỡ cài đặt giữ lại bộ tool + settings + log + SDK riêng.

---

## 7. CI (`.github/workflows/extensions.yml`, windows-latest)

Trigger: push nhánh bất kỳ có đổi `extensions/**` hoặc file workflow; PR tương tự; tag `ext-v*` (tạo trang Release kèm Setup.exe);
chạy tay. Các bước: setup-dotnet 8 + 10 → đọc Version → `tools/build-setup.ps1` → tạo bộ tool `CiKit` bằng `new-toolkit.ps1` và
chạy `build.ps1 -Revit 2024,2025,2026,2027` bằng **Windows PowerShell 5.1** → `dotnet run tests/UiSmoke` → `Setup.exe /screenshot`
→ upload 2 artifact. Run #1 hết ~2 phút.

---

## 8. Đã kiểm

- Add-in build **0 lỗi, 0 cảnh báo** cho 2024/2025/2026/2027 (Linux + Windows CI).
- Bộ khung build cho cả 4 năm (CI, qua `build.ps1` thật trên Windows PowerShell 5.1 với output bị chuyển hướng — đúng tình huống
  Revit gọi build).
- Bộ kiểm tra ngoài Revit (harness .NET 10 + RevitAPI giả), **31/31 đạt**: MiniJson; `MakeId("Công cụ của Thoại")=CongCuCuaThoai`;
  Validate (tên tab Revit, tên ngắn, trùng); Create (đổi tên .csproj, thay placeholder, BOM/CRLF, `.gitignore`); build #1 → Scan →
  `current.txt` → nạp từ bóng → đọc Text/Panel/Order/NeedsDocument; sửa + thêm tool → build #2 → bản cũ bị xóa khỏi bin →
  nạp bản 2 cạnh bản 1 → đọc Icon/Tooltip/NeedsDocument=false → báo lớp thiếu `[WcpButton]` → cùng kiểu `IExternalCommand`.
- UiSmoke trên Windows: Styles.xaml nạp được, cửa sổ Toolkits / báo cáo Reload / hộp lỗi / xác nhận xóa dựng được không lỗi XAML.

## 9. CHƯA kiểm — việc của phiên local (theo thứ tự)

Chủ dự án tự cài và mở Revit; Claude hướng dẫn từng bước và đọc `%LOCALAPPDATA%\WCP-Extensions\log.txt` khi có lỗi.

1. **Xem ảnh `ui-pictures`** của run #1 (hoặc chạy UiSmoke tại máy: mục 12) — soát giao diện theo gu WCP (bo góc, màu, chữ không bị cắt).
2. **Cài trên máy chủ dự án** (máy đã có WCP đầy đủ → **phải tick "Show WCP Extensions alongside WCP Tools"**, nếu không add-in tự ẩn;
   hoặc đặt `"runAlongsideWcp": true` trong settings.json). Revit hỏi add-in lạ → Always Load.
3. **Revit 2025** (năm chủ dự án hay dùng):
   - Tab "WCP Extensions" hiện, icon Toolkits/Reload đúng cỡ.
   - Toolkits → Create "Test Kit" → build tự chạy (lần đầu tải NuGet) → tab "Test Kit" có Develop[Reload, Build] + Start here[Hello, Selection Info].
   - Hello, Selection Info chạy; Esc/Close đóng cửa sổ.
   - Sửa chữ trong `HelloCmd.cs` → bấm Build trên tab → nút đổi chữ/hành vi **không cần tắt Revit**.
   - Build ngoài Revit (`Build.cmd`) → chấm đỏ hiện trên Reload trong vài giây → Reload → chấm tắt.
   - Thêm tool mới (panel mới) → Reload → nút mới; xóa thư mục tool → Reload → nút ẩn, panel trống ẩn.
   - Tool ném exception → hộp lỗi có file:line + "Copy for Claude".
   - Switch off → tab ẩn; Switch on → hiện lại; Delete → thư mục vào Thùng rác, tab ẩn.
   - Copy prompt for Claude → dán vào Claude Code trong thư mục bộ tool → Claude làm được 1 tool thật theo `CLAUDE.md`.
4. **Revit 2026, 2027**: lặp nhanh bước 3 (đặc biệt 2026+ có cơ chế cô lập add-in — để ý tool có chạy, cửa sổ XAML có mở).
5. **Revit 2024** (.NET Framework, `Assembly.LoadFrom`): Reload 2–3 lần liên tiếp có cửa sổ WPF (Selection Info) — xem cửa sổ có
   lấy đúng XAML của bản mới không.
6. **Setup.exe**: Update đè bản cũ khi Revit đã tắt; Uninstall từ Settings → Apps; `/quiet`.
7. Máy người dùng **chưa có .NET SDK**: lần build đầu tự cài SDK riêng (proxy công ty có chặn `dot.net` / `builds.dotnet.microsoft.com` không?).

Rủi ro cần để ý khi thử (chưa kiểm chứng được ngoài Revit):
- AdWindows: `RibbonTab.Id` có đúng bằng tên truyền vào `CreateRibbonTab` không (ẩn tab + nút Build dựa vào điều này).
- Đường dẫn tương đối `WcpExtensions\WcpExtensions.dll` trong `.addin`.
- `Application.LoadComponent("/WcpExtensions;component/UI/Styles.xaml")` trong Revit (không có WPF Application).
- Đổi `ItemText/ToolTip/LargeImage/AvailabilityClassName/Visible` của PushButton sau khi tạo.
- Chính sách công ty chặn `-ExecutionPolicy Bypass` → nút Build/Create báo lỗi PowerShell.
- SmartScreen với Setup.exe chưa ký số.

---

## 10. Việc mở / đề xuất (chưa làm, chờ chủ dự án)

- Mở PR gộp `claude/wcp-extensions-installer` → `main` (chủ dự án chưa yêu cầu).
- Phát hành bản có trang tải cố định: đẩy tag `ext-v0.1.0`.
- Thêm kiểm bố cục tự động (chữ bị cắt, tràn khung) vào UiSmoke, giống `theme-shots.ps1` của WCP.
- Tùy chọn "tự Reload sau khi build" (hiện chỉ có chấm đỏ + nút Reload — đúng mục đích "học dùng nút Reload").
- Ký số Setup.exe (cần IT / chứng chỉ).
- Nếu sau này muốn mang bộ tool sang WCP đầy đủ: cần đối chiếu định dạng `extension.json` / dev kit của WCP (mã ở OneDrive) — hiện **không tương thích**.

---

## 11. Bẫy đã gặp trong phiên (đã sửa)

1. **Workflow không chạy khi đẩy nhánh**: `on.push` chỉ có `tags` → GitHub chỉ chạy với tag. Phải khai cả `branches: ['**']`.
2. **Trùng khóa trong ResourceDictionary** (`Wx.Card`, `Wx.Danger`, `Wx.Console` vừa là brush vừa là style): build vẫn qua, nhưng
   mở cửa sổ sẽ crash. Đã tách brush → `Wx.B.*`. UiSmoke trên CI giờ bắt loại lỗi này.
3. **obj của năm khác bị biên dịch lẫn** (`CS0102 already contains a definition`): mỗi năm `obj\<năm>\`, phải thêm
   `DefaultItemExcludes … bin\**;obj\**`.
4. **Trùng tên kiểu WPF/Revit**: `MenuItem`, `ContextMenu`, `Button` (Autodesk.Revit.UI) → dùng alias `using MenuItem = System.Windows.Controls.MenuItem;`.
5. **Windows PowerShell 5.1 + `$ErrorActionPreference='Stop'`**: dòng stderr của dotnet thành lỗi dừng khi output bị chuyển hướng
   (Revit gọi build, CI) → dùng `Continue`, lỗi xử lý tường minh.
6. **Gói Nice3point RevitAPI/RevitAPIUI là mixed-mode** → không nạp được ngoài Revit (BadImageFormat) → thử nghiệm dùng RevitAPI giả
   (`tests/RevitStubs`, cùng tên + version 25.0.0.0).
7. **Đường dẫn XAML WPF không có version** trong URI sinh ra → nếu giữ tên assembly cố định, WPF có thể phục vụ XAML của bản cũ
   (cache theo tên). Đây là lý do chọn **tên assembly mới mỗi build** thay vì đổi version.

---

## 12. Lệnh hay dùng (trên máy local, từ thư mục repo)

```powershell
# Add-in một năm
dotnet build extensions/src/WcpExtensions -c Release -p:RevitVersion=2025
# Setup.exe đủ 4 năm  → extensions/artifacts/WCP Extensions Setup <ver>.exe
pwsh extensions/tools/build-setup.ps1        # hoặc: powershell -ExecutionPolicy Bypass -File extensions\tools\build-setup.ps1
# Thử khói giao diện ngoài Revit + chụp ảnh (cần build 2025 trước)
dotnet run --project extensions/tests/UiSmoke -c Release -- extensions/artifacts/ui-shots
# Tạo bộ tool thử ngoài thư mục người dùng và build
pwsh extensions/tools/new-toolkit.ps1 -Name "Test Kit" -Id TestKit -Root C:\temp\kits
powershell -NoProfile -ExecutionPolicy Bypass -File C:\temp\kits\TestKit\build.ps1 -Revit 2025
# Sinh lại lớp nút cố định (đổi số slot)
python extensions/tools/gen-slots.py 256
```

Ra bản mới: tăng `<Version>` trong `extensions/Directory.Build.props` → commit → CI build; muốn trang Release thì đẩy tag `ext-v<ver>`.
Commit kết thúc bằng hai dòng attribution như các commit trước; không ghi tên model trong commit/PR.

---

## 13. Quan hệ với hệ thống WCP Tools

- **Không đụng** Loader/Stub/WcpContracts/`.addin` của WCP, không đặt gì vào `01. Source (C#)` / `02. Release (C#)` / `03. Release-test (C#)`,
  không dùng `wcp-flow.ps1`. Đây là sản phẩm riêng trong repo GitHub `dynamicisland`.
- Tên dễ nhầm: thư mục bộ tool của bản này là `%USERPROFILE%\WCP-Extensions\<Id>\` (khác `%USERPROFILE%\WCP-<Tên>\` của WCP);
  tab chính "WCP Extensions"; tên "WCP-Manage…", "WCP-Design…" bị chặn khi đặt tên bộ tool.
- Phát hiện WCP đầy đủ: `WCP_Loader.addin` hoặc `.addin` chứa `L00_Stub` / `L00_Loader` trong `%APPDATA%\Autodesk\Revit\Addins\<năm>\`.
