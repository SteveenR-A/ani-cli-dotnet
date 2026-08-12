using LibVLCSharp.Shared;
using System;
using System.Threading.Tasks;
using AniCS;

namespace AniCS.Player;

/// <summary>
/// Backend de reproducción nativo usando LibVLC embebido.
/// 
/// ORDEN CRÍTICO para renderizado embebido (no ventana separada):
///   1. LibVLC + MediaPlayer se crean AQUÍ en el constructor (inicialización eager).
///   2. VideoPlayerControl.Attach() asigna VideoView.MediaPlayer = backend.MediaPlayer (no-null).
///   3. Solo entonces PlayAsync() llama a _mediaPlayer.Play() → VLC renderiza en el VideoView.
/// </summary>
public sealed class LibVlcBackend : IPlayerBackend
{
    private LibVLC?        _libVlc;
    private MediaPlayer?   _mediaPlayer;
    private bool           _isInitialized;
    private PlaySession?   _currentSession;
    private string         _currentUrl   = "";
    private string         _currentTitle = "";
    private System.Threading.Timer? _progressTimer;

    public string BackendName => "LibVLC";
    public bool   IsAvailable => _isInitialized;

    public PlaySession? CurrentSession => _currentSession;

    public event Action<PlaySession>? SessionChanged;
    public event Action<string>?      ErrorOccurred;
    /// <summary>
    /// Fired when a mid-playback error occurs AFTER video was already playing.
    /// Arg = last known position in seconds so the caller can resume there.
    /// </summary>
    public event Action<double>? RecoverRequested;

    // ── Propiedad pública para que VideoPlayerControl acceda al MediaPlayer ──
    public MediaPlayer? MediaPlayer => _mediaPlayer;

    // ──────────────────────────────────────────────────────────────────────────
    // Constructor — inicialización EAGER
    // ──────────────────────────────────────────────────────────────────────────

