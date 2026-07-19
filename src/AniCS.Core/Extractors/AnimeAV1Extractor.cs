using System.Text.RegularExpressions;
using AniCS.Models;

namespace AniCS.Extractors;

public class AnimeAV1Extractor : BaseExtractor
{
    private const string BaseUrl = "https://animeav1.com";

    public AnimeAV1Extractor(HttpClient client) : base(client) { }

    public override string Domain => "animeav1.com";

    public override async Task<List<AnimeResult>> SearchAsync(string query)
    {
        var results = new List<AnimeResult>();
        // Using catalog search instead of ?s= because it's a modern SPA framework
        var doc = await GetDocumentAsync($"{BaseUrl}/catalogo?search={Uri.EscapeDataString(query)}", BaseUrl);
        if (doc == null) return results;

        var nodes = doc.DocumentNode.SelectNodes("//article");
        if (nodes == null) return results;

        foreach (var node in nodes)
        {
            var linkNode = node.SelectSingleNode(".//a");
            var titleNode = node.SelectSingleNode(".//h3");
            var imgNode = node.SelectSingleNode(".//img");

            if (linkNode == null || titleNode == null) continue;

            var url = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrEmpty(url) || !url.StartsWith("/media/")) continue;

            var descNode = node.SelectSingleNode(".//p[contains(@class, 'line-clamp')]");
            var description = descNode != null ? System.Net.WebUtility.HtmlDecode(descNode.InnerText.Trim()) : "";

            results.Add(new AnimeResult
            {
                Title = System.Net.WebUtility.HtmlDecode(titleNode.InnerText.Trim()),
                Url = BaseUrl + url,
                ThumbnailUrl = imgNode?.GetAttributeValue("src", "") ?? "",
                Description = description
            });
        }

