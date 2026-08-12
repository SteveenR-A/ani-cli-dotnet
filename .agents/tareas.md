# Tareas (Tasks)

Registro de tareas pendientes, en progreso y completadas del proyecto AniCS.

## Pendientes / Sugerencias para el futuro

- [ ] **Migración Player/Resolver (Fase 3)**: Implementar pause/resume/seek en `MpvBackend` (hoy no-op) y el remux HLS→MP4 (hoy el resolvedor nativo guarda `.ts`).
- [ ] **Resolución sin yt-dlp**: Páginas protegidas por JS/Cloudflare (VidHide, Embedwish, VOE...) hoy dependen de `yt-dlp`.
- [ ] **Soporte multi-idioma (i18n)**: Extraer textos crudos del código `.axaml` y usar archivos de recursos (.resx) o diccionarios dinámicos (Inglés/Español).
- [ ] **Vistas "Paradigm" vacías**: HUD, Node, Kinetic y Spatial son esqueletos sin lógica (ver `.agents/paradigmas.md`).
- [ ] **Parámetro sin uso**: `showQualitySelector` de `ServerPickerDialog` se ignora.
- [ ] **WindowsPlayerService** (`AniCS.Core/Services`): vive porque la CLI (`AppState`) depende de él para reproducir con mpv. Evaluar migrar la CLI a `AniCS.Player` (`IPlayerBackend`) y retirarlo.
- [ ] **tests/**: No hay framework de tests formal. La carpeta `tests/` es un área de experimentos y tiene un `TestMDApp.csproj` con ruta rota (`..\AniCS.Core` apunta a una carpeta inexistente). Considerar xUnit para el Core.
- [ ] **changelog del instalador**: WiX no muestra notas de versión; vienen de `AppInfo.LatestChangelog` y del body de la Release de GitHub.

## Completadas recientemente

- [x] **AutoUpdate (GitHub)**: `AppUpdateService` (Desktop/Services) consulta `releases/latest`, descarga el `.msi` y lo instala en silencio (`msiexec /qn`) relanzando la app. Botón en Ajustes + aviso de novedad al inicio. Workflow CI `.github/workflows/build-release.yml` que compila el MSI y crea la Release desde un tag `v*`.
- [x] **Ajustes rediseñado**: `SettingsView.axaml` ahora agrupa opciones en tarjetas (Reproducción/Descarga, Apariencia, Fuentes, Actualizaciones, Acerca de).
- [x] **Estado vía DI**: `HomeViewModel` y `AppUpdateService` registrados como singletons e inyectados; `MainWindow` recibe el VM por constructor.
- [x] **Changelog centralizado**: Antes se duplicaba en 4 archivos; ahora vive solo en `AppInfo.cs` y en el body de la Release de GitHub.
- [x] **CommunityToolkit.Mvvm**: `RelayCommand`/`ViewModelBase` custom reemplazados por `ObservableObject`/`RelayCommand` de la librería oficial.
- [x] **Eliminar código muerto**: `Class1.cs`, `AnimeAV1Extractor` (registrado pero nunca seleccionado) y `YtDlpService` (duplicaba `YtDlpResolverBackend`). `DesktopPlayer` recortado de 703 → ~130 líneas: la descarga pasó al Resolver y solo conserva el audio de Openings/Trailers y apertura en navegador.
- [x] **Converters rotos**: `EpisodeStatusBrushConverter` y `BadgeColorConverter` ya no lanzan `NotImplementedException`; devuelven `BindingOperations.DoNothing`.
- [x] **Bug `AppSubtextColor`**: recurso inexistente usado en ~30 lugares del UI; se definió en `App.axaml` y en las 6 paletas de `ThemeManager`.
- [x] **Sin `GC.Collect` forzado**: quitado de `DataCache.ClearRamCache()` y `MainWindow.SetMainContent` (bloqueaban el UI thread).
- [x] **Log centralizado**: `AppLogger` (Core) escribe errores en `%LocalAppData%/AniCS/logs` en vez de `crash.txt` en el directorio de trabajo.
- [x] **Historial local de visualización**: `WatchHistory` persiste en `history.json` (anime, episodio, thumbnail); el sistema protege esas imágenes en la limpieza de caché.
- [x] **Nuevos Extractores**: MundoDonghua añadido; JKAnime con soporte Cartelera y Sinopsis.
- [x] **Migración a backends nativos**: `AniCS.Player` (LibVLC embebido + mpv) y `AniCS.Resolver` (resolvedor HLS nativo + yt-dlp), seleccionables en Ajustes (`PlayerBackend`/`ResolverBackend`).
- [x] Ajustar márgenes del menú hamburguesa en `MainWindow.axaml`.
- [x] Añadir y afinar paletas de colores en `ThemeManager.cs` (Dracula, Light, Tokyo Night, Cyberpunk).
- [x] Refactorizar los User-Agents "quemados" a la lista central de `AppConfig.cs` (`RandomUserAgent`).
- [x] Reparar la advertencia XAML en `SettingsView.axaml` (Watermark → PlaceholderText).
- [x] Limpiar archivos `.dll` inexistentes del instalador `.wxs` y forzar WiX `4.0.5` en el script PowerShell.
