using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AniCS.Models;
using AniCS.Terminal;
using Spectre.Console;

namespace AniCS.Commands
{
    public class DirectoryCommand
    {
        private readonly AppState _state;
        private readonly PlaybackController _playback;

        public DirectoryCommand(AppState state, PlaybackController playback)
        {
            _state = state;
            _playback = playback;
        }

        public async Task ExecuteAsync()
        {
            var filterChoice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[yellow bold]Directorio y Filtros Avanzados[/]")
                    .PageSize(10)
                    .AddChoices([
                        "Todos los Animes",
                        "Por Género (Acción, Comedia, Fantasía, etc.)",
                        "Por Estado (En emisión, Concluido)",
                        "Por Tipo (TV, Película, OVA, Especial)",
                        "Cancelar"
                    ]));

            if (filterChoice == "Cancelar") return;

            var filters = new SearchFilters();

            if (filterChoice.StartsWith("Por Género"))
            {
                var genre = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Selecciona un género")
                        .PageSize(15)
                        .AddChoices([
                            "accion", "aventura", "comedia", "drama", "fantasia",
                            "ciencia-ficcion", "romance", "recuentos-de-la-vida",
                            "sobrenatural", "terror", "misterio", "deportes", "mecha", "musica"
                        ]));
                filters.Genre = genre;
            }
            else if (filterChoice.StartsWith("Por Estado"))
            {
                var status = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Selecciona estado")
                        .AddChoices(["1", "2"]) // 1=En emision, 2=Concluido
                );
                filters.Status = status;
            }
            else if (filterChoice.StartsWith("Por Tipo"))
            {
                var type = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("Selecciona tipo")
                        .AddChoices(["tv", "movie", "ova", "special"])
                );
                filters.Type = type;
            }

            List<AnimeResult> results = [];

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync($"Filtrando catálogo en [yellow]{_state.ActiveExtractor.Domain}[/]...", async _ =>
                {
                    results = await DataCache.GetOrFetchDataAsync($"dir_{_state.ActiveExtractor.Domain}_{filters.Genre}_{filters.Status}_{filters.Type}", TimeSpan.FromMinutes(5), () => _state.ActiveExtractor.AdvancedSearchAsync(filters));
                });

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No se encontraron animes con los filtros seleccionados.[/]");
                return;
            }

            var anime = await DetailsPrompt.PromptWithDetailsAsync(
                _state.Http,
                $"Directorio — {_state.ActiveExtractor.Domain}",
                results,
                r => $"[white]{Markup.Escape(r.Title)}[/]",
                r => r.ThumbnailUrl,
                async r => await DataCache.GetOrFetchDataAsync($"synopsis_{r.Url}", TimeSpan.FromMinutes(5), () => _state.ActiveExtractor.GetSynopsisAsync(r.Url)),
                r => r.Description
            );

            if (anime == null) return;

            await UIHelpers.DisplayAnimeInfoAsync(_state, anime);

            // Load episodes
            List<Episode> episodes = [];
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync("Cargando episodios...", async _ =>
                {
                    episodes = await DataCache.GetOrFetchDataAsync($"eps_{anime.Url}", TimeSpan.FromMinutes(5), () => _state.ActiveExtractor.GetEpisodesAsync(anime.Url));
                });

            if (episodes.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No se pudo obtener la lista de episodios automáticamente.[/]");
                if (AnsiConsole.Confirm("¿Reproducir URL de la página del anime directamente?"))
                {
                    await _playback.PlayEpisodesLoop([new Episode { Url = anime.Url, Title = "Direct URL", EpisodeNumber = "1" }], 0, anime, false);
                }
                return;
            }

            while (true)
            {
                var selectedEpisode = await DetailsPrompt.PromptWithDetailsAsync(
                    _state.Http,
                    "Selecciona un episodio",
                    episodes,
                    e => $"[white]{Markup.Escape($"Ep {e.EpisodeNumber} — {e.Title}".TrimEnd('—', ' '))}[/]",
                    e => e.ThumbnailUrl,
                    e => Task.FromResult(string.Empty),
                    null,
                    showImage: false
                );

                if (selectedEpisode == null) return;

                var epIndex = episodes.IndexOf(selectedEpisode);

                bool exitToMain = await _playback.PlayEpisodesLoop(episodes, epIndex, anime, allowBinge: true);
                if (exitToMain) return;
            }
        }
    }
}
