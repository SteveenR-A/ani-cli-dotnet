using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AniCS.Models;
using AniCS.Terminal;
using Spectre.Console;

namespace AniCS.Commands
{
    public class LatestCommand
    {
        private readonly AppState _state;
        private readonly PlaybackController _playback;

        public LatestCommand(AppState state, PlaybackController playback)
        {
            _state = state;
            _playback = playback;
        }

        public async Task ExecuteAsync()
        {
            List<Episode> results = [];

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("deepskyblue1"))
                .StartAsync($"Obteniendo estrenos de [yellow]{_state.ActiveExtractor.Domain}[/]...", async _ =>
                {
                    results = await DataCache.GetOrFetchDataAsync($"latest_{_state.ActiveExtractor.Domain}", TimeSpan.FromMinutes(5), () => _state.ActiveExtractor.GetLatestReleasesAsync());
                });

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No se encontraron estrenos.[/]");
                return;
            }

            var selectedEpisode = await DetailsPrompt.PromptWithDetailsAsync(
                _state.Http,
                $"Últimos Estrenos — {_state.ActiveExtractor.Domain}",
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

            var dummyAnime = new AnimeResult { Title = selectedEpisode.Title, Url = selectedEpisode.Url };
            await _playback.PlayEpisodesLoop([selectedEpisode], 0, dummyAnime, allowBinge: false);
        }
    }
}
