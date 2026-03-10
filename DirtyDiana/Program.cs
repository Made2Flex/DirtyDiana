global using DownloadItem = (string name, string url);
global using ArchiveItem = (string name, string path);
global using HomebrewApp = (string name, string folder, string entryPoint);

using Spectre.Console;
using DirtyDiana.Models;
using DirtyDiana.Helpers;
using DirtyDiana.Utilities;

using static DirtyDiana.Utilities.Constants;

namespace DirtyDiana
{
    internal partial class Program
    {
        static readonly Style OrangeStyle = new(new Color(255, 114, 0));
        static readonly Style LightOrangeStyle = new(new Color(255, 172, 77));
        static readonly Style PeachStyle = new(new Color(255, 216, 153));

        static readonly Style GreenStyle = new(new Color(118, 185, 0));
        static readonly Style GrayStyle = new(new Color(132, 133, 137));

        // Toggle patching
        static readonly bool EnableLegacyXexPatching = false;

        static string XexToolPath = string.Empty;
        static string TargetDriveLetter = string.Empty;

        static ActionQueue actionQueue = new();

        static DiskInfo targetDisk = new("Z:\\", "Fixed", 0, "", 0, int.MaxValue);

        static void Main(string[] args)
        {
            // Open terminal in Linux
            if (OperatingSystem.IsLinux())
            {
                bool hasTerminal = !Console.IsInputRedirected &&
                !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TERM"));

                if (!hasTerminal)
                {
                    string exePath = Environment.ProcessPath ?? string.Empty;

                    if (OpenTerminalLinux.TryOpenInTerminal(exePath))
                    {
                        Environment.Exit(0);
                    }
                    else
                    {
                        Console.WriteLine("[!] Could not open a terminal emulator. Please run this program from a terminal.");
                        Environment.Exit(1);
                    }
                }
            }

            ClearConsole();

            while (true)
            {
                Console.WriteLine();
                string action = PromptForAction();

                if (action == "Exit")
                {
                    AnsiConsole.Clear();
                    Environment.Exit(0);
                }

                //Just give me homebrew!
                bool homebrewOnly = action == "Add homebrew apps";

                if (homebrewOnly)
                {
                    RunHomebrewOnly();
                    continue;
                }

                List<DiskInfo> disks = OperatingSystem.IsWindows()
                    ? DiskHelper.GetDisks()
                    : DiskHelperUnix.GetDisks();

                // Dont allow selection if no drives are found
                if (disks == null || disks.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[italic red][[!]] No USB drives detected! Please insert a USB drive and try again.[/]");
                    AnsiConsole.MarkupLine($"[italic gray]Press any key to retry, or Ctrl+C to exit.[/]");
                    Console.ReadKey();
                    ClearConsole();
                    continue;
                }

                string selectedDiskDisplay = PromptDiskSelection(disks);

                DiskInfo selectedDiskInfo = disks.First(d =>
                    OperatingSystem.IsWindows()
                        ? $"{d.DriveLetter} ({d.SizeFormatted}) - {d.Type}" == selectedDiskDisplay
                        : DiskHelperUnix.FormatDisk(d) == selectedDiskDisplay
                );

                TargetDriveLetter = OperatingSystem.IsWindows()
                    ? selectedDiskInfo.DriveLetter[..3]
                    : selectedDiskInfo.DriveLetter;

                AnsiConsole.MarkupLine($"[italic #FFAC4D]Selected Target: {TargetDriveLetter}[/]");

                if (!Directory.Exists(TargetDriveLetter))
                {
                    AnsiConsole.MarkupLine($"[italic red][[!]] Target drive path does not exist: {TargetDriveLetter}[/]");
                    Environment.Exit(1);
                }

                // Run compatibility check
                var report = UsbCompatibilityChecker.Check(TargetDriveLetter);

                UsbCompatibilityChecker.PrintCheck(report);

                if (!report.Writable)
                {
                    AnsiConsole.MarkupLine("[red][[!]] Target drive is not writable. Please select another drive.[/]");
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                if (!report.EnoughFreeSpace)
                {
                    AnsiConsole.MarkupLine("[red][[!]] Target drive does not have enough free space. Please select another drive.[/]");
                    Console.ReadKey();
                    Environment.Exit(1);
                }

                targetDisk = selectedDiskInfo;

                bool confirmation = PromptFormatConfirmation(selectedDiskDisplay);

                if (confirmation)
                {
                    if (!FormatDisk(targetDisk))
                    {
                        ClearConsole();
                        continue;
                    }
                }
                break;

                if (!confirmation)
                {
                    ClearConsole();
                    continue;
                }
            }

            List<ArchiveItem> downloadedFiles;

            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                // Linux / Mac
                var linuxDownloads = DownloadHelperLinux
                .DownloadAllReleasesAsync()
                .GetAwaiter()
                .GetResult();

                downloadedFiles = linuxDownloads
                .Select(d => new ArchiveItem(d.name, d.url))
                .ToList();
            }
            else
            {
                // Windows
                downloadedFiles = DownloadRequiredFiles().Result;
            }

            ExtractFiles(downloadedFiles).Wait();

            ClearConsole();
            string selectedDefaultApp = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                .Title("Which program should be launched by BadUpdate?")
                .HighlightStyle(GreenStyle)
                .AddChoices(
                    "FreeMyXe",
                    "XeUnshackle"
                )
            );

