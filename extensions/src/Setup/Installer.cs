using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace WcpExtensions.Setup
{
    /// <summary>Per-user install of the WCP Extensions add-in for Revit 2024-2027. No admin rights.</summary>
    static class Installer
    {
        public static readonly string[] Years = { "2024", "2025", "2026", "2027" };

        const string AddinFile = "WcpExtensions.addin";
        const string AddinFolder = "WcpExtensions";
        const string AddInId = "4F051520-A552-4FDF-87EE-11C8DF846ADF";
        const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WcpExtensions";

        public static string Version
        {
            get
            {
                var info = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(
                    typeof(Installer).Assembly, typeof(AssemblyInformationalVersionAttribute));
                string v = info?.InformationalVersion ?? typeof(Installer).Assembly.GetName().Version.ToString(3);
                int plus = v.IndexOf('+');
                return plus > 0 ? v.Substring(0, plus) : v;
            }
        }

        static string AppData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        public static string LocalRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WCP-Extensions");

        public static string ToolkitsRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WCP-Extensions");

        static string SettingsFile => Path.Combine(AppData, "WCP-Extensions", "settings.json");

        public static string AddinsDir(string year) => Path.Combine(AppData, "Autodesk", "Revit", "Addins", year);

        public static bool IsInstalled(string year) => File.Exists(Path.Combine(AddinsDir(year), AddinFile));

        public static bool AnyInstalled => Years.Any(IsInstalled);

        /// <summary>Best guess whether this Revit version is on the PC.</summary>
        public static bool RevitFound(string year)
        {
            try
            {
                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pd = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (File.Exists(Path.Combine(pf, "Autodesk", "Revit " + year, "Revit.exe"))) return true;
                if (Directory.Exists(Path.Combine(pd, "Autodesk", "Revit", "Addins", year))) return true;
                if (Directory.Exists(AddinsDir(year))) return true;
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Autodesk\Revit"))
                {
                    if (key != null && key.GetSubKeyNames().Any(n => n.IndexOf(year, StringComparison.Ordinal) >= 0)) return true;
                }
            }
            catch
            {
                // Detection is only a hint.
            }
            return false;
        }

        /// <summary>Full WCP Tools (WCP_Loader.addin / L00_Stub) has its own Extensions.</summary>
        public static bool FullWcpInstalled(string year)
        {
            try
            {
                string folder = AddinsDir(year);
                if (!Directory.Exists(folder)) return false;
                if (File.Exists(Path.Combine(folder, "WCP_Loader.addin"))) return true;
                foreach (string file in Directory.GetFiles(folder, "*.addin"))
                {
                    string text = File.ReadAllText(file);
                    if (text.IndexOf("L00_Stub", StringComparison.OrdinalIgnoreCase) >= 0
                        || text.IndexOf("L00_Loader", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
            }
            catch
            {
            }
            return false;
        }

        public static bool RevitRunning()
        {
            Process[] revit = Process.GetProcessesByName("Revit");
            foreach (Process p in revit) p.Dispose();
            return revit.Length > 0;
        }

        public static bool HasPayload => typeof(Installer).Assembly.GetManifestResourceNames().Contains("payload.zip");

        public static void Install(IList<string> years, bool runAlongsideWcp, Action<string> log)
        {
            if (!HasPayload) throw new InvalidOperationException("This Setup.exe has no add-in inside (developer build). Use the one from GitHub Actions.");

            using (Stream stream = typeof(Installer).Assembly.GetManifestResourceStream("payload.zip"))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (string year in years)
                {
                    string target = Path.Combine(AddinsDir(year), AddinFolder);
                    log("Revit " + year + ": copying the add-in to " + target);
                    Directory.CreateDirectory(target);
                    int files = 0;
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string name = entry.FullName.Replace('\\', '/');
                        if (!name.StartsWith(year + "/", StringComparison.Ordinal) || name.EndsWith("/", StringComparison.Ordinal)) continue;
                        string relative = name.Substring(year.Length + 1).Replace('/', Path.DirectorySeparatorChar);
                        string path = Path.Combine(target, relative);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using (Stream input = entry.Open())
                        using (FileStream output = File.Create(path))
                            input.CopyTo(output);
                        files++;
                    }
                    if (files == 0) throw new InvalidOperationException("The installer has no files for Revit " + year + ".");
                    File.WriteAllText(Path.Combine(AddinsDir(year), AddinFile), AddinManifest(), new UTF8Encoding(true));
                }
            }

            SaveRunAlongsideWcp(runAlongsideWcp);
            Directory.CreateDirectory(ToolkitsRoot);
            RegisterUninstall(log);
            log("Done.");
        }

        static string AddinManifest() =>
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<RevitAddIns>\r\n" +
            "  <AddIn Type=\"Application\">\r\n" +
            "    <Name>WCP Extensions</Name>\r\n" +
            "    <Assembly>" + AddinFolder + "\\WcpExtensions.dll</Assembly>\r\n" +
            "    <AddInId>" + AddInId + "</AddInId>\r\n" +
            "    <FullClassName>WcpExtensions.App</FullClassName>\r\n" +
            "    <VendorId>WHBIM</VendorId>\r\n" +
            "    <VendorDescription>WH BIM Team - WCP Extensions " + Version + "</VendorDescription>\r\n" +
            "  </AddIn>\r\n" +
            "</RevitAddIns>\r\n";

        /// <summary>Keeps other settings; only sets runAlongsideWcp.</summary>
        static void SaveRunAlongsideWcp(bool value)
        {
            Dictionary<string, object> settings;
            try
            {
                settings = File.Exists(SettingsFile)
                    ? MiniJson.ParseObject(File.ReadAllText(SettingsFile, Encoding.UTF8))
                    : new Dictionary<string, object>();
            }
            catch
            {
                settings = new Dictionary<string, object>();
            }
            settings["runAlongsideWcp"] = value;
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, MiniJson.Write(settings), new UTF8Encoding(false));
        }

        static void RegisterUninstall(Action<string> log)
        {
            try
            {
                Directory.CreateDirectory(LocalRoot);
                string self = Assembly.GetExecutingAssembly().Location;
                string copy = Path.Combine(LocalRoot, "Setup.exe");
                if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase))
                    File.Copy(self, copy, true);

                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(UninstallKey))
                {
                    key.SetValue("DisplayName", "WCP Extensions");
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", "WH BIM Team");
                    key.SetValue("InstallLocation", LocalRoot);
                    key.SetValue("DisplayIcon", copy);
                    key.SetValue("UninstallString", "\"" + copy + "\" /uninstall");
                    key.SetValue("QuietUninstallString", "\"" + copy + "\" /uninstall /quiet");
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    key.SetValue("EstimatedSize", 2048, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                log("Note: could not add WCP Extensions to Windows' app list (" + ex.Message + "). Uninstall by running Setup again.");
            }
        }

        /// <summary>Removes the add-in for every Revit version. Toolkits and settings stay.</summary>
        public static void Uninstall(Action<string> log)
        {
            foreach (string year in Years)
            {
                string addin = Path.Combine(AddinsDir(year), AddinFile);
                string folder = Path.Combine(AddinsDir(year), AddinFolder);
                if (!File.Exists(addin) && !Directory.Exists(folder)) continue;
                log("Revit " + year + ": removing the add-in");
                if (File.Exists(addin)) File.Delete(addin);
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }

            try
            {
                string shadow = Path.Combine(LocalRoot, "shadow");
                if (Directory.Exists(shadow)) Directory.Delete(shadow, true);
            }
            catch
            {
                // Leftover copies are harmless.
            }

            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false); }
            catch { }

            string copy = Path.Combine(LocalRoot, "Setup.exe");
            string self = Assembly.GetExecutingAssembly().Location;
            if (File.Exists(copy))
            {
                if (string.Equals(Path.GetFullPath(self), Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase))
                {
                    // Cannot delete a running exe: let cmd do it a moment after we exit.
                    Process.Start(new ProcessStartInfo("cmd.exe", "/c ping 127.0.0.1 -n 3 > nul & del /q \"" + copy + "\"")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        WindowStyle = ProcessWindowStyle.Hidden,
                    });
                }
                else
                {
                    try { File.Delete(copy); } catch { }
                }
            }
            log("Done. Your toolkits in " + ToolkitsRoot + " were kept.");
        }
    }
}
