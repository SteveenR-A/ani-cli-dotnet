# AniCS (Anime C#) — Cliente Multiplataforma

AniCS es una aplicación multiplataforma para buscar, reproducir y descargar anime. Todo construido en C# (.NET 10).

El proyecto se divide en dos interfaces:
- **AniCS Desktop**: Una interfaz gráfica (GUI) moderna y estética construida con Avalonia UI.
- **AniCS CLI**: Una interfaz de línea de comandos rápida y elegante impulsada por Spectre.Console.

Ambas versiones comparten el mismo núcleo de extracción y sincronizan tu historial de visualización.

---

## 🚀 Instalación (Windows & Linux)

### Versión de Escritorio (Desktop)
Para usuarios de Windows que deseen la interfaz gráfica, el proyecto incluye un automatizador para generar el instalador MSI.

1. Clona el repositorio:
   ```powershell
   git clone https://github.com/SteveenR-A/ani-cli-dotnet.git
   cd ani-cli-dotnet
   ```
2. Ejecuta el script creador de instalador:
   ```powershell
   .\build-msi.ps1
   ```
3. El archivo `AniCS-Installer.msi` se generará en la carpeta `Installer\`. Simplemente ejecútalo para instalar la aplicación en tu sistema.

### Versión de Consola (CLI)
Los scripts de instalación en bash/powershell instalan automáticamente la versión de consola en tu sistema de manera global.

**En Linux:** (Soporta Arch, Ubuntu, Debian, Fedora, etc.)
```bash
git clone https://github.com/SteveenR-A/ani-cli-dotnet.git
cd ani-cli-dotnet
./install.sh
```

**En Windows:**
```powershell
git clone https://github.com/SteveenR-A/ani-cli-dotnet.git
cd ani-cli-dotnet
.\install.ps1
```
*Ambos scripts muestran un menú interactivo para instalar, actualizar o desinstalar la CLI, y añaden el comando `anics` a tu PATH global.*

---

## 💻 Comandos del REPL (Modo CLI)

| Comando          | Alias | Descripción                                   |
|------------------|-------|-----------------------------------------------|
| `search <título>`| `s`   | Busca un anime en la fuente activa            |
| `latest`         | `l`   | Muestra los últimos episodios estrenados      |
| `scoop`          | `sc`  | Cartelera semanal de estrenos (Listado rápido)|
| `history`        | `h`   | Historial de animes que has visto             |
| `source <nombre>`| —     | Cambia la fuente activa                       |
| `clear`          | `cls` | Limpia la pantalla                            |
| `exit`           | `q`   | Salir de la aplicación                        |

---

## 📡 Fuentes y Servidores

**Fuentes de Anime:**
- `jkanime.net` (Principal ✅, Soporte Cartelera ✅, Extracción Sinopsis ✅)
- `animeav1.com` (Secundaria)

**Servidores Soportados (Streaming y Descarga):**
| Servidor                 | Streaming Directo (mpv) | Descarga (yt-dlp) |
|--------------------------|-------------------------|-------------------|
| **Desu / Magi** (Nativo) | ✅ Sí                    | ✅ Sí              |
| **Mediafire** (Nativo)   | ✅ Sí                    | ✅ Sí              |
| **Mp4upload / Streamtape** | ✅ Sí (vía yt-dlp)       | ✅ Sí              |
| **Mega**                 | ❌ No (cifrado JS)       | 🟡 Enlace Directo  |
| **VOE / Filemoon**       | ❌ Protegido por CF      | ❌ Protegido       |

---

## 🛠️ Requisitos del Sistema

- **.NET 10 SDK**: Requerido para compilar el código.
- **mpv**: Obligatorio para reproducir video nativamente. (En Windows es compatible con `mpv.net`).
- **yt-dlp**: Obligatorio para resolver servidores externos y descargar capítulos.
- **Kitty / Ghostty** (Opcional): Para renderizar imágenes de portadas nativamente en la terminal de Linux.

### Dependencias de Paquetes
- `HtmlAgilityPack` (Scraping de DOM)
- `Spectre.Console` (CLI UI)
- `Avalonia UI` (Desktop UI)

---

## 🏗️ Arquitectura del Código

El proyecto sigue una arquitectura modular separando la lógica de las interfaces:

```text
ani-cli-dotnet/
├── src/
│   ├── AniCS.Core/           # Núcleo: Extractores (JKAnime, etc.), Modelos, Historial interactivo, DataCache.
│   ├── AniCS.CLI/            # Interfaz CLI: Comandos, Prompt de Detalles, Renderizado (KittyGraphics).
│   └── AniCS.Desktop/        # Interfaz Gráfica: Ventanas (Avalonia), Descargador (DownloadManager).
├── Installer/                # Archivos de configuración WiX (v4) para el instalador.
├── build-msi.ps1             # Script (Windows) para compilar la GUI y generar el instalador MSI.
├── install.ps1               # Script (Windows) para instalar/actualizar la versión CLI en el sistema.
└── install.sh                # Script (Linux) para instalar/actualizar la versión CLI en el sistema.
```

---

## 🛡️ Seguridad y Características Adicionales

- **Sin navegadores Headless:** Todo funciona mediante peticiones HTTP estáticas rápidas.
- **Rotación de User-Agent:** Prevención de bloqueos usando cabeceras de navegadores reales.
- **Caché en Memoria:** Las sinopsis y portadas se guardan en memoria para evitar saturar el servidor y acelerar la navegación.
- **Historial Binge-Watching:** Al seleccionar el historial, el sistema resalta el último episodio visto y te permite reproducir directamente el siguiente en orden cronológico.
