using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace WcpExtensions
{
    /// <summary>Small message windows in the WCP Extensions style (instead of TaskDialog).</summary>
    static class Dialog
    {
        static ResourceDictionary _styles;

        // Relative component URI: works inside Revit, where no WPF Application object exists.
        public static ResourceDictionary Styles =>
            _styles ?? (_styles = (ResourceDictionary)Application.LoadComponent(
                new Uri("/WcpExtensions;component/UI/Styles.xaml", UriKind.Relative)));

        public static void Info(string title, string message, IntPtr owner = default(IntPtr))
        {
            Show(Build(title, message, null, null, "OK", null, false), owner);
        }

        public static bool Confirm(string title, string message, string yes, bool danger, IntPtr owner = default(IntPtr))
        {
            return Show(Build(title, message, null, null, yes, "Cancel", danger), owner);
        }

        /// <summary>Error with details; "Copy for Claude" puts a ready-made prompt on the clipboard.</summary>
        public static void Error(string title, string summary, string details, string forClaude, IntPtr owner = default(IntPtr))
        {
            Show(Build(title, summary, details, forClaude, "Close", null, false), owner);
        }

        public static void ShowReport(ReloadReport report, IntPtr owner)
        {
            Show(BuildReport(report), owner);
        }

        internal static Window BuildReport(ReloadReport report)
        {
            string title = report.AnyChange ? "Reloaded" : "Reload";
            string message = report.Toolkits.Count == 0
                ? "No toolkits yet.\n\nClick Toolkits (tab " + RibbonManager.MainTab + ") and create one — it comes with two sample tools to learn from."
                : report.Summary() + ".\n\n" + report.Details();
            return Build(title, message, null, null, "OK", null, false);
        }

        static bool Show(Window window, IntPtr owner)
        {
            if (owner == IntPtr.Zero) owner = Process.GetCurrentProcess().MainWindowHandle;
            if (owner != IntPtr.Zero) new WindowInteropHelper(window).Owner = owner;
            else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            window.ShowDialog();
            return window.Tag is bool ok && ok;
        }

        /// <summary>Makes the window without showing it. Tag becomes true when the main button is pressed.</summary>
        internal static Window Build(string title, string message, string details, string forClaude,
                                     string okText, string cancelText, bool danger)
        {
            ResourceDictionary styles = Styles;
            var window = new Window
            {
                Title = "WCP Extensions",
                Width = details != null ? 640 : 460,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Tag = false,
            };
            window.Resources.MergedDictionaries.Add(styles);
            window.Background = (Brush)styles["Wx.B.Page"];

            var root = new StackPanel { Margin = new Thickness(20, 18, 20, 18) };
            root.Children.Add(new TextBlock
            {
                Text = title,
                Style = (Style)styles["Wx.Heading"],
                FontSize = 17,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 2, 10),
            });

            var card = new Border { Style = (Style)styles["Wx.Card"] };
            card.Child = new TextBox
            {
                Text = message,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Foreground = (Brush)styles["Wx.B.Text"],
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 360,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            root.Children.Add(card);

            if (details != null)
            {
                root.Children.Add(new Border
                {
                    Background = (Brush)styles["Wx.B.Console"],
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 10, 0, 0),
                    Child = new TextBox { Text = details, Style = (Style)styles["Wx.Console"], Height = 220 },
                });
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0),
            };

            if (forClaude != null)
            {
                var copy = new Button
                {
                    Content = "Copy for Claude",
                    Style = (Style)styles["Wx.Button"],
                    Margin = new Thickness(0, 0, 8, 0),
                    ToolTip = "Copies the error and where your toolkit is, ready to paste into Claude.",
                };
                copy.Click += (s, e) =>
                {
                    copy.Content = Shell.CopyText(forClaude) ? "Copied — paste it into Claude" : "Clipboard busy, try again";
                };
                buttons.Children.Add(copy);
            }
            if (cancelText != null)
            {
                buttons.Children.Add(new Button
                {
                    Content = cancelText,
                    Style = (Style)styles["Wx.Button"],
                    IsCancel = true,
                    MinWidth = 84,
                    Margin = new Thickness(0, 0, 8, 0),
                });
            }
            var ok = new Button
            {
                Content = okText,
                Style = (Style)styles[danger ? "Wx.Danger" : (cancelText != null ? "Wx.Primary" : "Wx.Button")],
                MinWidth = 84,
                IsCancel = cancelText == null,
            };
            ok.Click += (s, e) =>
            {
                window.Tag = true;
                window.Close();
            };
            buttons.Children.Add(ok);
            root.Children.Add(buttons);

            window.Content = root;
            window.Loaded += (s, e) => ok.Focus();
            return window;
        }
    }
}
