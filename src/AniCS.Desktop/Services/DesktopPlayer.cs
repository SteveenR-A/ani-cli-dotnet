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

    public static void Play(string url, string title, string? referer, string quality = "Mejor")
    {
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
            "--cache-pause-wait=1"
        };

        if (quality != "Mejor" && !url.Contains(".m3u8") && !url.Contains(".mp4"))
        {
            // mpv soporta ytdl-format solo para sitios soportados, si es directo m3u8 lo ignoramos para que no lance error
            string height = quality.Replace("p", "");
            args.Add($"--ytdl-format=bestvideo[height<=?{height}]+bestaudio/best[height<=?{height}]");
        }

        bool isJkAnime = referer != null && referer.Contains("jkanime.net", StringComparison.OrdinalIgnoreCase);
        bool isMediafire = referer != null && referer.Contains("mediafire.com", StringComparison.OrdinalIgnoreCase);

        if (isJkAnime || isMediafire)
        {
            args.Add("--demuxer-max-bytes=150M");
            args.Add("--demuxer-max-back-bytes=50M");
            args.Add("--demuxer-readahead-secs=120");
            args.Add("--cache-secs=120");
            args.Add("--hr-seek=yes");
            args.Add("--network-timeout=15");
            args.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_on_http_error=4xx,reconnect_delay_max=10");

            // mpv soporta --http-header-fields separado por comas para compatibilidad máxima con versiones antiguas
            string origin = isJkAnime ? "https://jkanime.net" : "https://www.mediafire.com";
            var headerList = new System.Collections.Generic.List<string>
            {
                "Accept-Language: es-419",
                "Accept: */*",
                "Sec-Fetch-Dest: video",
                "Sec-Fetch-Mode: no-cors",
                "Sec-Fetch-Site: cross-site",
                $"User-Agent: {AniCS.ConfigManager.Current.RandomUserAgent}"
            };

            if (!string.IsNullOrEmpty(referer))
            {
                headerList.Add($"Referer: {referer}");
                headerList.Add($"Origin: {origin}");
            }
            
            args.Add($"--http-header-fields={string.Join(",", headerList)}");
        }
        else
        {
            args.Add("--demuxer-max-bytes=150M");
            args.Add("--demuxer-max-back-bytes=50M");
            args.Add("--demuxer-readahead-secs=120");
            
            if (exe.Contains("mpv", StringComparison.OrdinalIgnoreCase))
            {
                args.Add("--force-window=immediate");
                args.Add("--keep-open=yes");
                args.Add("--geometry=65%"); // Hace la ventana mucho más grande por defecto
                args.Add("--autofit=1280x720"); // Asegura un mínimo buen tamaño

                if (!string.IsNullOrEmpty(referer))
                {
                    try 
                    {
                        var uri = new Uri(referer);
                        string originUrl = uri.GetLeftPart(UriPartial.Authority);
                        string ua = AniCS.ConfigManager.Current.RandomUserAgent;
                        args.Add($"--http-header-fields=Referer: {referer},Origin: {originUrl},User-Agent: {ua}");
                    }
                    catch 
                    {
                        string ua = AniCS.ConfigManager.Current.RandomUserAgent;
                        args.Add($"--http-header-fields=Referer: {referer},User-Agent: {ua}");
                    }
                }
            }
        }

        args.Add($"--title={title}");
        args.Add(url);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                // UseShellExecute = false: permite usar ArgumentList (escaping 100% seguro por .NET).
                // CreateNoWindow = false: NO suprime la ventana de video de mpv.
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

    public static async System.Threading.Tasks.Task<DownloadResult> DownloadAsync(string videoUrl, AniCS.Models.AnimeResult anime, AniCS.Models.Episode episode, string downloadDir, string? referer = null, string quality = "Mejor", Action<double>? onProgress = null, System.Threading.CancellationToken cancellationToken = default)
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
}
