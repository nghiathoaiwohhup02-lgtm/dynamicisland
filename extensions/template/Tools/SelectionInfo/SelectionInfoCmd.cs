using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace __TOOLKIT_ID__
{
    /// <summary>
    /// A tool with a window: counts elements by category, then selects the category you pick.
    /// Files: SelectionInfoCmd.cs (Revit work), SelectionInfoWindow.xaml(.cs) (the window).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [WcpButton("Selection\nInfo", Panel = "Start here", Order = 2,
        Tooltip = "Counts elements by category in the selection (or the active view if nothing is selected). Pick a row and press Select.")]
    public class SelectionInfoCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
            bool useSelection = selectedIds.Count > 0;

            IEnumerable<Element> source;
            try
            {
                source = useSelection
                    ? selectedIds.Select(doc.GetElement)
                    : new FilteredElementCollector(doc, doc.ActiveView.Id).WhereElementIsNotElementType().ToElements();
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                Kit.Info("Selection Info", "This view cannot list elements. Open a plan, section or 3D view, or select some elements first.");
                return Result.Cancelled;
            }

            List<CategoryRow> rows = source
                .Where(e => e != null && e.Category != null)
                .GroupBy(e => e.Category.Name)
                .Select(g => new CategoryRow(g.Key, g.Select(e => e.Id).ToList()))
                .OrderByDescending(r => r.Count)
                .ThenBy(r => r.Category)
                .ToList();

            if (rows.Count == 0)
            {
                Kit.Info("Selection Info", "Nothing to count here.");
                return Result.Cancelled;
            }

            string scope = useSelection ? "Selection" : "Active view: " + doc.ActiveView.Name;
            var window = new SelectionInfoWindow(scope, rows);
            if (Kit.ShowDialog(window, commandData.Application) == true && window.Chosen != null)
            {
                uidoc.Selection.SetElementIds(window.Chosen.Ids);
            }
            return Result.Succeeded;
        }
    }

    /// <summary>One row in the window: a category and its elements.</summary>
    public class CategoryRow
    {
        public CategoryRow(string category, List<ElementId> ids)
        {
            Category = category;
            Ids = ids;
        }

        public string Category { get; }
        public List<ElementId> Ids { get; }
        public int Count => Ids.Count;
    }
}
