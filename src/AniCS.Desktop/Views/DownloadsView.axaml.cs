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
            active.Cancel();
            DownloadManager.RemoveActiveDownload(active);
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
