using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AniCS.Desktop.Services;

public record GitHubAsset(string Name, string BrowserDownloadUrl, long Size);

public record GitHubRelease(string TagName, string Name, string Body, GitHubAsset[] Assets);

/// <summary>
/// Fetches the latest release from GitHub, downloads the MSI installer and
/// applies it silently (msiexec /qn), then relaunches the app.
/// </summary>
public sealed class AppUpdateService : IDisposable
{
    private const string RepoOwner = "SteveenR-A";
    private const string RepoName = "ani-cli-dotnet";

    private readonly HttpClient _http = new();

    public string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";

    public string UpdatesDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniCS", "updates");

    /// <summary>Latest non-draft/non-prerelease release from GitHub, or null on failure.</summary>
    public async Task<GitHubRelease?> FetchLatestReleaseAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd($"AniCS/{CurrentVersion}");
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<GitHubRelease>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Parses a tag like "v1.5.6" or "1.5.6" into a Version.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var v = tag.Trim().TrimStart('v', 'V');
        return Version.TryParse(v, out var ver) ? ver : null;
    }

    /// <summary>True when the given release is newer than the running assembly.</summary>
    public bool IsNewerAvailable(GitHubRelease? release, out Version? latest)
    {
        latest = release == null ? null : ParseVersion(release.TagName);
        if (latest == null) return false;
        return latest > new Version(CurrentVersion);
    }

    /// <summary>Finds the first installer asset (*.msi) in the release.</summary>
    public GitHubAsset? FindMsi(GitHubRelease release)
    {
        if (release.Assets == null) return null;
        foreach (var asset in release.Assets)
        {
            if (asset.Name?.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true)
                return asset;
        }
        return null;
    }

    /// <summary>Downloads the asset into %LocalAppData%/AniCS/updates. Returns the local file path.</summary>
    public async Task<string?> DownloadMsiAsync(GitHubAsset asset, IProgress<double>? progress = null, CancellationToken ct = default)
    {
        try
        {
            Directory.CreateDirectory(UpdatesDir);
            var target = Path.Combine(UpdatesDir, asset.Name);

            using var resp = await _http.GetAsync(asset.BrowserDownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var total = asset.Size > 0 ? asset.Size : resp.Content.Headers.ContentLength ?? 0;
            await using var src = await resp.Content.ReadAsStreamAsync(ct);
            await using var dst = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                readTotal += read;
                if (total > 0) progress?.Report(readTotal * 100.0 / total);
            }

            return target;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Starts the silent MSI install in a detached process, waits for it to finish
    /// and relaunches the app. The current process exits immediately so that
    /// Windows Installer can replace the files that are in use.
    /// </summary>
    public void ApplyAndRelaunch(string msiPath)
    {
        var exe = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AniCS.Desktop.exe");
        var cmd = $"/c start /wait msiexec /i \"{msiPath}\" /qn /norestart & start \"\" \"{exe}\"";
        var psi = new ProcessStartInfo("cmd.exe", cmd)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi);
        Environment.Exit(0);
    }

    public void Dispose() => _http.Dispose();
}