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

    public static string GetImageCachePath(string url, string category = "")
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".jpg";

        // 1. Path en la raíz unificada (por defecto)
        var rootPath = Path.Combine(CacheDir, hash);
        if (File.Exists(rootPath)) return rootPath;

        // 2. Si no está en la raíz, buscar si existe en la categoría indicada
        if (!string.IsNullOrEmpty(category))
        {
            var categoryPath = Path.Combine(CacheDir, category, hash);
            if (File.Exists(categoryPath)) return categoryPath;
        }

        // 3. Fallback: buscar en subcarpetas legacy (Anime / Donghua)
        var animePath = Path.Combine(CacheDir, "Anime", hash);
        if (File.Exists(animePath)) return animePath;

        var donghuaPath = Path.Combine(CacheDir, "Donghua", hash);
        if (File.Exists(donghuaPath)) return donghuaPath;

        return rootPath;
    }

    /// <summary>
    /// Disk-based cache for images. Images are downloaded once and read from disk to save bandwidth and memory.
    /// </summary>
    public static async Task<byte[]> GetImageAsync(HttpClient client, string url, string category = "", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url)) return [];

        var filePath = GetImageCachePath(url, category);

        if (File.Exists(filePath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow);
                return bytes;
            }
            catch { /* Corrupted file or locked */ }
        }

        try
        {
            var bytes = await client.GetByteArrayAsync(url, cancellationToken);
            // Guardar siempre en la raíz de CacheDir para unificar todas las imágenes
            var targetPath = Path.Combine(CacheDir, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))) + ".jpg");
            var dir = Path.GetDirectoryName(targetPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            await File.WriteAllBytesAsync(targetPath, bytes, cancellationToken);
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
    /// Clears the RAM cache, forcing all next queries to hit the web and running garbage collection.
    /// </summary>
    public static void ClearRamCache()
    {
        _ramCache.Clear();
        GC.Collect(2, GCCollectionMode.Forced, true, true);
        GC.WaitForPendingFinalizers();
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

    private static HashSet<string> GetProtectedHashes()
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var history = new AniCS.History.WatchHistory();
            foreach (var entry in history.GetAll())
            {
                if (!string.IsNullOrEmpty(entry.AnimeThumbnailUrl))
                {
                    hashes.Add(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.AnimeThumbnailUrl))) + ".jpg");
                }
            }
        }
        catch { }
        return hashes;
    }

    /// <summary>
    /// Performs an LRU cleanup on the disk image cache, keeping only the most recently accessed files.
    /// </summary>
    public static void CleanupImageCache(int maxFiles = 30)
    {
        if (!Directory.Exists(CacheDir)) return;

        try
        {
            var protectedFiles = GetProtectedHashes();

            var files = Directory.GetFiles(CacheDir, "*.jpg", SearchOption.AllDirectories)
                .Select(f => new FileInfo(f))
                .Where(f => !protectedFiles.Contains(f.Name)) // Ignorar archivos protegidos por el historial
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
