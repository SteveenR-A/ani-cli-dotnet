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

    public static string GetImageCachePath(string url, string category = "")
    {
        if (string.IsNullOrEmpty(url)) return string.Empty;
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url)));
        var targetDir = string.IsNullOrEmpty(category) ? CacheDir : Path.Combine(CacheDir, category);
        return Path.Combine(targetDir, hash + ".jpg");
    }

    /// <summary>
    /// Disk-based cache for images. Images are downloaded once and read from disk to save bandwidth and memory.
    /// </summary>
    public static async Task<byte[]> GetImageAsync(HttpClient client, string url, string category = "", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(url)) return [];

        var sessionKey = category + "_" + url;
        if (_imageSessionCache.TryGetValue(sessionKey, out var sessionBytes))
        {
            return sessionBytes;
        }

        var filePath = GetImageCachePath(url, category);

        if (File.Exists(filePath))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                File.SetLastAccessTimeUtc(filePath, DateTime.UtcNow);
                _imageSessionCache[sessionKey] = bytes;
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
            _imageSessionCache[sessionKey] = bytes;
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
            var subDirs = Directory.GetDirectories(CacheDir);
            
            // Dividir el límite de archivos entre las categorías disponibles (ej. 100 / 2 = 50 por carpeta)
            int limitPerFolder = subDirs.Length > 0 ? Math.Max(1, maxFiles / subDirs.Length) : maxFiles;

            var protectedFiles = GetProtectedHashes();

            foreach (var dir in subDirs)
            {
                CleanFolder(dir, limitPerFolder, protectedFiles);
            }

            // También limpiar la raíz en caso de haber imágenes sin categoría
            CleanFolder(CacheDir, limitPerFolder, protectedFiles);
        }
        catch { /* Ignore directory access errors */ }
    }

    private static void CleanFolder(string folderPath, int maxFilesInFolder, HashSet<string> protectedFiles)
    {
        try
        {
            var files = Directory.GetFiles(folderPath, "*.jpg", SearchOption.TopDirectoryOnly)
                .Select(f => new FileInfo(f))
                .Where(f => !protectedFiles.Contains(f.Name)) // Ignorar archivos protegidos por el historial
                .OrderBy(f => f.LastAccessTimeUtc) // Oldest first
                .ToList();

            int filesToDelete = files.Count - maxFilesInFolder;
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
