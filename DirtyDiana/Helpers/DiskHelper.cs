using DirtyDiana.Models;
using DirtyDiana.Formatter;
using System.Runtime.InteropServices;
using Spectre.Console;

namespace DirtyDiana.Helpers
{
    internal static class DiskHelper
    {
        internal static List<DiskInfo> GetDisks()
        {
            var disks = new List<DiskInfo>();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    string driveLetter = drive.Name;
                    string volumeLabel = drive.VolumeLabel;
                    string type = drive.DriveType.ToString();
                    long totalSize = drive.TotalSize;
                    long availableFreeSpace = drive.AvailableFreeSpace;
                    int diskNumber = 2;

                    disks.Add(new DiskInfo(driveLetter, type, totalSize, volumeLabel, availableFreeSpace, diskNumber));
                }
            }

            return disks;
        }

        internal static string FormatDisk(DiskInfo disk)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "[-] Architecture not supported.";

            #if WINDOWS
            return DiskFormatter.FormatVolume(disk.DriveLetter[0], disk.TotalSize);
            #else
            return "[-] Architecture not supported.";
            #endif
        }
    }
}
