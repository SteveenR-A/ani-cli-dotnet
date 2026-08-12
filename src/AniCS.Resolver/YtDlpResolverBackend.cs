using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AniCS;

namespace AniCS.Resolver;

/// <summary>
/// Backend de resolución y descarga que delega a yt-dlp.exe externo.
/// Comportamiento idéntico al YtDlpService y DesktopPlayer.DownloadAsync originales.
/// </summary>
public sealed class YtDlpResolverBackend : IResolverBackend
{
    private string? _cachedPath;

    public string BackendName => "yt-dlp";

    public bool IsAvailable => GetYtDlpPath() != null;

    // ──────────────────────────────────────────────────────────────────────────
    // IResolverBackend — Resolve
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ResolvedMedia> ResolveAsync(string url, ResolveOptions? opts = null, CancellationToken ct = default)
    {
        var referer = opts?.Referer;

        // Manejar redirector.php antes de pasar a yt-dlp
        url = await ResolveRedirectorAsync(url);

        var ytdlp = GetYtDlpPath();

        // Si la URL ya es directa, no necesitamos yt-dlp
        var type = DetectMediaType(url);
        if (type != MediaType.Unknown)
            return new ResolvedMedia(url, url, type, referer);

        // Sin yt-dlp no podemos resolver URLs complejas
        if (ytdlp == null)
            return new ResolvedMedia(url, url, MediaType.Unknown, referer);

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

            si.ArgumentList.Add(url);

            using var p = new Process { StartInfo = si };
            p.Start();

            var outputTask = p.StandardOutput.ReadToEndAsync(ct);
            var errorTask = p.StandardError.ReadToEndAsync(ct);
            
            await Task.WhenAll(outputTask, errorTask);
            var output = outputTask.Result;
            
            await p.WaitForExitAsync(ct);

            if (p.ExitCode == 0)
            {
                var directUrl = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.StartsWith("http"));

                if (!string.IsNullOrEmpty(directUrl))
                    return new ResolvedMedia(url, directUrl, DetectMediaType(directUrl), referer);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        return new ResolvedMedia(url, url, MediaType.Unknown, referer);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IResolverBackend — Download
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<DownloadResult> DownloadAsync(
        ResolvedMedia media,
        string outputPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var ytdlp = GetYtDlpPath();
        if (ytdlp == null)
            return new DownloadResult(DownloadResultCode.Error, ErrorMessage: "yt-dlp no está instalado.");

        // Asegurar que el directorio de destino existe
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // yt-dlp necesita patrón de nombre (sin extensión fija para que elija)
        // Convertimos la ruta final .mp4 a un patrón %(ext)s
        var baseName = Path.GetFileNameWithoutExtension(outputPath);
        var outputDir = dir ?? ".";
        var pattern = Path.Combine(outputDir, $"{baseName}.%(ext)s");

        var ua = media.UserAgent ?? ConfigManager.Current.RandomUserAgent;

        Process? p = null;
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

            si.ArgumentList.Add("--newline");
            si.ArgumentList.Add("--concurrent-fragments"); si.ArgumentList.Add("1");
            si.ArgumentList.Add("--hls-prefer-native");
            si.ArgumentList.Add("--add-header"); si.ArgumentList.Add($"User-Agent:{ua}");

            if (!string.IsNullOrEmpty(media.Referer))
            {
                si.ArgumentList.Add("--add-header"); si.ArgumentList.Add($"Referer:{media.Referer}");
                try
                {
                    var uri = new Uri(media.Referer);
                    si.ArgumentList.Add("--add-header"); si.ArgumentList.Add($"Origin:{uri.GetLeftPart(UriPartial.Authority)}");
                }
                catch { }
            }

            si.ArgumentList.Add("-o"); si.ArgumentList.Add(pattern);
            si.ArgumentList.Add(media.DirectUrl);

            p = new Process { StartInfo = si, EnableRaisingEvents = true };
            p.Start();
            _ = p.StandardError.ReadToEndAsync(ct); // Drain stderr

            using var reg = ct.Register(() =>
            {
                try { if (!p.HasExited) p.Kill(true); } catch { }
            });

            while (true)
            {
                var line = await p.StandardOutput.ReadLineAsync(ct);
                if (line == null) break;

                if (progress != null)
                {
                    var (pct, sizeInfo, speed) = ParseYtDlpLine(line);
                    if (pct >= 0)
                        progress.Report(new DownloadProgress(pct, sizeInfo, speed, pct >= 100));
                }
            }

            await p.WaitForExitAsync(ct);

            // Buscar el archivo generado (yt-dlp puede elegir extensión)
            var generated = Directory.GetFiles(outputDir, $"{baseName}.*").FirstOrDefault();
            bool ok = generated != null && new FileInfo(generated).Length > 0;

            if ((p.ExitCode == 0 || ok) && !ct.IsCancellationRequested)
            {
                progress?.Report(new DownloadProgress(100, "", "", IsFinished: true));
                return new DownloadResult(DownloadResultCode.Success, generated);
            }

            return ct.IsCancellationRequested
                ? new DownloadResult(DownloadResultCode.Cancelled)
                : new DownloadResult(DownloadResultCode.Error, ErrorMessage: $"yt-dlp salió con código {p.ExitCode}");
        }
        catch (OperationCanceledException)
        {
            try { p?.Kill(true); p?.WaitForExit(2000); } catch { }
            return new DownloadResult(DownloadResultCode.Cancelled);
        }
        catch (Exception ex)
        {
            return new DownloadResult(DownloadResultCode.Error, ErrorMessage: ex.Message);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private string? GetYtDlpPath()
    {
        if (_cachedPath != null) return _cachedPath;
        _cachedPath = FindExe("yt-dlp");
        return _cachedPath;
    }

    private static string? FindExe(string name)
    {
        string fileName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths != null)
        {
            foreach (var dir in paths)
            {
                var full = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(full)) return full;
            }
        }
        var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (File.Exists(local)) return local;
        return null;
    }

