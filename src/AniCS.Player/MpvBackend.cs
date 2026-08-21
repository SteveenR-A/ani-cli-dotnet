using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

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
    private string? _currentPipeName;
    private readonly SemaphoreSlim _pipeLock = new(1, 1);
    private readonly History.WatchHistory _watchHistory = new();
    private int _volume = 100;

    public string BackendName => "mpv";

    public bool IsAvailable => GetMpvPath() != null;

    public PlaySession? CurrentSession => _currentSession;

    public event Action<PlaySession>? SessionChanged;
    public event Action<string>? ErrorOccurred;

    public MpvBackend()
    {
        try { AppDomain.CurrentDomain.ProcessExit += (_, _) => KillAll(); }
        catch (Exception ex) { AppLogger.Error("MpvBackend.ProcessExitRegistration", ex); }

        try { Console.CancelKeyPress += (_, _) => KillAll(); }
        catch (Exception ex) { AppLogger.Error("MpvBackend.CancelKeyPressRegistration", ex); }
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

    public async Task PauseAsync()
    {
        await SendMpvCommandAsync("set_property", "pause", true);
    }

    public async Task ResumeAsync()
    {
        await SendMpvCommandAsync("set_property", "pause", false);
    }

    public async Task SeekAsync(double seconds)
    {
        await SendMpvCommandAsync("seek", seconds, "absolute");
    }

    public int Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0, 200);
            _ = SendMpvCommandAsync("set_property", "volume", _volume);
        }
    }

    public void Stop()
    {
        _currentPipeName = null;
        KillAll();
    }

    public void Dispose()
    {
        _currentPipeName = null;
        _pipeLock.Dispose();
        KillAll();
    }

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

        var args = BuildMpvArgs(url, referer, quality);

        var pipeName = "anics_mpv_" + Guid.NewGuid().ToString("N");
        _currentPipeName = pipeName;
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

            var p = new Process();
            p.StartInfo = si;
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
                catch (Exception ex)
                {
                    AppLogger.Error("MpvBackend.ExitedHandler", ex);
                }
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

    private static List<string> BuildMpvArgs(string url, string? referer, string quality)
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
            if (Uri.TryCreate(referer, UriKind.Absolute, out var uri))
            {
                headerList.Add($"Origin: {uri.GetLeftPart(UriPartial.Authority)}");
            }
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
                await _pipeLock.WaitAsync();
                try
                {
                    using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
                    await pipe.ConnectAsync(600);

                    using var writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, leaveOpen: true);
                    writer.AutoFlush = true;
                    using var reader = new StreamReader(pipe, System.Text.Encoding.UTF8, leaveOpen: true);

                    await writer.WriteLineAsync("{\"command\": [\"get_property\", \"time-pos\"]}");
                    var posResp = await reader.ReadLineAsync();

                    await writer.WriteLineAsync("{\"command\": [\"get_property\", \"duration\"]}");
                    var durResp = await reader.ReadLineAsync();

                    double pos = ParseMpvNumber(posResp);
                    double dur = ParseMpvNumber(durResp);

                    if (pos > 0) lastPosition = pos;
                    if (dur > 0) lastDuration = dur;
                }
                finally
                {
                    _pipeLock.Release();
                }

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
                        _watchHistory.UpdateProgress(mediaUrl, lastPosition, lastDuration, isCompleted);
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("MpvBackend.WatchHistoryUpdate", ex);
                    }
                }
            }
            catch (Exception ex)
            {
                // Conexión IPC transitoria mientras mpv arranca o cierra
                AppLogger.Warn("MpvBackend.Ipc", ex.Message);
            }

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
                _watchHistory.UpdateProgress(mediaUrl, lastPosition, lastDuration, isCompleted);
            }
            catch (Exception ex)
            {
                AppLogger.Error("MpvBackend.FinalWatchHistoryUpdate", ex);
            }
        }
    }

    private async Task SendMpvCommandAsync(params object[] args)
    {
        var pipeName = _currentPipeName;
        if (string.IsNullOrEmpty(pipeName)) return;

        await _pipeLock.WaitAsync();
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            await pipe.ConnectAsync(400);

            using var writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, leaveOpen: false);
            writer.AutoFlush = true;

            var commandObj = new { command = args };
            string json = JsonSerializer.Serialize(commandObj);
            await writer.WriteLineAsync(json);
        }
        catch (Exception ex)
        {
            AppLogger.Warn("MpvBackend.SendCommand", ex.Message);
        }
        finally
        {
            _pipeLock.Release();
        }
    }

    private static double ParseMpvNumber(string? json)
    {
        if (string.IsNullOrEmpty(json)) return 0;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var dataEl) &&
                dataEl.ValueKind == JsonValueKind.Number)
                return dataEl.GetDouble();
        }
        catch (Exception ex)
        {
            AppLogger.Warn("MpvBackend.ParseMpvNumber", ex.Message);
        }
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
                try
                {
                    if (!p.HasExited) p.Kill(true);
                }
                catch (Exception ex)
                {
                    AppLogger.Error("MpvBackend.KillAll", ex);
                }
            }
            _activeProcesses.Clear();
        }
    }
}
