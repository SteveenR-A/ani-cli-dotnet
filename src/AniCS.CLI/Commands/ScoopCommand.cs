using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AniCS.Models;
using AniCS.Terminal;
using Spectre.Console;

namespace AniCS.Commands
{
    public class ScoopCommand
    {
        private readonly AppState _state;
        private readonly PlaybackController _playback;

        public ScoopCommand(AppState state, PlaybackController playback)
        {
            _state = state;
            _playback = playback;
        }

        public async Task ExecuteAsync()
        {
            List<ScheduleItem> results = [];

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync($"Cargando cartelera semanal de [yellow]{_state.ActiveExtractor.Domain}[/]...", async _ =>
                {
                    results = await DataCache.GetOrFetchDataAsync($"scoop_{_state.ActiveExtractor.Domain}", TimeSpan.FromMinutes(5), () => _state.ActiveExtractor.GetWeeklyScoopAsync());
                });

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No se encontró información del calendario.[/]");
                return;
            }

            var daysOfWeek = new List<string> { "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo" };
            var orderedResults = results.OrderByDescending(x => daysOfWeek.IndexOf(x.Day.ToLowerInvariant())).ToList();

            var options = orderedResults.Select(r => 
                $"[{GetDayColor(r.Day)}]{r.Day,-10}[/] │ {r.Title}"
            ).ToList();
            options.Add("[red]Cancelar[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold deepskyblue1]Cartelera Semanal (Más recientes primero)[/]")
                    .PageSize(15)
                    .HighlightStyle(Style.Parse("deepskyblue1 bold"))
                    .AddChoices(options));

            if (selected == "[red]Cancelar[/]") return;

            var selectedIndex = options.IndexOf(selected);
            var item = orderedResults[selectedIndex];
            var anime = new AnimeResult { Title = item.Title, Url = item.Url, ThumbnailUrl = item.ThumbnailUrl };

            await UIHelpers.DisplayAnimeInfoAsync(_state, anime, $"Emisión el {Markup.Escape(item.Day)}");

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

            var selectedEpisode = await DetailsPrompt.PromptWithDetailsAsync(
                _state.Http,
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
            await _playback.PlayEpisodesLoop(episodes, epIndex, anime);
        }

        private static string GetDayColor(string day)
        {
            return day.ToLowerInvariant() switch {
                "lunes" => "green",
                "martes" => "yellow",
                "miércoles" => "orange1",
                "jueves" => "magenta1",
                "viernes" => "red",
                "sábado" => "cornflowerblue",
                "domingo" => "deepskyblue1",
                _ => "white"
            };
        }
    }
}
