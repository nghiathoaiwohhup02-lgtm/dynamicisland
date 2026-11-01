using System;
using System.IO;

namespace WcpExtensions
{
    /// <summary>Facts about the Revit session we run in (no Revit types, so UI code and tests can use it).</summary>
    static class Host
    {
        /// <summary>"2024" … "2027".</summary>
        public static string RevitYear = "";
    }

    /// <summary>Where WCP Extensions keeps things on this PC.</summary>
    static class Paths
    {
        /// <summary>%LOCALAPPDATA%\WCP-Extensions — log, shadow copies, private .NET SDK, Setup.exe.</summary>
        public static string LocalRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WCP-Extensions");

        /// <summary>%APPDATA%\WCP-Extensions\settings.json</summary>
        public static string SettingsFile =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WCP-Extensions", "settings.json");

        /// <summary>Default folder for toolkits: %USERPROFILE%\WCP-Extensions (outside OneDrive on purpose).</summary>
        public static string DefaultToolkitsRoot =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WCP-Extensions");

        public static string LogFile => Path.Combine(LocalRoot, "log.txt");

        public static string ShadowRoot(string year) => Path.Combine(LocalRoot, "shadow", year);

        public static string PrivateDotnet => Path.Combine(LocalRoot, "dotnet");

        /// <summary>Folder holding WcpExtensions.dll (…\Addins\&lt;year&gt;\WcpExtensions).</summary>
        public static string AddinFolder => Path.GetDirectoryName(typeof(Paths).Assembly.Location);
    }
}
