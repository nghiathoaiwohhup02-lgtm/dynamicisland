using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WcpExtensions
{
    /// <summary>%APPDATA%\WCP-Extensions\settings.json (per user, all Revit versions).</summary>
    sealed class Settings
    {
        /// <summary>Folder whose sub-folders are toolkits.</summary>
        public string ToolkitsRoot = Paths.DefaultToolkitsRoot;

        /// <summary>Toolkit folder names that are switched off.</summary>
        public List<string> Disabled = new List<string>();

        /// <summary>Show WCP Extensions even when full WCP Tools is installed (set by Setup).</summary>
        public bool RunAlongsideWcp;

        public static Settings Load()
        {
            var s = new Settings();
            try
            {
                if (File.Exists(Paths.SettingsFile))
                {
                    var o = MiniJson.ParseObject(File.ReadAllText(Paths.SettingsFile, Encoding.UTF8));
                    string root = o.GetString("toolkitsRoot");
                    if (!string.IsNullOrWhiteSpace(root)) s.ToolkitsRoot = Environment.ExpandEnvironmentVariables(root);
                    s.Disabled = o.GetStrings("disabled");
                    s.RunAlongsideWcp = o.GetBool("runAlongsideWcp", false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not read " + Paths.SettingsFile + " - using defaults.", ex);
            }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Paths.SettingsFile));
                // Keep keys written by others (e.g. Setup) that this version does not know.
                Dictionary<string, object> o;
                try
                {
                    o = File.Exists(Paths.SettingsFile)
                        ? MiniJson.ParseObject(File.ReadAllText(Paths.SettingsFile, Encoding.UTF8))
                        : new Dictionary<string, object>();
                }
                catch
                {
                    o = new Dictionary<string, object>();
                }
                o["toolkitsRoot"] = ToolkitsRoot;
                o["disabled"] = Disabled;
                o["runAlongsideWcp"] = RunAlongsideWcp;
                string tmp = Paths.SettingsFile + ".tmp";
                File.WriteAllText(tmp, MiniJson.Write(o), new UTF8Encoding(false));
                if (File.Exists(Paths.SettingsFile)) File.Replace(tmp, Paths.SettingsFile, null);
                else File.Move(tmp, Paths.SettingsFile);
            }
            catch (Exception ex)
            {
                Log.Error("Could not save settings.", ex);
            }
        }

        public bool IsDisabled(string key) =>
            Disabled.Exists(d => string.Equals(d, key, StringComparison.OrdinalIgnoreCase));

        public void SetDisabled(string key, bool disabled)
        {
            Disabled.RemoveAll(d => string.Equals(d, key, StringComparison.OrdinalIgnoreCase));
            if (disabled) Disabled.Add(key);
        }
    }
}
