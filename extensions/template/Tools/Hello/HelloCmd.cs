using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace __TOOLKIT_ID__
{
    /// <summary>
    /// The smallest possible tool: one class, one button.
    /// Try it: change the message below, build, then press Reload in Revit.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [WcpButton("Hello", Panel = "Start here", Order = 1,
        Tooltip = "Says hello and counts the selected elements. Change the text in Tools\\Hello\\HelloCmd.cs, build, then press Reload.")]
    public class HelloCmd : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            int selected = uidoc.Selection.GetElementIds().Count;

            Kit.Info("Hello",
                "Hello from " + Kit.ToolkitName + "!\n\n" +
                "Model: " + doc.Title + "\n" +
                "Selected elements: " + selected + "\n\n" +
                "Edit Tools\\Hello\\HelloCmd.cs, build, then press Reload to see your change.");

            return Result.Succeeded;
        }
    }
}
