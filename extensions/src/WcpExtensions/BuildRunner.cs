using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace WcpExtensions
{
    /// <summary>Runs a toolkit's build.ps1 in the background and streams its output.</summary>
    sealed class BuildRunner
    {
        Process _process;

        /// <summary>Raised on a worker thread for each output line.</summary>
        public event Action<string> Line;

        /// <summary>Raised on a worker thread with the exit code (0 = OK).</summary>
        public event Action<int> Done;

        public void Start(string toolkitFolder, string year)
        {
            string powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!File.Exists(powershell)) powershell = "powershell.exe";
            var psi = new ProcessStartInfo
            {
                FileName = powershell,
                Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                            Path.Combine(toolkitFolder, "build.ps1") + "\" -Revit " + year,
                WorkingDirectory = toolkitFolder,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };
            // Revit's own .NET / MSBuild settings must not leak into the build.
            foreach (string name in new[] { "MSBUILD_EXE_PATH", "MSBuildExtensionsPath", "MSBuildSDKsPath", "DOTNET_HOST_PATH", "MSBuildLoadMicrosoftTargetsReadOnly" })
                psi.EnvironmentVariables.Remove(name);

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += (s, e) => { if (e.Data != null) Line?.Invoke(e.Data); };
            _process.ErrorDataReceived += (s, e) => { if (e.Data != null) Line?.Invoke(e.Data); };
            _process.Exited += (s, e) =>
            {
                int code = -1;
                try
                {
                    _process.WaitForExit(); // flushes the remaining output lines
                    code = _process.ExitCode;
                }
                catch { }
                Done?.Invoke(code);
            };
            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        /// <summary>Stops the build and everything it started (dotnet, MSBuild).</summary>
        public void Cancel()
        {
            try
            {
                if (_process == null || _process.HasExited) return;
                using (Process kill = Process.Start(new ProcessStartInfo("taskkill.exe", "/T /F /PID " + _process.Id)
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                }))
                {
                    kill?.WaitForExit(5000);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Cancel build", ex);
            }
        }
    }
}
