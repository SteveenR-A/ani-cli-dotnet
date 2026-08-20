using AniCS.Models;
namespace AniCS.Resolver;

/// <summary>
/// Factoría que instancia el backend de resolución correcto según <see cref="AppConfig.ResolverBackend"/>.
/// </summary>
public static class ResolverFactory
{
    /// <summary>
    /// Crea el backend de resolver adecuado según el modo configurado.
    /// </summary>
    /// <param name="mode">Modo seleccionado en Ajustes.</param>
    public static IResolverBackend Create(ResolverBackendMode mode)
    {
        if (OperatingSystem.IsAndroid())
        {
            return new NativeResolverBackend();
        }

        return mode switch
        {
            ResolverBackendMode.Native => new NativeResolverBackend(),
            ResolverBackendMode.YtDlp  => new YtDlpResolverBackend(),

            // Auto: preferir nativo (siempre disponible).
            // Si en el futuro hubiera razones para preferir yt-dlp en Auto,
            // se puede cambiar la lógica aquí sin tocar los backends.
            ResolverBackendMode.Auto   => new NativeResolverBackend(),

            _ => new NativeResolverBackend(),
        };
    }

    /// <summary>
    /// Crea el backend según la configuración actual del usuario.
    /// </summary>
    public static IResolverBackend CreateFromConfig()
        => Create(ConfigManager.Current.ResolverBackend);
}
