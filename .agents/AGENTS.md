# Normas y Flujo de Trabajo — AniCS

AniCS: aplicación C#/.NET 10 multiplataforma (Desktop/CLI/Android) para buscar, reproducir y descargar anime vía web scraping (JKAnime, MundoDonghua).

## Arquitectura (`AniCS.slnx`, 6 proyectos en `src/`)

- **AniCS.Core** — Motor central. Único lugar permitido para scraping/HTTP a fuentes: `Extractors/` (`BaseExtractor`, `JKAnimeExtractor`, `MundoDonghuaExtractor`, `ExtractorFactory`). También modelos, `ConfigManager`, `DataCache`, `WatchHistory`.
- **AniCS.Desktop** — GUI **Avalonia 12**, MVVM (CommunityToolkit). DI raíz en `App.axaml.cs`. Referenciado también por AniCS.Android.
- **AniCS.CLI** — Consola interactiva Spectre.Console (`PublishAot=true`). Entrada: `Program.cs`.
- **AniCS.Player** — `IPlayerBackend`: LibVLC embebido (`LibVlcBackend`) + `MpvBackend` fallback. Selección vía `PlayerFactory` según `AppConfig.PlayerBackend`.
- **AniCS.Resolver** — `IResolverBackend`: resolvedor nativo HLS (`Native/`) + `YtDlpResolverBackend` fallback. Selección vía `ResolverFactory` según `AppConfig.ResolverBackend`.
- **AniCS.Android** — Avalonia/Android; comparte vistas/viewmodels con Desktop.

Docs de soporte en `.agents/`: `paradigmas.md` (paradigmas visuales), `scraper_logic.md` (HTTP/selectores por fuente), `estructura.md` (árbol), `tareas.md`.

## Comandos

- Construir un proyecto concreto, p. ej. `dotnet build src/AniCS.Desktop/AniCS.Desktop.csproj`. **No** construir toda la solución salvo que esté instalado el workload `android` (AniCS.Android requiere Android SDK).
- Ejecutar CLI: `dotnet run --project src/AniCS.CLI`.
- Smoke test de extractores: `dotnet run --project TestScraper` (proyecto de consola gitignored; ajusta la URL de prueba en `Program.cs`).
- MSI Windows: `.\build-msi.ps1`. Requiere WiX v4 (`dotnet tool install --global wix --version 4.*` y `wix extension add -g WixToolset.UI.wixext/4.0.5`) y también compila el APK. Empaqueta mpv/yt-dlp en el instalador solo si existen en `InstallerDependencies/` (gitignored).
- APK: `dotnet publish src/AniCS.Android -c Release -f net10.0-android` (requiere workload `android`).
- Falsos positivos de Avalonia en `.axaml` ("el nombre no existe en el contexto"): `dotnet clean && dotnet build`.

## Reglas de Desarrollo

1. Scraping/peticiones HTTP a fuentes SOLO en `AniCS.Core/Extractors`. Nunca en Desktop/CLI/Player/Resolver.
2. Player y Resolver solo manejan URLs; no conocen las fuentes (JKAnime/MundoDonghua). Matices por servidor (ej. Referer/Origin de `redirector.php` de MundoDonghua) viven en los backends de Resolver o se resuelven antes en Core.
3. No hardcodear User-Agent en los extractores: usar `ConfigManager.Current.RandomUserAgent`.
4. `mpv`/`yt-dlp` son fallbacks opcionales; comprobar disponibilidad antes de usarlos, nunca asumir que están instalados.
5. Código en inglés (clases/métodos/variables); textos de UI al usuario final en español neutro.

## Gotchas

- `ConfigManager.BaseDataPath` debe fijarse en el entry-point de la plataforma antes de usar cualquier código de AniCS (Android: `MainActivity.OnCreate()`).
- AniCS.Android referencia `AniCS.Desktop` (que es `WinExe`) como librería vía `AdditionalProperties="OutputType=Library"` + `ValidateExecutableReferences=false`. No "arreglar" ese setup.
- En Android el video se renderiza con **ExoPlayer vía `NativeControlHost`**; LibVLCSharp.Avalonia no tiene VideoView para Android.
- `MpvBackend` aún sin pause/resume/seek (no-op); el resolvedor nativo descarga HLS a `.ts` (sin remux a MP4).
- **Version bump** (actual: **1.6.3**) — al publicar nueva versión actualizar los 4 sitios: `src/AniCS.Desktop/AniCS.Desktop.csproj` (`Version`/`AssemblyVersion`/`FileVersion`), `src/AniCS.Android/AniCS.Android.csproj` (`ApplicationDisplayVersion`/`Version`/`ApplicationVersion`), `Installer/AniCS-Installer.wxs` (`<Package Version>`), `src/AniCS.Desktop/AppInfo.cs` (`LatestChangelog`).
- **Release/AutoUpdate**: al empujar un tag `vX.Y.Z`, `.github/workflows/build-release.yml` compila MSI+APK y crea la Release. El AutoUpdate lee `releases/latest` y requiere un `.msi` adjunto para poder instalar (sin él solo avisa de novedad).