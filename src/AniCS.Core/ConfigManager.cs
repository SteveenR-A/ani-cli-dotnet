using System;
using System.IO;
using System.Text.Json;
using AniCS.Models;

namespace AniCS;

public static class ConfigManager
{
    private static readonly string ConfigPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniCS", "config.json");
    
    public static AppConfig Current { get; private set; }

    static ConfigManager()
    {
        Current = Load();
    }

    public static AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    return config;
                }
            }
            catch
            {
                // Fallback to default
            }
        }
        
        var @default = new AppConfig();
        Save(@default);
        return @default;
    }

    public static void Save(AppConfig config)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigPath, json);
            Current = config;
        }
        catch
        {
            // Ignore write errors for now
        }
    }
}
