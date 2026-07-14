using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
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
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        
        // Add Core dependencies (HttpClient, Extractors, Config, PlayerService)
        services.AddAniCSCore();
        
        // CLI specifics
        services.AddSingleton<WatchHistory>();
        services.AddSingleton<AppState>();
        services.AddSingleton<CommandRouter>();

        var serviceProvider = services.BuildServiceProvider();

        var state = serviceProvider.GetRequiredService<AppState>();
        var router = serviceProvider.GetRequiredService<CommandRouter>();

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
                $"yt-dlp: {(state.PlayerService.IsYtDlpAvailable() ? "[green]disponible ✓[/]" : "[grey]no instalado[/]")}  |  " +
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
