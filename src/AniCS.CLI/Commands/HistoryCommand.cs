using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AniCS.Models;
using AniCS.Terminal;
using Spectre.Console;

namespace AniCS.Commands
{
    public class HistoryCommand
    {
        private readonly AppState _state;
        private readonly PlaybackController _playback;

        public HistoryCommand(AppState state, PlaybackController playback)
        {
            _state = state;
            _playback = playback;
        }

        public async Task ExecuteAsync()
        {
            var entries = _state.History.GetAll();
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

            var dummyAnime = new AnimeResult { Title = selectedEntry.AnimeTitle, Url = selectedEntry.AnimeUrl };
            await UIHelpers.DisplayAnimeInfoAsync(_state, dummyAnime, "Desde el historial de visualización");

            List<Episode> episodes = [];
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync("Cargando lista de episodios...", async _ =>
                {
                    episodes = await _state.ActiveExtractor.GetEpisodesAsync(selectedEntry.AnimeUrl);
                });

            if (episodes.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No se pudieron obtener los episodios. Intentando reproducir el enlace directamente...[/]");
                await _playback.PlayEpisodesLoop([new Episode { Url = selectedEntry.AnimeUrl, Title = "Direct URL", EpisodeNumber = "1" }], 0, dummyAnime, false);
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
                bool exitToMain = await _playback.PlayEpisodesLoop(episodes, epIndex, dummyAnime, allowBinge: true);
                if (exitToMain) return;
            }
        }
    }
}
