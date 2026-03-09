using System;
using System.IO;
using Spectre.Console;
using System.Threading.Tasks;

namespace DirtyDiana.Helpers
{
    internal static class FileSystemHelper
    {
        internal static async Task MirrorDirectoryAsync(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine($"[ERROR][[!]] Source directory does not exist: {sourceDir}");
                return;
            }

            //Console.WriteLine($"[DEBUG] Mirroring directory: {sourceDir} -> {destDir}");

            try
            {
                Directory.CreateDirectory(destDir);

                string[] files = Directory.GetFiles(sourceDir);
                foreach (var file in files)
                {
                    string relativePath = Path.GetRelativePath(sourceDir, file);
                    string destFile = Path.Combine(destDir, relativePath);

                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);

                    await CopyFileAsync(file, destFile);
                }

                string[] directories = Directory.GetDirectories(sourceDir);
                foreach (var dir in directories)
                {
                    string relativePath = Path.GetRelativePath(sourceDir, dir);
                    string destSubDir = Path.Combine(destDir, relativePath);

                    await MirrorDirectoryAsync(dir, destSubDir);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR][[!]] Failed to mirror directory {sourceDir} -> {destDir}: {ex.Message}");
                throw;
            }
        }

        internal static async Task CopyFileAsync(string sourceFile, string destFile)
        {
            if (!File.Exists(sourceFile))
            {
                Console.WriteLine($"[ERROR][[!]] Source file does not exist: {sourceFile}");
                return;
            }

            try
            {
                AnsiConsole.MarkupLine($"[#76B900]{Markup.Escape("[*]")}[/] Copying: {sourceFile} -> {destFile}");
                using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read))
                using (var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                await sourceStream.CopyToAsync(destStream);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR][[!]] Failed to copy {sourceFile} -> {destFile}: {ex.Message}");
                throw;
            }
        }
    }
}
