# 🚀 AniCS v1.6.6 — Registro de Cambios

¡Nueva actualización v1.6.6 con cola organizada de descargas simultáneas (FIFO), navegación fluida de capítulos en el reproductor (⏮ / ⏭), acceso directo al anime desde descargas en Android, notificaciones de descarga mejoradas, carga instantánea y refactorización Clean Code integral!

---

### 📥 Control de Descargas Simultáneas y Cola Ordenada (FIFO)
- **Descarga organizada y progresiva:** Nuevo sistema de cola FIFO para descargas múltiples. Al agregar varios episodios, solo se descargan simultáneamente los permitidos por la configuración sin saturar el ancho de banda ni el disco.
- **Límite configurable en Ajustes:** Selector numérico de descargas simultáneas en la sección *Descargas y Almacenamiento* (de 1 a 10 en PC y de 1 a 5 en Android).
- **Auto-avance reactivo:** Al completar, pausar o cancelar un episodio, el siguiente capítulo en cola comienza automáticamente al instante.

---

### ⏮️ Navegación de Capítulos en el Reproductor (Streaming & Descargas)
- **Botones de Episodio Anterior y Siguiente:** Nuevos botones visuales dedicados **`⏮`** y **`⏭`** integrados en los controles del reproductor en PC y Android.
- **Atajos de teclado en PC:** Tecla **`P`** para retroceder al capítulo anterior y tecla **`N`** para avanzar al siguiente.
- **Navegación secuencial exacta:** Algoritmo de cambio directo sin cerrar la ventana del reproductor, respetando el orden numérico estricto tanto en transmisiones online como en archivos locales descargados.

---

### 🎬 Acceso Directo al Anime desde Descargas (Android)
- **Ficha del anime con un toque:** Al pulsar sobre el título o cabecera de cualquier anime en la lista de Descargas, la app abre directamente su vista de detalles completa (`MobileAnimeDetailsView`) para explorar información o continuar descargando más episodios.

---

### ⚡ Carga Instantánea y Asíncrona en Descargas (PC & Android)
- **Eliminación de bloqueos ("No responde"):** La sección de Descargas ahora se abre de manera instantánea cargando los datos desde memoria. El escaneo de archivos huérfanos se ejecuta en segundo plano (`Task.Run`) sin congelar la interfaz.

---

### 🔔 Notificaciones Mejoradas en Android
- **Progreso en tiempo real:** Barra de progreso y estado detallado en la notificación persistente durante las descargas.
- **Notificación de finalización:** Aviso al completarse todos los capítulos en cola para que el usuario sepa que su contenido está listo sin que la notificación desaparezca sin feedback.

---

### 🛠️ Refactorización de Código, Estabilidad y Memoria
- **Manejo estructurado de errores:** Reemplazo de bloques silenciosos por registro estructurado en `AppLogger` en todos los proyectos de la solución.
- **Limpieza de código muerto e imports:** Eliminación de dependencias y variables no utilizadas para un rendimiento óptimo.
- **Optimización de reproductores:** Eliminación de fugas de eventos en LibVLC, soporte IPC completo para MPV y reanudación limpia en el reproductor nativo de Android.

---

### 🎨 Mejoras Previas v1.6.5
- **Sincronización inteligente de pantalla activa (Android):** Mantiene la pantalla encendida solo durante la reproducción y suspende en pausa.
- **Scroll fluido en Descargas (PC & Móvil):** Cambio reactivo de estados sin reinicios de scroll.
- **Scroll táctil perfeccionado en Top Animes y Ver Más (Android):** Eliminación de clics accidentales al deslizar.
- **Sincronización en tiempo real del reproductor:** Evento de estado sincronizado con controles OSD.
- **Descarga continua y reanudación del actualizador (Android):** Descarga con WakeLock y soporte de HTTP Range.

---

¡Gracias por disfrutar de AniCS!
