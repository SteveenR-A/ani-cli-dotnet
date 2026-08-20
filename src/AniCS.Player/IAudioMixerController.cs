namespace AniCS.Player;

/// <summary>
/// Abstracción del control de volumen del sistema operativo.
/// En Windows: implementado por WindowsAudioSessionController (NAudio / Core Audio).
/// En otras plataformas: se pasa null → VideoPlayerControl usa LibVLC directamente.
/// </summary>
public interface IAudioMixerController : IDisposable
{
    /// <summary>
    /// Volumen de la sesión de audio del proceso en el mezclador del sistema (0–100).
    /// Leer este valor equivale a leer la barra del mezclador de Windows.
    /// </summary>
    int Volume { get; set; }

    /// <summary>Silenciado en la sesión de audio del sistema.</summary>
    bool IsMuted { get; set; }

    /// <summary>
    /// Disparado cuando el volumen cambia DESDE FUERA de la app
    /// (mezclador de Windows, Lenovo Vantage, etc.).
    /// El argumento es el nuevo valor en 0–100.
    /// Siempre se invoca en el hilo que lo detecta; el receptor debe marshalear a UI si es necesario.
    /// </summary>
    event Action<int>? ExternalVolumeChanged;

    /// <summary>
    /// Intenta enlazar (o reenlazar) con la sesión de audio activa del proceso.
    /// Debe llamarse después de que LibVLC haya empezado a reproducir audio,
    /// ya que la sesión no existe hasta ese momento.
    /// </summary>
    void TryAcquireSession();
}
