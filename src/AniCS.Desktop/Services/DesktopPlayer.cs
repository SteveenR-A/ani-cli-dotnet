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
    public static event Action<string>? OnPlayerError;

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

    public static async System.Threading.Tasks.Task<string> ResolveRedirectorUrlAsync(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.Contains("redirector.php"))
            return url;

        url = url.Replace("\\", "");

        try
        {
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(AniCS.ConfigManager.Current.RandomUserAgent);
            request.Headers.Referrer = new Uri("https://www.mundodonghua.com/");
            request.Headers.Add("Origin", "https://www.mundodonghua.com");

            using var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = true };
            using var client = new System.Net.Http.HttpClient(handler);
            using var response = await client.SendAsync(request);

            if (response.RequestMessage?.RequestUri != null && 
                !response.RequestMessage.RequestUri.ToString().Contains("redirector.php"))
            {
                return response.RequestMessage.RequestUri.ToString();
            }

            // Si la respuesta sigue teniendo redirector.php (es decir, devolvió 200 OK con HTML), analizar el HTML
            var html = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(html))
            {
                // 1. Buscar directamente .m3u8 o .mp4
                var streamMatch = System.Text.RegularExpressions.Regex.Match(html, @"https?://[^\s""'<>\\]+\.(?:m3u8|mp4)[^\s""'<>\\]*");
                if (streamMatch.Success)
                {
                    return streamMatch.Value.Replace("\\", "");
                }

                // 2. Buscar file: "..." o src: "..."
                var fileMatch = System.Text.RegularExpressions.Regex.Match(html, @"(?:file|src):\s*[""'](https?://[^""']+)[""']");
                if (fileMatch.Success)
                {
                    return fileMatch.Groups[1].Value.Replace("\\", "");
                }

                // 3. Buscar iframe
                var iframeMatch = System.Text.RegularExpressions.Regex.Match(html, @"<iframe[^>]+src=[""']([^""']+)[""']");
                if (iframeMatch.Success)
                {
                    return iframeMatch.Groups[1].Value.Replace("\\", "");
                }
            }
        }
        catch { }

        return url;
    }

    public static void Play(string url, string title, string? referer, string quality = "Mejor")
    {
        url = System.Threading.Tasks.Task.Run(() => ResolveRedirectorUrlAsync(url)).GetAwaiter().GetResult();

        var exe = GetExecutablePath("mpv") ?? GetExecutablePath("mpvnet");

        if (exe == null)
        {
            throw new Exception("mpv no está instalado. Por favor, descarga mpv y agrégalo al PATH o a la carpeta del programa.");
        }

        var args = new List<string>
        {
            "--force-window=yes",
            "--autofit=1024x576", // Para evitar la ventana super pequeña en JKAnime
            "--cache=yes",
            "--cache-pause=no" // Iniciar reproducción inmediatamente sin esperar a llenar el búfer
        };

        if (url.Contains(".m3u8") || url.Contains(".mp4"))
        {
            // Si el enlace ya es directo, omitir yt-dlp por completo ahorra hasta 5 segundos de carga inicial
            args.Add("--ytdl=no");
        }
        else if (quality != "Mejor")
        {
            // mpv soporta ytdl-format solo para sitios soportados
            string height = quality.Replace("p", "");
            args.Add($"--ytdl-format=bestvideo[height<=?{height}]+bestaudio/best[height<=?{height}]");
        }

        if (!string.IsNullOrEmpty(referer))
        {
            referer = System.Threading.Tasks.Task.Run(() => ResolveRedirectorUrlAsync(referer)).GetAwaiter().GetResult();
        }

        args.Add("--demuxer-max-bytes=150M");
        args.Add("--demuxer-max-back-bytes=50M");
        args.Add("--demuxer-readahead-secs=120");
        args.Add("--cache-secs=120");
        args.Add("--hr-seek=yes");
        args.Add("--network-timeout=15");
        args.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_on_http_error=4xx,reconnect_delay_max=10");

        if (exe.Contains("mpv", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--force-window=immediate");
            args.Add("--keep-open=yes");
            args.Add("--geometry=65%");
            args.Add("--autofit=1280x720");

            var ua = AniCS.ConfigManager.Current.RandomUserAgent.Replace(",", ";");
            var headerList = new List<string>
            {
                "Accept-Language: es-419",
                "Accept: */*",
                "Sec-Fetch-Dest: empty",
                "Sec-Fetch-Mode: cors",
                "Sec-Fetch-Site: cross-site",
                $"User-Agent: {ua}"
            };

            if (!string.IsNullOrEmpty(referer))
            {
                headerList.Add($"Referer: {referer}");
                try
                {
                    var uri = new Uri(referer);
                    headerList.Add($"Origin: {uri.GetLeftPart(UriPartial.Authority)}");
                }
                catch { }
            }

            args.Add($"--http-header-fields={string.Join(",", headerList)}");
        }

        args.Add($"--title={title}");
        args.Add(url);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            foreach (var arg in args)
                startInfo.ArgumentList.Add(arg);

            var p = new Process { StartInfo = startInfo };
            p.EnableRaisingEvents = true;
            p.Exited += (s, e) =>
            {
                lock (_activeProcesses) _activeProcesses.Remove(p);
                try
                {
                    if (p.ExitCode != 0)
                    {
                        var duration = p.ExitTime - p.StartTime;
                        if (duration.TotalSeconds < 10)
                        {
                            OnPlayerError?.Invoke($"El reproductor falló (Código: {p.ExitCode}). El video podría no estar disponible.");
                        }
                    }
                }
                catch { }
            };
            lock (_activeProcesses) _activeProcesses.Add(p);
            p.Start();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al iniciar el reproductor: {ex.Message}", ex);
        }
    }

    public static async System.Threading.Tasks.Task<DownloadResult> DownloadAsync(string videoUrl, AniCS.Models.AnimeResult anime, AniCS.Models.Episode episode, string downloadDir, string? referer = null, string quality = "Mejor", Action<double, string>? onProgress = null, System.Threading.CancellationToken cancellationToken = default)
    {
        videoUrl = await ResolveRedirectorUrlAsync(videoUrl);
        if (!string.IsNullOrEmpty(referer))
        {
            referer = await ResolveRedirectorUrlAsync(referer);
        }

        string animeDir = "";
        string episodeNumStr = "";
        Process? p = null;
        try
        {
            var rawTitle = string.IsNullOrWhiteSpace(anime.Title) ? "Anime_Desconocido" : anime.Title;
            var safeTitle = string.Join("_", rawTitle.Split(Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";

            animeDir = Path.Combine(downloadDir, safeTitle);

            
            if (!Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }

            if (!Directory.Exists(animeDir))
            {
                Directory.CreateDirectory(animeDir);
            }

            episodeNumStr = string.IsNullOrWhiteSpace(episode.EpisodeNumber) ? "Desconocido" : episode.EpisodeNumber;
            var filenamePattern = $"Episodio {episodeNumStr}.%(ext)s";
            var outputPath = Path.Combine(animeDir, filenamePattern);

            string headerArgs = $"--add-header \"User-Agent:{AniCS.ConfigManager.Current.RandomUserAgent}\" ";
            if (!string.IsNullOrEmpty(referer))
            {
                headerArgs += $"--add-header \"Referer:{referer}\" ";
                try
                {
                    var uri = new Uri(referer);
                    headerArgs += $"--add-header \"Origin:{uri.GetLeftPart(UriPartial.Authority)}\" ";
                }
                catch { }
            }

            var qualityArg = "";
            if (quality != "Mejor")
            {
                string height = quality.Replace("p", "");
                qualityArg = $"-S \"res:{height}\" ";
            }

            p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "yt-dlp",
                    Arguments = $"--newline --concurrent-fragments 1 --hls-prefer-native {headerArgs}{qualityArg}-o \"{outputPath}\" \"{videoUrl}\"",
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

            while (true)
            {
                var line = await p.StandardOutput.ReadLineAsync();
                if (line == null) break;
                if (onProgress != null)
                {
                    var (progress, sizeInfo) = ParseYtDlpProgress(line, animeDir, episodeNumStr);
                    if (progress >= 0)
                    {
                        onProgress(progress, sizeInfo);
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

    /// <summary>
    /// Convierte una lista de argumentos a una cadena escapada para ProcessStartInfo.Arguments.
    /// Necesario porque con UseShellExecute = true no se puede usar ArgumentList.
    /// </summary>
    private static string BuildArgumentString(List<string> args)
    {
        var parts = new List<string>();
        foreach (var arg in args)
        {
            // Si el argumento contiene espacios o comillas, debe ir entre comillas dobles
            // con las comillas internas escapadas con backslash.
            if (arg.Contains(' ') || arg.Contains('"') || arg.Contains('\t'))
            {
                // Escaping estándar de Windows: las backslashes antes de una comilla se duplican
                string escaped = arg.Replace("\\", "\\\\").Replace("\"", "\\\"");
                parts.Add($"\"{escaped}\"");
            }
            else
            {
                parts.Add(arg);
            }
        }
        return string.Join(" ", parts);
    }

    private static string? GetExecutablePath(string command)
    {
        string extension = OperatingSystem.IsWindows() ? ".exe" : "";
        string fileName = command + extension;

        // 1. Prioridad: buscar en el PATH del sistema (versión más actualizada del usuario)
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths != null)
        {
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path.Trim(), fileName);
                if (File.Exists(fullPath)) return fullPath;
            }
        }

        // 2. Fallback: mpv en la carpeta de la aplicación (interno / antiguo)
        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (File.Exists(localPath)) return localPath;

        string currentDirPath = Path.Combine(Environment.CurrentDirectory, fileName);
        if (File.Exists(currentDirPath)) return currentDirPath;

        return null;
    }

    private static DateTime _lastDiskCheckTime = DateTime.MinValue;
    private static long _cachedDiskBytes = 0;

    private static long GetDiskBytesForEpisode(string animeDir, string episodeNumStr)
    {
        try
        {
            if (string.IsNullOrEmpty(animeDir) || !Directory.Exists(animeDir)) return 0;

            // Throttle metadata check to 500 ms (0.5s interval, like btop/htop monitors)
            if ((DateTime.UtcNow - _lastDiskCheckTime).TotalMilliseconds < 500 && _cachedDiskBytes > 0)
            {
                return _cachedDiskBytes;
            }


            _lastDiskCheckTime = DateTime.UtcNow;
            var files = Directory.GetFiles(animeDir, $"Episodio {episodeNumStr}.*");
            long totalBytes = 0;
            foreach (var f in files)
            {
                var info = new FileInfo(f);
                totalBytes += info.Length;
            }
            _cachedDiskBytes = totalBytes;
            return totalBytes;
        }
        catch
        {
            return 0;
        }
    }


    public static (double progress, string sizeInfo) ParseYtDlpProgress(string line, string? animeDir = null, string? episodeNumStr = null)
    {
        if (string.IsNullOrWhiteSpace(line)) return (-1, string.Empty);

        string speedStr = string.Empty;
        var speedMatch = System.Text.RegularExpressions.Regex.Match(line, @"at\s+(\d+(?:\.\d+)?\s*[KMGTP]?i?B/s)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (speedMatch.Success)
        {
            speedStr = $" - {speedMatch.Groups[1].Value}";
        }

        double pct = -1;
        var pctMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[download\]\s+(\d+(?:\.\d+)?)\%");
        if (pctMatch.Success && double.TryParse(pctMatch.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedPct))
        {
            pct = parsedPct;
        }

        // High-precision physical disk calculation if animeDir and episodeNumStr are available
        if (pct > 0 && !string.IsNullOrEmpty(animeDir) && !string.IsNullOrEmpty(episodeNumStr))
        {
            long diskBytes = GetDiskBytesForEpisode(animeDir, episodeNumStr);
            if (diskBytes > 0)
            {
                double dlMB = diskBytes / (1024.0 * 1024.0);
                double estTotalMB = dlMB / (pct / 100.0);
                bool isFinal = pct >= 99.9;
                string tilde = isFinal ? "" : "~";
                return (pct, $"{dlMB:F1} MB / {tilde}{estTotalMB:F1} MB{speedStr}");
            }
        }

        // Fallback parsing directly from yt-dlp output line
        // 1. Explicit downloaded of total size: e.g. "[download] 45.10MiB of ~300.00MiB"
        var explicitMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[download\]\s+(~?\s*\d+(?:\.\d+)?\s*[KMGTP]?i?B)\s+of\s+(~?\s*\d+(?:\.\d+)?)\s*([KMGTP]?i?B)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (explicitMatch.Success)
        {
            var dlRaw = explicitMatch.Groups[1].Value.Replace(" ", "");
            var totalRaw = explicitMatch.Groups[2].Value.Replace(" ", "");
            var unit = explicitMatch.Groups[3].Value;

            double.TryParse(dlRaw.Replace("~", "").Replace(unit, ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double dl);
            double.TryParse(totalRaw.Replace("~", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double total);

            if (total > 0)
            {
                if (pct < 0) pct = Math.Min(100.0, (dl / total) * 100.0);
                bool isEstimate = line.Contains("of ~") || line.Contains("of  ~");
                string tilde = isEstimate ? "~" : "";
                return (pct, $"{dl:F1} {unit} / {tilde}{total:F1} {unit}{speedStr}");
            }
        }

        // 2. Percentage of total size: e.g. "[download] 12.5% of ~320.50MiB"
        var percentSizeMatch = System.Text.RegularExpressions.Regex.Match(line, @"\[download\]\s+(\d+(?:\.\d+)?)\%\s+of\s+(~?\s*\d+(?:\.\d+)?)\s*([KMGTP]?i?B)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (percentSizeMatch.Success && pct > 0)
        {
            var totalRaw = percentSizeMatch.Groups[2].Value.Replace(" ", "");
            var unit = percentSizeMatch.Groups[3].Value;
            double.TryParse(totalRaw.Replace("~", ""), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double total);

            if (total > 0)
            {
                double downloaded = (pct / 100.0) * total;
                bool isEstimate = line.Contains("of ~") || line.Contains("of  ~");
                string tilde = isEstimate ? "~" : "";
                return (pct, $"{downloaded:F1} {unit} / {tilde}{total:F1} {unit}{speedStr}");
            }
        }

        if (pct >= 0)
        {
            return (pct, speedStr.TrimStart(' ', '-'));
        }

        return (-1, string.Empty);
    }
}



