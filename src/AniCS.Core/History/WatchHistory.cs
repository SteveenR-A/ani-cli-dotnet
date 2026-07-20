using System.Text.Json;
using System.Text.Json.Serialization;

namespace AniCS.History;

public class WatchEntry
{
    public string AnimeTitle { get; set; } = string.Empty;
    public string AnimeUrl { get; set; } = string.Empty;
    public string AnimeThumbnailUrl { get; set; } = string.Empty;
    public string LastEpisodeNumber { get; set; } = string.Empty;
    public string LastEpisodeUrl { get; set; } = string.Empty;
    public DateTime WatchedAt { get; set; } = DateTime.Now;
}

[JsonSerializable(typeof(List<WatchEntry>))]
internal partial class WatchHistoryContext : JsonSerializerContext
{
}

/// <summary>
/// Persists watch history to a local JSON file, inspired by ani-cli's history tracking.
/// </summary>
public class WatchHistory
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "anics");
    private static readonly string HistoryFile = Path.Combine(HistoryDir, "history.json");

    private List<WatchEntry> _entries = [];

    public WatchHistory()
    {
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(HistoryFile))
            {
                var json = File.ReadAllText(HistoryFile);
                _entries = JsonSerializer.Deserialize(json, WatchHistoryContext.Default.ListWatchEntry) ?? [];
            }
        }
        catch { _entries = []; }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(HistoryDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var context = new WatchHistoryContext(options);
            var json = JsonSerializer.Serialize(_entries, context.ListWatchEntry);
            File.WriteAllText(HistoryFile, json);
        }
        catch { }
    }

    public void Record(string title, string animeUrl, string thumbnailUrl, string episodeNumber, string episodeUrl)
    {
        var existing = _entries.FirstOrDefault(e =>
            e.AnimeUrl.Equals(animeUrl, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.AnimeThumbnailUrl = thumbnailUrl; // Update thumbnail if it changed or was empty
            existing.LastEpisodeNumber = episodeNumber;
            existing.LastEpisodeUrl = episodeUrl;
            existing.WatchedAt = DateTime.Now;
        }
        else
        {
            _entries.Insert(0, new WatchEntry
            {
                AnimeTitle = title,
                AnimeUrl = animeUrl,
                AnimeThumbnailUrl = thumbnailUrl,
                LastEpisodeNumber = episodeNumber,
                LastEpisodeUrl = episodeUrl,
                WatchedAt = DateTime.Now
            });
        }

        // Keep max 50 entries
        if (_entries.Count > 50)
            _entries = _entries.Take(50).ToList();

        Save();
    }

    public IReadOnlyList<WatchEntry> GetAll() => _entries.AsReadOnly();

    public WatchEntry? FindByTitle(string title) =>
        _entries.FirstOrDefault(e => e.AnimeTitle.Contains(title, StringComparison.OrdinalIgnoreCase));

    public void RemoveEntry(string animeUrl)
    {
        var entry = _entries.FirstOrDefault(e => e.AnimeUrl.Equals(animeUrl, StringComparison.OrdinalIgnoreCase));
        if (entry != null)
        {
            _entries.Remove(entry);
            Save();
        }
    }

    public void Clear()
    {
        _entries.Clear();
        Save();
    }
}

