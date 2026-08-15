using System.Reflection;

namespace AniCS.Desktop;

/// <summary>
/// Single source of truth for the app version and release notes.
/// Both MainWindow and SettingsView read from here (no more duplicated changelogs).
/// </summary>
public static class AppInfo
{
    /// <summary>Version of the currently running assembly (e.g. "1.6.2").</summary>
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.6.2";

    /// <summary>Local fallback notes, shown when the GitHub release has no body.</summary>
    public static string LatestChangelog { get; set; } =
        "✨ ¡Novedades y Mejoras de la versión 1.6.2!\n\n" +
        "📁 Auto-Búsqueda y Sincronización de Animes Locales (PC & Móvil):\n" +
        "• Al presionar 'Ver Online' en animes importados desde la carpeta de videos sin metadata previa, el sistema busca automáticamente la serie en línea por su título.\n" +
        "• Recuperación y vinculación permanente de la portada oficial (HD), sinopsis y lista de episodios en el gestor de descargas.\n" +
        "• Modo de respaldo local: si el anime no está en línea, la vista muestra directamente los episodios descargados para su reproducción sin pantallas en blanco.\n\n" +
        "🎛️ Auto-Ocultado Inteligente en Pausa (PC):\n" +
        "• Los controles y el cursor del reproductor ahora se ocultan automáticamente tras 3 segundos de inactividad incluso cuando el video está pausado.\n" +
        "• Reaparición instantánea ante movimiento del ratón, clics en pantalla o cualquier tecla del teclado.\n\n" +
        "📂 Blindaje, Migración y Detección de Descargas:\n" +
        "• Garantizada la ruta oficial de descargas en PC en 'Videos\\AniCS' con reubicación inteligente ante carpetas de OneDrive o rutas movidas.\n" +
        "• Carga persistente garantizada (EnsureLoaded) y auto-escaneo automático en disco al abrir la pestaña Descargas en PC y Móvil.\n" +
        "• Migración automática de historiales legacy de descargas (downloads.json).\n\n" +
        "🎬 Experiencia de Reproducción y Navegación (PC & Móvil):\n" +
        "• Corrección de navegación móvil: al presionar 'Volver' en el reproductor regresa directamente a la lista de episodios del anime seleccionado sin saltar al Inicio.\n" +
        "• Indicadores visuales de estado en tiempo real: nuevo badge dinámico tanto en PC como en Android para identificar claramente si el video está 'Cargando stream', 'Almacenando en búfer', 'Reproduciendo' o 'En Pausa', además del avance del búfer de red.\n" +
        "• Eliminación del aviso flotante al rotar el dispositivo en el reproductor móvil, manteniendo la rotación y modo inmersivo 100% fluidos.\n\n" +
        "📥 Descargas Continuas, Reanudación Granular y Control de Pausa:\n" +
        "• Continuidad en segundo plano (Android): nuevo Foreground Service con notificación persistente de progreso y WakeLock para evitar cancelaciones al minimizar la app.\n" +
        "• Reanudación granular por segmentos (HLS .ts con checkpointing .idx) y por bytes (MP4 con cabeceras Range): reanuda exactamente donde se pausó sin empezar desde cero.\n" +
        "• Control de Pausar / Reanudar funcional: el botón cambia de forma instantánea a 'Reanudar' al pausar sin perder ni borrar los archivos parciales.\n" +
        "• Sistema de reintentos automáticos con Jitter (1-3s) y renovación automática de enlaces expirados ante cortes de internet.\n" +
        "• Corrección al iniciar descargas tras seleccionar servidor en el cuadro de diálogo.\n\n" +
        "📡 Monitorización de Red y Banner Offline en Tiempo Real:\n" +
        "• Detección automática del estado de conexión a Internet en PC y Android.\n" +
        "• Banner superior de estado inmediato al perder señal ('Sin conexión a internet') y confirmación visual al restablecerse.\n\n" +
        "🔎 Directorio y Búsqueda Avanzada:\n" +
        "• Paginación completa del catálogo (?p=1, ?p=2, ?p=3...) con navegación de páginas y salto manual directo por número de página.\n" +
        "• Carga dinámica de la lista completa de más de 45 géneros oficiales desde la web.\n\n" +
        "🔙 Mejoras previas v1.6.1:\n" +
        "• Sincronización de volumen con Windows Core Audio y estabilidad de foco.\n" +
        "• Navegación universal por gestos y botón físico de retroceso en Android.\n" +
        "• Rediseño estético del reproductor móvil con controles circulares translúcidos.\n\n" +
        "¡Gracias por disfrutar de AniCS!";
}