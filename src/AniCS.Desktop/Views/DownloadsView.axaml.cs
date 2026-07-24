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
                var rawTitle = string.IsNullOrWhiteSpace(active.AnimeTitle) ? "Anime_Desconocido" : active.AnimeTitle;
                var safeTitle = string.Join("_", rawTitle.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";
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
                    var extractor = AniCS.Extractors.ExtractorFactory.GetExtractor();
                    var servers = await extractor.GetVideoServersAsync(active.EpisodeUrl);

                    if (servers.Count > 0)
                    {
                        var server = servers.Find(s => s.IsDirectPlaySupported) ?? servers[0];
                        var videoUrl = await extractor.ResolveVideoUrlAsync(server.Url);
                        if (string.IsNullOrEmpty(videoUrl) && AniCS.Desktop.Services.YtDlpService.IsAvailable())
                        {
                            videoUrl = server.Url;
                        }

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
                                    (progress, sizeInfo) => Dispatcher.UIThread.Post(() => {
                                        active.Progress = progress;
                                        if (!string.IsNullOrEmpty(sizeInfo)) active.SizeText = sizeInfo;
                                    }), 

                                    active.CancellationTokenSource.Token);

                                if (result == AniCS.Desktop.Services.DownloadResult.Cancelled && active.State == AniCS.Desktop.Services.DownloadState.Cancelled)
                                {
                                    var rawTitle = string.IsNullOrWhiteSpace(active.AnimeTitle) ? "Anime_Desconocido" : active.AnimeTitle;
                                    var safeTitle = string.Join("_", rawTitle.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                                    if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";
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

    private DownloadedAnime? _currentAnime;
    private DownloadedEpisode? _currentEpisode;
    private DownloadedEpisode? _nextEpisode;

    private void OnPlayEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            var parentExpander = btn.GetVisualAncestors().OfType<Expander>().FirstOrDefault();
            if (parentExpander?.DataContext is DownloadedAnime anime)
            {
                PlayEpisodeWithQuickControl(anime, episode);
            }
        }
    }

    private void PlayEpisodeWithQuickControl(DownloadedAnime anime, DownloadedEpisode episode)
    {
        _currentAnime = anime;
        _currentEpisode = episode;
        _nextEpisode = DownloadManager.GetNextEpisode(anime.Url, episode.EpisodeNumber);

        // Si estaba sin ver, marcarlo como en progreso al reproducir
        if (episode.Status == EpisodeWatchStatus.Unwatched)
        {
            DownloadManager.UpdateEpisodeStatus(anime.Url, episode.EpisodeNumber, EpisodeWatchStatus.InProgress);
        }

        UpdateQuickControlBar();

        DesktopPlayer.Play(episode.FilePath, $"AniCS - {anime.Title} - {episode.EpisodeTitle}", null);
    }

    private void UpdateQuickControlBar()
    {
        if (_currentAnime != null && _currentEpisode != null)
        {
            QuickControlBar.IsVisible = true;
            QuickControlInfoText.Text = $"Último reproducido: {_currentAnime.Title} - {_currentEpisode.EpisodeTitle}";

            if (_nextEpisode != null)
            {
                PlayNextBtn.IsVisible = true;
                PlayNextBtnText.Text = $"Reproducir Siguiente (Ep {_nextEpisode.EpisodeNumber})";
            }
            else
            {
                PlayNextBtn.IsVisible = false;
            }
        }
    }

    private void OnPlayNextEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            var parentExpander = btn.GetVisualAncestors().OfType<Expander>().FirstOrDefault();
            if (parentExpander?.DataContext is DownloadedAnime anime)
            {
                var nextEp = DownloadManager.GetNextEpisode(anime.Url, episode.EpisodeNumber);
                if (nextEp != null)
                {
                    // Marcar el actual como completado y reproducir el siguiente
                    DownloadManager.UpdateEpisodeStatus(anime.Url, episode.EpisodeNumber, EpisodeWatchStatus.Completed);
                    PlayEpisodeWithQuickControl(anime, nextEp);
                }
            }
        }
    }

    private void OnToggleStatusClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            var parentExpander = btn.GetVisualAncestors().OfType<Expander>().FirstOrDefault();
            if (parentExpander?.DataContext is DownloadedAnime anime)
            {
                var nextStatus = episode.Status switch
                {
                    EpisodeWatchStatus.Unwatched => EpisodeWatchStatus.InProgress,
                    EpisodeWatchStatus.InProgress => EpisodeWatchStatus.Completed,
                    _ => EpisodeWatchStatus.Unwatched
                };
                DownloadManager.UpdateEpisodeStatus(anime.Url, episode.EpisodeNumber, nextStatus);
            }
        }
    }

    private void OnMarkCurrentCompletedClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentAnime != null && _currentEpisode != null)
        {
            DownloadManager.UpdateEpisodeStatus(_currentAnime.Url, _currentEpisode.EpisodeNumber, EpisodeWatchStatus.Completed);
        }
    }

    private void OnPlayNextFromBarClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentAnime != null && _currentEpisode != null && _nextEpisode != null)
        {
            DownloadManager.UpdateEpisodeStatus(_currentAnime.Url, _currentEpisode.EpisodeNumber, EpisodeWatchStatus.Completed);
            PlayEpisodeWithQuickControl(_currentAnime, _nextEpisode);
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
