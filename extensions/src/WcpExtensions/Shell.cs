using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace WcpExtensions
{
    /// <summary>Explorer, clipboard and Recycle Bin.</summary>
    static class Shell
    {
        public static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error("Open folder " + path, ex);
            }
        }

        public static void OpenFile(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error("Open file " + path, ex);
            }
        }

        public static bool CopyText(string text)
        {
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(60); // clipboard busy in another program
                }
            }
            return false;
        }

        /// <summary>Moves a folder to the Recycle Bin (so a deleted toolkit can be restored).</summary>
        public static bool Recycle(string folder, out string error)
        {
            error = null;
            var op = new SHFILEOPSTRUCT
            {
                wFunc = FO_DELETE,
                pFrom = Path.GetFullPath(folder) + "\0",
                fFlags = FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI,
            };
            int result = SHFileOperation(ref op);
            if (result != 0 || op.fAnyOperationsAborted || Directory.Exists(folder))
            {
                error = result != 0
                    ? "Windows could not move the folder to the Recycle Bin (code 0x" + result.ToString("X") + "). Close programs that use files in it and try again."
                    : "The folder is still there. Close programs that use files in it and try again.";
                return false;
            }
            return true;
        }

        const uint FO_DELETE = 3;
        const ushort FOF_SILENT = 0x0004;
        const ushort FOF_NOCONFIRMATION = 0x0010;
        const ushort FOF_ALLOWUNDO = 0x0040;
        const ushort FOF_NOERRORUI = 0x0400;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
            [MarshalAs(UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [MarshalAs(UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHFileOperation(ref SHFILEOPSTRUCT lpFileOp);
    }
}
