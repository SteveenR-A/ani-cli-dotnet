using AniCS.Models;

namespace AniCS.Extractors;

public interface IAnimeExtractor
{
    string Domain { get; }
    Task<List<AnimeResult>> SearchAsync(string query);
    Task<List<Episode>> GetLatestReleasesAsync();
    Task<List<ScheduleItem>> GetWeeklyScoopAsync();
    Task<List<Episode>> GetEpisodesAsync(string animeUrl);
    Task<List<VideoServer>> GetVideoServersAsync(string episodeUrl);
    Task<string> ResolveVideoUrlAsync(string url);
    Task<string> GetSynopsisAsync(string animeUrl);
}
