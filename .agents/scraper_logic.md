# Lógica de Scraping por Fuente (Scraper Logic)

Documentación de las peticiones HTTP y selectores HTML usados por los extractores de `AniCS.Core/Extractors`. Útil cuando una fuente cambia su HTML (regla: actualizar SOLO el `Extractor` correspondiente).

- `JKAnimeExtractor` → fuente principal (anime).
- `MundoDonghuaExtractor` → fuente de donghua.
- `AnimeAV1Extractor` → registrado en DI pero **nunca seleccionado** por `ExtractorFactory` (legacy).

---

# 1. JKAnime (`jkanime.net`)

- **Base URL**: `https://jkanime.net` (configurable vía `AppConfig.CustomJkAnimeBaseUrl`).
- Todos los selectores verificados contra el HTML real del sitio.

## 1.1 Búsqueda (Search)
- **URL**: `GET /buscar/{query}/` (query URL-encoded).
- **Items**: `//div[@class='anime__item']`.
- **Por item**:
  - Título + enlace: `div.anime__item__text > h5 > a` (`InnerText` y `href`).
  - Portada: `div[contains(@class,'anime__item__pic')]` → atributo `data-setbg`.

## 1.2 Búsqueda Avanzada (Directorio)
- **URL**: `GET /directorio?filtro=&genero=&letra=&demografia=&categoria=&tipo=&estado=&fecha=&temporada=&orden=`.
- Los resultados vienen en JSON embebido: regex `var\s+animes\s*=\s*(\{.*?\});` → propiedad `data` → items con `title`, `slug`, `image`.
- URL del anime: `{BaseUrl}/{slug}/`.

## 1.3 Últimos Episodios (Home / Estrenos)
- **URL**: `GET /` (portada) y `GET /estrenos/`.
- **Cards**: `//div[contains(@class,'dir1')]//div[contains(@class,'ml-2') and contains(@class,'card')]//a`.
- **Por card**:
  - Título: `.//h5[contains(@class,'card-title')]`.
  - Portada: `.//img[contains(@class,'card-img-top')]` → `src` o `data-animepic`.
  - Nº episodio: `.//span[contains(@class,'badge-primary')]` (texto `Ep N`).

## 1.4 Top Animes
- **URL**: `GET /top/` (paginación: `/top/page/{n}/`).
- **Cards**: `//div[contains(@class,'card') and .//div[contains(@class,'ranking')]]`.
- **Por card**: `.//h5[contains(@class,'card-title')]`, `.//img[contains(@class,'card-img-top')]`, `.//div[contains(@class,'card-badge')]` (votos).

## 1.5 Cartelera Semanal (Scoop)
- **URL**: `GET /horario/`.
- **Días**: `//div[contains(@class,'semana')]` → día en `.//h2`, episodios en `.//div[@class='boxx']//a` (deduplicar por `href`).

## 1.6 Episodios (AJAX con sesión + CSRF) ⚠️ Importante
1. `GET` a la página de la serie guardando las cookies `Set-Cookie`.
2. Extraer del HTML:
   - CSRF: `<meta name="csrf-token" content="...">`.
   - ID: regex `ajax/episodes/(\d+)/`.
3. `POST /ajax/episodes/{id}/1` con:
   - Headers: `Referer` (página serie), `X-Requested-With: XMLHttpRequest`, `Cookie` (sesión), `Accept: application/json`.
   - Body: `_token={csrf}`.
   - Reintentos: 3 con backoff 1s/2s.
4. Respuesta JSON: `{ data: [...], total }` → construir `N` episodios `{BaseUrl}/{slug}/{i}/`.

## 1.7 Detalles
- Título: `//div[contains(@class,'anime_info')]//h3` | `anime__details__title` (h3/h1) | `//h1`.
- Portada: `anime__details__pic` o `anime_pic//img` (`data-setbg`/`src`) | fallback `meta[property=og:image]`.
- Sinopsis: `p.scroll` | `p[rel=sinopsis]` | `p[itemprop=description]` | `div.sinopsis-box p`.
- Metadatos: `//li[span]` con labels (Tipo, Genero, Estado, Estudio, Temporada, Demografia, Episodios, Duracion, Emitido).
- Trailer/Opening: `//div[@data-yt]` → `data-yt` → `https://www.youtube-nocookie.com/embed/{id}`.

## 1.8 Servidores de Video
- Servidores nativos: regex `video\[(\d+)\]\s*=\s*'([^']+)'` (HTML de iframe) + `var\s+servers\s*=\s*(\[...\]);` (JSON con campo `remote` en Base64).
- Nombres: `data-id="(\d+)"` en enlaces.
- **Reproducción directa soportada**: Desu, Magi, Mediafire.
- Fallback: `iframe[class=player_conte]`.

