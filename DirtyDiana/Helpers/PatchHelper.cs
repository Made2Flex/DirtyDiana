using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace DirtyDiana.Helpers
{
    internal static class PatchHelper
    {
        /// <param name="xexPath">Path to the XEX file</param>
        /// <param name="xexToolPath">Path to XexTool executable</param>
        internal static async Task PatchXexAsync(string xexPath, string xexToolPath)
        {
            if (!File.Exists(xexPath))
                throw new FileNotFoundException($"XEX file not found: {xexPath}");

            if (!File.Exists(xexToolPath))
                throw new FileNotFoundException($"XexTool not found: {xexToolPath}");

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            // On Linux/macOS, see if Wine is installed
            if (!isWindows)
            {
                if (!CommandExists("wine"))
                    throw new Exception("Wine is required to run XexTool on Linux/macOS but was not found in PATH.");
            }

            ProcessStartInfo psi = isWindows
            ? new ProcessStartInfo
            {
                FileName = xexToolPath,
                Arguments = $"-m r -r a \"{xexPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
            : new ProcessStartInfo
            {
                FileName = "wine",
                Arguments = $"\"{xexToolPath}\" -m r -r a \"{xexPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Execute
            using var process = Process.Start(psi);
            if (process == null)
                throw new Exception("Failed to start XexTool process.");

            string stdout = await process.StandardOutput.ReadToEndAsync();
            string stderr = await process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new Exception($"XEX patching failed.\nStdout: {stdout}\nStderr: {stderr}");
        }

        private static bool CommandExists(string command)
        {
            try
            {
                using var p = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "which",
                        Arguments = command,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                p.Start();
                p.WaitForExit();
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
