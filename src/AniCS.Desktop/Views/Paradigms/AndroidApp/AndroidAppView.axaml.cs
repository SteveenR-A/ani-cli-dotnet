using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Desktop.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AniCS.Models;

namespace AniCS.Desktop.Views.Paradigms.AndroidApp;

public partial class AndroidAppView : UserControl
{
    public AndroidAppView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // ── DataContext wiring ────────────────────────────────────────────────

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(HomeViewModel.HistoryList))
                    UpdateHistoryEmptyState(vm);
            };
            UpdateHistoryEmptyState(vm);
        }
    }

    private void UpdateHistoryEmptyState(HomeViewModel vm)
    {
        bool isEmpty = vm.HistoryList.Count == 0;
        HistoryEmptyState.IsVisible    = isEmpty;
        HistoryScrollViewer.IsVisible  = !isEmpty;
    }

    // ── SearchBar ─────────────────────────────────────────────────────────

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not HomeViewModel vm) return;
        var query = SearchBox.Text?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(query))
        {
            // Restore originals — ViewModel already holds them
            LatestItemsControl.ItemsSource    = vm.LatestList;
            PremieresItemsControl.ItemsSource = vm.PremieresList;
            return;
        }

        // Filter both lists by title
        LatestItemsControl.ItemsSource = new ObservableCollection<AnimeResult>(
            vm.LatestList.Where(a => a.Title.Contains(query, System.StringComparison.OrdinalIgnoreCase)));

        PremieresItemsControl.ItemsSource = new ObservableCollection<AnimeResult>(
            vm.PremieresList.Where(a => a.Title.Contains(query, System.StringComparison.OrdinalIgnoreCase)));
    }

    // ── "Ver más" handlers ────────────────────────────────────────────────

    private void OnSeeMoreLatestClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
            NavigateToSeeMore("Emisión Reciente", vm.LatestList);
    }

    private void OnSeeMorePremieresClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
            NavigateToSeeMore("Estrenos / Destacados", vm.PremieresList);
    }

    private void OnSeeMoreHistoryClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
            NavigateToSeeMore("Seguir Viendo", vm.HistoryList);
    }

    private void NavigateToSeeMore(string title, IEnumerable<AnimeResult> items)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window is MainWindow mainWindow)
            mainWindow.NavigateToSeeMore(title, items);
    }

    // ── Bottom Navigation Bar ─────────────────────────────────────────────

    private void OnNavHomeClicked(object? sender, RoutedEventArgs e)
    {
        // Already on home — just scroll to top or clear search
        SearchBox.Text = string.Empty;
    }

    private void OnNavSearchClicked(object? sender, RoutedEventArgs e)
    {
        // Focus the SearchBar
        SearchBox.Focus();
    }

    private void OnNavDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window is MainWindow mainWindow)
            mainWindow.NavigateTo("Downloads");
    }
}
