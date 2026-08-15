# 🚀 AniCS v1.6.2 — Registro de Cambios

¡Nueva actualización repleta de mejoras en el gestor de descargas, conectividad, reproducción y navegación!

---

### 📁 Auto-Búsqueda y Sincronización de Animes Locales (PC & Móvil)
- **Sincronización online:** Al pulsar *"Ver Online"* en animes importados desde la carpeta de videos sin metadata previa, el sistema busca automáticamente la serie en línea por su título.
- **Portadas HD y Metadata:** Recuperación y vinculación permanente de la portada oficial (HD), sinopsis y lista de episodios en el gestor de descargas.
- **Modo de respaldo local:** Si el anime no está en línea, la vista muestra directamente los episodios descargados para su reproducción sin pantallas en blanco.

---

### 🎛️ Auto-Ocultado Inteligente en Pausa (PC)
- Los controles y el cursor del reproductor ahora se ocultan automáticamente tras 3 segundos de inactividad incluso cuando el video está pausado.
- Reaparición instantánea ante movimiento del ratón, clics en pantalla o cualquier tecla del teclado.

---

### 📂 Blindaje, Migración y Detección de Descargas
- **Ruta oficial blindada:** Garantizada la ruta oficial de descargas en PC en `Videos\AniCS` con reubicación inteligente ante carpetas de OneDrive o rutas movidas.
- **Carga garantizada (`EnsureLoaded`):** Auto-escaneo automático en disco al abrir la pestaña Descargas en PC y Móvil.
- **Migración automática:** Detección e importación automática de historiales legacy (`downloads.json`).

---

### 🎬 Experiencia de Reproducción y Navegación (PC & Móvil)
- **Navegación móvil fluida:** Al pulsar *"Volver"* en el reproductor móvil regresa directamente a la lista de episodios del anime seleccionado sin saltar al Inicio.
- **Indicadores visuales en tiempo real:** Nuevo badge dinámico tanto en PC como en Android para identificar claramente si el video está *'Cargando stream'*, *'Almacenando en búfer'*, *'Reproduciendo'* o *'En Pausa'*, además del avance del búfer de red.
- **Rotación sin avisos:** Eliminación del aviso flotante al rotar el dispositivo en el reproductor móvil, manteniendo la rotación y modo inmersivo 100% fluidos.

---

### 📥 Descargas Continuas, Reanudación Granular y Control de Pausa
- **Continuidad en segundo plano (Android):** Nuevo *Foreground Service* con notificación persistente de progreso y *WakeLock* para evitar cancelaciones al minimizar la app.
- **Reanudación granular:** Checkpointing por segmentos (HLS `.ts` con `.idx`) y por bytes (MP4 con cabeceras `Range`): reanuda exactamente donde se pausó sin empezar desde cero.
- **Control de Pausar / Reanudar funcional:** El botón cambia de forma instantánea a *"▶️ Reanudar"* al pausar sin perder ni borrar los archivos parciales.
- **Transición limpia:** Las descargas completadas se guardan en la biblioteca y se retiran de las descargas activas automáticamente.
- **Reintentos automáticos:** Sistema de hasta 3 intentos con Jitter (1-3s) y renovación automática de enlaces expirados ante cortes de internet.

---

### 📡 Monitorización de Red y Banner Offline en Tiempo Real
- Detección automática del estado de conexión a Internet en PC y Android con `NetworkService`.
- Banner superior de aviso inmediato al perder señal (*"Sin conexión a internet"*) y confirmación visual al restablecerse.

---

### 🔎 Directorio y Búsqueda Avanzada
- Paginación completa del catálogo (`?p=1`, `?p=2`, `?p=3`...) con barra de navegación y **salto manual directo por número de página**.
- Carga dinámica de la lista completa de más de 45 géneros oficiales desde la web.

---

### 🔙 Mejoras previas v1.6.1
- Sincronización de volumen con Windows Core Audio y estabilidad de foco.
- Navegación universal por gestos y botón físico de retroceso en Android.
- Rediseño estético del reproductor móvil con controles circulares translúcidos.
