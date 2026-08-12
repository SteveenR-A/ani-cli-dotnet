using System;
using System.IO;

namespace AniCS;

/// <summary>
/// Centralized logging for unhandled exceptions and diagnostics.
/// Writes to %LocalAppData%/AniCS/logs/ instead of the working directory,
/// so it works regardless of where the app is launched from.
/// </summary>
public static class AppLogger
{
    private static string LogDir =>
        Path.Combine(ConfigManager.BaseDataPath, "logs");


    private static readonly object SyncRoot = new();

    public static void Error(string source, Exception? exception)
    {
        if (exception == null) return;
        Write("ERROR", source, exception.ToString());
    }

    public static void Error(string source, string message)
    {
        Write("ERROR", source, message);
    }

    public static void Info(string source, string message)
    {
        Write("INFO", source, message);
    }

    private static void Write(string level, string source, string content)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var filePath = Path.Combine(LogDir, $"Log-{DateTime.Now:yyyyMMdd}.txt");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] [{source}]{Environment.NewLine}{content}{Environment.NewLine}";
            lock (SyncRoot)
            {
                File.AppendAllText(filePath, line);
            }
        }
        catch
        {
            // Never let logging break the app.
        }
    }
}