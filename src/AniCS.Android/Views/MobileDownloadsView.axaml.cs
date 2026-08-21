using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AniCS.Desktop.Services;
using AniCS.Models;
using AniCS.Player;
using Button = Avalonia.Controls.Button;
using DownloadManager = AniCS.Desktop.Services.DownloadManager;

namespace AniCS.Android.Views;

public partial class MobileDownloadsView : UserControl
{
    private enum DownloadFilter { All, Unwatched, InProgress, Completed }
    private DownloadFilter _currentFilter = DownloadFilter.All;

    public MobileDownloadsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged -= OnDownloadsChanged;
        DownloadManager.DownloadsChanged += OnDownloadsChanged;

        // Cargar inmediatamente desde memoria
        LoadDownloads();

        // Escanear disco en segundo plano
        _ = Task.Run(() =>
        {
            DownloadManager.ScanDiskDownloads();
        });
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged -= OnDownloadsChanged;
    }

    private void OnDownloadsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(LoadDownloads);
    }

    private async void OnReloadClicked(object? sender, RoutedEventArgs e)
    {
        await Task.Run(() => DownloadManager.ScanDiskDownloads());
        LoadDownloads();
    }

    public void LoadDownloads()
    {
        var active = DownloadManager.ActiveDownloads;
        ActiveDownloadsSection.IsVisible = active.Count > 0;
        ActiveDownloadsList.ItemsSource = active;

        var allDownloads = DownloadManager.GetAll().ToList();
        List<DownloadedAnime> filteredList;

        if (_currentFilter == DownloadFilter.All)
        {
            filteredList = allDownloads;
        }
        else
        {
            var targetStatus = _currentFilter switch
            {
                DownloadFilter.Unwatched => EpisodeWatchStatus.Unwatched,
                DownloadFilter.InProgress => EpisodeWatchStatus.InProgress,
                DownloadFilter.Completed => EpisodeWatchStatus.Completed,
                _ => EpisodeWatchStatus.Unwatched
            };

            filteredList = allDownloads
                .Select(a => new DownloadedAnime
                {
                    Title = a.Title,
                    Url = a.Url,
                    ThumbnailUrl = a.ThumbnailUrl,
                    Episodes = a.Episodes.Where(ep => ep.Status == targetStatus).ToList()
                })
                .Where(a => a.Episodes.Count > 0)
                .ToList();
        }

        if (filteredList.Count > 0)
        {
            EmptyDownloadsText.IsVisible = false;
            DownloadedAnimeList.IsVisible = true;

            if (!AreDownloadsListsIdentical(DownloadedAnimeList.ItemsSource as IEnumerable<DownloadedAnime>, filteredList))
            {
                var scrollOffset = MobileDownloadsScrollViewer?.Offset ?? default;

                if (DownloadedAnimeList.ItemsSource is IEnumerable<DownloadedAnime> existing)
                {
                    var map = existing.ToDictionary(a => a.Url, a => a.IsExpanded);
                    foreach (var d in filteredList)
                    {
                        if (map.TryGetValue(d.Url, out bool expanded))
                        {
                            d.IsExpanded = expanded;
                        }
                    }
                }

                DownloadedAnimeList.ItemsSource = filteredList;

                if (scrollOffset != default)
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (MobileDownloadsScrollViewer != null)
                        {
                            MobileDownloadsScrollViewer.Offset = scrollOffset;
                        }
                    }, DispatcherPriority.Loaded);
                }
            }
        }
        else
        {
            EmptyDownloadsText.IsVisible = true;
            EmptyDownloadsText.Text = _currentFilter == DownloadFilter.All
                ? "No hay animes descargados."
                : "No hay episodios en esta categoría.";
            DownloadedAnimeList.IsVisible = false;
            DownloadedAnimeList.ItemsSource = null;
        }
    }

    private static bool AreDownloadsListsIdentical(IEnumerable<DownloadedAnime>? current, List<DownloadedAnime> updated)
    {
        if (current == null) return false;
        var curList = current as IReadOnlyList<DownloadedAnime> ?? current.ToList();

        if (curList.Count != updated.Count) return false;

        for (int i = 0; i < curList.Count; i++)
        {
            var a = curList[i];
            var b = updated[i];
            if (a.Url != b.Url || a.Title != b.Title || a.Episodes.Count != b.Episodes.Count)
                return false;

            for (int j = 0; j < a.Episodes.Count; j++)
            {
                if (a.Episodes[j].EpisodeNumber != b.Episodes[j].EpisodeNumber ||
                    a.Episodes[j].FilePath != b.Episodes[j].FilePath)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void OnFilterAllClicked(object? sender, RoutedEventArgs e) => SetFilter(DownloadFilter.All, FilterAllBtn);
    private void OnFilterUnwatchedClicked(object? sender, RoutedEventArgs e) => SetFilter(DownloadFilter.Unwatched, FilterUnwatchedBtn);
    private void OnFilterInProgressClicked(object? sender, RoutedEventArgs e) => SetFilter(DownloadFilter.InProgress, FilterInProgressBtn);
    private void OnFilterCompletedClicked(object? sender, RoutedEventArgs e) => SetFilter(DownloadFilter.Completed, FilterCompletedBtn);

    private void SetFilter(DownloadFilter filter, Button activeBtn)
    {
        _currentFilter = filter;

        var primaryBrush = Avalonia.Application.Current?.Resources["AppPrimaryColor"] as IBrush ?? Brushes.Purple;
        var subtextBrush = Avalonia.Application.Current?.Resources["AppSubtextColor"] as IBrush ?? Brushes.Gray;

        FilterAllBtn.Background = Brushes.Transparent;
        FilterAllBtn.Foreground = subtextBrush;
        FilterUnwatchedBtn.Background = Brushes.Transparent;
        FilterUnwatchedBtn.Foreground = subtextBrush;
        FilterInProgressBtn.Background = Brushes.Transparent;
        FilterInProgressBtn.Foreground = subtextBrush;
        FilterCompletedBtn.Background = Brushes.Transparent;
        FilterCompletedBtn.Foreground = subtextBrush;

        activeBtn.Background = primaryBrush;
        activeBtn.Foreground = Brushes.White;

        LoadDownloads();
    }

    private void OnToggleStatusClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            var anime = DownloadManager.GetAll().FirstOrDefault(a => a.Episodes.Any(ep => ep.FilePath == episode.FilePath));
            if (anime != null)
            {
                var nextStatus = episode.Status switch
                {
                    EpisodeWatchStatus.Unwatched => EpisodeWatchStatus.InProgress,
                    EpisodeWatchStatus.InProgress => EpisodeWatchStatus.Completed,
                    EpisodeWatchStatus.Completed  => EpisodeWatchStatus.Unwatched,
                    _ => EpisodeWatchStatus.Unwatched
                };

                DownloadManager.UpdateEpisodeStatus(anime.Url, episode.EpisodeNumber, nextStatus);
            }
        }
    }

    private void OnPauseResumeActiveDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActiveDownload active)
        {
            if (active.State == DownloadState.Downloading)
            {
                active.State = DownloadState.Paused;
                active.CancellationTokenSource?.Cancel();
            }
            else if (active.State == DownloadState.Paused || active.State == DownloadState.Error)
            {
                active.RetryAttempt = 0;
                DownloadManager.StartOrResumeDownloadAsync(active);
            }
        }
    }

    private void OnCancelActiveDownloadClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is ActiveDownload active)
        {
            active.State = DownloadState.Cancelled;
            active.CancellationTokenSource?.Cancel();

            var defaultDir = Path.Combine(ConfigManager.BaseDataPath, "Downloads");
            var safeTitle = string.Join("_", active.AnimeTitle.Split(Path.GetInvalidFileNameChars())).Trim();
            var episodeNumStr = string.IsNullOrWhiteSpace(active.EpisodeNumber) ? "Desconocido" : active.EpisodeNumber;
            DownloadManager.CleanupPartialFiles(defaultDir, safeTitle, episodeNumStr);
            DownloadManager.RemoveActiveDownload(active);
        }
    }

    private void OnPlayEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            if (!File.Exists(episode.FilePath))
            {
                LoadDownloads();
                return;
            }

            var anime = DownloadManager.GetAll().FirstOrDefault(a => a.Episodes.Any(ep => ep.FilePath == episode.FilePath));
            var title = anime != null ? $"{anime.Title} — {episode.EpisodeTitle}" : episode.EpisodeTitle;

            MobileVideoPlayerView? playerView = null;

            Func<Task>? buildPrevAction(DownloadedEpisode currentEp)
            {
                if (anime == null) return null;
                var prev = DownloadManager.GetPreviousEpisode(anime, currentEp);
                if (prev == null || !File.Exists(prev.FilePath)) return null;

                return async () =>
                {
                    var pAction = buildPrevAction(prev);
                    var nAction = buildNextAction(prev);
                    if (playerView != null)
                    {
                        await playerView.ChangeEpisodeAsync(
                            () => Task.FromResult(prev.FilePath),
                            $"{anime.Title} — {prev.EpisodeTitle}",
                            prev.FilePath,
                            "Descargado (Local)",
                            pAction,
                            nAction);
                    }
                };
            }

            Func<Task>? buildNextAction(DownloadedEpisode currentEp)
            {
                if (anime == null) return null;
                var next = DownloadManager.GetNextEpisode(anime, currentEp);
                if (next == null || !File.Exists(next.FilePath)) return null;

                return async () =>
                {
                    var pAction = buildPrevAction(next);
                    var nAction = buildNextAction(next);
                    if (playerView != null)
                    {
                        await playerView.ChangeEpisodeAsync(
                            () => Task.FromResult(next.FilePath),
                            $"{anime.Title} — {next.EpisodeTitle}",
                            next.FilePath,
                            "Descargado (Local)",
                            pAction,
                            nAction);
                    }
                };
            }

            playerView = new MobileVideoPlayerView(
                () => Task.FromResult(episode.FilePath),
                title,
                episode.FilePath,
                "Descargado (Local)",
                animeTitle: anime?.Title ?? "",
                animeUrl: anime?.Url ?? "",
                thumbnailUrl: anime?.ThumbnailUrl ?? "",
                episodeNumber: episode.EpisodeNumber,
                episodeUrl: episode.FilePath,
                prevEpisodeAction: buildPrevAction(episode),
                nextEpisodeAction: buildNextAction(episode));

            AndroidMainView.Current?.PushPlayerView(playerView);
        }
    }

    private void OnDeleteEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            var anime = DownloadManager.GetAll().FirstOrDefault(a => a.Episodes.Any(ep => ep.FilePath == episode.FilePath));
            if (anime != null)
            {
                DownloadManager.DeleteEpisode(anime.Url, episode.EpisodeNumber);
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

    private void OnDownloadAnimeImageClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedAnime anime && !string.IsNullOrEmpty(anime.ThumbnailUrl))
        {
            AndroidMainView.Current?.ShowImageModal(anime.ThumbnailUrl, anime.Title);
        }
    }

    private void OnGoToAnimeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedAnime anime)
        {
            var animeResult = new AnimeResult
            {
                Title = anime.Title,
                Url = !string.IsNullOrEmpty(anime.Url) ? anime.Url : anime.Title,
                ThumbnailUrl = anime.ThumbnailUrl
            };
            AndroidMainView.Current?.NavigateToAnimeDetails(animeResult);
        }
    }
}
