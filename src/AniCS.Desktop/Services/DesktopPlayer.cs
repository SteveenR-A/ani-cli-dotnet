using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace AniCS.Desktop.Services;

public static class DesktopPlayer
{
    public static void Play(string url, string title, string? referer)
    {
        var exe = IsInstalled("mpv") ? "mpv" : IsInstalled("mpvnet") ? "mpvnet" : null;

        if (exe == null)
        {
            // Fallback: Open in browser if mpv is not installed
            OpenBrowser(url);
            return;
        }

        var args = new List<string>
        {
            "--force-window=yes",
            "--cache=yes",
            "--cache-pause-wait=5"
        };

        bool isJkAnime = referer != null && referer.Contains("jkanime.net", StringComparison.OrdinalIgnoreCase);
        bool isMediafire = referer != null && referer.Contains("mediafire.com", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(referer))
        {
            args.Add($"--http-header-fields=Referer: {referer}");
        }

        if (isJkAnime || isMediafire)
        {
            args.Add("--demuxer-max-bytes=5M");
            args.Add("--demuxer-readahead-secs=15");
            args.Add("--cache-secs=30");
            args.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_delay_max=5");

            string origin = isJkAnime ? "https://jkanime.net" : "https://www.mediafire.com";
            // For mpv we can pass multiple headers joined by commas
            string headers = $"Referer: {referer},Origin: {origin},Accept-Language: es-419,es;q=0.9,en;q=0.8,Accept: */*,Connection: keep-alive,Sec-Fetch-Dest: video,Sec-Fetch-Mode: no-cors,Sec-Fetch-Site: cross-site";
            
            // Remove previous Referer and add the combined one
            args.RemoveAll(a => a.StartsWith("--http-header-fields="));
            args.Add($"--http-header-fields={headers}");
        }
        else
        {
            args.Add("--demuxer-readahead-secs=20");
            args.Add("--demuxer-max-back-bytes=200M");
        }

        args.Add($"--title={title}");
        args.Add(url);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error launching player: {ex.Message}");
            OpenBrowser(url);
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static async System.Threading.Tasks.Task DownloadAsync(string videoUrl, AniCS.Models.AnimeResult anime, AniCS.Models.Episode episode, string downloadDir, string? referer = null)
    {
        try
        {
            Directory.CreateDirectory(downloadDir);
            var safeTitle = string.Join("_", anime.Title.Split(Path.GetInvalidFileNameChars()));
            var filenamePattern = $"[AniCS] {safeTitle} - Ep {episode.EpisodeNumber}.%(ext)s";
            var outputPath = Path.Combine(downloadDir, filenamePattern);

            var refererArg = string.IsNullOrEmpty(referer) ? "" : $"--add-header \"Referer:{referer}\" ";

            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"{refererArg}-o \"{outputPath}\" \"{videoUrl}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            p.Start();
            await p.WaitForExitAsync();

            if (p.ExitCode == 0)
            {
                var actualFileName = Directory.GetFiles(downloadDir, $"[AniCS] {safeTitle} - Ep {episode.EpisodeNumber}.*").FirstOrDefault();
                if (actualFileName != null)
                {
                    DownloadManager.RecordDownload(anime.Title, anime.Url, anime.ThumbnailUrl, episode.EpisodeNumber, episode.Title, actualFileName);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descargar: {ex.Message}");
        }
    }

    private static bool IsInstalled(string command)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths == null) return false;

        string extension = OperatingSystem.IsWindows() ? ".exe" : "";

        foreach (var path in paths)
        {
            var fullPath = Path.Combine(path.Trim(), command + extension);
            if (File.Exists(fullPath)) return true;
        }

        return false;
    }
}