    public LibVlcBackend()
    {
        try
        {
            LibVLCSharp.Shared.Core.Initialize();
            _libVlc      = new LibVLC(enableDebugLogs: false);
            _mediaPlayer = new MediaPlayer(_libVlc);

            _mediaPlayer.EndReached      += OnEndReached;
            _mediaPlayer.EncounteredError += OnError;
            _mediaPlayer.Playing         += OnPlaying;
            _mediaPlayer.Paused          += OnPaused;

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LibVlcBackend] Init failed: {ex.Message}");
            _isInitialized = false;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IPlayerBackend — Playback
    // ──────────────────────────────────────────────────────────────────────────

    public Task PlayAsync(string url, string title, PlayOptions? opts = null)
    {
        if (_mediaPlayer == null || _libVlc == null)
        {
            ErrorOccurred?.Invoke("LibVLC no pudo inicializarse. Cambia el motor en Ajustes.");
            return Task.CompletedTask;
        }

        opts ??= new PlayOptions();

        // Detener reproducción anterior
        _progressTimer?.Dispose();
        _progressTimer = null;
        _mediaPlayer.Stop();

        _currentUrl   = url;
        _currentTitle = title;

        try
        {
            Uri mediaUri = Uri.TryCreate(url, UriKind.Absolute, out var parsedUri)
                ? parsedUri
                : new Uri(url);

            var media = new Media(_libVlc, mediaUri);

            var ua = opts.UserAgent ?? ConfigManager.Current.RandomUserAgent;
            media.AddOption($":http-user-agent={ua}");

            if (!string.IsNullOrEmpty(opts.Referer))
            {
                media.AddOption($":http-referrer={opts.Referer}");
            }

            // Optimizaciones de red / caché
            media.AddOption(":network-caching=5000");
            media.AddOption(":live-caching=5000");
            media.AddOption(":file-caching=5000");
            media.AddOption(":clock-jitter=0");
            media.AddOption(":clock-synchro=0");

            if (opts.StartPosition > 0)
            {
                long startMs = (long)(opts.StartPosition * 1000);
                _mediaPlayer.Playing += (_, _) =>
                {
                    if (_mediaPlayer.Length > 0 && startMs > 0)
                        _mediaPlayer.Time = startMs;
                };
            }

            _mediaPlayer.Buffering += OnBuffering;

            _mediaPlayer.Play(media);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke($"Error al iniciar reproductor: {ex.Message}");
        }

        // Timer de progreso — cada 1 segundo
        _progressTimer = new System.Threading.Timer(
            OnProgressTick, null,
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        _mediaPlayer?.SetPause(true);
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        _mediaPlayer?.SetPause(false);
        return Task.CompletedTask;
    }

    public Task SeekAsync(double seconds)
    {
        if (_mediaPlayer != null)
            _mediaPlayer.Time = (long)(seconds * 1000);
        return Task.CompletedTask;
    }

    public void Stop()
    {
        _progressTimer?.Dispose();
        _progressTimer = null;
        _mediaPlayer?.Stop();
        _currentSession = null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Control de volumen y velocidad (extra para el reproductor embebido)
    // ──────────────────────────────────────────────────────────────────────────

    public int Volume
    {
        get => _mediaPlayer?.Volume ?? 100;
        set { if (_mediaPlayer != null) _mediaPlayer.Volume = Math.Clamp(value, 0, 200); }
    }

    public float Rate
    {
        get => _mediaPlayer?.Rate ?? 1f;
        set { if (_mediaPlayer != null) _mediaPlayer.SetRate(value); }
    }

    public bool IsMuted
    {
        get => _mediaPlayer?.Mute ?? false;
        set { if (_mediaPlayer != null) _mediaPlayer.Mute = value; }
    }

    public double Duration => (_mediaPlayer?.Length ?? 0) / 1000.0;
    public double Position => (_mediaPlayer?.Time ?? 0) / 1000.0;

    private double _bufferPercentage = 0.0;
    private uint _videoWidth = 0;
    private uint _videoHeight = 0;
    // Tracks whether video has actually started playing (used to distinguish
    // start-up errors from mid-playback errors when deciding to auto-recover)
    private bool _hasStartedPlaying = false;
    private double _lastKnownPosition = 0.0;

    private void OnProgressTick(object? _)
    {
        if (_mediaPlayer == null) return;

        var state = _mediaPlayer.State;
        if (state == VLCState.Stopped || state == VLCState.NothingSpecial) return;

        double pos = Math.Max(0, _mediaPlayer.Time  / 1000.0);
        double dur = Math.Max(0, _mediaPlayer.Length / 1000.0);
        bool isCompleted = dur > 0 && ((pos / dur >= 0.88) || (dur - pos <= 90));

        // Track last known position for auto-recover
        if (pos > 0) _lastKnownPosition = pos;

        var playerState = state switch
        {
            VLCState.Playing   => PlayerState.Playing,
            VLCState.Paused    => PlayerState.Paused,
            VLCState.Buffering => PlayerState.Buffering,
            VLCState.Ended     => PlayerState.Ended,
            VLCState.Error     => PlayerState.Error,
            _                  => PlayerState.Idle,
        };

        UpdateMediaInfo();

        var session = new PlaySession(_currentUrl, _currentTitle, pos, dur, playerState, isCompleted, _bufferPercentage, _videoWidth, _videoHeight);
        _currentSession = session;
        SessionChanged?.Invoke(session);

        if (pos > 0)
        {
            try
            {
                var history = new AniCS.History.WatchHistory();
                history.UpdateProgress(_currentUrl, pos, dur, isCompleted);
            }
            catch { }
        }
    }

    private void UpdateMediaInfo()
    {
        if (_mediaPlayer == null) return;

        uint w = 0, h = 0;
        try
        {
            _mediaPlayer.Size(0, ref w, ref h);
            if (w > 0 && h > 0)
            {
                _videoWidth = w;
                _videoHeight = h;
                return;
            }
        }
        catch { }

        if (_mediaPlayer.Media != null)
        {
            foreach (var track in _mediaPlayer.Media.Tracks)
            {
                if (track.TrackType == TrackType.Video)
                {
                    _videoWidth = track.Data.Video.Width;
                    _videoHeight = track.Data.Video.Height;
                    break;
                }
            }
        }
    }

    private void OnBuffering(object? sender, MediaPlayerBufferingEventArgs e)
    {
        _bufferPercentage = e.Cache / 100.0;
    }

    private void OnPlaying(object? sender, EventArgs e)
    {
        _hasStartedPlaying = true;
        EmitState(PlayerState.Playing);
    }

    private void OnPaused(object? sender, EventArgs e)
        => EmitState(PlayerState.Paused);

    private void OnEndReached(object? sender, EventArgs e)
    {
        _progressTimer?.Dispose();
        _progressTimer = null;

        double dur = (_mediaPlayer?.Length ?? 0) / 1000.0;
        var session = new PlaySession(_currentUrl, _currentTitle, dur, dur, PlayerState.Ended, true, 1.0, _videoWidth, _videoHeight);
        _currentSession = session;
        SessionChanged?.Invoke(session);

        try
        {
            var history = new AniCS.History.WatchHistory();
            history.UpdateProgress(_currentUrl, dur, dur, true);
        }
        catch { }
    }

    private void OnError(object? sender, EventArgs e)
    {
        _progressTimer?.Dispose();

        // If we were already playing, prefer auto-recover over hard error.
        if (_hasStartedPlaying && _lastKnownPosition > 0)
        {
            _hasStartedPlaying = false;
            RecoverRequested?.Invoke(_lastKnownPosition);
            return;
        }

        ErrorOccurred?.Invoke("Error al reproducir. La URL podría no estar disponible o el servidor rechazó la conexión.");
    }

    private void EmitState(PlayerState state)
    {
        if (_mediaPlayer == null) return;
        double pos = Math.Max(0, _mediaPlayer.Time   / 1000.0);
        double dur = Math.Max(0, _mediaPlayer.Length / 1000.0);
        bool isCompleted = dur > 0 && ((pos / dur >= 0.88) || (dur - pos <= 90));
        
        UpdateMediaInfo();
        
        var session = new PlaySession(_currentUrl, _currentTitle, pos, dur, state, isCompleted, _bufferPercentage, _videoWidth, _videoHeight);
        _currentSession = session;
        SessionChanged?.Invoke(session);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IDisposable
    // ──────────────────────────────────────────────────────────────────────────

    public void Dispose()
    {
        _progressTimer?.Dispose();
        try { _mediaPlayer?.Stop(); } catch { }
        _mediaPlayer?.Dispose();
        _libVlc?.Dispose();
        _mediaPlayer = null;
        _libVlc      = null;
    }
}
