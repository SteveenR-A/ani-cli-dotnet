using AniCS.Models;

namespace AniCS.Player;

/// <summary>
/// Factoría que instancia el backend de reproducción correcto
/// según <see cref="AppConfig.PlayerBackend"/>.
/// </summary>
public static class PlayerFactory
{
    /// <summary>
    /// Crea el backend según el modo indicado.
    /// </summary>
    public static IPlayerBackend Create(PlayerBackendMode mode)
    {
        if (System.OperatingSystem.IsAndroid())
        {
            return new LibVlcBackend();
        }

        return mode switch
        {
            PlayerBackendMode.Native => new LibVlcBackend(),
            PlayerBackendMode.Mpv    => new MpvBackend(),

            // Auto: intentar LibVLC primero; si no está disponible, caer a mpv.
            PlayerBackendMode.Auto => TryCreateLibVlc() ?? (IPlayerBackend)new MpvBackend(),

            _ => new MpvBackend(),
        };
    }

    /// <summary>
    /// Crea el backend según la configuración actual del usuario.
    /// </summary>
    public static IPlayerBackend CreateFromConfig()
        => Create(AniCS.ConfigManager.Current.PlayerBackend);

    private static LibVlcBackend? TryCreateLibVlc()
    {
        try
        {
            var backend = new LibVlcBackend();
            return backend.IsAvailable ? backend : null;
        }
        catch { return null; }
    }
}
