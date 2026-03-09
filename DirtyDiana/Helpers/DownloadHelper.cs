using Octokit;
using Spectre.Console;
using System.Net.Http;
using static DirtyDiana.Utilities.Constants;

namespace DirtyDiana.Helpers
{
    internal static class DownloadHelper
    {
        internal static async Task GetGitHubAssets(List<DownloadItem> items)
        {
            // Create GitHub client
            var gitClient = new GitHubClient(new ProductHeaderValue("DirtyDiana"));

            string? token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            if (!string.IsNullOrEmpty(token))
            {
                gitClient.Credentials = new Credentials(token);
            }

            var repos = new List<string>
            {
                "grimdoomer/Xbox360BadUpdate",
                "Byrom90/XeUnshackle",
                "FreeMyXe/FreeMyXe"
            };

            foreach (var repo in repos)
            {
                string[] split = repo.Split('/');
                string owner = split[0];
                string name = split[1];

                IReadOnlyList<Release> releases;
                try
                {
                    releases = await gitClient.Repository.Release.GetAll(owner, name);
                }
                catch (NotFoundException)
                {
                    AnsiConsole.MarkupLine($"[yellow][[!]] No releases found for {repo} — skipping.[/]");
                    continue;
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red][[!]] Failed to query {repo}: {ex.Message}[/]");
                    continue;
                }

                if (releases == null || releases.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow][[!]] No releases available for {repo} — skipping.[/]");
                    continue;
                }

                var latestRelease = releases
                .Where(r => r != null && !r.Draft && !r.Prerelease)
                .OrderByDescending(r => r.PublishedAt)
                .FirstOrDefault();

                if (latestRelease == null || latestRelease.Assets == null || latestRelease.Assets.Count == 0)
                {
                    AnsiConsole.MarkupLine($"[yellow][[!]] No usable assets in releases for {repo} — skipping.[/]");
                    continue;
                }

                foreach (var asset in latestRelease.Assets)
                {
                    if (asset == null || string.IsNullOrEmpty(asset.Name) || string.IsNullOrEmpty(asset.BrowserDownloadUrl))
                        continue;

                    string friendlyName = asset.Name switch
                    {
                        var n when n.Contains("Free", StringComparison.OrdinalIgnoreCase) => "FreeMyXe",
                        var n when n.Contains("Tools", StringComparison.OrdinalIgnoreCase) => "BadUpdate Tools",
                        var n when n.Contains("BadUpdate", StringComparison.OrdinalIgnoreCase) => "BadUpdate",
                        var n when n.Contains("XeUnshackle", StringComparison.OrdinalIgnoreCase) => "XeUnshackle",
                        _ => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ? asset.Name[..^4] : asset.Name
                    };

                    items.Add(new(friendlyName, asset.BrowserDownloadUrl));
                }
            }
        }

        internal static async Task DownloadFileAsync(HttpClient client, ProgressTask task, string url)
        {
            try
            {
                byte[] buffer = new byte[8192];

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                task.MaxValue(response.Content.Headers.ContentLength ?? 0);
                task.StartTask();

                string filename = Path.GetFileName(url);
                string destination = Path.Combine(DOWNLOAD_DIR, filename);

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new System.IO.FileStream(destination,
                                                                System.IO.FileMode.Create, System.IO.FileAccess.Write,
                                                                System.IO.FileShare.None, 8192, true);

                int bytesRead;
                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    task.Increment(bytesRead);
                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red][[!]] Error downloading file from {url}: {ex.Message}[/]");
            }
        }
    }
}
