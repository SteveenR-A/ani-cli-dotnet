# Paradigmas Visuales (View Paradigms)

La arquitectura de `AniCS.Desktop` se diseñó con un enfoque altamente experimental en la Interfaz de Usuario (UI). En lugar de tener una sola pantalla de inicio ("HomeView"), el proyecto permite intercambiar completamente el **paradigma visual** de la aplicación, manteniendo el mismo código de obtención de datos (`HomeViewModel`, `AniCS.Core`).

## Arquitectura

- **Motor de Datos (ViewModel)**: `HomeViewModel.cs` es la única fuente de la verdad para todas las vistas. Gestiona la lista de animes (`AnimeList`), el recargo asíncrono y la selección.
- **Selector de Paradigmas**: La ventana principal (`MainWindow.axaml.cs`) se encarga de instanciar y montar el "Paradigma" activo como el contenido principal (según `AppConfig.UiParadigm`).
- **Ruta Base**: `src/AniCS.Desktop/Views/Paradigms/`

## Modos Actuales y su Estado

### 1. Classic Mode (HomeView)
- **Estado**: ✅ Completo
- **Descripción**: La vista clásica de grillas. Muestra grandes bloques cuadrados (`AnimeBlockControl`) estilo Netflix/Crunchyroll.

### 2. ASCII Mode (TUI)
- **Estado**: ✅ Completo
- **Ruta**: `Paradigms/ASCII/ASCIIView.axaml`
- **Descripción**: Simula una Terminal de Consola (TUI).
  - La navegación depende exclusivamente de listas de texto (`ListBox`) y flechas del teclado, usando el prefijo `[*]` para la selección.
  - Sin botones embebidos: usa eventos nativos (`KeyUp`, `DoubleTapped`) enrutados a la lógica de navegación general de `MainWindow.axaml.cs`.

### 3. AndroidApp Mode (Mockup móvil)
- **Estado**: ✅ Funcional
- **Ruta**: `Paradigms/AndroidApp/AndroidAppView.axaml`
- **Descripción**: Mockup visual con estética de aplicación móvil para navegar la colección.

### 4. HUD Mode (Circular/Futurista)
- **Estado**: ⏳ WIP (esqueleto vacío creado)
- **Ruta**: `Paradigms/HUD/HUDView.axaml`
- **Descripción**: Interfaz estilo "Head-Up Display" (HUD) radial. Los elementos deberían rodear al usuario o presentarse en un arco/carrusel elíptico. Requiere matemáticas polares (Seno/Coseno) en C# (o un `Canvas` con rotaciones en XAML).
- **Relacionado**: `Controls/HudRadialMenuDialog.axaml` (menú radial para selección de episodios) ya existe.

### 5. Node Mode (Grafo)
- **Estado**: ⏳ WIP (esqueleto vacío creado)
- **Ruta**: `Paradigms/Node/NodeView.axaml`
- **Descripción**: Los animes se muestran como nodos interconectados (estilo Obsidian o diagramas de red). La navegación sería saltar entre nodos adyacentes.

### 6. Kinetic Mode (Tipográfico)
- **Estado**: ⏳ WIP (esqueleto vacío creado)
- **Ruta**: `Paradigms/Kinetic/KineticView.axaml`
- **Descripción**: Interfaz centrada en tipografía fluida y masiva. Los títulos rellenan la pantalla y el "hover"/selección revela un recorte de la imagen detrás del texto (usando `OpacityMask` o `VisualBrush` en Avalonia).

### 7. Spatial Mode (2.5D)
- **Estado**: ⏳ WIP (esqueleto vacío creado)
- **Ruta**: `Paradigms/Spatial/SpatialView.axaml`
- **Descripción**: Simulación de profundidad tridimensional. Usando transformaciones matriciales (`MatrixTransform`, perspectivas) para mostrar un carrusel inclinado que emule un flujo 3D (tipo CoverFlow de iTunes, moderno).

## Notas para Agentes de IA Futuros

1. **Reutilización**: Al construir los modos pendientes (HUD, Node, Kinetic, Spatial), **NO** escribas lógica para conectarte a JKAnime/MundoDonghua o hacer HTTP. Todo eso ya lo hace `HomeViewModel`. Tu única misión en un paradigma es mostrar la colección `AnimeList` de una manera gráfica y emitir el evento de "Seleccionado" a `MainWindow`.
2. **Manejo de Entrada (Inputs)**: Avalonia puede ser estricto con los enrutamientos de clics si alteras mucho la estructura (como pasó en el modo ASCII). Siempre verifica si la nueva UI intercepta los clics/teclas correctamente.
3. **Optimización**: Modos como Spatial o Node requerirán muchos redibujados gráficos (Transforms). Evita cálculos pesados en XAML y confía en el renderizador de Skia que trae Avalonia 12, o delega la posición de los nodos a un controlador en C# en el evento `ArrangeOverride`.
