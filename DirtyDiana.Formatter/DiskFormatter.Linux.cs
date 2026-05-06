#pragma warning disable CA1416

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

using static DirtyDiana.Formatter.Constants;
using static DirtyDiana.Formatter.Utilities;

namespace DirtyDiana.Formatter
{
    public static class DiskFormatter
    {
        // libc / ioctl

        private const int O_RDWR = 2;
        private const int O_SYNC = 0x101000;

        private const ulong BLKSSZGET = 0x1268;
        private const ulong BLKGETSIZE64 = 0x80081272;

        [DllImport("libc", SetLastError = true)]
        private static extern int open(string pathname, int flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern int ioctl(int fd, ulong request, ref int data);

        [DllImport("libc", SetLastError = true)]
        private static extern int ioctl(int fd, ulong request, ref long data);

        // Entry Point

        public static unsafe string FormatVolume(string devicePath, long diskSize)
        {
            uint volumeID = GetVolumeID();

            SafeFileHandle driveHandle = OpenDeviceHandle(devicePath);
            if (driveHandle.IsInvalid)
                return Error("Unable to open device. errno: " + Marshal.GetLastWin32Error());

            if (!EnableExtendedDASDIO(driveHandle) || !LockDevice(driveHandle))
                return Error($"Failed to initialize device access. errno: {Marshal.GetLastWin32Error()}");

            DISK_GEOMETRY diskGeometry;
            if (!TryGetDiskGeometry(driveHandle, out diskGeometry))
                return Error($"Failed to get disk geometry. errno: {Marshal.GetLastWin32Error()}");

            PARTITION_INFORMATION partitionInfo;
            bool isGPT = false;

            if (!TryGetPartitionInfo(driveHandle, ref diskGeometry, out partitionInfo, out isGPT))
                return Error($"Failed to get partition information. errno: {Marshal.GetLastWin32Error()}");

            uint totalSectors = (uint)(partitionInfo.PartitionLength / diskGeometry.BytesPerSector);

            if (!IsValidFAT32Size(totalSectors))
                return Error("Invalid drive size for FAT32.");

            FAT32BootSector bootSector = InitializeBootSector(diskGeometry, partitionInfo, totalSectors, volumeID);
            FAT32FsInfoSector fsInfo = InitializeFsInfo();
            uint[] firstFATSector = InitializeFirstFATSector(diskGeometry.BytesPerSector);

            string formatOutput = FormatVolumeData(
                driveHandle,
                diskGeometry,
                bootSector,
                fsInfo,
                firstFATSector,
                isGPT,
                partitionInfo
            );

            if (formatOutput != string.Empty)
                return formatOutput;

            if (!UnlockDevice(driveHandle) || !DismountVolume(devicePath))
                return Error($"Failed to release device.");

            driveHandle.Dispose();

            return string.Empty;
        }

        // Device Access

        private static SafeFileHandle OpenDeviceHandle(string devicePath)
        {
            int fd = open(devicePath, O_RDWR | O_SYNC);
            return new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        }

        private static bool EnableExtendedDASDIO(SafeFileHandle handle) => true;

        private static bool LockDevice(SafeFileHandle handle)
        {
            return true;
        }

        private static bool UnlockDevice(SafeFileHandle handle) => true;

        private static bool DismountVolume(string devicePath)
        {
            try
            {
                System.Diagnostics.Process.Start("umount", devicePath)?.WaitForExit();
            }
            catch { }

            return true;
        }

        // Geometry

        private static bool TryGetDiskGeometry(SafeFileHandle handle, out DISK_GEOMETRY diskGeometry)
        {
            diskGeometry = new DISK_GEOMETRY();

            int fd = handle.DangerousGetHandle().ToInt32();

            int sectorSize = 0;
            long totalSize = 0;

            if (ioctl(fd, BLKSSZGET, ref sectorSize) != 0)
                return false;

            if (ioctl(fd, BLKGETSIZE64, ref totalSize) != 0)
                return false;

            diskGeometry.BytesPerSector = (uint)sectorSize;
            diskGeometry.SectorsPerTrack = 63;
            diskGeometry.TracksPerCylinder = 255;
            diskGeometry.Cylinders = totalSize / sectorSize;

            return true;
        }

        private static bool TryGetPartitionInfo(
            SafeFileHandle handle,
            ref DISK_GEOMETRY diskGeometry,
            out PARTITION_INFORMATION partitionInfo,
            out bool isGPT)
        {
            partitionInfo = new PARTITION_INFORMATION();
            isGPT = false;

            int fd = handle.DangerousGetHandle().ToInt32();
            long totalSize = 0;

            if (ioctl(fd, BLKGETSIZE64, ref totalSize) != 0)
                return false;

            partitionInfo.StartingOffset = 0;
            partitionInfo.PartitionLength = totalSize;
            partitionInfo.HiddenSectors = 0;

            return true;
        }

        private static bool IsValidFAT32Size(uint totalSectors) =>
            totalSectors >= 65536 && totalSectors < 0xFFFFFFFF;

        private static FAT32BootSector InitializeBootSector(DISK_GEOMETRY diskGeometry, PARTITION_INFORMATION partitionInfo, uint totalSectors, uint volumeID)
        {
            uint sectorsPerCluster = (uint)CalculateSectorsPerCluster((ulong)partitionInfo.PartitionLength, diskGeometry.BytesPerSector);
            uint fatSize = CalculateFATSize(totalSectors, 32, sectorsPerCluster, 2, diskGeometry.BytesPerSector);

            uint aligned = (uint)MB / diskGeometry.BytesPerSector;
            uint sysAreaSize = ((34 * fatSize + aligned - 1) / aligned) * aligned;
            uint reserved = sysAreaSize - 2 * fatSize;

            FAT32BootSector bootSector = new FAT32BootSector
            {
                BytesPerSector = (ushort)diskGeometry.BytesPerSector,
                SectorsPerCluster = (byte)sectorsPerCluster,
                ReservedSectorCount = (ushort)reserved,
                NumberOfFATs = 2,
                MediaDescriptor = 0xF8,
                SectorsPerTrack = (ushort)diskGeometry.SectorsPerTrack,
                NumberOfHeads = (ushort)diskGeometry.TracksPerCylinder,
                HiddenSectors = partitionInfo.HiddenSectors,
                TotalSectors = totalSectors,
                SectorsPerFAT = fatSize,
                RootCluster = 2,
                FSInfoSector = 1,
                BackupBootSector = 6,
                DriveNumber = 0x80,
                BootSignature = 0x29,
                VolumeID = volumeID,
                Signature = 0x55AA
            };

            Span<byte> rawBytes = MemoryMarshal.AsBytes(new Span<FAT32BootSector>(ref bootSector));

            if (bootSector.BytesPerSector != 512)
            {
                rawBytes[bootSector.BytesPerSector - 2] = 0x55;
                rawBytes[bootSector.BytesPerSector - 1] = 0xAA;
            }

            rawBytes[0] = 0xEB;
            rawBytes[1] = 0x58;
            rawBytes[2] = 0x90;

            string oemName = "MSWIN4.1";
            Encoding.ASCII.GetBytes(oemName).CopyTo(rawBytes.Slice(3, 8));

            string volumeLabel = "BADUPDATE  ";
            Encoding.ASCII.GetBytes(volumeLabel).CopyTo(rawBytes.Slice(71, 11));

            string fileSystemType = "FAT32   ";
            Encoding.ASCII.GetBytes(fileSystemType).CopyTo(rawBytes.Slice(82, 8));

            return bootSector;
        }

        private static FAT32FsInfoSector InitializeFsInfo() => new FAT32FsInfoSector
        {
            LeadSignature = 0x41615252,
            StructureSignature = 0x61417272,
            TrailSignature = 0xAA550000,
            FreeClusterCount = 0,
            NextFreeCluster = 3
        };

        private static uint[] InitializeFirstFATSector(uint bytesPerSector)
        {
            uint[] sector = new uint[bytesPerSector / 4];
            sector[0] = 0x0FFFFFF8;
            sector[1] = 0x0FFFFFFF;
            sector[2] = 0x0FFFFFFF;
            return sector;
        }

        private static byte[] CreateRootVolumeLabelSector(uint bytesPerSector, string volumeLabel)
        {
            byte[] sector = new byte[bytesPerSector];

            string normalizedLabel = (volumeLabel ?? string.Empty)
                .Trim()
                .ToUpperInvariant();

            if (normalizedLabel.Length > 11)
                normalizedLabel = normalizedLabel[..11];

            normalizedLabel = normalizedLabel.PadRight(11, ' ');
            Encoding.ASCII.GetBytes(normalizedLabel).CopyTo(sector, 0);

            // Entry attribute
            sector[11] = 0x08;

            return sector;
        }

        private static string FormatVolumeData(
            SafeFileHandle driveHandle,
            DISK_GEOMETRY diskGeometry,
            FAT32BootSector bootSector,
            FAT32FsInfoSector fsInfo,
            uint[] firstFATSector,
            bool isGPT,
            PARTITION_INFORMATION partitionInfo)
        {
            uint bytesPerSector = diskGeometry.BytesPerSector;
            uint totalSectors = (uint)(partitionInfo.PartitionLength / bytesPerSector);
            uint userAreaSize = totalSectors - bootSector.ReservedSectorCount - (bootSector.NumberOfFATs * bootSector.SectorsPerFAT);
            uint zeroOut = bootSector.ReservedSectorCount + (bootSector.NumberOfFATs * bootSector.SectorsPerFAT) + bootSector.SectorsPerCluster;
            uint clusterCount = userAreaSize / bootSector.SectorsPerCluster;

            if (clusterCount < 65536 || clusterCount > 0x0FFFFFFF)
                return Error("The drive's cluster count is out of range (65536 < clusterCount < 0x0FFFFFFF)");

            fsInfo.FreeClusterCount = clusterCount - 1;

            ZeroOutSectors(driveHandle, 0, zeroOut, bytesPerSector);

            for (int i = 0; i < 2; i++)
            {
                uint sectorStart = (i == 0) ? 0 : (uint)bootSector.BackupBootSector;
                WriteSector(driveHandle, sectorStart, 1, bytesPerSector, StructToBytes(bootSector));
                WriteSector(driveHandle, sectorStart + 1, 1, bytesPerSector, StructToBytes(fsInfo));
            }

            for (int i = 0; i < bootSector.NumberOfFATs; i++)
            {
                uint sectorStart = (uint)(bootSector.ReservedSectorCount + (i * bootSector.SectorsPerFAT));
                WriteSector(driveHandle, sectorStart, 1, bytesPerSector, UintArrayToBytes(firstFATSector));
            }

            uint firstDataSector = (uint)(bootSector.ReservedSectorCount + (bootSector.NumberOfFATs * bootSector.SectorsPerFAT));
            byte[] rootDirFirstSector = CreateRootVolumeLabelSector(bytesPerSector, "BADUPDATE");
            WriteSector(driveHandle, firstDataSector, 1, bytesPerSector, rootDirFirstSector);

            return string.Empty;
        }
    }

    // Structs

    internal struct DISK_GEOMETRY
    {
        public long Cylinders;
        public uint MediaType;
        public uint TracksPerCylinder;
        public uint SectorsPerTrack;
        public uint BytesPerSector;
    }

    internal struct PARTITION_INFORMATION
    {
        public long StartingOffset;
        public long PartitionLength;
        public uint HiddenSectors;
    }
}
