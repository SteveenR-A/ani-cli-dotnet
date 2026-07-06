using System.Linq;
using AniCS.Models;
using Spectre.Console;

namespace AniCS.Commands
{
    public class SourceCommand
    {
        private readonly AppState _state;

        public SourceCommand(AppState state)
        {
            _state = state;
        }

        public void Execute()
        {
            var sourceNames = _state.Extractors.Select(e => e.Domain).ToList();
            sourceNames.Add("[red]Cancelar[/]");

            var selected = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Selecciona una fuente:[/]")
                    .PageSize(10)
                    .HighlightStyle(Style.Parse("yellow bold"))
                    .AddChoices(sourceNames));

            if (selected == "[red]Cancelar[/]") return;

            var match = _state.Extractors.First(e => e.Domain == selected);
            _state.ActiveExtractor = match;
            AnsiConsole.MarkupLine($"[green]Fuente cambiada a:[/] [bold]{_state.ActiveExtractor.Domain}[/]");
        }
    }
}
