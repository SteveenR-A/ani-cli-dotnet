using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform;
using Android.Views;
using Android.Media;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Widget;
using Android.OS;
using AniCS;
using AniCS.Android.Views;
using Button = Android.Widget.Button;
using Orientation = Android.Widget.Orientation;
using Canvas = Android.Graphics.Canvas;
using Path = Android.Graphics.Path;

namespace AniCS.Android.Controls;

public class AndroidVideoPlayerControl : NativeControlHost
{
    private FrameLayout? _rootLayout;
    private TextureView? _textureView;
    private FrameLayout? _overlayLayout;
    private MediaPlayer? _mediaPlayer;
    private Surface? _surface;

    // Overlay UI controls (Android Native)
    private LinearLayout? _topBar;
    private TextView? _titleText;
    private TextView? _statusBadge;
    private TextView? _qualityText;
    private LinearLayout? _centerControls;
    private ImageView? _playPauseCenterBtn;
    private ImageView? _rewindBtn;
    private ImageView? _forwardBtn;
    private LinearLayout? _bottomBar;
    private TextView? _timeText;
    private SeekBar? _seekBar;
    private Button? _speedBtn;
    private TextView? _toastText;
    private FrameLayout? _toastLayout;

    private string? _pendingUrl;
    private string? _pendingReferer;
    private string _title = "";
    private string _quality = "720p";

    private int _videoWidth = 0;
    private int _videoHeight = 0;
    private int _bufferPercentage = 0;
    private bool _isPrepared = false;
    private bool _isControlsVisible = true;
    private bool _isUserSeeking = false;
    private float _currentSpeed = 1.0f;

    private readonly Handler _handler = new(Looper.MainLooper!);
    private Action? _autoHideAction;
    private Action? _progressUpdateAction;

    public event EventHandler? PlaybackCompleted;
    public event EventHandler<string>? PlaybackError;
    public event EventHandler? BackRequested;
    public event EventHandler<(int Position, int Duration)>? ProgressChanged;

    public AndroidVideoPlayerControl()
    {
        _autoHideAction = () => HideControls();
        _progressUpdateAction = UpdateProgressLoop;
    }

    public void SetInfo(string title, string quality)
    {
        _title = title;
        _quality = quality;
        if (_titleText != null) _titleText.Text = title;
        if (_qualityText != null) _qualityText.Text = quality;
    }

    public void Play(string url, string? referer = null)
    {
        _pendingUrl = url;
        _pendingReferer = referer;

        if (_surface == null)
        {
            return;
        }

        StartPlaybackInternal(url, referer);
    }

