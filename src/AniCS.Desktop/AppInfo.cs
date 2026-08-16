using System.Reflection;

namespace AniCS.Desktop;

/// <summary>
/// Single source of truth for the app version and release notes.
/// Both MainWindow and SettingsView read from here (no more duplicated changelogs).
/// </summary>
public static class AppInfo
{
    /// <summary>Version of the currently running assembly (e.g. "1.6.3").</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.3";

    /// <summary>Local fallback notes, shown when the GitHub release has no body.</summary>
    public static string LatestChangelog { get; set; } =
        "✨ ¡Novedades y Mejoras de la versión 1.6.3!\n\n" +
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
        "• Nueva opción en Ajustes para elegir cualquier carpeta personalizada donde se guardan las descargas y animes locales.\n" +
        "• Selector de carpetas nativo (Examinar...) en PC y opción de restablecer a la ruta por defecto en ambas plataformas.\n\n" +
        "🛠️ Estabilidad y Corrección de Notas de Parche en Móvil:\n" +
        "• Corrección del cierre inesperado de la aplicación al presionar 'Ver Notas de Parche' en Ajustes de Android.\n" +
        "• Visualización dinámica y estilizada de las notas de la versión actual con desplazamiento fluido en el modal.\n\n" +
        "📡 Banner Offline No Intrusivo y Mejor Control de Red:\n" +
        "• El aviso 'Sin conexión a internet' se oculta automáticamente al abrir el reproductor de video para no obstaculizar la visualización de episodios locales o remotos.\n" +
        "• Se añade botón de descarte manual (X) y temporizador de auto-ocultado para el banner offline en PC y Móvil.\n\n" +
        "🔙 Mejoras previas v1.6.2:\n" +
        "• Auto-búsqueda y vinculación de metadata oficial para animes locales.\n" +
        "• Reanudación granular por segmentos HLS y soporte de descarga continua en segundo plano con Foreground Service.\n\n" +
        "¡Gracias por disfrutar de AniCS!";
}