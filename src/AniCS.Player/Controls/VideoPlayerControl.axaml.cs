using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LibVLCSharp.Shared;
using Material.Icons;
using System;
using System.Collections.Generic;

namespace AniCS.Player.Controls;

/// <summary>
/// Control de video embebido LibVLC con barra superior y barra inferior permanentes.
/// Controles: Play/Pause, Seek ±10s, barra de progreso, Volumen 0-200%,
/// Silenciar, Velocidad 0.5x-2x, Fullscreen, Buffer indicator, Botón Volver.
/// </summary>
public partial class VideoPlayerControl : UserControl
{
    // ── Estado ────────────────────────────────────────────────────────────────
    private LibVlcBackend? _backend;
    private bool _isDraggingSlider;
    private bool _isPlaying;
    private bool _isMuted;
    private bool _isFullscreen;
    private WindowState _previousWindowState = WindowState.Normal; // Estado antes de entrar a fullscreen
    private int  _lastVolume = 100;

    // ── Timers ────────────────────────────────────────────────────────────────
    private DispatcherTimer? _osdTimer;   // Oculta el OSD tras 1.5s
    private DispatcherTimer? _controlsTimer; // Oculta los controles principales

    // ── Velocidades disponibles ───────────────────────────────────────────────
    private static readonly List<(string Label, float Rate)> Speeds = new()
    {
        ("0.5×",  0.5f),
        ("0.75×", 0.75f),
        ("1×",    1.0f),
        ("1.25×", 1.25f),
        ("1.5×",  1.5f),
        ("2×",    2.0f),
    };

    // ── Evento para que el host (VideoPlayerView) pueda navegar atrás ─────────
    public event EventHandler? BackRequested;
    // Compatibilidad con código anterior que usa CloseRequested
    public event EventHandler? CloseRequested;

    // ──────────────────────────────────────────────────────────────────────────
    // Constructor
    // ──────────────────────────────────────────────────────────────────────────

    public VideoPlayerControl()
    {
        InitializeComponent();
        InitTimers();
        InitSpeedCombo();
        Focusable = true;

        ProgressSlider.AddHandler(InputElement.PointerPressedEvent, OnSliderPressed, RoutingStrategies.Tunnel);
        ProgressSlider.AddHandler(InputElement.PointerReleasedEvent, OnSliderReleased, RoutingStrategies.Tunnel);
    }

    private void InitTimers()
    {
        _osdTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _osdTimer.Tick += (_, _) => HideOsd();

        _controlsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _controlsTimer.Tick += (_, _) => HideControls();
    }

    private void InitSpeedCombo()
    {
        SpeedCombo.ItemsSource   = Speeds.ConvertAll(s => s.Label);
        SpeedCombo.SelectedIndex = 2; // 1×
    }

    // ──────────────────────────────────────────────────────────────────────────
    // API pública
    // ──────────────────────────────────────────────────────────────────────────

    public void Attach(LibVlcBackend backend)
    {
        Detach();
        _backend = backend;
        _backend.SessionChanged += OnSessionChanged;
        _backend.ErrorOccurred  += OnPlayerError;
        VideoViewControl.MediaPlayer = backend.MediaPlayer;
        
        // Sincronizar volumen inicial con la configuración global
        int globalVol = ConfigManager.Current.Volume;
        if (globalVol < 0 || globalVol > 200) globalVol = 100;

        _backend.Volume    = globalVol;
        VolumeSlider.Value = globalVol;
        _lastVolume        = globalVol;
        if (VolumeLabel != null) VolumeLabel.Text = $"{globalVol}%";
    }

    public void Detach()
    {
        if (_backend == null) return;
        _backend.SessionChanged -= OnSessionChanged;
        _backend.ErrorOccurred  -= OnPlayerError;
        VideoViewControl.MediaPlayer = null;
        _backend = null;
    }

    public void ShowLoading(string? subtitle = null)
    {
        LoadingOverlay.IsVisible = true;
        ErrorOverlay.IsVisible   = false;
        if (subtitle != null) LoadingSubtext.Text = subtitle;
    }

    public void HideLoading()
    {
        LoadingOverlay.IsVisible = false;
    }

    public void SetTitle(string title)
    {
        TitleLabel.Text = title;
    }