            ClearConsole();
            string selectedDefaultBaddy = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                .Title("Which flavor do you want to use?")
                .HighlightStyle(GreenStyle)
                .AddChoices(
                    "ABadAvatar",
                    "BadUpdate"
                )
            );

            AnsiConsole.MarkupLine($"[#76B900]{Markup.Escape("[+]")}[/] Copying requried files.");

            foreach (var folder in Directory.GetDirectories($@"{EXTRACTED_DIR}"))
            {
                string folderName = Path.GetFileName(
                    folder.TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar
                    )
                );

                switch (folderName)
                {
                    case "XeXmenu":
                        {
                            string xeXmenuSource = Path.Combine(folder, ContentFolder, "C0DE9999");
                            string xeXmenuDest = Path.Combine(TargetDriveLetter, ContentFolder, "C0DE9999");
                            if (!Directory.Exists(xeXmenuSource))
                            {
                                AnsiConsole.MarkupLine("[red][[ERROR]][/] Source directory does not exist: {0}", xeXmenuSource);
                                break;
                            }
                            EnqueueMirrorDirectory(xeXmenuSource, xeXmenuDest, 7);
                        }
                        break;

                    case "FreeMyXe":
                        if (selectedDefaultApp != "FreeMyXe") break;
                        EnqueueFileCopy(
                            Path.Combine(folder, "FreeMyXe.xex"),
                                        Path.Combine(TargetDriveLetter, "BadUpdatePayload", "default.xex"),
                                        9
                        );
                        break;

                    case "XeUnshackle":
                        if (selectedDefaultApp != "XeUnshackle") break;
                        string subFolderPath = Directory.GetDirectories(folder).FirstOrDefault();
                        File.Delete(Path.Combine(subFolderPath, "README - IMPORTANT.txt"));
                        EnqueueMirrorDirectory(
                            subFolderPath,
                            TargetDriveLetter,
                            9
                        );
                        break;

                    case "ABadAvatar":
                        // When ABadAvatar is selected, we install both
                        if (selectedDefaultBaddy != "ABadAvatar") break;
                        EnqueueMirrorDirectory(
                            folder,
                            TargetDriveLetter,
                            9 // Run after BadUpdate
                        );
                        break;

                    case "BadUpdate":
                        actionQueue.EnqueueAction(async () =>
                        {
                            using (StreamWriter writer = new(Path.Combine(TargetDriveLetter, "name.txt")))
                                writer.WriteLine("USB Storage Device");

                            using (StreamWriter writer = new(Path.Combine(TargetDriveLetter, "info.txt")))
                                writer.WriteLine($"This drive was created with DirtyDiana.\nModified to work on Unix/Linux by Made2Flex.\nFind more info here: https://github.com/Made2Flex/DirtyDiana\nBased on https://github.com/Pdawg-bytes/BadBuilder\nConfiguration: \n-  BadUpdate target binary: {selectedDefaultApp}");

                            Directory.CreateDirectory(Path.Combine(TargetDriveLetter, "Apps"));
                            await FileSystemHelper.MirrorDirectoryAsync(Path.Combine(folder, "Rock Band Blitz"), TargetDriveLetter);
                        }, 10);
                        break;

                    case "BadUpdate Tools":
                        string exeDir = Path.GetDirectoryName(Environment.ProcessPath!)
                        ?? AppContext.BaseDirectory;

                        XexToolPath = Path.Combine(exeDir, "XePatcher", "XexTool.exe");

                        break;

                    case "Rock Band Blitz":
                        {
                            string rbbSource = Path.Combine(
                                folder,
                                ContentFolder,
                                "5841122D",
                                "000D0000"
                            );
                            string rbbDest = Path.Combine(
                                TargetDriveLetter,
                                ContentFolder,
                                "5841122D",
                                "000D0000"
                            );
                            if (!Directory.Exists(rbbSource))
                            {
                                AnsiConsole.MarkupLine("[red][[ERROR]][/] Source directory does not exist: {0}", rbbSource);
                                break;
                            }
                            EnqueueMirrorDirectory(rbbSource, rbbDest, 8);
                        }
                        break;

                    case "Simple 360 NAND Flasher":
                        actionQueue.EnqueueAction(async () =>
                        {
                            string flasherFolder = Path.Combine(folder, "Simple 360 NAND Flasher");
                            string defaultXex = Path.Combine(flasherFolder, "Default.xex");
                            string targetAppFolder = Path.Combine(TargetDriveLetter, "Apps", "Simple 360 NAND Flasher");

                            Directory.CreateDirectory(targetAppFolder);

                            // Legacy function
                            if (EnableLegacyXexPatching)
                            {
                                await PatchHelper.PatchXexAsync(defaultXex, XexToolPath);
                            }

                            await FileSystemHelper.MirrorDirectoryAsync(flasherFolder, targetAppFolder);

                        }, 6);
                        break;

                    default:
                        throw new Exception($"[-] Unexpected directory in working folder: {folder}");
                }
            }

            actionQueue.ExecuteActionsAsync().Wait();

            File.AppendAllText(Path.Combine(TargetDriveLetter, "info.txt"), "-  Disk formatting: SKIPPED (existing filesystem retained)\n");
            File.AppendAllText(Path.Combine(TargetDriveLetter, "info.txt"), $"-  Disk total size: {targetDisk.TotalSize} bytes\n");

            // Mostly for debugging. can be removed..
            Console.Write("\nDone. Press any key to continue...");
            Console.ReadKey();

            ClearConsole();
            if (!PromptAddHomebrew())
            {
                WriteHomebrewLog(1);
                AnsiConsole.MarkupLine("\n[#76B900]{0}[/] Your USB drive is ready to go.", Markup.Escape("[+]"));
                Console.Write("\nPress any key to exit...");
                Console.ReadKey();
                Environment.Exit(0);
            }

            Console.WriteLine();

            List<HomebrewApp> homebrewApps = ManageHomebrewApps();

            AnsiConsole.Status()
            .SpinnerStyle(OrangeStyle)
            .StartAsync("Copying homebrew apps.", async ctx =>
            {
                await Task.WhenAll(homebrewApps.Select(async item =>
                {
                    await FileSystemHelper.MirrorDirectoryAsync(item.folder, Path.Combine(TargetDriveLetter, "Apps", item.name));

                    if (EnableLegacyXexPatching)
                    {
                        await PatchHelper.PatchXexAsync(item.entryPoint, XexToolPath);
                    }
                }));
            }).Wait();

            WriteHomebrewLog(homebrewApps.Count + 1);

            string status = "[+]";
            AnsiConsole.MarkupLineInterpolated($"\n[#76B900]{status}[/] [bold]{homebrewApps.Count}[/] apps copied.");

            AnsiConsole.MarkupLine("\n[#76B900]{0}[/] Your USB drive is ready to go.", Markup.Escape("[+]"));

            Console.Write("\nPress any key to exit...");
            Console.ReadKey();
        }

        static void EnqueueMirrorDirectory(string sourcePath, string destinationPath, int priority)
        {
            actionQueue.EnqueueAction(async () =>
            {
                await FileSystemHelper.MirrorDirectoryAsync(sourcePath, destinationPath);
            }, priority);
        }

        static void EnqueueFileCopy(string sourceFile, string destinationFile, int priority)
        {
            actionQueue.EnqueueAction(async () =>
            {
                await FileSystemHelper.CopyFileAsync(sourceFile, destinationFile);
            }, priority);
        }

        static void PrintCheck(bool ok, string text)
        {
            if (ok)
                AnsiConsole.MarkupLine($"[#76B900]✓[/] {text}");
            else
                AnsiConsole.MarkupLine($"[yellow]⚠[/] {text}");
        }

        static void WriteHomebrewLog(int count)
        {
            string logPath = Path.Combine(TargetDriveLetter, "info.txt");
            string logEntry = $"-  {count} homebrew app(s) added (Simple 360 NAND Flasher added by default)\n";
            File.AppendAllText(logPath, logEntry);
        }

        static void ShowWelcomeMessage() => AnsiConsole.Markup(
            """
            [#0A3D62]██████╗ ██╗██████╗ ████████╗██╗   ██╗██████╗ ██╗ █████╗ ███╗   ██╗ █████╗ [/]
            [#145DA0]██╔══██╗██║██╔══██╗╚══██╔══╝╚██╗ ██╔╝██╔══██╗██║██╔══██╗████╗  ██║██╔══██╗[/]
            [#1E81B0]██║  ██║██║██████╔╝   ██║    ╚████╔╝ ██║  ██║██║███████║██╔██╗ ██║███████║[/]
            [#2E8BC0]██║  ██║██║██╔══██╗   ██║     ╚██╔╝  ██║  ██║██║██╔══██║██║╚██╗██║██╔══██║[/]
            [#3FA7D6]██████╔╝██║██║  ██║   ██║      ██║   ██████╔╝██║██║  ██║██║ ╚████║██║  ██║[/]
            [#76C7F2]╚═════╝ ╚═╝╚═╝  ╚═╝   ╚═╝      ╚═╝   ╚═════╝ ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝╚═╝  ╚═╝[/]

            [#1E81B0]────────────────────────────────────────────────────────────────v0.34.7-gn[/]
            ──────────────────Xbox 360 [#FF7200]BadUpdate[/] USB Builder for Linux────────────────
            [#848589]                         ───  By Made2Flex  ───                           [/]
            [#1E81B0]──────────────────────────────────────────────────────────────────────────[/]

            """);

        private static void RunHomebrewOnly()
        {
            List<DiskInfo> disks = OperatingSystem.IsWindows()
            ? DiskHelper.GetDisks()
            : DiskHelperUnix.GetDisks();

            if (disks == null || disks.Count == 0)
            {
                AnsiConsole.MarkupLine("[italic red][[!]] No USB drives detected! Please insert a USB drive and try again.[/]");
                AnsiConsole.MarkupLine("[italic gray]Press any key to return to menu.[/]");
                Console.ReadKey();
                ClearConsole();
                return;
            }

            string selectedDiskDisplay = PromptDiskSelection(disks);

            DiskInfo selectedDiskInfo = disks.First(d =>
            OperatingSystem.IsWindows()
            ? $"{d.DriveLetter} ({d.SizeFormatted}) - {d.Type}" == selectedDiskDisplay
            : DiskHelperUnix.FormatDisk(d) == selectedDiskDisplay
            );

            string targetDriveLetter = OperatingSystem.IsWindows()
            ? selectedDiskInfo.DriveLetter[..3]
            : selectedDiskInfo.DriveLetter;

            TargetDriveLetter = targetDriveLetter;

            if (!Directory.Exists(TargetDriveLetter))
            {
                AnsiConsole.MarkupLine($"[italic red][[!]] Target drive path does not exist: {TargetDriveLetter}[/]");
                Console.ReadKey();
                return;
            }

            // Run compatibility check
            var report = UsbCompatibilityChecker.Check(TargetDriveLetter);

            UsbCompatibilityChecker.PrintCheck(report);

            if (!report.Writable)
            {
                AnsiConsole.MarkupLine("[red][[!]] Target drive is not writable. Please select another drive.[/]");
                Console.ReadKey();
                Environment.Exit(1);
            }

            if (!report.EnoughFreeSpace)
            {
                AnsiConsole.MarkupLine("[red][[!]] Target drive does not have enough free space. Please select another drive.[/]");
                Console.ReadKey();
                Environment.Exit(1);
            }

            string appsRoot = Path.Combine(TargetDriveLetter, "Apps");
            Directory.CreateDirectory(appsRoot);

            ClearConsole();

            var apps = ManageHomebrewApps();

            if (apps.Count == 0)
                return;

            AnsiConsole.Status()
            .SpinnerStyle(OrangeStyle)
            .Start("Adding Homebrew..", ctx =>
            {
                foreach (var item in apps)
                {
                    string FolderName = Path.GetFileName(
                        item.folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    );

                    string destinationPath = Path.Combine(appsRoot, FolderName);

                    // Prevent self-copy
                    string normalizedSource = Path.GetFullPath(item.folder);
                    string normalizedDest = Path.GetFullPath(destinationPath);

                    if (normalizedSource.Equals(normalizedDest, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Source and destination paths are identical. Aborting copy.");

                    void onFileCopied(string src, string dst)
                    {
                        var displaySrc = Markup.Escape(src);
                        var displayDst = Markup.Escape(dst);
                        AnsiConsole.MarkupLine($"[grey][[*]] Copying: [/] [italic]{displaySrc}[/] [blue]->[/] [italic]{displayDst}[/]");
                    }

                    async Task MirrorWithProgress(string src, string dst)
                    {
                        async Task Recurse(string from, string to)
                        {
                            if (Directory.Exists(from))
                            {
                                Directory.CreateDirectory(to);

                                foreach (var file in Directory.GetFiles(from))
                                {
                                    onFileCopied(file, Path.Combine(to, Path.GetFileName(file)));
                                    await FileSystemHelper.CopyFileAsync(file, Path.Combine(to, Path.GetFileName(file)));
                                }
                                foreach (var dir in Directory.GetDirectories(from))
                                    await Recurse(dir, Path.Combine(to, Path.GetFileName(dir)));
                            }
                        }
                        await Recurse(src, dst);
                    }

                    MirrorWithProgress(normalizedSource, normalizedDest).GetAwaiter().GetResult();

                    if (EnableLegacyXexPatching)
                    {
                        PatchHelper.PatchXexAsync(item.entryPoint, XexToolPath).GetAwaiter().GetResult();
                    }
                }
            });

            WriteHomebrewLog(apps.Count);

            AnsiConsole.MarkupLine($"\n[#76B900]{Markup.Escape("[+]")} [/][bold]{apps.Count}[/] apps copied.");

            Console.Write("\nPress any key to return to menu...");
            Console.ReadKey();
            ClearConsole();
        }

        static string PromptForAction() => AnsiConsole.Prompt(
            new SelectionPrompt<string>()
            .HighlightStyle(GreenStyle)
            .AddChoices(
                "Build exploit USB",
                "Add homebrew apps",
                "Exit"
            )
        );

        static void ClearConsole()
        {
            AnsiConsole.Clear();
            ShowWelcomeMessage();
            Console.WriteLine();
        }
    }
}
