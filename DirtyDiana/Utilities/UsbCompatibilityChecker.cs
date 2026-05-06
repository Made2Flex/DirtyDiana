using System;
using System.IO;

namespace DirtyDiana.Utilities
{
	internal sealed class UsbCompatibilityResult
	{
		public bool Writable { get; set; }
		public bool IsFat32 { get; set; }
		public bool EnoughFreeSpace { get; set; }

		public long FreeSpace { get; set; }
		public long RequiredSpace { get; set; }

		public string FileSystem { get; set; } = "";
	}

	internal static class UsbCompatibilityChecker
	{
		internal static UsbCompatibilityResult Check(string mountPath)
		{
			var result = new UsbCompatibilityResult();
			var drive = new DriveInfo(mountPath);

			result.Writable = CheckWritable(mountPath);

			// Check free space
			result.FreeSpace = drive.AvailableFreeSpace;
			result.RequiredSpace = 500_000_000; // ~500 MB
			result.EnoughFreeSpace = result.FreeSpace > result.RequiredSpace;

			// Check filesystem type
			if (OperatingSystem.IsWindows())
			{
				result.FileSystem = drive.DriveFormat;
			}
			else
			{
				result.FileSystem = DirtyDiana.Helpers.DiskHelperUnix.GetFilesystemType(mountPath);
			}

			result.IsFat32 = string.Equals(result.FileSystem, "fat32", StringComparison.OrdinalIgnoreCase);

			return result;
		}

		private static bool CheckWritable(string path)
		{
			try
			{
				string testFile = Path.Combine(path, ".write_test");
				File.WriteAllText(testFile, "test");
				File.Delete(testFile);
				return true;
			}
			catch
			{
				return false;
			}
		}

		public static void PrintCheck(UsbCompatibilityResult result)
		{
			Console.WriteLine();
			Console.WriteLine("Compatibility Check");
			Console.WriteLine("-----------------------");
			Console.WriteLine(result.Writable ? "✓ Drive is writable" : "⚠ Drive is not writable");
			Console.WriteLine(result.IsFat32 ? $"✓ FAT32 filesystem ({result.FileSystem})" : $"⚠ FAT32 filesystem ({result.FileSystem})");
			Console.WriteLine(result.EnoughFreeSpace ? "✓ Enough free space" : $"⚠ Not enough free space ({result.FreeSpace / Constants.MB} MB available)");
			Console.WriteLine();
			Console.WriteLine(result.Writable && result.IsFat32 && result.EnoughFreeSpace
			? "Drive appears compatible with Xbox 360."
			: "Drive may not be compatible with Xbox 360.");
			Console.WriteLine();
		}
	}
}
