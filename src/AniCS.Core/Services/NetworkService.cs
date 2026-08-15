using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;

namespace AniCS.Core.Services;

/// <summary>
/// Servicio global de monitorización de conectividad a Internet.
/// Detecta cambios en adaptadores de red y verifica acceso real a Internet.
/// </summary>
public static class NetworkService
{
    private static readonly HttpClient _pingClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(3)
    };

    private static Timer? _monitorTimer;
    private static int _isChecking = 0;
    private static bool _isConnected = true;

    /// <summary>
    /// Indica si actualmente hay conectividad a Internet.
    /// </summary>
    public static bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                ConnectivityChanged?.Invoke(_isConnected);
            }
        }
    }

    /// <summary>
    /// Evento disparado cuando la conectividad cambia de estado (online/offline).
    /// </summary>
    public static event Action<bool>? ConnectivityChanged;

    /// <summary>
    /// Inicia el monitoreo continuo en segundo plano.
    /// </summary>
    public static void StartMonitoring()
    {
        if (_monitorTimer != null) return;

        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        
        // Verificación inicial rápida
        _ = CheckConnectivityAsync();

        // Sondeo periódico cada 10 segundos
        _monitorTimer = new Timer(async _ =>
        {
            await CheckConnectivityAsync();
        }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Detiene el monitoreo.
    /// </summary>
    public static void StopMonitoring()
    {
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _monitorTimer?.Dispose();
        _monitorTimer = null;
    }

    private static void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
    {
        _ = CheckConnectivityAsync();
    }

    /// <summary>
    /// Realiza un chequeo asíncrono de conectividad real enviando una solicitud ligera.
    /// </summary>
    public static async Task<bool> CheckConnectivityAsync()
    {
        if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
        {
            return _isConnected;
        }

        try
        {
            // 1. Comprobación rápida de interfaz de red local
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                IsConnected = false;
                return false;
            }

            // 2. Comprobación de acceso HTTP real a endpoints ultrarrápidos y confiables
            bool reachable = false;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                using var req = new HttpRequestMessage(HttpMethod.Head, "https://1.1.1.1");
                using var res = await _pingClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                reachable = res.IsSuccessStatusCode || (int)res.StatusCode < 500;
            }
            catch
            {
                // Fallback a google si 1.1.1.1 está bloqueado en ciertas redes
                try
                {
                    using var cts2 = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    using var req2 = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com");
                    using var res2 = await _pingClient.SendAsync(req2, HttpCompletionOption.ResponseHeadersRead, cts2.Token);
                    reachable = res2.IsSuccessStatusCode || (int)res2.StatusCode < 500;
                }
                catch
                {
                    reachable = false;
                }
            }

            IsConnected = reachable;
            return reachable;
        }
        finally
        {
            Interlocked.Exchange(ref _isChecking, 0);
        }
    }
}
