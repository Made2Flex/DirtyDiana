using Spectre.Console;
using DirtyDiana.Models;
using DirtyDiana.Helpers;
using DirtyDiana.Formatter;
using DirtyDiana.Utilities;
using System.Runtime.InteropServices;

namespace DirtyDiana
{
    internal partial class Program
    {
        static string PromptDiskSelection(List<DiskInfo> disks)
        {
            var choices = new List<string>();
            foreach (var disk in disks)
                choices.Add($"{disk.DriveLetter} ({disk.SizeFormatted}) - {disk.Type}");

            return AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                .Title("Select a disk:")
                .HighlightStyle(GreenStyle)
                .AddChoices(choices)
            );
        }

        static bool PromptFormatConfirmation(string selectedDisk)
        {
            return AnsiConsole.Prompt(
                new TextPrompt<bool>($"[#FF7200 bold]WARNING: [/]Are you sure you would like to format the selected drive? All data on this drive will be lost.")
                .AddChoice(true)
                .AddChoice(false)
                .DefaultValue(false)
                .ChoicesStyle(GreenStyle)
                .DefaultValueStyle(OrangeStyle)
                .WithConverter(choice => choice ? "y" : "n")
            );
        }

        static bool FormatDisk(DiskInfo disk)
        {
            string output = string.Empty;
            bool success = true;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                LinuxPrivilegeHelper.EnsureRootOrExit();

                AnsiConsole.Status().SpinnerStyle(LightOrangeStyle)
                .Start($"[#76B900]Formatting disk[/] {disk.DriveLetter} ({disk.SizeFormatted}) - {disk.Type}", ctx =>
                {
                    ClearConsole();

                    try
                    {
                        var device = DiskHelperUnix.GetDeviceFromMountPoint(disk.DriveLetter);

                        if (string.IsNullOrWhiteSpace(device))
                            throw new Exception("Unable to resolve device from mount point.");

                        // unmount points
                        try
                        {
                            foreach (var line in File.ReadLines("/proc/self/mounts"))
                            {
                                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length < 2)
                                    continue;

                                string mountedDevice = parts[0];
                                string mountPoint = parts[1];

                                if (mountedDevice.StartsWith(device, StringComparison.Ordinal))
                                {
                                    System.Diagnostics.Process.Start("umount", $"\"{mountPoint}\"")?.WaitForExit();
                                }
                            }
                        }
                        catch
                        {
                            //
                        }

                        #if WINDOWS
                        output = DiskFormatter.FormatVolume(device[0], disk.TotalSize);
                        #else // Unix
                        output = DiskFormatter.FormatVolume(device, disk.TotalSize);
                        #endif

                        if (!string.IsNullOrEmpty(output))
                        {
                            success = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        output = ex.ToString();
                        success = false;
                    }
                });

                if (!success)
                {
                    AnsiConsole.Clear();
                    ShowWelcomeMessage();

                    if (!string.IsNullOrWhiteSpace(output))
                        AnsiConsole.MarkupLine($"[red]{Markup.Escape(output)}[/]");
                }

                return success;
            }

            // Windows branch
            AnsiConsole.Status().SpinnerStyle(LightOrangeStyle)
            .Start($"[#76B900]Formatting disk[/] {disk.DriveLetter} ({disk.SizeFormatted}) - {disk.Type}", ctx =>
            {
                ClearConsole();

                try
                {
                    char driveChar = disk.DriveLetter[0];
                    #if WINDOWS
                    output = DiskFormatter.FormatVolume(driveChar, disk.TotalSize);
                    #else
                    string devicePath = driveChar.ToString(); // convert char to string for Linux
                    output = DiskFormatter.FormatVolume(devicePath, disk.TotalSize);
                    #endif

                    if (!string.IsNullOrEmpty(output))
                        success = false;
                }
                catch (Exception ex)
                {
                    output = ex.Message;
                    success = false;
                }
            });

            if (!success)
            {
                AnsiConsole.Clear();
                ShowWelcomeMessage();
                if (!string.IsNullOrWhiteSpace(output))
                {
                    AnsiConsole.MarkupLine($"[red]{Markup.Escape(output)}[/]");
                }
            }

            return success;
        }
    }
}
