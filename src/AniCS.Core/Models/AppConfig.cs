namespace AniCS.Models;

public class AppConfig
{
    public string LastSeenVersion { get; set; } = "";
    public int MaxImageCacheCount { get; set; } = 100;
    
    // Can be Auto, Mpv, Vlc
    public string DefaultPlayer { get; set; } = "Auto";
    
    // Path to executable if not in PATH
    public string CustomPlayerExePath { get; set; } = string.Empty;
    
    // Content type preference (Anime or Donghua)
    public string ContentType { get; set; } = "Anime";

    // Extractor domain/name, e.g. "jkanime.net" or "animeav1.com"
    public string DefaultExtractor { get; set; } = "jkanime.net";
    
    // List of User-Agents to rotate automatically to avoid bot detection
    public System.Collections.Generic.List<string> UserAgents { get; set; } = new()
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML like Gecko) Chrome/125.0.0.0 Safari/537.36",
        "Mozilla/5.0 (X11; Linux x86_64; rv:126.0) Gecko/20100101 Firefox/126.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_5) AppleWebKit/605.1.15 (KHTML like Gecko) Version/17.5 Safari/605.1.15",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML like Gecko) Chrome/124.0.0.0 Safari/537.36 Edg/124.0.0.0"
    };

    private string? _sessionUserAgent;

    [System.Text.Json.Serialization.JsonIgnore]
    public string RandomUserAgent
    {
        get
        {
            if (_sessionUserAgent != null)
                return _sessionUserAgent;
                
            if (UserAgents == null || UserAgents.Count == 0)
                _sessionUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36";
            else
                _sessionUserAgent = UserAgents[System.Random.Shared.Next(UserAgents.Count)];
                
            return _sessionUserAgent;
        }
    }

    // UI Theme
    public string Theme { get; set; } = "Dark";

    // Window State
    public string WindowState { get; set; } = "Maximized";
    
    public string PreferredQuality { get; set; } = "720p";

    public double WindowWidth { get; set; } = 1000;
    public double WindowHeight { get; set; } = 700;

    // UI Paradigm Mode
    public string UiParadigm { get; set; } = "Normal";

    // Use Spatial HUD for episode selection
    public bool UseSpatialHud { get; set; } = false;
}

[System.Text.Json.Serialization.JsonSerializable(typeof(AppConfig))]
public partial class AppConfigJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
