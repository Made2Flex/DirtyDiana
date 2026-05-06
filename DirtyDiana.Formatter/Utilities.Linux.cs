using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DirtyDiana.Formatter
{
    static class Utilities
    {
        // libc bindings

        private const int O_RDWR = 2;
        private const int O_SYNC = 0x101000;

        private const int SEEK_SET = 0;

        [DllImport("libc", SetLastError = true)]
        private static extern int open(string pathname, int flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int close(int fd);

        [DllImport("libc", SetLastError = true)]
        private static extern long lseek(int fd, long offset, int whence);

        [DllImport("libc", SetLastError = true)]
        private static extern long write(int fd, IntPtr buf, ulong count);

        [DllImport("libc", SetLastError = true)]
        private static extern int fsync(int fd);

        // Helper: get fd from SafeHandle

        private static int GetFD(SafeHandle handle)
        {
            return handle.DangerousGetHandle().ToInt32();
        }

        internal static byte[] StructToBytes<T>(T @struct) where T : struct
        {
            Span<T> structSpan = MemoryMarshal.CreateSpan(ref @struct, 1);
            Span<byte> byteSpan = MemoryMarshal.AsBytes(structSpan);
            return byteSpan.ToArray();
        }

        internal static byte[] UintArrayToBytes(uint[] array) =>
        MemoryMarshal.AsBytes<uint>(array.AsSpan()).ToArray();

        internal static string Error(string error) =>
        $"{Constants.ORANGE}[-]{Constants.ANSI_RESET} {error}";

        internal static void ExitWithError(string error)
        {
            Console.WriteLine($"{Constants.ORANGE}[-]{Constants.ANSI_RESET} {error}");
            Environment.Exit(-1);
        }

        internal static uint GetVolumeID()
        {
            DateTime now = DateTime.Now;

            ushort low = (ushort)(now.Day + (now.Month << 8));
            low += (ushort)((now.Millisecond / 10) + (now.Second << 8));

            ushort hi = (ushort)(now.Minute + (now.Hour << 8));
            hi += (ushort)now.Year;

            return (uint)(low | (hi << 16));
        }

        internal static uint CalculateFATSize(uint diskSize, uint reservedSectors, uint sectorsPerCluster, uint numberOfFATs, uint bytesPerSector)
        {
            const ulong fatElementSize = 4;
            const ulong reservedClusters = 2;

            ulong numerator = diskSize - reservedSectors + reservedClusters * sectorsPerCluster;
            ulong denominator = (sectorsPerCluster * bytesPerSector / fatElementSize) + numberOfFATs;

            return (uint)(numerator / denominator + 1);
        }

        internal static long CalculateSectorsPerCluster(ulong diskSizeBytes, uint bytesPerSector) => diskSizeBytes switch
        {
            < 64 * Constants.MB => ((512) / bytesPerSector),
            < 128 * Constants.MB => ((1 * Constants.KB) / bytesPerSector),
            < 256 * Constants.MB => ((2 * Constants.KB) / bytesPerSector),
            < 8 * Constants.GB => ((4 * Constants.KB) / bytesPerSector),
            < 16 * Constants.GB => ((8 * Constants.KB) / bytesPerSector),
            < 32 * Constants.GB => ((16 * Constants.KB) / bytesPerSector),
            < 2 * Constants.TB => ((32 * Constants.KB) / bytesPerSector),
            _ => ((64 * Constants.KB) / bytesPerSector)
        };

        // Disk Ops

        internal static unsafe void SeekTo(SafeHandle hDevice, uint sector, uint bytesPerSector)
        {
            int fd = GetFD(hDevice);
            long offset = (long)sector * bytesPerSector;

            long result = lseek(fd, offset, SEEK_SET);
            if (result < 0)
            {
                ExitWithError($"lseek failed. errno: {Marshal.GetLastWin32Error()}");
            }
        }

        internal static unsafe void WriteSector(SafeHandle hDevice, uint sector, uint numberOfSectors, uint bytesPerSector, byte[] data)
        {
            int fd = GetFD(hDevice);

            SeekTo(hDevice, sector, bytesPerSector);

            ulong totalBytes = (ulong)(numberOfSectors * bytesPerSector);
            ulong written = 0;

            fixed (byte* pData = data)
            {
                while (written < totalBytes)
                {
                    IntPtr ptr = (IntPtr)(pData + (long)written);
                    ulong remaining = totalBytes - written;

                    long result = write(fd, ptr, remaining);
                    if (result <= 0)
                    {
                        ExitWithError($"write failed. errno: {Marshal.GetLastWin32Error()}");
                    }

                    written += (ulong)result;
                }
            }
        }

        internal static unsafe void ZeroOutSectors(SafeHandle hDevice, uint sector, uint numberOfSectors, uint bytesPerSector)
        {
            const uint burstSize = 128;

            int fd = GetFD(hDevice);

            byte[] zeroBuffer = new byte[bytesPerSector * burstSize];

            SeekTo(hDevice, sector, bytesPerSector);

            while (numberOfSectors > 0)
            {
                uint writeSize = (numberOfSectors > burstSize) ? burstSize : numberOfSectors;
                ulong totalBytes = (ulong)(writeSize * bytesPerSector);

                ulong written = 0;

                fixed (byte* pData = zeroBuffer)
                {
                    while (written < totalBytes)
                    {
                        IntPtr ptr = (IntPtr)(pData + (long)written);
                        ulong remaining = totalBytes - written;

                        long result = write(fd, ptr, remaining);
                        if (result <= 0)
                        {
                            ExitWithError($"write failed during zeroing. errno: {Marshal.GetLastWin32Error()}");
                        }

                        written += (ulong)result;
                    }
                }

                numberOfSectors -= writeSize;
            }
        }
    }
}
