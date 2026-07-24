using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Net.Http;
using AniCS.Extractors;
using AniCS.Models;
using AniCS.Desktop.Converters;
using AniCS.Desktop.Controls;
using System;

namespace AniCS.Desktop.Views;

public partial class AnimeDetailsView : UserControl
{
    private AnimeResult _anime;
    private static readonly HttpClient _httpClient = new HttpClient();

    public AnimeDetailsView()
    {
        InitializeComponent();
        _anime = null!;
    }

    public AnimeDetailsView(AnimeResult anime)
    {
        InitializeComponent();
        _anime = anime;
        DataContext = anime;
        TitleText.Text = anime.Title;

        Loaded += OnLoaded;
    }

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
        var extractor = ExtractorFactory.GetExtractor();

        try
        {
            var details = await extractor.GetDetailsAsync(_anime.Url);
            Dispatcher.UIThread.Invoke(() =>
            {
                // Conservar Title y Thumbnail si GetDetailsAsync no los trajo
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

        try
        {
            var episodes = await extractor.GetEpisodesAsync(_anime.Url);
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

        AniCS.Desktop.Services.DesktopPlayer.OnPlayerError += OnPlayerErrorReceived;
    }

    private void OnPlayerErrorReceived(string message)
    {
        Dispatcher.UIThread.InvokeAsync(() => {
            StatusText.Text = message;
            StatusText.IsVisible = true;
        });
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        AniCS.Desktop.Services.DownloadManager.DownloadsChanged -= OnDownloadsChanged;
        AniCS.Desktop.Services.DesktopPlayer.OnPlayerError -= OnPlayerErrorReceived;
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
            var extractor = ExtractorFactory.GetExtractor();
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

            StatusText.Text = $"Resolviendo video ({chosenServer.Name})... Por favor, espera.";
            StatusText.IsVisible = true;

            var videoUrl = await extractor.ResolveVideoUrlAsync(chosenServer.Url);

            // Si el enlace obtenido no es un stream directo (.m3u8 / .mp4) y yt-dlp está disponible,
            // resolvemos el video en segundo plano dentro de la app ANTES de abrir mpv para no mostrar una ventana negra vacía.
            if (!string.IsNullOrEmpty(videoUrl) && !videoUrl.Contains(".m3u8") && !videoUrl.Contains(".mp4"))
            {
                if (AniCS.Desktop.Services.YtDlpService.IsAvailable())
                {
                    StatusText.Text = $"Obteniendo enlace directo con yt-dlp ({chosenServer.Name})... Por favor, espera.";
                    var resolved = await AniCS.Desktop.Services.YtDlpService.ResolveAsync(videoUrl, chosenServer.Url);
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        videoUrl = resolved;
                    }
                }
            }
            else if (string.IsNullOrEmpty(videoUrl))
            {
                if (AniCS.Desktop.Services.YtDlpService.IsAvailable())
                {
                    StatusText.Text = $"Extractor interno falló. Intentando con yt-dlp ({chosenServer.Name})...";
                    videoUrl = await AniCS.Desktop.Services.YtDlpService.ResolveAsync(
                        chosenServer.Url,
                        referer: chosenServer.Url);
                }
            }

            if (!string.IsNullOrEmpty(videoUrl))
            {
                StatusText.Text = $"¡Abriendo reproductor para {vm.Title}!";
                StatusText.IsVisible = true;
                
                // Guardar historial
                var history = new AniCS.History.WatchHistory();
                history.Record(_anime.Title, _anime.Url, _anime.ThumbnailUrl, vm.EpisodeNumber, videoUrl);
                
                AniCS.Desktop.Services.DesktopPlayer.Play(videoUrl, $"AniCS - {_anime.Title} - {vm.Title}", chosenServer.Url, chosenQuality);

                // Ocultar el mensaje después de unos segundos si todo salió bien
                await System.Threading.Tasks.Task.Delay(3000);
                StatusText.IsVisible = false;
            }
            else
            {
                bool ytdlpAvailable = AniCS.Desktop.Services.YtDlpService.IsAvailable();
                StatusText.Text = ytdlpAvailable
                    ? $"Error: No se pudo extraer el video de '{chosenServer.Name}' (interno + yt-dlp fallaron)."
                    : $"Error: No se pudo extraer el video de '{chosenServer.Name}'. Instala yt-dlp para soporte de servidores externos.";
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
            var extractor = ExtractorFactory.GetExtractor();
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

            var videoUrl = await extractor.ResolveVideoUrlAsync(chosenServer.Url);
            if (string.IsNullOrEmpty(videoUrl) && AniCS.Desktop.Services.YtDlpService.IsAvailable())
            {
                videoUrl = chosenServer.Url;
            }

            if (!string.IsNullOrEmpty(videoUrl))
            {
                StatusText.Text = $"¡Descarga iniciada para {vm.Title}!";
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
                    var result = await AniCS.Desktop.Services.DesktopPlayer.DownloadAsync(
                        videoUrl, _anime, vm.Episode, defaultDir, chosenServer.Url, chosenQuality,
                        (progress, sizeInfo) => Dispatcher.UIThread.Post(() => {
                            activeDownload.Progress = progress;
                            if (!string.IsNullOrEmpty(sizeInfo)) activeDownload.SizeText = sizeInfo;
                        }),

                        activeDownload.CancellationTokenSource.Token);

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
                                activeDownload.State = AniCS.Desktop.Services.DownloadState.Completed;
                            else if (result == AniCS.Desktop.Services.DownloadResult.Error)
                                activeDownload.State = AniCS.Desktop.Services.DownloadState.Error;

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
}

public class EpisodeViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public Episode Episode { get; }
    public string EpisodeNumber => Episode.EpisodeNumber;
    public string Title => Episode.Title;
    public string Url => Episode.Url;
    
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
