using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AniCS.Models;
using Spectre.Console;

namespace AniCS.Terminal
{
    public class PlaybackController
    {
        private readonly AppState _state;

        public PlaybackController(AppState state)
        {
            _state = state;
        }

        public enum LoopAction
        {
            ExitWithTrue,
            ExitWithFalse,
            Repeat,
            Next
        }

        public async Task<bool> PlayEpisodesLoop(List<Episode> episodes, int startIndex, AnimeResult anime, bool allowBinge = true)
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

        private async Task<LoopAction> PlaySingleEpisodeAsync(List<Episode> episodes, int currentIndex, AnimeResult anime, bool allowBinge)
        {
            var episode = episodes[currentIndex];

            AnsiConsole.Clear();
            var selectedServer = await PromptServerSelection(episode.Url);
            if (selectedServer == null) return LoopAction.ExitWithFalse;

            var (epVideoUrl, epReferer) = await ResolveWithStatus(selectedServer.Url, selectedServer.Name);
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
            await _state.PlayerService.PlayAsync(epVideoUrl, $"AniCS — {anime.Title} Ep.{episode.EpisodeNumber}", epReferer);
            _state.History.Record(anime.Title, anime.Url, anime.ThumbnailUrl, episode.EpisodeNumber, epVideoUrl);

            if (!allowBinge) return LoopAction.ExitWithFalse;

            return PromptPostPlayAction();
        }

        private LoopAction PromptPostPlayAction()
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

        private void HandleDownload(VideoServer selectedServer, string epVideoUrl, string epReferer, AnimeResult anime, Episode episode)
        {
            if (selectedServer.Name.Contains("Mega", StringComparison.OrdinalIgnoreCase))
            {
                AnsiConsole.MarkupLine("[yellow]Mega cifra sus videos. Para descargar, abre este enlace en tu navegador o usa megatools:[/]");
                AnsiConsole.MarkupLine($"[blue underline]{Markup.Escape(selectedServer.Url)}[/]");
            }
            else
            {
                var defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AniCS");
                var dirPrompt = AnsiConsole.Prompt(
                    new TextPrompt<string>($"[bold]Ruta de descarga:[/] [grey](Presiona Enter para usar por defecto)[/]")
                        .DefaultValue(defaultDir)
                        .AllowEmpty());

                var targetDir = string.IsNullOrWhiteSpace(dirPrompt) ? defaultDir : dirPrompt;

                _state.PlayerService.Download(epVideoUrl, anime.Title, episode.EpisodeNumber, targetDir, epReferer);
                _state.History.Record(anime.Title, anime.Url, anime.ThumbnailUrl, episode.EpisodeNumber, epVideoUrl);
            }
        }

        private int GetNextEpisodeIndex(List<Episode> episodes, int currentIndex)
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

        private async Task<VideoServer?> PromptServerSelection(string url)
        {
            List<VideoServer> servers = [];
            await AnsiConsole.Status()
                .StartAsync("Obteniendo servidores...", async _ =>
                {
                    servers = await _state.ActiveExtractor.GetVideoServersAsync(url);
                });

            if (servers.Count == 0)
            {
                AnsiConsole.MarkupLine("[red]No se encontraron servidores de video.[/]");
                return null;
            }

            if (servers.Count == 1)
                return servers[0];

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

        private async Task<(string url, string referer)> ResolveWithStatus(string episodeUrl, string serverName)
        {
            string videoUrl = string.Empty;
            string referer = episodeUrl;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("green"))
                .StartAsync("Resolviendo enlace de video (extractor interno)...", async _ =>
                {
                    videoUrl = await _state.ActiveExtractor.ResolveVideoUrlAsync(episodeUrl);
                });

            if (string.IsNullOrEmpty(videoUrl) && _state.PlayerService.IsYtDlpAvailable())
            {
                var ytdlpServers = new[] { "Streamtape", "VOE", "Mp4upload", "Mixdrop" };
                if (ytdlpServers.Any(s => serverName.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    AnsiConsole.MarkupLine($"[grey]Intentando extraer '{serverName}' mediante yt-dlp...[/]");
                    try
                    {
                        videoUrl = await _state.PlayerService.ResolveVideoUrlWithYtDlpAsync(episodeUrl, referer);
                    }
                    catch
                    {
                        videoUrl = string.Empty;
                    }
                }
            }

            return (videoUrl, referer);
        }
    }
}
