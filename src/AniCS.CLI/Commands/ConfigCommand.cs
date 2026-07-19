using System.Text.Json;
using AniCS.Models;
using Spectre.Console;

namespace AniCS.Commands
{
    public class ConfigCommand
    {
        public void Execute()
        {
            var config = ConfigManager.Current;
            var options = new JsonSerializerOptions { WriteIndented = true };
            var context = new AppConfigJsonContext(options);
            var json = JsonSerializer.Serialize(config, context.AppConfig);

            var panel = new Panel($"[green]{Markup.Escape(json)}[/]")
            {
                Header = new PanelHeader("[bold]Configuración Actual (config.json)[/]"),
                Border = BoxBorder.Rounded,
                Padding = new Padding(1, 1, 1, 1)
            };

            AnsiConsole.Write(panel);
            AnsiConsole.MarkupLine("[dim]Para cambiar la configuración de forma gráfica, por favor usa la versión AniCS.Desktop.[/]");
        }
    }
}