    private static MediaType DetectMediaType(string url)
    {
        if (url.Contains(".m3u8")) return MediaType.Hls;
        if (url.Contains(".mp4") || url.Contains(".mkv") || url.Contains(".avi") || url.Contains(".webm"))
            return MediaType.Mp4;
        return MediaType.Unknown;
    }

    /// <summary>Resuelve redirector.php igual que DesktopPlayer.ResolveRedirectorUrlAsync.</summary>
    private static async Task<string> ResolveRedirectorAsync(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.Contains("redirector.php"))
            return url;

        url = url.Replace("\\", "");

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd(ConfigManager.Current.RandomUserAgent);
            request.Headers.Referrer = new Uri("https://www.mundodonghua.com/");
            request.Headers.Add("Origin", "https://www.mundodonghua.com");

            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client = new HttpClient(handler);
            using var response = await client.SendAsync(request);

            if (response.RequestMessage?.RequestUri != null &&
                !response.RequestMessage.RequestUri.ToString().Contains("redirector.php"))
                return response.RequestMessage.RequestUri.ToString();

            var html = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(html))
            {
                var m = Regex.Match(html, @"https?://[^\s""'<>\\]+\.(?:m3u8|mp4)[^\s""'<>\\]*");
                if (m.Success) return m.Value.Replace("\\", "");

                var fm = Regex.Match(html, @"(?:file|src):\s*[""'](https?://[^""']+)[""']");
                if (fm.Success) return fm.Groups[1].Value.Replace("\\", "");
            }
        }
        catch { }

        return url;
    }

    /// <summary>Parsea una línea de salida de yt-dlp y extrae porcentaje, tamaño y velocidad.</summary>
    private static (double pct, string sizeInfo, string speed) ParseYtDlpLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return (-1, "", "");

        string speed = "";
        var speedMatch = Regex.Match(line, @"at\s+(\d+(?:\.\d+)?\s*[KMGTP]?i?B/s)", RegexOptions.IgnoreCase);
        if (speedMatch.Success) speed = speedMatch.Groups[1].Value;

        double pct = -1;
        var pctMatch = Regex.Match(line, @"\[download\]\s+(\d+(?:\.\d+)?)\%");
        if (pctMatch.Success && double.TryParse(pctMatch.Groups[1].Value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double p))
            pct = p;

        // Tamaño: "45.1MiB of ~320.5MiB"
        var sizeMatch = Regex.Match(line,
            @"\[download\]\s+(\d+(?:\.\d+)?)\%\s+of\s+(~?\s*\d+(?:\.\d+)?)\s*([KMGTP]?i?B)",
            RegexOptions.IgnoreCase);
        if (sizeMatch.Success && pct > 0)
        {
            var totalRaw = sizeMatch.Groups[2].Value.Replace(" ", "");
            var unit = sizeMatch.Groups[3].Value;
            if (double.TryParse(totalRaw.Replace("~", ""),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double total) && total > 0)
            {
                double dl = (pct / 100.0) * total;
                bool isEst = line.Contains("of ~");
                return (pct, $"{dl:F1} {unit} / {(isEst ? "~" : "")}{total:F1} {unit}", speed);
            }
        }

        return (pct, "", speed);
    }
}
