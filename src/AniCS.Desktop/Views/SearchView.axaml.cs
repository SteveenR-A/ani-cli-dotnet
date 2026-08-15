using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AniCS.Extractors;
using AniCS.Models;

namespace AniCS.Desktop.Views;

public partial class SearchView : UserControl
{
    private SearchFilters _currentFilters = new();
    private int _currentPage = 1;
    private int _totalPages = 1;
    private int _totalItems = 0;

    public SearchView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        ReloadConfig();
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await LoadGenresAsync();
    }

    public void ReloadConfig()
    {
        bool isDonghua = AniCS.ConfigManager.Current.ContentType == "Donghua";
        
        var filterBy = this.FindControl<StackPanel>("FilterByPanel");
        var letter = this.FindControl<StackPanel>("LetterPanel");
        var demo = this.FindControl<StackPanel>("DemoPanel");
        var cat = this.FindControl<StackPanel>("CategoryPanel");
        var type = this.FindControl<StackPanel>("TypePanel");
        var status = this.FindControl<StackPanel>("StatusPanel");
        var year = this.FindControl<StackPanel>("YearPanel");
        var season = this.FindControl<StackPanel>("SeasonPanel");
        var order = this.FindControl<StackPanel>("OrderPanel");
        
        if (filterBy != null) filterBy.IsVisible = !isDonghua;
        if (letter != null) letter.IsVisible = !isDonghua;
        if (demo != null) demo.IsVisible = !isDonghua;
        if (cat != null) cat.IsVisible = !isDonghua;
        if (type != null) type.IsVisible = !isDonghua;
        if (status != null) status.IsVisible = !isDonghua;
        if (year != null) year.IsVisible = !isDonghua;
        if (season != null) season.IsVisible = !isDonghua;
        if (order != null) order.IsVisible = !isDonghua;
        
        // Limpiar la lista al cambiar de modo para evitar resultados viejos
        AnimeList.ItemsSource = null;
        StatusText.IsVisible = false;
        if (PaginationPanel != null) PaginationPanel.IsVisible = false;

        _ = LoadGenresAsync();
    }

    private async Task LoadGenresAsync()
    {
        try
        {
            var extractor = ExtractorFactory.GetExtractor();
            var genres = await extractor.GetGenresAsync();

            Dispatcher.UIThread.Post(() =>
            {
                if (GenreCombo == null) return;
                
                var selectedTag = (GenreCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
                GenreCombo.Items.Clear();

                var defaultItem = new ComboBoxItem { Content = "Sin género", Tag = "" };
                GenreCombo.Items.Add(defaultItem);
                int selectIdx = 0;

                for (int i = 0; i < genres.Count; i++)
                {
                    var g = genres[i];
                    var item = new ComboBoxItem { Content = g.Name, Tag = g.Slug };
                    GenreCombo.Items.Add(item);
                    if (!string.IsNullOrEmpty(selectedTag) && selectedTag.Equals(g.Slug, StringComparison.OrdinalIgnoreCase))
                    {
                        selectIdx = i + 1;
                    }
                }

                GenreCombo.SelectedIndex = selectIdx;
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("SearchView.LoadGenresAsync", ex);
        }
    }

    private void OnSearchBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            OnSearchClicked(sender, new RoutedEventArgs());
        }
    }

    private void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        _currentPage = 1;
        _currentFilters = new SearchFilters
        {
            Query = SearchBox.Text ?? string.Empty,
            FilterBy = (FilterByCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Genre = (GenreCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Letter = (LetterCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Demographic = (DemographicCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Category = (CategoryCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Type = (TypeCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Status = (StatusCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Year = (YearCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Season = (SeasonCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Order = (OrderCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty,
            Page = _currentPage
        };

        _ = ExecuteSearchAsync();
    }

    private async Task ExecuteSearchAsync()
    {
        SearchButton.IsEnabled = false;
        SearchButton.Content = "Buscando...";
        StatusText.Text = $"Cargando página {_currentPage}...";
        StatusText.IsVisible = true;
        AnimeList.ItemsSource = null;
        PaginationPanel.IsVisible = false;

        _currentFilters.Page = _currentPage;
        var extractor = ExtractorFactory.GetExtractor();

        try
        {
            var pageResult = await extractor.GetDirectoryPageAsync(_currentFilters);
            
            Dispatcher.UIThread.Invoke(() =>
            {
                _currentPage = pageResult.CurrentPage;
                _totalPages = Math.Max(1, pageResult.TotalPages);
                _totalItems = pageResult.TotalItems;

                if (pageResult.Results.Count > 0)
                {
                    StatusText.IsVisible = false;
                    AnimeList.ItemsSource = pageResult.Results;
                    UpdatePaginationUi();
                }
                else
                {
                    StatusText.Text = "No se encontraron resultados.";
                    PaginationPanel.IsVisible = false;
                }

                SearchButton.IsEnabled = true;
                SearchButton.Content = "Buscar / Filtrar";
                MainScroll.Offset = new Avalonia.Vector(0, 0);
            });
        }
        catch (HttpRequestException)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                StatusText.Text = "Sin conexión a Internet. Verifica tu red.";
                SearchButton.IsEnabled = true;
                SearchButton.Content = "Buscar / Filtrar";
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                StatusText.Text = $"Error: {ex.Message}";
                SearchButton.IsEnabled = true;
                SearchButton.Content = "Buscar / Filtrar";
            });
        }
    }

    private void UpdatePaginationUi()
    {
        if (_totalPages > 1)
        {
            PaginationPanel.IsVisible = true;
            PageInfoText.Text = _totalItems > 0 
                ? $"Página {_currentPage} de {_totalPages} ({_totalItems} animes)" 
                : $"Página {_currentPage} de {_totalPages}";

            FirstPageBtn.IsEnabled = _currentPage > 1;
            PrevPageBtn.IsEnabled = _currentPage > 1;
            NextPageBtn.IsEnabled = _currentPage < _totalPages;
            LastPageBtn.IsEnabled = _currentPage < _totalPages;
            if (PageJumpInput != null)
            {
                PageJumpInput.Text = _currentPage.ToString();
            }
        }
        else
        {
            PaginationPanel.IsVisible = false;
        }
    }

    private void OnFirstPageClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage = 1;
            _ = ExecuteSearchAsync();
        }
    }

    private void OnPrevPageClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            _ = ExecuteSearchAsync();
        }
    }

    private void OnNextPageClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            _ = ExecuteSearchAsync();
        }
    }

    private void OnLastPageClicked(object? sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage = _totalPages;
            _ = ExecuteSearchAsync();
        }
    }

    private void OnPageJumpClicked(object? sender, RoutedEventArgs e)
    {
        ExecutePageJump();
    }

    private void OnPageJumpKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            ExecutePageJump();
        }
    }

    private void ExecutePageJump()
    {
        if (int.TryParse(PageJumpInput.Text?.Trim(), out int targetPage))
        {
            targetPage = Math.Clamp(targetPage, 1, _totalPages);
            if (targetPage != _currentPage)
            {
                _currentPage = targetPage;
                _ = ExecuteSearchAsync();
            }
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            ScrollTopButton.IsVisible = scrollViewer.Offset.Y > 200;
        }
    }

    private void OnScrollTopClicked(object? sender, RoutedEventArgs e)
    {
        MainScroll.Offset = new Avalonia.Vector(0, 0);
    }
}
