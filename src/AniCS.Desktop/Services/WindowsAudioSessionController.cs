using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using AniCS.Player;
using NAudio.CoreAudioApi;

namespace AniCS.Desktop.Services;

/// <summary>
/// Implementación de <see cref="IAudioMixerController"/> que controla el
/// <b>volumen maestro del dispositivo de salida de audio por defecto</b>
/// (AudioEndpointVolume, la misma barra que muestra el tray de Windows).
///
/// Por qué AudioEndpointVolume y no SimpleAudioVolume (sesión de app):
///   - Las teclas Fn+F3/F4 de Lenovo Legion cambian el <em>master volume</em>,
///     no el volumen de la sesión de la app. Usando AudioEndpointVolume el
///     slider siempre refleja y controla lo que el usuario ve en el tray.
///   - AudioEndpointVolume existe desde el arranque (no necesita esperar a que
///     LibVLC emita audio), por lo que la lectura inicial es inmediata y exacta.
///   - OnVolumeNotification notifica CUALQUIER cambio externo (tray, teclas
///     multimedia, Lenovo Vantage, etc.) automáticamente.
///
/// LibVLC se fija en Volume=100 por software para no doblar la escala.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsAudioSessionController : IAudioMixerController
{
    private MMDeviceEnumerator?    _enumerator;
    private MMDevice?              _device;
    private AudioEndpointVolume?   _endpoint;
    private bool                   _disposed;

    public event Action<int>? ExternalVolumeChanged;

    public WindowsAudioSessionController()
    {
        try
        {
            _enumerator = new MMDeviceEnumerator();
            _device     = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _endpoint   = _device.AudioEndpointVolume;

            // Suscribirse a cambios externos (teclas multimedia, tray, Vantage, etc.)
            _endpoint.OnVolumeNotification += OnEndpointVolumeChanged;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WindowsAudioSessionController] Init failed: {ex.Message}");
        }
    }

    // ── IAudioMixerController ────────────────────────────────────────────────

    /// <summary>
    /// Volumen maestro del endpoint (0–100). Equivalente exacto a la barra del tray.
    /// Lee correctamente aunque LibVLC no haya empezado a reproducir.
    /// </summary>
    public int Volume
    {
        get
        {
            try
            {
                if (_endpoint != null)
                    return (int)Math.Round(_endpoint.MasterVolumeLevelScalar * 100f);
            }
            catch { }
            return 100;
        }
        set
        {
            try
            {
                if (_endpoint != null)
                    _endpoint.MasterVolumeLevelScalar = Math.Clamp(value, 0, 100) / 100f;
            }
            catch { }
        }
    }

    public bool IsMuted
    {
        get
        {
            try { return _endpoint?.Mute ?? false; }
            catch { return false; }
        }
        set
        {
            try { if (_endpoint != null) _endpoint.Mute = value; }
            catch { }
        }
    }

    /// <summary>
    /// No-op: AudioEndpointVolume existe desde el arranque, sin esperar audio de LibVLC.
    /// Se conserva en la interfaz para compatibilidad futura.
    /// </summary>
    public void TryAcquireSession() { /* noop — endpoint siempre disponible */ }

    // ── Notificación de cambios externos ────────────────────────────────────

    private void OnEndpointVolumeChanged(AudioVolumeNotificationData data)
    {
        int vol = (int)Math.Round(data.MasterVolume * 100f);
        ExternalVolumeChanged?.Invoke(vol);
    }

    // ── IDisposable ──────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            if (_endpoint != null)
                _endpoint.OnVolumeNotification -= OnEndpointVolumeChanged;
        }
        catch { }

        try { _device?.Dispose();     } catch { }
        try { _enumerator?.Dispose(); } catch { }

        _endpoint   = null;
        _device     = null;
        _enumerator = null;
    }
}
