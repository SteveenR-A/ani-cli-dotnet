using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AniCS.Resolver.Native;

/// <summary>
/// Descarga un stream HLS (segmentos .ts) y los concatena en un archivo de salida.
///
/// El resultado es un archivo Transport Stream (.ts) que todos los reproductores
/// modernos (VLC, mpv, Potplayer, etc.) pueden abrir directamente.
/// En Fase 3, cuando LibVLC esté disponible, se añadirá el remux a MP4.
/// </summary>
public sealed class HlsDownloader
{
    private readonly HttpClient _client;

    public HlsDownloader(HttpClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Descarga todos los segmentos de <paramref name="parseResult"/> y los escribe
    /// secuencialmente en <paramref name="outputPath"/> (extensión .ts o la que se indique).
    /// </summary>
    /// <param name="parseResult">Resultado del HlsParser con la lista de segmentos.</param>
    /// <param name="outputPath">Ruta del archivo de salida (sin importar la extensión).</param>
    /// <param name="progress">Progreso de 0 a 100.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Ruta del archivo generado.</returns>
    public async Task<string> DownloadAsync(
        HlsParser.ParseResult parseResult,
        string outputPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Contenedor nativo de HLS (MPEG-2 Transport Stream)
        var targetPath = Path.ChangeExtension(outputPath, ".ts");

        var segments = parseResult.SegmentUrls;
        int total = segments.Count;
        if (total == 0)
            throw new InvalidOperationException("El manifiesto HLS no contiene segmentos.");

        // Asegurar que el directorio existe
        var dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        long downloadedBytes = 0;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var idxPath = targetPath + ".idx";
        int startSegment = 0;

        if (File.Exists(targetPath) && File.Exists(idxPath))
        {
            try
            {
                var idxText = File.ReadAllText(idxPath);
                if (int.TryParse(idxText, out int savedIdx) && savedIdx > 0 && savedIdx < total)
                {
                    startSegment = savedIdx;
                    downloadedBytes = new FileInfo(targetPath).Length;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Debug("HlsDownloader", $"Failed to read idx file '{idxPath}': {ex.Message}");
            }
        }

        var fileMode = (startSegment > 0) ? FileMode.Append : FileMode.Create;
        using var outputStream = new FileStream(targetPath, fileMode, FileAccess.Write,
            FileShare.None, bufferSize: 65536, useAsync: true);

        int failedConsecutive = 0;
        int failedTotal = 0;

        for (int i = startSegment; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();

            var segUrl = segments[i];
            var segBytes = await DownloadSegmentWithRetryAsync(segUrl, retries: 3, ct);

            if (segBytes == null || segBytes.Length == 0)
            {
                failedConsecutive++;
                failedTotal++;

                // Si fallan 5 fragmentos seguidos o más del 15% del total, abortar con error
                if (failedConsecutive >= 5 || (failedTotal > 5 && (double)failedTotal / (i + 1) > 0.15))
                {
                    throw new IOException($"Demasiados fragmentos fallidos en el stream HLS ({failedTotal} fallidos de {i + 1}). El enlace del servidor pudo haber expirado.");
                }

                continue;
            }

            failedConsecutive = 0;
            await outputStream.WriteAsync(segBytes, ct);
            downloadedBytes += segBytes.Length;

            try
            {
                File.WriteAllText(idxPath, (i + 1).ToString());
            }
            catch (Exception ex)
            {
                AppLogger.Debug("HlsDownloader", $"Failed to write idx file '{idxPath}': {ex.Message}");
            }

            if (progress != null)
            {
                double pct = (i + 1.0) / total * 100.0;
                double mb = downloadedBytes / (1024.0 * 1024.0);
                string sizeInfo = $"{mb:F1} MB descargados";
                
                string speedStr = "";
                double seconds = stopwatch.Elapsed.TotalSeconds;
                if (seconds > 0)
                {
                    double bytesPerSec = downloadedBytes / seconds;
                    if (bytesPerSec >= 1024 * 1024) speedStr = $"{bytesPerSec / (1024.0 * 1024):F2} MB/s";
                    else if (bytesPerSec >= 1024) speedStr = $"{bytesPerSec / 1024.0:F2} KB/s";
                    else speedStr = $"{bytesPerSec:F0} B/s";
                }
                
                progress.Report(new DownloadProgress(pct, sizeInfo, speedStr, pct >= 100));
            }
        }

        // Validación final de integridad
        if (total > 5 && downloadedBytes < 1024 * 1024) // Menos de 1 MB con más de 5 segmentos
        {
            throw new IOException($"El archivo HLS descargado es demasiado pequeño ({downloadedBytes / 1024} KB), la descarga no fue exitosa.");
        }

        try
        {
            if (File.Exists(idxPath)) File.Delete(idxPath);
        }
        catch (Exception ex)
        {
            AppLogger.Debug("HlsDownloader", $"Failed to delete idx file '{idxPath}': {ex.Message}");
        }

        progress?.Report(new DownloadProgress(100, FormatBytes(downloadedBytes), "", IsFinished: true));
        return targetPath;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<byte[]?> DownloadSegmentWithRetryAsync(string url, int retries, CancellationToken ct)
    {
        for (int attempt = 0; attempt < retries; attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(25)); // 25s timeout por fragmento individual

                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd(ConfigManager.Current.RandomUserAgent);

                var resp = await _client.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token);
                if (resp.IsSuccessStatusCode)
                {
                    var data = await resp.Content.ReadAsByteArrayAsync(cts.Token);
                    if (data != null && data.Length > 0)
                        return data;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                if (attempt < retries - 1)
                    await Task.Delay(500 * (attempt + 1), ct);
            }
        }
        return null;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} B";
    }
}