    private void StartPlaybackInternal(string url, string? referer)
    {
        try
        {
            _isPrepared = false;
            _bufferPercentage = 0;

            if (_mediaPlayer == null)
            {
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.SetAudioAttributes(new AudioAttributes.Builder()
                    .SetContentType(AudioContentType.Movie)!
                    .SetUsage(AudioUsageKind.Media)!
                    .Build()!);

                _mediaPlayer.Completion += (s, e) =>
                {
                    PlaybackCompleted?.Invoke(this, EventArgs.Empty);
                    UpdatePlayPauseUi(false);
                };

                _mediaPlayer.Error += (s, e) =>
                {
                    PlaybackError?.Invoke(this, $"Error de reproducción ({e.What})");
                    e.Handled = true;
                };

                _mediaPlayer.BufferingUpdate += (s, e) =>
                {
                    _bufferPercentage = e.Percent;
                    if (_seekBar != null && !_isUserSeeking)
                    {
                        _seekBar.SecondaryProgress = (int)(_bufferPercentage * (_seekBar.Max / 100.0));
                    }
                };

                _mediaPlayer.Info += (s, e) =>
                {
                    if (e.What == MediaInfo.BufferingStart && _statusBadge != null)
                    {
                        _statusBadge.Text = "⏳ Buffering...";
                    }
                    else if (e.What == MediaInfo.BufferingEnd && _statusBadge != null)
                    {
                        _statusBadge.Text = _mediaPlayer.IsPlaying ? "▶ Reproduciendo" : "⏸ Pausa";
                    }
                };

                _mediaPlayer.VideoSizeChanged += (s, e) =>
                {
                    _videoWidth = e.Width;
                    _videoHeight = e.Height;
                    if (_videoHeight > 0 && _qualityText != null)
                    {
                        _qualityText.Text = $"{_videoHeight}p";
                    }
                    AdjustAspectRatio();
                };

                _mediaPlayer.Prepared += (s, e) =>
                {
                    _isPrepared = true;
                    _videoWidth = _mediaPlayer.VideoWidth;
                    _videoHeight = _mediaPlayer.VideoHeight;
                    if (_videoHeight > 0 && _qualityText != null)
                    {
                        _qualityText.Text = $"{_videoHeight}p";
                    }
                    AdjustAspectRatio();
                    _mediaPlayer.Start();
                    UpdatePlayPauseUi(true);
                    StartProgressUpdates();
                    ScheduleAutoHide();
                };
            }
            else
            {
                _mediaPlayer.Reset();
            }

            if (_statusBadge != null) _statusBadge.Text = "⏳ Cargando...";

            _mediaPlayer.SetSurface(_surface);

            var context = global::Android.App.Application.Context;
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(referer))
            {
                headers["Referer"] = referer;
            }
            headers["User-Agent"] = ConfigManager.Current.RandomUserAgent;

            if (url.StartsWith("/") || url.StartsWith("file://"))
            {
                var cleanPath = url.StartsWith("file://") ? url.Substring(7) : url;
                _mediaPlayer.SetDataSource(cleanPath);
            }
            else
            {
                _mediaPlayer.SetDataSource(context, global::Android.Net.Uri.Parse(url)!, headers);
            }

            _mediaPlayer.PrepareAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AndroidVideoPlayerControl.StartPlaybackInternal", ex);
            PlaybackError?.Invoke(this, ex.Message);
        }
    }

    private void AdjustAspectRatio()
    {
        if (_textureView == null || _videoWidth <= 0 || _videoHeight <= 0) return;

        try
        {
            int viewWidth = _textureView.Width;
            int viewHeight = _textureView.Height;
            if (viewWidth <= 0 || viewHeight <= 0) return;

            double videoAspect = (double)_videoWidth / _videoHeight;
            double viewAspect = (double)viewWidth / viewHeight;

            float scaleX = 1.0f;
            float scaleY = 1.0f;

            if (videoAspect > viewAspect)
            {
                scaleY = (float)(viewAspect / videoAspect);
            }
            else
            {
                scaleX = (float)(videoAspect / viewAspect);
            }

            var matrix = new Matrix();
            matrix.SetScale(scaleX, scaleY, viewWidth / 2f, viewHeight / 2f);
            _textureView.SetTransform(matrix);
        }
        catch (Exception ex)
        {
            AppLogger.Error("AndroidVideoPlayerControl.AdjustAspectRatio", ex);
        }
    }

    public int CurrentPosition => _isPrepared && _mediaPlayer != null ? _mediaPlayer.CurrentPosition : 0;
    public int Duration => _isPrepared && _mediaPlayer != null ? _mediaPlayer.Duration : 0;
    public int BufferPercentage => _bufferPercentage;
    public bool IsPlaying => _isPrepared && _mediaPlayer != null && _mediaPlayer.IsPlaying;

    public void Pause()
    {
        try
        {
            if (_isPrepared && _mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                UpdatePlayPauseUi(false);
                ShowControls();
            }
        }
        catch { }
    }

    public void Resume()
    {
        try
        {
            if (_isPrepared && _mediaPlayer != null)
            {
                _mediaPlayer.Start();
                UpdatePlayPauseUi(true);
                ScheduleAutoHide();
            }
        }
        catch { }
    }

    public void SeekTo(int msec)
    {
        try
        {
            if (_isPrepared && _mediaPlayer != null)
            {
                _mediaPlayer.SeekTo(msec);
            }
        }
        catch { }
    }

    public void SetSpeed(float speed)
    {
        _currentSpeed = speed;
        try
        {
            if (_isPrepared && _mediaPlayer != null && Build.VERSION.SdkInt >= BuildVersionCodes.M)
            {
                _mediaPlayer.PlaybackParams = new PlaybackParams().SetSpeed(speed);
            }
            if (_speedBtn != null) _speedBtn.Text = $"{speed:F1}x";
            ShowToast($"{speed:F2}x Velocidad");
        }
        catch { }
    }

    public void Stop()
    {
        try
        {
            _mediaPlayer?.Stop();
            _isPrepared = false;
            _handler.RemoveCallbacksAndMessages(null);
        }
        catch { }
    }

    // ── Native View Hierarchy Creation (FrameLayout with video + controls on top) ──

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context;

        // 1. Root FrameLayout
        _rootLayout = new FrameLayout(context)
        {
            LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        _rootLayout.SetBackgroundColor(Color.Black);

        // 2. TextureView (Child 0: Video layer)
        _textureView = new TextureView(context)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };
        _textureView.SurfaceTextureListener = new SurfaceTextureListenerHelper(this);
        _rootLayout.AddView(_textureView);

        // 3. Native Overlay FrameLayout (Child 1: Controls layer - Guaranteed 100% on top)
        _overlayLayout = BuildNativeOverlay(context);
        _rootLayout.AddView(_overlayLayout);

        return new AndroidPlatformHandle(_rootLayout.Handle, "AndroidView");
    }

    private FrameLayout BuildNativeOverlay(global::Android.Content.Context context)
    {
        var overlay = new FrameLayout(context)
        {
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.MatchParent)
        };

        // Touch Listener to Toggle Controls
        overlay.Click += (s, e) => ToggleControls();

        // ── A. Top Bar (Gradient Background) ─────────────────────────
        _topBar = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Top
            }
        };
        _topBar.SetPadding(DpToPx(12), DpToPx(10), DpToPx(12), DpToPx(14));

        var topGrad = new GradientDrawable(
            GradientDrawable.Orientation.TopBottom,
            new int[] { Color.Argb(220, 0, 0, 0), Color.Argb(0, 0, 0, 0) });
        _topBar.Background = topGrad;

        // Botón Volver (Píldora minimalista con Chevron Left)
        var backLayout = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal
        };
        backLayout.SetGravity(GravityFlags.CenterVertical);
        var backBtnBg = new GradientDrawable();
        backBtnBg.SetColor(Color.Argb(80, 0, 0, 0));
        backBtnBg.SetStroke(DpToPx(1), Color.Argb(40, 255, 255, 255));
        backBtnBg.SetCornerRadius(DpToPx(18));
        backLayout.Background = backBtnBg;
        backLayout.SetPadding(DpToPx(10), DpToPx(6), DpToPx(14), DpToPx(6));

        var backIcon = new ImageView(context)
        {
            LayoutParameters = new LinearLayout.LayoutParams(DpToPx(18), DpToPx(18))
            {
                Gravity = GravityFlags.CenterVertical,
                RightMargin = DpToPx(4)
            }
        };
        backIcon.SetImageDrawable(PlayerIconHelper.CreateChevronLeftDrawable(18, Color.White));
        backLayout.AddView(backIcon);

        var backLabel = new TextView(context)
        {
            Text = "Volver",
            TextSize = 12,
            Typeface = Typeface.Default
        };
        backLabel.SetTextColor(Color.White);
        backLayout.AddView(backLabel);

        backLayout.Click += (s, e) =>
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        };
        _topBar.AddView(backLayout);

        // Título del Anime & Episodio
        _titleText = new TextView(context)
        {
            Text = _title,
            TextSize = 13,
            Typeface = Typeface.DefaultBold,
            Ellipsize = global::Android.Text.TextUtils.TruncateAt.End
        };
        _titleText.SetTextColor(Color.White);
        _titleText.SetSingleLine(true);
        var titleParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.0f)
        {
            Gravity = GravityFlags.CenterVertical,
            LeftMargin = DpToPx(12),
            RightMargin = DpToPx(8)
        };
        _topBar.AddView(_titleText, titleParams);

        // Status Badge (Reproduciendo / Buffering / Pausa)
        _statusBadge = new TextView(context)
        {
            Text = "Cargando...",
            TextSize = 11,
            Typeface = Typeface.DefaultBold
        };
        _statusBadge.SetTextColor(Color.White);
        var sBadge = new GradientDrawable();
        sBadge.SetColor(Color.Argb(140, 34, 150, 243));
        sBadge.SetStroke(DpToPx(1), Color.Argb(50, 255, 255, 255));
        sBadge.SetCornerRadius(DpToPx(8));
        _statusBadge.Background = sBadge;
        _statusBadge.SetPadding(DpToPx(8), DpToPx(4), DpToPx(8), DpToPx(4));
        var sParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.CenterVertical,
            RightMargin = DpToPx(8)
        };
        _topBar.AddView(_statusBadge, sParams);

        // Quality Badge
        _qualityText = new TextView(context)
        {
            Text = _quality,
            TextSize = 11,
            Typeface = Typeface.DefaultBold
        };
        _qualityText.SetTextColor(Color.White);
        var qBadge = new GradientDrawable();
        qBadge.SetColor(Color.Argb(120, 40, 40, 40));
        qBadge.SetStroke(DpToPx(1), Color.Argb(40, 255, 255, 255));
        qBadge.SetCornerRadius(DpToPx(8));
        _qualityText.Background = qBadge;
        _qualityText.SetPadding(DpToPx(8), DpToPx(4), DpToPx(8), DpToPx(4));
        var qParams = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WrapContent, ViewGroup.LayoutParams.WrapContent)
        {
            Gravity = GravityFlags.CenterVertical,
            RightMargin = DpToPx(8)
        };
        _topBar.AddView(_qualityText, qParams);

        // Botón Rotar Orientación (Icono circular minimalista)
        var rotateBtn = new ImageView(context)
        {
            LayoutParameters = new LinearLayout.LayoutParams(DpToPx(34), DpToPx(34))
            {
                Gravity = GravityFlags.CenterVertical
            }
        };
        var rotateBg = new GradientDrawable();
        rotateBg.SetShape(ShapeType.Oval);
        rotateBg.SetColor(Color.Argb(80, 0, 0, 0));
        rotateBg.SetStroke(DpToPx(1), Color.Argb(40, 255, 255, 255));
        rotateBtn.Background = rotateBg;
        rotateBtn.SetPadding(DpToPx(7), DpToPx(7), DpToPx(7), DpToPx(7));
        rotateBtn.SetImageDrawable(PlayerIconHelper.CreateRotateDrawable(20, Color.White));
        rotateBtn.Click += (s, e) => ToggleOrientation();
        _topBar.AddView(rotateBtn);

        overlay.AddView(_topBar);

        // ── B. Center Controls (Replay 10s, Play/Pause, Forward 10s) ──
        _centerControls = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Center
            }
        };
        _centerControls.SetGravity(GravityFlags.Center);

        // Retroceder 10s (Icono replay_10 circular integrado)
        _rewindBtn = new ImageView(context)
        {
            LayoutParameters = new LinearLayout.LayoutParams(DpToPx(52), DpToPx(52))
        };
        var rewindBg = new GradientDrawable();
        rewindBg.SetShape(ShapeType.Oval);
        rewindBg.SetColor(Color.Argb(80, 0, 0, 0));
        rewindBg.SetStroke(DpToPx(1), Color.Argb(35, 255, 255, 255));
        _rewindBtn.Background = rewindBg;
        _rewindBtn.SetPadding(DpToPx(11), DpToPx(11), DpToPx(11), DpToPx(11));
        _rewindBtn.SetImageDrawable(PlayerIconHelper.CreateReplay10Drawable(30, Color.White));
        _rewindBtn.Click += (s, e) =>
        {
            int target = Math.Max(0, CurrentPosition - 10000);
            SeekTo(target);
            ShowToast("-10s");
            ScheduleAutoHide();
        };
        _centerControls.AddView(_rewindBtn);

        // Play / Pause Central (Icono circular grande con borde suave)
        _playPauseCenterBtn = new ImageView(context);
        var playParams = new LinearLayout.LayoutParams(DpToPx(64), DpToPx(64))
        {
            LeftMargin = DpToPx(24),
            RightMargin = DpToPx(24)
        };
        var playBtnBg = new GradientDrawable();
        playBtnBg.SetShape(ShapeType.Oval);
        playBtnBg.SetColor(Color.Argb(100, 0, 0, 0));
        playBtnBg.SetStroke(DpToPx(1.2f), Color.Argb(55, 255, 255, 255));
        _playPauseCenterBtn.Background = playBtnBg;
        _playPauseCenterBtn.SetPadding(DpToPx(16), DpToPx(16), DpToPx(16), DpToPx(16));
        _playPauseCenterBtn.SetImageDrawable(PlayerIconHelper.CreatePauseDrawable(32, Color.White));
        _playPauseCenterBtn.Click += (s, e) =>
        {
            if (IsPlaying) Pause(); else Resume();
        };
        _centerControls.AddView(_playPauseCenterBtn, playParams);

        // Adelantar 10s (Icono forward_10 circular integrado)
        _forwardBtn = new ImageView(context)
        {
            LayoutParameters = new LinearLayout.LayoutParams(DpToPx(52), DpToPx(52))
        };
        var forwardBg = new GradientDrawable();
        forwardBg.SetShape(ShapeType.Oval);
        forwardBg.SetColor(Color.Argb(80, 0, 0, 0));
        forwardBg.SetStroke(DpToPx(1), Color.Argb(35, 255, 255, 255));
        _forwardBtn.Background = forwardBg;
        _forwardBtn.SetPadding(DpToPx(11), DpToPx(11), DpToPx(11), DpToPx(11));
        _forwardBtn.SetImageDrawable(PlayerIconHelper.CreateForward10Drawable(30, Color.White));
        _forwardBtn.Click += (s, e) =>
        {
            int target = Math.Min(Duration, CurrentPosition + 10000);
            SeekTo(target);
            ShowToast("+10s");
            ScheduleAutoHide();
        };
        _centerControls.AddView(_forwardBtn);

        overlay.AddView(_centerControls);

        // ── C. Bottom Bar (Seekbar + Time + Speed) ────────────────────
        _bottomBar = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical,
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Bottom
            }
        };
        _bottomBar.SetPadding(DpToPx(14), DpToPx(14), DpToPx(14), DpToPx(12));

        var botGrad = new GradientDrawable(
            GradientDrawable.Orientation.BottomTop,
            new int[] { Color.Argb(220, 0, 0, 0), Color.Argb(0, 0, 0, 0) });
        _bottomBar.Background = botGrad;

        // SeekBar
        _seekBar = new SeekBar(context)
        {
            Max = 1000,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
        };
        _seekBar.Progress = 0;
        _seekBar.SetPadding(DpToPx(8), DpToPx(4), DpToPx(8), DpToPx(4));

        _seekBar.StartTrackingTouch += (s, e) =>
        {
            _isUserSeeking = true;
            _handler.RemoveCallbacks(_autoHideAction!);
        };

        _seekBar.StopTrackingTouch += (s, e) =>
        {
            _isUserSeeking = false;
            if (Duration > 0 && _seekBar != null)
            {
                int targetMs = (int)((double)_seekBar.Progress / _seekBar.Max * Duration);
                SeekTo(targetMs);
            }
            ScheduleAutoHide();
        };

        _seekBar.ProgressChanged += (s, e) =>
        {
            if (e.FromUser && Duration > 0 && _timeText != null)
            {
                int targetMs = (int)((double)e.Progress / 1000.0 * Duration);
                _timeText.Text = $"{FormatTime(targetMs)} / {FormatTime(Duration)}";
            }
        };

        _bottomBar.AddView(_seekBar);

        // Bottom Info Row: Time + Speed + Aspect
        var bottomInfoRow = new LinearLayout(context)
        {
            Orientation = Orientation.Horizontal,
            LayoutParameters = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent)
        };

        _timeText = new TextView(context)
        {
            Text = "0:00 / 0:00",
            TextSize = 12,
            Typeface = Typeface.DefaultBold
        };
        _timeText.SetTextColor(Color.White);
        var timeParams = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WrapContent, 1.0f)
        {
            Gravity = GravityFlags.CenterVertical,
            LeftMargin = DpToPx(6)
        };
        bottomInfoRow.AddView(_timeText, timeParams);

        // Speed Toggle Button
        _speedBtn = new Button(context)
        {
            Text = "1.0x",
            TextSize = 12,
            Typeface = Typeface.DefaultBold
        };
        _speedBtn.SetTextColor(Color.White);
        var speedBg = new GradientDrawable();
        speedBg.SetColor(Color.Argb(100, 0, 0, 0));
        speedBg.SetStroke(DpToPx(1), Color.Argb(40, 255, 255, 255));
        speedBg.SetCornerRadius(DpToPx(10));
        _speedBtn.Background = speedBg;
        _speedBtn.SetPadding(DpToPx(12), DpToPx(4), DpToPx(12), DpToPx(4));
        _speedBtn.Click += (s, e) =>
        {
            float nextSpeed = _currentSpeed switch
            {
                1.0f => 1.25f,
                1.25f => 1.5f,
                1.5f => 2.0f,
                _ => 1.0f
            };
            SetSpeed(nextSpeed);
            ScheduleAutoHide();
        };
        bottomInfoRow.AddView(_speedBtn);

        _bottomBar.AddView(bottomInfoRow);
        overlay.AddView(_bottomBar);

        // ── D. Center Feedback Toast ─────────────────────────────────
        _toastLayout = new FrameLayout(context)
        {
            Visibility = ViewStates.Gone,
            LayoutParameters = new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.WrapContent,
                ViewGroup.LayoutParams.WrapContent)
            {
                Gravity = GravityFlags.Center
            }
        };
        var toastBg = new GradientDrawable();
        toastBg.SetColor(Color.Argb(200, 0, 0, 0));
        toastBg.SetCornerRadius(DpToPx(12));
        _toastLayout.Background = toastBg;
        _toastLayout.SetPadding(DpToPx(20), DpToPx(10), DpToPx(20), DpToPx(10));

        _toastText = new TextView(context)
        {
            TextSize = 16,
            Typeface = Typeface.DefaultBold
        };
        _toastText.SetTextColor(Color.White);
        _toastLayout.AddView(_toastText);

        overlay.AddView(_toastLayout);

        return overlay;
    }

    private void UpdatePlayPauseUi(bool isPlaying)
    {
        if (_playPauseCenterBtn != null)
        {
            _playPauseCenterBtn.SetImageDrawable(isPlaying
                ? PlayerIconHelper.CreatePauseDrawable(32, Color.White)
                : PlayerIconHelper.CreatePlayDrawable(32, Color.White));
        }

        if (_statusBadge != null)
        {
            _statusBadge.Text = isPlaying ? "▶ Reproduciendo" : "⏸ Pausa";
        }
    }

    private void StartProgressUpdates()
    {
        _handler.RemoveCallbacks(_progressUpdateAction!);
        _handler.Post(_progressUpdateAction!);
    }

    private void UpdateProgressLoop()
    {
        try
        {
            if (_isPrepared && !_isUserSeeking && _seekBar != null && _timeText != null && Duration > 0)
            {
                int pos = CurrentPosition;
                int dur = Duration;
                _seekBar.Progress = (int)((double)pos / dur * _seekBar.Max);

                if (_bufferPercentage > 0)
                {
                    _seekBar.SecondaryProgress = (int)(_bufferPercentage * (_seekBar.Max / 100.0));
                    _timeText.Text = $"{FormatTime(pos)} / {FormatTime(dur)} · Buf: {_bufferPercentage}%";
                }
                else
                {
                    _timeText.Text = $"{FormatTime(pos)} / {FormatTime(dur)}";
                }

                ProgressChanged?.Invoke(this, (pos, dur));
            }
        }
        catch { }

        if (_isPrepared)
        {
            _handler.PostDelayed(_progressUpdateAction!, 500);
        }
    }

    private void ToggleControls()
    {
        if (_isControlsVisible) HideControls(); else ShowControls();
    }

    private void ShowControls()
    {
        _isControlsVisible = true;
        if (_topBar != null) _topBar.Visibility = ViewStates.Visible;
        if (_centerControls != null) _centerControls.Visibility = ViewStates.Visible;
        if (_bottomBar != null) _bottomBar.Visibility = ViewStates.Visible;
        ScheduleAutoHide();
    }

    private void HideControls()
    {
        if (!IsPlaying) return; // Mantener controles visibles si está pausado

        _isControlsVisible = false;
        if (_topBar != null) _topBar.Visibility = ViewStates.Gone;
        if (_centerControls != null) _centerControls.Visibility = ViewStates.Gone;
        if (_bottomBar != null) _bottomBar.Visibility = ViewStates.Gone;
    }

    private void ScheduleAutoHide()
    {
        _handler.RemoveCallbacks(_autoHideAction!);
        if (IsPlaying)
        {
            _handler.PostDelayed(_autoHideAction!, 3500);
        }
    }

    private void ShowToast(string message)
    {
        if (_toastText != null && _toastLayout != null)
        {
            _toastText.Text = message;
            _toastLayout.Visibility = ViewStates.Visible;
            _handler.RemoveCallbacks(HideToast);
            _handler.PostDelayed(HideToast, 1200);
        }
    }

    private void HideToast()
    {
        if (_toastLayout != null) _toastLayout.Visibility = ViewStates.Gone;
    }

    private void ToggleOrientation()
    {
        var main = MainActivity.Instance;
        if (main == null) return;

        if (main.RequestedOrientation == global::Android.Content.PM.ScreenOrientation.SensorLandscape)
        {
            main.SetOrientationPortrait();
        }
        else
        {
            main.SetOrientationLandscape();
        }
    }

    private static string FormatTime(int ms)
    {
        var span = TimeSpan.FromMilliseconds(ms);
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
            : $"{span.Minutes}:{span.Seconds:D2}";
    }

    public static int DpToPx(float dp)
    {
        float density = global::Android.App.Application.Context.Resources?.DisplayMetrics?.Density ?? 1.0f;
        return (int)(dp * density + 0.5f);
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        try
        {
            _handler.RemoveCallbacksAndMessages(null);
            _mediaPlayer?.Stop();
            _mediaPlayer?.Release();
            _mediaPlayer?.Dispose();
            _mediaPlayer = null;

            _surface?.Release();
            _surface?.Dispose();
            _surface = null;

            _textureView?.Dispose();
            _textureView = null;

            _rootLayout?.RemoveAllViews();
            _rootLayout?.Dispose();
            _rootLayout = null;
        }
        catch { }
        base.DestroyNativeControlCore(control);
    }

    #region ISurfaceTextureListener

    internal void OnSurfaceTextureAvailable(SurfaceTexture surfaceTexture, int width, int height)
    {
        _surface?.Dispose();
        _surface = new Surface(surfaceTexture);

        if (_mediaPlayer != null)
        {
            _mediaPlayer.SetSurface(_surface);
        }

        if (!string.IsNullOrEmpty(_pendingUrl))
        {
            StartPlaybackInternal(_pendingUrl, _pendingReferer);
        }
    }

    internal void OnSurfaceTextureSizeChanged(SurfaceTexture surfaceTexture, int width, int height)
    {
        AdjustAspectRatio();
    }

    internal bool OnSurfaceTextureDestroyed(SurfaceTexture surfaceTexture)
    {
        try
        {
            _mediaPlayer?.SetSurface(null);
            _surface?.Release();
            _surface?.Dispose();
            _surface = null;
        }
        catch { }
        return true;
    }

    internal void OnSurfaceTextureUpdated(SurfaceTexture surfaceTexture)
    {
    }

    private class SurfaceTextureListenerHelper : Java.Lang.Object, TextureView.ISurfaceTextureListener
    {
        private readonly AndroidVideoPlayerControl _owner;

        public SurfaceTextureListenerHelper(AndroidVideoPlayerControl owner)
        {
            _owner = owner;
        }

        public void OnSurfaceTextureAvailable(SurfaceTexture surface, int width, int height) => _owner.OnSurfaceTextureAvailable(surface, width, height);
        public bool OnSurfaceTextureDestroyed(SurfaceTexture surface) => _owner.OnSurfaceTextureDestroyed(surface);
        public void OnSurfaceTextureSizeChanged(SurfaceTexture surface, int width, int height) => _owner.OnSurfaceTextureSizeChanged(surface, width, height);
        public void OnSurfaceTextureUpdated(SurfaceTexture surface) => _owner.OnSurfaceTextureUpdated(surface);
    }

    #endregion
}

