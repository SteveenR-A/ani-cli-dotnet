using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Net.Http;
using AniCS.Extractors;
using AniCS.Models;
using AniCS.Desktop.Converters;
using System;

namespace AniCS.Desktop.Views;

public partial class AnimeDetailsView : UserControl
{
    private readonly AnimeResult _anime;
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
        var extractor = new JKAnimeExtractor(_httpClient);

        if (string.IsNullOrEmpty(_anime.ThumbnailUrl))
        {
            try
            {
                var thumb = await extractor.GetThumbnailAsync(_anime.Url);
                if (!string.IsNullOrEmpty(thumb))
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        _anime.ThumbnailUrl = thumb;
                        AniCS.Desktop.Converters.AsyncImageLoader.SetSourceUrl(CoverImage, thumb);
                    });
                }
            }
            catch { }
        }

        try
        {
            var syn = await extractor.GetSynopsisAsync(_anime.Url);
            Dispatcher.UIThread.Invoke(() =>
            {
                SynopsisText.Text = string.IsNullOrEmpty(syn) ? "Sinopsis no disponible." : syn;
            });
        }
        catch
        {
            Dispatcher.UIThread.Invoke(() => SynopsisText.Text = "Error cargando sinopsis.");
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
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        AniCS.Desktop.Services.DownloadManager.DownloadsChanged -= OnDownloadsChanged;
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
            vm.DownloadText = "✅ Descargado";
            vm.CanDownload = false;
            vm.IsDownloading = false;
            if (vm.ActiveDownload != null)
            {
                vm.ActiveDownload.PropertyChanged -= Vm_ActiveDownload_PropertyChanged;
                vm.ActiveDownload = null;
            }
        }
        else
        {
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
                vm.CanDownload = false;
                vm.IsDownloading = active.State == AniCS.Desktop.Services.DownloadState.Downloading;
            }
            else
            {
                vm.DownloadText = "📥 Descargar";
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
            StatusText.Text = $"Resolviendo video: {vm.Title}...";
            StatusText.IsVisible = true;
            btn.IsEnabled = false;

            try
            {
                var extractor = new JKAnimeExtractor(_httpClient);
                var servers = await extractor.GetVideoServersAsync(vm.Url);

                if (servers.Count > 0)
                {
                    var server = servers.Find(s => s.IsDirectPlaySupported) ?? servers[0];
                    var videoUrl = await extractor.ResolveVideoUrlAsync(server.Url);

                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        StatusText.IsVisible = false;
                        AniCS.Desktop.Services.DesktopPlayer.Play(videoUrl, $"AniCS - {_anime.Title} - {vm.Title}", server.Url);
                    }
                    else
                    {
                        StatusText.Text = "Error: No se pudo extraer el video directo.";
                    }
                }
                else
                {
                    StatusText.Text = "No se encontraron servidores de video.";
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
    }

    private async void OnDownloadEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, vm.EpisodeNumber))
            {
                StatusText.Text = "Este episodio ya está descargado.";
                StatusText.IsVisible = true;
                return;
            }

            StatusText.Text = $"Preparando descarga: {vm.Title}...";
            StatusText.IsVisible = true;
            vm.CanDownload = false;
            vm.DownloadText = "⏳ Preparando...";

            try
            {
                var extractor = new JKAnimeExtractor(_httpClient);
                var servers = await extractor.GetVideoServersAsync(vm.Url);

                if (servers.Count > 0)
                {
                    var server = servers.Find(s => s.IsDirectPlaySupported) ?? servers[0];
                    var videoUrl = await extractor.ResolveVideoUrlAsync(server.Url);

                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        StatusText.IsVisible = false;
                        var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AniCS");

                        var activeDownload = new AniCS.Desktop.Services.ActiveDownload
                        {
                            AnimeTitle = _anime.Title,
                            AnimeUrl = _anime.Url,
                            ThumbnailUrl = _anime.ThumbnailUrl,
                            EpisodeNumber = vm.EpisodeNumber,
                            EpisodeTitle = vm.Title,
                            State = AniCS.Desktop.Services.DownloadState.Downloading,
                            Progress = 0
                        };
                        AniCS.Desktop.Services.DownloadManager.AddActiveDownload(activeDownload);

                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            bool success = await AniCS.Desktop.Services.DesktopPlayer.DownloadAsync(
                                videoUrl, _anime, vm.Episode, defaultDir, server.Url, 
                                progress => Dispatcher.UIThread.Post(() => activeDownload.Progress = progress), 
                                activeDownload.CancellationTokenSource.Token);

                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                if (activeDownload.State == AniCS.Desktop.Services.DownloadState.Downloading)
                                {
                                    activeDownload.State = success ? AniCS.Desktop.Services.DownloadState.Completed : AniCS.Desktop.Services.DownloadState.Error;
                                    AniCS.Desktop.Services.DownloadManager.RemoveActiveDownload(activeDownload);
                                    UpdateEpisodeViewModelState(vm);
                                }
                            });
                        });
                    }
                    else
                    {
                        StatusText.Text = "Error: No se pudo extraer el video para descargar.";
                        vm.CanDownload = true;
                        vm.DownloadText = "📥 Descargar";
                    }
                }
                else
                {
                    StatusText.Text = "No se encontraron servidores de video.";
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
    }

    private void OnCancelDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            if (vm.ActiveDownload != null)
            {
                vm.ActiveDownload.Cancel();
                AniCS.Desktop.Services.DownloadManager.RemoveActiveDownload(vm.ActiveDownload);
                UpdateEpisodeViewModelState(vm);
            }
        }
    }
}

public class EpisodeViewModel : System.ComponentModel.INotifyPropertyChanged
{
    public Episode Episode { get; }
    public string EpisodeNumber => Episode.EpisodeNumber;
    public string Title => Episode.Title;
    public string Url => Episode.Url;
    
    private string _downloadText = "📥 Descargar";
    public string DownloadText
    {
        get => _downloadText;
        set { _downloadText = value; OnPropertyChanged(); }
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
