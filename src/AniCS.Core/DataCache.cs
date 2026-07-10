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
    private static readonly ConcurrentDictionary<string, byte[]> _imageSessionCache = new();

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
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        // Use .jpg extension so that ImageSharp (used by Spectre.Console CanvasImage)
        // can correctly detect the image format from the file extension.
        return Path.Combine(CacheDir, hash + ".jpg");
    }

    /// <summary>
    /// Disk-based cache for images. Images are downloaded once and read from disk to save bandwidth and memory.
    /// </summary>
    public static async Task<byte[]> GetImageAsync(HttpClient client, string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url)) return [];

        if (_imageSessionCache.TryGetValue(url, out var sessionBytes))
        {
            return sessionBytes;
        }

        var filePath = GetImageCachePath(url);

        if (File.Exists(filePath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow);
                _imageSessionCache[url] = bytes;
                return bytes;
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
            _imageSessionCache[url] = bytes;
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
        _imageSessionCache.Clear();
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

    /// <summary>
    /// Performs an LRU cleanup on the disk image cache, keeping only the most recently accessed files.
    /// </summary>
    public static void CleanupImageCache(int maxFiles = 30)
    {
        if (!Directory.Exists(CacheDir)) return;

        try
        {
            var files = Directory.GetFiles(CacheDir, "*.jpg")
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastAccessTimeUtc) // Oldest first
                .ToList();

            int filesToDelete = files.Count - maxFiles;
            if (filesToDelete <= 0) return;

            foreach (var file in files.Take(filesToDelete))
            {
                try
                {
                    file.Delete();
                }
                catch { /* Ignore locked files */ }
            }
        }
        catch { /* Ignore directory access errors */ }
    }
}
