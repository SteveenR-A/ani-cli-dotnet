using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Linq;
using AniCS.Desktop.Services;
using AniCS.Models;
using System.ComponentModel;

namespace AniCS.Desktop.Views;

public partial class DownloadsView : UserControl, INotifyPropertyChanged
{
    public bool HasActiveDownloads => DownloadManager.ActiveDownloads.Count > 0;

    public DownloadsView()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged += OnDownloadsChanged;
        LoadData();
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged -= OnDownloadsChanged;
    }

    private void OnDownloadsChanged(object? sender, System.EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(LoadData);
    }

    private void OnReloadClicked(object? sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void LoadData()
    {
        var downloads = DownloadManager.GetAll().ToList();
        
        ActiveDownloadsList.ItemsSource = null;
        ActiveDownloadsList.ItemsSource = DownloadManager.ActiveDownloads;
        
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasActiveDownloads)));

        AnimeList.ItemsSource = null;
        if (downloads.Count == 0)
        {
            StatusText.IsVisible = true;
        }
        else
        {
            StatusText.IsVisible = false;
            AnimeList.ItemsSource = downloads;
        }
    }

    private void OnCancelActiveDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActiveDownload active)
        {
            bool wasPaused = active.State == DownloadState.Paused;
            active.Cancel();
            
            if (wasPaused)
            {
                var defaultDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos), "AniCS");
                var safeTitle = string.Join("_", active.AnimeTitle.Split(System.IO.Path.GetInvalidFileNameChars()));
                var episodeNumStr = string.IsNullOrWhiteSpace(active.EpisodeNumber) ? "Desconocido" : active.EpisodeNumber;
                DownloadManager.CleanupPartialFiles(defaultDir, safeTitle, episodeNumStr);
            }
            
            DownloadManager.RemoveActiveDownload(active);
        }
    }

    private async void OnPauseResumeActiveDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActiveDownload active)
        {
            if (active.State == DownloadState.Downloading)
            {
                active.Pause();
            }
            else if (active.State == DownloadState.Paused)
            {
                active.Resume();
                
                try
                {
                    var extractor = new AniCS.Extractors.JKAnimeExtractor(new System.Net.Http.HttpClient());
                    var servers = await extractor.GetVideoServersAsync(active.EpisodeUrl);

                    if (servers.Count > 0)
                    {
                        var server = servers.Find(s => s.IsDirectPlaySupported) ?? servers[0];
                        var videoUrl = await extractor.ResolveVideoUrlAsync(server.Url);

                        if (!string.IsNullOrEmpty(videoUrl))
                        {
                            var defaultDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos), "AniCS");
                            
                            _ = System.Threading.Tasks.Task.Run(async () =>
                            {
                                var result = await AniCS.Desktop.Services.DesktopPlayer.DownloadAsync(
                                    videoUrl, 
                                    new AnimeResult { Title = active.AnimeTitle, Url = active.AnimeUrl, ThumbnailUrl = active.ThumbnailUrl }, 
                                    new Episode { EpisodeNumber = active.EpisodeNumber, Title = active.EpisodeTitle, Url = active.EpisodeUrl }, 
                                    defaultDir, 
                                    server.Url, 
                                    AniCS.ConfigManager.Current.PreferredQuality,
                                    progress => Dispatcher.UIThread.Post(() => active.Progress = progress), 
                                    active.CancellationTokenSource.Token);

                                if (result == AniCS.Desktop.Services.DownloadResult.Cancelled && active.State == AniCS.Desktop.Services.DownloadState.Cancelled)
                                {
                                    var safeTitle = string.Join("_", active.AnimeTitle.Split(System.IO.Path.GetInvalidFileNameChars()));
                                    var episodeNumStr = string.IsNullOrWhiteSpace(active.EpisodeNumber) ? "Desconocido" : active.EpisodeNumber;
                                    AniCS.Desktop.Services.DownloadManager.CleanupPartialFiles(defaultDir, safeTitle, episodeNumStr);
                                }

                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    if (active.State == AniCS.Desktop.Services.DownloadState.Downloading || result == AniCS.Desktop.Services.DownloadResult.Success || result == AniCS.Desktop.Services.DownloadResult.Error)
                                    {
                                        if (result == AniCS.Desktop.Services.DownloadResult.Success)
                                            active.State = AniCS.Desktop.Services.DownloadState.Completed;
                                        else if (result == AniCS.Desktop.Services.DownloadResult.Error)
                                            active.State = AniCS.Desktop.Services.DownloadState.Error;

                                        if (active.State == AniCS.Desktop.Services.DownloadState.Completed || active.State == AniCS.Desktop.Services.DownloadState.Error || active.State == AniCS.Desktop.Services.DownloadState.Cancelled)
                                        {
                                            AniCS.Desktop.Services.DownloadManager.RemoveActiveDownload(active);
                                        }
                                    }
                                });
                            });
                        }
                        else
                        {
                            active.State = DownloadState.Error;
                        }
                    }
                    else
                    {
                        active.State = DownloadState.Error;
                    }
                }
                catch
                {
                    active.State = DownloadState.Error;
                }
            }
        }
    }

    private void OnGoToAnimeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedAnime anime)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            {
                mainWindow.NavigateToAnimeDetails(new AnimeResult
                {
                    Title = anime.Title,
                    Url = anime.Url,
                    ThumbnailUrl = anime.ThumbnailUrl
                });
            }
        }
    }

    private void OnDeleteAnimeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedAnime anime)
        {
            DownloadManager.DeleteAnime(anime.Url);
        }
    }

    private void OnPlayEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            var parentExpander = btn.GetVisualAncestors().OfType<Expander>().FirstOrDefault();
            if (parentExpander?.DataContext is DownloadedAnime anime)
            {
                DesktopPlayer.Play(episode.FilePath, $"AniCS - {anime.Title} - {episode.EpisodeTitle}", null);
            }
        }
    }

    private void OnDeleteEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            var parentExpander = btn.GetVisualAncestors().OfType<Expander>().FirstOrDefault();
            if (parentExpander?.DataContext is DownloadedAnime anime)
            {
                DownloadManager.DeleteEpisode(anime.Url, episode.EpisodeNumber);
            }
        }
    }
    
    public new event PropertyChangedEventHandler? PropertyChanged;
}
