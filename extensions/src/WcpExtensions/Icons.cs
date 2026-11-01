using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WcpExtensions
{
    /// <summary>Ribbon icons drawn in code (no image files to ship) at 96 DPI.</summary>
    static class Icons
    {
        static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();

        // Segoe MDL2 Assets glyphs (Windows 10/11).
        const string GlyphPuzzle = "";
        const string GlyphRefresh = "";
        const string GlyphRepair = "";

        static readonly Color Blue = Color.FromRgb(0x0A, 0x84, 0xFF);
        static readonly Color Indigo = Color.FromRgb(0x5E, 0x5C, 0xE6);
        static readonly Color Orange = Color.FromRgb(0xF5, 0x8A, 0x07);
        static readonly Color Red = Color.FromRgb(0xFF, 0x3B, 0x30);

        static readonly Color[] Palette =
        {
            Color.FromRgb(0x0A, 0x84, 0xFF), Color.FromRgb(0x30, 0x9C, 0xB8), Color.FromRgb(0x28, 0xA7, 0x45),
            Color.FromRgb(0xE8, 0x7D, 0x0D), Color.FromRgb(0xE0, 0x35, 0x5A), Color.FromRgb(0xA0, 0x4E, 0xD6),
            Color.FromRgb(0x5E, 0x5C, 0xE6), Color.FromRgb(0x8E, 0x6C, 0x4A),
        };

        public static ImageSource Toolkits(int size) => Glyph(GlyphPuzzle, Indigo, size, false);

        public static ImageSource Reload(int size, bool dot) => Glyph(GlyphRefresh, Blue, size, dot);

        public static ImageSource Build(int size) => Glyph(GlyphRepair, Orange, size, false);

        /// <summary>The tool's PNG if it has one that loads, otherwise an initials tile.</summary>
        public static ImageSource ForTool(Toolkit toolkit, ToolDef tool, int size)
        {
            if (!string.IsNullOrWhiteSpace(tool.Icon))
            {
                try
                {
                    string path = Path.Combine(toolkit.Folder, tool.Icon.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(path)) return FromFile(path, size);
                    Log.Info("Icon not found: " + path);
                }
                catch (Exception ex)
                {
                    Log.Error("Icon " + tool.Icon, ex);
                }
            }
            return Initials(tool.Text, size);
        }

        static ImageSource FromFile(string path, int size)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;          // do not keep the file open
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            // Redraw at 96 DPI so a 72-DPI PNG does not come out too big on the ribbon.
            return Render(size, dc => dc.DrawImage(image, new Rect(0, 0, size, size)));
        }

        public static ImageSource Initials(string text, int size)
        {
            string letters = InitialsOf(text);
            string key = "init|" + letters + "|" + size + "|" + text;
            if (Cache.TryGetValue(key, out ImageSource cached)) return cached;
            Color color = Palette[StableHash(text) % Palette.Length];
            ImageSource image = Render(size, dc =>
            {
                Tile(dc, color, size);
                double em = size * (letters.Length > 1 ? 0.42 : 0.52);
                var ft = Text(letters, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), em);
                dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
            });
            Cache[key] = image;
            return image;
        }

        static ImageSource Glyph(string glyph, Color color, int size, bool dot)
        {
            string key = "glyph|" + glyph + "|" + color + "|" + size + "|" + dot;
            if (Cache.TryGetValue(key, out ImageSource cached)) return cached;
            ImageSource image = Render(size, dc =>
            {
                Tile(dc, color, size);
                var ft = Text(glyph, new Typeface("Segoe MDL2 Assets"), size * 0.56);
                dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
                if (dot)
                {
                    double r = size * 0.19;
                    dc.DrawEllipse(new SolidColorBrush(Red), new Pen(Brushes.White, Math.Max(1, size / 16.0)),
                        new Point(size - r - 0.5, r + 0.5), r, r);
                }
            });
            Cache[key] = image;
            return image;
        }

        static void Tile(DrawingContext dc, Color color, int size)
        {
            double radius = size * 0.22;
            dc.DrawRoundedRectangle(new SolidColorBrush(color), null, new Rect(0.5, 0.5, size - 1, size - 1), radius, radius);
        }

        static FormattedText Text(string text, Typeface typeface, double em) =>
            new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, em, Brushes.White, 1.0);

        static BitmapSource Render(int size, Action<DrawingContext> draw)
        {
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen()) draw(dc);
            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        static string InitialsOf(string text)
        {
            string[] words = (text ?? "?").Split(new[] { ' ', '\n', '\r', '\t', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return "?";
            if (words.Length == 1) return words[0].Substring(0, 1).ToUpperInvariant();
            return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();
        }

        /// <summary>Same number for the same text in every session (string.GetHashCode is not).</summary>
        static int StableHash(string text)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char c in text ?? "") h = (h ^ c) * 16777619;
                return (int)(h & 0x7FFFFFFF);
            }
        }
    }
}
