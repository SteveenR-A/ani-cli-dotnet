# Paradigmas Visuales (View Paradigms)

La arquitectura de `AniCS.Desktop` se diseñó con un enfoque altamente experimental en la Interfaz de Usuario (UI). En lugar de tener una sola pantalla de inicio ("HomeView"), el proyecto permite intercambiar completamente el **paradigma visual** de toda la aplicación, manteniendo el mismo código de obtención de datos (`HomeViewModel`, `AniCS.Core`).

## Arquitectura

- **Motor de Datos (ViewModel)**: `HomeViewModel.cs` es la única fuente de la verdad para todas las vistas. Gestiona la lista de animes (`AnimeList`), el recargo asíncrono y la selección.
- **Selector de Paradigmas**: La ventana principal (`MainWindow.axaml.cs`) se encarga de instanciar y montar el "Paradigma" activo como el contenido principal.
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
  - Se eliminaron los botones embebidos para favorecer eventos nativos (`KeyUp`, `DoubleTapped`) que luego se enrutan a la lógica de navegación general de `MainWindow.axaml.cs`.

### 3. HUD Mode (Circular/Futurista)
- **Estado**: ⏳ Pendiente (WIP)
- **Ruta**: `Paradigms/HUD/`
- **Descripción**: Se espera que sea una interfaz estilo "Head-Up Display" (HUD) radial. Los elementos en vez de estar en una grilla o lista vertical, deberían rodear al usuario o presentarse en un arco/carrusel elíptico. Requiere matemáticas polares (Seno/Coseno) en C# (o un `Canvas` y rotaciones en XAML) para posicionar los elementos.

### 4. Node Mode (Grafo)
- **Estado**: ⏳ Pendiente
- **Ruta**: `Paradigms/Node/`
- **Descripción**: Un paradigma en donde los animes se muestran como nodos interconectados (estilo Obsidian o diagramas de red). La navegación sería saltar entre nodos adyacentes.

### 5. Kinetic Mode (Tipográfico)
- **Estado**: ⏳ Pendiente
- **Ruta**: `Paradigms/Kinetic/`
- **Descripción**: Una interfaz centrada puramente en la tipografía fluida y masiva. Los títulos de los animes rellenan la pantalla y el "hover" o selección revela un recorte de la imagen detrás del texto (usando `OpacityMask` o `VisualBrush` en Avalonia).

### 6. Spatial Mode (2.5D)
- **Estado**: ⏳ Pendiente
- **Ruta**: `Paradigms/Spatial/`
- **Descripción**: Simulación de profundidad tridimensional. Usando transformaciones matrices (`MatrixTransform`, perspectivas) para mostrar un carrusel inclinado que emule un flujo tridimensional (tipo CoverFlow antiguo de iTunes, pero moderno).

## Notas para Agentes de IA Futuros

1. **Reutilización**: Al construir los modos pendientes (ej. HUD, Spatial), **NO** escribas lógica para conectarte a JkAnime o hacer HTTP. Todo eso ya lo hace `HomeViewModel`. Tu única misión en un paradigma es mostrar la colección `AnimeList` de una manera gráfica y asegurarte de enviar el evento de "Seleccionado" a `MainWindow`.
2. **Manejo de Entrada (Inputs)**: Avalonia puede ser estricto con los enrutamientos de clics si alteras mucho la estructura (como pasó en el modo ASCII). Siempre verifica si la nueva UI intercepta los clics/teclas correctamente. 
3. **Optimización**: Modos como Spatial o Node requerirán muchos redibujados gráficos (Transforms). Evita hacer cálculos pesados en XAML y confía en el renderizador de Skia que tiene Avalonia 11, o delega la posición de los nodos a un controlador en C# en el evento `ArrangeOverride`.
