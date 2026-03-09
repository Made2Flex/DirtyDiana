using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DirtyDiana.Utilities
{
    internal static class OpenTerminalLinux
    {
        /// <param name="exePath">The full path to the running executable.</param>
        /// <returns>True if launched in terminal emulator, false otherwise.</returns>
        public static bool TryOpenInTerminal(string exePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return false;

            if (string.IsNullOrWhiteSpace(exePath) || !System.IO.File.Exists(exePath))
                return false;

            string[] terminals = { "xfce4-terminal", "gnome-terminal", "konsole", "xterm" };
            string? terminal = null;

            foreach (var term in terminals)
            {
                if (CommandExists(term))
                {
                    terminal = term;
                    break;
                }
            }

            if (terminal == null)
                return false;

            // Build launch arguments for each terminal
            string[] args = Environment.GetCommandLineArgs();
            string joinedArgs = string.Join(" ", args.Skip(1).Select(arg => "\"" + arg.Replace("\"", "\\\"") + "\""));

            ProcessStartInfo psi;

            if (terminal == "gnome-terminal")
            {
                psi = new ProcessStartInfo
                {
                    FileName = "gnome-terminal",
                    Arguments = $"-- bash -c '{QuoteCmd(exePath)} {joinedArgs}; exec bash'",
                    UseShellExecute = false,
                    WorkingDirectory = Environment.CurrentDirectory
                };
            }
            else if (terminal == "xfce4-terminal")
            {
                psi = new ProcessStartInfo
                {
                    FileName = "xfce4-terminal",
                    Arguments = $"--hold -e \"{QuoteCmd(exePath)} {joinedArgs}\"",
                    UseShellExecute = false,
                    WorkingDirectory = Environment.CurrentDirectory
                };
            }
            else if (terminal == "konsole")
            {
                psi = new ProcessStartInfo
                {
                    FileName = "konsole",
                    Arguments = $"--hold -e {QuoteCmd(exePath)} {joinedArgs}",
                    UseShellExecute = false,
                    WorkingDirectory = Environment.CurrentDirectory
                };
            }
            else // xterm fallback
            {
                psi = new ProcessStartInfo
                {
                    FileName = "xterm",
                    Arguments = $"-hold -e {QuoteCmd(exePath)} {joinedArgs}",
                    UseShellExecute = false,
                    WorkingDirectory = Environment.CurrentDirectory
                };
            }

            try
            {
                Process.Start(psi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // Helper to quote a file path for safe shell usage
        private static string QuoteCmd(string path)
        {
            return "\"" + path.Replace("\"", "\\\"") + "\"";
        }

        // Checks if a command exists in the system's PATH
        private static bool CommandExists(string command)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "which",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null)
                    return false;
                string? output = process.StandardOutput.ReadLine();
                process.WaitForExit();
                return !string.IsNullOrEmpty(output);
            }
            catch
            {
                return false;
            }
        }
    }
}