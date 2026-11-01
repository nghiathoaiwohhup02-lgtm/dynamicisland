using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace WcpExtensions
{
    /// <summary>Reload: load the newest build of every toolkit.</summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class ReloadCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ReloadReport report = ToolkitHost.Reload(RibbonApi.From(commandData.Application));
                Dialog.ShowReport(report, commandData.Application.MainWindowHandle);
            }
            catch (Exception ex)
            {
                Log.Error("Reload", ex);
                Dialog.Error("Reload failed", ex.Message, ex.ToString(), null);
            }
            return Result.Succeeded;
        }
    }

    /// <summary>Toolkits: the manager window.</summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class ManagerCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            return ManagerWindow.Open(commandData.Application, null);
        }
    }

    /// <summary>Build (on a toolkit tab): build that tab's toolkits, then Reload.</summary>
    [Transaction(TransactionMode.Manual)]
    public sealed class BuildCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string tab = RibbonManager.ActiveTab();
            List<string> keys = ToolkitHost.Toolkits
                .Where(t => t.Enabled && t.Problem == null && string.Equals(t.Tab, tab, StringComparison.Ordinal))
                .Select(t => t.Key)
                .ToList();
            return ManagerWindow.Open(commandData.Application, keys.Count > 0 ? keys : null);
        }
    }

    /// <summary>Button works with or without an open model.</summary>
    public sealed class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) => true;
    }

    /// <summary>Button works only when a model is open.</summary>
    public sealed class DocumentAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories) =>
            applicationData.ActiveUIDocument != null;
    }
}
