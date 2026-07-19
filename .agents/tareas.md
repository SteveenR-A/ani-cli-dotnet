# Tareas (Tasks)

Registro de tareas pendientes, en progreso y completadas del proyecto AniCS.

## Pendientes / Sugerencias para el futuro
- [ ] **Soporte multi-idioma (i18n)**: Extraer los textos en crudo del código `.axaml` y utilizar archivos de recursos (.resx) o diccionarios dinámicos para soportar Inglés/Español.
- [ ] **Historial Local**: Guardar el último episodio visto de cada serie localmente en el archivo `config.json` para facilitar la continuación.

## Completadas recientemente
- [x] **Nuevos Extractores**: Implementar scraping para nuevos sitios web (MundoDonghua añadido).
- [x] Ajustar márgenes del menú hamburguesa en `MainWindow.axaml` para que no sobresalga.
- [x] Añadir y afinar paletas de colores en `ThemeManager.cs` (Dracula, Light, Tokyo Night, Cyberpunk con menos brillo amarillo).
- [x] Refactorizar los User-Agents "quemados" y rotativos moviéndolos a la lista central de `AppConfig.cs`.
- [x] Reparar la advertencia XAML en `SettingsView.axaml` (reemplazar Watermark obsoleto por PlaceholderText).
- [x] Limpiar archivos `.dll` inexistentes del instalador `.wxs` de WiX y forzar la versión `4.0.5` en el script PowerShell de construcción.
