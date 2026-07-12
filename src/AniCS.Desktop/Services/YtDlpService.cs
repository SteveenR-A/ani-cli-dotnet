using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AniCS.Desktop.Services;

/// <summary>
/// Resolución de URLs de video vía yt-dlp para servidores externos.
/// Actúa como fallback cuando el extractor interno falla.
/// Compatible con Windows (no usa bash/pipeline).
/// </summary>
public static class YtDlpService
{
    private static string? _cachedPath;

    /// <summary>
    /// Devuelve la ruta al ejecutable de yt-dlp, o null si no está instalado.
    /// </summary>
    public static string? GetExecutablePath()
    {
        if (_cachedPath != null) return _cachedPath;

        string exe = OperatingSystem.IsWindows() ? "yt-dlp.exe" : "yt-dlp";

        // Buscar en PATH
        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(System.IO.Path.PathSeparator);
        if (paths != null)
        {
            foreach (var dir in paths)
            {
                var full = System.IO.Path.Combine(dir.Trim(), exe);
                if (System.IO.File.Exists(full))
                {
                    _cachedPath = full;
                    return full;
                }
            }
        }

        // Buscar junto al ejecutable de la app
        var localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, exe);
        if (System.IO.File.Exists(localPath))
        {
            _cachedPath = localPath;
            return localPath;
        }

        return null;
    }

    /// <summary>
    /// Comprueba si yt-dlp está disponible en el sistema.
    /// </summary>
    public static bool IsAvailable() => GetExecutablePath() != null;

    /// <summary>
    /// Usa yt-dlp para resolver la URL directa de video de una página de servidor.
    /// Equivale a: yt-dlp -g --no-warnings URL
    /// Retorna la primera URL http encontrada, o string.Empty si falla.
    /// </summary>
    public static async Task<string> ResolveAsync(string pageUrl, string? referer = null)
    {
        var ytdlp = GetExecutablePath();
        if (ytdlp == null) return string.Empty;

        try
        {
            var si = new ProcessStartInfo
            {
                FileName = ytdlp,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            si.ArgumentList.Add("-g");
            si.ArgumentList.Add("--no-warnings");
            si.ArgumentList.Add("--no-playlist");

            if (!string.IsNullOrEmpty(referer))
            {
                si.ArgumentList.Add("--add-header");
                si.ArgumentList.Add($"Referer:{referer}");
            }

            si.ArgumentList.Add(pageUrl);

            using var p = new Process { StartInfo = si };
            p.Start();

            var output = await p.StandardOutput.ReadToEndAsync();
            _ = p.StandardError.ReadToEndAsync(); // drain stderr

            await p.WaitForExitAsync();

            if (p.ExitCode == 0)
            {
                // yt-dlp -g puede devolver varias URLs (audio + video separados en DASH).
                // Tomamos la primera URL http que parezca un video
                var url = output
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .FirstOrDefault(l => l.StartsWith("http"));
                return url ?? string.Empty;
            }

            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
