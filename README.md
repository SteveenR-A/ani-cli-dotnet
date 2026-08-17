# AniCS (Anime C#) — by YumeWorks

> *Siente la fluidez hacia tus historias favoritas.*

AniCS es una aplicación para buscar, reproducir y descargar anime. Todo construido en C# (.NET 10).

Tres interfaces que comparten el mismo núcleo de extracción y sincronizan el historial:
- **AniCS Desktop** — GUI moderna con Avalonia UI (Windows, Linux y Android).
- **AniCS CLI** — Consola interactiva con Spectre.Console.
- **AniCS Android** — Aplicación móvil con Avalonia + ExoPlayer.

---

## 🚀 Instalación

### Windows — Desktop (MSI)

```powershell
git clone https://github.com/SteveenR-A/ani-cli-dotnet.git
cd ani-cli-dotnet
.\build-msi.ps1
```

El instalador `AniCS-Installer.msi` se genera en `Installer\`.

### Linux — Desktop

```bash
git clone https://github.com/SteveenR-A/ani-cli-dotnet.git
cd ani-cli-dotnet
./install-desktop.sh
```

### CLI (Linux o Windows)

```bash
./install.sh        # Linux
.\install.ps1       # Windows
```

Los scripts instaladores muestran un menú interactivo y añaden el comando `anics` al PATH global.

---

## 💻 Comandos del REPL (CLI)

| Comando           | Alias | Descripción                              |
|-------------------|-------|------------------------------------------|
| `search <título>` | `s`   | Busca un anime                           |
| `latest`          | `l`   | Últimos episodios estrenados             |
| `scoop`           | `sc`  | Cartelera semanal de estrenos            |
| `history`         | `h`   | Historial de animes vistos               |
| `source <nombre>` | —     | Cambia la fuente activa                  |
| `clear`           | `cls` | Limpia la pantalla                       |
| `exit`            | `q`   | Salir                                    |

---

## 🛠️ Requisitos

- **.NET 10 SDK** para compilar.
- **mpv** y **yt-dlp**: obligatorios para reproducción y resolución/descarga de capítulos.

> [!WARNING]
> En Windows debes instalar `mpv` y `yt-dlp` manualmente (p. ej. `scoop install mpv yt-dlp`) y asegurarte de que estén en el PATH. Los instaladores de Linux lo hacen automáticamente.

---

## 🏗️ Arquitectura

Proyectos en `src/`:

- **AniCS.Core** — Núcleo: extractores, modelos, configuración, caché e historial.
- **AniCS.Desktop** — GUI Avalonia (MVVM).
- **AniCS.CLI** — Consola interactiva.
- **AniCS.Player** — Backends de reproducción (LibVLC, fallback mpv).
- **AniCS.Resolver** — Resolvedor nativo de enlaces (fallback yt-dlp).
- **AniCS.Android** — App móvil, comparte vistas con Desktop.

---

## 🛡️ Notas

- AniCS accede a sitios de terceros mediante **web scraping** (sin navegadores headless). Estos sitios pueden cambiar o dejar de estar disponibles sin previo aviso.
- El software **no aloja contenido**: únicamente facilita el acceso a enlaces públicos. El usuario es responsable del uso que haga conforme a las leyes de su país.
- Caché en memoria para sinopsis y portadas.
- Historial con binge-watching: resalta el último episodio visto y permite reproducir el siguiente.
- Distribuido bajo **licencia MIT** (ver `LICENSE`).