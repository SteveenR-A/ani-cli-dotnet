# Libros y Referencias (Books/References)

Enlaces de referencia, documentación técnica y bibliotecas usadas para trabajar en este proyecto.

## Lenguaje y Plataforma
- [Documentación Oficial de C#](https://learn.microsoft.com/en-us/dotnet/csharp/)
- [Guía de .NET (SDK 10)](https://learn.microsoft.com/en-us/dotnet/fundamentals/)
- [System.Text.Json (Source Generators)](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)

## Dependencias del Proyecto
- [Avalonia UI (Docs)](https://docs.avaloniaui.net/) — GUI multiplataforma. El proyecto usa **Avalonia 12**.
- [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) — MVVM en Desktop.
- [HtmlAgilityPack](https://html-agility-pack.net/) — Scraping de DOM en `AniCS.Core`.
- [Spectre.Console](https://spectreconsole.net/) — CLI UI en `AniCS.CLI`.
- [LibVLCSharp](https://github.com/videolan/libvlcsharp) — Reproductor embebido (backends `LibVlcBackend`).
- [Microsoft.Extensions.DependencyInjection](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection) — DI en Core/Desktop.

## Herramientas Externas
- [mpv player](https://mpv.io/manual/stable/) — Reproductor externo (fallback). IPC y modos de renderizado.
- [yt-dlp](https://github.com/yt-dlp/yt-dlp) — Resolución de servidores externos y descargas (fallback).
- [WiX Toolset v4](https://wixtoolset.org/documentation/) — Instalador MSI (`Installer/AniCS-Installer.wxs`).

## Últimos episodios / Changelog del proyecto
- Consultar `MainWindow.axaml.cs` (`CheckForUpdates`) y `SettingsView.axaml.cs` (`OnViewChangelogClicked`) para las notas de versión.
