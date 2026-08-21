using System;
using System.IO;
using System.Text.Json;
using AniCS.Models;

namespace AniCS;

public static class ConfigManager
{
    /// <summary>
    /// Base directory for all AniCS data (config, cache, logs, history).
    /// Defaults to %LocalAppData%/AniCS on Windows.
    /// Set this once at startup from the platform entry-point before any other
    /// AniCS code runs — e.g. in Android's MainActivity.OnCreate().
    /// </summary>
    public static string BaseDataPath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AniCS");

    // ConfigPath is computed on each call so it respects BaseDataPath set by Android at startup.
    private static string ConfigPath => Path.Combine(BaseDataPath, "config.json");

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
                var config = JsonSerializer.Deserialize(json, AppConfigJsonContext.Default.AppConfig);
                if (config != null)
                {
                    return config;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn("ConfigManager", $"Failed to load config: {ex.Message}");
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
            var context = new AppConfigJsonContext(options);
            var json = JsonSerializer.Serialize(config, context.AppConfig);
            File.WriteAllText(ConfigPath, json);
            Current = config;
        }
        catch (Exception ex)
        {
            AppLogger.Error("ConfigManager", ex);
        }
    }
}
