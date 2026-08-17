# 🚀 AniCS v1.6.4 — Registro de Cambios

¡Nueva actualización v1.6.4 con la nueva identidad visual e iconos oficiales de AniCS, además de las mejoras integrales de almacenamiento, reproducción inmersiva y estabilidad!

---

### 🎨 Nueva Identidad Visual e Iconos Oficiales
- **Iconografía renovada:** Actualización de los iconos de la aplicación e instalador con el diseño visual oficial de AniCS.
- **Consistencia visual:** Integración optimizada de recursos gráficos para accesos directos, barra de tareas y empaquetado del instalador MSI.

---

### 📁 Almacenamiento en Carpeta Pública del Sistema DCIM (Android)
- **Descargas en DCIM:** Las descargas en Android ahora se guardan directamente en la carpeta pública del sistema `DCIM/AniCS` (`/storage/emulated/0/DCIM/AniCS`).
- **Compatibilidad total con apps del sistema:** Los episodios descargados son reconocidos de forma inmediata por la galería multimedia de Android, reproductores nativos y gestores de archivos.
- **Gestión de Permisos:** Configuración automática y solicitud en tiempo de ejecución de permisos de almacenamiento (`READ_MEDIA_VIDEO`, `READ_MEDIA_IMAGES`, `READ_EXTERNAL_STORAGE`, `WRITE_EXTERNAL_STORAGE`) y `requestLegacyExternalStorage`.

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
