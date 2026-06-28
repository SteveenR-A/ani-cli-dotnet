# AniCS — Manual de Uso

## Instalación rápida

```bash
git clone https://github.com/SteveenR-A/ani-cli-dotnet.git
cd ani-cli-dotnet
dotnet run
```

### Compilar ejecutable nativo (recomendado para uso diario)

```bash
dotnet publish -c Release -r linux-x64
# Ejecutable en: bin/Release/net10.0/linux-x64/publish/AniCS
```

Moverlo a tu PATH:

```bash
sudo cp bin/Release/net10.0/linux-x64/publish/AniCS /usr/local/bin/anics
anics  # Ejecutar desde cualquier lugar
```

---

## Comandos del REPL

| Comando          | Alias | Descripción                                   |
|------------------|-------|-----------------------------------------------|
| `search <título>` | `s`  | Busca un anime en la fuente activa            |
| `latest`         | `l`   | Muestra los últimos episodios estrenados      |
| `scoop`          | `sc`  | Cartelera semanal de estrenos (Modo Scoop)    |
| `history`        | `h`   | Historial de animes que has visto             |
| `source <nombre>` | —   | Cambia la fuente activa                       |
| `clear`          | `cls` | Limpia la pantalla                            |
| `exit`           | `q`   | Salir de la aplicación                        |

---

## Fuentes disponibles

| Fuente   | Dominio         | Prioridad     | Scoop |
|----------|-----------------|---------------|-------|
| JKAnime  | `jkanime.net`   | Principal ✅  | ✅    |
| AnimeAV1 | `animeav1.com`  | Secundaria    | ❌    |

Cambiar fuente en caliente:
```
anics > source animeav1
anics > source jkanime
```

---

## Integración con Kitty Terminal

Al seleccionar un anime, AniCS descarga el póster y lo renderiza directamente
en la terminal usando el Kitty Graphics Protocol:

    ESC_G f=100,a=T,m=0;<base64> ESC\

No requiere configuración adicional — funciona automáticamente en Kitty.

---

## Historial Interactivo y Maratones

El historial (`h`) es completamente interactivo. Al seleccionar un anime:
- Descarga la lista de episodios y resalta el último episodio visto con `[yellow](Último visto)[/]`.
- Al terminar un episodio en `mpv`, la aplicación te permite continuar al **Siguiente Episodio** automáticamente (binge-watching), calculando cronológicamente el orden correcto.

El historial se guarda en: `~/.config/anics/history.json`

---

## Servidores Soportados y Descargas

AniCS incluye un menú que permite elegir si quieres **Reproducir** o **Descargar** el episodio, con soporte integrado de `yt-dlp` para descargas.

| Servidor                 | Streaming Directo (mpv) | Descarga (yt-dlp) |
|--------------------------|-------------------------|-------------------|
| **Desu / Magi** (Nativo) | ✅ Sí                    | ✅ Sí              |
| **Mediafire** (Nativo)   | ✅ Sí                    | ✅ Sí              |
| **Mp4upload / Streamtape** | ✅ Sí (vía yt-dlp)       | ✅ Sí              |
| **Mega**                 | ❌ No (cifrado JS)       | 🟡 Enlace Directo  |
| **VOE / Filemoon**       | ❌ Protegido por CF      | ❌ Protegido       |

> Para descargas, el sistema preguntará por una ruta, sugiriendo `~/Descargas/AniCS/` por defecto.

---

## Requisitos

- .NET 10 SDK
- mpv (Obligatorio para la mejor experiencia)
- yt-dlp (Obligatorio para resolver servidores externos y descargar)
- Kitty Terminal (opcional, para imágenes de portada)

---

## Arquitectura

```
AniCS/
├── Extractors/
│   ├── IAnimeExtractor.cs      # Interfaz del patrón extractor
│   ├── BaseExtractor.cs        # Clase base (yt-dlp style): HTTP, retry, UA rotation
│   ├── JKAnimeExtractor.cs     # jkanime.net
│   └── AnimeAV1Extractor.cs    # animeav1.com
├── History/
│   └── WatchHistory.cs         # Historial local JSON
├── Models/                     # AnimeResult, Episode, ScheduleItem
├── Terminal/
│   ├── KittyGraphics.cs        # Kitty Graphics Protocol
│   └── PlayerManager.cs        # Lanzador mpv/vlc
└── Program.cs                  # REPL principal
```

### Añadir nueva fuente

```csharp
public class MiFuenteExtractor : BaseExtractor
{
    public override string Domain => "mifuente.com";
    // implementar métodos abstractos...
}

// En Program.cs, añadir a _extractors:
new MiFuenteExtractor(_http),
```

---

## Seguridad

- Sin Selenium ni navegadores headless — solo HTTP estático
- User-Agent rotado automáticamente entre 4 navegadores reales
- Peticiones a velocidad humana — sin riesgo de bloqueo de IP
- Sin almacenamiento de credenciales
