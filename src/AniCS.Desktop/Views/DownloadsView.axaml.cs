using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using System.Linq;
using AniCS.Desktop.Services;
using AniCS.Models;

namespace AniCS.Desktop.Views;

public partial class DownloadsView : UserControl
{
    public DownloadsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void OnReloadClicked(object? sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private void LoadData()
    {
        var downloads = DownloadManager.GetAll();
        if (downloads.Count == 0)
        {
            StatusText.IsVisible = true;
            AnimeList.ItemsSource = null;
        }
        else
        {
            StatusText.IsVisible = false;
            AnimeList.ItemsSource = downloads;
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
            LoadData();
        }
    }

    private void OnPlayEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is DownloadedEpisode episode)
        {
            // Find parent anime
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
                LoadData();
            }
        }
    }
}
