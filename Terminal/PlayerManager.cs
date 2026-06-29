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
    private static readonly string[] PlayerPriority = ["mpv", "mpvnet", "vlc", "ffplay"];

    public static PlayerType Detect()
    {
        foreach (var player in PlayerPriority)
        {
            if (IsInstalled(player))
                return (player == "mpv" || player == "mpvnet") ? PlayerType.Mpv : PlayerType.Vlc;
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
            var startInfo = new ProcessStartInfo
            {
                FileName               = exe,
                UseShellExecute        = false
            };

            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            using var p = new Process { StartInfo = startInfo };

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

    private static (string exe, List<string> args) BuildCommand(PlayerType player, string url, string? title, string? referer)
    {
        var args = new List<string>();

        if (player == PlayerType.Mpv)
        {
            var exe = IsInstalled("mpv") ? "mpv" : "mpvnet";
            args.Add("--force-window=yes");
            args.Add("--cache=yes");
            args.Add("--cache-pause-wait=5");
            args.Add("--demuxer-readahead-secs=20");
            args.Add("--demuxer-max-back-bytes=200M");
            args.Add("--user-agent=Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
            
            if (!string.IsNullOrEmpty(title))
            {
                args.Add($"--title={title}");
            }
            if (!string.IsNullOrEmpty(referer))
            {
                args.Add($"--http-header-fields=Referer: {referer}");
            }
            
            args.Add(url);
            return (exe, args);
        }
        else if (player == PlayerType.Vlc)
        {
            args.Add(url);
            return ("vlc", args);
        }
        else
        {
            args.Add(url);
            return ("xdg-open", args);
        }
    }

    public static bool IsInstalled(string name)
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
