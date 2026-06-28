using AniCS.Models;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AniCS.Extractors;

/// <summary>
/// JKAnime extractor. All selectors verified against live site HTML (June 2025).
///
/// Search:   GET /buscar/{query}/          → div.anime__item → div.anime__item__text > h5 > a
/// Latest:   GET /                         → div.dir1 > div.card[class*=ml-2] > a > h5.card-title
/// Scoop:    GET /horario/                 → div.semana > h2, div.boxx > a
/// Episodes: GET series page → extract animeId + CSRF cookie
///           POST /ajax/episodes/{id}/1   → JSON {data:[{number,id,image}], total}
/// Video:    GET /slug/ep/               → iframe.player_conte → GET /jkplayer/... → .m3u8
/// </summary>
public class JKAnimeExtractor : BaseExtractor
{
    private const string BaseUrl = "https://jkanime.net";

    // Cookie container so we can reuse session cookies for AJAX calls
    private readonly CookieContainer _cookies;

    public JKAnimeExtractor(HttpClient client) : base(client)
    {
        _cookies = new CookieContainer();
    }

    public override string Domain => "jkanime.net";

    // ── Search ────────────────────────────────────────────────────
    public override async Task<List<AnimeResult>> SearchAsync(string query)
    {
        var results = new List<AnimeResult>();

        // /buscar/{query}/ — the working search URL
        var doc = await GetDocumentAsync($"{BaseUrl}/buscar/{Uri.EscapeDataString(query)}/", BaseUrl);
        if (doc == null) return results;

        // div.anime__item > a (image link goes to latest ep) + div.anime__item__text > h5 > a (series)
        var items = doc.DocumentNode.SelectNodes("//div[@class='anime__item']");
        if (items == null) return results;

        foreach (var item in items)
        {
            var seriesLink = item.SelectSingleNode(".//div[@class='anime__item__text']//h5/a");
            var picNode    = item.SelectSingleNode(".//div[contains(@class,'anime__item__pic')]");

            if (seriesLink == null) continue;

            var href  = seriesLink.GetAttributeValue("href", "");
            var title = WebUtility.HtmlDecode(seriesLink.InnerText.Trim());
            if (string.IsNullOrEmpty(href)) continue;

            results.Add(new AnimeResult
            {
                Title        = title,
                Url          = href,
                ThumbnailUrl = picNode?.GetAttributeValue("data-setbg", "") ?? ""
            });
        }

        return results;
    }