## 1.9 Resolución de URL de Video
- Servidores internos (`/jkplayer`, `um.php`, `um2.php`, `jk.php`, `desu.php`): buscar `.m3u8` y luego `.mp4` en el HTML del player.
- **Mediafire**: 4 estrategias en cascada:
  1. JSON embebido (`normal_download`/`download_url`/`downloadUrl`).
  2. `<a aria-label="Download file" href="...">`.
  3. `id="downloadButton"`.
  4. Regex amplio de `download*.mediafire.com`.
- Otros servidores externos (Mp4upload, Streamtape...): devolver vacío → lo resuelve yt-dlp.

---

# 2. MundoDonghua (`www.mundodonghua.com`)

## 2.1 Headers Obligatorios
Todas las peticiones **DEBEN** incluir (si no, 403 Forbidden):
```json
{
  "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
  "Referer": "https://www.mundodonghua.com"
}
```

## 2.2 Búsqueda de Donghuas
- **Por texto**: `GET /busquedas?donghua={query}` *(param por query string, NO `/busquedas/{query}`)*.
- **Por género**: `GET /genero/{genre}`.
- **Lista completa**: `GET /lista-donghuas`.
- **Elementos (Tarjetas)**: `//div[contains(@class, 'md-card')]`.
- **Por tarjeta**:
  - Enlace: `.//a` → `href` (prefijar base si es relativo).
  - Imagen: `.//img` → `src` (prefijar base).
  - Título: `.//*[contains(@class, 'md-card-title')]`.
  - Tipo: `.//*[contains(@class, 'md-card-badge')]`.
  - **Filtro**: solo incluir tarjetas cuyo enlace contenga `/donghua/`.

## 2.3 Últimos Episodios (Home)
- **URL**: `GET https://www.mundodonghua.com`.
- **Contenedor**: `<div id="nuevos-episodios-grid">`.
- **Tarjetas**: `.//div[contains(@class, 'md-card')]` dentro del contenedor.
- Título desde `h3.md-card-title`.

## 2.4 Detalles del Donghua
- Portada: `meta[property=og:image]`. Título: `//h1`.
- Sinopsis: `p.md-detail-synopsis` | `div.sinopsis` | `p.description`.
- Géneros: `a.md-genre-tag` | `div.md-genres-block a`.
- Info: `p.md-info-item` | `div.info p` | `li` con prefijos `Tipo:`, `Estudio(s):`, `Estado:`/`Emisión:`, `Episodios:`.

## 2.5 Lista de Episodios
- **URL**: `GET {link}` (enlace `/donghua/...`).
- **Enlaces**: `//a[contains(@href, '/ver/')]`.
- Deduplicar con `HashSet` por `href`; numerar en orden ascendente.

## 2.6 Servidores de Video
- El HTML de la página de episodio contiene scripts `eval(function(p,a,c,k,e,d)...)` **empaquetados (packer JS)**. Se desempaquetan y se busca:
  - `<iframe ... src="...">` → servidor externo.
  - `file: "..."` → stream HLS directo (se marca como "MundoDonghua HLS").
- Nombres de servidor por URL (`GetServerNameFromUrl`): Voe, Fembed/Fmoon, VGEmbed, OkRu, Mp4Upload, YourUpload, DoodStream, VidHide, Embedwish.
- ⚠️ VidHide y Embedwish están protegidos por Cloudflare → **ya NO son de reproducción directa**; se dejan para yt-dlp.

## 2.7 Resolución de URL de Video
1. Resolver `redirector.php` (seguir redirección con `Referer`/`Origin: https://www.mundodonghua.com`; si devuelve HTML 200, buscar `.m3u8`/`.mp4`, `file:`/`src:`, o `iframe`).
2. Si ya es `.m3u8`/`.mp4` directo → devolver.
3. Si es iframe de VidHide/Embedwish/Streamwish/etc. → devolver el iframe (para yt-dlp).
4. Descargar la página embed, desempaquetar `eval` y extraer `.m3u8`/`.mp4` o `file:`.
5. Fallback: devolver la URL tal cual (yt-dlp la procesa).

---

# Notas generales

- **Anti-bloqueo**: `BaseExtractor` añade `JitterAsync` (800–2200ms), rotación de User-Agent (`ConfigManager.Current.RandomUserAgent`) y caché DOM en memoria (TTL 30s, se vacía >50 entradas).
- Si una fuente cambia de HTML, actualizar el `Extractor` correspondiente y verificar contra HTML real (no asumir selectores).
