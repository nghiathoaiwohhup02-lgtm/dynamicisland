using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using DynamicIsland.Core;
using Forms = System.Windows.Forms;

namespace DynamicIsland.Shell;

/// <summary>Biểu tượng ở khay hệ thống: ẩn/hiện đảo, đưa về giữa, mở cấu hình, khởi động cùng Windows, thoát.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private readonly Icon _iconImage;

    public TrayIcon(Action toggleVisible, Action resetPosition, Action restart, Action exit)
    {
        _iconImage = CreateIcon();

        var startup = new Forms.ToolStripMenuItem("Khởi động cùng Windows") { CheckOnClick = true };
        try { startup.Checked = StartupRegistration.IsEnabled; } catch { }
        startup.CheckedChanged += (_, _) =>
        {
            try { StartupRegistration.Set(startup.Checked); }
            catch (Exception ex) { Log.Error("Không ghi được khóa Run", ex); }
        };

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Ẩn / hiện đảo", null, (_, _) => toggleVisible());
        menu.Items.Add("Đưa đảo về giữa màn hình", null, (_, _) => resetPosition());
        menu.Items.Add("Mở file cấu hình", null, (_, _) => OpenSettingsFile());
        menu.Items.Add("Khởi động lại (áp dụng cấu hình)", null, (_, _) => restart());
        menu.Items.Add(startup);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Thoát", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Icon = _iconImage,
            Text = "Dynamic Island",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == Forms.MouseButtons.Left) toggleVisible();
        };
    }

    public static void OpenSettingsFile()
    {
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{SettingsStore.FilePath}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Không mở được file cấu hình", ex);
        }
    }

    // Vẽ biểu tượng viên thuốc đen bằng mã để khỏi phải kèm file .ico.
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using var path = new GraphicsPath();
            var r = new RectangleF(2, 9, 28, 14);
            path.AddArc(r.X, r.Y, r.Height, r.Height, 90, 180);
            path.AddArc(r.Right - r.Height, r.Y, r.Height, r.Height, 270, 180);
            path.CloseFigure();
            using var fill = new SolidBrush(Color.Black);
            using var outline = new Pen(Color.FromArgb(200, 255, 255, 255), 1.5f);
            g.FillPath(fill, path);
            g.DrawPath(outline, path);
            using var dot = new SolidBrush(Color.FromArgb(255, 48, 209, 88));
            g.FillEllipse(dot, 21, 13, 6, 6);
        }

        var handle = bitmap.GetHicon();
        var icon = (Icon)Icon.FromHandle(handle).Clone();
        NativeMethods.DestroyIcon(handle);
        return icon;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _iconImage.Dispose();
    }
}
