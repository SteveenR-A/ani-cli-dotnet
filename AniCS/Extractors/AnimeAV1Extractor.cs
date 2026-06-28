using AniCS.Models;

namespace AniCS.Extractors;

/// <summary>
/// AnimeAV1 extractor. Secondary source (related to AnimeFLV network).
/// Inherits from BaseExtractor for full anti-bot protections.
/// </summary>
public class AnimeAV1Extractor : BaseExtractor
{
    private const string BaseUrl = "https://animeav1.com";

    public AnimeAV1Extractor(HttpClient client) : base(client) { }

    public override string Domain => "animeav1.com";

    public override async Task<List<AnimeResult>> SearchAsync(string query)
    {
        var results = new List<AnimeResult>();
        var doc = await GetDocumentAsync($"{BaseUrl}/?s={Uri.EscapeDataString(query)}", BaseUrl);
        if (doc == null) return results;

        var nodes = doc.DocumentNode.SelectNodes("//article[contains(@class,'TPost')]");
        if (nodes == null) return results;

        foreach (var node in nodes)
        {
            var linkNode = node.SelectSingleNode(".//a");
            var titleNode = node.SelectSingleNode(".//h2[@class='Title']");
            var imgNode = node.SelectSingleNode(".//img");

            if (linkNode == null || titleNode == null) continue;

            results.Add(new AnimeResult
            {
                Title = titleNode.InnerText.Trim(),
                Url = linkNode.GetAttributeValue("href", ""),
                ThumbnailUrl = imgNode?.GetAttributeValue("src", "") ?? imgNode?.GetAttributeValue("data-src", "") ?? ""
            });
        }

        return results;
    }

    public override async Task<List<Episode>> GetLatestReleasesAsync()
    {
        var episodes = new List<Episode>();
        var doc = await GetDocumentAsync(BaseUrl);
        if (doc == null) return episodes;

        var nodes = doc.DocumentNode.SelectNodes("//article[contains(@class,'TPost')]");
        if (nodes == null) return episodes;

        foreach (var node in nodes)
        {
            var linkNode = node.SelectSingleNode(".//a");
            var titleNode = node.SelectSingleNode(".//h2[@class='Title']");
            var imgNode = node.SelectSingleNode(".//img");

            if (linkNode == null || titleNode == null) continue;

            episodes.Add(new Episode
            {
                Title = titleNode.InnerText.Trim(),
                Url = linkNode.GetAttributeValue("href", ""),
                ThumbnailUrl = imgNode?.GetAttributeValue("src", "") ?? ""
            });
        }

        return episodes;
    }

    // AnimeAV1 doesn't have a publicly listed weekly schedule, so we return empty
    public override Task<List<ScheduleItem>> GetWeeklyScoopAsync() =>
        Task.FromResult(new List<ScheduleItem>());

    public override Task<List<Episode>> GetEpisodesAsync(string animeUrl) =>
        Task.FromResult(new List<Episode>());

    public override Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl)
    {
        // AnimeAV1 has embedded players directly on the page. We just return a single default server.
        return Task.FromResult(new List<VideoServer>
        {
            new VideoServer { Name = "AnimeAV1 Default", Url = episodeUrl }
        });
    }

    public override async Task<string> ResolveVideoUrlAsync(string url)
    {
        var html = await DownloadWebpageAsync(url, BaseUrl);
        if (string.IsNullOrEmpty(html)) return string.Empty;

        // Common pattern for AnimeFLV-network players
        var m3u8 = SearchRegex(@"""file""\s*:\s*""([^""]+\.m3u8[^""]*)""", html);
        if (!string.IsNullOrEmpty(m3u8)) return m3u8;

        var mp4 = SearchRegex(@"""file""\s*:\s*""([^""]+\.mp4[^""]*)""", html);
        if (!string.IsNullOrEmpty(mp4)) return mp4;

        return string.Empty;
    }
}