        return results;
    }

    public override async Task<List<Episode>> GetLatestReleasesAsync()
    {
        var episodes = new List<Episode>();
        var doc = await GetDocumentAsync(BaseUrl);
        if (doc == null) return episodes;

        var sections = doc.DocumentNode.SelectNodes("//section");
        if (sections == null) return episodes;

        foreach (var section in sections)
        {
            var header = section.SelectSingleNode(".//h2");
            if (header != null && header.InnerText.Contains("Episodios", StringComparison.OrdinalIgnoreCase))
            {
                var articles = section.SelectNodes(".//article");
                if (articles != null)
                {
                    foreach (var art in articles)
                    {
                        var titleNode = art.SelectSingleNode(".//div[contains(@class, 'text-2xs')]");
                        var epNode = art.SelectSingleNode(".//span[contains(@class, 'text-lead')]");
                        var linkNode = art.SelectSingleNode(".//a");
                        var imgNode = art.SelectSingleNode(".//img");

                        if (linkNode == null || titleNode == null) continue;

                        episodes.Add(new Episode
                        {
                            Title = titleNode.InnerText.Trim(),
                            EpisodeNumber = epNode?.InnerText.Trim() ?? "",
                            Url = BaseUrl + linkNode.GetAttributeValue("href", ""),
                            ThumbnailUrl = imgNode?.GetAttributeValue("src", "") ?? ""
                        });
                    }
                }
                break;
            }
        }

        return episodes;
    }

    public override Task<List<ScheduleItem>> GetWeeklyScoopAsync()
    {
        return Task.FromResult(new List<ScheduleItem>
        {
            new ScheduleItem
            {
                Day = "No Disponible",
                Title = "AnimeAV1 oculta el horario en código cerrado. Usa Jkanime para el calendario (sc).",
                Url = BaseUrl
            }
        });
    }

    public override async Task<List<Episode>> GetEpisodesAsync(string animeUrl)
    {
        var episodes = new List<Episode>();
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        if (doc == null) return episodes;

        var added = new HashSet<string>();

        // 1. Try to extract from raw SvelteKit data (bypasses 50-episode pagination limit)
        var html = doc.DocumentNode.OuterHtml;
        var svelteMatches = System.Text.RegularExpressions.Regex.Matches(html, @"number:(\d+)");
        foreach (System.Text.RegularExpressions.Match m in svelteMatches)
        {
            var text = m.Groups[1].Value;
            var href = new Uri(new Uri(BaseUrl), new Uri(animeUrl).AbsolutePath + "/" + text).AbsolutePath;

            if (!added.Contains(href))
            {
                added.Add(href);
                episodes.Add(new Episode { Title = $"Episodio {text}", Url = BaseUrl + href, EpisodeNumber = text });
            }
        }

        // 2. Fallback to extracting from HTML <a> tags in case it's a movie or single episode format
        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/media/') and not(contains(@class, 'btn'))]");
        if (links != null)
        {
            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                if (href.StartsWith("/media/") && !added.Contains(href))
                {
                    var lastSegment = href.TrimEnd('/').Split('/').LastOrDefault();
                    if (int.TryParse(lastSegment, out _))
                    {
                        added.Add(href);
                        episodes.Add(new Episode { Title = $"Episodio {lastSegment}", Url = BaseUrl + href, EpisodeNumber = lastSegment });
                    }
                }
            }
        }

        if (episodes.Count == 0)
        {
            episodes.Add(new Episode { Title = "Película / Episodio Único", Url = animeUrl, EpisodeNumber = "1" });
        }
        else
        {
            // Reverse so Episode 1168 is at index 0 (latest first)
            episodes = episodes.OrderByDescending(e => int.Parse(e.EpisodeNumber)).ToList();
        }

        return episodes;
    }

    public override async Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl)
    {
        var servers = new List<VideoServer>();
        var html = await DownloadWebpageAsync(episodeUrl, BaseUrl);
        if (string.IsNullOrEmpty(html)) return servers;

        var mp4UploadRegex = System.Text.RegularExpressions.Regex.Match(html, @"https?://(?:www\.)?mp4upload\.com/embed[^""'\s]*");
        if (mp4UploadRegex.Success)
            servers.Add(new VideoServer { Name = "MP4Upload", Url = mp4UploadRegex.Value });

        var megaRegex = System.Text.RegularExpressions.Regex.Match(html, @"https?://mega\.nz/embed[^""'\s]*");
        if (megaRegex.Success)
            servers.Add(new VideoServer { Name = "Mega", Url = megaRegex.Value });

        var zillaRegex = System.Text.RegularExpressions.Regex.Match(html, @"https?://player\.zilla-networks\.com/play/[^""'\s]*");
        if (zillaRegex.Success)
            servers.Add(new VideoServer { Name = "Zilla", Url = zillaRegex.Value });

        var teraboxRegex = System.Text.RegularExpressions.Regex.Match(html, @"https?://(?:www\.)?terabox\.com/sharing/embed[^""'\s]*");
        if (teraboxRegex.Success)
            servers.Add(new VideoServer { Name = "TeraBox", Url = teraboxRegex.Value });

        if (servers.Count == 0)
        {
            servers.Add(new VideoServer { Name = "AnimeAV1 Default", Url = episodeUrl });
        }

        return servers;
    }

    public override async Task<string> GetSynopsisAsync(string animeUrl)
    {
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        if (doc == null) return string.Empty;

        var pNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'entry')]//p");
        if (pNodes != null && pNodes.Count > 0)
        {
            var text = string.Join("\n\n", pNodes.Select(p => System.Net.WebUtility.HtmlDecode(p.InnerText.Trim())));
            return text;
        }

        // Fallback: check meta description
        var metaNode = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
        if (metaNode != null)
        {
            var desc = metaNode.GetAttributeValue("content", "");
            if (!string.IsNullOrEmpty(desc))
                return System.Net.WebUtility.HtmlDecode(desc.Trim());
        }

        return "Sinopsis no disponible.";
    }

    public override async Task<string> ResolveVideoUrlAsync(string url)
    {
        // If the URL is already an external server (like mp4upload), return it so mpv/yt-dlp can handle it.
        if (url.Contains("mp4upload.com") || url.Contains("mega.nz") || url.Contains("zilla-networks") || url.Contains("terabox.com"))
        {
            return url;
        }

        var html = await DownloadWebpageAsync(url, BaseUrl);
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // In SvelteKit / NextJS, data might be embedded in script tags or JSON.
        // Let's try to find m3u8 directly first
        var m3u8Regex = System.Text.RegularExpressions.Regex.Match(html, @"https?://[^""'\s]+\.m3u8[^""'\s]*");
        if (m3u8Regex.Success) return m3u8Regex.Value;

        var mp4Regex = System.Text.RegularExpressions.Regex.Match(html, @"https?://[^""'\s]+\.mp4[^""'\s]*");
        if (mp4Regex.Success) return mp4Regex.Value;

        return string.Empty;
    }
}
