using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.History;
using AniCS.Models;

namespace AniCS.Desktop.Views;

public partial class HistoryView : UserControl
{
    private readonly WatchHistory _history;

    public HistoryView()
    {
        InitializeComponent();
        _history = new WatchHistory();
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

    public void Reload() => LoadData();

    private void LoadData()
    {
        // Re-instantiate to load latest from disk
        var history = new WatchHistory();
        var entries = history.GetAll();
        
        if (entries.Count == 0)
        {
            StatusText.IsVisible = true;
            HistoryList.ItemsSource = null;
        }
        else
        {
            StatusText.IsVisible = false;
            HistoryList.ItemsSource = entries;
        }
    }

    private void OnGoToAnimeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchEntry entry)
        {
            if (TopLevel.GetTopLevel(this) is MainWindow mainWindow)
            {
                mainWindow.NavigateToAnimeDetails(new AnimeResult
                {
                    Title = entry.AnimeTitle,
                    Url = entry.AnimeUrl,
                    ThumbnailUrl = entry.AnimeThumbnailUrl
                });
            }
        }
    }

    private void OnClearClicked(object? sender, RoutedEventArgs e)
    {
        var history = new WatchHistory();
        history.Clear();
        LoadData();
    }

    private void OnDeleteEntryClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is WatchEntry entry)
        {
            var history = new WatchHistory();
            history.RemoveEntry(entry.AnimeUrl);
            LoadData();
        }
    }
}
