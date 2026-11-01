using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WcpExtensions.UiSmoke
{
    /// <summary>
    /// Builds every WCP Extensions window outside Revit (XAML errors show up here, not in front of a
    /// user) and saves a PNG of each, so the layout can be checked without Revit.
    /// </summary>
    static class Program
    {
        static int _failures;
        static string _out;

        [STAThread]
        static int Main(string[] args)
        {
            _out = Path.GetFullPath(args.Length > 0 ? args[0] : "ui-shots");
            Directory.CreateDirectory(_out);
            Host.RevitYear = "2025";

            string root = Path.Combine(Path.GetTempPath(), "wcpx-smoke", "toolkits");
            if (Directory.Exists(root)) Directory.Delete(root, true);
            Directory.CreateDirectory(root);
            Settings settings = Settings.Load();
            settings.ToolkitsRoot = root;
            settings.Disabled = new List<string> { "OldKit" };
            settings.Save();
            ToolkitHost.Settings = settings;

            Step("Create toolkits from the embedded template", () =>
            {
                TemplateWriter.Create("Thoai Tools", settings);
                TemplateWriter.Create("Survey Kit", settings);
                TemplateWriter.Create("Old Kit", settings);
                if (!File.Exists(Path.Combine(root, "ThoaiTools", "CLAUDE.md"))) throw new Exception("CLAUDE.md missing");
                // "Thoai Tools" has a build waiting to be loaded.
                string bin = Path.Combine(root, "ThoaiTools", "bin", "2025");
                Directory.CreateDirectory(bin);
                File.WriteAllText(Path.Combine(bin, "ThoaiTools_b261101120000.dll"), "");
                File.WriteAllText(Path.Combine(bin, "current.txt"), "ThoaiTools_b261101120000.dll");
            });

            Step("Styles.xaml loads", () =>
            {
                ResourceDictionary s = Dialog.Styles;
                foreach (string key in new[] { "Wx.Primary", "Wx.Button", "Wx.Danger", "Wx.Card", "Wx.Console", "Wx.B.Page", "Wx.B.Danger" })
                    if (s[key] == null) throw new Exception("missing " + key);
            });

            Step("Ribbon icons", () =>
            {
                var strip = new StackPanel { Orientation = Orientation.Horizontal, Background = Brushes.White };
                foreach (ImageSource img in new[]
                {
                    Icons.Toolkits(32), Icons.Reload(32, false), Icons.Reload(32, true), Icons.Build(32),
                    Icons.Initials("Hello", 32), Icons.Initials("Selection\nInfo", 32), Icons.Initials("Count Walls", 32),
                    Icons.Toolkits(16), Icons.Reload(16, true), Icons.Initials("Hello", 16),
                })
                    strip.Children.Add(new Image { Source = img, Width = img.Width, Height = img.Height, Margin = new Thickness(8), Stretch = Stretch.None });
                Save(strip, "ribbon-icons.png", double.NaN, double.NaN, Brushes.White);
            });

            Step("Toolkits window", () => Shot(new ManagerWindow(null), "toolkits.png"));

            Step("Reload report", () =>
            {
                var report = new ReloadReport("2025");
                ToolkitReport a = report.For(new Toolkit { Key = "ThoaiTools", Name = "Thoai Tools", Tab = "Thoai Tools" });
                a.Status = "Loaded new build 15:42:10";
                a.Changed = true;
                a.Tools = 3;
                a.NewButtons = 1;
                ToolkitReport b = report.For(new Toolkit { Key = "SurveyKit", Name = "Survey Kit", Tab = "Survey Kit" });
                b.Status = "Not built for Revit 2025 yet — open Toolkits and press Build & Reload.";
                report.Warnings.Add("Thoai Tools: ThoaiTools.DraftCmd has no [WcpButton], so it gets no button.");
                Shot(Dialog.BuildReport(report), "reload-report.png");
            });

            Step("Error dialog", () => Shot(Dialog.Build(
                "Count Walls stopped with an error",
                "NullReferenceException: Object reference not set to an instance of an object.",
                "System.NullReferenceException: Object reference not set to an instance of an object.\r\n" +
                "   at ThoaiTools.CountWallsCmd.Execute(ExternalCommandData commandData, String& message, ElementSet elements) in C:\\Users\\me\\WCP-Extensions\\ThoaiTools\\Tools\\CountWalls\\CountWallsCmd.cs:line 21\r\n" +
                "   at WcpExtensions.ToolkitHost.RunSlot(Int32 slot, ExternalCommandData data, String& message, ElementSet elements)",
                "prompt", "Close", null, false), "error.png"));

            Step("Delete confirmation", () => Shot(Dialog.Build(
                "Delete \"Survey Kit\"?",
                "The folder moves to the Recycle Bin, so you can still restore it from there.\n\nC:\\Users\\me\\WCP-Extensions\\SurveyKit\n\nIts tab disappears from Revit.",
                null, null, "Delete", "Cancel", true), "delete.png"));

            Console.WriteLine(_failures == 0 ? "UI smoke test OK" : _failures + " step(s) failed");
            return _failures == 0 ? 0 : 1;
        }

        static void Step(string name, Action action)
        {
            try
            {
                action();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                _failures++;
                Console.WriteLine("FAIL " + name + ": " + ex);
            }
        }

        /// <summary>Lays the window's content out at the window's size and saves a PNG (no screen needed).</summary>
        static void Shot(Window window, string file)
        {
            var content = (FrameworkElement)window.Content;
            Brush background = window.Background;
            double width = double.IsNaN(window.Width) ? 460 : window.Width;
            double height = window.SizeToContent == SizeToContent.Height || double.IsNaN(window.Height) ? double.NaN : window.Height;
            ResourceDictionary resources = window.Resources;
            window.Content = null;
            var host = new Border { Child = content, Background = background, Width = width, Resources = resources };
            if (!double.IsNaN(height)) host.Height = height;
            host.SetValue(TextElement.FontFamilyProperty, window.FontFamily);
            host.SetValue(TextElement.FontSizeProperty, window.FontSize);
            host.SetValue(TextElement.ForegroundProperty, window.Foreground);
            Save(host, file, width, height, background);
            window.Close();
        }

        static void Save(FrameworkElement element, string file, double width, double height, Brush background)
        {
            var available = new Size(double.IsNaN(width) ? double.PositiveInfinity : width, double.IsNaN(height) ? double.PositiveInfinity : height);
            element.Measure(available);
            Size size = element.DesiredSize;
            if (!double.IsNaN(width)) size.Width = width;
            if (!double.IsNaN(height)) size.Height = height;
            element.Arrange(new Rect(size));
            element.UpdateLayout();

            const double scale = 1.5; // sharper pictures
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(background ?? Brushes.White, null, new Rect(size));
                dc.DrawRectangle(new VisualBrush(element), null, new Rect(size));
            }
            bitmap.Render(visual);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (FileStream stream = File.Create(Path.Combine(_out, file))) encoder.Save(stream);
            Console.WriteLine("  saved " + file + " (" + (int)size.Width + "x" + (int)size.Height + ")");
        }
    }
}
