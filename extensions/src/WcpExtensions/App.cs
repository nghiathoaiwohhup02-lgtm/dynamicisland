using System;
using System.IO;
using System.Linq;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace WcpExtensions
{
    /// <summary>Revit add-in entry point (WcpExtensions.addin points here).</summary>
    public sealed class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            Host.RevitYear = application.ControlledApplication.VersionNumber;
            try
            {
                Settings settings = Settings.Load();
                if (!settings.RunAlongsideWcp && FullWcpInstalled(Host.RevitYear))
                {
                    Log.Info("Full WCP Tools is installed for Revit " + Host.RevitYear +
                             " (it has its own Extensions) - WCP Extensions stays hidden. " +
                             "Set \"runAlongsideWcp\": true in " + Paths.SettingsFile + " to show it anyway.");
                    return Result.Succeeded;
                }

                RibbonApi ribbon = RibbonApi.From(application);
                RibbonManager.CreateMain(ribbon);
                ToolkitHost.Startup(ribbon);
                application.Idling += OnIdling;
                Log.Info("WCP Extensions " + typeof(App).Assembly.GetName().Version + " started.");
            }
            catch (Exception ex)
            {
                Log.Error("Startup failed.", ex);
            }
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                application.Idling -= OnIdling;
                ToolkitHost.Shutdown();
            }
            catch (Exception ex)
            {
                Log.Error("Shutdown", ex);
            }
            return Result.Succeeded;
        }

        /// <summary>When a build finishes, put a dot on the Reload buttons (cheap unless something changed).</summary>
        static void OnIdling(object sender, IdlingEventArgs e)
        {
            if (!ToolkitHost.BuildChanged) return;
            ToolkitHost.BuildChanged = false;
            try
            {
                RibbonManager.SetReloadDot(ToolkitHost.AnyNewBuild());
            }
            catch (Exception ex)
            {
                Log.Error("Idling", ex);
            }
        }

        /// <summary>Full WCP Tools installs WCP_Loader.addin (pointing at L00_Stub.dll).</summary>
        static bool FullWcpInstalled(string year)
        {
            try
            {
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Autodesk", "Revit", "Addins", year);
                if (!Directory.Exists(folder)) return false;
                if (File.Exists(Path.Combine(folder, "WCP_Loader.addin"))) return true;
                return Directory.GetFiles(folder, "*.addin").Any(f =>
                {
                    try
                    {
                        string text = File.ReadAllText(f);
                        return text.IndexOf("L00_Stub", StringComparison.OrdinalIgnoreCase) >= 0
                            || text.IndexOf("L00_Loader", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    catch
                    {
                        return false;
                    }
                });
            }
            catch
            {
                return false;
            }
        }
    }
}
