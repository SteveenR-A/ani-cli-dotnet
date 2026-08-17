using System.Reflection;

namespace AniCS.Desktop;

/// <summary>
/// Single source of truth for the app version and release notes.
/// Both MainWindow and SettingsView read from here (no more duplicated changelogs).
/// </summary>
public static class AppInfo
{
    /// <summary>Official organization / studio name.</summary>
    public static string Brand => "YumeWorks";

    /// <summary>Official project slogan.</summary>
    public static string Slogan => "Siente la fluidez hacia tus historias favoritas";

    /// <summary>Version of the currently running assembly (e.g. "1.6.4").</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.4";

    /// <summary>Local fallback notes, shown when the GitHub release has no body.</summary>
    public static string LatestChangelog { get; set; } =
        "✨ ¡Novedades y Mejoras de la versión 1.6.4!\n\n" +
        "🎨 Nueva Identidad Visual e Iconos Oficiales:\n" +
        "• Actualización completa del icono de la aplicación en escritorio e instalador con la nueva identidad visual de AniCS.\n" +
        "• Mejoras en la presentación visual y consistencia de recursos en todas las plataformas.\n\n" +
        "📁 Almacenamiento en Carpeta del Sistema DCIM (Android):\n" +
        "• Las descargas en Android ahora se guardan directamente en la carpeta pública del sistema 'DCIM/AniCS' (/storage/emulated/0/DCIM/AniCS).\n" +
        "• Tus episodios descargados ahora son reconocidos de forma instantánea por tu galería, gestores de archivos y reproductores nativos del móvil.\n" +
        "• Solicitud automática de permisos de almacenamiento en tiempo de ejecución (READ_MEDIA_VIDEO, WRITE_EXTERNAL_STORAGE y READ_EXTERNAL_STORAGE).\n\n" +
        "📱 Modo Pantalla Completa Inmersivo 100% (Android):\n" +
        "• Eliminación total de la barra de estado (señal, hora, batería, wifi y notificaciones) durante la reproducción en horizontal mediante WindowCompat y WindowInsetsController moderno.\n" +
        "• Control de inmersión transitoria por deslizamiento para una experiencia cinemática limpia y sin distracciones.\n\n" +
        "💡 Pantalla Siempre Activa / Wake Lock (Android):\n" +
        "• El reproductor mantiene la pantalla encendida automáticamente durante la reproducción de videos, evitando que el dispositivo se apague o bloquee por inactividad.\n\n" +
        "⚙️ Personalización de Ubicación de Almacenamiento (PC & Móvil):\n" +
        "• Opción en Ajustes para elegir cualquier carpeta personalizada donde se guardan las descargas y animes locales con selector nativo en PC.\n\n" +
        "📡 Banner Offline No Intrusivo y Mejor Control de Red:\n" +
        "• El aviso 'Sin conexión a internet' se oculta automáticamente en el reproductor de video con botón de descarte manual (X) y temporizador de auto-ocultado.\n\n" +
        "¡Gracias por disfrutar de AniCS!";
}