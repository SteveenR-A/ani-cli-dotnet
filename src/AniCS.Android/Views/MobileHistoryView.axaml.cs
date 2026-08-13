using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AniCS.Models;
using AniCS.History;
using AniCS.Desktop.Services;
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
        var history = new WatchHistory();
        var entries = history.GetAll();
        EmptyHistoryText.IsVisible = entries == null || entries.Count == 0;
        HistoryItemsControl.ItemsSource = entries;
    }

    private void OnClearHistoryClicked(object? sender, RoutedEventArgs e)
    {
        _history.Clear();
        LoadHistory();
    }

    private void OnHistoryCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is WatchEntry entry)
        {
            NavigateToEntry(entry);
        }
    }

    private void OnPlayHistoryClicked(object? sender, RoutedEventArgs e)
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
                ThumbnailUrl = entry.AnimeThumbnailUrl ?? ""
            };
            AndroidMainView.Current?.NavigateToAnimeDetails(anime);
        }
    }
}
