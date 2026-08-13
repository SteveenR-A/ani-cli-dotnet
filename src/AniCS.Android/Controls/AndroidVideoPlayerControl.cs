using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Platform;
using Android.Widget;
using Android.Net;
using AniCS;

namespace AniCS.Android.Controls;

public class AndroidVideoPlayerControl : NativeControlHost
{
    private VideoView? _videoView;
    private MediaController? _mediaController;

    public event EventHandler? PlaybackCompleted;
    public event EventHandler<string>? PlaybackError;

    public void Play(string url, string? referer = null)
    {
        if (_videoView == null) return;

        try
        {
            var headers = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(referer))
            {
                headers["Referer"] = referer;
            }
            headers["User-Agent"] = ConfigManager.Current.RandomUserAgent;

            _videoView.SetVideoURI(global::Android.Net.Uri.Parse(url), headers);
            _videoView.Start();
        }
        catch (Exception ex)
        {
            AppLogger.Error("AndroidVideoPlayerControl.Play", ex);
            PlaybackError?.Invoke(this, ex.Message);
        }
    }

    public int CurrentPosition => _videoView?.CurrentPosition ?? 0;
    public int Duration => _videoView?.Duration ?? 0;
    public int BufferPercentage => _videoView?.BufferPercentage ?? 0;
    public bool IsPlaying => _videoView?.IsPlaying ?? false;

    public void Pause()
    {
        try { _videoView?.Pause(); } catch { }
    }

    public void Resume()
    {
        try { _videoView?.Start(); } catch { }
    }

    public void SeekTo(int msec)
    {
        try { _videoView?.SeekTo(msec); } catch { }
    }

    public void SetSpeed(float speed)
    {
    }

    public void Stop()
    {
        try
        {
            _videoView?.StopPlayback();
        }
        catch { }
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        var context = global::Android.App.Application.Context;

        _videoView = new VideoView(context);
        try
        {
            _videoView.SetZOrderMediaOverlay(true);
        }
        catch { }

        _mediaController = new MediaController(context);
        _mediaController.SetAnchorView(_videoView);
        _videoView.SetMediaController(_mediaController);

        _videoView.Completion += (s, e) => PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        _videoView.Error += (s, e) =>
        {
            PlaybackError?.Invoke(this, $"Error de reproducción ({e.What})");
            e.Handled = true;
        };

        return new AndroidPlatformHandle(_videoView.Handle, "AndroidView");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        try
        {
            _videoView?.StopPlayback();
            _videoView?.Dispose();
            _videoView = null;
        }
        catch { }
        base.DestroyNativeControlCore(control);
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
