namespace AniCS.Resolver;

/// <summary>
/// Tipo de media resuelta.
/// </summary>
public enum MediaType
{
    Unknown,
    Hls,        // Streaming en segmentos (.m3u8)
    Mp4,        // Video directo (.mp4, .mkv, .avi...)
    Audio,      // Solo audio
}

/// <summary>
/// Resultado de la resolución de una URL de video.
/// </summary>
public record ResolvedMedia(
    string OriginalUrl,
    string DirectUrl,       // URL directa al stream/archivo
    MediaType Type,
    string? Referer = null,
    string? UserAgent = null
);

/// <summary>
/// Opciones de resolución.
/// </summary>
public class ResolveOptions
{
    public string? Referer { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// Progreso de descarga.
/// </summary>
public record DownloadProgress(
    double Percent,
    string SizeInfo,    // Ej: "45.2 MB / ~320 MB"
    string Speed,       // Ej: "2.5 MiB/s"
    bool IsFinished
);

/// <summary>
/// Resultado final de una descarga.
/// </summary>
public enum DownloadResultCode { Success, Cancelled, Error }

public record DownloadResult(
    DownloadResultCode Code,
    string? OutputPath = null,
    string? ErrorMessage = null
);

/// <summary>
/// Interfaz principal del backend de resolución y descarga.
/// Implementaciones: YtDlpResolverBackend (externo), NativeResolverBackend (nativo HLS).
/// </summary>
public interface IResolverBackend
{
    /// <summary>Nombre descriptivo, p.ej. "yt-dlp" o "Native".</summary>
    string BackendName { get; }

    /// <summary>True si el backend está disponible.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Resuelve la URL de una página/servidor a la URL directa del stream.
    /// Devuelve <see cref="ResolvedMedia"/> con la URL y tipo detectado.
    /// </summary>
    Task<ResolvedMedia> ResolveAsync(string url, ResolveOptions? opts = null, CancellationToken ct = default);

    /// <summary>
    /// Descarga el media resuelto al path indicado.
    /// El archivo de salida será .mp4 en todos los casos (remux si es HLS).
    /// </summary>
    Task<DownloadResult> DownloadAsync(
        ResolvedMedia media,
        string outputPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken ct = default);
}
