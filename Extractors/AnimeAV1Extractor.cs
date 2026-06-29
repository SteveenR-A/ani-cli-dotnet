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

            results.Add(new AnimeResult
            {
                Title = titleNode.InnerText.Trim(),
                Url = BaseUrl + url,
                ThumbnailUrl = imgNode?.GetAttributeValue("src", "") ?? ""
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

    public override Task<List<ScheduleItem>> GetWeeklyScoopAsync() => Task.FromResult(new List<ScheduleItem>());

    public override async Task<List<Episode>> GetEpisodesAsync(string animeUrl)
    {
        var episodes = new List<Episode>();
        var doc = await GetDocumentAsync(animeUrl, BaseUrl);
        if (doc == null) return episodes;

        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/media/') and not(contains(@class, 'btn'))]");
        if (links != null)
        {
            var added = new HashSet<string>();
            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                if (href.StartsWith("/media/") && !added.Contains(href))
                {
                    // Check if it's an episode number block
                    var text = link.InnerText.Trim();
                    if (int.TryParse(text, out _))
                    {
                        added.Add(href);
                        episodes.Add(new Episode
                        {
                            Title = $"Episodio {text}",
                            Url = BaseUrl + href,
                            EpisodeNumber = text
                        });
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
            episodes.Reverse(); // Modern sites usually have 1, 2, 3... we want 1 at index 0. Actually, if they are ordered 1..12 in HTML, no reverse is needed.
        }

        return episodes;
    }

    public override Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl)
    {
        return Task.FromResult(new List<VideoServer>
        {
            new VideoServer { Name = "AnimeAV1 Default", Url = episodeUrl }
        });
    }

    public override async Task<string> ResolveVideoUrlAsync(string url)
    {
        var html = await DownloadWebpageAsync(url, BaseUrl);
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // In SvelteKit / NextJS, data might be embedded in script tags or JSON.
        // Let's try to find m3u8 directly first
        var m3u8Regex = Regex.Match(html, @"https?://[^""'\s]+\.m3u8[^""'\s]*");
        if (m3u8Regex.Success) return m3u8Regex.Value;

        var mp4Regex = Regex.Match(html, @"https?://[^""'\s]+\.mp4[^""'\s]*");
        if (mp4Regex.Success) return mp4Regex.Value;

        return string.Empty;
    }
}
