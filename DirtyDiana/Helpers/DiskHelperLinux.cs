using System;
using System.Collections.Generic;
using System.IO;
using DirtyDiana.Models;

namespace DirtyDiana.Helpers
{
    internal static class DiskHelperUnix
    {
        internal static List<DiskInfo> GetDisks()
        {
            var disks = new List<DiskInfo>();

            // Common Linux removable media mount locations
            string[] mountRoots = new[]
            {
                "/media",
                "/run/media"
            };

            foreach (var root in mountRoots)
            {
                if (!Directory.Exists(root))
                    continue;

                foreach (var userDir in Directory.GetDirectories(root))
                {
                    foreach (var mountDir in Directory.GetDirectories(userDir))
                    {
                        try
                        {
                            var driveInfo = new DriveInfo(mountDir);

                            long totalSize = driveInfo.TotalSize;
                            long availableFreeSpace = driveInfo.AvailableFreeSpace;

                            disks.Add(new DiskInfo(
                                driveLetter: mountDir,
                                type: "Fixed",
                                totalSize: totalSize,
                                volumeLabel: Path.GetFileName(mountDir),
                                                   availableFreeSpace: availableFreeSpace,
                                                   diskNumber: int.MaxValue
                            ));
                        }
                        catch
                        {
                            // Skip inaccessible mounts
                        }
                    }
                }
            }

            return disks;
        }

        internal static string FormatDisk(DiskInfo disk)
        {
            return $"{disk.DriveLetter} ({FormatSize(disk.TotalSize)}) - {disk.Type}";
        }

        internal static string GetFilesystemType(string mountPoint)
        {
            try
            {
                var drive = new DriveInfo(mountPoint);
                return drive.DriveFormat;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string FormatSize(long bytes)
        {
            const double KB = 1024.0;
            const double MB = KB * 1024;
            const double GB = MB * 1024;
            const double TB = GB * 1024;

            if (bytes >= TB) return $"{bytes / TB:F2} TB";
            if (bytes >= GB) return $"{bytes / GB:F2} GB";
            if (bytes >= MB) return $"{bytes / MB:F2} MB";
            if (bytes >= KB) return $"{bytes / KB:F2} KB";

            return $"{bytes} bytes";
        }
    }
}