    public void SetStreamInfo(string info)
    {
        QualityBadge.Text = info;
        InfoLabel.Text    = $"LibVLC · {info}";
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Actualización de UI desde eventos del backend
    // ──────────────────────────────────────────────────────────────────────────

    private void OnSessionChanged(PlaySession session)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Ocultar loading cuando empieza a reproducir o pausa
            if (session.State == PlayerState.Playing || session.State == PlayerState.Paused)
                LoadingOverlay.IsVisible = false;

            if (session.State == PlayerState.Buffering)
            {
                LoadingOverlay.IsVisible = true;
                LoadingSubtext.Text      = "Buffering...";
                ErrorOverlay.IsVisible   = false;
            }

            if (session.State == PlayerState.Error)
            {
                ErrorOverlay.IsVisible   = true;
                LoadingOverlay.IsVisible = false;
            }

            // Icono play/pause
            _isPlaying = session.State == PlayerState.Playing;
            PlayPauseIcon.Kind = _isPlaying ? MaterialIconKind.Pause : MaterialIconKind.Play;

            // Barra de progreso
            if (!_isDraggingSlider && session.Duration > 0)
                ProgressSlider.Value = session.Position / session.Duration;

            // Etiquetas de tiempo
            TimeLabel.Text     = FormatTime(session.Position);
            DurationLabel.Text = FormatTime(session.Duration);

            // Actualizar barra visual del buffer (BufferProgressFill)
            if (BufferProgressBg.Bounds.Width > 0)
            {
                double cachePercent = Math.Clamp(session.BufferPercentage, 0, 1.0);
                BufferProgressFill.Width = BufferProgressBg.Bounds.Width * cachePercent;
            }

            // Indicador de buffer texto (calculando los segundos basados en 5000ms = 5s)
            double secondsBuffered = session.BufferPercentage * 5.0;
            NetworkCacheLabel.Text = $"{secondsBuffered:0.0}s descargado";

            // Mostrar la resolución detectada dinámicamente si está disponible
            if (session.VideoWidth > 0 && session.VideoHeight > 0)
            {
                QualityBadge.Text = $"{session.VideoWidth}x{session.VideoHeight}";
            }
        });
    }

    private void OnPlayerError(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LoadingOverlay.IsVisible = false;
            ErrorOverlay.IsVisible   = true;
            ErrorText.Text           = message;
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Interacción con el área de video (click para play/pause y auto-hide)
    // ──────────────────────────────────────────────────────────────────────────

    private void OnRootPointerMoved(object? sender, PointerEventArgs e)
    {
        // Mantener cursor visible mientras el ratón se mueve y mostrar controles
        Cursor = Cursor.Default;
        ControlsOverlay.IsVisible = true;
        
        _controlsTimer?.Stop();
        if (_isPlaying) _controlsTimer?.Start();
    }

    private void HideControls()
    {
        _controlsTimer?.Stop();
        if (_isPlaying)
        {
            Cursor = new Cursor(StandardCursorType.None);
            ControlsOverlay.IsVisible = false;
        }
    }

    private void OnVideoAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = TogglePlayPause();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // OSD (On-Screen Display)
    // ──────────────────────────────────────────────────────────────────────────

    private void ShowOsd(string message)
    {
        OsdText.Text       = message;
        OsdPanel.IsVisible = true;
        _osdTimer?.Stop();
        _osdTimer?.Start();
    }

    private void HideOsd() => OsdPanel.IsVisible = false;

    // ──────────────────────────────────────────────────────────────────────────
    // Botones de control
    // ──────────────────────────────────────────────────────────────────────────

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window win)
        {
            win.Activated += OnWindowActivated;
            win.Deactivated += OnWindowDeactivated;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window win)
        {
            win.Activated -= OnWindowActivated;
            win.Deactivated -= OnWindowDeactivated;
        }
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (OverlayPopup != null)
            OverlayPopup.IsOpen = true;
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (OverlayPopup != null)
            OverlayPopup.IsOpen = false;
    }

    private async void OnPlayPauseClicked(object? sender, RoutedEventArgs e)
        => await TogglePlayPause();

    private async System.Threading.Tasks.Task TogglePlayPause()
    {
        if (_backend == null) return;
        if (_isPlaying)
        {
            await _backend.PauseAsync();
            ShowOsd("⏸ Pausado");
        }
        else
        {
            await _backend.ResumeAsync();
            ShowOsd("▶ Reproduciendo");
        }
    }

    private async void OnRewindClicked(object? sender, RoutedEventArgs e)
    {
        if (_backend == null) return;
        double target = Math.Max(0, _backend.Position - 10);
        await _backend.SeekAsync(target);
        ShowOsd("⏪ −10s");
    }

    private async void OnForwardClicked(object? sender, RoutedEventArgs e)
    {
        if (_backend == null) return;
        double dur    = _backend.Duration;
        double target = dur > 0 ? Math.Min(dur - 1, _backend.Position + 10) : _backend.Position + 10;
        await _backend.SeekAsync(target);
        ShowOsd("⏩ +10s");
    }

    /// <summary>
    /// Botón "Volver" — restaura el estado de la ventana (sale de fullscreen si aplica)
    /// y luego dispara BackRequested para que el host navegue atrás.
    /// </summary>
    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        // Si estamos en fullscreen, restorear primero antes de navegar
        if (_isFullscreen)
        {
            _isFullscreen = false;
            if (TopLevel.GetTopLevel(this) is Window win)
                win.WindowState = _previousWindowState;
            FullscreenIcon.Kind = MaterialIconKind.Fullscreen;
        }

        BackRequested?.Invoke(this, EventArgs.Empty);
        CloseRequested?.Invoke(this, EventArgs.Empty); // compatibilidad
    }

    private void OnStopClicked(object? sender, RoutedEventArgs e)
    {
        _backend?.Stop();
        ProgressSlider.Value   = 0;
        TimeLabel.Text         = "0:00";
        PlayPauseIcon.Kind     = MaterialIconKind.Play;
        ShowLoading("Detenido");
    }

    private void OnMuteClicked(object? sender, RoutedEventArgs e)
    {
        if (_backend == null) return;
        _isMuted = !_isMuted;
        if (_isMuted)
        {
            _lastVolume        = (int)VolumeSlider.Value;
            VolumeSlider.Value = 0;
        }
        else
        {
            VolumeSlider.Value = _lastVolume > 0 ? _lastVolume : 100;
        }
        _backend.IsMuted  = _isMuted;
        MuteIcon.Kind     = _isMuted ? MaterialIconKind.VolumeOff : MaterialIconKind.VolumeHigh;
        ShowOsd(_isMuted ? "🔇 Silenciado" : "🔊 Con sonido");
    }

    private void OnVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        int pct = (int)e.NewValue;
        if (_backend != null)
            _backend.Volume = pct;

        // Guardar volumen global sincronizado
        try
        {
            ConfigManager.Current.Volume = pct;
            ConfigManager.Save(ConfigManager.Current);
        }
        catch { }

        if (VolumeLabel != null)
            VolumeLabel.Text = $"{pct}%";

        if (pct > 0) _lastVolume = pct;
        if (!_isMuted)
            MuteIcon.Kind = pct == 0 ? MaterialIconKind.VolumeOff : (pct < 50 ? MaterialIconKind.VolumeMedium : MaterialIconKind.VolumeHigh);
    }

    private void OnSpeedChanged(object? sender, SelectionChangedEventArgs e)
    {
        int idx = SpeedCombo.SelectedIndex;
        if (idx < 0 || idx >= Speeds.Count) return;
        var (label, rate) = Speeds[idx];
        if (_backend != null) _backend.Rate = rate;
        ShowOsd($" {label}");
    }

    private void OnFullscreenClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
        {
            if (!_isFullscreen)
            {
                // Guardar el estado actual ANTES de entrar a fullscreen
                _previousWindowState = window.WindowState;
                window.WindowState   = WindowState.FullScreen;
                _isFullscreen        = true;
                FullscreenIcon.Kind  = MaterialIconKind.FullscreenExit;
            }
            else
            {
                // Restaurar el estado previo al salir de fullscreen
                window.WindowState  = _previousWindowState;
                _isFullscreen       = false;
                FullscreenIcon.Kind = MaterialIconKind.Fullscreen;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Seek mediante slider
    // ──────────────────────────────────────────────────────────────────────────

    private void OnSliderPressed(object? sender, PointerPressedEventArgs e)
        => _isDraggingSlider = true;

    private async void OnSliderReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDraggingSlider = false;
        if (_backend == null) return;

        double dur    = _backend.Duration;
        double target = ProgressSlider.Value * (dur > 0 ? dur : 1);
        await _backend.SeekAsync(target);
        ShowOsd($"⏩ {FormatTime(target)}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Teclado — el control DEBE tener Focusable=true y recibir foco
    // ──────────────────────────────────────────────────────────────────────────

    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.Space: await TogglePlayPause(); e.Handled = true; break;
            case Key.Left:  OnRewindClicked(null, null!); e.Handled = true; break;
            case Key.Right: OnForwardClicked(null, null!); e.Handled = true; break;
            case Key.M:     OnMuteClicked(null, null!); e.Handled = true; break;
            case Key.F:     OnFullscreenClicked(null, null!); e.Handled = true; break;
            case Key.Escape:
                // Esc siempre llama OnBackClicked:
                // - Si hay fullscreen activo, lo restaura primero y luego navega atrás.
                // - Si no, directamente navega atrás.
                OnBackClicked(null, null!);
                e.Handled = true;
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0) return "0:00";
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }
}
