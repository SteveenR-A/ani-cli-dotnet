using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AniCS.Services;

public class WindowsPlayerService : IPlayerService
{
    private static readonly List<Process> _activeProcesses = new();
    private string? _cachedYtDlpPath;

    public WindowsPlayerService()
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => KillAll();
        Console.CancelKeyPress += (s, e) => KillAll();
    }

    private void KillAll()
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

    private string? GetExecutablePath(string exeName)
    {
        string exe = OperatingSystem.IsWindows() && !exeName.EndsWith(".exe") ? $"{exeName}.exe" : exeName;

        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths != null)
        {
            foreach (var dir in paths)
            {
                var full = Path.Combine(dir.Trim(), exe);
                if (File.Exists(full))
                {
                    return full;
                }
            }
        }

        var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exe);
        if (File.Exists(localPath))
        {
            return localPath;
        }

        return null;
    }

    public bool IsYtDlpAvailable()
    {
        if (_cachedYtDlpPath != null) return true;
        _cachedYtDlpPath = GetExecutablePath("yt-dlp");
        return _cachedYtDlpPath != null;
    }

    public async Task<string> ResolveVideoUrlWithYtDlpAsync(string pageUrl, string? referer = null)
    {
        var ytdlp = _cachedYtDlpPath ?? GetExecutablePath("yt-dlp");
        if (ytdlp == null) return string.Empty;

        try
        {
            var si = new ProcessStartInfo
            {
                FileName = ytdlp,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            si.ArgumentList.Add("-g");
            si.ArgumentList.Add("--no-warnings");
            si.ArgumentList.Add("--no-playlist");

            if (!string.IsNullOrEmpty(referer))
            {
                si.ArgumentList.Add("--add-header");
                si.ArgumentList.Add($"Referer:{referer}");
            }

            si.ArgumentList.Add(pageUrl);

            using var p = new Process { StartInfo = si };
            p.Start();

            var output = await p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync(); // drain stderr

            await p.WaitForExitAsync();

            if (p.ExitCode == 0)
            {
                var url = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.StartsWith("http"));
                return url ?? string.Empty;
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task PlayAsync(string url, string title, string? referer)
    {
        var exe = GetExecutablePath("mpv") ?? GetExecutablePath("mpvnet");

        if (exe == null)
        {
            throw new Exception("mpv no está instalado. Por favor, descarga mpv y agrégalo al PATH o a la carpeta del programa.");
        }

        var args = new List<string>
        {
            "--force-window=yes",
            "--cache=yes",
            "--cache-pause-wait=1"
        };

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

            string origin = isJkAnime ? "https://jkanime.net" : "https://www.mediafire.com";
            if (!string.IsNullOrEmpty(referer))
            {
                args.Add($"--http-header-fields=Referer: {referer}");
                args.Add($"--http-header-fields=Origin: {origin}");
            }
            args.Add("--http-header-fields=Accept-Language: es-419");
            args.Add("--http-header-fields=Accept: */*");
            args.Add("--http-header-fields=Sec-Fetch-Dest: video");
            args.Add("--http-header-fields=Sec-Fetch-Mode: no-cors");
            args.Add("--http-header-fields=Sec-Fetch-Site: cross-site");
        }
        else
        {
            args.Add("--demuxer-max-bytes=150M");
            args.Add("--demuxer-max-back-bytes=50M");
            args.Add("--demuxer-readahead-secs=120");
            if (!string.IsNullOrEmpty(referer))
            {
                args.Add($"--http-header-fields=Referer: {referer}");
            }
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
            };
            lock (_activeProcesses) _activeProcesses.Add(p);
            p.Start();
            await p.WaitForExitAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error al iniciar el reproductor: {ex.Message}", ex);
        }
    }

    public void Download(string videoUrl, string animeTitle, string episodeNum, string targetDir, string? referer)
    {
        var ytdlp = GetExecutablePath("yt-dlp");
        if (ytdlp == null)
        {
            Console.WriteLine("yt-dlp no está instalado. No se puede descargar.");
            return;
        }

        try
        {
            string safeTitle = string.Join("_", animeTitle.Split(Path.GetInvalidFileNameChars()));
            string animeDir = Path.Combine(targetDir, safeTitle);
            Directory.CreateDirectory(animeDir);

            string episodeNumStr = string.IsNullOrWhiteSpace(episodeNum) ? "Desconocido" : episodeNum;
            string filenamePattern = $"Episodio {episodeNumStr}.%(ext)s";
            string outputPath = Path.Combine(animeDir, filenamePattern);

            var refererArg = string.IsNullOrEmpty(referer) ? "" : $"--add-header \"Referer:{referer}\"";

            var si = new ProcessStartInfo
            {
                FileName = ytdlp,
                Arguments = $"--newline --concurrent-fragments 1 --hls-prefer-native {refererArg} -o \"{outputPath}\" \"{videoUrl}\"",
                UseShellExecute = false,
            };

            using var p = new Process { StartInfo = si };
            p.Start();
            p.WaitForExit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descargar: {ex.Message}");
        }
    }
}

