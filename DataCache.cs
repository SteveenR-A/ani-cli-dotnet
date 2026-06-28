using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;

namespace AniCS;

public static class DataCache
{
    public static ConcurrentDictionary<string, byte[]> Images { get; } = new();
    public static ConcurrentDictionary<string, string> Synopsis { get; } = new();

    public static async Task<byte[]> GetImageAsync(HttpClient client, string url)
    {
        if (Images.TryGetValue(url, out var data)) return data;
        try 
        {
            var bytes = await client.GetByteArrayAsync(url);
            Images[url] = bytes;
            return bytes;
        }
        catch 
        {
            return [];
        }
    }
}
