using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace AniCS;

public static class DataCache
{
    private static readonly string CacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniCS", "Cache", "Images");
    
    // RAM Cache for API Data with TTL
    private class CacheEntry<T>
    {
        public T Data { get; set; } = default!;
        public DateTime Expiration { get; set; }
    }
    
    private static readonly ConcurrentDictionary<string, object> _ramCache = new();

    static DataCache()
    {
        if (!Directory.Exists(CacheDir))
        {
            Directory.CreateDirectory(CacheDir);
        }
    }

    public static string GetImageCachePath(string url)
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(url)));
        return Path.Combine(CacheDir, hash + ".img");
    }

    /// <summary>
    /// Disk-based cache for images. Images are downloaded once and read from disk to save bandwidth and memory.
    /// </summary>
    public static async Task<byte[]> GetImageAsync(HttpClient client, string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url)) return [];

        var filePath = GetImageCachePath(url);

        if (File.Exists(filePath))
        {
            try
            {
                return await File.ReadAllBytesAsync(filePath, cancellationToken);
            }
            catch { /* Corrupted file or locked */ }
        }

        try
        {
            var bytes = await client.GetByteArrayAsync(url, cancellationToken);
            var dir = Path.GetDirectoryName(filePath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllBytesAsync(filePath, bytes, cancellationToken);
            return bytes;
        }
        catch
        {
            try { if (File.Exists(filePath)) File.Delete(filePath); } catch {}
            return [];
        }
    }

    /// <summary>
    /// RAM-based cache for API requests. Zero disk writes. Uses a Time-To-Live (TTL).
    /// </summary>
    public static async Task<T> GetOrFetchDataAsync<T>(string key, TimeSpan ttl, Func<Task<T>> fetcher)
    {
        if (_ramCache.TryGetValue(key, out var cachedObj) && cachedObj is CacheEntry<T> entry)
        {
            if (DateTime.UtcNow < entry.Expiration)
            {
                return entry.Data;
            }
            else
            {
                // Expired
                _ramCache.TryRemove(key, out _);
            }
        }

        var data = await fetcher();
        
        // Cache if valid
        if (data != null && (data is not System.Collections.ICollection col || col.Count > 0))
        {
            _ramCache[key] = new CacheEntry<T>
            {
                Data = data,
                Expiration = DateTime.UtcNow.Add(ttl)
            };
        }

        return data;
    }

    /// <summary>
    /// Clears the RAM cache, forcing all next queries to hit the web.
    /// </summary>
    public static void ClearRamCache()
    {
        _ramCache.Clear();
    }

    /// <summary>
    /// Deletes all files in the cache directory and the directory itself.
    /// </summary>
    public static void ClearCacheDirectory()
    {
        try
        {
            if (Directory.Exists(CacheDir))
            {
                Directory.Delete(CacheDir, true);
            }
        }
        catch
        {
            // Ignore directory deletion errors
        }
    }
}
