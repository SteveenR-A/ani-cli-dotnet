# Estructura del Proyecto (Structure)

Este documento describe la arquitectura de directorios del proyecto `ani-cli-dotnet` para un fácil entendimiento. El árbol refleja la estructura real de `src/` (5 proyectos).

## Directorios Principales

```text
/
├── .agents/                 # Documentación e instrucciones para asistentes de IA.
├── Installer/               # Archivos de empaquetado (WiX v4)
│   ├── AniCS-Installer.wxs  # Código base de empaquetado y registro de Windows
│   └── InstallerDependencies/ # Binarios externos (ej. mpv.exe, yt-dlp) si los hubiere
├── tests/                   # Zona de experimentos/pruebas (sin framework formal)
├── src/
│   ├── AniCS.Core/          # Lógica central del negocio (C#)
│   │   ├── Extractors/      # Scraping web (BaseExtractor, JKAnimeExtractor, MundoDonghuaExtractor, ExtractorFactory)
│   │   ├── History/         # Historial local de visualización (WatchHistory.cs)
│   │   ├── Models/          # Entidades (AnimeResult, Episode, VideoServer, AppConfig)
│   │   ├── Services/        # IPlayerService / WindowsPlayerService (usados por la CLI)
│   │   ├── AppLogger.cs     # Log centralizado a %LocalAppData%/AniCS/logs
│   │   ├── ConfigManager.cs # Carga/guardado de config.json (%LocalAppData%/AniCS)
│   │   ├── DataCache.cs     # Caché RAM con TTL + caché de imágenes en disco (LRU)
│   │   └── CoreServiceCollectionExtensions.cs # Configuración de Inyección de Dependencias (AddAniCSCore)
│   ├── AniCS.CLI/           # Interfaz de Consola interactiva (Spectre.Console, AOT)
│   │   ├── Commands/        # Comandos de CLI (Patrón Command + CommandRouter)
│   │   ├── Terminal/        # Renderizado en terminal (DetailsPrompt, PlaybackController, KittyGraphics, UIHelpers)
│   │   └── Program.cs       # Punto de entrada CLI
│   ├── AniCS.Desktop/       # Interfaz Gráfica con Avalonia UI 12 (MVVM + CommunityToolkit)
│   │   ├── App.axaml.cs     # Composición raíz de DI (Core + IPlayerBackend + IResolverBackend)
│   │   ├── MainWindow.axaml.cs # Navegación general + montado de Paradigmas
│   │   ├── Assets/          # Imágenes, íconos y logos
│   │   ├── Controls/        # AnimeBlockControl, AnimeCardControl, ServerPickerDialog, HudRadialMenuDialog, ChangelogWindow
│   │   ├── Converters/      # Convertidores de Binding de Avalonia (IValueConverter)
│   │   ├── Services/        # DesktopPlayer (audio Openings), DownloadManager, AppUpdateService (AutoUpdate GitHub)
│   │   ├── ViewModels/      # HomeViewModel, ViewModelBase (CommunityToolkit.Mvvm)
│   │   ├── Views/           # HomeView, SearchView, AnimeDetailsView, CalendarView, TopAnimesView, SeeMoreView, HistoryView, SettingsView, DownloadsView
│   │   │   └── Paradigms/   # Paradigmas visuales (ASCII, HUD, Node, Kinetic, Spatial, AndroidApp)
│   │   └── ThemeManager.cs  # Gestión de colores dinámicos (Cyberpunk, Dracula, Light, etc.)
│   ├── AniCS.Player/        # Reproducción (NUEVO)
│   │   ├── IPlayerBackend.cs # Contrato de reproducción + PlayerState/PlaySession
│   │   ├── PlayerFactory.cs  # Selección de backend según AppConfig.PlayerBackend
│   │   ├── LibVlcBackend.cs  # Reproductor embebido (LibVLCSharp)
│   │   ├── MpvBackend.cs     # Fallback: lanza mpv.exe/mpvnet.exe
│   │   └── Controls/         # VideoPlayerControl (reproductor embebido con OSD/fullscreen)
│   └── AniCS.Resolver/      # Resolución y descarga (NUEVO)
│       ├── IResolverBackend.cs # Contrato + ResolvedMedia/DownloadResultCode
│       ├── ResolverFactory.cs  # Selección de backend según AppConfig.ResolverBackend
│       ├── YtDlpResolverBackend.cs # Fallback externo (redirector.php, etc.)
│       └── Native/            # Resolvedor nativo (NativeResolverBackend, HlsParser, HlsDownloader)
├── build-msi.ps1            # Script de automatización para generar el instalador MSI
└── AniCS.slnx               # Solución principal del proyecto (Formato XML moderno)
```

## Flujo de Datos

1. El usuario interactúa con Desktop o CLI y pide buscar "Naruto".
2. La interfaz llama a un objeto que implementa `IAnimeExtractor` (dentro de Core), elegido por `ExtractorFactory` según `AppConfig.ContentType`/`DefaultExtractor` (MundoDonghua si es donghua, si no JKAnime).
3. El extractor usa `HtmlAgilityPack` para bajar y parsear el HTML, devolviendo objetos `AnimeResult`/`Episode`. (JKAnime usa sesión + CSRF para el AJAX de episodios.)
4. El usuario selecciona un episodio.
5. Core extrae los `VideoServer` y resuelve el enlace directo (`.m3u8`/`.mp4`).
6. **Reproducción**: Desktop envía la URL al `IPlayerBackend` activo (LibVLC embebido o mpv).
7. **Descarga**: Desktop envía la URL al `IResolverBackend` activo (nativo HLS o yt-dlp).
