using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using AniCS.Commands;
using AniCS.Extractors;
using AniCS.History;
using AniCS.Models;
using AniCS.Terminal;
using Spectre.Console;

namespace AniCS;

public class Program
{
    public static async Task Main(string[] args)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        
        List<IAnimeExtractor> extractors =
        [
            new JKAnimeExtractor(http),
            new AnimeAV1Extractor(http),
        ];
        var defaultConfig = ConfigManager.Current;
        var active = extractors.FirstOrDefault(e => e.Domain.Contains(defaultConfig.DefaultExtractor, StringComparison.OrdinalIgnoreCase)) ?? extractors[0];
        var history = new WatchHistory();

        var state = new AppState(http, extractors, active, history);
        var router = new CommandRouter(state);

        try
        {
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
                $"Fuente activa: [bold yellow]{state.ActiveExtractor.Domain}[/]  |  " +
                $"yt-dlp: {(YtDlpResolver.IsAvailable() ? "[green]disponible ✓[/]" : "[grey]no instalado[/]")}  |  " +
                "Escribe [bold yellow]help[/] para los comandos o [bold red]exit[/] para salir.")
            {
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 0)
            });

            AnsiConsole.WriteLine();

            // ── REPL Loop ─────────────────────────────────────────────
            bool isRunning = true;
            while (isRunning)
            {
                var raw = AnsiConsole.Ask<string>($"[bold deepskyblue1]anics[/] [grey]>[/] ").Trim();
                isRunning = await router.RouteAsync(raw);
            }
        }
        finally
        {
            DataCache.CleanupImageCache(ConfigManager.Current.MaxImageCacheCount);
        }
    }
}
