using System.Threading.Tasks;
using AniCS.Models;
using AniCS.Terminal;
using Spectre.Console;

namespace AniCS.Commands
{
    public class CommandRouter
    {
        private readonly AppState _state;
        private readonly PlaybackController _playback;
        private readonly SearchCommand _searchCommand;
        private readonly LatestCommand _latestCommand;
        private readonly ScoopCommand _scoopCommand;
        private readonly HistoryCommand _historyCommand;
        private readonly SourceCommand _sourceCommand;
        private readonly PremieresCommand _premieresCommand;
        private readonly ConfigCommand _configCommand;

        public CommandRouter(AppState state)
        {
            _state = state;
            _playback = new PlaybackController(state);
            
            _searchCommand = new SearchCommand(state, _playback);
            _latestCommand = new LatestCommand(state, _playback);
            _scoopCommand = new ScoopCommand(state, _playback);
            _historyCommand = new HistoryCommand(state, _playback);
            _sourceCommand = new SourceCommand(state);
            _premieresCommand = new PremieresCommand(state, _playback);
            _configCommand = new ConfigCommand();
        }

        public async Task<bool> RouteAsync(string input)
        {
            var parts = input.Split(' ', 2, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return true;

            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : string.Empty;

            switch (cmd)
            {
                case "exit":
                case "quit":
                case "q":
                    AnsiConsole.MarkupLine("[dim]Hasta luego 👋[/]");
                    return false; // Stop the loop

                case "help":
                    UIHelpers.ShowHelp(_state);
                    break;

                case "search":
                case "s":
                    await _searchCommand.ExecuteAsync(arg);
                    break;

                case "latest":
                case "l":
                    await _latestCommand.ExecuteAsync();
                    break;

                case "scoop":
                case "sc":
                    await _scoopCommand.ExecuteAsync();
                    break;

                case "estrenos":
                case "e":
                    await _premieresCommand.ExecuteAsync();
                    break;

                case "history":
                case "h":
                    await _historyCommand.ExecuteAsync();
                    break;

                case "fuente":
                case "f":
                    _sourceCommand.Execute();
                    break;

                case "clearcache":
                case "cc":
                    DataCache.ClearRamCache();
                    AnsiConsole.MarkupLine("[green]Caché de memoria RAM limpiado con éxito.[/]");
                    break;

                case "config":
                case "c":
                    _configCommand.Execute();
                    break;

                case "clear":
                case "cls":
                    AnsiConsole.Clear();
                    break;

                default:
                    AnsiConsole.MarkupLine($"[red]Comando desconocido:[/] '{Markup.Escape(cmd)}'. Escribe [bold]help[/].");
                    break;
            }

            return true; // Continue the loop
        }
    }
}
