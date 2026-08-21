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

    /// <summary>Version of the currently running assembly (e.g. "1.6.6").</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.6";

    /// <summary>Local fallback notes, shown when the GitHub release has no body.</summary>
    public static string LatestChangelog { get; set; } =
        "✨ ¡Novedades y Mejoras de la versión 1.6.6!\n\n" +
        "📥 Control de Descargas Simultáneas y Cola Ordenada (FIFO):\n" +
        "• Nuevo sistema de cola organizada para descargas múltiples: los episodios se descargan progresivamente en orden según el límite configurado sin saturar el ancho de banda.\n" +
        "• Ajuste configurable de descargas simultáneas en la sección de Configuración (1 a 10 en PC, 1 a 5 en Android).\n" +
        "• Auto-avance instantáneo en la cola cuando un capítulo se completa, se pausa o se cancela.\n\n" +
        "⏮️ Navegación de Capítulos en el Reproductor (Streaming & Descargas):\n" +
        "• Nuevos botones visuales de Episodio Anterior (⏮) y Siguiente (⏭) en los reproductores de PC y Android.\n" +
        "• Atajos de teclado en PC: tecla 'P' para el capítulo anterior y tecla 'N' para el capítulo siguiente.\n" +
        "• Cambio fluido de episodio sin necesidad de cerrar la ventana del reproductor ni recargar la vista.\n\n" +
        "🔔 Mejoras en Notificaciones de Descarga (Android):\n" +
        "• Notificaciones enriquecidas con progreso detallado de la descarga en segundo plano.\n" +
        "• Notificación de finalización que informa al usuario cuando todos los capítulos en cola se han descargado con éxito.\n\n" +
        "🛠️ Estabilidad y Optimización de Memoria:\n" +
        "• Corrección de fugas de eventos en LibVLC y reutilización de caché en el historial de visualización.\n" +
        "• Soporte completo de control IPC para MPV (Pausa, Reanudar, Búsqueda temporal y Volumen).\n" +
        "• Reanudación limpia y determinista en el reproductor nativo de Android sin demoras arbitrarias.\n\n" +
        "¡Gracias por disfrutar de AniCS!";
}