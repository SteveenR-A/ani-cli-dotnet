using AniCS.Models;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AniCS.Extractors;

public class MundoDonghuaExtractor : BaseExtractor
{
    private const string BaseUrl = "https://www.mundodonghua.com";

    public MundoDonghuaExtractor(HttpClient client) : base(client)
    {
    }

    public override string Domain => "mundodonghua.com";

    public override Task<List<AnimeResult>> SearchAsync(string query)
    {
        return AdvancedSearchAsync(new SearchFilters { Query = query });
    }

    public override async Task<List<AnimeResult>> AdvancedSearchAsync(SearchFilters filters)
    {
        var results = new List<AnimeResult>();
        string url;

        if (!string.IsNullOrEmpty(filters.Genre) && string.IsNullOrEmpty(filters.Query))
        {
            // Búsqueda por género (ej. /genero/accion)
            url = $"{BaseUrl}/genero/{Uri.EscapeDataString(filters.Genre)}";
        }
        else if (!string.IsNullOrEmpty(filters.Query))
        {
            // Búsqueda normal por texto
            url = $"{BaseUrl}/busquedas/{Uri.EscapeDataString(filters.Query)}";
        }
        else
        {
            // Sin filtros, devolvemos lista general
            url = $"{BaseUrl}/lista-donghuas";
        }

        var doc = await GetDocumentAsync(url, BaseUrl);
        if (doc == null) return results;

        var cards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'md-card')]");
        if (cards == null) return results;

        foreach (var card in cards)
        {
            var linkNode = card.SelectSingleNode(".//a");
            var imgNode = card.SelectSingleNode(".//img");
            var titleNode = card.SelectSingleNode(".//*[contains(@class, 'md-card-title')]");
            var badgeNode = card.SelectSingleNode(".//*[contains(@class, 'md-card-badge')]");

            if (linkNode == null) continue;

            var href = linkNode.GetAttributeValue("href", "");
            if (!href.Contains("/donghua/")) continue; // Filter only donghuas

            if (!href.StartsWith("http")) href = BaseUrl + href;

            var thumbUrl = imgNode?.GetAttributeValue("src", "") ?? "";
            if (!string.IsNullOrEmpty(thumbUrl) && !thumbUrl.StartsWith("http")) thumbUrl = BaseUrl + thumbUrl;

            results.Add(new AnimeResult
            {
                Title = titleNode?.InnerText.Trim() ?? "Desconocido",
                Url = href,
                ThumbnailUrl = thumbUrl,
                Type = badgeNode?.InnerText.Trim().ToUpper() ?? ""
            });
        }

        return results;
    }

    public override async Task<List<Episode>> GetLatestReleasesAsync()
    {
        var results = new List<Episode>();
        var doc = await GetDocumentAsync(BaseUrl, BaseUrl);
        if (doc == null) return results;

        var container = doc.DocumentNode.SelectSingleNode("//div[@id='nuevos-episodios-grid']");
        if (container == null) return results;

        var cards = container.SelectNodes(".//div[contains(@class, 'md-card')]");
        if (cards == null) return results;

        foreach (var card in cards)
        {
            var linkNode = card.SelectSingleNode(".//a");
            var imgNode = card.SelectSingleNode(".//img");
            var titleNode = card.SelectSingleNode(".//h3[contains(@class, 'md-card-title')]");
            var badgeNode = card.SelectSingleNode(".//*[contains(@class, 'md-card-badge')]");

            if (linkNode == null) continue;

            var href = linkNode.GetAttributeValue("href", "");
            if (!href.StartsWith("http")) href = BaseUrl + href;

            var thumbUrl = imgNode?.GetAttributeValue("src", "") ?? "";
            if (!string.IsNullOrEmpty(thumbUrl) && !thumbUrl.StartsWith("http")) thumbUrl = BaseUrl + thumbUrl;

            results.Add(new Episode
            {
                Title = titleNode?.InnerText.Trim() ?? "Donghua",
                Url = href,
                ThumbnailUrl = thumbUrl,
                Type = badgeNode?.InnerText.Trim().ToUpper() ?? "",
                EpisodeNumber = "1" // Will be overridden or used as is
            });
        }

        return results;
    }

    public override async Task<AnimeResult> GetDetailsAsync(string animeUrl)
    {
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        var result = new AnimeResult { Url = animeUrl };
        if (doc == null) return result;

        var imgNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
        if (imgNode != null) result.ThumbnailUrl = imgNode.GetAttributeValue("content", "");

        var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
        if (titleNode != null) result.Title = titleNode.InnerText.Trim();

        var synNode = doc.DocumentNode.SelectSingleNode("//p[contains(@class, 'md-detail-synopsis')]") ??
                      doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'sinopsis')]") ??
                      doc.DocumentNode.SelectSingleNode("//p[contains(@class, 'description')]");
        if (synNode != null) result.Synopsis = synNode.InnerText.Trim();

        // Extract genres and other details if present (using generic label matching as fallback)
        var detailNodes = doc.DocumentNode.SelectNodes("//li | //div[contains(@class, 'info')]//p");
        if (detailNodes != null)
        {
            foreach (var node in detailNodes)
            {
                var text = node.InnerText.Trim();
                if (text.StartsWith("Géneros:", StringComparison.OrdinalIgnoreCase))
                {
                    result.Genres = text.Substring(8).Split(',').Select(g => g.Trim()).ToList();
                }
                else if (text.StartsWith("Estudio:", StringComparison.OrdinalIgnoreCase)) result.Studios = text.Substring(8).Trim();
                else if (text.StartsWith("Estado:", StringComparison.OrdinalIgnoreCase)) result.Status = text.Substring(7).Trim();
                else if (text.StartsWith("Episodios:", StringComparison.OrdinalIgnoreCase)) result.TotalEpisodes = text.Substring(10).Trim();
            }
        }

        return result;
    }

    public override async Task<List<Episode>> GetEpisodesAsync(string animeUrl)
    {
        var results = new List<Episode>();
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        if (doc == null) return results;

        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/ver/')]");
        if (links == null) return results;

        var uniqueLinks = new HashSet<string>();

        int epNum = links.Count;
        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");
            if (!href.StartsWith("http")) href = BaseUrl + href;

            if (uniqueLinks.Add(href))
            {
                results.Add(new Episode
                {
                    Title = link.InnerText.Trim(),
                    Url = href,
                    EpisodeNumber = epNum.ToString()
                });
                epNum--;
            }
        }

        results.Reverse(); // Usually sites list newest first, so we reverse to have ascending order if desired, or keep as is.
        // Actually, we re-assign episode numbers correctly:
        for (int i = 0; i < results.Count; i++)
        {
            results[i].EpisodeNumber = (i + 1).ToString();
        }

        return results;
    }

    public override async Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl)
    {
        var servers = new List<VideoServer>();
        
        // Fix legacy history links that used dashes instead of slashes for episodes
        // e.g. /ver/martial-master-669 -> /ver/martial-master/669
        var legacyUrlMatch = System.Text.RegularExpressions.Regex.Match(episodeUrl, @"/ver/(.+?)-(\d+)$");
        if (legacyUrlMatch.Success)
        {
            int index = legacyUrlMatch.Groups[2].Index;
            episodeUrl = episodeUrl.Substring(0, index - 1) + "/" + legacyUrlMatch.Groups[2].Value;
        }

        var doc = await GetDocumentAsync(episodeUrl, BaseUrl);
        if (doc == null) return servers;

        var html = doc.DocumentNode.OuterHtml;
        var evalMatches = System.Text.RegularExpressions.Regex.Matches(html, @"eval\(function\(p,a,c,k,e,d\).*?return p}\('(.*?)',(\d+),(\d+),'(.*?)'\.split\('\|'\)");

        foreach (System.Text.RegularExpressions.Match match in evalMatches)
        {
            try
            {
                string p = match.Groups[1].Value;
                int a = int.Parse(match.Groups[2].Value);
                int c = int.Parse(match.Groups[3].Value);
                string[] k = match.Groups[4].Value.Split('|');

                string unpacked = Unpack(p, a, c, k);

                // Extract iframe src
                var iframeMatch = System.Text.RegularExpressions.Regex.Match(unpacked, @"<iframe[^>]+src=\\?['""]([^'""]+?)(\\?['""])");
                if (iframeMatch.Success)
                {
                    string url = iframeMatch.Groups[1].Value.Replace("\\", "");
                    string name = GetServerNameFromUrl(url);
                    bool isDirect = name == "VidHide" || name == "Embedwish";
                    if (!servers.Any(s => s.Url == url))
                    {
                        servers.Add(new VideoServer { Name = name, Url = url, IsDirectPlaySupported = isDirect });
                    }
                }
                else
                {
                    // Fallback to JWPlayer file
                    var fileMatch = System.Text.RegularExpressions.Regex.Match(unpacked, @"file:\s*\\?['""]([^'""]+)\\?['""]");
                    if (fileMatch.Success)
                    {
                        string url = fileMatch.Groups[1].Value;
                        if (!servers.Any(s => s.Url == url))
                        {
                            servers.Add(new VideoServer { Name = "MundoDonghua HLS", Url = url, IsDirectPlaySupported = true });
                        }
                    }
                }

            }
            catch { }
        }

        // Fallback for direct iframes if any
        if (servers.Count == 0)
        {
            var iframeNodes = doc.DocumentNode.SelectNodes("//iframe");
            if (iframeNodes != null)
            {
                foreach (var iframe in iframeNodes)
                {
                    var src = iframe.GetAttributeValue("src", "");
                    if (!string.IsNullOrEmpty(src))
                    {
                        string name = GetServerNameFromUrl(src);
                        bool isDirect = name == "VidHide" || name == "Embedwish";
                        servers.Add(new VideoServer
                        {
                            Name = name,
                            Url = src,
                            IsDirectPlaySupported = isDirect
                        });
                    }
                }
            }
        }

        return servers;
    }

    private string GetServerNameFromUrl(string url)
    {
        if (url.Contains("voe")) return "Voe";
        if (url.Contains("fembed") || url.Contains("fmoon") || url.Contains("bysekoze")) return "Fembed / Fmoon";
        if (url.Contains("vgembed")) return "VGEmbed";
        if (url.Contains("ok.ru")) return "OkRu";
        if (url.Contains("mp4upload")) return "Mp4Upload";
        if (url.Contains("yourupload")) return "YourUpload";
        if (url.Contains("dood")) return "DoodStream";
        if (url.Contains("vidhide") || url.Contains("callistanise")) return "VidHide";
        if (url.Contains("embedwish")) return "Embedwish";
        return "Servidor Extra";
    }

    private string Unpack(string p, int a, int c, string[] k)
    {
        for (int i = c - 1; i >= 0; i--)
        {
            if (!string.IsNullOrEmpty(k[i]))
            {
                string find = EncodeBase(i, a);
                p = System.Text.RegularExpressions.Regex.Replace(p, @"\b" + find + @"\b", k[i]);
            }
        }
        return p;
    }

    private string EncodeBase(int c, int a)
    {
        if (c < a) return EncodeChar(c);
        return EncodeBase(c / a, a) + EncodeChar(c % a);
    }

    private string EncodeChar(int c)
    {
        if (c > 35) return ((char)(c + 29)).ToString();
        const string chars = "0123456789abcdefghijklmnopqrstuvwxyz";
        return chars[c].ToString();
    }

    public override async Task<string> ResolveVideoUrlAsync(string url)
    {
        if (url.Contains("redirector.php"))
        {
            try
            {
                var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false };
                using var client = new System.Net.Http.HttpClient(handler);
                client.DefaultRequestHeaders.Add("User-Agent", AniCS.ConfigManager.Current.RandomUserAgent);
                var response = await client.GetAsync(url);
                if (response.StatusCode == System.Net.HttpStatusCode.Found || response.StatusCode == System.Net.HttpStatusCode.Redirect || response.StatusCode == System.Net.HttpStatusCode.MovedPermanently)
                {
                    if (response.Headers.Location != null)
                    {
                        if (!response.Headers.Location.IsAbsoluteUri)
                        {
                            return new Uri(new Uri(url), response.Headers.Location).ToString();
                        }
                        return response.Headers.Location.ToString();
                    }
                }
            }
            catch { }
            return url;
        }

        if (url.EndsWith(".m3u8") || url.EndsWith(".mp4") || url.Contains(".mp4?") || url.Contains(".m3u8?"))
        {
            return url;
        }

        try
        {
            var html = await Http.GetStringAsync(url);
            
            // Attempt to unpack eval scripts (common in VidHide, Embedwish, etc.)
            var evalMatch = System.Text.RegularExpressions.Regex.Match(html, @"eval\(function\(p,a,c,k,e,d\).*?return p}\('(.*?)',(\d+),(\d+),'(.*?)'\.split\('\|'\)");
            if (evalMatch.Success)
            {
                string p = evalMatch.Groups[1].Value;
                int a = int.Parse(evalMatch.Groups[2].Value);
                int c = int.Parse(evalMatch.Groups[3].Value);
                string[] k = evalMatch.Groups[4].Value.Split('|');
                
                string unpacked = Unpack(p, a, c, k);
                
                var match = System.Text.RegularExpressions.Regex.Match(unpacked, @"https?://[^""'\s]+\.(?:m3u8|mp4)[^""'\s]*");
                if (!match.Success)
                {
                    match = System.Text.RegularExpressions.Regex.Match(unpacked, @"https?://[^""'\s]+redirector\.php\?slug=[^""'\s]+");
                }
                
                if (match.Success)
                    return match.Value;
            }
            
            // Search raw HTML if no packed script is found
            var rawMatch = System.Text.RegularExpressions.Regex.Match(html, @"https?://[^""'\s]+\.(?:m3u8|mp4)[^""'\s]*");
            if (!rawMatch.Success)
            {
                rawMatch = System.Text.RegularExpressions.Regex.Match(html, @"https?://[^""'\s]+redirector\.php\?slug=[^""'\s]+");
            }

            if (rawMatch.Success)
                return rawMatch.Value;
        }
        catch { }

        return string.Empty; // Fallback to yt-dlp
    }

    public override Task<List<ScheduleItem>> GetWeeklyScoopAsync()
    {
        return Task.FromResult(new List<ScheduleItem>());
    }

    public override string NormalizeSeriesUrl(string url)
    {
        // Convierte un enlace de episodio (/ver/...) a un enlace de serie (/donghua/...)
        if (url.Contains("/ver/"))
        {
            // e.g. https://www.mundodonghua.com/ver/the-demon-hunter-3/15
            int lastSlash = url.LastIndexOf('/');
            if (lastSlash > 0)
            {
                string withoutEp = url.Substring(0, lastSlash);
                return withoutEp.Replace("/ver/", "/donghua/");
            }
        }
        return url;
    }
}
