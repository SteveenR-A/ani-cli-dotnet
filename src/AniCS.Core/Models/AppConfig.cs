namespace AniCS.Models;

public class AppConfig
{
    public int MaxImageCacheCount { get; set; } = 30;
    
    // Can be Auto, Mpv, Vlc
    public string DefaultPlayer { get; set; } = "Auto";
    
    // Path to executable if not in PATH
    public string CustomPlayerExePath { get; set; } = string.Empty;
    
    // Extractor domain/name, e.g. "jkanime.net" or "animeav1.com"
    public string DefaultExtractor { get; set; } = "jkanime.net";
    
    // List of User-Agents to rotate automatically to avoid bot detection
    public System.Collections.Generic.List<string> UserAgents { get; set; } = new()
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64; rv:126.0) Gecko/20100101 Firefox/126.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Safari/605.1.15",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Edg/124.0.0.0"
    };

    [System.Text.Json.Serialization.JsonIgnore]
    public string RandomUserAgent
    {
        get
        {
            if (UserAgents == null || UserAgents.Count == 0)
                return "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
            
            return UserAgents[new System.Random().Next(UserAgents.Count)];
        }
    }

    // UI Theme
    public string Theme { get; set; } = "Dark";
}
