using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Autodesk.Revit.UI
{
    public enum Result { Failed = -1, Succeeded = 0, Cancelled = 1 }

    public class RibbonPanel { public string Name { get; set; } public bool Visible { get; set; } }

    public class UIControlledApplication
    {
        public void CreateRibbonTab(string tabName) { }
        public RibbonPanel CreateRibbonPanel(string tabName, string panelName) => new RibbonPanel { Name = panelName };
        public List<RibbonPanel> GetRibbonPanels(string tabName) => new List<RibbonPanel>();
    }

    public class UIApplication
    {
        public IntPtr MainWindowHandle => IntPtr.Zero;
        public void CreateRibbonTab(string tabName) { }
        public RibbonPanel CreateRibbonPanel(string tabName, string panelName) => new RibbonPanel { Name = panelName };
        public List<RibbonPanel> GetRibbonPanels(string tabName) => new List<RibbonPanel>();
    }

    public class ExternalCommandData { public UIApplication Application { get; set; } }

    public interface IExternalCommand { Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements); }

    public interface IExternalApplication
    {
        Result OnStartup(UIControlledApplication application);
        Result OnShutdown(UIControlledApplication application);
    }

    public interface IExternalCommandAvailability { bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories); }
}
