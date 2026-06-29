using AniCS.Extractors;
using AniCS.History;
using AniCS.Terminal;
using AniCS.Models;
using Spectre.Console;

namespace AniCS;

public class Program
{
    private static readonly HttpClientHandler _handler = new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.All
    };
    private static readonly HttpClient _http = new HttpClient(_handler) { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly List<IAnimeExtractor> _extractors =
    [
        new JKAnimeExtractor(_http),
        new AnimeAV1Extractor(_http),
    ];
    private static IAnimeExtractor _active = null!;
    private static readonly WatchHistory _history = new();

    public static async Task Main(string[] args)
    {
        try
        {
            _active = _extractors[0]; // default to JKAnime

            AnsiConsole.Clear();

            // ── ASCII Banner ──────────────────────────────────────────
            AnsiConsole.Write(
                new FigletText("AniCS")
                    .LeftJustified()
                    .Color(Color.DeepSkyBlue1));

            AnsiConsole.Write(new Rule("[deepskyblue1]ani-cli · versión .NET 10[/]")
                .LeftJustified().RuleStyle("grey23"));

            AnsiConsole.WriteLine();

            AnsiConsole.Write(new Panel(
                "[bold green]Bienvenido a AniCS (Modo Interactivo)[/]\n" +
                $"Fuente activa: [bold yellow]{_active.Domain}[/]  |  " +
                $"yt-dlp: {(YtDlpResolver.IsAvailable() ? "[green]disponible ✓[/]" : "[grey]no instalado[/]")}  |  " +
                "Escribe [bold yellow]help[/] para los comandos o [bold red]exit[/] para salir.")
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0)
            });

            AnsiConsole.WriteLine();

            // ── REPL Loop ─────────────────────────────────────────────
            while (true)
            {
                var raw = AnsiConsole.Ask<string>("[bold deepskyblue1]anics[/] [grey]>[/] ").Trim();
                var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;

                var cmd = parts[0].ToLowerInvariant();
                var arg = parts.Length > 1 ? parts[1] : string.Empty;

                switch (cmd)
                {
                    case "exit":
                    case "quit":
                    case "q":
                        AnsiConsole.MarkupLine("[dim]Hasta luego 👋[/]");
                        return;

                    case "help":
                        ShowHelp();
                        break;

                    case "search":
                    case "s":
                        if (string.IsNullOrWhiteSpace(arg))
                            AnsiConsole.MarkupLine("[yellow]Uso:[/] search [grey]<título>[/]");
                        else
                            await HandleSearch(arg);
                        break;

                    case "latest":
                    case "l":
                        await HandleLatest();
                        break;

                    case "scoop":
                    case "sc":
                        await HandleScoop();
                        break;

                    case "history":
                    case "h":
                        await HandleHistory();
                        break;

                    case "fuente":
                    case "f":
                        HandleSource();
                        break;

                    case "clearcache":
                    case "cc":
                        DataCache.ClearRamCache();
                        AnsiConsole.MarkupLine("[green]Caché de memoria RAM limpiado con éxito.[/]");
                        break;

                    case "clear":
                    case "cls":
                        AnsiConsole.Clear();
                        break;

                    default:
                        AnsiConsole.MarkupLine($"[red]Comando desconocido:[/] '{Markup.Escape(cmd)}'. Escribe [bold]help[/].");
                        break;
                }
            }
        }
        finally
        {
            DataCache.ClearCacheDirectory();
        }
    }

    // ── Help ──────────────────────────────────────────────────────
    private static void ShowHelp()
    {
        // ── Comandos principales ─────────────────────────────────
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[yellow bold]AniCS — Comandos[/]")
            .AddColumn(new TableColumn("[bold]Comando[/]"))
            .AddColumn(new TableColumn("[bold]Alias[/]").Centered())
            .AddColumn(new TableColumn("[bold]Descripción[/]"));

        table.AddRow("[bold]search[/] [[título]]", "[grey]s[/]",   "Busca un anime específico");
        table.AddRow("[bold]latest[/]",             "[grey]l[/]",   "Últimos episodios estrenados");
        table.AddRow("[bold]scoop[/]",              "[grey]sc[/]",  "Cartelera semanal de estrenos");
        table.AddRow("[bold]history[/]",            "[grey]h[/]",   "Ver historial de animes vistos");
        table.AddRow("[bold]fuente[/]",             "[grey]f[/]",   "Cambiar la fuente activa");
        table.AddRow("[bold]clear[/]",              "[grey]cls[/]", "Limpiar pantalla");
        table.AddRow("[bold]clearcache[/]",         "[grey]cc[/]",  "Limpiar caché de RAM (forzar actualización)");
        table.AddRow("[bold]exit[/]",               "[grey]q[/]",   "Salir de la aplicación");

        AnsiConsole.Write(table);

        // ── Fuentes disponibles ──────────────────────────────────
        var sources = new Table()
            .Border(TableBorder.Rounded)
            .Title("[deepskyblue1 bold]Fuentes — f[/]")
            .AddColumn(new TableColumn("[bold]Nombre[/]"))
            .AddColumn(new TableColumn("[bold]Dominio[/]"))
            .AddColumn(new TableColumn("[bold]Estado[/]").Centered());

        foreach (var ext in _extractors)
        {
            bool isActive = ext.Domain == _active.Domain;
            sources.AddRow(
                isActive ? $"[bold green]{ext.Domain.Split('.')[0]}[/]" : ext.Domain.Split('.')[0],
                $"[grey]{ext.Domain}[/]",
                isActive ? "[bold green]● Activa[/]" : "[grey]○[/]"
            );
        }

        AnsiConsole.Write(sources);
        AnsiConsole.MarkupLine($"  [dim]Ejemplo:[/] [bold]f[/] [dim](Abre el selector de fuentes)[/]");
        AnsiConsole.WriteLine();

        // ── Servidores Soportados ────────────────────────────────
        var servers = new Table()
            .Border(TableBorder.Rounded)
            .Title("[yellow bold]Servidores y Funciones[/]")
            .AddColumn(new TableColumn("[bold]Servidor[/]"))
            .AddColumn(new TableColumn("[bold]Streaming (mpv)[/]").Centered())
            .AddColumn(new TableColumn("[bold]Descarga (yt-dlp)[/]").Centered());

        servers.AddRow("Desu / Magi (Nativo)", "[green]✓ Soportado[/]", "[green]✓ Soportado[/]");
        servers.AddRow("Mediafire (Nativo)",   "[green]✓ Soportado[/]", "[green]✓ Soportado[/]");
        servers.AddRow("Mp4upload / Streamtape", "[green]✓ Soportado[/]", "[green]✓ Soportado[/]");
        servers.AddRow("Mega",                 "[red]✗ No Soportado[/]", "[yellow]Enlace Directo[/]");
        servers.AddRow("VOE / Filemoon",       "[red]✗ Protegido (CF)[/]", "[red]✗ Protegido (CF)[/]");

        AnsiConsole.Write(servers);
        AnsiConsole.WriteLine();
    }

    // ── Search ────────────────────────────────────────────────────
    private static async Task HandleSearch(string query)
    {
        List<AniCS.Models.AnimeResult> results = [];

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync($"Buscando en [yellow]{_active.Domain}[/]...", async _ =>
            {
                results = await DataCache.GetOrFetchDataAsync($"search_{_active.Domain}_{query}", TimeSpan.FromMinutes(5), () => _active.SearchAsync(query));
            });

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No se encontraron resultados.[/]");
            return;
        }

        var anime = await AniCS.Terminal.DetailsPrompt.PromptWithDetailsAsync(
            _http,
            "Selecciona un anime",
            results,
            r => r.Title,
            r => r.ThumbnailUrl,
            async r => await DataCache.GetOrFetchDataAsync($"synopsis_{r.Url}", TimeSpan.FromMinutes(5), () => _active.GetSynopsisAsync(r.Url)),
            r => r.Description
        );

        if (anime == null) return;

        await DisplayAnimeInfoAsync(anime);

        // Load episodes
        List<AniCS.Models.Episode> episodes = [];
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync("Cargando episodios...", async _ =>
            {
                episodes = await DataCache.GetOrFetchDataAsync($"eps_{anime.Url}", TimeSpan.FromMinutes(5), () => _active.GetEpisodesAsync(anime.Url));
            });

        if (episodes.Count == 0)
        {
            // If no episodes parsed, play the URL directly
            AnsiConsole.MarkupLine("[yellow]No se pudo obtener la lista de episodios automáticamente.[/]");
            if (AnsiConsole.Confirm("¿Reproducir URL de la página del anime directamente?"))
            {
                await PlayEpisodesLoop([new AniCS.Models.Episode { Url = anime.Url, Title = "Direct URL", EpisodeNumber = "1" }], 0, anime, false);
            }
            return;
        }

        while (true)
        {
            var selectedEpisode = await AniCS.Terminal.DetailsPrompt.PromptWithDetailsAsync(
                _http,
                "Selecciona un episodio",
                episodes,
                e => $"Ep {e.EpisodeNumber} — {e.Title}".TrimEnd('—', ' '),
                e => e.ThumbnailUrl,
                e => Task.FromResult(string.Empty),
                null,
                showImage: false
            );

            if (selectedEpisode == null) return;

            var epIndex = episodes.IndexOf(selectedEpisode);

            bool exitToMain = await PlayEpisodesLoop(episodes, epIndex, anime, allowBinge: true);
            if (exitToMain) return;
        }
    }

    // ── Latest ────────────────────────────────────────────────────
    private static async Task HandleLatest()
    {
        List<AniCS.Models.Episode> results = [];

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync($"Obteniendo estrenos de [yellow]{_active.Domain}[/]...", async _ =>
            {
                results = await DataCache.GetOrFetchDataAsync($"latest_{_active.Domain}", TimeSpan.FromMinutes(5), () => _active.GetLatestReleasesAsync());
            });

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No se encontraron estrenos.[/]");
            return;
        }

        var selectedEpisode = await AniCS.Terminal.DetailsPrompt.PromptWithDetailsAsync(
            _http,
            $"Últimos Estrenos — {_active.Domain}",
            results,
            r => $"Ep {(string.IsNullOrEmpty(r.EpisodeNumber) ? "—" : r.EpisodeNumber),-4} │ {r.Title}",
            r => r.ThumbnailUrl,
            r => Task.FromResult(string.Empty),
            null,
            pageSize: 12,
            showImage: false
        );

        if (selectedEpisode == null) return;

        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(selectedEpisode.Title)}[/]").RuleStyle("deepskyblue1"));

        var dummyAnime = new AniCS.Models.AnimeResult { Title = selectedEpisode.Title, Url = selectedEpisode.Url };
        await PlayEpisodesLoop([selectedEpisode], 0, dummyAnime, allowBinge: false);
    }

    // ── Scoop ─────────────────────────────────────────────────────
    private static async Task HandleScoop()
    {
        List<AniCS.Models.ScheduleItem> results = [];

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync($"Cargando cartelera semanal de [yellow]{_active.Domain}[/]...", async _ =>
            {
                results = await DataCache.GetOrFetchDataAsync($"scoop_{_active.Domain}", TimeSpan.FromMinutes(5), () => _active.GetWeeklyScoopAsync());
            });

        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No se encontró información del calendario.[/]");
            return;
        }

        var daysOfWeek = new List<string> { "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo" };
        var orderedResults = results.OrderByDescending(x => daysOfWeek.IndexOf(x.Day.ToLowerInvariant())).ToList();

        var selectedItem = await AniCS.Terminal.DetailsPrompt.PromptWithDetailsAsync(
            _http,
            "Cartelera Semanal (Más recientes primero)",
            orderedResults,
            r => $"[{r.Day}] {r.Title}",
            r => r.ThumbnailUrl,
            async r => await DataCache.GetOrFetchDataAsync($"synopsis_{r.Url}", TimeSpan.FromMinutes(5), () => _active.GetSynopsisAsync(r.Url)),
            r => r.Day
        );

        if (selectedItem == null) return;

        var item = selectedItem;
        var anime = new AniCS.Models.AnimeResult { Title = item.Title, Url = item.Url, ThumbnailUrl = item.ThumbnailUrl };

        await DisplayAnimeInfoAsync(anime, $"Emisión el {Markup.Escape(item.Day)}");

        // Load episodes
        List<AniCS.Models.Episode> episodes = [];
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync("Cargando episodios...", async _ =>
            {
                episodes = await DataCache.GetOrFetchDataAsync($"eps_{anime.Url}", TimeSpan.FromMinutes(5), () => _active.GetEpisodesAsync(anime.Url));
            });

        if (episodes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No se pudo obtener la lista de episodios automáticamente.[/]");
            if (AnsiConsole.Confirm("¿Reproducir URL de la página del anime directamente?"))
            {
                await PlayEpisodesLoop([new AniCS.Models.Episode { Url = anime.Url, Title = "Direct URL", EpisodeNumber = "1" }], 0, anime, false);
            }
            return;
        }

        var selectedEpisode = await AniCS.Terminal.DetailsPrompt.PromptWithDetailsAsync(
            _http,
            "Selecciona un episodio",
            episodes,
            e => $"Ep {e.EpisodeNumber} — {e.Title}".TrimEnd('—', ' '),
            e => e.ThumbnailUrl,
            e => Task.FromResult(string.Empty),
            null,
            showImage: false
        );

        if (selectedEpisode == null) return;

        var epIndex = episodes.IndexOf(selectedEpisode);
        await PlayEpisodesLoop(episodes, epIndex, anime);
    }

    // ── History ───────────────────────────────────────────────────
    private static async Task HandleHistory()
    {
        var entries = _history.GetAll();
        if (entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No hay historial todavía. ¡Empieza a ver anime![/]");
            return;
        }

        var options = entries.Select(e => 
            $"{Markup.Escape(e.AnimeTitle)} [grey](Último Ep: {Markup.Escape(e.LastEpisodeNumber)})[/]").ToList();
        options.Add("[red]Volver al menú principal[/]");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold deepskyblue1]Historial de Reproducción[/]")
                .PageSize(15)
                .HighlightStyle(Style.Parse("green bold"))
                .AddChoices(options));

        if (selected == "[red]Volver al menú principal[/]") return;

        var selectedEntry = entries[options.IndexOf(selected)];

        var dummyAnime = new AniCS.Models.AnimeResult { Title = selectedEntry.AnimeTitle, Url = selectedEntry.AnimeUrl };
        await DisplayAnimeInfoAsync(dummyAnime, "Desde el historial de visualización");

        List<AniCS.Models.Episode> episodes = [];
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync("Cargando lista de episodios...", async _ =>
            {
                episodes = await _active.GetEpisodesAsync(selectedEntry.AnimeUrl);
            });

        if (episodes.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No se pudieron obtener los episodios. Intentando reproducir el enlace directamente...[/]");
            await PlayEpisodesLoop([new AniCS.Models.Episode { Url = selectedEntry.AnimeUrl, Title = "Direct URL", EpisodeNumber = "1" }], 0, dummyAnime, false);
            return;
        }

        var epTitles = episodes.Select(e => 
        {
            string title = $"Ep {e.EpisodeNumber} — {e.Title}".TrimEnd('—', ' ');
            if (e.EpisodeNumber == selectedEntry.LastEpisodeNumber)
                title += " [yellow](Último visto)[/]";
            return title;
        }).ToList();
        epTitles.Add("[red]Volver al menú principal[/]");

        while (true)
        {
            var epSelected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"[bold]Episodios de {Markup.Escape(selectedEntry.AnimeTitle)}:[/]")
                    .PageSize(15)
                    .HighlightStyle(Style.Parse("green bold"))
                    .AddChoices(epTitles));

            if (epSelected == "[red]Volver al menú principal[/]") return;

            var epIndex = epTitles.IndexOf(epSelected);
            bool exitToMain = await PlayEpisodesLoop(episodes, epIndex, dummyAnime, allowBinge: true);
            if (exitToMain) return;
        }
    }

    // ── Server Selection Helper ───────────────────────────────────
    private static async Task<VideoServer?> PromptServerSelection(string url)
    {
        List<VideoServer> servers = [];
        await AnsiConsole.Status()
            .StartAsync("Obteniendo servidores...", async _ =>
            {
                servers = await _active.GetVideoServersAsync(url);
            });

        if (servers.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No se encontraron servidores de video.[/]");
            return null;
        }

        if (servers.Count == 1)
            return servers[0]; // Auto-seleccionar si solo hay 1

        var serverNames = servers.Select(s => 
            s.IsDirectPlaySupported ? $"[green]{s.Name}[/]" : $"[blue]{s.Name}[/]"
        ).ToList();
        serverNames.Add("[red]Cancelar[/]");

        var sSelected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Selecciona un servidor:[/]")
                .PageSize(10)
                .HighlightStyle(Style.Parse("yellow bold"))
                .AddChoices(serverNames));

        if (sSelected == "[red]Cancelar[/]") return null;

        return servers.First(s => sSelected.Contains(s.Name));
    }

    // ── Common URL Resolving ─────────────────────────────────────────────
    private static void HandleSource()
    {
        var sourceNames = _extractors.Select(e => e.Domain).ToList();
        sourceNames.Add("[red]Cancelar[/]");

        var selected = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]Selecciona una fuente:[/]")
                .PageSize(10)
                .HighlightStyle(Style.Parse("yellow bold"))
                .AddChoices(sourceNames));

        if (selected == "[red]Cancelar[/]") return;

        var match = _extractors.First(e => e.Domain == selected);
        _active = match;
        AnsiConsole.MarkupLine($"[green]Fuente cambiada a:[/] [bold]{_active.Domain}[/]");
    }

    // ── Helpers ───────────────────────────────────────────────────
    /// <summary>
    /// Resolves video URL: tries internal extractor first, then yt-dlp as fallback.
    /// yt-dlp handles dozens of video hosts with proven anti-bot techniques.
    /// </summary>
    private static async Task<(string url, string referer)> ResolveWithStatus(string episodeUrl)
    {
        string videoUrl = string.Empty;
        string referer  = episodeUrl; // The episode page is the Referer for CDN requests

        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("green"))
            .StartAsync("Resolviendo enlace de video (extractor interno)...", async _ =>
            {
                videoUrl = await _active.ResolveVideoUrlAsync(episodeUrl);
            });

        if (string.IsNullOrEmpty(videoUrl) && YtDlpResolver.IsAvailable())
        {
            AnsiConsole.MarkupLine("[dim]Extractor interno falló. Probando con yt-dlp...[/]");
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots2)
                .SpinnerStyle(Style.Parse("yellow"))
                .StartAsync("Resolviendo con yt-dlp...", async _ =>
                {
                    videoUrl = await YtDlpResolver.ResolveAsync(episodeUrl, referer);
                });
        }

        return (videoUrl, referer);
    }

    private enum LoopAction
    {
        ExitWithTrue,
        ExitWithFalse,
        Repeat,
        Next
    }

    private static int GetNextEpisodeIndex(List<AniCS.Models.Episode> episodes, int currentIndex)
    {
        bool isDescending = false;
        if (episodes.Count > 1)
        {
            double.TryParse(episodes[0].EpisodeNumber, out double num0);
            double.TryParse(episodes[episodes.Count - 1].EpisodeNumber, out double num1);
            if (num0 > num1) isDescending = true;
        }

        return isDescending ? currentIndex - 1 : currentIndex + 1;
    }

    private static void HandleDownload(AniCS.Models.VideoServer selectedServer, string epVideoUrl, string epReferer, AniCS.Models.AnimeResult anime, AniCS.Models.Episode episode)
    {
        if (selectedServer.Name.Contains("Mega", StringComparison.OrdinalIgnoreCase))
        {
            AnsiConsole.MarkupLine("[yellow]Mega cifra sus videos. Para descargar, abre este enlace en tu navegador o usa megatools:[/]");
            AnsiConsole.MarkupLine($"[blue underline]{Markup.Escape(selectedServer.Url)}[/]");
        }
        else
        {
            var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Descargas", "AniCS");
            var dirPrompt = AnsiConsole.Prompt(
                new TextPrompt<string>($"[bold]Ruta de descarga:[/] [grey](Presiona Enter para usar por defecto)[/]")
                    .DefaultValue(defaultDir)
                    .AllowEmpty());

            var targetDir = string.IsNullOrWhiteSpace(dirPrompt) ? defaultDir : dirPrompt;

            YtDlpResolver.Download(epVideoUrl, anime.Title, episode.EpisodeNumber, targetDir, epReferer);
            _history.Record(anime.Title, anime.Url, episode.EpisodeNumber, epVideoUrl);
        }
    }

    private static LoopAction PromptPostPlayAction()
    {
        var postAction = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[bold]¿Qué deseas hacer ahora?[/]")
                .HighlightStyle(Style.Parse("cyan bold"))
                .AddChoices("▶ Siguiente Episodio", "↺ Repetir Episodio", "Volver a lista de episodios", "Volver al menú principal"));

        return postAction switch
        {
            "Volver al menú principal" => LoopAction.ExitWithTrue,
            "Volver a lista de episodios" => LoopAction.ExitWithFalse,
            "↺ Repetir Episodio" => LoopAction.Repeat,
            _ => LoopAction.Next
        };
    }

    private static async Task<LoopAction> PlaySingleEpisodeAsync(List<AniCS.Models.Episode> episodes, int currentIndex, AniCS.Models.AnimeResult anime, bool allowBinge)
    {
        var episode = episodes[currentIndex];

        AnsiConsole.Clear();
        var selectedServer = await PromptServerSelection(episode.Url);
        if (selectedServer == null) return LoopAction.ExitWithFalse;

        var (epVideoUrl, epReferer) = await ResolveWithStatus(selectedServer.Url);
        if (string.IsNullOrEmpty(epVideoUrl))
        {
            AnsiConsole.MarkupLine("[red]No se pudo resolver el enlace de video.[/]");
            return LoopAction.ExitWithFalse;
        }

        var action = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title($"[bold]¿Qué hacer con {Markup.Escape(episode.Title)} (Ep {episode.EpisodeNumber})?[/]")
                .AddChoices("▶ Reproducir", "⬇ Descargar", "Cancelar"));

        if (action == "Cancelar") return LoopAction.ExitWithFalse;

        if (action == "⬇ Descargar")
        {
            HandleDownload(selectedServer, epVideoUrl, epReferer, anime, episode);
            return LoopAction.ExitWithFalse;
        }

        // Play
        AnsiConsole.MarkupLine($"[dim]Iniciando reproductor:[/] [bold]{Markup.Escape(anime.Title)}[/] [grey]Ep.{Markup.Escape(episode.EpisodeNumber)}[/]");
        PlayerManager.Play(epVideoUrl, $"AniCS — {anime.Title} Ep.{episode.EpisodeNumber}", epReferer);
        _history.Record(anime.Title, anime.Url, episode.EpisodeNumber, epVideoUrl);

        if (!allowBinge) return LoopAction.ExitWithFalse;

        return PromptPostPlayAction();
    }

    private static async Task<bool> PlayEpisodesLoop(List<AniCS.Models.Episode> episodes, int startIndex, AniCS.Models.AnimeResult anime, bool allowBinge = true)
    {
        int currentIndex = startIndex;
        while (currentIndex >= 0 && currentIndex < episodes.Count)
        {
            var result = await PlaySingleEpisodeAsync(episodes, currentIndex, anime, allowBinge);
            switch (result)
            {
                case LoopAction.ExitWithTrue:
                    return true;
                case LoopAction.ExitWithFalse:
                    return false;
                case LoopAction.Repeat:
                    continue;
                case LoopAction.Next:
                    currentIndex = GetNextEpisodeIndex(episodes, currentIndex);
                    if (currentIndex < 0 || currentIndex >= episodes.Count)
                    {
                        AnsiConsole.MarkupLine("[yellow]Ya no hay más episodios en esta lista.[/]");
                        return false;
                    }
                    break;
            }
        }
        return false;
    }

    private static async Task DisplayAnimeInfoAsync(AniCS.Models.AnimeResult anime, string? extraInfo = null)
    {
        AnsiConsole.Clear();
        if (!string.IsNullOrEmpty(anime.ThumbnailUrl))
        {
            await AniCS.Terminal.KittyGraphics.DisplayImageAsync(_http, anime.ThumbnailUrl);
            
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                string localImagePath = Path.Combine(Path.GetTempPath(), "anics_thumb.jpg");
                try {
                    var imgBytes = await DataCache.GetImageAsync(_http, anime.ThumbnailUrl);
                    if (imgBytes.Length > 0)
                    {
                        await File.WriteAllBytesAsync(localImagePath, imgBytes);
                        AnsiConsole.MarkupLine($"[bold]Imagen guardada en:[/] [link]{localImagePath}[/]");
                    }
                }
                catch (Exception)
                {
                    // Ignore exceptions during thumbnail downloading/saving as this is non-critical.
                    // The application should still display the anime details even if the image cannot be saved.
                }
            }
        }

        AnsiConsole.Write(new Rule($"[bold]{Markup.Escape(anime.Title)}[/]").RuleStyle("deepskyblue1"));
        AnsiConsole.MarkupLine($"[bold]Anime:[/] {Markup.Escape(anime.Title)}");
        
        if (!string.IsNullOrEmpty(extraInfo))
            AnsiConsole.MarkupLine($"[bold]Info:[/] {Markup.Escape(extraInfo)}");

        string synopsis = string.Empty;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("deepskyblue1"))
            .StartAsync("Obteniendo sinopsis...", async _ =>
            {
                synopsis = await DataCache.GetOrFetchDataAsync($"synopsis_{anime.Url}", TimeSpan.FromMinutes(5), () => _active.GetSynopsisAsync(anime.Url));
            });

        if (!string.IsNullOrEmpty(synopsis))
        {
            var panel = new Panel(Markup.Escape(synopsis))
                .Header("[bold]Sinopsis[/]")
                .BorderColor(Color.DeepSkyBlue1)
                .Padding(1, 1, 1, 1);
            AnsiConsole.Write(panel);
        }
        AnsiConsole.WriteLine();
    }
}
