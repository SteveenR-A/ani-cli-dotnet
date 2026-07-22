using System;
using System.IO;
using System.Threading.Tasks;
using AniCS.Models;
using Spectre.Console;

namespace AniCS.Terminal
{
    public static class UIHelpers
    {
        public static void ShowHelp(AppState state)
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
            table.AddRow("[bold]estrenos[/]",           "[grey]e[/]",   "Nuevas series y estrenos");
            table.AddRow("[bold]top[/]",                "[grey]t[/]",   "Ranking Top Anime más votados");
            table.AddRow("[bold]directorio[/]",         "[grey]dir[/]", "Explorar catálogo con filtros (género, estado, tipo)");
            table.AddRow("[bold]scoop[/]",              "[grey]sc[/]",  "Cartelera semanal de estrenos");
            table.AddRow("[bold]history[/]",            "[grey]h[/]",   "Ver historial de animes vistos");
            table.AddRow("[bold]fuente[/]",             "[grey]f[/]",   "Cambiar la fuente activa");
            table.AddRow("[bold]config[/]",             "[grey]c[/]",   "Ver la configuración actual");
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

            foreach (var ext in state.Extractors)
            {
                bool isActive = ext.Domain == state.ActiveExtractor.Domain;
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

        public static async Task DisplayAnimeInfoAsync(AppState state, AnimeResult anime, string? extraInfo = null)
        {
            AnsiConsole.Clear();
            if (!string.IsNullOrEmpty(anime.ThumbnailUrl))
            {
                await KittyGraphics.DisplayImageAsync(state.Http, anime.ThumbnailUrl);
                
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    string localImagePath = Path.Combine(Path.GetTempPath(), "anics_thumb.jpg");
                    try {
                        var imgBytes = await DataCache.GetImageAsync(state.Http, anime.ThumbnailUrl);
                        if (imgBytes.Length > 0)
                        {
                            await File.WriteAllBytesAsync(localImagePath, imgBytes);
                            AnsiConsole.MarkupLine($"[bold]Imagen guardada en:[/] [link]{localImagePath}[/]");
                        }
                    }
                    catch (Exception)
                    {
                        // Ignore exceptions during thumbnail downloading/saving as this is non-critical.
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
                    synopsis = await DataCache.GetOrFetchDataAsync($"synopsis_{anime.Url}", TimeSpan.FromMinutes(5), () => state.ActiveExtractor.GetSynopsisAsync(anime.Url));
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
}
