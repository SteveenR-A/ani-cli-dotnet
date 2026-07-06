using AniCS.Extractors;
using System.Net.Http;
using System.Threading.Tasks;
using System;

class Program
{
    static async Task Main()
    {
        var client = new HttpClient();
        var ext = new JKAnimeExtractor(client);
        var res = await ext.GetLatestReleasesAsync();
        Console.WriteLine($"Got {res.Count} latest releases.");
        foreach(var r in res) {
            Console.WriteLine($"- {r.Title}");
        }
        
        var search = await ext.SearchAsync("naruto");
        Console.WriteLine($"Got {search.Count} search results.");
    }
}
