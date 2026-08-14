using System.Reflection;

namespace AniCS.Desktop;

/// <summary>
/// Single source of truth for the app version and release notes.
/// Both MainWindow and SettingsView read from here (no more duplicated changelogs).
/// </summary>
public static class AppInfo
{
    /// <summary>Version of the currently running assembly (e.g. "1.6.1").</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.6.1";

    /// <summary>Local fallback notes, shown when the GitHub release has no body.</summary>
    public static string LatestChangelog { get; set; } =
        "✨ ¡Novedades y Mejoras de la versión 1.6.1!\n\n" +
        "🔊 Sincronización de Audio (PC):\n" +
        "• Solucionado el desajuste de sincronía entre el reproductor y el volumen del sistema mediante la integración directa con sesiones de Windows Core Audio.\n\n" +
        "🎛️ Estabilidad del Controlador de Video (PC & Móvil):\n" +
        "• Corregido el problema donde los controles / OSD del reproductor nativo se activaban o parpadeaban involuntariamente por pérdida de foco o eventos de ventana.\n\n" +
        "🔙 Navegación Gestual y Botón Físico Universal (Android):\n" +
        "• Corregido el bloqueo de eventos de retroceso mediante el nuevo servicio desacoplado MobileNavigationService y el puente con AvaloniaActivity.BackRequested y AndroidX OnBackPressedDispatcher.\n" +
        "• Soporte completo y fluido para gestos de deslizamiento en bordes y barra de navegación de 3 botones.\n\n" +
        "📱 Reproductor Móvil Rediseñado (Android):\n" +
        "• Nuevos iconos vectoriales circulares con estilo 'frosted glass' (replay_10, forward_10, play, pause, chevron_left, rotación).\n" +
        "• Detección y visualización dinámica de la resolución física real (1080p, 720p, etc.) decodificada por el procesador del teléfono.\n\n" +
        "🧹 Pulido de Interfaz y Usabilidad (Móvil):\n" +
        "• Eliminado el botón de recarga duplicado en la cabecera superior para una barra más limpia y ergonómica.\n" +
        "• Ocultado reactivo inteligente de la sección 'Estrenos / Destacados' cuando no hay estrenos activos en cartelera.\n\n" +
        "⏱️ Sincronización Inteligente de Estados (Streaming y Descargas):\n" +
        "• Marcado automático a 'En progreso' al iniciar y a 'Visto' al superar el 85% de la reproducción (adaptado a endings largos y créditos de películas).\n\n" +
        "📥 Filtros en Descargas y Gestión de Historial:\n" +
        "• Filtros por estado (Todos, Sin ver, En progreso, Vistos) y badges interactivos.\n" +
        "• Botón de eliminación individual por anime en el Historial.\n" +
        "• Visor modal en pantalla completa para ver portadas de anime en alta definición.\n\n" +
        "¡Gracias por disfrutar de AniCS!";
}