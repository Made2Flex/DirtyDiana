using Spectre.Console;
using DirtyDiana.Models;
using DirtyDiana.Helpers;
using DirtyDiana.Formatter;
using DirtyDiana.Utilities;
using System.Runtime.InteropServices; // portability

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
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AnsiConsole.MarkupLine(
                    Markup.Escape("[-] Automatic formatting is currently only supported on Windows.")
                );

                AnsiConsole.MarkupLine(
                    Markup.Escape("[+] Beginning download Process..")
                );

                var check = UsbCompatibilityChecker.Check(disk.DriveLetter);

                if (!check.IsFat32)
                {
                    AnsiConsole.MarkupLine(
                        Markup.Escape($"[!] Detected filesystem: {check.FileSystem}. FAT32 is required.")
                    );
                    Environment.Exit(1);
                }

                return true;
            }

            string output = string.Empty;
            bool success = true;

            AnsiConsole.Status().SpinnerStyle(LightOrangeStyle)
            .Start($"[#76B900]Formatting disk[/] {disk.DriveLetter} ({disk.SizeFormatted}) - {disk.Type}", ctx =>
            {
                ClearConsole();

                try
                {
                    char driveChar = disk.DriveLetter[0];
                    output = DiskFormatter.FormatVolume(driveChar, disk.TotalSize);

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
