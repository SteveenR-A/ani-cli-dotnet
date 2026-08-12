using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Net.Http;
using AniCS.Extractors;
using AniCS.Models;
using AniCS.Desktop.Converters;
using AniCS.Desktop.Controls;
using System;
using System.Linq;
using AniCS.Player;
using AniCS.Player.Controls;
using AniCS.Resolver;
using AniCS.Desktop.Services;
using System.Threading.Tasks;


namespace AniCS.Desktop.Views;

public partial class AnimeDetailsView : UserControl
{
    private AnimeResult _anime;
    private static readonly HttpClient _httpClient = new HttpClient();
    // Backends de reproducción y resolución inyectados desde DI
    private readonly IPlayerBackend   _playerBackend;
    private readonly IResolverBackend _resolverBackend;
    // Fallback forcing yt-dlp (reemplaza al antiguo YtDlpService.cs)
    private readonly IResolverBackend _ytdlpFallback = ResolverFactory.Create(ResolverBackendMode.YtDlp);
    // Episodio en reproducción actualmente (para actualizar estado)
    private EpisodeViewModel? _nowPlayingVm;

    public AnimeDetailsView()
    {
        InitializeComponent();
        _anime = null!;
        _playerBackend   = App.Services.GetService(typeof(IPlayerBackend))  as IPlayerBackend  ?? new MpvBackend();
        _resolverBackend = App.Services.GetService(typeof(IResolverBackend)) as IResolverBackend ?? new YtDlpResolverBackend();
    }

    public AnimeDetailsView(AnimeResult anime)
    {
        InitializeComponent();
        _anime = anime;
        DataContext = anime;
        TitleText.Text = anime.Title;
        _playerBackend   = App.Services.GetService(typeof(IPlayerBackend))  as IPlayerBackend  ?? new MpvBackend();
        _resolverBackend = App.Services.GetService(typeof(IResolverBackend)) as IResolverBackend ?? new YtDlpResolverBackend();

        Loaded += OnLoaded;
    }

    // ── Reproductor embebido ──────────────────────────────────────────────────

