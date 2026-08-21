using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AniCS.Android.Controls;
using AniCS.Desktop.Services;
using DownloadManager = AniCS.Desktop.Services.DownloadManager;

namespace AniCS.Android.Views;

public partial class MobileVideoPlayerView : UserControl
{
    private Func<Task<string>>? _urlResolver;
    private string _title;
    private string _serverUrl;
    private string _quality;

    private string _animeTitle;
    private string _animeUrl;
    private string _thumbnailUrl;
    private string _episodeNumber;
    private string _episodeUrl;

    private AndroidVideoPlayerControl? _nativePlayer;
    private DispatcherTimer? _progressTimer;
    private DispatcherTimer? _osdTimer;
    private DispatcherTimer? _toastTimer;

    private bool _isLandscape = true;
    private bool _isSeeking;
    private bool _isRecovering;
    private int _recoverAttempts;
    private const int MaxRecoverAttempts = 3;
    private bool _hasMarkedCompleted;

    public Func<Task>? PreviousEpisodeAction { get; set; }
    public Func<Task>? NextEpisodeAction { get; set; }

    public bool IsPlaying => _nativePlayer?.IsPlaying ?? false;
    public void Pause() => _nativePlayer?.Pause();
    public void Resume() => _nativePlayer?.Resume();

    public async Task ChangeEpisodeAsync(
        Func<Task<string>> urlResolver,
        string title,
        string serverUrl,
        string quality,
        Func<Task>? prevEpisodeAction = null,
        Func<Task>? nextEpisodeAction = null)
    {
        _urlResolver          = urlResolver;
        _title                = title;
        _serverUrl            = serverUrl;
        _quality              = quality;
        PreviousEpisodeAction = prevEpisodeAction;
        NextEpisodeAction     = nextEpisodeAction;
        _recoverAttempts      = 0;

        TitleLabel.Text   = _title;
        QualityBadge.Text = !string.IsNullOrEmpty(_quality) ? _quality : "Nativo";
        UpdateNavigationButtons();

        await StartPlaybackAsync(0);
    }

    public MobileVideoPlayerView()
    {
        InitializeComponent();
        _title = "";
        _serverUrl = "";
        _quality = "";
        _animeTitle = "";
        _animeUrl = "";
        _thumbnailUrl = "";
        _episodeNumber = "";
        _episodeUrl = "";
    }

    public MobileVideoPlayerView(
        Func<Task<string>> urlResolver,
        string title,
        string serverUrl,
        string quality,
        string animeTitle = "",
        string animeUrl = "",
        string thumbnailUrl = "",
        string episodeNumber = "",
        string episodeUrl = "",
        Func<Task>? prevEpisodeAction = null,
        Func<Task>? nextEpisodeAction = null)
    {
        InitializeComponent();
        _urlResolver = urlResolver;
        _title = title;
        _serverUrl = serverUrl;
        _quality = quality;
        _animeTitle = animeTitle;
        _animeUrl = animeUrl;
        _thumbnailUrl = thumbnailUrl;
        _episodeNumber = episodeNumber;
        _episodeUrl = episodeUrl;
        PreviousEpisodeAction = prevEpisodeAction;
        NextEpisodeAction = nextEpisodeAction;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        TitleLabel.Text = _title;
        QualityBadge.Text = !string.IsNullOrEmpty(_quality) ? _quality : "Nativo";
        UpdateNavigationButtons();

        // Abrir automáticamente en modo Horizontal al reproducir video y pantalla completa inmersiva
        MainActivity.Instance?.SetOrientationLandscape();
        MainActivity.Instance?.EnableImmersiveMode();

        // Timer de progreso (2x por segundo)
        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _progressTimer.Tick += OnProgressTimerTick;

        // Timer de ocultado automático de OSD (4 segundos)
        _osdTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(4)
        };
        _osdTimer.Tick += (_, _) =>
        {
            _osdTimer.Stop();
            OsdOverlay.IsVisible = false;
        };

