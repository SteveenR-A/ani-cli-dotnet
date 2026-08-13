using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using AniCS;

namespace AniCS.Player;

/// <summary>
/// Backend de reproducción que delega a mpv.exe / mpvnet.exe externo.
/// Comportamiento idéntico al DesktopPlayer original, refactorizado como IPlayerBackend.
/// </summary>
public sealed class MpvBackend : IPlayerBackend
{
    private readonly List<Process> _activeProcesses = new();
    private PlaySession? _currentSession;
    private string? _cachedMpvPath;

    public string BackendName => "mpv";

    public bool IsAvailable => GetMpvPath() != null;

    public PlaySession? CurrentSession => _currentSession;

    public event Action<PlaySession>? SessionChanged;
    public event Action<string>? ErrorOccurred;

    public MpvBackend()
    {
        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll(); } catch { }
        try { Console.CancelKeyPress += (_, _) => KillAll(); } catch { }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IPlayerBackend
    // ──────────────────────────────────────────────────────────────────────────

    public Task PlayAsync(string url, string title, PlayOptions? opts = null)
    {
        opts ??= new PlayOptions();
        Play(url, title, opts.Referer, opts.Quality);
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        // mpv no tiene IPC de pausa en el MpvBackend básico (el IPC lo hace DesktopPlayer).
        // En Fase 3 LibVlcBackend tendrá control completo.
        return Task.CompletedTask;
    }

    public Task ResumeAsync() => Task.CompletedTask;

    public Task SeekAsync(double seconds)
    {
        return Task.CompletedTask;
    }

    public int Volume { get; set; } = 100;

    public void Stop()
    {
        KillAll();
    }

    public void Dispose() => KillAll();

    // ──────────────────────────────────────────────────────────────────────────
    // Implementación interna (migrada de DesktopPlayer.cs)
    // ──────────────────────────────────────────────────────────────────────────

