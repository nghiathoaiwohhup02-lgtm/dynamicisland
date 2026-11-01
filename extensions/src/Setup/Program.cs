using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;

namespace WcpExtensions.Setup
{
    static class Program
    {
        /// <summary>
        /// WCP Extensions Setup.exe              window
        /// WCP Extensions Setup.exe /quiet       install for every Revit version found, no window
        /// WCP Extensions Setup.exe /uninstall   remove (asks first; add /quiet to skip the question)
        /// WCP Extensions Setup.exe /screenshot file.png   picture of the window (used by CI)
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            int shot = Array.FindIndex(args, a => Is(a, "screenshot"));
            if (shot >= 0 && shot + 1 < args.Length) return Screenshot(args[shot + 1]);

            bool quiet = args.Any(a => Is(a, "quiet") || Is(a, "q"));
            bool uninstall = args.Any(a => Is(a, "uninstall") || Is(a, "u"));

            if (quiet)
            {
                try
                {
                    if (Installer.RevitRunning()) return 2;
                    if (uninstall)
                    {
                        Installer.Uninstall(_ => { });
                    }
                    else
                    {
                        var years = Installer.Years.Where(y => Installer.RevitFound(y) || Installer.IsInstalled(y)).ToList();
                        if (years.Count == 0) return 3;
                        Installer.Install(years, false, _ => { });
                    }
                    return 0;
                }
                catch
                {
                    return 1;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new SetupForm())
            {
                if (uninstall) form.Shown += (s, e) => form.BeginInvoke(new Action(() => form.StartUninstall()));
                Application.Run(form);
            }
            return 0;
        }

        static int Screenshot(string path)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var form = new SetupForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-32000, -32000);
                form.ShowInTaskbar = false;
                form.Show();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
                    bitmap.Save(path, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        static bool Is(string arg, string name) =>
            string.Equals(arg.TrimStart('/', '-'), name, StringComparison.OrdinalIgnoreCase);
    }
}
