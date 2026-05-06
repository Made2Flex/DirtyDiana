using System;
using System.Collections.Generic;
using DirtyDiana.Formatter;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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
                    // Skip root mount
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
                        // Skip user dir
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
                                diskNumber: int.MaxValue
                            ));
                        }
                        catch (Exception ex) when (
                            ex is UnauthorizedAccessException ||
                            ex is IOException ||
                            ex is ArgumentException ||
                            ex is DriveNotFoundException
                        )
                        {
                            // Skip invalid mounts
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
                mountPoint = Path.GetFullPath(mountPoint);

                foreach (var line in File.ReadLines("/proc/self/mounts"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 3)
                        continue;

                    var mount = Path.GetFullPath(parts[1]);
                    var fs = parts[2].ToLowerInvariant();

                    if (!string.Equals(mount, mountPoint, StringComparison.Ordinal))
                        continue;

                    if (fs == "vfat")
                        fs = "fat32";

                    if (fs == "ntfs3" || fs == "fuseblk")
                        fs = "ntfs";

                    return fs;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        internal static string GetDeviceFromMountPoint(string mountPoint)
        {
            try
            {
                mountPoint = Path.GetFullPath(mountPoint);

                foreach (var line in File.ReadLines("/proc/self/mounts"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    var device = parts[0];
                    var mount = Path.GetFullPath(parts[1]);

                    if (!string.Equals(mount, mountPoint, StringComparison.Ordinal))
                        continue;

                    if (device.StartsWith("/dev/nvme") || device.StartsWith("/dev/mmcblk"))
                    {
                        int pIndex = device.LastIndexOf('p');
                        if (pIndex > 0)
                            return device.Substring(0, pIndex);
                    }
                    else
                    {
                        return device.TrimEnd('0','1','2','3','4','5','6','7','8','9');
                    }

                    return device;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        internal static string ResolveWritableMountPoint(string currentMountPoint, string devicePath, int timeoutMs = 3000)
        {
            if (IsWritableDirectory(currentMountPoint))
                return currentMountPoint;

            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

            while (DateTime.UtcNow < deadline)
            {
                var mountedPath = FindMountedPathForDevice(devicePath);
                if (IsWritableDirectory(mountedPath))
                    return mountedPath;

                Thread.Sleep(500);
            }

            TryMountDevice(devicePath);

            deadline = DateTime.UtcNow.AddMilliseconds(3000);
            while (DateTime.UtcNow < deadline)
            {
                var mountedPath = FindMountedPathForDevice(devicePath);
                if (IsWritableDirectory(mountedPath))
                    return mountedPath;

                Thread.Sleep(500);
            }

            return currentMountPoint;
        }

        private static string FindMountedPathForDevice(string devicePath)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
                return string.Empty;

            string normalizedDevice = Path.GetFullPath(devicePath);
            var mountCandidates = new List<string>();

            try
            {
                foreach (var line in File.ReadLines("/proc/self/mounts"))
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2)
                        continue;

                    string mountedDevice = DecodeMountField(parts[0]);
                    string mountPoint = DecodeMountField(parts[1]);

                    if (string.IsNullOrWhiteSpace(mountedDevice) || string.IsNullOrWhiteSpace(mountPoint))
                        continue;

                    bool deviceMatches = string.Equals(mountedDevice, normalizedDevice, StringComparison.Ordinal)
                        || mountedDevice.StartsWith(normalizedDevice, StringComparison.Ordinal);

                    if (!deviceMatches)
                        continue;

                    mountCandidates.Add(mountPoint);
                }
            }
            catch
            {
                return string.Empty;
            }

            return mountCandidates
                .Where(Directory.Exists)
                .OrderByDescending(IsPreferredRemovableMountPath)
                .ThenByDescending(path => path.Length)
                .FirstOrDefault() ?? string.Empty;
        }

        private static bool IsPreferredRemovableMountPath(string path) =>
            path.StartsWith("/run/media/", StringComparison.Ordinal) ||
            path.StartsWith("/media/", StringComparison.Ordinal);

        private static string DecodeMountField(string value) =>
            value
                .Replace("\\040", " ")
                .Replace("\\011", "\t")
                .Replace("\\012", "\n")
                .Replace("\\134", "\\");

        private static bool IsWritableDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;

            string probePath = Path.Combine(path, $".dirtydiana-write-test-{Guid.NewGuid():N}.tmp");

            try
            {
                File.WriteAllText(probePath, "ok");
                File.Delete(probePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void TryMountDevice(string devicePath)
        {
            if (string.IsNullOrWhiteSpace(devicePath))
                return;

            if (RunCommand("udisksctl", $"mount -b {EscapeArg(devicePath)}") == 0)
                return;

            string fallbackRoot = "/mnt/dirtydiana";
            string fallbackMountPoint = Path.Combine(fallbackRoot, Path.GetFileName(devicePath));

            try
            {
                Directory.CreateDirectory(fallbackMountPoint);
            }
            catch
            {
                return;
            }

            _ = RunCommand("mount", $"{EscapeArg(devicePath)} {EscapeArg(fallbackMountPoint)}");
        }

        private static int RunCommand(string command, string arguments)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                if (!process.WaitForExit(2000))
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    return -1;
                }
                return process.ExitCode;
            }
            catch
            {
                return -1;
            }
        }

        private static string EscapeArg(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

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
