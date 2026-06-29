using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace AniCS.Terminal;

public static class DetailsPrompt
{
    private static readonly Dictionary<string, uint> ImageIdCache = new();
    private static readonly Dictionary<string, IRenderable> RenderableCache = new();
    private static uint _nextImageId = 1;

    private static bool IsKittyGraphicsSupported()
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            return false;

        var term = Environment.GetEnvironmentVariable("TERM");
        var termProgram = Environment.GetEnvironmentVariable("TERMINAL_PROGRAM");

        return (term != null && (term.Contains("kitty", StringComparison.OrdinalIgnoreCase) || term.Contains("ghostty", StringComparison.OrdinalIgnoreCase))) ||
               (termProgram != null && (termProgram.Contains("kitty", StringComparison.OrdinalIgnoreCase) || termProgram.Contains("ghostty", StringComparison.OrdinalIgnoreCase)));
    }

    private static void TransmitImageIfSupported(string imagePath, string imageUrl)
    {
        if (!IsKittyGraphicsSupported()) return;

        lock (ImageIdCache)
        {
            if (ImageIdCache.ContainsKey(imageUrl)) return; // Already transmitted
        }

        try
        {
            var bytes = File.ReadAllBytes(imagePath);
            var base64 = Convert.ToBase64String(bytes);
            uint id;
            lock (ImageIdCache)
            {
                id = _nextImageId++;
                ImageIdCache[imageUrl] = id;
            }

            // Transmit only (a=t)
            Console.Write($"\x1b_Ga=t,i={id},f=100;{base64}\x1b\\");
        }
        catch
        {
            // Ignore transmission errors
        }
    }

    private class KittyImageRenderable : IRenderable
    {
        private readonly uint _imageId;
        private readonly int _width;
        private readonly int _height;

        public KittyImageRenderable(uint imageId, int width, int height)
        {
            _imageId = imageId;
            _width = width;
            _height = height;
        }

        public Measurement Measure(RenderOptions options, int maxWidth)
        {
            return new Measurement(_width, _width);
        }

        public IEnumerable<Segment> Render(RenderOptions options, int maxWidth)
        {
            var segments = new List<Segment>();
            var escapeSequence = $"\x1b_Ga=p,i={_imageId},c={_width},r={_height};\x1b\\";
            segments.Add(new Segment(escapeSequence));

            for (int r = 0; r < _height; r++)
            {
                segments.Add(new Segment(new string(' ', _width)));
                if (r < _height - 1)
                {
                    segments.Add(Segment.LineBreak);
                }
            }

            return segments;
        }
    }

    public static async Task<T?> PromptWithDetailsAsync<T>(
        HttpClient client,
        string promptTitle,
        List<T> items,
        Func<T, string> getTitle,
        Func<T, string> getThumbnailUrl,
        Func<T, Task<string>> getSynopsis,
        Func<T, string>? getDescription = null,
        int pageSize = 10) where T : class
    {
        if (items == null || items.Count == 0) return null;

        var synopsisCache = new Dictionary<T, string>();
        
        int selectedIndex = 0;
        int topIndex = 0;
        bool running = true;
        T? selectedItem = null;

        // Current state for active item
        T currentItem = items[0];
        string activeSynopsis = "Cargando sinopsis...";
        string? activeImagePath = null;
        bool needsRedraw = true;

        // We run a cancellation token source to cancel previous image/synopsis loads
        CancellationTokenSource? cts = null;

        void StartLoading(T item)
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            activeSynopsis = "Cargando sinopsis...";
            activeImagePath = null;
            needsRedraw = true;

            // Load Synopsis
            if (synopsisCache.TryGetValue(item, out var cachedSyn))
            {
                activeSynopsis = cachedSyn;
                needsRedraw = true;
            }
            else
            {
                var itemCopy = item;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var syn = await getSynopsis(itemCopy);
                        if (token.IsCancellationRequested) return;
                        synopsisCache[itemCopy] = syn;
                        if (items[selectedIndex] == itemCopy)
                        {
                            activeSynopsis = syn;
                            needsRedraw = true;
                        }
                    }
                    catch
                    {
                        if (token.IsCancellationRequested) return;
                        if (items[selectedIndex] == itemCopy)
                        {
                            activeSynopsis = "Sinopsis no disponible.";
                            needsRedraw = true;
                        }
                    }
                }, token);
            }

            // Load Image
            var imageUrl = getThumbnailUrl(item);
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                activeImagePath = null;
                needsRedraw = true;
            }
            else
            {
                string cachedPath = DataCache.GetImageCachePath(imageUrl);

                if (File.Exists(cachedPath))
                {
                    activeImagePath = cachedPath;
                    TransmitImageIfSupported(cachedPath, imageUrl);
                    needsRedraw = true;
                }
                else
                {
                    var itemCopy = item;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var bytes = await DataCache.GetImageAsync(client, imageUrl, token);
                            if (token.IsCancellationRequested) return;

                            if (bytes.Length > 0 && items[selectedIndex] == itemCopy)
                            {
                                activeImagePath = cachedPath;
                                TransmitImageIfSupported(cachedPath, imageUrl);
                                needsRedraw = true;
                            }
                        }
                        catch
                        {
                            // Ignore image download errors
                        }
                    }, token);
                }
            }
        }

        // Initialize first item loading
        StartLoading(currentItem);

        Table BuildUI()
        {
            var terminalWidth = AnsiConsole.Console.Profile.Width;
            // Cap width to prevent massive stretched layouts
            if (terminalWidth <= 0) terminalWidth = 80;
            var isWide = terminalWidth >= 85;

            // Split into two main columns: Left (List) and Right (Details)
            var outerTable = new Table().Border(TableBorder.None).Collapse();
            outerTable.AddColumn(new TableColumn("List").Width(isWide ? 40 : terminalWidth));
            if (isWide)
            {
                outerTable.AddColumn(new TableColumn("Details").Width(Math.Max(40, terminalWidth - 45)));
            }

            // 1. Build List column
            var listRows = new List<IRenderable>();
            
            // Header
            listRows.Add(new Markup($"[bold cyan]{promptTitle}[/]"));
            listRows.Add(new Text(new string('─', isWide ? 38 : terminalWidth - 2), new Style(foreground: Color.Grey)));

            // Up Indicator
            if (topIndex > 0)
                listRows.Add(new Markup("  [grey]▲ ... más arriba[/]"));
            else
                listRows.Add(new Text(""));

            // Items
            var visibleCount = Math.Min(pageSize, items.Count - topIndex);
            for (int i = 0; i < visibleCount; i++)
            {
                var index = topIndex + i;
                var item = items[index];
                var displayTitle = getTitle(item);
                
                // Crop title if it is too long to fit in the list column
                var maxLen = isWide ? 34 : terminalWidth - 6;
                if (displayTitle.Length > maxLen)
                    displayTitle = displayTitle.Substring(0, maxLen - 3) + "...";

                if (index == selectedIndex)
                {
                    listRows.Add(new Markup($"[bold deepskyblue1]> {Markup.Escape(displayTitle)}[/]"));
                }
                else
                {
                    listRows.Add(new Markup($"  [white]{Markup.Escape(displayTitle)}[/]"));
                }
            }

            // Down Indicator
            if (topIndex + pageSize < items.Count)
                listRows.Add(new Markup("  [grey]▼ ... más abajo[/]"));
            else
                listRows.Add(new Text(""));

            listRows.Add(new Text(""));
            listRows.Add(new Markup("[grey]Flechas/j/k: Mover | Enter: Elegir | q/Esc: Cancelar[/]"));

            var listGrid = new Grid();
            listGrid.AddColumn();
            foreach (var row in listRows)
                listGrid.AddRow(row);

            // 2. Build Details column (or embed details inside a bottom layout if not wide)
            var detailsPanel = BuildDetailsPanel(getTitle(currentItem), activeSynopsis, activeImagePath, getDescription?.Invoke(currentItem), terminalWidth);

            if (isWide)
            {
                outerTable.AddRow(listGrid, detailsPanel);
            }
            else
            {
                // In narrow terminals, stack them vertically
                var stackTable = new Table().Border(TableBorder.None).Collapse();
                stackTable.AddColumn(new TableColumn("Content"));
                stackTable.AddRow(listGrid);
                stackTable.AddRow(new Text(new string('─', terminalWidth - 2), new Style(foreground: Color.Grey)));
                stackTable.AddRow(detailsPanel);
                return stackTable;
            }

            return outerTable;
        }

        Panel BuildDetailsPanel(string itemTitle, string synopsis, string? imagePath, string? description, int terminalWidth)
        {
            var panelGrid = new Grid().AddColumn();

            // Item Title
            panelGrid.AddRow(new Markup($"[bold yellow]{Markup.Escape(itemTitle)}[/]"));
            
            // Quick description (like genre/year if available)
            if (!string.IsNullOrEmpty(description))
            {
                panelGrid.AddRow(new Markup($"[grey]{Markup.Escape(description)}[/]"));
            }
            panelGrid.AddRow(new Text(""));

            // Image and Text block
            var blockTable = new Table().Border(TableBorder.None).Collapse();
            bool hasImage = !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath);

            bool isWide = terminalWidth >= 85;

            // Calculate widths dynamically to take advantage of the terminal window size
            int imgColumnWidth = hasImage ? 26 : 0;
            int textColumnWidth;
            if (isWide)
            {
                textColumnWidth = hasImage 
                    ? Math.Max(25, terminalWidth - 45 - 4 - imgColumnWidth) 
                    : Math.Max(45, terminalWidth - 45 - 4);
            }
            else
            {
                textColumnWidth = hasImage 
                    ? Math.Max(25, terminalWidth - 4 - imgColumnWidth) 
                    : Math.Max(45, terminalWidth - 4);
            }

            blockTable.AddColumn(new TableColumn("Img").Width(imgColumnWidth));
            blockTable.AddColumn(new TableColumn("Text").Width(textColumnWidth));

            IRenderable imageRenderable = new Text("");
            if (hasImage)
            {
                lock (RenderableCache)
                {
                    if (RenderableCache.TryGetValue(imagePath!, out var cachedRenderable))
                    {
                        imageRenderable = cachedRenderable;
                    }
                    else
                    {
                        try
                        {
                            var canvas = new CanvasImage(imagePath!);
                            canvas.MaxWidth(24);

                            uint? imageId = null;
                            if (IsKittyGraphicsSupported())
                            {
                                var imageUrl = getThumbnailUrl(currentItem);
                                lock (ImageIdCache)
                                {
                                    if (ImageIdCache.TryGetValue(imageUrl, out var id))
                                    {
                                        imageId = id;
                                    }
                                }
                            }

                            if (imageId.HasValue)
                            {
                                int imageWidth = canvas.Width;
                                int imageHeight = canvas.Height;
                                
                                int cellWidth = 24;
                                int cellHeight = 12; // default fallback
                                if (imageWidth > 0)
                                {
                                    cellHeight = (int)Math.Round(((double)imageHeight / imageWidth) * cellWidth / 2.0);
                                }

                                imageRenderable = new KittyImageRenderable(imageId.Value, cellWidth, cellHeight);
                            }
                            else
                            {
                                imageRenderable = canvas;
                            }

                            RenderableCache[imagePath!] = imageRenderable;
                        }
                        catch
                        {
                            imageRenderable = new Markup("[red]Error cargando img[/]");
                        }
                    }
                }
            }

            var textRows = new Grid().AddColumn();
            textRows.AddRow(new Markup("[bold deepskyblue1]Sinopsis:[/]"));
            
            // Format synopsis
            var synText = string.IsNullOrEmpty(synopsis) ? "Sinopsis no disponible." : synopsis;
            textRows.AddRow(new Text(synText));

            if (hasImage)
            {
                blockTable.AddRow(imageRenderable, textRows);
            }
            else
            {
                blockTable.AddRow(textRows);
            }

            panelGrid.AddRow(blockTable);

            var panel = new Panel(panelGrid)
                .Header("[bold]Detalles[/]")
                .BorderColor(Color.DeepSkyBlue1);

            return panel;
        }

        try
        {
            AnsiConsole.Clear();
            var initialUI = BuildUI();
            await AnsiConsole.Live(initialUI)
                .StartAsync(async ctx =>
                {
                    while (running)
                    {
                        if (needsRedraw)
                        {
                            ctx.UpdateTarget(BuildUI());
                            ctx.Refresh();
                            needsRedraw = false;
                        }

                        if (Console.KeyAvailable)
                        {
                            var keyInfo = Console.ReadKey(true);
                            var key = keyInfo.Key;
                            var keyChar = char.ToLower(keyInfo.KeyChar);

                            switch (key, keyChar)
                            {
                                case (ConsoleKey.UpArrow, _):
                                case (_, 'k'):
                                    if (selectedIndex > 0)
                                    {
                                        selectedIndex--;
                                        if (selectedIndex < topIndex)
                                            topIndex = selectedIndex;
                                        
                                        currentItem = items[selectedIndex];
                                        StartLoading(currentItem);
                                        needsRedraw = true;
                                    }
                                    break;

                                case (ConsoleKey.DownArrow, _):
                                case (_, 'j'):
                                    if (selectedIndex < items.Count - 1)
                                    {
                                        selectedIndex++;
                                        if (selectedIndex >= topIndex + pageSize)
                                            topIndex = selectedIndex - pageSize + 1;

                                        currentItem = items[selectedIndex];
                                        StartLoading(currentItem);
                                        needsRedraw = true;
                                    }
                                    break;

                                case (ConsoleKey.Enter, _):
                                    selectedItem = items[selectedIndex];
                                    running = false;
                                    break;

                                case (ConsoleKey.Escape, _):
                                case (_, 'q'):
                                    selectedItem = null;
                                    running = false;
                                    break;
                            }
                        }
                        else
                        {
                            await Task.Delay(50);
                        }
                    }
                });
        }
        finally
        {
            cts?.Cancel();
        }

        return selectedItem;
    }
}
