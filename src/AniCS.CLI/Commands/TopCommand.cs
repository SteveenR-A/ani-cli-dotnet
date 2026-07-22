using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AniCS.Models;
using AniCS.Terminal;
using Spectre.Console;

namespace AniCS.Commands
{
    public class TopCommand
    {
        private readonly AppState _state;
        private readonly PlaybackController _playback;

        public TopCommand(AppState state, PlaybackController playback)
        {
            _state = state;
            _playback = playback;
        }

        public async Task ExecuteAsync()
        {
            List<AnimeResult> results = [];

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync($"Obteniendo Ranking Top de [yellow]{_state.ActiveExtractor.Domain}[/]...", async _ =>
                {
                    results = await DataCache.GetOrFetchDataAsync($"top_{_state.ActiveExtractor.Domain}", TimeSpan.FromMinutes(5), () => _state.ActiveExtractor.GetTopAnimesAsync("all", "all", 1));
                });

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No se encontraron resultados en el Ranking Top para esta fuente.[/]");
                return;
            }

            var anime = await DetailsPrompt.PromptWithDetailsAsync(
                _state.Http,
                $"Ranking Top Anime — {_state.ActiveExtractor.Domain}",
                results,
                r => $"[gold1]#{r.Rank}[/] [white]{Markup.Escape(r.Title)}[/] [dim]({r.Votes} votos)[/]",
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
