using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace WcpExtensions
{
    /// <summary>Keeps track of toolkits, loads their builds and runs their commands.</summary>
    static class ToolkitHost
    {
        public static Settings Settings = new Settings();
        public static List<Toolkit> Toolkits = new List<Toolkit>();

        /// <summary>Builds loaded in this session, by toolkit key. Kept across Reloads.</summary>
        static readonly Dictionary<string, LoadedBuild> Loaded = new Dictionary<string, LoadedBuild>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Set by the file watcher when a build finishes; checked on Idling.</summary>
        public static volatile bool BuildChanged;
        static FileSystemWatcher _watcher;

        public static void Startup(RibbonApi ribbon)
        {
            Settings = Settings.Load();
            try { Directory.CreateDirectory(Settings.ToolkitsRoot); }
            catch (Exception ex) { Log.Error("Could not create " + Settings.ToolkitsRoot, ex); }
            BuildLoader.CleanOldShadows(Host.RevitYear);
            ReloadReport report = Reload(ribbon);
            Log.Info("Startup: " + report.Summary());
        }

        public static void Shutdown()
        {
            try { _watcher?.Dispose(); } catch { }
            _watcher = null;
        }

        /// <summary>Re-reads every toolkit, loads new builds and updates the ribbon.</summary>
        public static ReloadReport Reload(RibbonApi ribbon)
        {
            var report = new ReloadReport(Host.RevitYear);
            Settings = Settings.Load();
            List<Toolkit> toolkits = Toolkit.Scan(Settings);
            string year = Host.RevitYear;

            foreach (Toolkit tk in toolkits)
            {
                ToolkitReport line = report.For(tk);
                Loaded.TryGetValue(tk.Key, out LoadedBuild previous);
                tk.Loaded = previous;

                if (tk.Problem != null) { line.Status = "Problem: " + tk.Problem; continue; }
                if (!tk.Enabled) { line.Status = "Off"; continue; }

                string current = tk.CurrentBuildFile(year);
                if (current == null)
                {
                    line.Status = "Not built for Revit " + year + " yet — open Toolkits and press Build & Reload.";
                    continue;
                }
                if (previous != null && string.Equals(previous.FileName, current, StringComparison.OrdinalIgnoreCase))
                {
                    line.Status = "No new build";
                    continue;
                }

                try
                {
                    LoadedBuild build = BuildLoader.Load(tk, year);
                    tk.Loaded = build;
                    Loaded[tk.Key] = build;
                    line.Status = (previous == null ? "Loaded build " : "Loaded new build ") + build.BuildTime.ToString("HH:mm:ss");
                    line.Changed = true;
                    foreach (string type in build.NoAttribute)
                        report.Warnings.Add(tk.Name + ": " + type + " has no [WcpButton], so it gets no button.");
                    Log.Info("Loaded " + tk.Key + " " + build.FileName + " (" + build.Tools.Count + " tools)");
                }
                catch (Exception ex)
                {
                    tk.LoadError = Unwrap(ex).Message;
                    line.Status = "Could not load the new build: " + tk.LoadError +
                                  (previous != null ? " (the previous build stays in use)" : "");
                    Log.Error("Load " + tk.Key + " " + current, ex);
                }
            }

            Toolkits = toolkits;
            RibbonManager.Sync(ribbon, toolkits, report);
            RibbonManager.SetReloadDot(false);
            BuildChanged = false;
            EnsureWatcher();
            return report;
        }

        public static LoadedBuild LoadedBuildOf(string key) =>
            Loaded.TryGetValue(key, out LoadedBuild build) ? build : null;

        public static string LoadErrorOf(string key) =>
            Toolkits.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))?.LoadError;

        /// <summary>True when some enabled toolkit has a build that is not loaded yet.</summary>
        public static bool AnyNewBuild()
        {
            foreach (Toolkit tk in Toolkit.Scan(Settings))
            {
                if (!tk.Enabled || tk.Problem != null) continue;
                Loaded.TryGetValue(tk.Key, out tk.Loaded);
                if (tk.HasNewBuild(Host.RevitYear)) return true;
            }
            return false;
        }

        /// <summary>Called by the fixed command classes Slots.Snnn behind every toolkit button.</summary>
        public static Result RunSlot(int slot, ExternalCommandData data, ref string message, ElementSet elements)
        {
            SlotTarget target = RibbonManager.GetSlot(slot);
            if (target == null)
            {
                Dialog.Info("WCP Extensions", "This tool is no longer in its toolkit. Press Reload to update the ribbon.");
                return Result.Cancelled;
            }

            IExternalCommand command;
            try
            {
                command = (IExternalCommand)Activator.CreateInstance(target.Tool.Type);
            }
            catch (Exception ex)
            {
                ShowToolError(target, Unwrap(ex));
                return Result.Cancelled;
            }

            try
            {
                return command.Execute(data, ref message, elements);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                ShowToolError(target, ex);
                return Result.Cancelled;
            }
        }

        static void ShowToolError(SlotTarget target, Exception ex)
        {
            Log.Error("Tool " + target.Tool.TypeName + " failed", ex);
            string forClaude =
                "A tool in my WCP Extensions toolkit \"" + target.Toolkit.Name + "\" stopped with an error.\n" +
                "Toolkit folder: " + target.Toolkit.Folder + "\n" +
                "Tool: " + target.Tool.Text.Replace("\n", " ") + " (" + target.Tool.TypeName + "), Revit " + Host.RevitYear +
                ", build " + target.Build.FileName + "\n" +
                "Read CLAUDE.md in that folder first. Find the cause, fix it, build, and tell me to press Reload.\n\n" +
                "What I did: <describe what you clicked / selected>\n\n" +
                "Error:\n" + ex;
            Dialog.Error(
                target.Tool.Text.Replace("\n", " ") + " stopped with an error",
                ex.GetType().Name + ": " + ex.Message,
                ex.ToString(),
                forClaude);
        }

        static Exception Unwrap(Exception ex)
        {
            while (ex is TargetInvocationException && ex.InnerException != null) ex = ex.InnerException;
            return ex;
        }

        static void EnsureWatcher()
        {
            string root = Settings.ToolkitsRoot;
            if (_watcher != null && string.Equals(_watcher.Path, root, StringComparison.OrdinalIgnoreCase)) return;
            Shutdown();
            if (!Directory.Exists(root)) return;
            try
            {
                _watcher = new FileSystemWatcher(root, "current.txt")
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size,
                };
                FileSystemEventHandler changed = (s, e) => BuildChanged = true;
                _watcher.Changed += changed;
                _watcher.Created += changed;
                _watcher.Renamed += (s, e) => BuildChanged = true;
                _watcher.EnableRaisingEvents = true;
            }
            catch (Exception ex)
            {
                Log.Error("Could not watch " + root, ex);
                _watcher = null;
            }
        }
    }

    /// <summary>What a toolkit button runs right now.</summary>
    sealed class SlotTarget
    {
        public Toolkit Toolkit;
        public ToolDef Tool;
        public LoadedBuild Build;
    }

    sealed class ToolkitReport
    {
        public string Name;
        public string Tab;
        public string Status = "";
        public bool Changed;
        public int Tools;
        public int NewButtons;
        public int RemovedButtons;
    }

    /// <summary>Result of a Reload, shown to the user.</summary>
    sealed class ReloadReport
    {
        public ReloadReport(string year) { Year = year; }

        public readonly string Year;
        public readonly List<ToolkitReport> Toolkits = new List<ToolkitReport>();
        public readonly List<string> Warnings = new List<string>();
        readonly Dictionary<string, ToolkitReport> _byKey = new Dictionary<string, ToolkitReport>(StringComparer.OrdinalIgnoreCase);

        public ToolkitReport For(Toolkit tk)
        {
            if (!_byKey.TryGetValue(tk.Key, out ToolkitReport r))
            {
                r = new ToolkitReport { Name = tk.Name, Tab = tk.Tab };
                _byKey[tk.Key] = r;
                Toolkits.Add(r);
            }
            return r;
        }

        public bool AnyChange => Toolkits.Exists(t => t.Changed || t.NewButtons > 0 || t.RemovedButtons > 0);

        public string Summary()
        {
            if (Toolkits.Count == 0) return "No toolkits yet.";
            int tools = Toolkits.Sum(t => t.Tools);
            return Toolkits.Count + (Toolkits.Count == 1 ? " toolkit, " : " toolkits, ") + tools + (tools == 1 ? " tool" : " tools") +
                   (AnyChange ? "" : " — everything is up to date");
        }

        public string Details()
        {
            var lines = new List<string>();
            foreach (ToolkitReport t in Toolkits)
            {
                string counts = t.Tools > 0 ? " — " + t.Tools + (t.Tools == 1 ? " tool" : " tools") : "";
                if (t.NewButtons > 0) counts += ", " + t.NewButtons + " new";
                if (t.RemovedButtons > 0) counts += ", " + t.RemovedButtons + " removed";
                lines.Add("• " + t.Name + counts + "\n   " + t.Status);
            }
            if (Warnings.Count > 0)
            {
                lines.Add("");
                lines.AddRange(Warnings.Select(w => "⚠ " + w));
            }
            return string.Join("\n", lines);
        }
    }
}
