using System.Net.Http;
using System.Collections.Generic;
using AniCS.Extractors;
using AniCS.History;

namespace AniCS.Models
{
    public class AppState
    {
        public HttpClient Http { get; }
        public List<IAnimeExtractor> Extractors { get; }
        public IAnimeExtractor ActiveExtractor { get; set; }
        public WatchHistory History { get; }
        public AniCS.Services.IPlayerService PlayerService { get; }

        public AppState(HttpClient http, IEnumerable<IAnimeExtractor> extractors, WatchHistory history, AniCS.Services.IPlayerService playerService)
        {
            Http = http;
            Extractors = new List<IAnimeExtractor>(extractors);
            History = history;
            PlayerService = playerService;
            
            var defaultConfig = ConfigManager.Current;
            ActiveExtractor = Extractors.Find(e => e.Domain.Contains(defaultConfig.DefaultExtractor, System.StringComparison.OrdinalIgnoreCase)) ?? Extractors[0];
        }
    }
}
