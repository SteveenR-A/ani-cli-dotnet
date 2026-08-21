using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AniCS.Resolver.Native;

/// <summary>
/// Parsea manifiestos HLS (.m3u8) para seleccionar el stream de la calidad deseada
/// y obtener la lista de segmentos a descargar.
/// </summary>
public static class HlsParser
{
    // ──────────────────────────────────────────────────────────────────────────
    // Tipos de resultado
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Variante de un manifiesto master (resolución + URL de la playlist de medios).</summary>
    public record StreamVariant(
        int Width,
        int Height,
        long Bandwidth,
        string MediaPlaylistUrl
    );

    /// <summary>Resultado del parseo: URL de la playlist de medios elegida + sus segmentos.</summary>
    public record ParseResult(
        string MediaPlaylistUrl,
        List<string> SegmentUrls,
        bool IsEncrypted  // true si #EXT-X-KEY está presente (AES-128)
    );

    // ──────────────────────────────────────────────────────────────────────────
    // API pública
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A partir de una URL (master o media playlist), resuelve los segmentos a descargar
    /// respetando la calidad preferida.
    /// </summary>
    /// <param name="m3u8Url">URL del .m3u8 (master o media playlist)</param>
    /// <param name="preferredHeight">Altura máxima deseada (0 = mejor disponible)</param>
    /// <param name="client">HttpClient con headers ya configurados</param>
    public static async Task<ParseResult?> ParseAsync(
        string m3u8Url,
        int preferredHeight,
        HttpClient client,
        CancellationToken ct = default)
    {
        var content = await FetchTextAsync(client, m3u8Url, ct);
        if (string.IsNullOrEmpty(content)) return null;

        // ¿Es un master playlist o una media playlist directa?
        if (content.Contains("#EXT-X-STREAM-INF"))
        {
            // Es un master playlist — elegir la mejor variante
            var variants = ParseMasterPlaylist(content, m3u8Url);
            if (variants.Count == 0) return null;

            var chosen = ChooseBestVariant(variants, preferredHeight);
            var mediaContent = await FetchTextAsync(client, chosen.MediaPlaylistUrl, ct);
            if (string.IsNullOrEmpty(mediaContent)) return null;

            var segments = ParseMediaPlaylist(mediaContent, chosen.MediaPlaylistUrl);
            bool encrypted = mediaContent.Contains("#EXT-X-KEY");
            return new ParseResult(chosen.MediaPlaylistUrl, segments, encrypted);
        }
        else
        {
            // Ya es una media playlist directa
            var segments = ParseMediaPlaylist(content, m3u8Url);
            bool encrypted = content.Contains("#EXT-X-KEY");
            return new ParseResult(m3u8Url, segments, encrypted);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Master Playlist
    // ──────────────────────────────────────────────────────────────────────────

    private static List<StreamVariant> ParseMasterPlaylist(string content, string baseUrl)
    {
        var variants = new List<StreamVariant>();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < lines.Length - 1; i++)
        {
            var line = lines[i].Trim();
            if (!line.StartsWith("#EXT-X-STREAM-INF")) continue;

            var nextLine = lines[i + 1].Trim();
            if (nextLine.StartsWith("#")) continue; // línea de tag, no URL

            long bandwidth = ParseTagLong(line, "BANDWIDTH");
            int width = 0, height = 0;

            var resMatch = Regex.Match(line, @"RESOLUTION=(\d+)x(\d+)", RegexOptions.IgnoreCase);
            if (resMatch.Success)
            {
                int.TryParse(resMatch.Groups[1].Value, out width);
                int.TryParse(resMatch.Groups[2].Value, out height);
            }

            string mediaUrl = ResolveUrl(nextLine, baseUrl);
            variants.Add(new StreamVariant(width, height, bandwidth, mediaUrl));
        }

        return variants;
    }

    /// <summary>
    /// Elige la variante más apropiada según la altura preferida.
    /// - preferredHeight = 0 → la de mayor resolución disponible.
    /// - preferredHeight > 0 → la más alta que no exceda ese valor; si no hay ninguna, la más baja.
    /// </summary>
    private static StreamVariant ChooseBestVariant(List<StreamVariant> variants, int preferredHeight)
    {
        if (preferredHeight <= 0)
            return variants.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();

        // Filtrar las que caben en la altura deseada
        var fitting = variants.Where(v => v.Height <= preferredHeight).ToList();
        if (fitting.Count > 0)
            return fitting.OrderByDescending(v => v.Height).ThenByDescending(v => v.Bandwidth).First();

        // Si ninguna cabe, dar la de menor resolución
        return variants.OrderBy(v => v.Height).First();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Media Playlist (segmentos)
    // ──────────────────────────────────────────────────────────────────────────

    private static List<string> ParseMediaPlaylist(string content, string baseUrl)
    {
        var segments = new List<string>();
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

            // Las líneas de URL pueden ser absolutas o relativas
            segments.Add(ResolveUrl(line, baseUrl));
        }
        return segments;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<string?> FetchTextAsync(HttpClient client, string url, CancellationToken ct)
    {
        try
        {
            var resp = await client.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            AppLogger.Debug("HlsParser", $"FetchTextAsync failed for '{url}': {ex.Message}");
            return null;
        }
    }

    /// <summary>Resuelve una URL relativa usando la URL base del manifiesto.</summary>
    public static string ResolveUrl(string url, string baseUrl)
    {
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        try { return new Uri(new Uri(baseUrl), url).ToString(); }
        catch (Exception ex)
        {
            AppLogger.Debug("HlsParser", $"ResolveUrl failed for '{url}' with base '{baseUrl}': {ex.Message}");
            return url;
        }
    }

    private static long ParseTagLong(string tag, string key)
    {
        var m = Regex.Match(tag, $@"{key}=(\d+)", RegexOptions.IgnoreCase);
        return m.Success && long.TryParse(m.Groups[1].Value, out long val) ? val : 0;
    }
}
