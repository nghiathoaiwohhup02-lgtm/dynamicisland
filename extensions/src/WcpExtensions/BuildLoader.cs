using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Autodesk.Revit.UI;
#if NETCOREAPP
using System.Runtime.Loader;
#endif

namespace WcpExtensions
{
    /// <summary>
    /// Loads a toolkit build into this Revit session.
    ///
    /// Every build has its own assembly name (Toolkit_b&lt;stamp&gt;.dll), so a new build can always be
    /// loaded next to the old one — that is what makes Reload work without restarting Revit.
    /// The build folder is copied first ("shadow copy") so Revit never locks the files the next
    /// build wants to write.
    /// </summary>
    static class BuildLoader
    {
        static readonly List<string> ShadowFolders = new List<string>();
        static bool _resolverHooked;

        public static LoadedBuild Load(Toolkit toolkit, string year)
        {
            string file = toolkit.CurrentBuildFile(year)
                ?? throw new InvalidOperationException("Not built for Revit " + year + ".");
            string bin = toolkit.BinFolder(year);
            string shadow = Path.Combine(Paths.ShadowRoot(year), Process.GetCurrentProcess().Id.ToString(),
                toolkit.Key, Path.GetFileNameWithoutExtension(file));

            if (!File.Exists(Path.Combine(shadow, file)))
                CopyBuild(bin, shadow, toolkit.ProjectName, file);

            HookResolver();
            lock (ShadowFolders) ShadowFolders.Insert(0, shadow);

            string mainPath = Path.Combine(shadow, file);
            Assembly assembly;
#if NETCOREAPP
            var context = new ToolkitLoadContext(toolkit.Key + "/" + file, shadow);
            assembly = context.LoadFromAssemblyPath(mainPath);
#else
            assembly = Assembly.LoadFrom(mainPath);
#endif
            // Kit.ToolkitFolder / Kit.ToolkitName read these (Assembly.Location points at the shadow copy).
            string asmName = assembly.GetName().Name;
            AppDomain.CurrentDomain.SetData("WcpExtensions.Folder:" + asmName, toolkit.Folder);
            AppDomain.CurrentDomain.SetData("WcpExtensions.Name:" + asmName, toolkit.Name);

            var build = new LoadedBuild
            {
                FileName = file,
                BuildTime = File.GetLastWriteTime(Path.Combine(bin, file)),
                Assembly = assembly,
            };
            FindTools(build);
            return build;
        }

        static void CopyBuild(string bin, string shadow, string projectName, string currentFile)
        {
            string temp = shadow + ".tmp-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            CopyDirectory(bin, temp, top: true, projectName, currentFile);
            if (Directory.Exists(shadow)) Directory.Delete(shadow, true);
            Directory.Move(temp, shadow);
        }

        static void CopyDirectory(string from, string to, bool top, string projectName, string currentFile)
        {
            Directory.CreateDirectory(to);
            foreach (string src in Directory.GetFiles(from))
            {
                string name = Path.GetFileName(src);
                if (top)
                {
                    if (string.Equals(name, "current.txt", StringComparison.OrdinalIgnoreCase)) continue;
                    // Older builds that were not cleaned up yet.
                    if (projectName != null
                        && name.StartsWith(projectName + "_b", StringComparison.OrdinalIgnoreCase)
                        && !name.StartsWith(Path.GetFileNameWithoutExtension(currentFile) + ".", StringComparison.OrdinalIgnoreCase))
                        continue;
                }
                File.Copy(src, Path.Combine(to, name), true);
            }
            foreach (string dir in Directory.GetDirectories(from))
                CopyDirectory(dir, Path.Combine(to, Path.GetFileName(dir)), false, projectName, currentFile);
        }

