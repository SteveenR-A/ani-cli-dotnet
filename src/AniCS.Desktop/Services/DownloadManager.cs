using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AniCS.Models;

namespace AniCS.Desktop.Services;

public class DownloadedEpisode
{
    public string EpisodeNumber { get; set; } = string.Empty;
    public string EpisodeTitle { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; } = DateTime.Now;
}

public class DownloadedAnime
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public List<DownloadedEpisode> Episodes { get; set; } = new();
}



public static class DownloadManager
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "anics");
    private static readonly string DownloadsFile = Path.Combine(ConfigDir, "downloads.json");

    private static List<DownloadedAnime> _downloads = new();

    static DownloadManager()
    {
        Load();
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(DownloadsFile))
            {
                var json = File.ReadAllText(DownloadsFile);
                _downloads = JsonSerializer.Deserialize<List<DownloadedAnime>>(json) ?? new();
                CleanupMissingFiles();
            }
        }
        catch { _downloads = new(); }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_downloads, options);
            File.WriteAllText(DownloadsFile, json);
        }
        catch { }
    }

    /// <summary>
    /// Checks if files still exist on disk. If user deleted them manually, removes them from the registry.
    /// </summary>
    private static void CleanupMissingFiles()
    {
        bool changed = false;
        foreach (var anime in _downloads.ToList())
        {
            var existingEpisodes = anime.Episodes.Where(e => File.Exists(e.FilePath)).ToList();
            if (existingEpisodes.Count != anime.Episodes.Count)
            {
                anime.Episodes = existingEpisodes;
                changed = true;
            }
            if (anime.Episodes.Count == 0)
            {
                _downloads.Remove(anime);
                changed = true;
            }
        }
        
        if (changed)
            Save();
    }

    public static void RecordDownload(string animeTitle, string animeUrl, string thumbnailUrl, string episodeNumber, string episodeTitle, string filePath)
    {
        Load(); // Reload to sync with possible other instances
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime == null)
        {
            anime = new DownloadedAnime
            {
                Title = animeTitle,
                Url = animeUrl,
                ThumbnailUrl = thumbnailUrl
            };
            _downloads.Insert(0, anime);
        }

        var ep = anime.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
        if (ep != null)
        {
            ep.FilePath = filePath;
            ep.DownloadedAt = DateTime.Now;
        }
        else
        {
            anime.Episodes.Add(new DownloadedEpisode
            {
                EpisodeNumber = episodeNumber,
                EpisodeTitle = episodeTitle,
                FilePath = filePath,
                DownloadedAt = DateTime.Now
            });
        }
        Save();
    }

    public static IReadOnlyList<DownloadedAnime> GetAll()
    {
        CleanupMissingFiles();
        return _downloads.AsReadOnly();
    }

    public static void DeleteEpisode(string animeUrl, string episodeNumber)
    {
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime != null)
        {
            var ep = anime.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
            if (ep != null)
            {
                if (File.Exists(ep.FilePath))
                {
                    try { File.Delete(ep.FilePath); } catch { }
                }
                anime.Episodes.Remove(ep);
                if (anime.Episodes.Count == 0)
                {
                    _downloads.Remove(anime);
                }
                Save();
            }
        }
    }

    public static void DeleteAnime(string animeUrl)
    {
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime != null)
        {
            foreach (var ep in anime.Episodes)
            {
                if (File.Exists(ep.FilePath))
                {
                    try { File.Delete(ep.FilePath); } catch { }
                }
            }
            _downloads.Remove(anime);
            Save();
        }
    }

    public static bool IsEpisodeDownloaded(string animeUrl, string episodeNumber)
    {
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime != null)
        {
            var ep = anime.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
            return ep != null && File.Exists(ep.FilePath);
        }
        return false;
    }
}
