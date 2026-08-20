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

    /// <summary>Version of the currently running assembly (e.g. "1.6.5").</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.5";

    /// <summary>Local fallback notes, shown when the GitHub release has no body.</summary>
    public static string LatestChangelog { get; set; } =
        "✨ ¡Novedades y Mejoras de la versión 1.6.5!\n\n" +
        "💡 Sincronización Inteligente de Pantalla Activa (Android):\n" +
        "• La pantalla se mantiene encendida exclusivamente mientras el video se encuentra en reproducción activa.\n" +
        "• Al pausar el video, el sistema ahora atenúa y suspende la pantalla normalmente según el tiempo de inactividad del dispositivo.\n" +
        "• Al pasar la app a segundo plano o bloquear la pantalla, la reproducción se pausa y se liberan inmediatamente las banderas de pantalla activa.\n\n" +
        "📥 Corrección de Scroll y Estado en Descargas (PC & Móvil):\n" +
        "• Cambiar manualmente el estado de progreso (Sin ver / En progreso / Terminado) ya no recarga la lista completa ni reinicia la posición del scroll hacia arriba.\n" +
        "• Actualización reactiva e instantánea de badges y conservación del estado desplegado de los animes.\n\n" +
        "👆 Corrección de Desplazamiento en Top Animes y Ver Más (Android):\n" +
        "• Solución integral al conflicto donde arrastrar o deslizar para hacer scroll en la lista de Top Animes y Ver Más abría accidentalmente el anime seleccionado.\n" +
        "• Migración al sistema de gestos Tapped de Avalonia para un desplazamiento táctil suave y fluido.\n\n" +
        "⚡ Sincronización en Tiempo Real del Reproductor:\n" +
        "• Nuevo evento de cambio de estado de reproducción que sincroniza al instante el botón central de Play/Pausa y la visibilidad de controles.\n\n" +
        "🎨 Mejoras Previas v1.6.4:\n" +
        "• Nueva identidad visual e iconos oficiales de AniCS.\n" +
        "• Almacenamiento en carpeta pública del sistema DCIM/AniCS en Android.\n" +
        "• Modo pantalla completa inmersivo 100% sin barra de estado.\n" +
        "• Personalización de carpeta de descargas en PC y Móvil.\n\n" +
        "¡Gracias por disfrutar de AniCS!";
}