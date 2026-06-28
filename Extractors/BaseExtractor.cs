using AniCS.Models;
using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace AniCS.Extractors;

/// <summary>
/// Abstract base class inspired by yt-dlp's InfoExtractor pattern.
/// Centralizes HTTP, anti-bot and regex logic so individual extractors stay clean.
/// </summary>
public abstract class BaseExtractor : IAnimeExtractor
{
    private static readonly string[] UserAgents =
    [
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64; rv:126.0) Gecko/20100101 Firefox/126.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Edg/124.0.0.0",
    ];

    private static int _uaIndex = 0;
    private static readonly Random _rng = new();
    protected readonly HttpClient Http;

    /// <summary>
    /// Returns a jitter delay between minMs and maxMs to mimic human browsing pace.
    /// Prevents bot-detection systems from recognizing fixed request intervals.
    /// </summary>
    protected static Task JitterAsync(int minMs = 800, int maxMs = 2200)
        => Task.Delay(_rng.Next(minMs, maxMs));

    protected BaseExtractor(HttpClient client)
    {
        Http = client;
    }

    public abstract string Domain { get; }
    public abstract Task<List<AnimeResult>> SearchAsync(string query);
    public abstract Task<List<Episode>> GetLatestReleasesAsync();
    public abstract Task<List<ScheduleItem>> GetWeeklyScoopAsync();
    public abstract Task<List<Episode>> GetEpisodesAsync(string animeUrl);
    public abstract Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl);
    public abstract Task<string> ResolveVideoUrlAsync(string url);

    public virtual Task<string> GetSynopsisAsync(string animeUrl) => Task.FromResult(string.Empty);

    /// <summary>
    /// Downloads a webpage and returns its content. Rotates User-Agent automatically.
    /// Includes retry logic (up to 2 times) inspired by yt-dlp.
    /// </summary>
    protected async Task<string> DownloadWebpageAsync(string url, string? referer = null)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                var ua = UserAgents[_uaIndex % UserAgents.Length];
                _uaIndex++;

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                // Header order matters for TLS fingerprinting — match Firefox/Chrome order
                request.Headers.TryAddWithoutValidation("User-Agent", ua);
                request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
                request.Headers.TryAddWithoutValidation("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
                request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
                request.Headers.TryAddWithoutValidation("DNT", "1");
                request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
                request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
                request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", string.IsNullOrEmpty(referer) ? "none" : "same-origin");

                if (!string.IsNullOrEmpty(referer))
                    request.Headers.TryAddWithoutValidation("Referer", referer);

                var response = await Http.SendAsync(request);

                // Graceful handling — don't crash on 404/403, just return empty
                if (!response.IsSuccessStatusCode) return string.Empty;

                return await response.Content.ReadAsStringAsync();
            }
            catch when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(1 * (attempt + 1)));
            }
        }
        return string.Empty;
    }

    /// <summary>
    /// Searches for the first match of a regex pattern in text. Inspired by yt-dlp's _search_regex.
    /// </summary>
    protected string? SearchRegex(string pattern, string text, string? fallback = null, RegexOptions options = RegexOptions.None)
    {
        var match = Regex.Match(text, pattern, options);
        return match.Success ? match.Groups[1].Value : fallback;
    }

    /// <summary>
    /// Loads an HTML document from a URL.
    /// </summary>
    protected async Task<HtmlDocument?> GetDocumentAsync(string url, string? referer = null)
    {
        var html = await DownloadWebpageAsync(url, referer);
        if (string.IsNullOrEmpty(html)) return null;
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }
}
