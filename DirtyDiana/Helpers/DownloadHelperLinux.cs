using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using Spectre.Console;
using System.Text.Json;
using System.Linq;
using static DirtyDiana.Utilities.Constants;

namespace DirtyDiana.Helpers
{
    internal static class DownloadHelperLinux
    {
        internal static async Task<List<DownloadItem>> DownloadAllReleasesAsync()
        {
            var items = new List<DownloadItem>();

            var repos = new List<(string owner, string repo)>
            {
                ("grimdoomer", "Xbox360BadUpdate"),
                ("Byrom90", "XeUnshackle"),
                ("FreeMyXe", "FreeMyXe"),
                ("shutterbug2000", "ABadAvatar")
            };

            foreach (var (owner, repo) in repos)
            {
                try
                {
                    var downloaded = await DownloadLatestReleaseAsync(owner, repo);
                    items.AddRange(downloaded);
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(
                        $"[red]{Markup.Escape("[!]")} Failed to query {owner}/{repo}: {ex.Message}[/]"
                    );
                }
            }

            var localCriticalItems = new List<DownloadItem>
            {
                ("XeXmenu", "https://github.com/Pdawg-bytes/BadBuilder/releases/download/v0.10a/MenuData.7z"),
                ("Rock Band Blitz", "https://github.com/Pdawg-bytes/BadBuilder/releases/download/v0.10a/GameData.zip"),
                ("Simple 360 NAND Flasher", "https://github.com/Pdawg-bytes/BadBuilder/releases/download/v0.10a/Flasher.7z"),
            };

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DirtyDiana-Linux");

            foreach (var item in localCriticalItems)
            {
                string fileName = Path.GetFileName(item.url);
                string destinationPath = Path.Combine(DOWNLOAD_DIR, fileName);

                // Skip if already downloaded
                if (File.Exists(destinationPath))
                {
                    AnsiConsole.MarkupLine(
                        $"[#76B900]{Markup.Escape("[+]")} Using cached file: {fileName}[/]"
                    );
                    items.Add(new DownloadItem(item.name, destinationPath));
                    continue;
                }

                try
                {
                    await DownloadFileAsync(client, item.url, destinationPath);

                    AnsiConsole.MarkupLine(
                        $"[#76B900]{Markup.Escape("[+]")} Downloaded {fileName} to {destinationPath}[/]"
                    );

                    items.Add(new DownloadItem(item.name, destinationPath));
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(
                        $"[red]{Markup.Escape("[!]")} Failed to download {item.name}: {ex.Message}[/]"
                    );
                }
            }

            return items;
        }

        private static async Task<List<DownloadItem>> DownloadLatestReleaseAsync(string owner, string repo)
        {
            var items = new List<DownloadItem>();

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DirtyDiana-Linux");

            bool isAbadAvatar = repo == "ABadAvatar";
            string apiUrl = isAbadAvatar
            ? $"https://api.github.com/repos/{owner}/{repo}/releases"
            : $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(apiUrl);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new Exception($"Unable to fetch release info: {ex.Message}");
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            JsonElement releaseObj;

            if (isAbadAvatar)
            {
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape("[!]")} No releases found for {owner}/{repo} — skipping.[/]");
                    return items;
                }

                // Get first non-draft, non-prerelease release
                releaseObj = doc.RootElement.EnumerateArray()
                .FirstOrDefault(r =>
                (!r.TryGetProperty("draft", out var draft) || !draft.GetBoolean()) &&
                (!r.TryGetProperty("prerelease", out var pre) || !pre.GetBoolean())
                );

                if (releaseObj.ValueKind == JsonValueKind.Undefined)
                    releaseObj = doc.RootElement[0];

                if (!releaseObj.TryGetProperty("assets", out var assets))
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape("[!]")} No assets found in latest release of {owner}/{repo} — skipping.[/]");
                    return items;
                }

                foreach (var asset in assets.EnumerateArray())
                {
                    await ProcessAsset(asset, items, client);
                }
            }
            else
            {
                releaseObj = doc.RootElement;

                if (!releaseObj.TryGetProperty("assets", out var assets))
                {
                    AnsiConsole.MarkupLine($"[yellow]{Markup.Escape("[!]")} No assets found in latest release of {owner}/{repo} — skipping.[/]");
                    return items;
                }

                foreach (var asset in assets.EnumerateArray())
                {
                    await ProcessAsset(asset, items, client);
                }
            }

            return items;
        }

        private static async Task ProcessAsset(JsonElement asset, List<DownloadItem> items, HttpClient client)
        {
            string name = asset.GetProperty("name").GetString() ?? "";
            string url = asset.GetProperty("browser_download_url").GetString() ?? "";

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url))
                return;

            string friendlyName = name switch
            {
                var n when n.Contains("Free", StringComparison.OrdinalIgnoreCase) => "FreeMyXe",
                var n when n.Contains("Tools", StringComparison.OrdinalIgnoreCase) => "BadUpdate Tools",
                var n when n.Contains("BadUpdate", StringComparison.OrdinalIgnoreCase) => "BadUpdate",
                var n when n.Contains("XeUnshackle", StringComparison.OrdinalIgnoreCase) => "XeUnshackle",
                var n when n.Contains("ABadAvatar", StringComparison.OrdinalIgnoreCase) => "ABadAvatar",
                _ => name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name
            };

            string destinationPath = Path.Combine(DOWNLOAD_DIR, name);

            if (File.Exists(destinationPath))
            {
                AnsiConsole.MarkupLine($"[#76B900]{Markup.Escape("[+]")} Using cached file: {name}[/]");
                items.Add(new DownloadItem(friendlyName, destinationPath));
                return;
            }

            try
            {
                await DownloadFileAsync(client, url, destinationPath);

                AnsiConsole.MarkupLine($"[#76B900]{Markup.Escape("[+]")} Downloaded {name} to {destinationPath}[/]");

                items.Add(new DownloadItem(friendlyName, destinationPath));
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]{Markup.Escape("[!]")} Failed to download {name}: {ex.Message}[/]");
            }
        }

        private static async Task DownloadFileAsync(HttpClient client, string url, string destination)
        {
            byte[] buffer = new byte[8192];

            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? DOWNLOAD_DIR);

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
            }
        }
    }
}
