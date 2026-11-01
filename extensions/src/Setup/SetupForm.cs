using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace WcpExtensions.Setup
{
    /// <summary>The one Setup window: pick Revit versions, Install / Uninstall.</summary>
    sealed class SetupForm : Form
    {
        static readonly Color Page = Color.FromArgb(0xF2, 0xF2, 0xF7);
        static readonly Color TextMain = Color.FromArgb(0x1C, 0x1C, 0x1E);
        static readonly Color TextDim = Color.FromArgb(0x6E, 0x6E, 0x73);
        static readonly Color Accent = Color.FromArgb(0x0A, 0x84, 0xFF);

        readonly Dictionary<string, CheckBox> _years = new Dictionary<string, CheckBox>();
        readonly CheckBox _alongside;
        readonly Label _wcpNote;
        readonly TextBox _log;
        readonly Button _install;
        readonly Button _uninstall;

        public SetupForm()
        {
            Text = "WCP Extensions Setup";
            Font = new Font("Segoe UI", 9.75f);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Page;
            ForeColor = TextMain;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(22, 18, 22, 16);

            var layout = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
            };
            Controls.Add(layout);

            layout.Controls.Add(new Label
            {
                Text = "WCP Extensions",
                Font = new Font("Segoe UI Semibold", 17f),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 0),
            });
            layout.Controls.Add(new Label
            {
                Text = "Version " + Installer.Version,
                ForeColor = TextDim,
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 10),
            });
            layout.Controls.Add(new Label
            {
                Text = "Make your own Revit tools with Claude and load them with one Reload button — " +
                       "Revit never needs a restart. Installs for you only; no admin rights needed.",
                AutoSize = true,
                MaximumSize = new Size(460, 0),
                Margin = new Padding(2, 0, 0, 14),
            });

            var group = new GroupBox
            {
                Text = "Revit versions",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(12, 8, 12, 8),
                Margin = new Padding(0, 0, 0, 10),
                MinimumSize = new Size(464, 0),
            };
            var yearsPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                WrapContents = false,
            };
            bool anyFound = Installer.Years.Any(Installer.RevitFound);
            foreach (string year in Installer.Years)
            {
                bool found = Installer.RevitFound(year);
                bool installed = Installer.IsInstalled(year);
                var box = new CheckBox
                {
                    Text = "Revit " + year + (installed ? "   (installed — will be updated)" : found ? "" : "   (not found on this PC)"),
                    Checked = found || installed,
                    Enabled = found || installed || !anyFound,
                    AutoSize = true,
                    Margin = new Padding(2, 3, 0, 3),
                };
                box.CheckedChanged += (s, e) => UpdateState();
                _years[year] = box;
                yearsPanel.Controls.Add(box);
            }
            group.Controls.Add(yearsPanel);
            layout.Controls.Add(group);

            _wcpNote = new Label
            {
                Text = "WCP Tools (full) is installed for a selected Revit version. It already has Extensions, " +
                       "so WCP Extensions stays hidden there unless you tick the box below.",
                ForeColor = TextDim,
                AutoSize = true,
                MaximumSize = new Size(460, 0),
                Margin = new Padding(2, 0, 0, 4),
                Visible = false,
            };
            layout.Controls.Add(_wcpNote);
            _alongside = new CheckBox
            {
                Text = "Show WCP Extensions alongside WCP Tools",
                AutoSize = true,
                Margin = new Padding(2, 0, 0, 10),
                Visible = false,
            };
            layout.Controls.Add(_alongside);

            _log = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Size = new Size(464, 110),
                Margin = new Padding(0, 0, 0, 12),
                Text = Installer.HasPayload
                    ? "Close Revit before you install. Your toolkits will live in " + Installer.ToolkitsRoot + "."
                    : "This Setup.exe has no add-in inside (developer build). Use the one from GitHub Actions.",
            };
            layout.Controls.Add(_log);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
            };
            var close = MakeButton("Close", false);
            close.Click += (s, e) => Close();
            _install = MakeButton(Installer.AnyInstalled ? "Update" : "Install", true);
            _install.Click += (s, e) => DoInstall();
            _uninstall = MakeButton("Uninstall", false);
            _uninstall.Click += (s, e) => DoUninstall();
            buttons.Controls.Add(close);
            buttons.Controls.Add(_install);
            buttons.Controls.Add(_uninstall);
            layout.Controls.Add(buttons);

            AcceptButton = _install;
            CancelButton = close;
            UpdateState();
        }

        static Button MakeButton(string text, bool primary)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                MinimumSize = new Size(96, 32),
                Margin = new Padding(8, 0, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Color.White,
                ForeColor = primary ? Color.White : TextMain,
                Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = primary ? Accent : Color.FromArgb(0xD1, 0xD1, 0xD6);
            if (primary) b.Font = new Font("Segoe UI Semibold", 9.75f);
            return b;
        }

        List<string> SelectedYears() => _years.Where(kv => kv.Value.Checked && kv.Value.Enabled).Select(kv => kv.Key).ToList();

        void UpdateState()
        {
            List<string> years = SelectedYears();
            bool wcp = years.Any(Installer.FullWcpInstalled);
            _wcpNote.Visible = wcp;
            _alongside.Visible = wcp;
            _install.Enabled = years.Count > 0 && Installer.HasPayload;
            _uninstall.Enabled = Installer.AnyInstalled;
        }

        void Log(string line)
        {
            _log.AppendText(line + Environment.NewLine);
            Application.DoEvents();
        }

        bool EnsureRevitClosed()
        {
            while (Installer.RevitRunning())
            {
                DialogResult answer = MessageBox.Show(this,
                    "Revit is open. Close every Revit window, then press Retry.",
                    "WCP Extensions Setup", MessageBoxButtons.RetryCancel, MessageBoxIcon.Information);
                if (answer != DialogResult.Retry) return false;
            }
            return true;
        }

        void DoInstall()
        {
            List<string> years = SelectedYears();
            if (years.Count == 0 || !EnsureRevitClosed()) return;
            _log.Clear();
            UseWaitCursor = true;
            try
            {
                Installer.Install(years, _alongside.Visible && _alongside.Checked, Log);
                Log("");
                Log("Installed for Revit " + string.Join(", ", years) + ".");
                Log("Open Revit → tab \"WCP Extensions\" → Toolkits → type a name → Create.");
                Log("The first time, Revit asks about an add-in it does not know: choose \"Always Load\".");
                _install.Text = "Update";
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
            }
            finally
            {
                UseWaitCursor = false;
                UpdateState();
            }
        }

        /// <summary>Setup.exe /uninstall (from Windows' app list).</summary>
        public void StartUninstall() => DoUninstall();

        void DoUninstall()
        {
            if (MessageBox.Show(this,
                    "Remove WCP Extensions from Revit?\n\nYour toolkits in " + Installer.ToolkitsRoot + " are kept.",
                    "WCP Extensions Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (!EnsureRevitClosed()) return;
            _log.Clear();
            try
            {
                Installer.Uninstall(Log);
                _install.Text = "Install";
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
            }
            UpdateState();
        }
    }
}
