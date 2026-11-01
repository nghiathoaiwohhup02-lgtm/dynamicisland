using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.UI;

namespace WcpExtensions
{
    /// <summary>
    /// The same ribbon calls exist on UIControlledApplication (at startup) and UIApplication
    /// (inside a command); this wraps whichever one we have.
    /// </summary>
    sealed class RibbonApi
    {
        public Action<string> CreateTab;
        public Func<string, string, RibbonPanel> CreatePanel;
        public Func<string, IList<RibbonPanel>> GetPanels;

        public static RibbonApi From(UIControlledApplication app) => new RibbonApi
        {
            CreateTab = app.CreateRibbonTab,
            CreatePanel = app.CreateRibbonPanel,
            GetPanels = tab => app.GetRibbonPanels(tab),
        };

        public static RibbonApi From(UIApplication app) => new RibbonApi
        {
            CreateTab = app.CreateRibbonTab,
            CreatePanel = app.CreateRibbonPanel,
            GetPanels = tab => app.GetRibbonPanels(tab),
        };
    }

    /// <summary>
    /// Builds and updates the ribbon.
    ///
    /// Revit cannot delete ribbon items, so a removed tool's button is hidden, and a button always
    /// keeps the fixed command class it was created with (Slots.Snnn). What a slot runs is looked
    /// up at click time, which is how a new build takes over an existing button.
    /// </summary>
    static class RibbonManager
    {
        public const string MainTab = "WCP Extensions";
        const string DevelopPanel = "Develop";

        static readonly SlotTarget[] SlotTargets = new SlotTarget[Slots.SlotTable.Count];
        static int _nextSlot;
        static int _nextName;

