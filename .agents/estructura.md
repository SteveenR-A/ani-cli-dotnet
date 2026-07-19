# Estructura del Proyecto (Structure)

Este documento describe con mayor detalle la arquitectura de directorios del proyecto `ani-cli-dotnet` para un fácil entendimiento.

## Directorios Principales
```text
/
├── .agents/                 # Documentación e instrucciones para asistentes de IA.
├── Installer/               # Archivos de empaquetado (WiX v4)
│   ├── AniCS-Installer.wxs  # Código base de empaquetado y registro de Windows
│   └── InstallerDependencies/ # Binarios externos (ej. mpv.exe, yt-dlp) si los hubiere
├── src/
│   ├── AniCS.Core/          # Lógica central del negocio (C#)
│   │   ├── Extractors/      # Clases de web scraping (BaseExtractor, JKAnimeExtractor, etc.)
│   │   ├── History/         # Gestión de historial de episodios vistos
│   │   ├── Models/          # Entidades (Anime, Episode, AppConfig)
│   │   ├── Services/        # Servicios compartidos del motor central
│   │   ├── ConfigManager.cs # Singleton que gestiona la carga/guardado de config.json
│   │   ├── DataCache.cs     # Sistema de caché para evitar requests redundantes
│   │   └── CoreServiceCollectionExtensions.cs # Configuración de Inyección de Dependencias
│   ├── AniCS.CLI/           # Interfaz de Consola interactiva
│   │   ├── Commands/        # Comandos de CLI (Patrón Command)
│   │   ├── Terminal/        # Lógica de renderizado en terminal (UIHelpers, PlayerManager)
│   │   └── Program.cs       # Punto de entrada CLI
│   └── AniCS.Desktop/       # Interfaz Gráfica con Avalonia UI (Patrón MVVM)
│       ├── Assets/          # Imágenes, íconos y logos
│       ├── Controls/        # Controles personalizados de usuario (UserControls / TemplatedControls)
│       ├── Converters/      # Convertidores de Binding de Avalonia (IValueConverter)
│       ├── Services/        # Servicios específicos de UI (DesktopPlayer, DownloadManager)
│       ├── ViewModels/      # Lógica de presentación (MVVM Toolkit)
│       ├── Views/           # Pantallas XAML (MainWindow, SettingsView, PlayerView)
│       └── ThemeManager.cs  # Gestión de colores dinámicos (Cyberpunk, Dracula, Light, etc)
├── build-msi.ps1            # Script de automatización para generar el instalador MSI
└── AniCS.slnx               # Solución principal del proyecto (Formato XML moderno)
```

## Flujo de Datos
1. El usuario interactúa con Desktop o CLI y pide buscar "Naruto".
2. La interfaz llama a un objeto que implementa `IAnimeExtractor` (dentro de Core).
3. `JkAnimeExtractor` usa `HtmlAgilityPack` para bajar y parsear el HTML, devolviendo objetos `Anime`.
4. El usuario selecciona un episodio.
5. `Core` extrae el enlace directo de `.mp4` o `m3u8` desde los servidores de video.
6. La interfaz de usuario lanza el proceso `mpv.exe` pasándole el enlace directo.
