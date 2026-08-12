using System.Reflection;

namespace AniCS.Desktop;

/// <summary>
/// Single source of truth for the app version and release notes.
/// Both MainWindow and SettingsView read from here (no more duplicated changelogs).
/// </summary>
public static class AppInfo
{
    /// <summary>Version of the currently running assembly (e.g. "1.5.5.0").</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

    /// <summary>Local fallback notes, shown when the GitHub release has no body.</summary>
    public static string LatestChangelog { get; set; } =
        "¡Hola! Novedades de la versión 1.6.0:\n\n" +
        "• 🔄 Auto-Recover en Reproductor: Reconexión y reanudación automática transparente si el stream HLS o enlace de servidor se interrumpe mid-playback.\n" +
        "• 📱 Soporte e Interfaz Móvil Android: UI rediseñada con carruseles, barra de búsqueda en vivo, estado vacío de historial y Bottom Navigation Bar.\n" +
        "• ⚙️ Barra Sticky en Ajustes: El botón 'Guardar Cambios' permanece fijo abajo en pantalla sin importar el desplazamiento de la página.\n" +
        "• 🚀 Integración CI/CD Multiplataforma: Compilación y distribución automatizada en GitHub Actions generando tanto instaladores MSI para Windows como paquetes APK firmados para Android.\n\n" +
        "¡Gracias por usar AniCS!";
}