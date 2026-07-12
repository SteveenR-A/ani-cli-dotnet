using System;
using System.Net.Http;
using System.Threading.Tasks;
using AniCS.Extractors;
using AniCS.Models;

class Program
{
    static async Task Main(string[] args)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
        var extractor = new JKAnimeExtractor(client);

        var episodes = await extractor.GetLatestReleasesAsync();
        if (episodes.Count > 0)
        {
            Console.WriteLine($"Found episode: {episodes[0].Url}");
            var servers = await extractor.GetVideoServersAsync(episodes[0].Url);
            foreach (var server in servers)
            {
                Console.WriteLine($"Server: {server.Name} - {server.Url}");
                if (server.Name.Contains("Mediafire", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Resolving Mediafire...");
                    var videoUrl = await extractor.ResolveVideoUrlAsync(server.Url);
                    Console.WriteLine($"Resolved: {videoUrl}");
                }
            }
        }
    }
}
