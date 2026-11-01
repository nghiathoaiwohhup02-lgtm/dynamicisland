# Toolkit "__TOOLKIT_NAME__" — instructions for Claude

This folder is a **WCP Extensions toolkit**: Revit tools written in C#. WCP Extensions (a Revit
add-in) puts every tool on the ribbon tab **__TOOLKIT_NAME__**. After a build, the user presses
**Reload** in Revit and the new version runs right away. Revit never has to be restarted.

The user may write in Vietnamese or English. Answer in the user's language, short and plain.
They are BIM people, not programmers: don't hand them commands to run; do the work, then tell
them what to click in Revit.

## Folder layout

```
__TOOLKIT_ID__.csproj        project file (add NuGet packages here only if really needed; ask first)
toolkit.json                 toolkit name and ribbon tab
CLAUDE.md, README.md         this file / short guide for the user
build.ps1, Build.cmd         build script (kit file, do not edit)
Directory.Build.props        build settings (kit file, do not edit)
Directory.Build.targets      build steps (kit file, do not edit)
_kit\                        WcpButtonAttribute.cs, Kit.cs (kit files, do not edit)
Tools\<ToolName>\            one folder per tool
Icons\                       optional 32x32 PNG button icons
bin\<year>\, obj\            build output (never edit, never commit)
```

## Add a tool

1. Create `Tools\<ToolName>\<ToolName>Cmd.cs`: a **public** class implementing `IExternalCommand`,
   with `[Transaction(TransactionMode.Manual)]` and `[WcpButton(...)]`. Namespace `__TOOLKIT_ID__`.
2. If it needs a window: `Tools\<ToolName>\<ToolName>Window.xaml` + `.xaml.cs` (WPF).
3. Build (below). Fix every error until the build says `BUILD OK`.
4. Tell the user: "In Revit, press **Reload**, then open tab **__TOOLKIT_NAME__** → `<button>`."
   Also say in one or two lines how to try it.

Look at `Tools\Hello` (smallest tool) and `Tools\SelectionInfo` (tool with a window) first.

```csharp
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace __TOOLKIT_ID__
{
    [Transaction(TransactionMode.Manual)]
    [WcpButton("Count\nWalls", Panel = "Walls", Order = 1,
        Tooltip = "Counts the walls in the active view.")]
    public class CountWallsCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            int count = new FilteredElementCollector(doc, doc.ActiveView.Id)
                .OfCategory(BuiltInCategory.OST_Walls).WhereElementIsNotElementType().GetElementCount();
            Kit.Info("Count Walls", count + " walls in this view.");
            return Result.Succeeded;
        }
    }
}
```

### `[WcpButton]` options

| Property | Meaning | Default |
|---|---|---|
| `Text` (first argument) | Button text, `\n` = two lines | — |
| `Panel` | Ribbon panel name | `"Tools"` |
| `Tooltip` | Text when hovering | none |
| `Icon` | `"Icons/name.png"` (32x32 PNG, relative to this folder) | icon from initials |
| `Order` | Order inside the panel | 0 |
| `NeedsDocument` | `false` = works with no model open | `true` |

To remove a tool: delete its folder and build. Its button disappears on Reload.
To rename a button or change its tooltip/icon: edit the attribute and build.

## Build

Always build with the script (from this folder):

```
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1              # every Revit version WCP Extensions is installed for
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1 -Revit 2025  # one version
```

- Never run plain `dotnet build`: it fails on purpose. The script gives each build a unique
  assembly name (`__TOOLKIT_ID___b<stamp>.dll`) — that is what lets Revit load the new version.
- The first build downloads NuGet packages (and the .NET SDK if missing). That can take minutes.
- Revit may stay open while you build. Nothing is locked.
- Warnings are fine; errors are not. Build until `BUILD OK`.

## Rules

