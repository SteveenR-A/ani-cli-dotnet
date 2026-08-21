using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace AniCS.Desktop.Services;

public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;

    [JsonPropertyName("assets")]
    public GitHubAsset[] Assets { get; set; } = Array.Empty<GitHubAsset>();
}

/// <summary>
/// Fetches the latest release from GitHub, downloads the MSI installer and
/// applies it silently (msiexec /qn), then relaunches the app.
/// </summary>
public sealed class AppUpdateService : IDisposable
{
    private const string RepoOwner = "SteveenR-A";
    private const string RepoName = "ani-cli-dotnet";

    private readonly HttpClient _http = new();

    public AppUpdateService()
    {
        // Limpieza automática al iniciar: borra instaladores o logs residuales de sesiones anteriores
        CleanOldUpdates();
    }

    public string CurrentVersion =>
        AppInfo.CurrentVersion;

    public string UpdatesDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniCS", "updates");

    /// <summary>
    /// Deletes all leftover update installers (.msi), logs and batch scripts from %LocalAppData%/AniCS/updates.
    /// </summary>
    public void CleanOldUpdates(string? keepFileName = null)
    {
        try
        {
            if (!Directory.Exists(UpdatesDir)) return;
            var dir = new DirectoryInfo(UpdatesDir);
            foreach (var file in dir.GetFiles())
            {
                if (keepFileName != null && file.Name.Equals(keepFileName, StringComparison.OrdinalIgnoreCase))
                    continue;

                try { file.Delete(); } catch (Exception ex) { AppLogger.Debug("AppUpdateService", $"Failed to delete old file '{file.Name}': {ex.Message}"); }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("AppUpdateService.CleanOldUpdates", ex);
        }
    }

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
        catch (Exception ex)
        {
            AppLogger.Error("AppUpdateService.FetchLatestReleaseAsync", ex);
            return null;
        }
    }

    /// <summary>Parses a tag like "v1.5.6" or "1.5.6" into a Version.</summary>
    public static Version? ParseVersion(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return null;
        var v = tag.Trim().TrimStart('v', 'V');
        int dashIdx = v.IndexOf('-');
        if (dashIdx > 0) v = v.Substring(0, dashIdx);
        if (!v.Contains('.')) v += ".0";
        return Version.TryParse(v, out var ver) ? ver : null;
    }

    /// <summary>True when the given release is newer than the running assembly.</summary>
    public bool IsNewerAvailable(GitHubRelease? release, out Version? latest)
    {
        latest = release == null ? null : ParseVersion(release.TagName);
        if (latest == null) return false;
        var current = ParseVersion(CurrentVersion) ?? new Version(0, 0, 0);
        return latest > current;
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
            CleanOldUpdates(asset.Name);
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
        catch (Exception ex)
        {
            AppLogger.Warn("AppUpdateService", $"DownloadMsiAsync failed for '{asset.Name}': {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Inicia la instalación del MSI en un proceso desacoplado, espera a que finalice y relanza la aplicación.
    /// Utiliza /passive para permitir la elevación de permisos (UAC) requerida por Program Files y mostrar el progreso.
    /// </summary>
    public void ApplyAndRelaunch(string msiPath)
    {
        try
        {
            var programFilesExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AniCS", "AniCS.Desktop.exe");
            var currentExe = Environment.ProcessPath ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AniCS.Desktop.exe");
            var logPath = Path.Combine(UpdatesDir, "msi_install.log");
            var scriptPath = Path.Combine(UpdatesDir, "apply_update.cmd");

            Directory.CreateDirectory(UpdatesDir);

            // Script para esperar cierre, ejecutar msiexec con elevación UAC permitida (/passive), limpiar residuos y relanzar la app
            var scriptLines = new[]
            {
                "@echo off",
                "timeout /t 2 /nobreak >nul",
                $"msiexec.exe /i \"{msiPath}\" /passive /norestart /lv \"{logPath}\"",
                "timeout /t 1 /nobreak >nul",
                $"del /f /q \"{msiPath}\" >nul 2>&1",
                $"del /f /q \"{logPath}\" >nul 2>&1",
                $"if exist \"{programFilesExe}\" (",
                $"    start \"\" \"{programFilesExe}\"",
                ") else (",
                $"    start \"\" \"{currentExe}\"",
                ")",
                "(goto) 2>nul & del \"%~f0\""
            };

            File.WriteAllLines(scriptPath, scriptLines);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process.Start(psi);
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AppUpdateService.ApplyAndRelaunch", ex);
        }
    }

    public void Dispose() => _http.Dispose();
}