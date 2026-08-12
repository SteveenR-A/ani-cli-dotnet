using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AniCS.Desktop.Services;

/// <summary>
/// Helpers de reproducción de audio (Openings/Trailers) y apertura en navegador.
/// La reproducción de episodios y las descargas fueron migradas a
/// <c>AniCS.Player.IPlayerBackend</c> y <c>AniCS.Resolver.IResolverBackend</c>.
/// </summary>
public static class DesktopPlayer
{
    private static readonly List<Process> _activeProcesses = new();
    public static event Action? AudioStateChanged;

    static DesktopPlayer()
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => KillAll();
        Console.CancelKeyPress += (s, e) => KillAll();
    }

    private static void KillAll()
    {
        lock (_activeProcesses)
        {
            foreach (var p in _activeProcesses.ToList())
            {
                try
                {
                    if (!p.HasExited) p.Kill(true);
                }
                catch { }
            }
            _activeProcesses.Clear();
        }
    }

    private static Process? _activeAudioProcess = null;

    public static bool IsAudioPlaying => _activeAudioProcess != null && !_activeAudioProcess.HasExited;

    public static void StopAudio()
    {
        try
        {
            if (_activeAudioProcess != null && !_activeAudioProcess.HasExited)
            {
                _activeAudioProcess.Kill(true);
            }
        }
        catch { }
        finally
        {
            _activeAudioProcess = null;
            AudioStateChanged?.Invoke();
        }
    }

    public static void OpenInBrowser(string url)
    {
        try
        {
            if (string.IsNullOrEmpty(url)) return;
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch { }
    }

    public static void PlayAudio(string url, string title, string? referer = null)
    {
        StopAudio();

        var exe = GetExecutablePath("mpv") ?? GetExecutablePath("mpvnet");
        if (exe == null)
        {
            OpenInBrowser(url);
            return;
        }

        var ytdlpPath = GetExecutablePath("yt-dlp");

        var args = new List<string>
        {
            "--force-window=immediate",
            "--autofit=520x280",
            "--cache=yes",
            "--cache-pause=no"
        };

        if (!string.IsNullOrEmpty(ytdlpPath))
        {
            args.Add($"--script-opts=ytdl_hook-ytdl_path={ytdlpPath.Replace("\\", "/")}");
        }

        args.Add($"--title={title}");
        args.Add(url);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = false
            };
            foreach (var arg in args) startInfo.ArgumentList.Add(arg);

            var p = new Process { StartInfo = startInfo };
            p.EnableRaisingEvents = true;
            p.Exited += (s, e) =>
            {
                lock (_activeProcesses) _activeProcesses.Remove(p);
                if (_activeAudioProcess == p)
                {
                    _activeAudioProcess = null;
                    AudioStateChanged?.Invoke();
                }
                try
                {
                    if (p.ExitCode != 0)
                    {
                        OpenInBrowser(url);
                    }
                }
                catch { }
            };
            lock (_activeProcesses) _activeProcesses.Add(p);
            _activeAudioProcess = p;
            p.Start();
            AudioStateChanged?.Invoke();
        }
        catch
        {
            OpenInBrowser(url);
        }
    }

    private static string? GetExecutablePath(string command)
    {
        string extension = OperatingSystem.IsWindows() ? ".exe" : "";
        string fileName = command + extension;

        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths != null)
        {
            foreach (var path in paths)
            {
                var fullPath = Path.Combine(path.Trim(), fileName);
                if (File.Exists(fullPath)) return fullPath;
            }
        }

        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (File.Exists(localPath)) return localPath;

        string currentDirPath = Path.Combine(Environment.CurrentDirectory, fileName);
        if (File.Exists(currentDirPath)) return currentDirPath;

        return null;
    }
}