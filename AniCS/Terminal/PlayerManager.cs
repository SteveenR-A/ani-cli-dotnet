using System.Diagnostics;

namespace AniCS.Terminal;

public enum PlayerType { Mpv, Vlc, Auto }

/// <summary>
/// Manages launching video players with proper anti-403 headers.
/// Inspired by ani-cli's player abstraction.
///
/// mpv is passed --http-header-fields for Referer because anime video CDNs
/// verify the Referer header and return 403 Forbidden if it's missing.
/// </summary>
public static class PlayerManager
{
    private static readonly string[] PlayerPriority = ["mpv", "vlc", "ffplay"];

    public static PlayerType Detect()
    {
        foreach (var player in PlayerPriority)
        {
            if (IsInstalled(player))
                return player == "mpv" ? PlayerType.Mpv : PlayerType.Vlc;
        }
        return PlayerType.Auto;
    }

    /// <summary>
    /// Launches a video player. Passes Referer header to mpv to avoid 403 on CDN streams.
    /// </summary>
    public static void Play(string url, string? title = null, string? referer = null, PlayerType player = PlayerType.Auto)
    {
        var detected = player == PlayerType.Auto ? Detect() : player;
        var (exe, args) = BuildCommand(detected, url, title, referer);

        try
        {
            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = exe,
                    Arguments              = args,
                    UseShellExecute        = false, // Necesario para redirigir la salida
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true
                }
            };

            Spectre.Console.AnsiConsole.MarkupLine("[dim italic]Reproductor en curso... Cierra la ventana del video para volver a AniCS.[/]");
            p.Start();
            p.WaitForExit(); // Bloquea la app hasta que el video se cierre
            Spectre.Console.AnsiConsole.Clear(); // Limpia la pantalla al volver
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[red]Error al lanzar el reproductor: {ex.Message}[/]");
        }
    }

    private static (string exe, string args) BuildCommand(PlayerType player, string url, string? title, string? referer)
    {
        var refererArg = string.IsNullOrEmpty(referer)
            ? ""
            : $" --http-header-fields=\"Referer: {referer}\"";

        var userAgent = "--user-agent=\"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36\"";

        return player switch
        {
            PlayerType.Mpv => (
                "mpv",
                $"--force-window=yes" +
                $" --cache=yes --demuxer-max-bytes=400M --demuxer-readahead-secs=120" + // Aggressive buffering
                $" --demuxer-lavf-o=http_persistent=0" + // Fixes HLS disconnects
                $" {userAgent}" +
                $"{(title != null ? $" --title=\"{title}\"" : "")}" +
                $"{refererArg}" +
                $" \"{url}\""
            ),
            PlayerType.Vlc => (
                "vlc",
                $"\"{url}\""
            ),
            _ => ("xdg-open", $"\"{url}\"")
        };
    }

    private static bool IsInstalled(string name)
    {
        try
        {
            var isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows);
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName               = isWindows ? "where" : "which",
                Arguments              = name,
                RedirectStandardOutput = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            });
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }
}