    // ── Latest ────────────────────────────────────────────────────
    public override async Task<List<Episode>> GetLatestReleasesAsync()
    {
        var episodes = new List<Episode>();
        var doc = await GetDocumentAsync(BaseUrl);
        if (doc == null) return episodes;

        // Homepage: div.dir1 > div[class contains 'card ml-2'] > a
        // Note: exact class is "card ml-2 mr-2", so use contains
        var cards = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'dir1')]//div[contains(@class,'ml-2') and contains(@class,'card')]//a");
        if (cards == null) return episodes;

        foreach (var card in cards)
        {
            var href      = card.GetAttributeValue("href", "");
            var titleNode = card.SelectSingleNode(".//h5[contains(@class,'card-title')]");
            var imgNode   = card.SelectSingleNode(".//img[contains(@class,'card-img-top')]");
            var epBadge   = card.SelectSingleNode(".//span[contains(@class,'badge-primary')]");

            if (titleNode == null || string.IsNullOrEmpty(href)) continue;

            episodes.Add(new Episode
            {
                Title         = WebUtility.HtmlDecode(titleNode.InnerText.Trim()),
                EpisodeNumber = epBadge?.InnerText.Replace("Ep ", "").Trim() ?? "",
                Url           = href,
                ThumbnailUrl  = imgNode?.GetAttributeValue("src", "") ?? ""
            });
        }

        return episodes;
    }

    // ── Scoop ─────────────────────────────────────────────────────
    public override async Task<List<ScheduleItem>> GetWeeklyScoopAsync()
    {
        var schedule = new List<ScheduleItem>();
        var doc = await GetDocumentAsync($"{BaseUrl}/horario/", BaseUrl);
        if (doc == null) return schedule;

        // div.semana > h2 (day) > div.cajas > div.boxx > a
        var dayBlocks = doc.DocumentNode.SelectNodes("//div[contains(@class,'semana')]");
        if (dayBlocks == null) return schedule;

        foreach (var dayBlock in dayBlocks)
        {
            var h2  = dayBlock.SelectSingleNode(".//h2");
            var day = h2 != null ? Regex.Replace(h2.InnerText, @"\s+", " ").Trim() : "?";

            var links = dayBlock.SelectNodes(".//div[@class='boxx']//a");
            if (links == null) continue;

            foreach (var a in links)
            {
                var href      = a.GetAttributeValue("href", "");
                var parentDiv = a.ParentNode?.ParentNode;
                var title     = parentDiv?.GetAttributeValue("title", "")
                             ?? a.SelectSingleNode(".//strong")?.InnerText.Trim()
                             ?? "";
                var img = a.SelectSingleNode(".//img");

                if (string.IsNullOrEmpty(href)) continue;

                schedule.Add(new ScheduleItem
                {
                    Day          = day,
                    Title        = WebUtility.HtmlDecode(title),
                    Url          = href,
                    ThumbnailUrl = img?.GetAttributeValue("src", "") ?? ""
                });
            }
        }

        return schedule;
    }

    // ── Episodes (via AJAX with session + CSRF) ───────────────────
    public override async Task<List<Episode>> GetEpisodesAsync(string animeUrl)
    {
        var episodes = new List<Episode>();
        var seriesUrl = NormalizeToSeriesUrl(animeUrl);
        var slug      = ExtractSlug(seriesUrl);
        if (string.IsNullOrEmpty(slug)) return episodes;

        // Step 1: GET series page with cookies — extract CSRF token & anime ID
        var (html, cookies) = await DownloadWithCookiesAsync(seriesUrl, BaseUrl);
        if (string.IsNullOrEmpty(html)) return episodes;

        var csrfMatch   = Regex.Match(html, @"name=""csrf-token""\s+content=""([^""]+)""");
        var animeIdMatch = Regex.Match(html, @"ajax/episodes/(\d+)/");
        if (!csrfMatch.Success || !animeIdMatch.Success) return episodes;

        var csrf    = csrfMatch.Groups[1].Value;
        var animeId = animeIdMatch.Groups[1].Value;

        // Step 2: POST to /ajax/episodes/{id}/1 with session cookie + CSRF
        var epData = await PostEpisodesAsync(animeId, csrf, cookies, seriesUrl);
        if (epData == null) return episodes;

        // Step 3: Build episode list from JSON response
        var root  = epData.Value;
        int total = root.TryGetProperty("total", out var t) ? t.GetInt32() : 0;
        if (total == 0 && root.TryGetProperty("data", out var data))
            total = data.GetArrayLength();

        for (int i = 1; i <= total; i++)
        {
            episodes.Add(new Episode
            {
                Title         = $"Episodio {i}",
                EpisodeNumber = i.ToString(),
                Url           = $"{BaseUrl}/{slug}/{i}/"
            });
        }

        return episodes;
    }

    private async Task<JsonElement?> PostEpisodesAsync(string animeId, string csrf, string cookies, string referer)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    $"{BaseUrl}/ajax/episodes/{animeId}/1");

                request.Headers.TryAddWithoutValidation("User-Agent",
                    "Mozilla/5.0 (X11; Linux x86_64; rv:126.0) Gecko/20100101 Firefox/126.0");
                request.Headers.TryAddWithoutValidation("Referer", referer);
                request.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
                request.Headers.TryAddWithoutValidation("Cookie", cookies);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                request.Content = new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("_token", csrf)
                ]);

                var response = await Http.SendAsync(request);
                if (!response.IsSuccessStatusCode) return null;

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.Clone();
            }
            catch when (attempt < 2)
            {
                await Task.Delay(1000 * (attempt + 1));
            }
        }
        return null;
    }

    // ── Get Video Servers ─────────────────────────────────────────
    public override async Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl)
    {
        var servers = new List<VideoServer>();
        var epHtml = await DownloadWebpageAsync(episodeUrl, BaseUrl);
        if (string.IsNullOrEmpty(epHtml)) return servers;

        // Parse default servers (Desu, Magi) from video[] arrays
        var defaultNames = Regex.Matches(epHtml, @"<a\s+[^>]*data-id=""(\d+)""[^>]*>([^<]+)</a>");
        var videoArrays = Regex.Matches(epHtml, @"video\[(\d+)\]\s*=\s*'([^']+)'");
        
        var nameMap = new Dictionary<string, string>();
        foreach (Match m in defaultNames) nameMap[m.Groups[1].Value] = m.Groups[2].Value.Trim();

        foreach (Match m in videoArrays)
        {
            var id = m.Groups[1].Value;
            var iframeHtml = m.Groups[2].Value;
            var src = SearchRegex(@"src=""([^""]+)""", iframeHtml);
            
            if (!string.IsNullOrEmpty(src))
            {
                var name = nameMap.TryGetValue(id, out var n) ? n : $"Servidor {id}";
                servers.Add(new VideoServer { Name = name, Url = src.Replace(@"\/", "/") });
            }
        }

        // Parse external servers from JSON `var servers = [...]`
        var serversJsonRaw = SearchRegex(@"var\s+servers\s*=\s*(\[.*?\]);", epHtml);
        if (!string.IsNullOrEmpty(serversJsonRaw))
        {
            try
            {
                using var doc = JsonDocument.Parse(serversJsonRaw);
                foreach (var s in doc.RootElement.EnumerateArray())
                {
                    var name = s.GetProperty("server").GetString() ?? "Unknown";
                    var remote64 = s.GetProperty("remote").GetString() ?? "";
                    if (!string.IsNullOrEmpty(remote64))
                    {
                        var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(remote64));
                        servers.Add(new VideoServer { Name = name, Url = decoded });
                    }
                }
            }
            catch { /* Ignore parsing errors */ }
        }

        // Fallback if no servers found but there is an iframe
        if (servers.Count == 0)
        {
            var iframeSrc = SearchRegex(@"<iframe[^>]+class=""player_conte""[^>]+src=""([^""]+)""", epHtml);
            if (!string.IsNullOrEmpty(iframeSrc))
                servers.Add(new VideoServer { Name = "Desu (Fallback)", Url = iframeSrc });
        }

        return servers;
    }

    // ── Resolve Video URL ─────────────────────────────────────────
    public override async Task<string> ResolveVideoUrlAsync(string url)
    {
        // Internal JKAnime servers (Desu, Magi)
        if (url.Contains("jkanime.net/jkplayer"))
        {
            var playerHtml = await DownloadWebpageAsync(url, BaseUrl);
            if (string.IsNullOrEmpty(playerHtml)) return string.Empty;

            var m3u8 = SearchRegex(@"(https://[^\s'""\\]+\.m3u8[^\s'""\\]*)", playerHtml);
            return m3u8 ?? string.Empty;
        }

        // Custom Mediafire Resolver (yt-dlp does not support it natively)
        if (url.Contains("mediafire.com"))
        {
            var mfHtml = await DownloadWebpageAsync(url);
            if (string.IsNullOrEmpty(mfHtml)) return string.Empty;

            var downloadLink = SearchRegex(@"href=""(https?://download[^""]+)""", mfHtml);
            return downloadLink ?? string.Empty;
        }

        // Return empty for other external servers so Program.cs uses yt-dlp fallback
        // yt-dlp will handle Mp4upload, Streamtape, Mixdrop, etc.
        return string.Empty;
    }

    // ── Helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Downloads a page and returns (html, raw Set-Cookie header string) for AJAX reuse.
    /// </summary>
    private async Task<(string html, string cookies)> DownloadWithCookiesAsync(string url, string? referer = null)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (X11; Linux x86_64; rv:126.0) Gecko/20100101 Firefox/126.0");
            request.Headers.TryAddWithoutValidation("Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            if (!string.IsNullOrEmpty(referer))
                request.Headers.TryAddWithoutValidation("Referer", referer);

            var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode) return (string.Empty, string.Empty);

            // Extract Set-Cookie headers to replay in AJAX call
            var cookieHeaders = response.Headers
                .Where(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                .SelectMany(h => h.Value)
                .Select(c => c.Split(';')[0].Trim())
                .ToList();

            var html    = await response.Content.ReadAsStringAsync();
            var cookies = string.Join("; ", cookieHeaders);
            return (html, cookies);
        }
        catch { return (string.Empty, string.Empty); }
    }

    private static string ExtractSlug(string url)
    {
        var m = Regex.Match(url, @"jkanime\.net/([^/]+)/?");
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static string NormalizeToSeriesUrl(string url)
    {
        var m = Regex.Match(url, @"(https://jkanime\.net/[^/]+/)");
        return m.Success ? m.Groups[1].Value : url;
    }
}