    // ── Reproductor ──────────────────────────────────────────────────────────

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window is MainWindow mainWindow)
        {
            mainWindow.GoBack();
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {

        AniCS.Desktop.Services.DownloadManager.DownloadsChanged += OnDownloadsChanged;
        var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);

        // ── Peticiones HTTP en PARALELO para velocidad instantánea ─────────
        var detailsTask  = extractor.GetDetailsAsync(_anime.Url);
        var episodesTask = extractor.GetEpisodesAsync(_anime.Url);

        try
        {
            await Task.WhenAll(detailsTask, episodesTask);
        }
        catch { }

        // Aplicar información de detalles
        try
        {
            var details = await detailsTask;
            
            Dispatcher.UIThread.Invoke(() => 
            {
                if (string.IsNullOrWhiteSpace(details.Title)) details.Title = _anime.Title;
                if (string.IsNullOrEmpty(details.ThumbnailUrl)) details.ThumbnailUrl = _anime.ThumbnailUrl;

                _anime = details;
                DataContext = _anime;

                if (!string.IsNullOrEmpty(_anime.ThumbnailUrl))
                {
                    AniCS.Desktop.Converters.AsyncImageLoader.SetSourceUrl(CoverImage, _anime.ThumbnailUrl);
                }

                SynopsisText.Text = string.IsNullOrEmpty(_anime.Synopsis) ? "Sinopsis no disponible." : _anime.Synopsis;
            });
        }
        catch
        {
            Dispatcher.UIThread.Invoke(() => SynopsisText.Text = "Error cargando detalles.");
        }

        // Aplicar lista de episodios
        try
        {
            var episodes = await episodesTask;
            Dispatcher.UIThread.Invoke(() => 
            {
                if (episodes.Count > 0)
                {
                    StatusText.IsVisible = false;
                    var viewModels = new System.Collections.Generic.List<EpisodeViewModel>();
                    foreach (var ep in episodes)
                    {
                        var vm = new EpisodeViewModel(ep);
                        UpdateEpisodeViewModelState(vm);
                        viewModels.Add(vm);
                    }
                    EpisodesList.ItemsSource = viewModels;
                }
                else
                {
                    StatusText.Text = "No se encontraron episodios.";
                }
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Invoke(() => StatusText.Text = $"Error: {ex.Message}");
        }

        AniCS.Desktop.Services.DesktopPlayer.AudioStateChanged += OnAudioStateChanged;
        // Seguimiento de progreso mediante el backend configurado
        _playerBackend.SessionChanged += OnPlayerSessionChanged;
        OnAudioStateChanged();
        UpdateOpeningDownloadState();
    }

    /// <summary>
    /// Recibe actualizaciones de progreso del backend activo (LibVLC o mpv).
    /// Actualiza el estado del episodio en la UI y en el historial de descargas.
    /// </summary>
    private void OnPlayerSessionChanged(PlaySession session)
    {
        if (_nowPlayingVm == null) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Actualizar estado visual del episodio en la lista
            if (session.State == PlayerState.Playing || session.State == PlayerState.Buffering)
            {
                _nowPlayingVm.WatchStatus = EpisodeWatchStatus.InProgress;
            }
            else if (session.IsCompleted || session.State == PlayerState.Ended)
            {
                _nowPlayingVm.WatchStatus = EpisodeWatchStatus.Completed;
                // Si había un descargado, actualizar su estado también
                AniCS.Desktop.Services.DownloadManager.UpdateEpisodeStatus(
                    _anime.Url, _nowPlayingVm.EpisodeNumber,
                    EpisodeWatchStatus.Completed);
            }
        });
    }

    /// <summary>
    /// Falls back to an unconditional yt-dlp resolution (was YtDlpService.ResolveAsync).
    /// Returns the direct URL, or string.Empty if yt-dlp could not resolve it.
    /// </summary>
    private async Task<string> ResolveWithYtDlpFallbackAsync(string url, string? referer)
    {
        if (_ytdlpFallback == null || !_ytdlpFallback.IsAvailable) return string.Empty;
        var resolved = await _ytdlpFallback.ResolveAsync(url, new ResolveOptions { Referer = referer });
        return resolved.Type != MediaType.Unknown ? resolved.DirectUrl : string.Empty;
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        AniCS.Desktop.Services.DownloadManager.DownloadsChanged -= OnDownloadsChanged;
        AniCS.Desktop.Services.DesktopPlayer.AudioStateChanged -= OnAudioStateChanged;
        _playerBackend.SessionChanged -= OnPlayerSessionChanged;
    }


    private void OnDownloadsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (EpisodesList.ItemsSource is System.Collections.Generic.List<EpisodeViewModel> viewModels)
            {
                foreach (var vm in viewModels)
                {
                    UpdateEpisodeViewModelState(vm);
                }
            }
            UpdateOpeningDownloadState();
        });
    }

    private void UpdateOpeningDownloadState()
    {
        if (_anime == null || string.IsNullOrEmpty(_anime.OpeningUrl)) return;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (DownloadOpeningBtn == null) return;

            var active = AniCS.Desktop.Services.DownloadManager.ActiveDownloads
                .FirstOrDefault(d => d.AnimeUrl == _anime.Url && d.EpisodeNumber == "Opening");


            if (active != null)
            {
                DownloadOpeningBtn.IsEnabled = false;
                DownloadOpeningBtnText.Text = active.Progress > 0 ? $"Descargando {active.Progress:F0}%" : "Descargando...";
                DownloadOpeningBtnIcon.Kind = Material.Icons.MaterialIconKind.Refresh;
                CancelOpeningDownloadBtn.IsVisible = true;
                DeleteOpeningDownloadBtn.IsVisible = false;
            }
            else if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, "Opening"))
            {
                DownloadOpeningBtn.IsEnabled = false;
                DownloadOpeningBtnText.Text = "Descargado";
                DownloadOpeningBtnIcon.Kind = Material.Icons.MaterialIconKind.Check;
                CancelOpeningDownloadBtn.IsVisible = false;
                DeleteOpeningDownloadBtn.IsVisible = true;
            }
            else
            {
                DownloadOpeningBtn.IsEnabled = true;
                DownloadOpeningBtnText.Text = "Descargar";
                DownloadOpeningBtnIcon.Kind = Material.Icons.MaterialIconKind.Download;
                CancelOpeningDownloadBtn.IsVisible = false;
                DeleteOpeningDownloadBtn.IsVisible = false;
            }
        });
    }

    private void UpdateEpisodeViewModelState(EpisodeViewModel vm)
    {
        if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, vm.EpisodeNumber))
        {
            vm.DownloadText = "Descargado";
            vm.DownloadIcon = "Check";
            vm.CanDownload = false;
            vm.IsDownloading = false;
            vm.IsDownloaded = true;
            if (vm.ActiveDownload != null)
            {
                vm.ActiveDownload.PropertyChanged -= Vm_ActiveDownload_PropertyChanged;
                vm.ActiveDownload = null;
            }
        }
        else
        {
            vm.IsDownloaded = false;
            var active = AniCS.Desktop.Services.DownloadManager.GetActiveDownload(_anime.Url, vm.EpisodeNumber);
            if (active != null)
            {
                if (vm.ActiveDownload != active)
                {
                    if (vm.ActiveDownload != null) vm.ActiveDownload.PropertyChanged -= Vm_ActiveDownload_PropertyChanged;
                    vm.ActiveDownload = active;
                    active.PropertyChanged += Vm_ActiveDownload_PropertyChanged;
                }
                vm.DownloadText = active.StatusText;
                vm.DownloadIcon = active.StatusIcon;
                vm.CanDownload = false;
                vm.IsDownloading = active.State == AniCS.Desktop.Services.DownloadState.Downloading;
            }
            else
            {
                vm.DownloadText = "Descargar";
                vm.DownloadIcon = "Download";
                vm.CanDownload = true;
                vm.IsDownloading = false;
                if (vm.ActiveDownload != null)
                {
                    vm.ActiveDownload.PropertyChanged -= Vm_ActiveDownload_PropertyChanged;
                    vm.ActiveDownload = null;
                }
            }
        }
    }


    private void Vm_ActiveDownload_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is AniCS.Desktop.Services.ActiveDownload active && e.PropertyName == nameof(active.StatusText))
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (EpisodesList.ItemsSource is System.Collections.Generic.List<EpisodeViewModel> viewModels)
                {
                    var vm = viewModels.Find(v => v.ActiveDownload == active);
                    if (vm != null)
                    {
                        vm.DownloadText = active.StatusText;
                        vm.DownloadIcon = active.StatusIcon;
                        vm.IsDownloading = active.State == AniCS.Desktop.Services.DownloadState.Downloading;
                    }
                }
            });
        }
    }

    private async void OnEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            var ownerWindow = TopLevel.GetTopLevel(this) as Window;
            if (ownerWindow == null) return;
            
            if (AniCS.ConfigManager.Current.UseSpatialHud)
            {
                var options = new System.Collections.Generic.List<AniCS.Desktop.Controls.RadialMenuOption> 
                { 
                    new AniCS.Desktop.Controls.RadialMenuOption { Text = "Reproducir" }, 
                    new AniCS.Desktop.Controls.RadialMenuOption { Text = "Descargar" } 
                };
                int actionIndex = await AniCS.Desktop.Controls.HudRadialMenuDialog.ShowAsync(ownerWindow, options, "");
                
                if (actionIndex == -1) return; // Cancel
                
                if (actionIndex == 0) // Reproducir
                {
                    await ProceedWithPlay(btn, vm, ownerWindow, true);
                }
                else if (actionIndex == 1) // Descargar
                {
                    await ProceedWithDownload(btn, vm, ownerWindow, true);
                }
            }
            else
            {
                await ProceedWithPlay(btn, vm, ownerWindow, false);
            }
        }
    }

    private async System.Threading.Tasks.Task ProceedWithPlay(Button btn, EpisodeViewModel vm, Window ownerWindow, bool useHud)
    {
        StatusText.Text = $"Cargando servidores: {vm.Title}...";
        StatusText.IsVisible = true;
        btn.IsEnabled = false;

        try
        {
            var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);
            var servers = await extractor.GetVideoServersAsync(vm.Url);

            if (servers.Count == 0)
            {
                StatusText.Text = "No se encontraron servidores de video.";
                return;
            }

            // Si hay más de un servidor, mostrar el diálogo de selección
            VideoServer? chosenServer = null;
            string chosenQuality = "Mejor";
            bool isDonghua = AniCS.ConfigManager.Current.ContentType == "Donghua";
            if (servers.Count == 1)
            {
                chosenServer = servers[0];
                chosenQuality = AniCS.ConfigManager.Current.PreferredQuality;
            }
            else
            {
                StatusText.IsVisible = false;
                if (useHud)
                {
                    var options = new System.Collections.Generic.List<AniCS.Desktop.Controls.RadialMenuOption>();
                    foreach (var s in servers) options.Add(new AniCS.Desktop.Controls.RadialMenuOption { Text = s.Name, IsSupported = s.IsDirectPlaySupported });
                    
                    int srvIdx = await AniCS.Desktop.Controls.HudRadialMenuDialog.ShowAsync(ownerWindow, options, "");
                    if (srvIdx != -1) 
                    {
                        chosenServer = servers[srvIdx];
                        chosenQuality = AniCS.ConfigManager.Current.PreferredQuality;
                    }
                }
                else
                {
                    var result = await ServerPickerDialog.ShowAsync(ownerWindow, servers, $"{_anime.Title} — {vm.Title}", isDonghua);
                    chosenServer = result.Server;
                    chosenQuality = result.Quality;
                }
            }

            if (chosenServer == null)
            {
                // El usuario canceló
                StatusText.IsVisible = false;
                return;
            }

            // ── Encapsular toda la resolución en una lambda reutilizable ──────
            // PlayerWindow la llamará una vez al inicio y de nuevo en cada auto-recover.
            var serverUrl   = chosenServer.Url;
            var quality     = chosenQuality;
            Func<System.Threading.Tasks.Task<string>> urlResolver = async () =>
            {
                var freshUrl = await extractor.ResolveVideoUrlAsync(serverUrl);

                if (!string.IsNullOrEmpty(freshUrl) && !chosenServer.IsDirectPlaySupported
                    && !freshUrl.Contains(".m3u8") && !freshUrl.Contains(".mp4"))
                {
                    var resolved = await _resolverBackend.ResolveAsync(freshUrl,
                        new ResolveOptions { Referer = serverUrl });

                    if (resolved.Type != MediaType.Unknown)
                        freshUrl = resolved.DirectUrl;
                    else if (_ytdlpFallback.IsAvailable)
                    {
                        freshUrl = await ResolveWithYtDlpFallbackAsync(freshUrl, serverUrl);
                        if (string.IsNullOrEmpty(freshUrl))
                            freshUrl = await ResolveWithYtDlpFallbackAsync(vm.Url, _anime.Url);
                    }
                }
                else if (string.IsNullOrEmpty(freshUrl))
                {
                    var resolved = await _resolverBackend.ResolveAsync(serverUrl,
                        new ResolveOptions { Referer = serverUrl });

                    if (resolved.Type != MediaType.Unknown)
                        freshUrl = resolved.DirectUrl;
                    else if (_ytdlpFallback.IsAvailable)
                    {
                        freshUrl = await ResolveWithYtDlpFallbackAsync(serverUrl, serverUrl);
                        if (string.IsNullOrEmpty(freshUrl))
                            freshUrl = await ResolveWithYtDlpFallbackAsync(vm.Url, _anime.Url);
                    }
                }

                return freshUrl ?? string.Empty;
            };

            // Obtener la URL inicial para validar que hay algo reproducible antes de abrir la ventana
            StatusText.Text = $"Resolviendo video ({chosenServer.Name})... Por favor, espera.";
            StatusText.IsVisible = true;
            var videoUrl = await urlResolver();

            if (!string.IsNullOrEmpty(videoUrl))
            {
                StatusText.Text = $"¡Abriendo reproductor para {vm.Title}!";
                StatusText.IsVisible = true;

                // Guardar historial
                var history = new AniCS.History.WatchHistory();
                history.Record(_anime.Title, _anime.Url, _anime.ThumbnailUrl, vm.EpisodeNumber, videoUrl);

                _nowPlayingVm = vm;

                // Abrir la nueva ventana independiente del reproductor
                if (_playerBackend is LibVlcBackend)
                {
                    StatusText.IsVisible = false;
                    var playerWindow = new PlayerWindow(
                        _playerBackend,
                        urlResolver,
                        $"{_anime.Title} — {vm.Title}",
                        serverUrl,
                        quality);
                    playerWindow.Show(ownerWindow);
                }
                else
                {
                    // Reproducir con el reproductor externo (mpv)
                    _ = _playerBackend.PlayAsync(videoUrl, $"{_anime.Title} — {vm.Title}", new PlayOptions
                    {
                        Referer = serverUrl,
                        Quality = quality
                    });

                    // Con mpv (proceso externo), solo mostrar el mensaje unos segundos
                    await System.Threading.Tasks.Task.Delay(3000);
                    StatusText.IsVisible = false;
                }
            }
            else
            {
                StatusText.Text = $"Error: No se pudo extraer el video de '{chosenServer.Name}'. " +
                    $"Motor activo: {_resolverBackend.BackendName} / {_playerBackend.BackendName}.";
            }
        }

        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            btn.IsEnabled = true;
        }
    }

    private async void OnDownloadEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            var ownerWindow = TopLevel.GetTopLevel(this) as Window;
            if (ownerWindow != null)
            {
                await ProceedWithDownload(btn, vm, ownerWindow, false);
            }
        }
    }

    private async System.Threading.Tasks.Task ProceedWithDownload(Button btn, EpisodeViewModel vm, Window ownerWindow, bool useHud)
    {
        if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, vm.EpisodeNumber))
        {
            StatusText.Text = "Este episodio ya está descargado.";
            StatusText.IsVisible = true;
            return;
        }

        StatusText.Text = $"Cargando servidores: {vm.Title}...";
        StatusText.IsVisible = true;
        vm.CanDownload = false;
        vm.DownloadText = "⏳ Preparando...";

        try
        {
            var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);
            var servers = await extractor.GetVideoServersAsync(vm.Url);

            if (servers.Count == 0)
            {
                StatusText.Text = "No se encontraron servidores de video.";
                vm.CanDownload = true;
                vm.DownloadText = "📥 Descargar";
                return;
            }

            // Mostrar diálogo de selección si hay más de un servidor
            VideoServer? chosenServer = null;
            string chosenQuality = "Mejor";
            bool isDonghua = AniCS.ConfigManager.Current.ContentType == "Donghua";
            
            if (servers.Count == 1)
            {
                chosenServer = servers[0];
                chosenQuality = AniCS.ConfigManager.Current.PreferredQuality;
            }
            else
            {
                StatusText.IsVisible = false;
                if (useHud)
                {
                    var options = new System.Collections.Generic.List<AniCS.Desktop.Controls.RadialMenuOption>();
                    foreach (var s in servers) options.Add(new AniCS.Desktop.Controls.RadialMenuOption { Text = s.Name, IsSupported = null });
                    
                    int srvIdx = await AniCS.Desktop.Controls.HudRadialMenuDialog.ShowAsync(ownerWindow, options, "");
                    if (srvIdx != -1) 
                    {
                        chosenServer = servers[srvIdx];
                        chosenQuality = AniCS.ConfigManager.Current.PreferredQuality;
                    }
                }
                else
                {
                    var result = await ServerPickerDialog.ShowAsync(ownerWindow, servers, $"{_anime.Title} — {vm.Title}", isDonghua);
                    chosenServer = result.Server;
                    chosenQuality = result.Quality;
                }
            }

            if (chosenServer == null)
            {
                // Usuario canceló
                StatusText.IsVisible = false;
                vm.CanDownload = true;
                vm.DownloadText = "📥 Descargar";
                return;
            }

            StatusText.Text = $"Preparando descarga ({chosenServer.Name})... Por favor, espera.";
            StatusText.IsVisible = true;

            string resolverMethod = _resolverBackend.BackendName;
            var videoUrl = await extractor.ResolveVideoUrlAsync(chosenServer.Url);
            ResolvedMedia? resolvedMedia = null;

            if (!string.IsNullOrEmpty(videoUrl) && !chosenServer.IsDirectPlaySupported && !videoUrl.Contains(".m3u8") && !videoUrl.Contains(".mp4"))
            {
                StatusText.Text = $"Resolviendo enlace directo ({chosenServer.Name})...";
                resolvedMedia = await _resolverBackend.ResolveAsync(videoUrl, new ResolveOptions { Referer = chosenServer.Url });
                if (resolvedMedia.Type == MediaType.Unknown && _ytdlpFallback.IsAvailable)
                {
                    StatusText.Text = $"Obteniendo enlace con yt-dlp ({chosenServer.Name})...";
                    videoUrl = await ResolveWithYtDlpFallbackAsync(videoUrl, chosenServer.Url);
                    if (string.IsNullOrEmpty(videoUrl)) videoUrl = await ResolveWithYtDlpFallbackAsync(vm.Url, _anime.Url);
                    
                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        var type = videoUrl.Contains(".m3u8") || chosenServer.Name.Contains("HLS", StringComparison.OrdinalIgnoreCase) ? MediaType.Hls : MediaType.Mp4;
                        resolvedMedia = new ResolvedMedia(videoUrl, videoUrl, type, chosenServer.Url);
                    }
                    else
                    {
                        resolvedMedia = null;
                    }
                }
            }
            else if (string.IsNullOrEmpty(videoUrl))
            {
                StatusText.Text = $"Extractor interno falló. Intentando resolver ({chosenServer.Name})...";
                resolvedMedia = await _resolverBackend.ResolveAsync(chosenServer.Url, new ResolveOptions { Referer = chosenServer.Url });
                if (resolvedMedia.Type == MediaType.Unknown && _ytdlpFallback.IsAvailable)
                {
                    StatusText.Text = $"Extractor interno falló. Intentando con yt-dlp ({chosenServer.Name})...";
                    videoUrl = await ResolveWithYtDlpFallbackAsync(chosenServer.Url, chosenServer.Url);
                    if (string.IsNullOrEmpty(videoUrl)) videoUrl = await ResolveWithYtDlpFallbackAsync(vm.Url, _anime.Url);
                    
                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        var type = videoUrl.Contains(".m3u8") || chosenServer.Name.Contains("HLS", StringComparison.OrdinalIgnoreCase) ? MediaType.Hls : MediaType.Mp4;
                        resolvedMedia = new ResolvedMedia(videoUrl, videoUrl, type, chosenServer.Url);
                    }
                    else
                    {
                        resolvedMedia = null;
                    }
                }
            }
            else
            {
                // Es un enlace directo
                var type = videoUrl.Contains(".m3u8") || chosenServer.Name.Contains("HLS", StringComparison.OrdinalIgnoreCase) ? MediaType.Hls : MediaType.Mp4;
                resolvedMedia = new ResolvedMedia(videoUrl, videoUrl, type, chosenServer.Url);
            }

            if (resolvedMedia == null || resolvedMedia.Type == MediaType.Unknown)
            {
                resolverMethod = "yt-dlp fallback";
            }

            if (!string.IsNullOrEmpty(videoUrl) || (resolvedMedia != null && resolvedMedia.Type != MediaType.Unknown))
            {
                StatusText.Text = $"¡Descarga iniciada para {vm.Title} ({resolverMethod})!";
                StatusText.IsVisible = true;
                var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AniCS");

                var animeTitle = string.IsNullOrWhiteSpace(_anime.Title) ? "Anime_Desconocido" : _anime.Title;
                var activeDownload = new AniCS.Desktop.Services.ActiveDownload
                {
                    AnimeTitle = animeTitle,
                    AnimeUrl = _anime.Url,
                    ThumbnailUrl = _anime.ThumbnailUrl,
                    EpisodeUrl = vm.Url,
                    EpisodeNumber = vm.EpisodeNumber,
                    EpisodeTitle = vm.Title,
                    State = AniCS.Desktop.Services.DownloadState.Downloading,
                    Progress = 0
                };

                AniCS.Desktop.Services.DownloadManager.AddActiveDownload(activeDownload);

                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    AniCS.Desktop.Services.DownloadResult result;
                    if (resolvedMedia != null && resolvedMedia.Type != MediaType.Unknown)
                    {
                        var safeTitle = string.Join("_", animeTitle.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";
                        var animeDir = System.IO.Path.Combine(defaultDir, safeTitle);
                        if (!System.IO.Directory.Exists(animeDir)) System.IO.Directory.CreateDirectory(animeDir);
                        var episodeNumStr = string.IsNullOrWhiteSpace(vm.EpisodeNumber) ? "Desconocido" : vm.EpisodeNumber;
                        var outputPath = System.IO.Path.Combine(animeDir, $"Episodio {episodeNumStr}.mp4");
                        
                        var progress = new Progress<DownloadProgress>(p => 
                        {
                            Dispatcher.UIThread.Post(() => {
                                activeDownload.Progress = p.Percent;
                                if (!string.IsNullOrEmpty(p.SizeInfo)) activeDownload.SizeText = p.SizeInfo;
                            });
                        });
                        
                        var resolverResult = await _resolverBackend.DownloadAsync(resolvedMedia, outputPath, progress, activeDownload.CancellationTokenSource.Token);
                        result = resolverResult.Code switch
                        {
                            DownloadResultCode.Success => AniCS.Desktop.Services.DownloadResult.Success,
                            DownloadResultCode.Cancelled => AniCS.Desktop.Services.DownloadResult.Cancelled,
                            _ => AniCS.Desktop.Services.DownloadResult.Error
                        };
                    }
                    else
                    {
                        var safeTitle = string.Join("_", animeTitle.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";
                        var animeDir = System.IO.Path.Combine(defaultDir, safeTitle);
                        if (!System.IO.Directory.Exists(animeDir)) System.IO.Directory.CreateDirectory(animeDir);
                        var episodeNumStr = string.IsNullOrWhiteSpace(vm.EpisodeNumber) ? "Desconocido" : vm.EpisodeNumber;
                        var outputPath = System.IO.Path.Combine(animeDir, $"Episodio {episodeNumStr}.mp4");

                        var progress = new Progress<DownloadProgress>(p =>
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                activeDownload.Progress = p.Percent;
                                if (!string.IsNullOrEmpty(p.SizeInfo)) activeDownload.SizeText = p.SizeInfo;
                            });
                        });

                        var fbResult = await _ytdlpFallback.DownloadAsync(
                            new ResolvedMedia(videoUrl, videoUrl, MediaType.Unknown, chosenServer.Url),
                            outputPath, progress, activeDownload.CancellationTokenSource.Token);
                        result = fbResult.Code switch
                        {
                            DownloadResultCode.Success => AniCS.Desktop.Services.DownloadResult.Success,
                            DownloadResultCode.Cancelled => AniCS.Desktop.Services.DownloadResult.Cancelled,
                            _ => AniCS.Desktop.Services.DownloadResult.Error
                        };
                    }

                    if (result == AniCS.Desktop.Services.DownloadResult.Cancelled && activeDownload.State == AniCS.Desktop.Services.DownloadState.Cancelled)
                    {
                        var safeTitle = string.Join("_", animeTitle.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";
                        var episodeNumStr = string.IsNullOrWhiteSpace(vm.EpisodeNumber) ? "Desconocido" : vm.EpisodeNumber;
                        AniCS.Desktop.Services.DownloadManager.CleanupPartialFiles(defaultDir, safeTitle, episodeNumStr);
                    }

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (activeDownload.State == AniCS.Desktop.Services.DownloadState.Downloading || result == AniCS.Desktop.Services.DownloadResult.Success || result == AniCS.Desktop.Services.DownloadResult.Error)
                        {
                            if (result == AniCS.Desktop.Services.DownloadResult.Success)
                            {
                                activeDownload.State = AniCS.Desktop.Services.DownloadState.Completed;
                                StatusText.Text = $"¡Descarga completada para {vm.Title}!";
                            }
                            else if (result == AniCS.Desktop.Services.DownloadResult.Error)
                            {
                                activeDownload.State = AniCS.Desktop.Services.DownloadState.Error;
                                StatusText.Text = $"Error al descargar {vm.Title}. Intenta con otro servidor.";
                                StatusText.IsVisible = true;
                            }

                            if (activeDownload.State == AniCS.Desktop.Services.DownloadState.Completed || activeDownload.State == AniCS.Desktop.Services.DownloadState.Error || activeDownload.State == AniCS.Desktop.Services.DownloadState.Cancelled)
                            {
                                AniCS.Desktop.Services.DownloadManager.RemoveActiveDownload(activeDownload);
                            }
                            UpdateEpisodeViewModelState(vm);
                        }
                    });
                });
            }
            else
            {
                StatusText.Text = $"Error: No se pudo extraer el video de '{chosenServer.Name}'.";
                vm.CanDownload = true;
                vm.DownloadText = "📥 Descargar";
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
            vm.CanDownload = true;
            vm.DownloadText = "📥 Descargar";
        }
    }

    private void OnCancelDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            if (vm.ActiveDownload != null)
            {
                bool wasPaused = vm.ActiveDownload.State == AniCS.Desktop.Services.DownloadState.Paused;
                vm.ActiveDownload.Cancel();
                
                if (wasPaused)
                {
                    var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AniCS");
                    var safeTitle = string.Join("_", _anime.Title.Split(System.IO.Path.GetInvalidFileNameChars()));
                    var episodeNumStr = string.IsNullOrWhiteSpace(vm.EpisodeNumber) ? "Desconocido" : vm.EpisodeNumber;
                    AniCS.Desktop.Services.DownloadManager.CleanupPartialFiles(defaultDir, safeTitle, episodeNumStr);
                }
                
                AniCS.Desktop.Services.DownloadManager.RemoveActiveDownload(vm.ActiveDownload);
                UpdateEpisodeViewModelState(vm);
            }
        }
    }

    private void OnDeleteDownloadedEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            AniCS.Desktop.Services.DownloadManager.DeleteEpisode(_anime.Url, vm.EpisodeNumber);
            UpdateEpisodeViewModelState(vm);
        }
    }

    private void OnAudioStateChanged()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (AniCS.Desktop.Services.DesktopPlayer.IsAudioPlaying)
            {
                PlayAudioBtnText.Text = "Detener Audio";
                PlayAudioBtnIcon.Kind = Material.Icons.MaterialIconKind.Stop;
                if (this.TryFindResource("AppStatusInProgressColor", out var resStop) && resStop is Avalonia.Media.IBrush bStop)
                {
                    PlayAudioBtn.Background = bStop;
                }
            }

            else
            {
                PlayAudioBtnText.Text = "Audio en App";
                PlayAudioBtnIcon.Kind = Material.Icons.MaterialIconKind.VolumeHigh;
                if (this.TryFindResource("AppPrimaryColor", out var res) && res is Avalonia.Media.IBrush b)
                {
                    PlayAudioBtn.Background = b;
                }
            }
        });
    }

    private void OnPlayOpeningAudioClicked(object? sender, RoutedEventArgs e)
    {
        if (AniCS.Desktop.Services.DesktopPlayer.IsAudioPlaying)
        {
            AniCS.Desktop.Services.DesktopPlayer.StopAudio();
        }
        else if (_anime != null && !string.IsNullOrEmpty(_anime.OpeningUrl))
        {
            AniCS.Desktop.Services.DesktopPlayer.PlayAudio(_anime.OpeningUrl, $"AniCS - {_anime.Title} - Opening/Trailer", null);
        }
    }

    private void OnOpenOpeningInBrowserClicked(object? sender, RoutedEventArgs e)
    {
        if (_anime != null && !string.IsNullOrEmpty(_anime.OpeningUrl))
        {
            AniCS.Desktop.Services.DesktopPlayer.OpenInBrowser(_anime.OpeningUrl);
        }
    }



    private async void OnDownloadOpeningClicked(object? sender, RoutedEventArgs e)
    {
        if (_anime == null || string.IsNullOrEmpty(_anime.OpeningUrl)) return;

        var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AniCS");
        var safeTitle = string.Join("_", _anime.Title.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";
        var animeDir = System.IO.Path.Combine(defaultDir, safeTitle);

        var activeDownload = new AniCS.Desktop.Services.ActiveDownload
        {
            AnimeTitle = _anime.Title,
            AnimeUrl = _anime.Url,
            ThumbnailUrl = _anime.ThumbnailUrl,
            EpisodeUrl = _anime.OpeningUrl,
            EpisodeNumber = "Opening",
            EpisodeTitle = "Opening / Trailer",
            State = AniCS.Desktop.Services.DownloadState.Downloading
        };

        AniCS.Desktop.Services.DownloadManager.AddActiveDownload(activeDownload);
        UpdateOpeningDownloadState();

        var outputPath = System.IO.Path.Combine(animeDir, "Episodio Opening.mp4");
        var cancellationToken = activeDownload.CancellationTokenSource.Token;
        var progress = new Progress<DownloadProgress>(p =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                activeDownload.Progress = p.Percent;
                if (!string.IsNullOrEmpty(p.SizeInfo)) activeDownload.SizeText = p.SizeInfo;
                UpdateOpeningDownloadState();
            });
        });
        var fbResult = await _ytdlpFallback.DownloadAsync(
            new ResolvedMedia(_anime.OpeningUrl, _anime.OpeningUrl, MediaType.Unknown),
            outputPath, progress, cancellationToken);
        var result = fbResult.Code switch
        {
            DownloadResultCode.Success => AniCS.Desktop.Services.DownloadResult.Success,
            DownloadResultCode.Cancelled => AniCS.Desktop.Services.DownloadResult.Cancelled,
            _ => AniCS.Desktop.Services.DownloadResult.Error
        };

        if (result == AniCS.Desktop.Services.DownloadResult.Success)
        {
            activeDownload.State = AniCS.Desktop.Services.DownloadState.Completed;
            var filePath = System.IO.Path.Combine(animeDir, "Episodio Opening.mp4");
            if (!System.IO.File.Exists(filePath))
            {
                var files = System.IO.Directory.GetFiles(animeDir, "Episodio Opening.*");
                if (files.Length > 0) filePath = files[0];
            }
            AniCS.Desktop.Services.DownloadManager.RecordDownload(_anime.Title, _anime.Url, _anime.ThumbnailUrl, "Opening", "Opening / Trailer", filePath);
        }
        else if (result == AniCS.Desktop.Services.DownloadResult.Error)
        {
            activeDownload.State = AniCS.Desktop.Services.DownloadState.Error;
        }

        UpdateOpeningDownloadState();
    }

    private void OnCancelOpeningDownloadClicked(object? sender, RoutedEventArgs e)
    {
        var active = AniCS.Desktop.Services.DownloadManager.ActiveDownloads
            .FirstOrDefault(d => d.AnimeUrl == _anime.Url && d.EpisodeNumber == "Opening");


        if (active != null)
        {
            active.Cancel();
            var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AniCS");
            var safeTitle = string.Join("_", _anime.Title.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
            if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";

            AniCS.Desktop.Services.DownloadManager.CleanupPartialFiles(defaultDir, safeTitle, "Opening");
            AniCS.Desktop.Services.DownloadManager.RemoveActiveDownload(active);
            UpdateOpeningDownloadState();
        }
    }

    private void OnDeleteOpeningDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (_anime != null)
        {
            AniCS.Desktop.Services.DownloadManager.DeleteEpisode(_anime.Url, "Opening");
            UpdateOpeningDownloadState();
        }
    }
}