    private void Play(string url, string title, string? referer, string quality = "Mejor")
    {
        var exe = GetMpvPath();
        if (exe == null)
        {
            ErrorOccurred?.Invoke("mpv no está instalado. Por favor, descarga mpv y agrégalo al PATH o a la carpeta del programa.");
            return;
        }

        var args = BuildMpvArgs(url, title, referer, quality, exe);

        var pipeName = "anics_mpv_" + Guid.NewGuid().ToString("N");
        args.Add("--save-position-on-quit");
        args.Add($"--input-ipc-server=\\\\.\\pipe\\{pipeName}");
        args.Add($"--title={title}");
        args.Add(url);

        try
        {
            var si = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = false,
                CreateNoWindow = false,
            };
            foreach (var arg in args) si.ArgumentList.Add(arg);

            var p = new Process { StartInfo = si };
            p.EnableRaisingEvents = true;
            p.Exited += (_, _) =>
            {
                lock (_activeProcesses) _activeProcesses.Remove(p);
                try
                {
                    if (p.ExitCode != 0)
                    {
                        var duration = p.ExitTime - p.StartTime;
                        if (duration.TotalSeconds < 10)
                            ErrorOccurred?.Invoke($"El reproductor falló (Código: {p.ExitCode}). El video podría no estar disponible.");
                    }
                }
                catch { }
            };
            lock (_activeProcesses) _activeProcesses.Add(p);
            p.Start();

            // Monitoreo de progreso vía IPC (named pipe) en background
            _ = Task.Run(() => MonitorMpvIpcAsync(p, url, title, pipeName));
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Error al iniciar mpv: {ex.Message}");
        }
    }

    private List<string> BuildMpvArgs(string url, string title, string? referer, string quality, string exe)
    {
        var args = new List<string>
        {
            "--force-window=yes",
            "--cache=yes",
            "--cache-pause=no",
        };

        if (url.Contains(".m3u8") || url.Contains(".mp4"))
        {
            args.Add("--ytdl=no");
        }
        else if (quality != "Mejor")
        {
            string height = quality.Replace("p", "");
            args.Add($"--ytdl-format=bestvideo[height<=?{height}]+bestaudio/best[height<=?{height}]");
        }

        args.Add("--demuxer-max-bytes=150M");
        args.Add("--demuxer-max-back-bytes=50M");
        args.Add("--demuxer-readahead-secs=120");
        args.Add("--cache-secs=120");
        args.Add("--hr-seek=yes");
        args.Add("--network-timeout=15");
        args.Add("--demuxer-lavf-o=reconnect=1,reconnect_streamed=1,reconnect_on_http_error=4xx,reconnect_delay_max=10");
        args.Add("--force-window=immediate");
        args.Add("--keep-open=yes");
        args.Add("--geometry=65%");
        args.Add("--autofit=1280x720");

        var ua = ConfigManager.Current.RandomUserAgent.Replace(",", ";");
        var headerList = new List<string>
        {
            "Accept-Language: es-419",
            "Accept: */*",
            "Sec-Fetch-Dest: empty",
            "Sec-Fetch-Mode: cors",
            "Sec-Fetch-Site: cross-site",
            $"User-Agent: {ua}"
        };

        if (!string.IsNullOrEmpty(referer))
        {
            headerList.Add($"Referer: {referer}");
            try
            {
                var uri = new Uri(referer);
                headerList.Add($"Origin: {uri.GetLeftPart(UriPartial.Authority)}");
            }
            catch { }
        }

        args.Add($"--http-header-fields={string.Join(",", headerList)}");
        return args;
    }

    private async Task MonitorMpvIpcAsync(Process p, string mediaUrl, string title, string pipeName)
    {
        await Task.Delay(1500);
        double lastPosition = 0;
        double lastDuration = 0;
        bool isCompleted = false;

        while (!p.HasExited)
        {
            try
            {
                using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut);
                await pipe.ConnectAsync(1000);

                using var writer = new System.IO.StreamWriter(pipe, System.Text.Encoding.UTF8, leaveOpen: true) { AutoFlush = true };
                using var reader = new System.IO.StreamReader(pipe, System.Text.Encoding.UTF8, leaveOpen: true);

                await writer.WriteLineAsync("{\"command\": [\"get_property\", \"time-pos\"]}");
                var posResp = await reader.ReadLineAsync();

                await writer.WriteLineAsync("{\"command\": [\"get_property\", \"duration\"]}");
                var durResp = await reader.ReadLineAsync();

                double pos = ParseMpvNumber(posResp);
                double dur = ParseMpvNumber(durResp);

                if (pos > 0) lastPosition = pos;
                if (dur > 0) lastDuration = dur;

                if (lastDuration > 0)
                    isCompleted = (lastPosition / lastDuration >= 0.88) || (lastDuration - lastPosition <= 90);

                if (lastPosition > 0)
                {
                    var session = new PlaySession(mediaUrl, title, lastPosition, lastDuration,
                        PlayerState.Playing, isCompleted);
                    _currentSession = session;
                    SessionChanged?.Invoke(session);

                    try
                    {
                        var history = new AniCS.History.WatchHistory();
                        history.UpdateProgress(mediaUrl, lastPosition, lastDuration, isCompleted);
                    }
                    catch { }
                }
            }
            catch { }

            await Task.Delay(2500);
        }

        // Reporte final al salir mpv
        if (lastPosition > 0)
        {
            if (lastDuration > 0)
                isCompleted = (lastPosition / lastDuration >= 0.88) || (lastDuration - lastPosition <= 90);

            var finalSession = new PlaySession(mediaUrl, title, lastPosition, lastDuration,
                PlayerState.Ended, isCompleted);
            _currentSession = finalSession;
            SessionChanged?.Invoke(finalSession);

            try
            {
                var history = new AniCS.History.WatchHistory();
                history.UpdateProgress(mediaUrl, lastPosition, lastDuration, isCompleted);
            }
            catch { }
        }
    }

    private static double ParseMpvNumber(string? json)
    {
        if (string.IsNullOrEmpty(json)) return 0;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                return dataEl.GetDouble();
        }
        catch { }
        return 0;
    }

    private string? GetMpvPath()
    {
        if (_cachedMpvPath != null) return _cachedMpvPath;

        _cachedMpvPath = FindExe("mpv") ?? FindExe("mpvnet");
        return _cachedMpvPath;
    }

    private static string? FindExe(string name)
    {
        string fileName = OperatingSystem.IsWindows() ? $"{name}.exe" : name;

        var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (paths != null)
        {
            foreach (var dir in paths)
            {
                var full = Path.Combine(dir.Trim(), fileName);
                if (File.Exists(full)) return full;
            }
        }

        var local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
        if (File.Exists(local)) return local;

        var cwd = Path.Combine(Environment.CurrentDirectory, fileName);
        if (File.Exists(cwd)) return cwd;

        return null;
    }

    private void KillAll()
    {
        lock (_activeProcesses)
        {
            foreach (var p in _activeProcesses.ToList())
            {
                try { if (!p.HasExited) p.Kill(true); } catch { }
            }
            _activeProcesses.Clear();
        }
    }
}
