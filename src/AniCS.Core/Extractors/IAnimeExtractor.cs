using AniCS.Models;

namespace AniCS.Extractors;

public interface IAnimeExtractor
{
    string Domain { get; }
    Task<List<AnimeResult>> SearchAsync(string query);
    Task<List<AnimeResult>> AdvancedSearchAsync(SearchFilters filters);
    Task<List<Episode>> GetLatestReleasesAsync();
    Task<List<ScheduleItem>> GetWeeklyScoopAsync();
    Task<List<Episode>> GetEpisodesAsync(string animeUrl);
    Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl);
    Task<string> ResolveVideoUrlAsync(string url);
    Task<string> GetSynopsisAsync(string animeUrl);
    Task<AnimeResult> GetDetailsAsync(string animeUrl);
    Task<string> GetThumbnailAsync(string animeUrl);
    Task<List<AnimeResult>> GetTopAnimesAsync(string topType, string yearFilter, int page = 1);
    Task<List<AnimeResult>> GetPremieresAsync();
    string NormalizeSeriesUrl(string url);
}
