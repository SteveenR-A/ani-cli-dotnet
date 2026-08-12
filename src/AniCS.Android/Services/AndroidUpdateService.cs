using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
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
/// Checks the latest GitHub Release, downloads the APK, and triggers installation
/// via Android PackageInstaller (requires REQUEST_INSTALL_PACKAGES permission).
/// </summary>
public class AndroidUpdateService
{
    private const string GitHubApiUrl = "https://api.github.com/repos/SteveenR-A/ani-cli-dotnet/releases/latest";
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    static AndroidUpdateService()
    {
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AniCS-Android/1.0");
    }

    public record UpdateInfo(string Version, string ApkUrl, string ReleaseNotes);

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

    /// <summary>
    /// Downloads the APK to the app's cache dir and triggers system install prompt.
    /// </summary>
    public static async Task DownloadAndInstallAsync(
        Activity activity,
        string apkUrl,
        Action<double>? onProgress = null)
    {
        var cacheDir = activity.CacheDir!.AbsolutePath;
        var apkPath = Path.Combine(cacheDir, "AniCS-update.apk");

        try
        {
            using var response = await _http.GetAsync(apkUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var buffer = new byte[81920];
            long downloaded = 0;

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var file = System.IO.File.Create(apkPath);

            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                if (totalBytes > 0)
                    onProgress?.Invoke((double)downloaded / totalBytes);
            }
        }
        catch (Exception ex)
        {
            AniCS.AppLogger.Error("AndroidUpdateService.Download", ex);
            Toast.MakeText(activity, "Error al descargar la actualización.", ToastLength.Long)?.Show();
            return;
        }

        // Trigger install
        TriggerInstall(activity, apkPath);
    }

    private static void TriggerInstall(Activity activity, string apkPath)
    {
        try
        {
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
