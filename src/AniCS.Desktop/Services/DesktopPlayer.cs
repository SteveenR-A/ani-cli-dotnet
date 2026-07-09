using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace AniCS.Desktop.Services;

public enum DownloadResult
{
    Success,
    Cancelled,
    Error
}

public static class DesktopPlayer
{
    private static readonly List<Process> _activeProcesses = new();

    static DesktopPlayer()
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => KillAll();
        Console.CancelKeyPress += (s, e) => KillAll();
    }

    private static void KillAll()
    {
        lock (_activeProcesses)
        {
            foreach (var p in _activeProcesses.ToList())
            {
                try
                {
                    if (!p.HasExited) p.Kill(true);
                }
                catch { }
            }
            _activeProcesses.Clear();
        }
    }

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
            "--cache-pause-wait=1"   // 1 s de buffer para reanudar tras seek (independiente del readahead)
        };

        bool isJkAnime = referer != null && referer.Contains("jkanime.net", StringComparison.OrdinalIgnoreCase);
        bool isMediafire = referer != null && referer.Contains("mediafire.com", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(referer))
        {
            args.Add($"--http-header-fields=Referer: {referer}");
        }

        if (isJkAnime || isMediafire)
        {
            args.Add("--demuxer-max-bytes=150M");       // Hasta 150 MB en buffer
            args.Add("--demuxer-max-back-bytes=50M");   // Guarda 50 MB ya reproducidos (rebobinar sin re-descargar)
            args.Add("--demuxer-readahead-secs=120");   // Pre-descarga hasta 2 min durante reproducción normal
            args.Add("--cache-secs=120");               // Cache total 2 min (cache-pause-wait controla seek por separado)
            args.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_delay_max=5");

            string origin = isJkAnime ? "https://jkanime.net" : "https://www.mediafire.com";
            string headers = $"Referer: {referer},Origin: {origin},Accept-Language: es-419,es;q=0.9,en;q=0.8,Accept: */*,Connection: keep-alive,Sec-Fetch-Dest: video,Sec-Fetch-Mode: no-cors,Sec-Fetch-Site: cross-site";

            // Remove previous Referer and add the combined one
            args.RemoveAll(a => a.StartsWith("--http-header-fields="));
            args.Add($"--http-header-fields={headers}");
        }
        else
        {
            args.Add("--demuxer-max-bytes=150M");
            args.Add("--demuxer-max-back-bytes=50M");
            args.Add("--demuxer-readahead-secs=120");
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

            var p = new Process { StartInfo = startInfo };
            p.EnableRaisingEvents = true;
            p.Exited += (s, e) =>
            {
                lock (_activeProcesses) _activeProcesses.Remove(p);
            };
            lock (_activeProcesses) _activeProcesses.Add(p);
            p.Start();
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

    public static async System.Threading.Tasks.Task<DownloadResult> DownloadAsync(string videoUrl, AniCS.Models.AnimeResult anime, AniCS.Models.Episode episode, string downloadDir, string? referer = null, Action<double>? onProgress = null, System.Threading.CancellationToken cancellationToken = default)
    {
        string animeDir = "";
        string episodeNumStr = "";
        Process? p = null;
        try
        {
            var safeTitle = string.Join("_", anime.Title.Split(Path.GetInvalidFileNameChars()));
            animeDir = Path.Combine(downloadDir, safeTitle);
            Directory.CreateDirectory(animeDir);

            episodeNumStr = string.IsNullOrWhiteSpace(episode.EpisodeNumber) ? "Desconocido" : episode.EpisodeNumber;
            var filenamePattern = $"Episodio {episodeNumStr}.%(ext)s";
            var outputPath = Path.Combine(animeDir, filenamePattern);

            var refererArg = string.IsNullOrEmpty(referer) ? "" : $"--add-header \"Referer:{referer}\" ";

            p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--newline --concurrent-fragments 1 --hls-prefer-native {refererArg}-o \"{outputPath}\" \"{videoUrl}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true, // Prevent stderr deadlock
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                },
                EnableRaisingEvents = true
            };

            p.Exited += (s, e) =>
            {
                lock (_activeProcesses) _activeProcesses.Remove(p);
            };
            lock (_activeProcesses) _activeProcesses.Add(p);
            p.Start();
            _ = p.StandardError.ReadToEndAsync(); // Drain stderr so it doesn't block

            using var reg = cancellationToken.Register(() =>
            {
                try { if (!p.HasExited) p.Kill(true); } catch { }
            });

            var regex = new System.Text.RegularExpressions.Regex(@"\[download\]\s+(\d+\.\d+)%");

            while (true)
            {
                var line = await p.StandardOutput.ReadLineAsync();
                if (line == null) break;
                if (onProgress != null)
                {
                    var match = regex.Match(line);
                    if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double progress))
                    {
                        onProgress(progress);
                    }
                }
            }

            await p.WaitForExitAsync(cancellationToken);

            if (p.ExitCode == 0 && !cancellationToken.IsCancellationRequested)
            {
                var actualFileName = Directory.GetFiles(animeDir, $"Episodio {episodeNumStr}.*").FirstOrDefault();
                if (actualFileName != null)
                {
                    DownloadManager.RecordDownload(anime.Title, anime.Url, anime.ThumbnailUrl, episode.EpisodeNumber, episode.Title, actualFileName);
                    return DownloadResult.Success;
                }
            }
            return cancellationToken.IsCancellationRequested ? DownloadResult.Cancelled : DownloadResult.Error;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Descarga cancelada o pausada.");
            try { p?.Kill(true); p?.WaitForExit(2000); } catch { }
            return DownloadResult.Cancelled;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descargar: {ex.Message}");
            return DownloadResult.Error;
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
