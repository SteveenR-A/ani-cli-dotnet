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
            var escapeSequence = $"\x1b_Ga=p,i={_imageId},q=2,c={_width},r={_height};\x1b\\";
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
        int pageSize = 10,
        bool showImage = true) where T : class
    {
        if (items == null || items.Count == 0) return null;

        // Dynamic page size based on terminal height to utilize full vertical space.
        // Console.WindowHeight is more reliable than AnsiConsole.Console.Profile.Height
        // because Spectre's Profile.Height may not be updated after the terminal is resized
        // or may return 0 in some terminal emulators.
        // Subtract 7: title(1) + separator(1) + up-indicator(1) + down-indicator(1) + empty(1) + controls(1) + margin(1)
        {
            int termHeight = Console.WindowHeight;
            if (termHeight <= 0) termHeight = AnsiConsole.Console.Profile.Height;
            if (termHeight > 0)
                pageSize = Math.Max(8, termHeight - 7);
        }

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
            if (!showImage || string.IsNullOrWhiteSpace(imageUrl))
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
                    needsRedraw = true;
                }
                else
                {
                    var itemCopy = item;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var bytes = await DataCache.GetImageAsync(client, imageUrl, cancellationToken: token);
                            if (token.IsCancellationRequested) return;

                            if (bytes.Length > 0 && items[selectedIndex] == itemCopy)
                            {
                                activeImagePath = cachedPath;
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

        IRenderable BuildUI()
        {
            var terminalWidth = AnsiConsole.Console.Profile.Width;
            // Cap width to prevent massive stretched layouts
            if (terminalWidth <= 0) terminalWidth = 80;
            var isWide = terminalWidth >= 85;

            // Calculate column widths dynamically based on terminal width
            int listColumnWidth;
            int detailsColumnWidth;

            // Only show details if showImage is true (since image visualizer mode is sc only, and latest releases (l) has empty details)
            bool displayDetails = showImage;

            if (isWide && displayDetails)
            {
                listColumnWidth = Math.Clamp((int)(terminalWidth * 0.4), 40, 70);
                detailsColumnWidth = terminalWidth - listColumnWidth - 2;
            }
            else
            {
                listColumnWidth = terminalWidth;
                detailsColumnWidth = 0;
            }

            // Split into two main columns: Left (List) and Right (Details)
            var outerTable = new Table().Border(TableBorder.None).Collapse();
            outerTable.AddColumn(new TableColumn("List").Width(listColumnWidth));
            if (isWide && displayDetails)
            {
                outerTable.AddColumn(new TableColumn("Details").Width(detailsColumnWidth));
            }

            // 1. Build List column
            var listRows = new List<IRenderable>();
            
            // Header
            listRows.Add(new Markup($"[bold cyan]{promptTitle}[/]"));
            listRows.Add(new Text(new string('─', listColumnWidth - 2), new Style(foreground: Color.Grey)));

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
                // Note: displayTitle may contain markup tags. Stripping markup for length calculation is ideal,
                // but for simplicity we assume maxLen is generous enough.
                
                if (index == selectedIndex)
                {
                    listRows.Add(new Markup($"[bold deepskyblue1]> {displayTitle}[/]"));
                }
                else
                {
                    // Callers must provide their own color or escape their strings.
                    listRows.Add(new Markup($"  {displayTitle}"));
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
            if (displayDetails)
            {
                var detailsPanel = BuildDetailsPanel(getTitle(currentItem), activeSynopsis, activeImagePath, getDescription?.Invoke(currentItem), detailsColumnWidth);

                if (isWide)
                {
                    outerTable.AddRow(listGrid, detailsPanel);
                    return outerTable;
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
            }
            else
            {
                // Single column layout (List only) taking full terminal width and height!
                return listGrid;
            }
        }

        Panel BuildDetailsPanel(string itemTitle, string synopsis, string? imagePath, string? description, int detailsWidth)
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
            bool hasImage = showImage && !string.IsNullOrEmpty(imagePath) && File.Exists(imagePath);

            var terminalWidth = AnsiConsole.Console.Profile.Width;
            if (terminalWidth <= 0) terminalWidth = 80;
            bool isWide = terminalWidth >= 85;

            // Calculate widths dynamically to take advantage of the terminal window size
            int imgColumnWidth = hasImage ? 26 : 0;
            int textColumnWidth;
            if (isWide)
            {
                textColumnWidth = hasImage 
                    ? Math.Max(25, detailsWidth - 4 - imgColumnWidth) 
                    : Math.Max(45, detailsWidth - 4);
            }
            else
            {
                textColumnWidth = hasImage 
                    ? Math.Max(25, terminalWidth - 4 - imgColumnWidth) 
                    : Math.Max(45, terminalWidth - 4);
            }

            if (hasImage)
            {
                blockTable.AddColumn(new TableColumn("").Width(imgColumnWidth));
                blockTable.AddColumn(new TableColumn("").Width(textColumnWidth));
            }
            else
            {
                blockTable.AddColumn(new TableColumn("").Width(textColumnWidth));
            }

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
                                    else
                                    {
                                        try
                                        {
                                            var bytes = File.ReadAllBytes(imagePath!);
                                            var base64 = Convert.ToBase64String(bytes);
                                            id = _nextImageId++;
                                            ImageIdCache[imageUrl] = id;

                                            // Transmit only (a=t) with q=2 to prevent response pollution of stdin
                                            Console.Write($"\x1b_Ga=t,i={id},q=2,f=100;{base64}\x1b\\");
                                            imageId = id;
                                        }
                                        catch
                                        {
                                            // Ignore transmission errors
                                        }
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
                        catch (Exception ex)
                        {
                            imageRenderable = new Markup($"[red]Err: {Markup.Escape(ex.GetType().Name)}: {Markup.Escape(ex.Message.Length > 40 ? ex.Message[..40] : ex.Message)}[/]");
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
