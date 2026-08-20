using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using AndroidX.Core.Content;
using Java.IO;

namespace AniCS.Android.Services;

/// <summary>
/// Android implementation of in-app update.
/// Checks the latest GitHub Release, downloads the APK with background WakeLock & Range resuming,
/// and triggers installation via Android PackageInstaller.
/// </summary>
public class AndroidUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/SteveenR-A/ani-cli-dotnet/releases/latest";
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    static AndroidUpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AniCS-Android/1.0");
    }

    public record UpdateInfo(string Version, string ApkUrl, string ReleaseNotes);

    public static string GetApkPath(Activity activity, string? version = null)
    {
        var cacheDir = activity.CacheDir?.AbsolutePath ?? activity.FilesDir?.AbsolutePath ?? "";
        return string.IsNullOrEmpty(version)
            ? Path.Combine(cacheDir, "AniCS-update.apk")
            : Path.Combine(cacheDir, $"AniCS-update-v{version}.apk");
    }

    public static string GetPartPath(Activity activity, string? version = null)
    {
        return GetApkPath(activity, version) + ".part";
    }

    /// <summary>
    /// Checks if a valid, fully downloaded APK file is already cached on disk.
    /// </summary>
    public static bool IsApkReady(Activity? activity, string? version = null)
    {
        if (activity == null) return false;
        try
        {
            var apkPath = GetApkPath(activity, version);
            if (System.IO.File.Exists(apkPath))
            {
                var fi = new FileInfo(apkPath);
                return fi.Length > 2 * 1024 * 1024; // > 2MB valid APK
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Checks if there is a partial download (.part) available to resume.
    /// </summary>
    public static bool HasPartialDownload(Activity? activity, string? version = null)
    {
        if (activity == null) return false;
        try
        {
            var partPath = GetPartPath(activity, version);
            if (System.IO.File.Exists(partPath))
            {
                var fi = new FileInfo(partPath);
                return fi.Length > 0;
            }
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Returns info about the latest release, or null if already up to date / error.
    /// </summary>
    public static async Task<UpdateInfo?> CheckAsync(string currentVersion)
    {
        try
        {
            var json = await _http.GetStringAsync(GitHubApiUrl);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tag = root.GetProperty("tag_name").GetString() ?? string.Empty;
            var latestVersion = tag.TrimStart('v');
            var notes = root.GetProperty("body").GetString() ?? string.Empty;

            if (!IsNewer(latestVersion, currentVersion)) return null;

            // Find the APK asset
            foreach (var asset in root.GetProperty("assets").EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? string.Empty;
                if (name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                {
                    var url = asset.GetProperty("browser_download_url").GetString() ?? string.Empty;
                    return new UpdateInfo(latestVersion, url, notes);
                }
            }
        }
        catch (Exception ex)
        {
            AniCS.AppLogger.Error("AndroidUpdateService.CheckAsync", ex);
        }

        return null;
    }

    public static Task<bool> DownloadAndInstallAsync(
        Activity activity,
        string apkUrl,
        Action<double>? onProgress)
        => DownloadAndInstallAsync(activity, apkUrl, null, onProgress, CancellationToken.None);

    /// <summary>
    /// Downloads the APK with WakeLock and HTTP Range resume support, then triggers installation.
    /// </summary>
    public static async Task<bool> DownloadAndInstallAsync(
        Activity activity,
        string apkUrl,
        string? version,
        Action<double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var apkPath = GetApkPath(activity, version);
        var partPath = GetPartPath(activity, version);

        if (IsApkReady(activity, version))
        {
            TriggerInstall(activity, version);
            return true;
        }

        PowerManager.WakeLock? wakeLock = null;
        try
        {
            var powerManager = activity.GetSystemService(Context.PowerService) as PowerManager;
            wakeLock = powerManager?.NewWakeLock(WakeLockFlags.Partial, "AniCS:UpdateDownload");
            wakeLock?.Acquire(15 * 60 * 1000L /* 15 minutos max */);
        }
        catch (Exception ex)
        {
            AniCS.AppLogger.Error("AndroidUpdateService.WakeLock", ex);
        }

        try
        {
            long existingBytes = 0;
            if (System.IO.File.Exists(partPath))
            {
                try { existingBytes = new FileInfo(partPath).Length; } catch { existingBytes = 0; }
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, apkUrl);
            if (existingBytes > 0)
            {
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);
            }

            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            bool isPartial = response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            if (!response.IsSuccessStatusCode && !isPartial)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    try { System.IO.File.Delete(partPath); } catch { }
                    existingBytes = 0;
                    using var freshReq = new HttpRequestMessage(HttpMethod.Get, apkUrl);
                    using var freshResp = await _http.SendAsync(freshReq, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    freshResp.EnsureSuccessStatusCode();
                    await DownloadStreamToFile(freshResp, partPath, 0, onProgress, cancellationToken);
                }
                else
                {
                    response.EnsureSuccessStatusCode();
                }
            }
            else
            {
                long startOffset = isPartial ? existingBytes : 0;
                await DownloadStreamToFile(response, partPath, startOffset, onProgress, cancellationToken);
            }

            if (System.IO.File.Exists(partPath))
            {
                if (System.IO.File.Exists(apkPath))
                {
                    try { System.IO.File.Delete(apkPath); } catch { }
                }
                System.IO.File.Move(partPath, apkPath);
            }

            TriggerInstall(activity, version);
            return true;
        }
        catch (Exception ex)
        {
            AniCS.AppLogger.Error("AndroidUpdateService.Download", ex);
            Toast.MakeText(activity, "Descarga pausada o interrumpida. Puedes reanudarla cuando desees.", ToastLength.Long)?.Show();
            return false;
        }
        finally
        {
            try
            {
                if (wakeLock != null && wakeLock.IsHeld)
                {
                    wakeLock.Release();
                }
            }
            catch { }
        }
    }

    private static async Task DownloadStreamToFile(
        HttpResponseMessage response,
        string partPath,
        long startOffset,
        Action<double>? onProgress,
        CancellationToken cancellationToken)
    {
        var contentLen = response.Content.Headers.ContentLength ?? -1L;
        var totalBytes = contentLen > 0 ? startOffset + contentLen : -1L;

        var buffer = new byte[81920];
        long currentDownloaded = startOffset;

        var fileMode = startOffset > 0 ? FileMode.Append : FileMode.Create;
        await using var file = new FileStream(partPath, fileMode, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            currentDownloaded += read;
            if (totalBytes > 0)
            {
                onProgress?.Invoke((double)currentDownloaded / totalBytes);
            }
        }
    }

    /// <summary>
    /// Launches the Android system package installer for the downloaded APK.
    /// </summary>
    public static void TriggerInstall(Activity activity, string? version = null)
    {
        try
        {
            var apkPath = GetApkPath(activity, version);
            if (!System.IO.File.Exists(apkPath))
            {
                Toast.MakeText(activity, "El archivo de actualización no se encontró.", ToastLength.Long)?.Show();
                return;
            }

            var apkFile = new Java.IO.File(apkPath);
            var uri = Build.VERSION.SdkInt >= BuildVersionCodes.N
                ? FileProvider.GetUriForFile(
                    activity,
                    $"{activity.PackageName}.fileprovider",
                    apkFile)
                : global::Android.Net.Uri.FromFile(apkFile);

            var intent = new Intent(Intent.ActionView)
                .SetDataAndType(uri, "application/vnd.android.package-archive")
                .AddFlags(ActivityFlags.GrantReadUriPermission)
                .AddFlags(ActivityFlags.NewTask);

            activity.StartActivity(intent);
        }
        catch (Exception ex)
        {
            AniCS.AppLogger.Error("AndroidUpdateService.Install", ex);
            Toast.MakeText(activity, "No se pudo iniciar la instalación.", ToastLength.Long)?.Show();
        }
    }

    private static bool IsNewer(string latest, string current)
    {
        return Version.TryParse(latest, out var l)
            && Version.TryParse(current, out var c)
            && l > c;
    }
}
