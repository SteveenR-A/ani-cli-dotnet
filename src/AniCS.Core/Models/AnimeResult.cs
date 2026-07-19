namespace AniCS.Models;

public class AnimeResult
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Additional properties for details
    public string Synopsis { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> Genres { get; set; } = new();
    public string Studios { get; set; } = string.Empty;
    public string Season { get; set; } = string.Empty;
    public string Demography { get; set; } = string.Empty;
    public string Languages { get; set; } = string.Empty;
    public string TotalEpisodes { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string Broadcast { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    
    // Top Animes properties
    public int Rank { get; set; } = 0;
    public string Votes { get; set; } = string.Empty;
}
