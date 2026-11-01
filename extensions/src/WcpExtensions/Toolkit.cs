using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace WcpExtensions
{
    /// <summary>One toolkit folder (has toolkit.json and one .csproj).</summary>
    sealed class Toolkit
    {
        /// <summary>Folder name — the toolkit's key everywhere (settings, buttons).</summary>
        public string Key;
        public string Folder;
        /// <summary>.csproj name; builds are named &lt;ProjectName&gt;_b&lt;stamp&gt;.dll.</summary>
        public string ProjectName;
        public string Name;
        public string Tab;
        public bool Enabled;
        /// <summary>Problem with toolkit.json / the project, if any.</summary>
        public string Problem;

        // ---- Filled in by Reload ----
        public LoadedBuild Loaded;
        public string LoadError;

        public string BinFolder(string year) => Path.Combine(Folder, "bin", year);

        /// <summary>File name of the newest build for this Revit version (from bin\&lt;year&gt;\current.txt), or null.</summary>
        public string CurrentBuildFile(string year)
        {
            try
            {
                string pointer = Path.Combine(BinFolder(year), "current.txt");
                if (!File.Exists(pointer)) return null;
                string name = File.ReadAllText(pointer, Encoding.UTF8).Trim();
                if (name.Length == 0 || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return null;
                return File.Exists(Path.Combine(BinFolder(year), name)) ? name : null;
            }
            catch
            {
                return null;
            }
        }

        public DateTime? CurrentBuildTime(string year)
        {
            string file = CurrentBuildFile(year);
            if (file == null) return null;
            return File.GetLastWriteTime(Path.Combine(BinFolder(year), file));
        }

        public bool HasNewBuild(string year)
        {
            string current = CurrentBuildFile(year);
            return current != null && (Loaded == null || !string.Equals(Loaded.FileName, current, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Reads every toolkit under the root folder.</summary>
        public static List<Toolkit> Scan(Settings settings)
        {
            var list = new List<Toolkit>();
            string root = settings.ToolkitsRoot;
            try
            {
                if (!Directory.Exists(root)) return list;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    if (!File.Exists(Path.Combine(dir, "toolkit.json"))) continue;
                    list.Add(Read(dir, settings));
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not list toolkits in " + root, ex);
            }
            return list.OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        static Toolkit Read(string dir, Settings settings)
        {
            var t = new Toolkit
            {
                Folder = dir,
                Key = Path.GetFileName(dir),
            };
            t.Name = t.Key;
            t.Tab = t.Key;
            t.Enabled = !settings.IsDisabled(t.Key);
            try
            {
                var json = MiniJson.ParseObject(File.ReadAllText(Path.Combine(dir, "toolkit.json"), Encoding.UTF8));
                t.Name = Clean(json.GetString("name"), t.Key);
                t.Tab = Clean(json.GetString("tab"), t.Name);
            }
            catch (Exception ex)
            {
                t.Problem = "toolkit.json is not valid JSON: " + ex.Message;
            }

            string[] projects = Directory.GetFiles(dir, "*.csproj");
            if (projects.Length == 1) t.ProjectName = Path.GetFileNameWithoutExtension(projects[0]);
            else t.Problem = t.Problem ?? (projects.Length == 0 ? "No .csproj file in the folder." : "More than one .csproj file in the folder.");
            return t;
        }

        static string Clean(string value, string fallback)
        {
            value = value?.Trim();
            return string.IsNullOrEmpty(value) ? fallback : value;
        }
    }

    /// <summary>A build of a toolkit that is loaded in this Revit session.</summary>
    sealed class LoadedBuild
    {
        public string FileName;
        public DateTime BuildTime;
        public Assembly Assembly;
        public List<ToolDef> Tools = new List<ToolDef>();
        /// <summary>Public IExternalCommand classes without [WcpButton] (reported, no button).</summary>
        public List<string> NoAttribute = new List<string>();
    }

    /// <summary>One button, read from a [WcpButton] attribute.</summary>
    sealed class ToolDef
    {
        public Type Type;
        public string Text;
        public string Panel = "Tools";
        public string Tooltip;
        public string Icon;
        public int Order;
        public bool NeedsDocument = true;

        public string TypeName => Type.FullName;
    }
}
