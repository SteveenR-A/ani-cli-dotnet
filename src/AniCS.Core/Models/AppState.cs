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

        public AppState(HttpClient http, List<IAnimeExtractor> extractors, IAnimeExtractor activeExtractor, WatchHistory history)
        {
            Http = http;
            Extractors = extractors;
            ActiveExtractor = activeExtractor;
            History = history;
        }
    }
}