public static class PlayerIconHelper
{
    public static Drawable CreateChevronLeftDrawable(int sizeDp, Color color)
    {
        int size = AndroidVideoPlayerControl.DpToPx(sizeDp);
        var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap);
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
            StrokeWidth = AndroidVideoPlayerControl.DpToPx(2.4f),
            StrokeCap = Paint.Cap.Round,
            StrokeJoin = Paint.Join.Round
        };
        paint.SetStyle(Paint.Style.Stroke);

        float cx = size / 2f;
        float cy = size / 2f;
        float w = size * 0.22f;
        float h = size * 0.32f;

        var path = new Path();
        path.MoveTo(cx + w * 0.5f, cy - h);
        path.LineTo(cx - w * 0.6f, cy);
        path.LineTo(cx + w * 0.5f, cy + h);

        canvas.DrawPath(path, paint);
        return new BitmapDrawable(global::Android.App.Application.Context.Resources, bitmap);
    }

    public static Drawable CreateReplay10Drawable(int sizeDp, Color color)
    {
        int size = AndroidVideoPlayerControl.DpToPx(sizeDp);
        var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap);
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
            StrokeWidth = AndroidVideoPlayerControl.DpToPx(2.2f),
            StrokeCap = Paint.Cap.Round
        };
        paint.SetStyle(Paint.Style.Stroke);

        float cx = size / 2f;
        float cy = size / 2f;
        float r = size * 0.38f;

        // Circular arc ~270 degrees
        var rectF = new RectF(cx - r, cy - r, cx + r, cy + r);
        canvas.DrawArc(rectF, -210, 265, false, paint);

        // Arrow head CCW
        float arrowX = cx - r * 0.866f;
        float arrowY = cy - r * 0.5f;
        paint.SetStyle(Paint.Style.Fill);
        var path = new Path();
        path.MoveTo(arrowX - AndroidVideoPlayerControl.DpToPx(3), arrowY - AndroidVideoPlayerControl.DpToPx(4.5f));
        path.LineTo(arrowX + AndroidVideoPlayerControl.DpToPx(3.5f), arrowY);
        path.LineTo(arrowX - AndroidVideoPlayerControl.DpToPx(4.5f), arrowY + AndroidVideoPlayerControl.DpToPx(3));
        path.Close();
        canvas.DrawPath(path, paint);

        // "10" text inside
        paint.SetStyle(Paint.Style.Fill);
        paint.TextSize = AndroidVideoPlayerControl.DpToPx(sizeDp * 0.28f);
        paint.SetTypeface(Typeface.DefaultBold);
        paint.TextAlign = Paint.Align.Center;
        var textBounds = new Rect();
        paint.GetTextBounds("10", 0, 2, textBounds);
        canvas.DrawText("10", cx, cy + textBounds.Height() / 2f, paint);

        return new BitmapDrawable(global::Android.App.Application.Context.Resources, bitmap);
    }

    public static Drawable CreateForward10Drawable(int sizeDp, Color color)
    {
        int size = AndroidVideoPlayerControl.DpToPx(sizeDp);
        var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap);
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
            StrokeWidth = AndroidVideoPlayerControl.DpToPx(2.2f),
            StrokeCap = Paint.Cap.Round
        };
        paint.SetStyle(Paint.Style.Stroke);

        float cx = size / 2f;
        float cy = size / 2f;
        float r = size * 0.38f;

        // Circular arc ~270 degrees
        var rectF = new RectF(cx - r, cy - r, cx + r, cy + r);
        canvas.DrawArc(rectF, -55, 265, false, paint);

        // Arrow head CW
        float arrowX = cx + r * 0.866f;
        float arrowY = cy - r * 0.5f;
        paint.SetStyle(Paint.Style.Fill);
        var path = new Path();
        path.MoveTo(arrowX + AndroidVideoPlayerControl.DpToPx(3), arrowY - AndroidVideoPlayerControl.DpToPx(4.5f));
        path.LineTo(arrowX - AndroidVideoPlayerControl.DpToPx(3.5f), arrowY);
        path.LineTo(arrowX + AndroidVideoPlayerControl.DpToPx(4.5f), arrowY + AndroidVideoPlayerControl.DpToPx(3));
        path.Close();
        canvas.DrawPath(path, paint);

        // "10" text inside
        paint.SetStyle(Paint.Style.Fill);
        paint.TextSize = AndroidVideoPlayerControl.DpToPx(sizeDp * 0.28f);
        paint.SetTypeface(Typeface.DefaultBold);
        paint.TextAlign = Paint.Align.Center;
        var textBounds = new Rect();
        paint.GetTextBounds("10", 0, 2, textBounds);
        canvas.DrawText("10", cx, cy + textBounds.Height() / 2f, paint);

        return new BitmapDrawable(global::Android.App.Application.Context.Resources, bitmap);
    }

    public static Drawable CreatePlayDrawable(int sizeDp, Color color)
    {
        int size = AndroidVideoPlayerControl.DpToPx(sizeDp);
        var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap);
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color
        };
        paint.SetStyle(Paint.Style.Fill);

        float cx = size / 2f;
        float cy = size / 2f;
        float r = size * 0.34f;

        var path = new Path();
        path.MoveTo(cx - r * 0.65f, cy - r);
        path.LineTo(cx + r * 0.95f, cy);
        path.LineTo(cx - r * 0.65f, cy + r);
        path.Close();

        var cornerPathEffect = new CornerPathEffect(AndroidVideoPlayerControl.DpToPx(3.5f));
        paint.SetPathEffect(cornerPathEffect);
        canvas.DrawPath(path, paint);

        return new BitmapDrawable(global::Android.App.Application.Context.Resources, bitmap);
    }

    public static Drawable CreatePauseDrawable(int sizeDp, Color color)
    {
        int size = AndroidVideoPlayerControl.DpToPx(sizeDp);
        var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap);
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color
        };
        paint.SetStyle(Paint.Style.Fill);

        float cx = size / 2f;
        float cy = size / 2f;
        float barW = AndroidVideoPlayerControl.DpToPx(4.5f);
        float barH = size * 0.45f;
        float gap = AndroidVideoPlayerControl.DpToPx(5f);

        var rectLeft = new RectF(cx - gap - barW, cy - barH / 2f, cx - gap, cy + barH / 2f);
        var rectRight = new RectF(cx + gap, cy - barH / 2f, cx + gap + barW, cy + barH / 2f);

        canvas.DrawRoundRect(rectLeft, AndroidVideoPlayerControl.DpToPx(2.5f), AndroidVideoPlayerControl.DpToPx(2.5f), paint);
        canvas.DrawRoundRect(rectRight, AndroidVideoPlayerControl.DpToPx(2.5f), AndroidVideoPlayerControl.DpToPx(2.5f), paint);

        return new BitmapDrawable(global::Android.App.Application.Context.Resources, bitmap);
    }

    public static Drawable CreateRotateDrawable(int sizeDp, Color color)
    {
        int size = AndroidVideoPlayerControl.DpToPx(sizeDp);
        var bitmap = Bitmap.CreateBitmap(size, size, Bitmap.Config.Argb8888!);
        var canvas = new Canvas(bitmap);
        var paint = new Paint(PaintFlags.AntiAlias)
        {
            Color = color,
            StrokeWidth = AndroidVideoPlayerControl.DpToPx(2.0f),
            StrokeCap = Paint.Cap.Round
        };
        paint.SetStyle(Paint.Style.Stroke);

        float cx = size / 2f;
        float cy = size / 2f;
        float r = size * 0.32f;

        // Top arc
        canvas.DrawArc(new RectF(cx - r, cy - r, cx + r, cy + r), -135, 90, false, paint);
        // Bottom arc
        canvas.DrawArc(new RectF(cx - r, cy - r, cx + r, cy + r), 45, 90, false, paint);

        // Arrow heads
        paint.SetStyle(Paint.Style.Fill);
        var p1 = new Path();
        p1.MoveTo(cx + r * 0.707f - AndroidVideoPlayerControl.DpToPx(3), cy - r * 0.707f - AndroidVideoPlayerControl.DpToPx(4));
        p1.LineTo(cx + r * 0.707f + AndroidVideoPlayerControl.DpToPx(4), cy - r * 0.707f);
        p1.LineTo(cx + r * 0.707f - AndroidVideoPlayerControl.DpToPx(2), cy - r * 0.707f + AndroidVideoPlayerControl.DpToPx(4));
        p1.Close();
        canvas.DrawPath(p1, paint);

        var p2 = new Path();
        p2.MoveTo(cx - r * 0.707f + AndroidVideoPlayerControl.DpToPx(3), cy + r * 0.707f + AndroidVideoPlayerControl.DpToPx(4));
        p2.LineTo(cx - r * 0.707f - AndroidVideoPlayerControl.DpToPx(4), cy + r * 0.707f);
        p2.LineTo(cx - r * 0.707f + AndroidVideoPlayerControl.DpToPx(2), cy + r * 0.707f - AndroidVideoPlayerControl.DpToPx(4));
        p2.Close();
        canvas.DrawPath(p2, paint);

        return new BitmapDrawable(global::Android.App.Application.Context.Resources, bitmap);
    }
}

public class AndroidPlatformHandle : IPlatformHandle
{
    public IntPtr Handle { get; }
    public string HandleDescriptor { get; }

    public AndroidPlatformHandle(IntPtr handle, string handleDescriptor)
    {
        Handle = handle;
        HandleDescriptor = handleDescriptor;
    }
}
