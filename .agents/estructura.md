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
│   │   ├── Extractors/      # Clases de web scraping (BaseExtractor, JkAnimeExtractor)
│   │   ├── Models/          # Entidades (Anime, Episode, AppConfig)
│   │   └── ConfigManager.cs # Singleton que gestiona la carga/guardado de config.json
│   ├── AniCS.CLI/           # Interfaz de Consola interactiva
│   │   ├── Terminal/        # Lógica de renderizado en terminal (UIHelpers, PlayerManager)
│   │   └── Program.cs       # Punto de entrada CLI
│   └── AniCS.Desktop/       # Interfaz Gráfica con Avalonia UI
│       ├── Views/           # Pantallas XAML (MainWindow, SettingsView, PlayerView)
│       ├── Assets/          # Imágenes, íconos y logos
│       └── ThemeManager.cs  # Gestión de colores dinámicos (Cyberpunk, Dracula, Light, etc)
├── build-msi.ps1            # Script de automatización para generar el instalador MSI
└── AniCS.sln                # Solución principal del proyecto
```

## Flujo de Datos
1. El usuario interactúa con Desktop o CLI y pide buscar "Naruto".
2. La interfaz llama a un objeto que implementa `IAnimeExtractor` (dentro de Core).
3. `JkAnimeExtractor` usa `HtmlAgilityPack` para bajar y parsear el HTML, devolviendo objetos `Anime`.
4. El usuario selecciona un episodio.
5. `Core` extrae el enlace directo de `.mp4` o `m3u8` desde los servidores de video.
6. La interfaz de usuario lanza el proceso `mpv.exe` pasándole el enlace directo.