        // Timer de notificación Toast central (1.5 segundos)
        _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            OsdToastPanel.IsVisible = false;
        };

        await StartPlaybackAsync();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _progressTimer?.Stop();
        _osdTimer?.Stop();
        _toastTimer?.Stop();

        _nativePlayer?.Stop();
        _nativePlayer = null;
        PlayerHostContainer.Children.Clear();

        // Restablecer orientación global, barra de estado y pantalla activa al salir
        MainActivity.Instance?.ResetOrientation();
        MainActivity.Instance?.DisableImmersiveMode();
        MainActivity.Instance?.DisableKeepScreenOn();
    }

    public void ClosePlayer()
    {
        _progressTimer?.Stop();
        _osdTimer?.Stop();
        _toastTimer?.Stop();
        _nativePlayer?.Stop();

        MainActivity.Instance?.ResetOrientation();
        MainActivity.Instance?.DisableImmersiveMode();
        MainActivity.Instance?.DisableKeepScreenOn();

        AndroidMainView.Current?.FinishPlayerClose(this);
    }

    private async Task StartPlaybackAsync(int resumePositionMsec = 0)
    {
        if (_urlResolver == null) return;

        RetryBtn.IsVisible = false;
        StatusText.Text = _recoverAttempts > 0 
            ? $"Reconectando al servidor ({_recoverAttempts}/{MaxRecoverAttempts})..." 
            : "Obteniendo enlace de video...";
        LoadingPanel.IsVisible = true;

        string url;
        try
        {
            url = await _urlResolver();
        }
        catch (Exception ex)
        {
            AppLogger.Error("MobileVideoPlayerView.StartPlaybackAsync", ex);
            HandlePlaybackFailure($"Error al resolver enlace: {ex.Message}");
            return;
        }

        if (string.IsNullOrEmpty(url))
        {
            HandlePlaybackFailure("No se pudo obtener el enlace del video.");
            return;
        }

        StatusText.Text = "Iniciando reproductor...";

        Dispatcher.UIThread.Invoke(() =>
        {
            if (_nativePlayer == null)
            {
                _nativePlayer = new AndroidVideoPlayerControl();
                _nativePlayer.SetInfo(_title, !string.IsNullOrEmpty(_quality) ? _quality : "Nativo");
                _nativePlayer.BackRequested += (_, _) => ClosePlayer();
                _nativePlayer.PreviousEpisodeRequested += (_, _) => OnPrevEpisodeClicked(null, null!);
                _nativePlayer.NextEpisodeRequested += (_, _) => OnNextEpisodeClicked(null, null!);
                _nativePlayer.PlaybackError += OnNativePlaybackError;
                _nativePlayer.PlaybackStateChanged += OnNativePlaybackStateChanged;
                _nativePlayer.ProgressChanged += (_, ev) => UpdatePlaybackWatchHistory(ev.Position, ev.Duration);
                _nativePlayer.PlaybackCompleted += (_, _) =>
                {
                    UpdatePlaybackWatchHistory(1, 1, isCompleted: true);
                    ClosePlayer();
                };

                PlayerHostContainer.Children.Clear();
                PlayerHostContainer.Children.Add(_nativePlayer);
            }
            else
            {
                _nativePlayer.SetInfo(_title, !string.IsNullOrEmpty(_quality) ? _quality : "Nativo");
            }

            _nativePlayer.SetNavigationState(PreviousEpisodeAction != null, NextEpisodeAction != null);

            _nativePlayer.Play(url, _serverUrl, resumePositionMsec);

            _progressTimer?.Start();
            _osdTimer?.Start();

            // Ocultar pantalla de carga
            Task.Delay(1200).ContinueWith(_ =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    LoadingPanel.IsVisible = false;
                    _isRecovering = false;
                });
            });
        });
    }

    private void OnNativePlaybackStateChanged(object? sender, bool isPlaying)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            CenterPlayPauseIcon.Kind = isPlaying 
                ? Material.Icons.MaterialIconKind.Pause 
                : Material.Icons.MaterialIconKind.Play;

            if (isPlaying)
            {
                _progressTimer?.Start();
                _osdTimer?.Start();
            }
            else
            {
                _osdTimer?.Stop();
            }
        });
    }

    private void OnNativePlaybackError(object? sender, string errorMsg)
    {
        Dispatcher.UIThread.Invoke(async () =>
        {
            AppLogger.Error("MobileVideoPlayerView.OnNativePlaybackError", errorMsg);

            if (_isRecovering) return;
            _isRecovering = true;
            _recoverAttempts++;

            if (_recoverAttempts <= MaxRecoverAttempts)
            {
                int currentPos = _nativePlayer?.CurrentPosition ?? 0;
                StatusText.Text = $"Conexión perdida. Reconectando ({_recoverAttempts}/{MaxRecoverAttempts})...";
                LoadingPanel.IsVisible = true;
                _nativePlayer?.Stop();

                await Task.Delay(2000);
                await StartPlaybackAsync(currentPos);
            }
            else
            {
                HandlePlaybackFailure("Error de conexión recurrente. Verifica tu red o prueba con otro servidor.");
            }
        });
    }

    private void HandlePlaybackFailure(string message)
    {
        _isRecovering = false;
        StatusText.Text = message;
        SubtextLabel.Text = "Prueba seleccionando otro servidor de la lista.";
        RetryBtn.IsVisible = true;
        LoadingPanel.IsVisible = true;
    }

    private async void OnRetryClicked(object? sender, RoutedEventArgs e)
    {
        _recoverAttempts = 0;
        await StartPlaybackAsync();
    }

    private void OnProgressTimerTick(object? sender, EventArgs e)
    {
        if (_nativePlayer == null || _isSeeking) return;

        int currentMs = _nativePlayer.CurrentPosition;
        int durationMs = _nativePlayer.Duration;

        if (durationMs > 0)
        {
            ProgressSlider.Maximum = durationMs;
            ProgressSlider.Value = currentMs;

            TimeLabel.Text = FormatTime(currentMs);
            DurationLabel.Text = FormatTime(durationMs);
        }

        // Buffer percentage fill visual
        int bufferPercent = _nativePlayer.BufferPercentage;
        if (BufferProgressBg.Bounds.Width > 0)
        {
            double fillWidth = BufferProgressBg.Bounds.Width * (Math.Clamp(bufferPercent, 0, 100) / 100.0);
            BufferProgressFill.Width = fillWidth;
        }

        double secondsBuffered = (bufferPercent / 100.0) * 5.0;
        NetworkCacheLabel.Text = $"{secondsBuffered:0.0}s descargado";

        CenterPlayPauseIcon.Kind = _nativePlayer.IsPlaying 
            ? Material.Icons.MaterialIconKind.Pause 
            : Material.Icons.MaterialIconKind.Play;
    }

    private string FormatTime(int ms)
    {
        TimeSpan t = TimeSpan.FromMilliseconds(ms);
        return t.Hours > 0 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
    }

    private void ShowToast(string message)
    {
        OsdToastText.Text = message;
        OsdToastPanel.IsVisible = true;
        _toastTimer?.Stop();
        _toastTimer?.Start();
    }

    private void OnVideoAreaTapped(object? sender, PointerPressedEventArgs e)
    {
        OsdOverlay.IsVisible = !OsdOverlay.IsVisible;
        if (OsdOverlay.IsVisible)
        {
            _osdTimer?.Stop();
            _osdTimer?.Start();
        }
    }

    private void OnPlayPauseClicked(object? sender, RoutedEventArgs e)
    {
        if (_nativePlayer == null) return;

        if (_nativePlayer.IsPlaying)
        {
            _nativePlayer.Pause();
            CenterPlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Play;
            ShowToast("Pausa");
        }
        else
        {
            _nativePlayer.Resume();
            CenterPlayPauseIcon.Kind = Material.Icons.MaterialIconKind.Pause;
            ShowToast("Reproduciendo");
        }

        _osdTimer?.Stop();
        _osdTimer?.Start();
    }

    public void UpdateNavigationButtons()
    {
        if (PrevEpisodeBtn != null) PrevEpisodeBtn.IsEnabled = PreviousEpisodeAction != null;
        if (NextEpisodeBtn != null) NextEpisodeBtn.IsEnabled = NextEpisodeAction != null;
        _nativePlayer?.SetNavigationState(PreviousEpisodeAction != null, NextEpisodeAction != null);
    }

    private async void OnPrevEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (PreviousEpisodeAction == null) return;
        StatusText.Text = "Cargando episodio anterior...";
        LoadingPanel.IsVisible = true;
        _osdTimer?.Stop();
        try
        {
            await PreviousEpisodeAction();
        }
        catch (Exception ex)
        {
            HandlePlaybackFailure($"Error: {ex.Message}");
        }
    }

    private async void OnNextEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (NextEpisodeAction == null) return;
        StatusText.Text = "Cargando siguiente episodio...";
        LoadingPanel.IsVisible = true;
        _osdTimer?.Stop();
        try
        {
            await NextEpisodeAction();
        }
        catch (Exception ex)
        {
            HandlePlaybackFailure($"Error: {ex.Message}");
        }
    }

    private void OnRewindClicked(object? sender, RoutedEventArgs e)
    {
        if (_nativePlayer == null) return;
        int newPos = Math.Max(0, _nativePlayer.CurrentPosition - 10000);
        _nativePlayer.SeekTo(newPos);
        ShowToast("-10s");
        _osdTimer?.Stop();
        _osdTimer?.Start();
    }

    private void OnForwardClicked(object? sender, RoutedEventArgs e)
    {
        if (_nativePlayer == null) return;
        int newPos = Math.Min(_nativePlayer.Duration, _nativePlayer.CurrentPosition + 10000);
        _nativePlayer.SeekTo(newPos);
        ShowToast("+10s");
        _osdTimer?.Stop();
        _osdTimer?.Start();
    }

    private void OnRotateClicked(object? sender, RoutedEventArgs e)
    {
        _isLandscape = !_isLandscape;
        if (_isLandscape)
        {
            MainActivity.Instance?.SetOrientationLandscape();
        }
        else
        {
            MainActivity.Instance?.SetOrientationPortrait();
        }
        _osdTimer?.Stop();
        _osdTimer?.Start();
    }

    private void OnSpeedChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (SpeedCombo.SelectedItem is ComboBoxItem item && float.TryParse(item.Tag?.ToString(), out float speed))
        {
            _nativePlayer?.SetSpeed(speed);
            ShowToast($"Velocidad: {speed}x");
        }
    }

    private void OnSliderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _isSeeking = true;
    }

    private void OnSliderPointerReleased(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_nativePlayer != null)
        {
            int targetMs = (int)ProgressSlider.Value;
            _nativePlayer.SeekTo(targetMs);
            ShowToast(FormatTime(targetMs));
        }
        _isSeeking = false;
        _osdTimer?.Stop();
        _osdTimer?.Start();
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        ClosePlayer();
    }

    private void UpdatePlaybackWatchHistory(int currentPosMs, int durationMs, bool isCompleted = false)
    {
        if (string.IsNullOrEmpty(_animeUrl)) return;

        double posSec = currentPosMs / 1000.0;
        double durSec = durationMs / 1000.0;
        double pct = durationMs > 0 ? (double)currentPosMs / durationMs : 0;

        bool completed = isCompleted || pct >= 0.85;

        if (completed && _hasMarkedCompleted) return;
        if (completed) _hasMarkedCompleted = true;

        if (currentPosMs > 1000 || completed)
        {
            var history = new History.WatchHistory();
            history.Record(_animeTitle, _animeUrl, _thumbnailUrl, _episodeNumber, _episodeUrl, posSec, durSec, completed);

            DownloadManager.UpdateEpisodeStatus(
                _animeUrl,
                _episodeNumber,
                completed ? EpisodeWatchStatus.Completed : EpisodeWatchStatus.InProgress);
        }
    }
}
