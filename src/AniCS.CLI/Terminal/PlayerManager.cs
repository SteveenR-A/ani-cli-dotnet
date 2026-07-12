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
        var configPlayer = ConfigManager.Current.DefaultPlayer.ToLower();
        var detected = player == PlayerType.Auto ? 
            (configPlayer == "mpv" ? PlayerType.Mpv : configPlayer == "vlc" ? PlayerType.Vlc : Detect()) 
            : player;
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

            string displayUrl = url.Length > 60 ? url.Substring(0, 57) + "..." : url;
            Spectre.Console.AnsiConsole.MarkupLine($"[dim italic]Reproduciendo:[/] [link={Spectre.Console.Markup.Escape(url)}]{Spectre.Console.Markup.Escape(displayUrl)}[/]");
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
            var exe = !string.IsNullOrEmpty(ConfigManager.Current.CustomPlayerExePath) ? ConfigManager.Current.CustomPlayerExePath : (IsInstalled("mpv") ? "mpv" : "mpvnet");
            args.Add("--force-window=yes");
            args.Add("--cache=yes");
            // 2 s es suficiente para que el CDN entregue el siguiente chunk sin crear
            // el ciclo pausa-5s-descarga-2s que causaba la sensación de "congelado permanente"
            args.Add("--cache-pause-wait=2");
            
            bool isJkAnime = referer != null && referer.Contains("jkanime.net", StringComparison.OrdinalIgnoreCase);
            bool isMediafire = referer != null && referer.Contains("mediafire.com", StringComparison.OrdinalIgnoreCase);
            
            List<string> headers = new List<string>();
            if (!string.IsNullOrEmpty(referer))
            {
                headers.Add($"Referer: {referer}");
            }

            if (isJkAnime || isMediafire)
            {
                // Buffer grande: evita que mpv llegue al borde del buffer con la
                // latencia alta del CDN de MediaFire (antes era 5 M → congelaba a los ~30 s)
                args.Add("--demuxer-max-bytes=150M");
                args.Add("--demuxer-max-back-bytes=50M");    // permite rebobinar sin re-descargar
                args.Add("--demuxer-readahead-secs=120");    // pre-descarga hasta 2 min
                args.Add("--cache-secs=120");

                // Optimización de Seek y Red para Streaming HTTP:
                //   hr-seek=check-cache: Hace un seek preciso sólo si ya está en caché. Si está fuera,
                //   hace seek rápido al keyframe más cercano sin tener que descargar todos los bytes intermedios.
                args.Add("--hr-seek=check-cache");
                args.Add("--demuxer-seekable=yes");
                args.Add("--network-timeout=15");            // Si el seek tarda más de 15s, reconecta en vez de quedarse congelado.

                // Reconexión robusta:
                //   reconnect_on_http_error cubre 403 (URL firmada expirada) y errores 5xx del CDN
                //   reconnect_delay_max=10 evita flood pero no hace esperar demasiado
                args.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_on_http_error=403,5xx,reconnect_delay_max=10");

                // Cabeceras mínimas necesarias para no recibir 403 del CDN.
                // NOTA: NO incluir 'Connection: keep-alive' — mpv gestiona sus propias
                // conexiones y esa cabecera entra en conflicto con su lógica de reconexión,
                // causando que el servidor cierre la conexión tras el primer chunk (~30 s).
                string origin = isJkAnime ? "https://jkanime.net" : "https://www.mediafire.com";
                headers.Add($"Origin: {origin}");
                headers.Add("Accept-Language: es-419,es;q=0.9,en;q=0.8");
                headers.Add("Accept: */*");
                headers.Add("Sec-Fetch-Dest: video");
                headers.Add("Sec-Fetch-Mode: no-cors");
                headers.Add("Sec-Fetch-Site: cross-site");
            }
            else
            {
                // Configuración estándar para AnimeAV1 y otros
                args.Add("--demuxer-max-bytes=150M");
                args.Add("--demuxer-max-back-bytes=50M");
                args.Add("--demuxer-readahead-secs=60");
            }

            args.Add($":http-user-agent={ConfigManager.Current.RandomUserAgent}");
            
            if (!string.IsNullOrEmpty(title))
            {
                args.Add($"--title={title}");
            }
            
            if (headers.Count > 0)
            {
                args.Add($"--http-header-fields={string.Join(",", headers)}");
            }
            
            args.Add(url);
            return (exe, args);
        }
        else if (player == PlayerType.Vlc)
        {
            var exe = !string.IsNullOrEmpty(ConfigManager.Current.CustomPlayerExePath) ? ConfigManager.Current.CustomPlayerExePath : "vlc";
            args.Add(url);
            return (exe, args);
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
