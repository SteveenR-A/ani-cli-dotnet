using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Net.Http;
using AniCS.Extractors;
using AniCS.Models;
using AniCS.Desktop.Converters;
using AniCS.Desktop.Controls;
using System;
using System.Collections.Generic;
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
    // No es readonly: se actualiza en cada reproducción con la config actual del usuario
    private IPlayerBackend _playerBackend;
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
        Services.NavigationHelper.GoBack(this);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        UpdateHeaderLayout(e.NewSize.Width);
    }

    private void UpdateHeaderLayout(double width)
    {
        if (HeaderGrid == null || LeftPanel == null || RightPanel == null) return;

        if (width < 620)
        {
            HeaderGrid.ColumnDefinitions = ColumnDefinitions.Parse("*");
            HeaderGrid.RowDefinitions = RowDefinitions.Parse("Auto, Auto");

            Grid.SetRow(LeftPanel, 0);
            Grid.SetColumn(LeftPanel, 0);
            LeftPanel.Margin = new Thickness(0, 0, 0, 15);
            LeftPanel.Width = double.NaN;

            Grid.SetRow(RightPanel, 1);
            Grid.SetColumn(RightPanel, 0);
        }
        else
        {
            HeaderGrid.ColumnDefinitions = ColumnDefinitions.Parse("Auto, *");
            HeaderGrid.RowDefinitions = RowDefinitions.Parse("Auto");

            Grid.SetRow(LeftPanel, 0);
            Grid.SetColumn(LeftPanel, 0);
            LeftPanel.Margin = new Thickness(0, 0, 20, 15);
            LeftPanel.Width = 280;

            Grid.SetRow(RightPanel, 0);
            Grid.SetColumn(RightPanel, 1);
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Suscribir eventos del backend de reproducción
        _playerBackend.SessionChanged += OnPlayerSessionChanged;
        _playerBackend.ErrorOccurred  += OnPlayerError;

        AniCS.Desktop.Services.DownloadManager.DownloadsChanged += OnDownloadsChanged;

        // Si la URL está vacía (ej. importado desde disco sin metadata en JSON), buscar online por título
        if (string.IsNullOrWhiteSpace(_anime.Url) && !string.IsNullOrWhiteSpace(_anime.Title))
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                StatusText.Text = $"Buscando '{_anime.Title}' en línea...";
                StatusText.IsVisible = true;
            });

            try
            {
                var searchExtractor = ExtractorFactory.GetExtractor();
                var searchResults = await searchExtractor.SearchAsync(_anime.Title);
                if (searchResults.Count == 0)
                {
                    IAnimeExtractor altExtractor = searchExtractor is JKAnimeExtractor
                        ? new MundoDonghuaExtractor(_httpClient)
                        : new JKAnimeExtractor(_httpClient);
                    searchResults = await altExtractor.SearchAsync(_anime.Title);
                }

                if (searchResults.Count > 0)
                {
                    var match = searchResults.FirstOrDefault(r => 
                        string.Equals(r.Title, _anime.Title, StringComparison.OrdinalIgnoreCase)) ?? searchResults[0];

                    _anime.Url = match.Url;
                    if (string.IsNullOrEmpty(_anime.ThumbnailUrl)) _anime.ThumbnailUrl = match.ThumbnailUrl;

                    AniCS.Desktop.Services.DownloadManager.LinkAnimeUrl(_anime.Title, _anime.Url, _anime.ThumbnailUrl);
                }
            }
            catch (Exception ex)
            {
                AniCS.AppLogger.Error("AnimeDetailsView.SearchOnlineForUrl", ex);
            }
        }

        // Si aún no tiene URL online, mostrar los episodios descargados localmente
        if (string.IsNullOrWhiteSpace(_anime.Url))
        {
            var downloadedAnime = AniCS.Desktop.Services.DownloadManager.GetAll()
                .FirstOrDefault(a => string.Equals(a.Title, _anime.Title, StringComparison.OrdinalIgnoreCase));

            Dispatcher.UIThread.Invoke(() =>
            {
                if (downloadedAnime != null && downloadedAnime.Episodes.Count > 0)
                {
                    StatusText.IsVisible = false;
                    var viewModels = new System.Collections.Generic.List<EpisodeViewModel>();
                    foreach (var ep in downloadedAnime.Episodes)
                    {
                        var episodeModel = new Episode
                        {
                            EpisodeNumber = ep.EpisodeNumber,
                            Title = ep.EpisodeTitle,
                            Url = ep.FilePath
                        };
                        var vm = new EpisodeViewModel(episodeModel)
                        {
                            IsDownloaded = true,
                            CanDownload = false,
                            DownloadText = "Descargado",
                            DownloadIcon = "Check"
                        };
                        viewModels.Add(vm);
                    }
                    EpisodesList.ItemsSource = viewModels;
                    SynopsisText.Text = "Anime importado desde archivos locales en la carpeta de descargas.";
                }
                else
                {
                    StatusText.Text = "No se pudo encontrar información en línea para este anime.";
                }
            });

            AniCS.Desktop.Services.DesktopPlayer.AudioStateChanged += OnAudioStateChanged;
            OnAudioStateChanged();
            UpdateOpeningDownloadState();
            return;
        }

        var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);

        // ── Peticiones HTTP en PARALELO para velocidad instantánea ─────────
        var detailsTask  = extractor.GetDetailsAsync(_anime.Url);
        var episodesTask = extractor.GetEpisodesAsync(_anime.Url);

        try
        {
            await Task.WhenAll(detailsTask, episodesTask);
        }
        catch (Exception ex)
        {
            AniCS.AppLogger.Warn("AnimeDetailsView", $"Task.WhenAll details/episodes failed: {ex.Message}");
        }

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
    /// Recibe errores del backend activo y los muestra en el StatusText de la vista.
    /// </summary>
    private void OnPlayerError(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusText.Text = $"Error del reproductor: {message}";
            StatusText.IsVisible = true;
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
        _playerBackend.ErrorOccurred  -= OnPlayerError;
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
            else if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, "Opening", _anime.Title))
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
        if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, vm.EpisodeNumber, _anime.Title))
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
            
            if (ownerWindow != null && AniCS.ConfigManager.Current.UseSpatialHud)
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

    private async Task<(VideoServer? Server, string Quality)> ShowServerPickerModalAsync(
        System.Collections.Generic.List<VideoServer> servers, string title, bool useHud, Window? ownerWindow)
    {
        bool isDonghua = AniCS.ConfigManager.Current.ContentType == "Donghua";
        if (ownerWindow != null)
        {
            if (useHud)
            {
                var options = new System.Collections.Generic.List<AniCS.Desktop.Controls.RadialMenuOption>();
                foreach (var s in servers) options.Add(new AniCS.Desktop.Controls.RadialMenuOption { Text = s.Name, IsSupported = s.IsDirectPlaySupported });
                
                int srvIdx = await AniCS.Desktop.Controls.HudRadialMenuDialog.ShowAsync(ownerWindow, options, "");
                if (srvIdx != -1) return (servers[srvIdx], AniCS.ConfigManager.Current.PreferredQuality);
                return (null, AniCS.ConfigManager.Current.PreferredQuality);
            }
            else
            {
                var res = await ServerPickerDialog.ShowAsync(ownerWindow, servers, title, isDonghua);
                return (res.Server, res.Quality);
            }
        }

        // Android / Non-Window environment: show internal modal overlay
        var tcs = new System.Threading.Tasks.TaskCompletionSource<(VideoServer?, string)>();
        var container = new StackPanel { Spacing = 10, Margin = new Avalonia.Thickness(10) };

        foreach (var srv in servers)
        {
            var btn = new Button
            {
                Content = srv.Name,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Avalonia.Thickness(12),
                Background = Brushes.Purple,
                Foreground = Brushes.White,
                CornerRadius = new Avalonia.CornerRadius(8)
            };
            var capturedServer = srv;
            btn.Click += (s, e) =>
            {
                CloseAndroidModal();
                tcs.TrySetResult((capturedServer, AniCS.ConfigManager.Current.PreferredQuality));
            };
            container.Children.Add(btn);
        }

        if (!ShowAndroidModal(title, container))
        {
            return (servers.FirstOrDefault(), AniCS.ConfigManager.Current.PreferredQuality);
        }

        return await tcs.Task;
    }

    private bool ShowAndroidModal(string title, Control content)
    {
        Visual? current = this;
        while (current != null)
        {
            if (current.GetType().Name == "AndroidMainView")
            {
                var method = current.GetType().GetMethod("ShowModal");
                method?.Invoke(current, new object[] { title, content });
                return true;
            }
            current = current.GetVisualParent();
        }
        return false;
    }

    private void CloseAndroidModal()
    {
        Visual? current = this;
        while (current != null)
        {
            if (current.GetType().Name == "AndroidMainView")
            {
                var method = current.GetType().GetMethod("CloseModal");
                method?.Invoke(current, null);
                return;
            }
            current = current.GetVisualParent();
        }
    }

    private async System.Threading.Tasks.Task<(string VideoUrl, string ServerUrl, string Quality, Func<System.Threading.Tasks.Task<string>> Resolver)?> ResolveEpisodeStreamAsync(EpisodeViewModel targetVm)
    {
        try
        {
            var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);
            var servers = await extractor.GetVideoServersAsync(targetVm.Url);
            if (servers.Count == 0) return null;

            var chosenServer = servers[0];
            string chosenQuality = AniCS.ConfigManager.Current.PreferredQuality;
            var serverUrl = chosenServer.Url;

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
                        var fb = await ResolveWithYtDlpFallbackAsync(freshUrl, serverUrl);
                        if (string.IsNullOrEmpty(fb))
                            fb = await ResolveWithYtDlpFallbackAsync(targetVm.Url, _anime.Url);
                        if (!string.IsNullOrEmpty(fb))
                            freshUrl = fb;
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
                        var fb = await ResolveWithYtDlpFallbackAsync(serverUrl, serverUrl);
                        if (string.IsNullOrEmpty(fb))
                            fb = await ResolveWithYtDlpFallbackAsync(targetVm.Url, _anime.Url);
                        if (!string.IsNullOrEmpty(fb))
                            freshUrl = fb;
                    }
                    
                    if (string.IsNullOrEmpty(freshUrl))
                        freshUrl = serverUrl;
                }

                return freshUrl ?? string.Empty;
            };

            var videoUrl = await urlResolver();
            if (string.IsNullOrEmpty(videoUrl)) return null;

            return (videoUrl, serverUrl, chosenQuality, urlResolver);
        }
        catch (Exception ex)
        {
            AniCS.AppLogger.Error("AnimeDetailsView.ResolveEpisodeStreamAsync", ex);
            return null;
        }
    }

    private async System.Threading.Tasks.Task ProceedWithPlay(Button btn, EpisodeViewModel vm, Window? ownerWindow, bool useHud)
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

            VideoServer? chosenServer = null;
            string chosenQuality = "Mejor";
            if (servers.Count == 1)
            {
                chosenServer = servers[0];
                chosenQuality = AniCS.ConfigManager.Current.PreferredQuality;
            }
            else
            {
                StatusText.IsVisible = false;
                var pickerResult = await ShowServerPickerModalAsync(servers, $"{_anime.Title} — {vm.Title}", useHud, ownerWindow);
                chosenServer = pickerResult.Server;
                chosenQuality = pickerResult.Quality;
            }

            if (chosenServer == null)
            {
                StatusText.IsVisible = false;
                btn.IsEnabled = true;
                return;
            }

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
                        var fb = await ResolveWithYtDlpFallbackAsync(freshUrl, serverUrl);
                        if (string.IsNullOrEmpty(fb))
                            fb = await ResolveWithYtDlpFallbackAsync(vm.Url, _anime.Url);
                        if (!string.IsNullOrEmpty(fb))
                            freshUrl = fb;
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
                        var fb = await ResolveWithYtDlpFallbackAsync(serverUrl, serverUrl);
                        if (string.IsNullOrEmpty(fb))
                            fb = await ResolveWithYtDlpFallbackAsync(vm.Url, _anime.Url);
                        if (!string.IsNullOrEmpty(fb))
                            freshUrl = fb;
                    }
                    
                    if (string.IsNullOrEmpty(freshUrl))
                        freshUrl = serverUrl;
                }

                return freshUrl ?? string.Empty;
            };

            StatusText.Text = $"Resolviendo video ({chosenServer.Name})... Por favor, espera.";
            StatusText.IsVisible = true;
            var videoUrl = await urlResolver();

            if (!string.IsNullOrEmpty(videoUrl))
            {
                StatusText.Text = $"¡Abriendo reproductor para {vm.Title}!";
                StatusText.IsVisible = true;

                var history = new AniCS.History.WatchHistory();
                history.Record(_anime.Title, _anime.Url, _anime.ThumbnailUrl, vm.EpisodeNumber, videoUrl);

                _nowPlayingVm = vm;

                // Detener cualquier reproducción de audio global/secundaria en segundo plano
                AniCS.Desktop.Services.DesktopPlayer.StopAudio();

                // Crear un backend fresco según la configuración actual del usuario.
                // Esto garantiza que si el usuario cambió el motor en Ajustes (sin reiniciar
                // la app), o si LibVLC falló al arrancar y quedó un MpvBackend como singleton,
                // se respete la elección actual del usuario.
                var currentBackend = PlayerFactory.CreateFromConfig();

                // Actualizar el campo para que OnUnloaded desuscriba el evento correcto
                _playerBackend.SessionChanged -= OnPlayerSessionChanged;
                _playerBackend.ErrorOccurred  -= OnPlayerError;
                _playerBackend = currentBackend;
                _playerBackend.SessionChanged += OnPlayerSessionChanged;
                _playerBackend.ErrorOccurred  += OnPlayerError;

                if (currentBackend is LibVlcBackend libVlcForPlay)
                {
                    StatusText.IsVisible = false;

                    PlayerWindow? playerWindow = null;

                    Func<Task>? buildPrevAction(EpisodeViewModel currentVm)
                    {
                        var list = (EpisodesList.ItemsSource as IEnumerable<EpisodeViewModel>)?.ToList();
                        if (list == null) return null;
                        int idx = list.IndexOf(currentVm);
                        if (idx <= 0) return null;
                        var prevVm = list[idx - 1];

                        return async () =>
                        {
                            var res = await ResolveEpisodeStreamAsync(prevVm);
                            if (res != null && playerWindow != null)
                            {
                                _nowPlayingVm = prevVm;
                                var (vUrl, sUrl, q, resolver) = res.Value;
                                var pAction = buildPrevAction(prevVm);
                                var nAction = buildNextAction(prevVm);
                                await playerWindow.ChangeEpisodeAsync(
                                    $"{_anime.Title} — {prevVm.Title}",
                                    resolver,
                                    sUrl,
                                    q,
                                    pAction,
                                    nAction);
                            }
                        };
                    }

                    Func<Task>? buildNextAction(EpisodeViewModel currentVm)
                    {
                        var list = (EpisodesList.ItemsSource as IEnumerable<EpisodeViewModel>)?.ToList();
                        if (list == null) return null;
                        int idx = list.IndexOf(currentVm);
                        if (idx < 0 || idx + 1 >= list.Count) return null;
                        var nextVm = list[idx + 1];

                        return async () =>
                        {
                            var res = await ResolveEpisodeStreamAsync(nextVm);
                            if (res != null && playerWindow != null)
                            {
                                _nowPlayingVm = nextVm;
                                var (vUrl, sUrl, q, resolver) = res.Value;
                                var pAction = buildPrevAction(nextVm);
                                var nAction = buildNextAction(nextVm);
                                await playerWindow.ChangeEpisodeAsync(
                                    $"{_anime.Title} — {nextVm.Title}",
                                    resolver,
                                    sUrl,
                                    q,
                                    pAction,
                                    nAction);
                            }
                        };
                    }

                    string initialResolvedUrl = videoUrl;
                    Func<System.Threading.Tasks.Task<string>> safeResolver = async () =>
                    {
                        if (!string.IsNullOrEmpty(initialResolvedUrl))
                        {
                            var u = initialResolvedUrl;
                            initialResolvedUrl = null!;
                            return u;
                        }
                        return await urlResolver();
                    };

                    playerWindow = new PlayerWindow(
                        libVlcForPlay,
                        safeResolver,
                        $"{_anime.Title} — {vm.Title}",
                        serverUrl,
                        quality,
                        buildPrevAction(vm),
                        buildNextAction(vm));

                    var ownerWin = TopLevel.GetTopLevel(this) as Window;
                    if (ownerWin != null)
                        playerWindow.Show(ownerWin);
                    else
                        playerWindow.Show();
                }
                else
                {
                    _ = currentBackend.PlayAsync(videoUrl, $"{_anime.Title} — {vm.Title}", new PlayOptions
                    {
                        Referer = serverUrl,
                        Quality = quality
                    });

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
            await ProceedWithDownload(btn, vm, ownerWindow, false);
        }
    }

    private async System.Threading.Tasks.Task ProceedWithDownload(Button btn, EpisodeViewModel vm, Window? ownerWindow, bool useHud)
    {
        if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, vm.EpisodeNumber, _anime.Title))
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

            VideoServer? chosenServer = null;
            string chosenQuality = "Mejor";
            
            if (servers.Count == 1)
            {
                chosenServer = servers[0];
                chosenQuality = AniCS.ConfigManager.Current.PreferredQuality;
            }
            else
            {
                StatusText.IsVisible = false;
                var pickerResult = await ShowServerPickerModalAsync(servers, $"{_anime.Title} — {vm.Title}", useHud, ownerWindow);
                chosenServer = pickerResult.Server;
                chosenQuality = pickerResult.Quality;
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
                var defaultDir = AniCS.Desktop.Services.DownloadManager.DefaultDownloadDirectory;

                var animeTitle = string.IsNullOrWhiteSpace(_anime.Title) ? "Anime_Desconocido" : _anime.Title;
                var activeDownload = new AniCS.Desktop.Services.ActiveDownload
                {
                    AnimeTitle = animeTitle,
                    AnimeUrl = _anime.Url,
                    ThumbnailUrl = _anime.ThumbnailUrl,
                    EpisodeUrl = vm.Url,
                    EpisodeNumber = vm.EpisodeNumber,
                    EpisodeTitle = vm.Title,
                    ServerUrl = chosenServer.Url,
                    DirectVideoUrl = videoUrl,
                    Progress = 0
                };

                AniCS.Desktop.Services.DownloadManager.StartOrResumeDownloadAsync(activeDownload);
                UpdateEpisodeViewModelState(vm);
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
                    var defaultDir = AniCS.Desktop.Services.DownloadManager.DefaultDownloadDirectory;
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

        var defaultDir = AniCS.Desktop.Services.DownloadManager.DefaultDownloadDirectory;
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
            var defaultDir = AniCS.Desktop.Services.DownloadManager.DefaultDownloadDirectory;
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
