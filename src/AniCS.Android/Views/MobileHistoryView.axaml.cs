using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AniCS.Models;
using AniCS.History;
using Button = Avalonia.Controls.Button;

namespace AniCS.Android.Views;

public partial class MobileHistoryView : UserControl
{
    private readonly WatchHistory _history;

    public MobileHistoryView()
    {
        InitializeComponent();
        _history = new WatchHistory();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        LoadHistory();
    }

    public void LoadHistory()
    {
        var entries = _history.GetAll();
        EmptyHistoryText.IsVisible = entries.Count == 0;
        HistoryItemsControl.ItemsSource = entries;
    }

    public void OnClearHistoryClicked(object? sender, RoutedEventArgs e)
    {
        _history.Clear();
        LoadHistory();
    }

    private void OnDeleteSingleHistoryClicked(object? sender, RoutedEventArgs _)
    {
        if (sender is Button btn && btn.Tag is WatchEntry entry)
        {
            _history.RemoveEntry(entry.AnimeUrl);
            LoadHistory();
        }
    }

    private void OnHistoryImageTapped(object? sender, TappedEventArgs _)
    {
        if (sender is Control ctrl && (ctrl.Tag is WatchEntry entry || ctrl.DataContext is WatchEntry entryDc && (entry = entryDc) != null) && !string.IsNullOrEmpty(entry.AnimeThumbnailUrl))
        {
            AndroidMainView.Current?.ShowImageModal(entry.AnimeThumbnailUrl, entry.AnimeTitle);
        }
    }

    private void OnHistoryCardTapped(object? sender, TappedEventArgs _)
    {
        if (sender is Control ctrl && (ctrl.Tag is WatchEntry entry || ctrl.DataContext is WatchEntry entryDc && (entry = entryDc) != null))
        {
            NavigateToEntry(entry);
        }
    }

    private void OnPlayHistoryClicked(object? sender, RoutedEventArgs _)
    {
        if (sender is Button btn && btn.DataContext is WatchEntry entry)
        {
            NavigateToEntry(entry);
        }
    }

    private void NavigateToEntry(WatchEntry entry)
    {
        if (!string.IsNullOrEmpty(entry.AnimeUrl))
        {
            var anime = new AnimeResult
            {
                Title = entry.AnimeTitle,
                Url = entry.AnimeUrl,
                ThumbnailUrl = entry.AnimeThumbnailUrl
            };
            AndroidMainView.Current?.NavigateToAnimeDetails(anime);
        }
    }
}
