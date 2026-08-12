using System.Threading.Tasks;

namespace AniCS.Player;

/// <summary>
/// Representa el estado actual de la sesión de reproducción.
/// </summary>
public enum PlayerState
{
    Idle,       // Sin media cargada
    Buffering,  // Cargando/buffering
    Playing,    // Reproduciendo
    Paused,     // En pausa
    Ended,      // Terminó
    Error       // Error de reproducción
}

/// <summary>
/// Datos completos de la sesión de reproducción activa.
/// </summary>
public record PlaySession(
    string Url,
    string Title,
    double Position,    // segundos
    double Duration,    // segundos
    PlayerState State,
    bool IsCompleted,   // >= 88% visto o faltan <= 90s
    double BufferPercentage = 0, // 0.0 a 1.0 (cuánto caché hay)
    uint VideoWidth = 0,
    uint VideoHeight = 0
);

/// <summary>
/// Opciones al iniciar la reproducción.
/// </summary>
public class PlayOptions
{
    public string? Referer { get; set; }
    public string? UserAgent { get; set; }
    public string Quality { get; set; } = "Mejor";
    public double StartPosition { get; set; } = 0;
}

/// <summary>
/// Interfaz principal del backend de reproducción.
/// Implementaciones: MpvBackend (externo), LibVlcBackend (embebido).
/// </summary>
public interface IPlayerBackend : IDisposable
{
    /// <summary>Nombre descriptivo del backend, p.ej. "mpv" o "LibVLC".</summary>
    string BackendName { get; }

    /// <summary>True si el backend está disponible en el sistema actual.</summary>
    bool IsAvailable { get; }

    /// <summary>Sesión de reproducción activa, null si no hay nada reproduciendo.</summary>
    PlaySession? CurrentSession { get; }

    /// <summary>Se dispara cuando cambia la posición, estado o duración.</summary>
    event Action<PlaySession>? SessionChanged;

    /// <summary>Se dispara si el backend reporta un error.</summary>
    event Action<string>? ErrorOccurred;

    /// <summary>Inicia la reproducción de la URL dada.</summary>
    Task PlayAsync(string url, string title, PlayOptions? opts = null);

    /// <summary>Pausa la reproducción activa.</summary>
    Task PauseAsync();

    /// <summary>Reanuda la reproducción pausada.</summary>
    Task ResumeAsync();

    /// <summary>Salta a la posición indicada en segundos.</summary>
    Task SeekAsync(double seconds);

    /// <summary>Detiene la reproducción y libera recursos de media (no el backend).</summary>
    void Stop();

    /// <summary>Volumen del reproductor (0-100+).</summary>
    int Volume { get; set; }
}
