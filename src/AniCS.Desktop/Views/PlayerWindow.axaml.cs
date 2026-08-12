using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AniCS.Player;
using System;
using System.Threading.Tasks;

namespace AniCS.Desktop.Views;

public partial class PlayerWindow : Window
{
    private IPlayerBackend?   _playerBackend;
    // Resolver function: called each time we need a fresh URL (initial play + auto-recover)
    private Func<Task<string>>? _urlResolver;
    private string _title     = "";
    private string _serverUrl = "";
    private string _quality   = "";

    // Auto-recover state
    private bool   _isRecovering   = false;
    private int    _recoverAttempts = 0;
    private const int MaxRecoverAttempts = 3;

    public PlayerWindow()
    {
        InitializeComponent();
    }

    /// <param name="urlResolver">
    /// Async function that returns a fresh direct video URL.
    /// Called once at startup and again on each auto-recover attempt.
    /// </param>
    public PlayerWindow(
        IPlayerBackend     playerBackend,
        Func<Task<string>> urlResolver,
        string             title,
        string             serverUrl,
        string             quality)
    {
        InitializeComponent();

        _playerBackend = playerBackend;
        _urlResolver   = urlResolver;
        _title         = title;
        _serverUrl     = serverUrl;
        _quality       = quality;

        Title = title;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_playerBackend is LibVlcBackend libVlcBackend)
        {
            EmbeddedVideoPlayer.Attach(libVlcBackend);
            // Subscribe to auto-recover hook
            libVlcBackend.RecoverRequested += OnRecoverRequested;
        }

        EmbeddedVideoPlayer.BackRequested  += OnCloseRequested;
        EmbeddedVideoPlayer.CloseRequested += OnCloseRequested;
        EmbeddedVideoPlayer.Focus();

        await StartPlaybackAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        EmbeddedVideoPlayer.BackRequested  -= OnCloseRequested;
        EmbeddedVideoPlayer.CloseRequested -= OnCloseRequested;
        EmbeddedVideoPlayer.Detach();

        if (_playerBackend is LibVlcBackend libVlcBackend)
            libVlcBackend.RecoverRequested -= OnRecoverRequested;

        _playerBackend?.Stop();
    }

    // ── Playback ──────────────────────────────────────────────────────────────

    private async Task StartPlaybackAsync(double resumePosition = 0)
    {
        if (_playerBackend == null || _urlResolver == null) return;

        string? url = null;
        try
        {
            url = await _urlResolver();
        }
        catch (Exception ex)
        {
            EmbeddedVideoPlayer.HideLoading();
            // Show error inline using the player's own error overlay
            // (triggers via ErrorOccurred event from the backend - nothing to do here)
            System.Diagnostics.Debug.WriteLine($"[PlayerWindow] URL resolver failed: {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(url))
        {
            System.Diagnostics.Debug.WriteLine("[PlayerWindow] URL resolver returned empty string.");
            return;
        }

        EmbeddedVideoPlayer.SetTitle(_title);
        EmbeddedVideoPlayer.ShowLoading(resumePosition > 0 ? "Reconectando..." : "Conectando al servidor...");

        await _playerBackend.PlayAsync(url, _title, new PlayOptions
        {
            Referer       = _serverUrl,
            Quality       = _quality,
            StartPosition = resumePosition
        });
    }

    // ── Auto-Recover ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called by LibVlcBackend when a mid-playback error is detected.
    /// Tries to fetch a fresh URL and resume from <paramref name="lastPosition"/>.
    /// </summary>
    private void OnRecoverRequested(double lastPosition)
    {
        if (_isRecovering) return;

        Dispatcher.UIThread.Post(async () =>
        {
            if (_isRecovering) return;
            _isRecovering = true;
            _recoverAttempts++;

            if (_recoverAttempts > MaxRecoverAttempts)
            {
                // Too many consecutive failures — surface the error to the user
                _isRecovering = false;
                EmbeddedVideoPlayer.ShowLoading(
                    $"No se pudo reconectar después de {MaxRecoverAttempts} intentos.");
                await Task.Delay(3000);
                EmbeddedVideoPlayer.HideLoading();
                return;
            }

            EmbeddedVideoPlayer.ShowLoading(
                $"Reconectando... (intento {_recoverAttempts}/{MaxRecoverAttempts})");

            // Brief pause before retrying to avoid hammering the server
            await Task.Delay(TimeSpan.FromSeconds(2));

            await StartPlaybackAsync(lastPosition);
            _isRecovering = false;
        });
    }

    // Reset the consecutive-failure counter whenever the user manually triggers
    // a new session (e.g. goes back and re-opens the episode)
    private void OnCloseRequested(object? sender, EventArgs e)
    {
        _recoverAttempts = 0;
        Close();
    }
}
