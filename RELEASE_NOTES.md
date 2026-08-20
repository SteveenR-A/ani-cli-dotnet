# 🚀 AniCS v1.6.5 — Registro de Cambios

¡Nueva actualización v1.6.5 con sincronización inteligente de pantalla activa en Android, corrección de gestos y desplazamiento táctil en Top Animes y Ver Más, y sincronización en tiempo real del reproductor!

---

### 💡 Sincronización Inteligente de Pantalla Activa (Android)
- **Pantalla activa por reproducción:** La pantalla se mantiene encendida de forma automática exclusivamente mientras el video se encuentra en reproducción activa (`IsPlaying == true`).
- **Atenuación y suspensión en pausa:** Al pausar el video, el sistema libera la bandera de pantalla activa permitiendo que el móvil se atenúe y apague según el tiempo de inactividad configurado por el usuario.
- **Gestión del ciclo de vida (`OnPause`/`OnStop`):** Al minimizar la aplicación o bloquear la pantalla, la reproducción se pausa automáticamente y se liberan de inmediato las banderas de pantalla activa.

---

### 📥 Corrección de Scroll y Estado en Descargas (PC & Móvil)
- **Scroll fluido y sin reinicios:** Cambiar manualmente el estado de progreso de un episodio (Sin ver / En progreso / Terminado) ya no recarga la vista completa ni envía la barra de desplazamiento a la parte superior.
- **Actualización reactiva en tiempo real:** Los badges, iconos y textos de estado se actualizan instantáneamente sin colapsar los animes desplegados.

---

### 👆 Corrección de Desplazamiento y Gestos en Top Animes y Ver Más (Android)
- **Scroll táctil sin clics accidentales:** Se eliminó el conflicto donde arrastrar o deslizar para hacer scroll sobre la lista de Top Animes y Ver Más activaba el clic de la tarjeta.
- **Gestos `Tapped`:** Implementación del reconocedor de gestos de Avalonia que cancela la selección cuando se detecta un deslizamiento, garantizando un desplazamiento suave y natural.

---

### ⚡ Sincronización en Tiempo Real del Reproductor
- **Evento de estado de reproducción:** Integración del evento `PlaybackStateChanged` para sincronizar en tiempo real el botón central de Play/Pausa y la visibilidad de los controles OSD.

---

### 🔄 Descarga Continua y Reanudación del Actualizador (Android)
- **Descarga con pantalla apagada (WakeLock):** La descarga de la actualización APK ahora mantiene activo el CPU del dispositivo mediante `WakeLock`, permitiendo que la descarga finalice aunque la pantalla se apague por inactividad o se bloquee el teléfono.
- **Reanudación por HTTP Range:** Si la conexión se interrumpe, el actualizador continúa desde el punto exacto donde se quedó (`.part`) sin descargar todo el archivo desde cero.
- **Opción de instalación manual:** Si el instalador del sistema no se abre automáticamente o el usuario lo cierra, ahora se muestra el botón *"Instalar actualización"* para abrir el instalador del APK descargado en cualquier momento sin volver a descargarlo.

---

### 🎨 Mejoras Previas v1.6.4
- **Nueva identidad visual e iconos oficiales:** Actualización completa de la iconografía de AniCS en todas las plataformas e instaladores.
- **Descargas en carpeta pública DCIM/AniCS:** Reconocimiento inmediato por galería y apps del sistema en Android.
- **Modo pantalla completa inmersivo 100%:** Ocultación total de la barra de estado en reproducción horizontal.
- **Personalización de la carpeta de descargas:** Selector de carpetas en Ajustes para PC y Móvil.

---

### 📱 Modo Pantalla Completa Inmersivo 100% (Android)
- **Ocultación total de barras del sistema:** Eliminación completa de la barra de estado superior (señal, hora, batería, wifi y notificaciones) durante la reproducción en horizontal mediante `WindowCompat` y `WindowInsetsControllerCompat`.
- **Inmersión transitoria (`BehaviorShowTransientBarsBySwipe`):** Deslizar brevemente desde los bordes muestra los controles del sistema momentáneamente sin interrumpir la experiencia cinemática.
- **Persistencia al rotar:** El reproductor conserva la inmersión completa ante cambios de orientación o cambios de foco en la aplicación.

---

### 💡 Pantalla Siempre Activa / Wake Lock (Android)
- **Prevención de suspensión:** El reproductor mantiene la pantalla encendida de forma continua durante la reproducción de videos, evitando que el móvil se apague o bloquee por inactividad.
- **Liberación automática:** El bloqueo de suspensión se desactiva al pausar o salir del reproductor de video para optimizar la batería.

---

### ⚙️ Personalización de la Carpeta de Descargas (PC & Móvil)
- **Ubicación a medida:** Nueva sección en Ajustes para elegir cualquier carpeta personalizada donde almacenar las descargas y animes locales.
- **Selector de carpetas nativo:** Botón *"Examinar..."* en PC para seleccionar directorios visualmente y botón *"Restablecer"* a la ruta por defecto en ambas plataformas.

---

### 🛠️ Estabilidad y Corrección de Notas de Parche en Móvil
- **Solución al cierre inesperado:** Corrección de la excepción de recursos al pulsar *"Ver Notas de Parche"* en Ajustes de Android.
- **Notas dinámicas y estilizadas:** Visualización interactiva y con diseño moderno del registro de cambios completo dentro del modal.

---

### 📡 Banner Offline No Intrusivo y Mejor Control de Red
- **Ocultado en reproducción:** El banner *"Sin conexión a internet"* se oculta automáticamente al abrir el reproductor de video para disfrutar del contenido sin obstrucciones visuales.
- **Descarte manual y auto-ocultado:** Se añade botón de descarte manual `(X)` y temporizador de auto-ocultado tras 6 segundos al perder la conexión en PC y Móvil.

---

### 🔙 Mejoras previas v1.6.2
- Auto-búsqueda y vinculación de metadata oficial para animes locales.
- Auto-ocultado de controles en pausa en PC.
- Checkpointing y reanudación granular de descargas por segmentos HLS y soporte de descarga continua en segundo plano con Foreground Service.
- Paginación completa del catálogo con salto manual a cualquier página.