public class EpisodeViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public Episode Episode { get; }
    public string EpisodeNumber => Episode.EpisodeNumber;
    public string Title => Episode.Title;
    public string Url => Episode.Url;

    // ── Estado de visualización (Sin ver / En proceso / Finalizado) ──────────
    private EpisodeWatchStatus _watchStatus = EpisodeWatchStatus.Unwatched;
    public EpisodeWatchStatus WatchStatus
    {
        get => _watchStatus;
        set
        {
            _watchStatus = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WatchStatusIcon));
            OnPropertyChanged(nameof(WatchStatusTip));
        }
    }

    public string WatchStatusIcon => _watchStatus switch
    {
        EpisodeWatchStatus.InProgress => "▶",
        EpisodeWatchStatus.Completed  => "✅",
        _                             => "☐",
    };

    public string WatchStatusTip => _watchStatus switch
    {
        EpisodeWatchStatus.InProgress => "En proceso",
        EpisodeWatchStatus.Completed  => "Finalizado",
        _                             => "Sin ver",
    };

    private string _downloadText = "Descargar";
    public string DownloadText
    {
        get => _downloadText;
        set { _downloadText = value; OnPropertyChanged(); }
    }

    private string _downloadIcon = "Download";
    public string DownloadIcon
    {
        get => _downloadIcon;
        set { _downloadIcon = value; OnPropertyChanged(); }
    }
    
    private bool _canDownload = true;
    public bool CanDownload
    {
        get => _canDownload;
        set { _canDownload = value; OnPropertyChanged(); }
    }
    
    private bool _isDownloading = false;
    public bool IsDownloading
    {
        get => _isDownloading;
        set { _isDownloading = value; OnPropertyChanged(); }
    }

    private bool _isDownloaded = false;
    public bool IsDownloaded
    {
        get => _isDownloaded;
        set { _isDownloaded = value; OnPropertyChanged(); }
    }
    
    public bool IsDownloadButtonVisible => !AniCS.ConfigManager.Current.UseSpatialHud;

    
    public AniCS.Desktop.Services.ActiveDownload? ActiveDownload { get; set; }
    
    public EpisodeViewModel(Episode episode)
    {
        Episode = episode;
    }
    
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
