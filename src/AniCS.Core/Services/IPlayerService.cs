using System.Threading.Tasks;

namespace AniCS.Services;

/// <summary>
/// Interfaz para manejar el lanzamiento de reproductores externos y la resolución de videos.
/// </summary>
public interface IPlayerService
{
    /// <summary>
    /// Comprueba si el extractor (yt-dlp) está disponible en el sistema.
    /// </summary>
    bool IsYtDlpAvailable();

    /// <summary>
    /// Usa yt-dlp para resolver la URL directa de video de una página de servidor.
    /// </summary>
    Task<string> ResolveVideoUrlWithYtDlpAsync(string pageUrl, string? referer = null);

    /// <summary>
    /// Lanza el reproductor externo (mpv) con la URL especificada.
    /// </summary>
    Task PlayAsync(string url, string title, string? referer);

    /// <summary>
    /// Descarga el video utilizando yt-dlp.
    /// </summary>
    void Download(string videoUrl, string animeTitle, string episodeNum, string targetDir, string? referer);
}