**Revit versions.** One code base is compiled for Revit 2024 (.NET Framework 4.8), 2025/2026
(.NET 8) and 2027 (.NET 10). Write C# that compiles on all of them:
- No `record`, `init`, `required`, ranges/indexes (`^1`, `..`), `Math.Clamp`, `HashCode`,
  `string.Contains(char)`, `Split(char, options)`, `GetValueOrDefault` on dictionaries,
  `Path.GetRelativePath`, `File.*Async`, `TryAdd` on dictionaries. Normal LINQ, generics,
  `async`, pattern matching, switch expressions, string interpolation are all fine.
- `ElementId.Value` (long). Never `IntegerValue`.
- Only APIs that exist in Revit 2024–2027. If unsure, build for every version:
  `.\build.ps1 -Revit 2024,2025,2026,2027`.
- `#if REVIT2024` … `#endif` exists for the rare case where versions really differ.

**Revit API.**
- Every change to the model goes inside a `Transaction` with a clear name
  (`using (var t = new Transaction(doc, "Renumber Sheets")) { t.Start(); … t.Commit(); }`).
  Several steps → `TransactionGroup` + `Assimilate()`.
- Picking: hide your window, call `uidoc.Selection.PickObject(...)`, show it again.
  Catch `Autodesk.Revit.Exceptions.OperationCanceledException` (user pressed Esc) and return
  `Result.Cancelled`.
- Windows: modal only — `Kit.ShowDialog(window, commandData.Application)`, then do the Revit
  work after it closes. A modeless window needs an `ExternalEvent`; avoid it unless the user
  asks for one and understands why.
- Do not keep Revit objects in `static` fields between runs.

**Things that break Reload — never do these.**
- `Assembly.Location`, `Assembly.GetExecutingAssembly().Location`, `AppDomain.BaseDirectory`
  to find files → use `Kit.ToolkitFolder` / `Kit.FilePath("Data/x.csv")`.
- Typing the assembly name: `pack://application:,,,/__TOOLKIT_ID__;component/...` or
  `clr-namespace:X;assembly=__TOOLKIT_ID__`. The name changes every build. Use
  `Kit.Resource("Tools/X/logo.png")` in C#, a relative path in XAML (`Source="logo.png"`),
  and `xmlns:local="clr-namespace:__TOOLKIT_ID__"` with no `;assembly=`.
- `static` event handlers attached to Revit (`DocumentChanged`, `Idling`, …): the old version
  keeps running after Reload. Don't use them.

**Interface.**
- Button text, window text and messages in **English** (short, plain).
- Look like the samples: light grey page (`#F2F2F7`), white rounded cards, blue primary
  button (`#0A84FF`), Segoe UI 13. Messages: `Kit.Info(title, text)`, `Kit.Confirm(title, question)`.
- Tool windows: resizable, nothing cut off, Esc closes (`IsCancel="True"` on Close).

**Boundaries.**
- Only change files inside this folder. Never touch `%APPDATA%\Autodesk\Revit\Addins`,
  `.addin` files, or the WCP Extensions add-in itself.
- Don't start, close or install Revit. Don't edit kit files (`_kit\`, `build.ps1`,
  `Build.cmd`, `Directory.Build.*`).
- Don't add NuGet packages or download tools without asking the user first.
- Never copy `RevitAPI.dll` / `RevitAPIUI.dll` into the toolkit or reference
  `C:\Program Files\Autodesk`.

## When the user reports a problem

| They say | Check |
|---|---|
| Button does not appear | Class is `public`, implements `IExternalCommand`, has `[WcpButton]`; build said `BUILD OK`; they pressed Reload. The Reload report lists classes without `[WcpButton]`. |
| "Not built for Revit 20xx" | Build for that version: `.\build.ps1 -Revit 20xx`. |
| Old behaviour after Reload | A window of the old version was still open — close it and click the button again. Or the build failed: build again and read the errors. |
| An error box when clicking | The box shows the exception and file/line. Fix that line, build, Reload. |
| Button order is odd | New buttons go at the end of their panel until Revit restarts. That is normal. |
