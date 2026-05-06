using System.Runtime.InteropServices;

internal static class LinuxPrivilegeHelper
{
    [DllImport("libc")]
    private static extern uint geteuid();

    internal static void EnsureRootOrExit()
    {
        if (!OperatingSystem.IsLinux())
            return;

        if (geteuid() != 0)
        {
            Console.WriteLine("[ERROR] This operation requires root privileges.");
            Console.WriteLine("Please re-run with: sudo ./DirtyDiana");
            Environment.Exit(1);
        }
    }
}