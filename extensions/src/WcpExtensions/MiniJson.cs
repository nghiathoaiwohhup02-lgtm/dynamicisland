using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WcpExtensions
{
    /// <summary>
    /// Tiny JSON reader/writer (objects, arrays, strings, numbers, true/false/null), so the add-in
    /// needs no JSON library on .NET Framework 4.8. Objects are Dictionary&lt;string, object&gt;,
    /// arrays List&lt;object&gt;, numbers double.
    /// </summary>
    static class MiniJson
    {
        public static object Parse(string text)
        {
            var p = new Parser(text);
            p.SkipWs();
            object value = p.ReadValue();
            p.SkipWs();
            if (!p.End) throw new FormatException("Unexpected text after JSON value at " + p.Pos);
            return value;
        }

        public static Dictionary<string, object> ParseObject(string text) =>
            Parse(text) as Dictionary<string, object> ?? throw new FormatException("JSON is not an object.");

        public static string GetString(this Dictionary<string, object> o, string key, string fallback = null) =>
            o != null && o.TryGetValue(key, out object v) && v is string s ? s : fallback;

        public static bool GetBool(this Dictionary<string, object> o, string key, bool fallback) =>
            o != null && o.TryGetValue(key, out object v) && v is bool b ? b : fallback;

        public static List<string> GetStrings(this Dictionary<string, object> o, string key)
        {
            var list = new List<string>();
            if (o != null && o.TryGetValue(key, out object v) && v is List<object> items)
                foreach (object item in items)
                    if (item is string s) list.Add(s);
            return list;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value, int indent)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case string s: WriteString(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> obj:
                    if (obj.Count == 0) { sb.Append("{}"); break; }
                    sb.Append("{\n");
                    int n = 0;
                    foreach (var kv in obj)
                    {
                        sb.Append(' ', (indent + 1) * 2);
                        WriteString(sb, kv.Key);
                        sb.Append(": ");
                        WriteValue(sb, kv.Value, indent + 1);
                        if (++n < obj.Count) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', indent * 2).Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    var items = new List<object>();
                    foreach (object item in list) items.Add(item);
                    if (items.Count == 0) { sb.Append("[]"); break; }
                    sb.Append("[\n");
                    for (int k = 0; k < items.Count; k++)
                    {
                        sb.Append(' ', (indent + 1) * 2);
                        WriteValue(sb, items[k], indent + 1);
                        if (k < items.Count - 1) sb.Append(',');
                        sb.Append('\n');
                    }
                    sb.Append(' ', indent * 2).Append(']');
                    break;
                default: WriteString(sb, Convert.ToString(value, CultureInfo.InvariantCulture)); break;
            }
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Parser
        {
            readonly string _s;
            public int Pos;

            public Parser(string s)
            {
                _s = s ?? "";
                // Skip a UTF-8 BOM if the file was read without stripping it.
                if (_s.Length > 0 && _s[0] == '﻿') Pos = 1;
            }

            public bool End => Pos >= _s.Length;

            public void SkipWs()
            {
                while (!End && char.IsWhiteSpace(_s[Pos])) Pos++;
            }

            char Peek => End ? '\0' : _s[Pos];

            public object ReadValue()
            {
                SkipWs();
                char c = Peek;
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't') { Expect("true"); return true; }
                if (c == 'f') { Expect("false"); return false; }
                if (c == 'n') { Expect("null"); return null; }
                if (c == '-' || char.IsDigit(c)) return ReadNumber();
                throw new FormatException("Unexpected character '" + c + "' at " + Pos);
            }

            void Expect(string word)
            {
                if (string.CompareOrdinal(_s, Pos, word, 0, word.Length) != 0)
                    throw new FormatException("Expected '" + word + "' at " + Pos);
                Pos += word.Length;
            }

            Dictionary<string, object> ReadObject()
            {
                var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                Pos++; // {
                SkipWs();
                if (Peek == '}') { Pos++; return obj; }
                while (true)
                {
                    SkipWs();
                    if (Peek != '"') throw new FormatException("Expected a property name at " + Pos);
                    string key = ReadString();
                    SkipWs();
                    if (Peek != ':') throw new FormatException("Expected ':' at " + Pos);
                    Pos++;
                    obj[key] = ReadValue();
                    SkipWs();
                    if (Peek == ',') { Pos++; continue; }
                    if (Peek == '}') { Pos++; return obj; }
                    throw new FormatException("Expected ',' or '}' at " + Pos);
                }
            }

            List<object> ReadArray()
            {
                var list = new List<object>();
                Pos++; // [
                SkipWs();
                if (Peek == ']') { Pos++; return list; }
                while (true)
                {
                    list.Add(ReadValue());
                    SkipWs();
                    if (Peek == ',') { Pos++; continue; }
                    if (Peek == ']') { Pos++; return list; }
                    throw new FormatException("Expected ',' or ']' at " + Pos);
                }
            }

            string ReadString()
            {
                var sb = new StringBuilder();
                Pos++; // "
                while (true)
                {
                    if (End) throw new FormatException("Unterminated string");
                    char c = _s[Pos++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw new FormatException("Unterminated string");
                    char e = _s[Pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (Pos + 4 > _s.Length) throw new FormatException("Bad \\u escape");
                            sb.Append((char)int.Parse(_s.Substring(Pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            Pos += 4;
                            break;
                        default: throw new FormatException("Bad escape \\" + e);
                    }
                }
            }

            double ReadNumber()
            {
                int start = Pos;
                while (!End && "+-0123456789.eE".IndexOf(_s[Pos]) >= 0) Pos++;
                return double.Parse(_s.Substring(start, Pos - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