        static readonly Dictionary<string, RibbonPanel> Panels = new Dictionary<string, RibbonPanel>(StringComparer.Ordinal);
        static readonly Dictionary<string, ButtonEntry> Buttons = new Dictionary<string, ButtonEntry>(StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> ToolkitTabs = new HashSet<string>(StringComparer.Ordinal);
        static readonly List<PushButton> ReloadButtons = new List<PushButton>();

        sealed class ButtonEntry
        {
            public PushButton Button;
            public int Slot;
            public string Tab;
            public string Panel;
            public bool Visible;
        }

        static string CoreDll => typeof(RibbonManager).Assembly.Location;

        public static SlotTarget GetSlot(int slot) =>
            slot >= 0 && slot < SlotTargets.Length ? SlotTargets[slot] : null;

        /// <summary>Tab "WCP Extensions" with the Toolkits and Reload buttons.</summary>
        public static void CreateMain(RibbonApi api)
        {
            try { api.CreateTab(MainTab); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { /* already there */ }
            RibbonPanel panel = GetOrCreatePanel(api, MainTab, "Extensions");

            var toolkits = new PushButtonData(NextName("Toolkits"), "Toolkits", CoreDll, typeof(ManagerCmd).FullName)
            {
                ToolTip = "Create a toolkit, copy a prompt for Claude, build, switch toolkits on or off.",
                LongDescription = "A toolkit is a folder of your own tools. Each toolkit gets its own ribbon tab.",
                LargeImage = Icons.Toolkits(32),
                Image = Icons.Toolkits(16),
                AvailabilityClassName = typeof(AlwaysAvailable).FullName,
            };
            panel.AddItem(toolkits);
            AddReloadButton(panel);
        }

        static void AddReloadButton(RibbonPanel panel)
        {
            var data = new PushButtonData(NextName("Reload"), "Reload", CoreDll, typeof(ReloadCmd).FullName)
            {
                ToolTip = ReloadTip(false),
                LargeImage = Icons.Reload(32, false),
                Image = Icons.Reload(16, false),
                AvailabilityClassName = typeof(AlwaysAvailable).FullName,
            };
            if (panel.AddItem(data) is PushButton button) ReloadButtons.Add(button);
        }

        static string ReloadTip(bool newBuild) =>
            (newBuild ? "A new build is ready. " : "") +
            "Load the newest build of every toolkit. Revit does not need a restart.";

        /// <summary>Puts a red dot on every Reload button when a new build is waiting.</summary>
        public static void SetReloadDot(bool newBuild)
        {
            foreach (PushButton button in ReloadButtons)
            {
                try
                {
                    button.LargeImage = Icons.Reload(32, newBuild);
                    button.Image = Icons.Reload(16, newBuild);
                    button.ToolTip = ReloadTip(newBuild);
                }
                catch (Exception ex)
                {
                    Log.Error("SetReloadDot", ex);
                }
            }
        }

        static void EnsureToolkitTab(RibbonApi api, string tab)
        {
            if (tab == MainTab || ToolkitTabs.Contains(tab)) return;
            try { api.CreateTab(tab); }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { /* exists already */ }
            ToolkitTabs.Add(tab);

            // Every toolkit tab starts with Reload and Build, so learners never have to switch tabs.
            RibbonPanel develop = GetOrCreatePanel(api, tab, DevelopPanel);
            AddReloadButton(develop);
            var build = new PushButtonData(NextName("Build"), "Build", CoreDll, typeof(BuildCmd).FullName)
            {
                ToolTip = "Build the toolkit(s) of this tab for this Revit version, then Reload.",
                LargeImage = Icons.Build(32),
                Image = Icons.Build(16),
                AvailabilityClassName = typeof(AlwaysAvailable).FullName,
            };
            develop.AddItem(build);
        }

        static RibbonPanel GetOrCreatePanel(RibbonApi api, string tab, string name)
        {
            string key = tab + "\n" + name;
            if (Panels.TryGetValue(key, out RibbonPanel panel)) return panel;
            try
            {
                panel = api.CreatePanel(tab, name);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                panel = api.GetPanels(tab).FirstOrDefault(p => p.Name == name);
                if (panel == null) throw;
            }
            Panels[key] = panel;
            return panel;
        }

        static string NextName(string prefix) => "wcpx_" + prefix + "_" + (_nextName++).ToString("000");

        /// <summary>Makes the ribbon match the loaded builds.</summary>
        public static void Sync(RibbonApi api, List<Toolkit> toolkits, ReloadReport report)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tabsInUse = new HashSet<string>(StringComparer.Ordinal);

            foreach (Toolkit tk in toolkits)
            {
                LoadedBuild build = tk.Enabled && tk.Problem == null ? tk.Loaded : null;
                if (build == null) continue;
                ToolkitReport line = report.For(tk);
                line.Tools = build.Tools.Count;
                if (build.Tools.Count == 0)
                {
                    report.Warnings.Add(tk.Name + ": no class with [WcpButton] yet, so the toolkit has no buttons.");
                    continue;
                }

                try
                {
                    EnsureToolkitTab(api, tk.Tab);
                }
                catch (Exception ex)
                {
                    line.Status = "Could not create the ribbon tab \"" + tk.Tab + "\": " + ex.Message;
                    Log.Error("Tab " + tk.Tab, ex);
                    continue;
                }
                tabsInUse.Add(tk.Tab);

                foreach (ToolDef tool in Ordered(build.Tools))
                {
                    string key = tk.Key + "\n" + tool.TypeName;
                    seen.Add(key);
                    var target = new SlotTarget { Toolkit = tk, Tool = tool, Build = build };
                    try
                    {
                        if (Buttons.TryGetValue(key, out ButtonEntry entry) && entry.Tab == tk.Tab && entry.Panel == tool.Panel)
                        {
                            if (!entry.Visible) line.NewButtons++;
                            Apply(entry, tk, tool);
                            SlotTargets[entry.Slot] = target;
                            continue;
                        }

                        // New button (or the tool moved to another panel / tab).
                        if (entry != null) Hide(entry);
                        if (_nextSlot >= SlotTargets.Length)
                        {
                            report.Warnings.Add("Too many buttons for one Revit session (" + SlotTargets.Length +
                                                "). Restart Revit to show \"" + tool.Text.Replace("\n", " ") + "\".");
                            continue;
                        }
                        RibbonPanel panel = GetOrCreatePanel(api, tk.Tab, tool.Panel);
                        panel.Visible = true;
                        int slot = _nextSlot++;
                        var data = new PushButtonData(NextName("Tool"), tool.Text, CoreDll, Slots.SlotTable.ClassName(slot));
                        if (panel.AddItem(data) is PushButton button)
                        {
                            entry = new ButtonEntry { Button = button, Slot = slot, Tab = tk.Tab, Panel = tool.Panel };
                            Buttons[key] = entry;
                            Apply(entry, tk, tool);
                            SlotTargets[slot] = target;
                            line.NewButtons++;
                        }
                    }
                    catch (Exception ex)
                    {
                        report.Warnings.Add(tk.Name + ": could not add the button \"" + tool.Text.Replace("\n", " ") + "\": " + ex.Message);
                        Log.Error("Button " + key, ex);
                    }
                }
            }

            // Buttons of tools that are gone (deleted, toolkit switched off, folder removed).
            foreach (KeyValuePair<string, ButtonEntry> kv in Buttons)
            {
                if (seen.Contains(kv.Key) || !kv.Value.Visible) continue;
                Hide(kv.Value);
                string toolkitKey = kv.Key.Substring(0, kv.Key.IndexOf('\n'));
                Toolkit tk = toolkits.FirstOrDefault(t => string.Equals(t.Key, toolkitKey, StringComparison.OrdinalIgnoreCase));
                if (tk != null) report.For(tk).RemovedButtons++;
            }

            // Panels with nothing visible left are hidden; a tab with no toolkit left is hidden.
            foreach (KeyValuePair<string, RibbonPanel> kv in Panels)
            {
                string[] parts = kv.Key.Split('\n');
                string tab = parts[0], name = parts[1];
                bool visible;
                if (tab == MainTab && name == "Extensions") visible = true;
                else if (name == DevelopPanel) visible = tabsInUse.Contains(tab);
                else visible = Buttons.Values.Any(b => b.Visible && b.Tab == tab && b.Panel == name);
                try { kv.Value.Visible = visible; } catch (Exception ex) { Log.Error("Panel " + kv.Key, ex); }
            }
            foreach (string tab in ToolkitTabs)
                SetTabVisible(tab, tabsInUse.Contains(tab));
        }

        /// <summary>Panels in order of their lowest Order; buttons by Order, then text.</summary>
        static IEnumerable<ToolDef> Ordered(List<ToolDef> tools)
        {
            var panelRank = tools.GroupBy(t => t.Panel)
                .Select(g => new { Panel = g.Key, Rank = g.Min(t => t.Order) })
                .OrderBy(p => p.Rank).ThenBy(p => p.Panel, StringComparer.CurrentCultureIgnoreCase)
                .Select((p, i) => new { p.Panel, Index = i })
                .ToDictionary(p => p.Panel, p => p.Index);
            return tools.OrderBy(t => panelRank[t.Panel]).ThenBy(t => t.Order)
                .ThenBy(t => t.Text, StringComparer.CurrentCultureIgnoreCase);
        }

        static void Apply(ButtonEntry entry, Toolkit tk, ToolDef tool)
        {
            PushButton b = entry.Button;
            b.ItemText = tool.Text;
            b.ToolTip = string.IsNullOrWhiteSpace(tool.Tooltip) ? tool.Text.Replace("\n", " ") : tool.Tooltip;
            b.LongDescription = "Toolkit: " + tk.Name + "  ·  " + tool.TypeName;
            b.LargeImage = Icons.ForTool(tk, tool, 32);
            b.Image = Icons.ForTool(tk, tool, 16);
            b.AvailabilityClassName = tool.NeedsDocument ? typeof(DocumentAvailable).FullName : typeof(AlwaysAvailable).FullName;
            b.Enabled = true;
            b.Visible = true;
            entry.Visible = true;
        }

        static void Hide(ButtonEntry entry)
        {
            SlotTargets[entry.Slot] = null;
            entry.Visible = false;
            try { entry.Button.Visible = false; } catch (Exception ex) { Log.Error("Hide button", ex); }
        }

        static void SetTabVisible(string tabId, bool visible)
        {
            try
            {
                Autodesk.Windows.RibbonControl ribbon = Autodesk.Windows.ComponentManager.Ribbon;
                Autodesk.Windows.RibbonTab tab = ribbon?.Tabs.FirstOrDefault(t => t.Id == tabId);
                if (tab != null && tab.IsVisible != visible) tab.IsVisible = visible;
            }
            catch (Exception ex)
            {
                Log.Error("Tab visibility " + tabId, ex);
            }
        }

        /// <summary>Id (= name) of the ribbon tab the user is looking at.</summary>
        public static string ActiveTab()
        {
            try { return Autodesk.Windows.ComponentManager.Ribbon?.ActiveTab?.Id; }
            catch { return null; }
        }
    }
}
