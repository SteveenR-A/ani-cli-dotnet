using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AniCS;
using AniCS.Resolver.Native;

namespace AniCS.Resolver;

/// <summary>
/// Backend de resolución y descarga nativo — no requiere yt-dlp ni herramientas externas.
///
/// Capacidades:
///   - Resuelve URLs directas (HLS .m3u8 y MP4) sin procesos externos.
///   - Maneja redirector.php mediante HttpClient.
///   - Descarga HLS concatenando segmentos .ts natiamente.
///   - Descarga MP4 directos con HttpClient + progreso.
///
/// Limitaciones conocidas (se resuelven en Fase 3):
///   - El output de HLS es .ts (no .mp4). El remux a MP4 requiere LibVLC (Fase 3).
///   - No resuelve páginas con JavaScript (Mega, etc.) — usar yt-dlp para esos.
/// </summary>
public sealed class NativeResolverBackend : IResolverBackend, IDisposable
{
    private readonly HttpClient _client;
    private readonly HttpClientHandler _handler;

    public string BackendName => "Native";

    /// <summary>El backend nativo siempre está disponible (solo usa HttpClient de .NET).</summary>
    public bool IsAvailable => true;

    public NativeResolverBackend()
    {
        _handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
        };
        _client = new HttpClient(_handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        ApplyDefaultHeaders();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IResolverBackend — Resolve
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ResolvedMedia> ResolveAsync(
        string url,
        ResolveOptions? opts = null,
        CancellationToken ct = default)
    {
        var referer = opts?.Referer;

        // 1. Resolver redirector.php si aplica
        url = await ResolveRedirectorAsync(url, referer, ct);

        // 2. Detectar si ya es una URL directa
        var type = DetectMediaType(url);
        if (type != MediaType.Unknown)
            return new ResolvedMedia(url, url, type, referer, ConfigManager.Current.RandomUserAgent);

        // 3. Intentar extraer la URL del stream desde el HTML de la página
        var extracted = await ExtractFromPageAsync(url, referer, ct);
        if (!string.IsNullOrEmpty(extracted))
        {
            var extractedType = DetectMediaType(extracted);
            return new ResolvedMedia(url, extracted, extractedType, referer, ConfigManager.Current.RandomUserAgent);
        }

        // 4. No pudimos resolver nativamente
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
        try
        {
            // Actualizar headers con el UA/Referer del media
            UpdateClientHeaders(media);

            return media.Type switch
            {
                MediaType.Hls     => await DownloadHlsAsync(media, outputPath, progress, ct),
                MediaType.Mp4     => await DownloadDirectAsync(media, outputPath, progress, ct),
                MediaType.Audio   => await DownloadDirectAsync(media, outputPath, progress, ct),
                _                 => new DownloadResult(DownloadResultCode.Error,
                                        ErrorMessage: $"Tipo de media no soportado nativamente: {media.Type}. Usa el backend yt-dlp para este servidor."),
            };
        }
        catch (OperationCanceledException)
        {
            return new DownloadResult(DownloadResultCode.Cancelled);
        }
        catch (Exception ex)
        {
            return new DownloadResult(DownloadResultCode.Error, ErrorMessage: ex.Message);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Descarga HLS
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<DownloadResult> DownloadHlsAsync(
        ResolvedMedia media,
        string outputPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        // Parsear el manifiesto — selecciona la variante de calidad correcta
        int preferredHeight = ParsePreferredHeight(ConfigManager.Current.PreferredQuality);
        var parseResult = await HlsParser.ParseAsync(media.DirectUrl, preferredHeight, _client, ct);

        if (parseResult == null)
            return new DownloadResult(DownloadResultCode.Error, ErrorMessage: "No se pudo parsear el manifiesto HLS.");

        if (parseResult.IsEncrypted)
        {
            // AES-128 cifrado — por ahora solo advertir (soporte completo en Fase 3)
            return new DownloadResult(DownloadResultCode.Error,
                ErrorMessage: "El stream HLS está cifrado (AES-128). Usa el backend yt-dlp para descargarlo.");
        }

        if (parseResult.SegmentUrls.Count == 0)
            return new DownloadResult(DownloadResultCode.Error, ErrorMessage: "El manifiesto HLS no contiene segmentos.");

        var downloader = new HlsDownloader(_client);
        var tsPath = await downloader.DownloadAsync(parseResult, outputPath, progress, ct);

        bool ok = File.Exists(tsPath) && new FileInfo(tsPath).Length > 0;
        return ok
            ? new DownloadResult(DownloadResultCode.Success, tsPath)
            : new DownloadResult(DownloadResultCode.Error, ErrorMessage: "El archivo de salida está vacío.");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Descarga directa (MP4 / audio)
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<DownloadResult> DownloadDirectAsync(
        ResolvedMedia media,
        string outputPath,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        // Preservar la extensión original para MP4/MKV directos
        string ext = Path.GetExtension(new Uri(media.DirectUrl).AbsolutePath);
        if (string.IsNullOrEmpty(ext)) ext = ".mp4";

        var finalPath = Path.ChangeExtension(outputPath, ext);

        var dir = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using var response = await _client.GetAsync(media.DirectUrl,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? totalBytes = response.Content.Headers.ContentLength;

        using var inputStream  = await response.Content.ReadAsStreamAsync(ct);
        using var outputStream = new FileStream(finalPath, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize: 65536, useAsync: true);

        var buffer = new byte[65536];
        long downloadedBytes = 0;
        int bytesRead;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        while ((bytesRead = await inputStream.ReadAsync(buffer, ct)) > 0)
        {
            await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            downloadedBytes += bytesRead;

            if (progress != null && totalBytes.HasValue && totalBytes.Value > 0)
            {
                double pct = (double)downloadedBytes / totalBytes.Value * 100.0;
                double dlMb = downloadedBytes / (1024.0 * 1024.0);
                double totalMb = totalBytes.Value / (1024.0 * 1024.0);
                
                string speedStr = "";
                double seconds = stopwatch.Elapsed.TotalSeconds;
                if (seconds > 0)
                {
                    double bytesPerSec = downloadedBytes / seconds;
                    if (bytesPerSec >= 1024 * 1024) speedStr = $"{bytesPerSec / (1024.0 * 1024):F2} MB/s";
                    else if (bytesPerSec >= 1024) speedStr = $"{bytesPerSec / 1024.0:F2} KB/s";
                    else speedStr = $"{bytesPerSec:F0} B/s";
                }
                
                progress.Report(new DownloadProgress(pct, $"{dlMb:F1} MB / {totalMb:F1} MB", speedStr, pct >= 100));
            }
        }

        progress?.Report(new DownloadProgress(100, "", "", IsFinished: true));
        return new DownloadResult(DownloadResultCode.Success, finalPath);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Resolución de URL de página (extracción de HTML)
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Intenta extraer la URL directa del stream desde el HTML de la página.
    /// Cubre los patrones más comunes de iframes y objetos de video.
    /// Para JKAnime, el extractor de AniCS.Core ya devuelve la URL .m3u8 directa,
    /// así que este método sirve como safety net para otros servidores.
    /// </summary>
    private async Task<string?> ExtractFromPageAsync(string pageUrl, string? referer, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, pageUrl);
            req.Headers.UserAgent.ParseAdd(ConfigManager.Current.RandomUserAgent);
            if (!string.IsNullOrEmpty(referer))
                req.Headers.Referrer = new Uri(referer);

            using var resp = await _client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;

            var html = await resp.Content.ReadAsStringAsync(ct);

            // 1. Buscar .m3u8 directamente en el HTML
            var m3u8 = Regex.Match(html,
                @"https?://[^\s""'<>\\]+\.m3u8[^\s""'<>\\]*",
                RegexOptions.IgnoreCase);
            if (m3u8.Success) return m3u8.Value.Replace("\\", "");

            // 2. Buscar .mp4 directamente
            var mp4 = Regex.Match(html,
                @"https?://[^\s""'<>\\]+\.mp4[^\s""'<>\\]*",
                RegexOptions.IgnoreCase);
            if (mp4.Success) return mp4.Value.Replace("\\", "");

            // 3. Patrón file: "url" o src: "url" (JWPlayer, Video.js, etc.)
            var fileField = Regex.Match(html,
                @"(?:file|src)\s*:\s*[""'](https?://[^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (fileField.Success) return fileField.Groups[1].Value.Replace("\\", "");

            // 4. Atributo src de un <source> o <video>
            var sourceTag = Regex.Match(html,
                @"<source[^>]+src=[""'](https?://[^""'>]+\.(?:mp4|m3u8|webm|mkv)[^""']*)[""']",
                RegexOptions.IgnoreCase);
            if (sourceTag.Success) return sourceTag.Groups[1].Value;
        }
        catch { }

        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // redirector.php
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<string> ResolveRedirectorAsync(string url, string? referer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(url) || !url.Contains("redirector.php"))
            return url;

        url = url.Replace("\\", "");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd(ConfigManager.Current.RandomUserAgent);
            req.Headers.Referrer = new Uri("https://www.mundodonghua.com/");
            req.Headers.Add("Origin", "https://www.mundodonghua.com");

            using var handler = new HttpClientHandler { AllowAutoRedirect = true };
            using var client  = new HttpClient(handler);
            using var resp    = await client.SendAsync(req, ct);

            if (resp.RequestMessage?.RequestUri != null &&
                !resp.RequestMessage.RequestUri.ToString().Contains("redirector.php"))
                return resp.RequestMessage.RequestUri.ToString();

            var html = await resp.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrEmpty(html))
            {
                var m = Regex.Match(html,
                    @"https?://[^\s""'<>\\]+\.(?:m3u8|mp4)[^\s""'<>\\]*");
                if (m.Success) return m.Value.Replace("\\", "");

                var fm = Regex.Match(html,
                    @"(?:file|src):\s*[""'](https?://[^""']+)[""']");
                if (fm.Success) return fm.Groups[1].Value.Replace("\\", "");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { }

        return url;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static MediaType DetectMediaType(string url)
    {
        // Ignorar query strings al detectar la extensión
        var path = url.Split('?')[0].Split('#')[0];
        if (path.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)) return MediaType.Hls;
        if (path.Contains(".mp4",  StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".mkv",  StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".avi",  StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".webm", StringComparison.OrdinalIgnoreCase)) return MediaType.Mp4;
        if (path.Contains(".mp3",  StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".aac",  StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".ogg",  StringComparison.OrdinalIgnoreCase)) return MediaType.Audio;
        return MediaType.Unknown;
    }

    /// <summary>Convierte la calidad preferida ("720p", "1080p", "Mejor") a altura en pixels.</summary>
    private static int ParsePreferredHeight(string quality)
    {
        if (string.IsNullOrEmpty(quality) || quality.Equals("Mejor", StringComparison.OrdinalIgnoreCase))
            return 0; // 0 = mejor disponible
        var digits = Regex.Match(quality, @"\d+");
        return digits.Success && int.TryParse(digits.Value, out int h) ? h : 0;
    }

    private void ApplyDefaultHeaders()
    {
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(ConfigManager.Current.RandomUserAgent);
        _client.DefaultRequestHeaders.Add("Accept-Language", "es-419,es;q=0.9,en;q=0.8");
        _client.DefaultRequestHeaders.Add("Accept", "*/*");
    }

    private void UpdateClientHeaders(ResolvedMedia media)
    {
        _client.DefaultRequestHeaders.UserAgent.Clear();
        var ua = media.UserAgent ?? ConfigManager.Current.RandomUserAgent;
        _client.DefaultRequestHeaders.UserAgent.ParseAdd(ua);

        _client.DefaultRequestHeaders.Referrer = null;
        if (!string.IsNullOrEmpty(media.Referer))
        {
            try { _client.DefaultRequestHeaders.Referrer = new Uri(media.Referer); }
            catch { }
        }
    }

    public void Dispose()
    {
        _client.Dispose();
        _handler.Dispose();
    }
}
