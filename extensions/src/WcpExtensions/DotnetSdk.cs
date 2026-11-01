using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WcpExtensions
{
    /// <summary>Looks for a .NET SDK the same way build.ps1 does (without starting a process).</summary>
    static class DotnetSdk
    {
        public static Version Newest()
        {
            var versions = new List<Version>();
            var roots = new List<string>
            {
                Paths.PrivateDotnet,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
            };
            foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            {
                try
                {
                    if (dir.Length > 0 && File.Exists(Path.Combine(dir.Trim('"'), "dotnet.exe"))) roots.Add(dir.Trim('"'));
                }
                catch { }
            }
            foreach (string root in roots)
            {
                try
                {
                    string sdk = Path.Combine(root, "sdk");
                    if (!Directory.Exists(sdk)) continue;
                    foreach (string dir in Directory.GetDirectories(sdk))
                    {
                        string name = Path.GetFileName(dir);
                        int dash = name.IndexOf('-');
                        if (dash > 0) name = name.Substring(0, dash);
                        if (Version.TryParse(name, out Version v)) versions.Add(v);
                    }
                }
                catch { }
            }
            return versions.OrderByDescending(v => v).FirstOrDefault();
        }

        public static string Describe(string year)
        {
            int need = year == "2027" ? 10 : 8;
            Version v = Newest();
            if (v == null || v.Major < need)
                return ".NET SDK " + need + "+ not found — the first build installs it (about 250 MB, a few minutes).";
            return ".NET SDK " + v + " ready.";
        }
    }
}
