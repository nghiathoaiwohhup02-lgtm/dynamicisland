using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Autodesk.Revit.UI;
using Button = System.Windows.Controls.Button;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace WcpExtensions
{
    /// <summary>
    /// Toolkits window: create a toolkit, copy a prompt for Claude, build, reload, switch off, delete.
    /// It is modal, so Reload can run straight from its buttons (we are still inside the command).
    /// </summary>
    public partial class ManagerWindow : Window
    {
        readonly RibbonApi _ribbon;
        readonly Queue<Toolkit> _queue = new Queue<Toolkit>();
        readonly List<string> _failed = new List<string>();
        readonly Dictionary<string, List<string>> _buildLog = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        BuildRunner _runner;
        Toolkit _building;
        bool _anyBuilt;
        bool _cancelled;
        bool _closed;
        string _afterBuild;

        internal static Result Open(UIApplication app, List<string> buildKeys)
        {
            try
            {
                var window = new ManagerWindow(RibbonApi.From(app));
                new WindowInteropHelper(window).Owner = app.MainWindowHandle;
                if (buildKeys != null)
                {
                    window.Loaded += (s, e) =>
                    {
                        List<Toolkit> toBuild = Toolkit.Scan(Settings.Load())
                            .Where(t => buildKeys.Contains(t.Key, StringComparer.OrdinalIgnoreCase)).ToList();
                        if (toBuild.Count > 0) window.StartBuild(toBuild);
                    };
                }
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Error("Toolkits window", ex);
                Dialog.Error("Could not open Toolkits", ex.Message, ex.ToString(), null);
            }
            return Result.Succeeded;
        }

        /// <param name="ribbon">How Reload reaches the ribbon (null only in the UI smoke test).</param>
        internal ManagerWindow(RibbonApi ribbon)
        {
            InitializeComponent();
            _ribbon = ribbon;
            RootText.Text = ToolkitHost.Settings.ToolkitsRoot;
            SdkText.Text = "Revit " + Host.RevitYear + " · " + DotnetSdk.Describe(Host.RevitYear);
            Closing += OnClosing;
            Refresh();
        }

        IntPtr Handle => new WindowInteropHelper(this).Handle;

        // ---- List -----------------------------------------------------------------------

        void Refresh()
        {
            List<Toolkit> toolkits = Toolkit.Scan(Settings.Load());
            bool busy = _runner != null || _queue.Count > 0;
            ToolkitList.ItemsSource = toolkits.Select(t => ToolkitRow.From(t, _building, busy)).ToList();
            EmptyText.Visibility = toolkits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            RootText.Text = ToolkitHost.Settings.ToolkitsRoot;
        }

        static ToolkitRow Row(object sender) => (sender as FrameworkElement)?.DataContext as ToolkitRow;

        // ---- Create ---------------------------------------------------------------------

        void NameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            NameHint.Visibility = NameBox.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        void NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                Create_Click(sender, e);
            }
        }

        void Create_Click(object sender, RoutedEventArgs e)
        {
            if (_runner != null) return;
            string name = NameBox.Text.Trim();
            Settings settings = Settings.Load();
            string error = TemplateWriter.Validate(name, settings);
            if (error != null)
            {
                ShowCreateMessage(error, true);
                NameBox.Focus();
                return;
            }
            try
            {
                Directory.CreateDirectory(settings.ToolkitsRoot);
                string folder = TemplateWriter.Create(name, settings);
                NameBox.Text = "";
                ShowCreateMessage("Created " + folder + ". Building it now — the first build downloads packages and can take a few minutes.", false);
                Toolkit created = Toolkit.Scan(settings)
                    .FirstOrDefault(t => string.Equals(t.Folder, folder, StringComparison.OrdinalIgnoreCase));
                _afterBuild = "Done! Open the tab \"" + name + "\" and try Hello and Selection Info. " +
                              "Next: Copy prompt for Claude → New tool.";
                Refresh();
                if (created != null) StartBuild(new List<Toolkit> { created });
            }
            catch (Exception ex)
            {
                Log.Error("Create toolkit " + name, ex);
                ShowCreateMessage("Could not create the toolkit: " + ex.Message, true);
            }
        }

        void ShowCreateMessage(string text, bool isError)
        {
            CreateMessage.Text = text;
            CreateMessage.Foreground = (Brush)FindResource(isError ? "Wx.B.Danger" : "Wx.B.TextSecondary");
        }

        // ---- Prompts for Claude ---------------------------------------------------------

        void Prompt_Click(object sender, RoutedEventArgs e)
        {
            ToolkitRow row = Row(sender);
            if (row == null) return;
            var button = (Button)sender;
            var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
            menu.Items.Add(PromptItem("New tool", "Ask Claude to make a new tool", row.Toolkit, "new"));
            menu.Items.Add(PromptItem("Change a tool", "Ask Claude to change what a tool does", row.Toolkit, "change"));
            menu.Items.Add(PromptItem("Fix an error", "Ask Claude to fix a tool or a failed build", row.Toolkit, "fix"));
            menu.IsOpen = true;
        }

        MenuItem PromptItem(string header, string tip, Toolkit tk, string kind)
        {
            var item = new MenuItem { Header = header, ToolTip = tip };
            item.Click += (s, e) =>
            {
                bool ok = Shell.CopyText(Prompt(tk, kind));
                StatusText.Text = ok
                    ? "Copied. Open Claude Code in the toolkit folder (Open folder), paste, and replace the <…> part with your own words."
                    : "The clipboard is busy. Try again.";
            };
            return item;
        }

        string Prompt(Toolkit tk, string kind)
        {
            string head =
                "My WCP Extensions toolkit \"" + tk.Name + "\" is in this folder:\n" + tk.Folder + "\n" +
                "Read CLAUDE.md in that folder first and follow it. I use Revit " + Host.RevitYear + ".\n\n";
            switch (kind)
            {
                case "change":
                    return head +
                           "Change the tool \"<button name>\":\n" +
                           "<describe what should be different>\n\n" +
                           "When it builds without errors, tell me to press Reload in Revit.";
                case "fix":
                    string text = head +
                                  "The tool \"<button name>\" does not work. What I did and what happened:\n" +
                                  "<describe it, and paste the error text if there was one>\n\n" +
                                  "Find the cause, fix it, build, and tell me to press Reload in Revit.";
                    if (_buildLog.TryGetValue(tk.Key, out List<string> log) && log.Count > 0)
                        text += "\n\nThe last build failed with:\n" + string.Join("\n", log.Skip(Math.Max(0, log.Count - 60)));
                    return text;
                default:
                    return head +
                           "Make a new tool:\n" +
                           "<describe the tool here: what to select or pick, what to check or change, what to show at the end. Vietnamese is fine.>\n\n" +
                           "When it builds without errors, tell me to press Reload in Revit and how to try the tool.";
            }
        }

        // ---- Build ----------------------------------------------------------------------

        void Build_Click(object sender, RoutedEventArgs e)
        {
            ToolkitRow row = Row(sender);
            if (row != null) StartBuild(new List<Toolkit> { row.Toolkit });
        }

        internal void StartBuild(List<Toolkit> toolkits)
        {
            if (_runner != null || _queue.Count > 0) return;
            foreach (Toolkit tk in toolkits) _queue.Enqueue(tk);
            _failed.Clear();
            _anyBuilt = false;
            _cancelled = false;
            LogBox.Clear();
            BuildPanel.Visibility = Visibility.Visible;
            CancelButton.Visibility = Visibility.Visible;
            CreateButton.IsEnabled = false;
            ReloadButton.IsEnabled = false;
            BuildNext();
        }

        void BuildNext()
        {
            if (_closed) return;
            if (_queue.Count == 0 || _cancelled)
            {
                _queue.Clear();
                FinishBuilds();
                return;
            }

            Toolkit tk = _queue.Dequeue();
            _building = tk;
            _buildLog[tk.Key] = new List<string>();
            BuildStatus.Text = "Building " + tk.Name + " for Revit " + Host.RevitYear + "…";
            Append("== " + tk.Name + "  (" + tk.Folder + ")");
            Refresh();

            var runner = new BuildRunner();
            _runner = runner;
            runner.Line += line => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_closed) return;
                Append(line);
                if (_buildLog.TryGetValue(tk.Key, out List<string> log) && line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0)
                    log.Add(line.Trim());
            }));
            runner.Done += code => Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_closed) return;
                _runner = null;
                _building = null;
                if (code == 0)
                {
                    _anyBuilt = true;
                    _buildLog.Remove(tk.Key);
                }
                else
                {
                    _failed.Add(tk.Name);
                }
                BuildNext();
            }));

            try
            {
                runner.Start(tk.Folder, Host.RevitYear);
            }
            catch (Exception ex)
            {
                Log.Error("Start build " + tk.Key, ex);
                Append("ERROR: could not start PowerShell: " + ex.Message);
                _runner = null;
                _building = null;
                _failed.Add(tk.Name);
                BuildNext();
            }
        }

        void FinishBuilds()
        {
            CancelButton.Visibility = Visibility.Collapsed;
            CreateButton.IsEnabled = true;
            ReloadButton.IsEnabled = true;

            if (_anyBuilt) DoReload();

            if (_cancelled)
            {
                BuildStatus.Text = "Build stopped.";
            }
            else if (_failed.Count == 0)
            {
                BuildStatus.Text = "Build OK — reloaded.";
                if (_afterBuild != null) StatusText.Text = _afterBuild;
            }
            else
            {
                BuildStatus.Text = "Build failed: " + string.Join(", ", _failed) + ". Read the errors below, or Copy prompt for Claude → Fix an error.";
                StatusText.Text = "Build failed. Copy prompt for Claude → Fix an error includes the errors for Claude.";
            }
            _afterBuild = null;
            Refresh();
        }

        void Cancel_Click(object sender, RoutedEventArgs e)
        {
            _cancelled = true;
            _queue.Clear();
            _runner?.Cancel();
        }

        void HideLog_Click(object sender, RoutedEventArgs e)
        {
            if (_runner == null) BuildPanel.Visibility = Visibility.Collapsed;
        }

        void Append(string line)
        {
            if (LogBox.Text.Length > 200000) LogBox.Text = LogBox.Text.Substring(LogBox.Text.Length - 100000);
            LogBox.AppendText(line + Environment.NewLine);
            LogBox.ScrollToEnd();
        }

        // ---- Reload / switch / delete ---------------------------------------------------

        void Reload_Click(object sender, RoutedEventArgs e) => DoReload();

        void DoReload()
        {
            try
            {
                ReloadReport report = ToolkitHost.Reload(_ribbon);
                StatusText.Text = "Reloaded: " + report.Summary() + ".";
                if (report.Warnings.Count > 0)
                {
                    BuildPanel.Visibility = Visibility.Visible;
                    foreach (string w in report.Warnings) Append("⚠ " + w);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Reload from Toolkits", ex);
                StatusText.Text = "Reload failed: " + ex.Message;
            }
            Refresh();
        }

        void Enable_Click(object sender, RoutedEventArgs e)
        {
            ToolkitRow row = Row(sender);
            if (row == null) return;
            Settings settings = Settings.Load();
            settings.SetDisabled(row.Toolkit.Key, row.Toolkit.Enabled);
            settings.Save();
            DoReload();
        }

        void Delete_Click(object sender, RoutedEventArgs e)
        {
            ToolkitRow row = Row(sender);
            if (row == null) return;
            Toolkit tk = row.Toolkit;
            if (_building != null && string.Equals(_building.Key, tk.Key, StringComparison.OrdinalIgnoreCase)) return;
            if (!Dialog.Confirm("Delete \"" + tk.Name + "\"?",
                    "The folder moves to the Recycle Bin, so you can still restore it from there.\n\n" + tk.Folder +
                    "\n\nIts tab disappears from Revit.",
                    "Delete", true, Handle))
                return;

            if (!Shell.Recycle(tk.Folder, out string error))
            {
                Dialog.Error("Could not delete \"" + tk.Name + "\"", error, null, null, Handle);
                return;
            }
            Settings settings = Settings.Load();
            settings.SetDisabled(tk.Key, false);
            settings.Save();
            DoReload();
            StatusText.Text = "\"" + tk.Name + "\" moved to the Recycle Bin.";
        }

        void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            ToolkitRow row = Row(sender);
            if (row != null) Shell.OpenFolder(row.Toolkit.Folder);
        }

        void OpenRoot_Click(object sender, RoutedEventArgs e) => Shell.OpenFolder(ToolkitHost.Settings.ToolkitsRoot);

        void OpenLog_Click(object sender, RoutedEventArgs e)
        {
            if (File.Exists(Paths.LogFile)) Shell.OpenFile(Paths.LogFile);
            else StatusText.Text = "No log yet.";
        }

        void Close_Click(object sender, RoutedEventArgs e) => Close();

        void OnClosing(object sender, CancelEventArgs e)
        {
            if (_runner == null && _queue.Count == 0)
            {
                _closed = true;
                return;
            }
            if (!Dialog.Confirm("Stop the build?", "A build is still running. Close the window and stop it?", "Stop and close", true, Handle))
            {
                e.Cancel = true;
                return;
            }
            _closed = true;
            _queue.Clear();
            _runner?.Cancel();
        }
    }

    /// <summary>One card in the Toolkits list.</summary>
    public sealed class ToolkitRow
    {
        internal Toolkit Toolkit;

        public string Name { get; private set; }
        public string Detail { get; private set; }
        public string Folder { get; private set; }
        public string ChipText { get; private set; }
        public Brush ChipBackground { get; private set; }
        public Brush ChipForeground { get; private set; }
        public string EnableText { get; private set; }
        public bool CanBuild { get; private set; }

        static readonly Brush GreenBg = Freeze(0xE3, 0xF7, 0xE8), GreenFg = Freeze(0x1E, 0x7B, 0x34);
        static readonly Brush OrangeBg = Freeze(0xFF, 0xF1, 0xDC), OrangeFg = Freeze(0xA3, 0x5C, 0x00);
        static readonly Brush RedBg = Freeze(0xFF, 0xE5, 0xE3), RedFg = Freeze(0xC4, 0x25, 0x1B);
        static readonly Brush GrayBg = Freeze(0xEC, 0xEC, 0xF0), GrayFg = Freeze(0x5B, 0x5B, 0x60);
        static readonly Brush BlueBg = Freeze(0xE1, 0xEF, 0xFF), BlueFg = Freeze(0x0A, 0x5B, 0xC4);

        static Brush Freeze(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        internal static ToolkitRow From(Toolkit tk, Toolkit building, bool busy)
        {
            string year = Host.RevitYear;
            var row = new ToolkitRow
            {
                Toolkit = tk,
                Name = tk.Name,
                Folder = tk.Folder,
                EnableText = tk.Enabled ? "Switch off" : "Switch on",
                CanBuild = !busy && tk.Enabled && tk.Problem == null,
            };

            if (building != null && string.Equals(building.Key, tk.Key, StringComparison.OrdinalIgnoreCase))
            {
                row.Set("Building…", BlueBg, BlueFg, "Building for Revit " + year + ". Output below.");
                return row;
            }
            if (tk.Problem != null)
            {
                row.Set("Problem", RedBg, RedFg, tk.Problem);
                return row;
            }
            if (!tk.Enabled)
            {
                row.Set("Off", GrayBg, GrayFg, "Its tab is hidden. Switch it on to use it again.");
                return row;
            }

            LoadedBuild loaded = ToolkitHost.LoadedBuildOf(tk.Key);
            string loadError = ToolkitHost.LoadErrorOf(tk.Key);
            string current = tk.CurrentBuildFile(year);
            DateTime? built = tk.CurrentBuildTime(year);

            if (current == null)
                row.Set("Not built", OrangeBg, OrangeFg, "Not built for Revit " + year + " yet — press Build & Reload.");
            else if (loaded == null || !string.Equals(loaded.FileName, current, StringComparison.OrdinalIgnoreCase))
            {
                if (loadError != null)
                    row.Set("Load failed", RedBg, RedFg, loadError);
                else
                    row.Set("New build", OrangeBg, OrangeFg, "Built " + When(built) + ". Press Reload to load it.");
            }
            else
            {
                int n = loaded.Tools.Count;
                row.Set("Loaded", GreenBg, GreenFg,
                    n + (n == 1 ? " tool" : " tools") + " on tab \"" + tk.Tab + "\" · built " + When(built));
            }
            return row;
        }

        void Set(string chip, Brush bg, Brush fg, string detail)
        {
            ChipText = chip;
            ChipBackground = bg;
            ChipForeground = fg;
            Detail = detail;
        }

        static string When(DateTime? time)
        {
            if (time == null) return "—";
            DateTime t = time.Value;
            return t.Date == DateTime.Today ? "today " + t.ToString("HH:mm") : t.ToString("dd MMM HH:mm");
        }
    }
}
