# MundoDonghua Scraper Logic

Esta documentación describe las peticiones HTTP y los selectores HTML necesarios para extraer la información de MundoDonghua. 

## 1. Headers Obligatorios
Para evitar ser bloqueado (Error 403 Forbidden), todas las peticiones **DEBEN** incluir los siguientes headers:
```json
{
  "User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
  "Referer": "https://www.mundodonghua.com"
}
```

---

## 2. Últimos Episodios (Home)
- **URL**: `GET https://www.mundodonghua.com`
- **Contenedor Principal**: `<div id="nuevos-episodios-grid">`
- **Elementos (Tarjetas)**: `<div class="md-card">` (dentro del contenedor principal)

### Extracción por Tarjeta:
- **Enlace del episodio (link)**: Buscar la etiqueta `<a>`, obtener el atributo `href` y añadir el dominio base.
- **Imagen (image)**: Buscar la etiqueta `<img>`, obtener el atributo `src` y añadir el dominio base.
- **Título (title)**: Buscar la etiqueta `<h3 class="md-card-title">` y obtener el texto (innerText).

---

## 3. Búsqueda de Donghuas
- **URL**: `GET https://www.mundodonghua.com/busquedas/{query}` 
  *(Nota: El parámetro `{query}` debe estar codificado en formato URL (URL-encoded), por ejemplo "The%20Girl%20Downstairs")*
- **Elementos (Tarjetas)**: `<div class="md-card">` (en toda la página)

### Extracción por Tarjeta:
- **Enlace (link)**: Atributo `href` de la etiqueta `<a>`.
- **Imagen (image)**: Atributo `src` de la etiqueta `<img>`.
- **Título (title)**: Texto de `<h3 class="md-card-title">`, `<h4 class="md-card-title">` o `<h5 class="md-card-title">`.
- **Condición de filtrado**: Sólo incluir tarjetas cuyo enlace contenga `/donghua/` (esto evita falsos positivos en búsquedas que devuelvan otros tipos de elementos).

---

## 4. Detalles del Donghua (Episodios)
- **URL**: `GET {link}` *(El enlace obtenido en la búsqueda, ej: `https://www.mundodonghua.com/donghua/...`)*
- **Elementos (Enlaces de Episodios)**: `<a href="...">` donde el atributo `href` contenga el texto `/ver/`.

### Extracción de Lista de Episodios:
1. Obtener todas las etiquetas `<a>` que cumplan la condición.
2. Iterar sobre las etiquetas.
3. Extraer el texto (innerText) que será el **Título del Episodio**.
4. Extraer el atributo `href`. Si no empieza por `http`, concatenarle el dominio base.
5. **Filtrar duplicados**: La página puede tener el mismo enlace repetido para diferentes botones del mismo episodio. Se debe usar un Set o HashSet para guardar únicamente enlaces únicos.