        /// <summary>Reads [WcpButton] from every public IExternalCommand class.</summary>
        static void FindTools(LoadedBuild build)
        {
            Type[] types;
            try
            {
                types = build.Assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                string first = ex.LoaderExceptions?.FirstOrDefault(e => e != null)?.Message ?? ex.Message;
                throw new InvalidOperationException("Some classes could not be loaded: " + first, ex);
            }

            foreach (Type type in types)
            {
                if (!type.IsClass || type.IsAbstract || !type.IsVisible) continue;
                if (!typeof(IExternalCommand).IsAssignableFrom(type)) continue;

                CustomAttributeData attr = type.GetCustomAttributesData()
                    .FirstOrDefault(a => a.AttributeType.Name == "WcpButtonAttribute");
                if (attr == null)
                {
                    build.NoAttribute.Add(type.FullName);
                    continue;
                }

                var tool = new ToolDef { Type = type, Text = type.Name };
                if (attr.ConstructorArguments.Count > 0 && attr.ConstructorArguments[0].Value is string text && text.Trim().Length > 0)
                    tool.Text = text.Trim();
                foreach (CustomAttributeNamedArgument arg in attr.NamedArguments)
                {
                    object value = arg.TypedValue.Value;
                    switch (arg.MemberName)
                    {
                        case "Panel":
                            if (value is string panel && panel.Trim().Length > 0) tool.Panel = panel.Trim();
                            break;
                        case "Tooltip":
                            tool.Tooltip = value as string;
                            break;
                        case "Icon":
                            tool.Icon = value as string;
                            break;
                        case "Order":
                            if (value is int order) tool.Order = order;
                            break;
                        case "NeedsDocument":
                            if (value is bool needs) tool.NeedsDocument = needs;
                            break;
                    }
                }
                build.Tools.Add(tool);
            }
        }

        /// <summary>Removes shadow copies left by Revit sessions that have ended.</summary>
        public static void CleanOldShadows(string year)
        {
            try
            {
                string root = Paths.ShadowRoot(year);
                if (!Directory.Exists(root)) return;
                foreach (string dir in Directory.GetDirectories(root))
                {
                    if (int.TryParse(Path.GetFileName(dir), out int pid) && IsRunning(pid)) continue;
                    try { Directory.Delete(dir, true); }
                    catch { /* still in use - next time */ }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Could not clean shadow copies.", ex);
            }
        }

        static bool IsRunning(int pid)
        {
            try
            {
                using (Process p = Process.GetProcessById(pid)) return !p.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Last resort for dependencies: look in the shadow folders of loaded builds.</summary>
        static void HookResolver()
        {
            if (_resolverHooked) return;
            _resolverHooked = true;
            AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                try
                {
                    string name = new AssemblyName(args.Name).Name;
                    if (name == null || name.EndsWith(".resources", StringComparison.OrdinalIgnoreCase)) return null;
                    string[] folders;
                    lock (ShadowFolders) folders = ShadowFolders.ToArray();
                    foreach (string folder in folders)
                    {
                        string path = Path.Combine(folder, name + ".dll");
                        if (File.Exists(path)) return Assembly.LoadFrom(path);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("AssemblyResolve " + args.Name, ex);
                }
                return null;
            };
        }
    }

#if NETCOREAPP
    /// <summary>
    /// One load context per loaded build. Revit's own assemblies come from Revit; everything the
    /// toolkit ships in its bin folder is loaded from the shadow copy.
    /// </summary>
    sealed class ToolkitLoadContext : AssemblyLoadContext
    {
        static readonly HashSet<string> FromRevit = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "RevitAPI", "RevitAPIUI", "AdWindows", "UIFramework", "UIFrameworkServices",
            "RevitAPIIFC", "RevitAPIMacros", "RevitAddInUtility", "RevitServices",
        };

        readonly string _folder;

        public ToolkitLoadContext(string name, string folder) : base(name, isCollectible: false)
        {
            _folder = folder;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == null || FromRevit.Contains(assemblyName.Name)) return null;
            string path = Path.Combine(_folder, assemblyName.Name + ".dll");
            return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string file = unmanagedDllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? unmanagedDllName : unmanagedDllName + ".dll";
            foreach (string path in new[]
            {
                Path.Combine(_folder, file),
                Path.Combine(_folder, "runtimes", "win-x64", "native", file),
            })
            {
                if (File.Exists(path)) return LoadUnmanagedDllFromPath(path);
            }
            return IntPtr.Zero;
        }
    }
#endif
}
