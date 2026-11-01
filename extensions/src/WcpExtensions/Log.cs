using System;
using System.IO;
using System.Text;

namespace WcpExtensions
{
    /// <summary>Plain text log in %LOCALAPPDATA%\WCP-Extensions\log.txt. Never throws.</summary>
    static class Log
    {
        static readonly object Gate = new object();

        public static void Info(string message) => Write("INFO ", message);

        public static void Error(string message, Exception ex = null) =>
            Write("ERROR", ex == null ? message : message + Environment.NewLine + ex);

        static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    string file = Paths.LogFile;
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    var info = new FileInfo(file);
                    if (info.Exists && info.Length > 1024 * 1024)
                    {
                        File.Copy(file, file + ".old", true);
                        File.Delete(file);
                    }
                    File.AppendAllText(file,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " [" + Host.RevitYear + "] " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // Logging must never break Revit.
            }
        }
    }
}
