using System.Diagnostics;

namespace AniCS.Terminal;

/// <summary>
/// Delegates video URL resolution to yt-dlp when the internal extractor fails.
/// yt-dlp handles dozens of video hosts (Streamtape, VOE, Mp4upload, Filemoon, etc.)
/// and has proven anti-bot techniques (proper TLS, cookie handling, etc.) built in.
/// This is the safest approach for resolving iframe-embedded video hosts.
/// </summary>
public static class YtDlpResolver
{
    public static bool IsAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName              = "yt-dlp",
                Arguments             = "--version",
                RedirectStandardOutput = true,
                UseShellExecute       = false
            });
            p?.WaitForExit();
            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    /// <summary>
    /// Asks yt-dlp to resolve the best video URL from a given page URL.
    /// Returns empty string if yt-dlp is not installed or fails.
    /// </summary>
    public static async Task<string> ResolveAsync(string pageUrl, string? referer = null)
    {
        try
        {
            var refererArg = string.IsNullOrEmpty(referer) ? "" : $"--add-header \"Referer:{referer}\" ";
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName              = "yt-dlp",
                    Arguments             = $"-g --no-warnings {refererArg}\"{pageUrl}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute       = false,
                    CreateNoWindow        = true
                }
            };

            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var url = output.Trim().Split('\n').FirstOrDefault(l => l.StartsWith("http"))?.Trim();
            return url ?? string.Empty;
        }
        catch { return string.Empty; }
    }

    /// <summary>
    /// Plays a URL directly via yt-dlp piped to mpv, the most reliable way to stream
    /// from hosts with complex anti-hotlinking measures.
    /// Equivalent to: yt-dlp -o - URL | mpv -
    /// </summary>
    public static void PlayWithYtDlp(string pageUrl, string? title = null, string? referer = null)
    {
        try
        {
            var refererArg = string.IsNullOrEmpty(referer) ? "" : $"--add-header \"Referer:{referer}\" ";
            var titleArg   = string.IsNullOrEmpty(title)   ? "" : $"--title \"{title}\" ";

            var mpvBin = PlayerManager.Detect() == PlayerType.Mpv && !PlayerManager.IsInstalled("mpv") ? "mpvnet" : "mpv";

            // Use shell pipeline: yt-dlp -o - URL | mpv -
            Process.Start(new ProcessStartInfo
            {
                FileName  = "bash",
                Arguments = $"-c \"yt-dlp {refererArg}-o - '{pageUrl}' | {mpvBin} {titleArg}--force-window=yes -\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[red]Error al lanzar yt-dlp + mpv: {ex.Message}[/]");
        }
    }

    /// <summary>
    /// Uses yt-dlp to download the video to a specific directory.
    /// </summary>
    public static void Download(string videoUrl, string animeTitle, string epNum, string downloadDir, string? referer = null)
    {
        try
        {
            Directory.CreateDirectory(downloadDir);

            // Clean title for filename
            var safeTitle = string.Join("_", animeTitle.Split(Path.GetInvalidFileNameChars()));
            var filename = $"[AniCS] {safeTitle} - Ep {epNum}.%(ext)s";
            var outputPath = Path.Combine(downloadDir, filename);

            var refererArg = string.IsNullOrEmpty(referer) ? "" : $"--add-header \"Referer:{referer}\" ";

            Spectre.Console.AnsiConsole.MarkupLine($"[dim]Descargando en:[/] [bold]{downloadDir}[/]");

            using var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = "yt-dlp",
                    Arguments              = $"{refererArg}-o \"{outputPath}\" \"{videoUrl}\"",
                    UseShellExecute        = false,
                }
            };
            p.Start();
            p.WaitForExit();
        }
        catch (Exception ex)
        {
            Spectre.Console.AnsiConsole.MarkupLine($"[red]Error al descargar: {ex.Message}[/]");
        }
    }
}
