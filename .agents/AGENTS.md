# Normas y Flujo de Trabajo (Rules & Workflow) - AniCS

Este archivo (`AGENTS.md`) define las reglas globales y convenciones del proyecto `AniCS`, el cual es una aplicación para consumir anime a través de web scraping (JkAnime) utilizando un enfoque modular.

## Arquitectura y División de Proyectos (Contexto)
El proyecto principal se divide en tres partes (ubicadas en `/src/`):
- **AniCS.Core**: El motor central. Contiene los modelos (`AppConfig`, `Anime`), interfaces y la lógica pesada de scraping web utilizando `HtmlAgilityPack` (ej. `JkAnimeExtractor.cs`). Gestiona también la configuración del usuario (`ConfigManager`).
- **AniCS.Desktop**: La interfaz gráfica de usuario multiplataforma desarrollada en **Avalonia UI** (C# / XAML). Utiliza una arquitectura orientada a eventos para reproducir, buscar y configurar temas (Dracula, Light, etc.).
- **AniCS.CLI**: Una interfaz en modo consola interactiva para quienes prefieren la terminal.

> **Importante**: Para entender los diferentes "Paradigmas Visuales" (Classic, ASCII, HUD, etc.) revisa el archivo `.agents/paradigmas.md`.

Adicionalmente:
- **/Installer**: Contiene la definición en XML de WiX Toolset v4 (`AniCS-Installer.wxs`) para construir el `.msi` en Windows. Para compilarlo se usa `build-msi.ps1`.

## Reglas de Desarrollo (Norms/Guidelines)
1. **Responsabilidad Separada (SOLID)**: Bajo ninguna circunstancia se debe incluir lógica de scraping o peticiones web dentro de `AniCS.Desktop` o `AniCS.CLI`. Todo eso pertenece a `AniCS.Core/Extractors`.
2. **Dependencias Externas**: AniCS delega la reproducción a reproductores externos. Por defecto, requiere `mpv.exe` para reproducir y `yt-dlp.exe` opcional para ciertos extractores. No asumas que el reproductor está siempre instalado a menos que le indiques al usuario que lo instale (el instalador MSI no trae el peso completo de .NET por recortes de publicación).
3. **Manejo de HTTP y User-Agents**: No "quemes" (hardcode) las cabeceras HTTP directamente en las peticiones de los Extractors. Utiliza siempre la configuración de `AppConfig` gestionada por `ConfigManager.Current.RandomUserAgent`.
4. **Avalonia UI**: Al trabajar con `.axaml` en `AniCS.Desktop`, recuerda que algunos editores presentan falsos positivos (como "el nombre no existe en el contexto"). Se soluciona ejecutando `dotnet clean && dotnet build`.
5. **Idioma**: Mantener el código en inglés (clases, métodos, variables) pero los textos de la interfaz al usuario final (UI) preferiblemente en Español neutro a menos que haya un sistema de localización.

## Flujo de Trabajo (Workflow)
1. Modificar interfaz (Desktop/CLI) -> Validar -> Construir.
2. Si un proveedor de anime (web) cambia, actualizar ÚNICAMENTE el `Extractor` correspondiente en `AniCS.Core` y probar.
3. **Actualización de Versiones (Version Bumping)**: Al generar una nueva versión pública (ej. `1.5.1`), se deben actualizar las versiones y notas de parche en las siguientes ubicaciones obligatorias:
   - `src/AniCS.Desktop/AniCS.Desktop.csproj`: Modificar `<Version>`, `<AssemblyVersion>` y `<FileVersion>`.
   - `Installer/AniCS-Installer.wxs`: Modificar el atributo `Version` en el nodo `<Package>`.
   - `src/AniCS.Desktop/MainWindow.axaml.cs`: Actualizar el texto del `changelog` dentro de `CheckForUpdates()` para notificar al usuario al iniciar tras actualizar.
   - `src/AniCS.Desktop/Views/SettingsView.axaml.cs`: Actualizar el texto del `changelog` en `OnViewChangelogClicked()` para coincidir con la ventana de Ajustes.
4. Para generar el instalador final, correr `build-msi.ps1`.
