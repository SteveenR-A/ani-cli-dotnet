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
using AniCS.Player;

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
    private IAudioMixerController? _mixer;      // Controlador de volumen del sistema (Windows)
    private bool _isDraggingSlider;
    private bool _isDraggingVolume;              // Verdadero mientras el usuario arrastra el slider de volumen
    private bool _isPlaying;
    private bool _isMuted;
    private bool _isFullscreen;
    private WindowState _previousWindowState = WindowState.Normal; // Estado antes de entrar a fullscreen
    private int  _lastVolume = 100;

    // Seguimiento de posición del mouse para filtrar micro-movimientos de daemons
    private Avalonia.Point _lastMousePos;  // última posición conocida del puntero
    private Avalonia.Point _hideMousePos;  // posición cuando los controles se ocultaron
    /// <summary>
    /// Distancia mínima (píxeles) que debe moverse el puntero para revelar los controles.
    /// Filtra micro-movimientos de 1-3 px del daemon OLED de Lenovo Vantage (modo cuidado de pantalla).
    /// 15 px es imperceptible para el usuario pero muy superior al jitter del daemon.
    /// </summary>
    private const double PointerRevealThreshold = 15.0;

    // ── Timers ────────────────────────────────────────────────────────────────
    private DispatcherTimer? _osdTimer;          // Oculta el OSD tras 1.5s
    private DispatcherTimer? _controlsTimer;     // Oculta los controles principales
    private DispatcherTimer? _volumeSaveTimer;   // Debounce: guarda config en disco 400ms después del último cambio
    private DispatcherTimer? _deactivateTimer;   // Debounce: cierra el popup 400ms después de perder el foco

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

        // Debounce: escribe la config de volumen en disco solo cuando el usuario
        // deja de mover el slider durante 400 ms (evita bloquear el hilo UI).
        _volumeSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _volumeSaveTimer.Tick += (_, _) =>
        {
            _volumeSaveTimer.Stop();
            _isDraggingVolume = false;
            int vol = (int)VolumeSlider.Value;
            try
            {
                ConfigManager.Current.Volume = vol;
                ConfigManager.Save(ConfigManager.Current);
            }
            catch { }
        };

        // Debounce para cierre del Popup: evita que el daemon de Lenovo Vantage
        // (u otros procesos que ciclan el foco) cause parpadeo del overlay.
        _deactivateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _deactivateTimer.Tick += (_, _) =>
        {
            _deactivateTimer.Stop();
            // Solo cerrar si la ventana sigue sin foco
            if (TopLevel.GetTopLevel(this) is Window win && !win.IsActive)
            {
                if (OverlayPopup != null) OverlayPopup.IsOpen = false;
                ControlsOverlay.IsVisible = false;
                _controlsTimer?.Stop();
            }
        };
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

        if (_mixer != null)
        {
            // Modo mixer: LibVLC queda al 100% (no dobla la escala);
            // el volumen real lo controla WindowsAudioSessionController.
            _backend.Volume    = 100;
            int sysVol         = _mixer.Volume;
            VolumeSlider.Value = sysVol;
            _lastVolume        = sysVol;
            if (VolumeLabel != null) VolumeLabel.Text = $"{sysVol}%";
        }
        else
        {
            // Fallback: usar el volumen por software de LibVLC (plataformas no-Windows)
            int startVol = SyncSystemVolume();
            _backend.Volume    = startVol;
            VolumeSlider.Value = startVol;
            _lastVolume        = startVol;
            if (VolumeLabel != null) VolumeLabel.Text = $"{startVol}%";
        }

        // Mostrar controles brevemente al arrancar para indicar al usuario que están disponibles
        ControlsOverlay.IsVisible = true;
        _controlsTimer?.Stop();
        _controlsTimer?.Start();
    }

    /// <summary>
    /// Devuelve el volumen inicial que debe aplicarse al arrancar el reproductor.
    /// LibVLC no expone el mezclador de audio del sistema operativo — su propiedad
    /// <c>MediaPlayer.Volume</c> devuelve siempre 100 hasta que se le asigna un Media
    /// y empieza a reproducir, por lo que leerla aquí no aporta información real.
    /// La única fuente de verdad persistente es <see cref="ConfigManager.Current.Volume"/>,
    /// que se actualiza cada vez que el usuario mueve el slider (con debounce).
    /// </summary>
    private static int SyncSystemVolume()
    {
        int vol = ConfigManager.Current.Volume;
        return Math.Clamp(vol is < 0 or > 200 ? 100 : vol, 0, 200);
    }

    public void Detach()
    {
        if (_backend == null) return;
        _backend.SessionChanged -= OnSessionChanged;
        _backend.ErrorOccurred  -= OnPlayerError;
        VideoViewControl.MediaPlayer = null;
        _backend = null;
    }

    /// <summary>
    /// Inyecta el controlador de audio del sistema operativo.
    /// Debe llamarse antes de <see cref="Attach"/> o justo después.
    /// Pasar null deshabilita la integración (comportamiento LibVLC puro).
    /// </summary>
    public void SetMixer(IAudioMixerController? mixer)
    {
        if (_mixer != null)
            _mixer.ExternalVolumeChanged -= OnExternalVolumeChanged;

        _mixer = mixer;

        if (_mixer != null)
            _mixer.ExternalVolumeChanged += OnExternalVolumeChanged;
    }

    /// <summary>
    /// Llamado cuando el mezclador de Windows cambia el volumen por fuera de la app
    /// (rueda del ratón en el tray de sonido, Lenovo Vantage, etc.).
    /// Solo actualiza la barra si el usuario no está arrastrando el slider.
    /// </summary>
    private void OnExternalVolumeChanged(int newVolume)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_isDraggingVolume) return;
            VolumeSlider.Value = newVolume;
            if (VolumeLabel != null) VolumeLabel.Text = $"{newVolume}%";
            if (newVolume > 0) _lastVolume = newVolume;
            if (!_isMuted)
                MuteIcon.Kind = newVolume == 0
                    ? MaterialIconKind.VolumeOff
                    : (newVolume < 50 ? MaterialIconKind.VolumeMedium : MaterialIconKind.VolumeHigh);
        });
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

            // Intentar enlazar la sesión de audio si aún no lo hemos hecho
            // (la sesión de Core Audio aparece solo después de que LibVLC emite audio)
            if (session.State == PlayerState.Playing)
                _mixer?.TryAcquireSession();
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
        var pos = e.GetPosition(this);
        _lastMousePos = pos;

        if (!ControlsOverlay.IsVisible)
        {
            // Controles ocultos: solo revelarlos si el puntero se movió lo suficiente
            // desde el punto donde se ocultaron. Esto filtra el jitter del daemon OLED
            // de Lenovo Vantage (1-3 px) sin afectar al usuario (movimiento real ≥15 px).
            var dx = pos.X - _hideMousePos.X;
            var dy = pos.Y - _hideMousePos.Y;
            if (Math.Sqrt(dx * dx + dy * dy) < PointerRevealThreshold)
                return;
        }

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
            // Guardar la posición del puntero al ocultar — se usa como referencia
            // para el umbral de revelado en OnRootPointerMoved.
            _hideMousePos = _lastMousePos;
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
        // Cancelar cierre pendiente del popup (daemon de Vantage u otro proceso
        // dispara Deactivated/Activated rápidamente — el debounce lo filtra).
        _deactivateTimer?.Stop();

        // Reabrir el popup para que el overlay sea interactuable
        if (OverlayPopup != null)
            OverlayPopup.IsOpen = true;

        // NO mostrar ControlsOverlay aquí: solo se muestra con movimiento de mouse
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        // Iniciar el debounce: si Activated llega en <400ms (ciclo del daemon)
        // se cancela y el popup no se cierra. Si el usuario cambió de ventana de
        // verdad, el timer expira y cierra el popup.
        _deactivateTimer?.Stop();
        _deactivateTimer?.Start();
    }

    private async void OnPlayPauseClicked(object? sender, RoutedEventArgs e)
        => await TogglePlayPause();

    private async System.Threading.Tasks.Task TogglePlayPause()
    {
        if (_backend == null) return;
        if (_isPlaying)
        {
            await _backend.PauseAsync();
            ShowOsd("Pausado");
        }
        else
        {
            await _backend.ResumeAsync();
            ShowOsd("Reproduciendo");
        }
    }

    private async void OnRewindClicked(object? sender, RoutedEventArgs e)
    {
        if (_backend == null) return;
        double target = Math.Max(0, _backend.Position - 10);
        await _backend.SeekAsync(target);
        ShowOsd("-10s");
    }

    private async void OnForwardClicked(object? sender, RoutedEventArgs e)
    {
        if (_backend == null) return;
        double dur    = _backend.Duration;
        double target = dur > 0 ? Math.Min(dur - 1, _backend.Position + 10) : _backend.Position + 10;
        await _backend.SeekAsync(target);
        ShowOsd("+10s");
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

        if (_mixer != null)
            _mixer.IsMuted = _isMuted;
        else
            _backend.IsMuted = _isMuted;

        MuteIcon.Kind = _isMuted ? MaterialIconKind.VolumeOff : MaterialIconKind.VolumeHigh;
        ShowOsd(_isMuted ? "Silenciado" : "Con sonido");
    }

    private void OnVolumeChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        int pct = (int)e.NewValue;
        _isDraggingVolume = true;

        // Aplicar el volumen al mixer del sistema (si está disponible) o al backend
        if (_mixer != null)
            _mixer.Volume = pct;
        else if (_backend != null)
            _backend.Volume = pct;

        // Actualizar UI
        if (VolumeLabel != null)
            VolumeLabel.Text = $"{pct}%";

        if (pct > 0) _lastVolume = pct;
        if (!_isMuted)
            MuteIcon.Kind = pct == 0 ? MaterialIconKind.VolumeOff : (pct < 50 ? MaterialIconKind.VolumeMedium : MaterialIconKind.VolumeHigh);

        // Debounce: reiniciar el timer — el Save al disco ocurre 400ms después
        // del último movimiento del slider, no en cada tick.
        _volumeSaveTimer?.Stop();
        _volumeSaveTimer?.Start();
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
        ShowOsd(FormatTime(target));
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
