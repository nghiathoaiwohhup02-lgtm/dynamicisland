using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace WcpExtensions
{
    /// <summary>Creates a new toolkit folder from the template embedded in this DLL.</summary>
    static class TemplateWriter
    {
        const string Prefix = "template/";

        static readonly string[] RevitTabs =
        {
            "Architecture", "Structure", "Steel", "Precast", "Systems", "Insert", "Annotate", "Analyze",
            "Massing & Site", "Collaborate", "View", "Manage", "Add-Ins", "Modify", "Quick Access",
        };

        static readonly HashSet<string> ReservedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "System", "Autodesk", "Microsoft", "Kit", "WcpExtensions", "Revit", "Windows", "Tools",
        };

        /// <summary>Error message for a toolkit name, or null when the name is fine.</summary>
        public static string Validate(string name, Settings settings)
        {
            name = (name ?? "").Trim();
            if (name.Length < 2) return "Type a name (at least 2 characters).";
            if (name.Length > 40) return "Keep the name under 40 characters.";
            foreach (char c in name)
                if (!(char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_'))
                    return "Use letters, digits, spaces, - and _ only.";
            if (!char.IsLetter(name[0])) return "Start the name with a letter.";
            if (string.Equals(name, RibbonManager.MainTab, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("WCP-Manage", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("WCP-Design", StringComparison.OrdinalIgnoreCase)
                || RevitTabs.Any(t => string.Equals(t, name, StringComparison.OrdinalIgnoreCase)))
                return "\"" + name + "\" is already a ribbon tab. Pick another name.";

            string id = MakeId(name);
            if (Directory.Exists(Path.Combine(settings.ToolkitsRoot, id)))
                return "A toolkit folder \"" + id + "\" already exists.";
            if (Toolkit.Scan(settings).Any(t => string.Equals(t.Tab, name, StringComparison.OrdinalIgnoreCase)))
                return "Another toolkit already uses the tab \"" + name + "\".";
            return null;
        }

        /// <summary>"Công cụ của Thoại" → "CongCuCuaThoai": folder, project and C# namespace.</summary>
        public static string MakeId(string name)
        {
            string plain = RemoveDiacritics(name ?? "");
            var sb = new StringBuilder();
            bool upperNext = true;
            foreach (char c in plain)
            {
                if (c < 128 && char.IsLetterOrDigit(c))
                {
                    sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                }
                else
                {
                    upperNext = true;
                }
            }
            string id = sb.ToString();
            if (id.Length == 0) id = "MyToolkit";
            if (char.IsDigit(id[0])) id = "T" + id;
            if (id.Length > 40) id = id.Substring(0, 40);
            if (ReservedIds.Contains(id)) id += "Toolkit";
            return id;
        }

        static string RemoveDiacritics(string text)
        {
            text = text.Replace('đ', 'd').Replace('Đ', 'D');
            var sb = new StringBuilder();
            foreach (char c in text.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }

        /// <summary>Writes the toolkit and returns its folder.</summary>
        public static string Create(string name, Settings settings)
        {
            name = name.Trim();
            string id = MakeId(name);
            string folder = Path.Combine(settings.ToolkitsRoot, id);
            string temp = folder + ".creating";
            if (Directory.Exists(temp)) Directory.Delete(temp, true);

            var values = new Dictionary<string, string>
            {
                { "__TOOLKIT_ID__", id },
                { "__TOOLKIT_NAME__", name },
                { "__AUTHOR__", Environment.UserName },
                { "__DATE__", DateTime.Now.ToString("yyyy-MM-dd") },
            };

            Assembly asm = typeof(TemplateWriter).Assembly;
            int written = 0;
            foreach (string resource in asm.GetManifestResourceNames())
            {
                string normalized = resource.Replace('\\', '/');
                if (!normalized.StartsWith(Prefix, StringComparison.Ordinal)) continue;
                string relative = Fill(normalized.Substring(Prefix.Length), values);
                string target = Path.Combine(temp, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(target));

                string text;
                using (Stream stream = asm.GetManifestResourceStream(resource))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                    text = reader.ReadToEnd();
                text = Fill(text, values);
                text = text.Replace("\r\n", "\n").Replace("\n", "\r\n"); // Windows line endings

                string ext = Path.GetExtension(target).ToLowerInvariant();
                // C#, XAML and PowerShell files get a BOM (Windows PowerShell needs it for non-ASCII text);
                // .cmd must not have one.
                bool bom = ext == ".cs" || ext == ".xaml" || ext == ".ps1";
                File.WriteAllText(target, text, new UTF8Encoding(bom));
                written++;
            }
            if (written == 0)
                throw new InvalidOperationException("The toolkit template is missing from WcpExtensions.dll. Reinstall WCP Extensions.");

            Directory.Move(temp, folder);
            Log.Info("Created toolkit " + folder);
            return folder;
        }

        static string Fill(string text, Dictionary<string, string> values)
        {
            foreach (KeyValuePair<string, string> kv in values) text = text.Replace(kv.Key, kv.Value);
            return text;
        }
    }
}
