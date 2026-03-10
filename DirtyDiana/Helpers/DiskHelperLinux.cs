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

            string[] mountRoots = new[]
            {
                "/media",
                "/run/media"
            };

            foreach (var root in mountRoots)
            {
                string[] userDirs;

                try
                {
                    if (!Directory.Exists(root))
                        continue;

                    userDirs = Directory.GetDirectories(root);
                }
                catch (Exception ex) when (
                    ex is UnauthorizedAccessException ||
                    ex is IOException
                )
                {
                    // Skip root mount point if we cannot list it
                    continue;
                }

                foreach (var userDir in userDirs)
                {
                    string[] mountDirs;

                    try
                    {
                        mountDirs = Directory.GetDirectories(userDir);
                    }
                    catch (Exception ex) when (
                        ex is UnauthorizedAccessException ||
                        ex is IOException
                    )
                    {
                        // Skip user dir if we cannot enumerate it
                        continue;
                    }

                    foreach (var mountDir in mountDirs)
                    {
                        try
                        {
                            if (!Directory.Exists(mountDir))
                                continue;

                            // Prevent duplicates
                            if (disks.Exists(d => d.DriveLetter == mountDir))
                                continue;

                            var driveInfo = new DriveInfo(mountDir);

                            long totalSize = driveInfo.TotalSize;
                            long availableFreeSpace = driveInfo.AvailableFreeSpace;

                            disks.Add(new DiskInfo(
                                driveLetter: mountDir,
                                type: "Removable",
                                totalSize: totalSize,
                                volumeLabel: Path.GetFileName(mountDir),
                                availableFreeSpace: availableFreeSpace,
                                diskNumber: int.MaxValue // Unix mounts do not expose disk index
                            ));
                        }
                        catch (Exception ex) when (
                            ex is UnauthorizedAccessException ||
                            ex is IOException ||
                            ex is ArgumentException ||
                            ex is DriveNotFoundException
                        )
                        {
                            // Skip inaccessible or invalid mounts
                            continue;
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
            catch (Exception ex) when (
                ex is UnauthorizedAccessException ||
                ex is IOException ||
                ex is ArgumentException
            )
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
