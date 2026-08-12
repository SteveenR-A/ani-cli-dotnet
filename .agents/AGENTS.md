# Normas y Flujo de Trabajo (Rules & Workflow) - AniCS

Este archivo (`AGENTS.md`) define las reglas globales y convenciones del proyecto `AniCS`, una aplicación multiplataforma (C#/.NET 10) para buscar, reproducir y descargar anime mediante web scraping (JKAnime, MundoDonghua), con arquitectura modular.

## Arquitectura y División de Proyectos (Contexto)

La solución `AniCS.slnx` contiene **5 proyectos** (en `/src/`):

- **AniCS.Core**: El motor central. Modelos (`AppConfig`, `AnimeResult`, `Episode`, `VideoServer`), interfaces (`IAnimeExtractor`), la lógica pesada de scraping web con `HtmlAgilityPack` (`BaseExtractor`, `JKAnimeExtractor`, `MundoDonghuaExtractor`, `AnimeAV1Extractor`). Gestiona configuración (`ConfigManager`), caché (`DataCache`) e historial (`WatchHistory`).
- **AniCS.Desktop**: La interfaz gráfica multiplataforma en **Avalonia UI 12** (C# / XAML, MVVM con CommunityToolkit). La composición raíz de DI se hace en `App.axaml.cs`.
- **AniCS.CLI**: Interfaz de consola interactiva con Spectre.Console (AOT).
- **AniCS.Player**: Abstracción de reproducción `IPlayerBackend` con backends **LibVLC embebido** (`LibVlcBackend`) y **mpv externo** (`MpvBackend`). Selección vía `PlayerFactory` según `AppConfig.PlayerBackend` (`Auto`/`Native`/`Mpv`).
- **AniCS.Resolver**: Abstracción de resolución/descarga `IResolverBackend` con **resolvedor nativo** (`NativeResolverBackend`, HLS propio) y **yt-dlp** (`YtDlpResolverBackend`). Selección vía `ResolverFactory` según `AppConfig.ResolverBackend` (`Auto`/`Native`/`YtDlp`).

> **Importante**: Para entender los "Paradigmas Visuales" revisa `.agents/paradigmas.md`. La lógica HTTP/selectores de cada fuente está documentada en `.agents/scraper_logic.md`.

Adicionalmente:
- **/Installer**: Definición WiX Toolset v4 (`AniCS-Installer.wxs`) para el `.msi` en Windows. Se compila con `build-msi.ps1`.

## Reglas de Desarrollo (Norms/Guidelines)

1. **Responsabilidad Separada (SOLID)**: Bajo ninguna circunstancia se debe incluir lógica de scraping o peticiones web a las fuentes dentro de `AniCS.Desktop`, `AniCS.CLI`, `AniCS.Player` o `AniCS.Resolver`. Todo eso pertenece a `AniCS.Core/Extractors`.
2. **Backends Player/Resolver**: `AniCS.Player` y `AniCS.Resolver` solo reproducen y resuelven/descargan URLs; NO conocen las fuentes (JKAnime/MundoDonghua). Los matices por servidor (ej. Referer/Origin de `redirector.php` de MundoDonghua) viven en los backends de Resolver o se resuelven antes en `AniCS.Core`.
3. **Dependencias Externas**: La reproducción embebida usa **LibVLC** (bundled, no requiere VLC instalado); `mpv.exe` es el fallback clásico. La resolución usa el **resolvedor nativo .NET**; `yt-dlp.exe` es el fallback para servidores externos protegidos (Mp4upload, Streamtape, etc.). No asumas que `mpv`/`yt-dlp` están instalados; comprueba disponibilidad y guía al usuario.
4. **Manejo de HTTP y User-Agents**: No "quemes" (hardcode) las cabeceras HTTP directamente en las peticiones de los Extractors. Utiliza siempre la configuración de `AppConfig` gestionada por `ConfigManager.Current.RandomUserAgent`.
5. **Avalonia UI**: Al trabajar con `.axaml` en `AniCS.Desktop`, algunos editores presentan falsos positivos ("el nombre no existe en el contexto"). Se soluciona ejecutando `dotnet clean && dotnet build`.
6. **Idioma**: Mantener el código en inglés (clases, métodos, variables) pero los textos de la interfaz al usuario final (UI) preferiblemente en Español neutro a menos que haya un sistema de localización.

## Flujo de Trabajo (Workflow)

1. Modificar interfaz (Desktop/CLI) -> Validar -> Construir.
2. Si un proveedor de anime (web) cambia, actualizar ÚNICAMENTE el `Extractor` correspondiente en `AniCS.Core` y probar (selectores documentados en `.agents/scraper_logic.md`).
3. **Actualización de Versiones (Version Bumping)**: Al generar una nueva versión pública (actual: **1.6.0**), actualizar:
   - `src/AniCS.Desktop/AniCS.Desktop.csproj`: `<Version>`, `<AssemblyVersion>` y `<FileVersion>`.
   - `Installer/AniCS-Installer.wxs`: atributo `Version` en el nodo `<Package>`.
   - `src/AniCS.Desktop/AppInfo.cs`: texto del `LatestChangelog` (fallback local). Las notas de la release se toman del `body` de la Release de GitHub.
4. **Publicar una Release (para el AutoUpdate)**: El AutoUpdate lee las **Release de GitHub** (`releases/latest`): versión del tag `v*` + archivo `.msi`. Al empujar un tag `vX.Y.Z` con el workflow `.github/workflows/build-release.yml`, GitHub Actions compila el MSI y crea la release automáticamente. Sin `.msi` en la release, la app avisa de novedad pero no puede instalar.

## Estado de la Migración (Player/Resolver)

El proyecto está migrando del stack clásico (mpv + yt-dlp) a backends nativos .NET. Registro de lo hecho y lo pendiente:

- [x] LibVLC embebido (reproducción sin VLC instalado).
- [x] Resolvedor nativo HLS -> `.ts` (descarga secuencial con progreso).
- [ ] `MpvBackend`: pause/resume/seek (hoy no-op, "Fase 3").
- [ ] Remux HLS -> MP4 (hoy se guarda `.ts`, "Fase 3").
- [ ] Resolución de páginas protegidas por JS sin depender de yt-dlp.

## Tareas a Futuro (Roadmap)

- **AutoUpdate (GitHub)** ✅: `AniCS.Desktop/Services/AppUpdateService.cs` consulta `releases/latest`, descarga el `.msi` y lo instala en silencio (`msiexec /qn`), relanzando la app. Botón en Ajustes + aviso de novedad al inicio. Requiere publicar releases con el workflow CI.
- **Mejoras del Reproductor Nativo**: Corregir pequeños errores (bugs) de reproducción en LibVLC y mejorar la estabilidad de la UI del reproductor interno.
- **Optimización General**: Continuar puliendo rendimiento y eliminando lógica redundante heredada.
- **Soporte Móvil (Android/iOS)**: Como la aplicación ahora usa el motor nativo de descarga en .NET y LibVLC embebido, ya no depende estrictamente de los ejecutables de Windows (mpv.exe y yt-dlp.exe). Esto abre la puerta para desarrollar una versión móvil multiplataforma usando Avalonia UI o .NET MAUI en el futuro.
