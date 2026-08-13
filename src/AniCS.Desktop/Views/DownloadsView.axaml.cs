using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using AniCS.Desktop.Services;
using AniCS.Models;
using System.ComponentModel;

using AniCS.Player;
using AniCS.Resolver;

namespace AniCS.Desktop.Views;

public partial class DownloadsView : UserControl, INotifyPropertyChanged
{
    public bool HasActiveDownloads => DownloadManager.ActiveDownloads.Count > 0;
    // No es readonly: se actualiza en cada reproducción con la config actual del usuario
    private IPlayerBackend _playerBackend;
    private readonly IResolverBackend _resolverBackend;
    private readonly IResolverBackend _ytdlpFallback = ResolverFactory.Create(ResolverBackendMode.YtDlp);

    public DownloadsView()
    {
        InitializeComponent();
        DataContext = this;
        _playerBackend = App.Services.GetService(typeof(IPlayerBackend)) as IPlayerBackend ?? new MpvBackend();
        _resolverBackend = App.Services.GetService(typeof(IResolverBackend)) as IResolverBackend ?? new AniCS.Resolver.YtDlpResolverBackend();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged += OnDownloadsChanged;
        _playerBackend.SessionChanged += OnPlayerSessionChanged;
        LoadData();
    }
    
    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged -= OnDownloadsChanged;
        _playerBackend.SessionChanged -= OnPlayerSessionChanged;
    }

    private void OnPlayerSessionChanged(PlaySession session)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_currentAnime != null && _currentEpisode != null)
            {
                if (session.Url.Equals(_currentEpisode.FilePath, System.StringComparison.OrdinalIgnoreCase) ||
                    session.Title.Contains(_currentEpisode.EpisodeTitle))
                {
                    var status = session.IsCompleted ? EpisodeWatchStatus.Completed : EpisodeWatchStatus.InProgress;
                    DownloadManager.UpdateEpisodeStatus(_currentAnime.Url, _currentEpisode.EpisodeNumber, status);
                }
            }
        });
    }

    private void OnDownloadsChanged(object? sender, System.EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(LoadData);
    }

    private void OnReloadClicked(object? sender, RoutedEventArgs e)
    {
        // 1. Escanear el disco en busca de archivos huérfanos (no registrados)
        int found = DownloadManager.ScanDiskDownloads();

        // 2. Recargar la UI (ScanDiskDownloads ya llama DownloadsChanged si hay cambios,
        //    pero llamamos LoadData igualmente para refrescar aunque no haya nada nuevo)
        LoadData();

        // 3. Mostrar feedback breve
        StatusText.IsVisible = true;
        StatusText.Text = found > 0
            ? $"Escáner completado: {found} episodio(s) importado(s) del disco."
            : "Escáner completado: sin episodios nuevos para importar.";

        // Ocultar el mensaje tras 4 segundos si hay descargas visibles
        var downloads = DownloadManager.GetAll();
        if (downloads.Count > 0)
        {
            Avalonia.Threading.DispatcherTimer.RunOnce(
                () => { StatusText.IsVisible = false; },
                TimeSpan.FromSeconds(4));
        }
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
                        if (string.IsNullOrEmpty(videoUrl) && _ytdlpFallback.IsAvailable)
                        {
                            videoUrl = server.Url;
                        }

                        if (!string.IsNullOrEmpty(videoUrl))
                        {
                            var defaultDir = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos), "AniCS");
                            
                            _ = System.Threading.Tasks.Task.Run(async () =>
                            {
                                var rawTitle = string.IsNullOrWhiteSpace(active.AnimeTitle) ? "Anime_Desconocido" : active.AnimeTitle;
                                var safeTitle = string.Join("_", rawTitle.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                                if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime_Desconocido";
                                var episodeNumStr = string.IsNullOrWhiteSpace(active.EpisodeNumber) ? "Desconocido" : active.EpisodeNumber;
                                
                                var animeDir = System.IO.Path.Combine(defaultDir, safeTitle);
                                System.IO.Directory.CreateDirectory(animeDir);
                                var outputPath = System.IO.Path.Combine(animeDir, $"Episodio {episodeNumStr}.mp4");

                                var progress = new System.Progress<AniCS.Resolver.DownloadProgress>(p => {
                                    Dispatcher.UIThread.Post(() => {
                                        active.Progress = p.Percent;
                                        if (!string.IsNullOrEmpty(p.SizeInfo)) active.SizeText = p.SizeInfo;
                                    });
                                });

                                var result = await _resolverBackend.DownloadAsync(
                                    new AniCS.Resolver.ResolvedMedia(videoUrl, videoUrl, AniCS.Resolver.MediaType.Unknown, server.Url), 
                                    outputPath,
                                    progress,
                                    active.CancellationTokenSource.Token);

                                // Convertir el resultado para mantener compatibilidad con el enumerador local en caso de que existiera
                                var convertedResult = result.Code == AniCS.Resolver.DownloadResultCode.Success ? AniCS.Desktop.Services.DownloadResult.Success :
                                                      result.Code == AniCS.Resolver.DownloadResultCode.Cancelled ? AniCS.Desktop.Services.DownloadResult.Cancelled :
                                                      AniCS.Desktop.Services.DownloadResult.Error;

                                if (convertedResult == AniCS.Desktop.Services.DownloadResult.Success && result.OutputPath != null)
                                {
                                    AniCS.Desktop.Services.DownloadManager.RecordDownload(active.AnimeTitle, active.AnimeUrl, active.ThumbnailUrl, active.EpisodeNumber, active.EpisodeTitle, result.OutputPath);
                                }

                                if (convertedResult == AniCS.Desktop.Services.DownloadResult.Cancelled && active.State == AniCS.Desktop.Services.DownloadState.Cancelled)
                                {
                                    AniCS.Desktop.Services.DownloadManager.CleanupPartialFiles(defaultDir, safeTitle, episodeNumStr);
                                }

                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    if (active.State == AniCS.Desktop.Services.DownloadState.Downloading || convertedResult == AniCS.Desktop.Services.DownloadResult.Success || convertedResult == AniCS.Desktop.Services.DownloadResult.Error)
                                    {
                                        if (convertedResult == AniCS.Desktop.Services.DownloadResult.Success)
                                            active.State = AniCS.Desktop.Services.DownloadState.Completed;
                                        else if (convertedResult == AniCS.Desktop.Services.DownloadResult.Error)
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

    private async void PlayEpisodeWithQuickControl(DownloadedAnime anime, DownloadedEpisode episode)
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

        // Detener cualquier reproducción de audio global/secundaria en segundo plano
        AniCS.Desktop.Services.DesktopPlayer.StopAudio();

        // Crear un backend fresco según la configuración actual del usuario
        var currentBackend = PlayerFactory.CreateFromConfig();
        // Reasignar eventos de sesión al nuevo backend
        _playerBackend.SessionChanged -= OnPlayerSessionChanged;
        _playerBackend = currentBackend;
        _playerBackend.SessionChanged += OnPlayerSessionChanged;

        if (currentBackend is LibVlcBackend libVlcForPlay)
        {
            // Para archivos locales el resolver simplemente devuelve la ruta fija;
            // el auto-recover no aplica, pero el constructor lo requiere.
            var filePath = episode.FilePath;
            Func<System.Threading.Tasks.Task<string>> localResolver =
                () => System.Threading.Tasks.Task.FromResult(filePath);

            var playerWindow = new PlayerWindow(
                libVlcForPlay,
                localResolver,
                $"AniCS - {anime.Title} - {episode.EpisodeTitle}",
                "",
                "Mejor");
            var ownerWin = TopLevel.GetTopLevel(this) as Window;
            if (ownerWin != null)
                playerWindow.Show(ownerWin);
            else
                playerWindow.Show();
        }
        else
        {
            _ = currentBackend.PlayAsync(episode.FilePath, $"AniCS - {anime.Title} - {episode.EpisodeTitle}");
        }
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
