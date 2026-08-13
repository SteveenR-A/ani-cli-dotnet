using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AniCS.Android.Controls;
using AniCS.Desktop.Services;
using AniCS.Player;

namespace AniCS.Android.Views;

public partial class MobileVideoPlayerView : UserControl
{
    private readonly IPlayerBackend? _playerBackend;
    private readonly Func<Task<string>>? _urlResolver;
    private readonly string _title;
    private readonly string _serverUrl;
    private readonly string _quality;

    private AndroidVideoPlayerControl? _nativePlayer;
    private DispatcherTimer? _progressTimer;
    private DispatcherTimer? _osdTimer;
    private DispatcherTimer? _toastTimer;

    private bool _isLandscape = true;
    private bool _isSeeking;
    private bool _isRecovering;
    private int _recoverAttempts;
    private const int MaxRecoverAttempts = 3;

    public MobileVideoPlayerView()
    {
        InitializeComponent();
        _title = "";
        _serverUrl = "";
        _quality = "";
    }

    public MobileVideoPlayerView(
        IPlayerBackend playerBackend,
        Func<Task<string>> urlResolver,
        string title,
        string serverUrl,
        string quality)
    {
        InitializeComponent();
        _playerBackend = playerBackend;
        _urlResolver = urlResolver;
        _title = title;
        _serverUrl = serverUrl;
        _quality = quality;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        TitleLabel.Text = _title;
        QualityBadge.Text = !string.IsNullOrEmpty(_quality) ? _quality : "Nativo";

        // Abrir automáticamente en modo Horizontal al reproducir video
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
        _osdTimer.Tick += (s, ev) =>
        {
            _osdTimer.Stop();
            OsdOverlay.IsVisible = false;
        };

        // Timer de notificación Toast central (1.5 segundos)
        _toastTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1500)
        };
        _toastTimer.Tick += (s, ev) =>
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

        // Restablecer orientación global y barra de estado al salir
        MainActivity.Instance?.ResetOrientation();
        MainActivity.Instance?.DisableImmersiveMode();
    }

    public void ClosePlayer()
    {
        _progressTimer?.Stop();
        _osdTimer?.Stop();
        _toastTimer?.Stop();
        _nativePlayer?.Stop();

        MainActivity.Instance?.ResetOrientation();
        MainActivity.Instance?.DisableImmersiveMode();

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

        string? url = null;
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
                _nativePlayer.PlaybackError += OnNativePlaybackError;
                _nativePlayer.PlaybackCompleted += (s, e) => ClosePlayer();

                PlayerHostContainer.Children.Clear();
                PlayerHostContainer.Children.Add(_nativePlayer);
            }

            _nativePlayer.Play(url, _serverUrl);

            if (resumePositionMsec > 0)
            {
                Task.Delay(1000).ContinueWith(_ =>
                {
                    Dispatcher.UIThread.Invoke(() => _nativePlayer?.SeekTo(resumePositionMsec));
                });
            }

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
            ShowToast("Modo Horizontal");
        }
        else
        {
            MainActivity.Instance?.SetOrientationPortrait();
            ShowToast("Modo Vertical");
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
}
