// WCP Extensions kit - do not edit.
using System;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.UI;

namespace __TOOLKIT_ID__
{
    /// <summary>Small helpers shared by every tool in this toolkit.</summary>
    public static class Kit
    {
        static string AssemblyName => typeof(Kit).Assembly.GetName().Name;

        /// <summary>Toolkit name as shown on the ribbon tab.</summary>
        public static string ToolkitName =>
            (AppDomain.CurrentDomain.GetData("WcpExtensions.Name:" + AssemblyName) as string) ?? "__TOOLKIT_NAME__";

        /// <summary>
        /// The toolkit folder (where toolkit.json is). Use this instead of Assembly.Location:
        /// Revit runs a copy of the build from another folder.
        /// </summary>
        public static string ToolkitFolder =>
            (AppDomain.CurrentDomain.GetData("WcpExtensions.Folder:" + AssemblyName) as string)
            ?? Path.GetDirectoryName(typeof(Kit).Assembly.Location);

        /// <summary>Full path of a file inside the toolkit folder, e.g. Kit.FilePath("Data/rules.csv").</summary>
        public static string FilePath(string relativePath) =>
            Path.Combine(ToolkitFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>
        /// pack:// URI of a WPF Resource compiled into this toolkit (Build Action "Resource"),
        /// e.g. Kit.Resource("Tools/Hello/logo.png"). Never type the assembly name yourself:
        /// it changes with every build.
        /// </summary>
        public static Uri Resource(string relativePath) =>
            new Uri("pack://application:,,,/" + AssemblyName + ";component/" + relativePath.TrimStart('/').Replace('\\', '/'), UriKind.Absolute);

        /// <summary>Shows a WPF window as a dialog on top of Revit. Returns DialogResult.</summary>
        public static bool? ShowDialog(Window window, UIApplication app)
        {
            new WindowInteropHelper(window).Owner = app.MainWindowHandle;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowInTaskbar = false;
            return window.ShowDialog();
        }

        /// <summary>Simple message box.</summary>
        public static void Info(string title, string message)
        {
            var dialog = new TaskDialog(title) { MainContent = message, TitleAutoPrefix = false };
            dialog.Show();
        }

        /// <summary>Yes / No question. Returns true for Yes.</summary>
        public static bool Confirm(string title, string question)
        {
            var dialog = new TaskDialog(title)
            {
                MainContent = question,
                TitleAutoPrefix = false,
                CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No,
                DefaultButton = TaskDialogResult.No,
            };
            return dialog.Show() == TaskDialogResult.Yes;
        }
    }
}
