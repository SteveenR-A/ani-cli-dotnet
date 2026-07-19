using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Extractors;
using System.Net.Http;
using Avalonia.Threading;

using AniCS.Models;

namespace AniCS.Desktop.Views;

public partial class SearchView : UserControl
{
    public SearchView()
    {
        InitializeComponent();
        ReloadConfig();
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
    }

    private void OnSearchBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            OnSearchClicked(sender, new RoutedEventArgs());
        }
    }

    private async void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        SearchButton.IsEnabled = false;
        SearchButton.Content = "Buscando...";
        StatusText.Text = "Buscando resultados...";
        StatusText.IsVisible = true;
        AnimeList.ItemsSource = null;

        var extractor = ExtractorFactory.GetExtractor();
        
        var filters = new SearchFilters
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
            Order = (OrderCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? string.Empty
        };

        try
        {
            var results = await extractor.AdvancedSearchAsync(filters);
            
            Dispatcher.UIThread.Invoke(() => {
                if (results.Count > 0)
                {
                    StatusText.IsVisible = false;
                    AnimeList.ItemsSource = results;
                }
                else
                {
                    StatusText.Text = "No se encontraron resultados.";
                }
                SearchButton.IsEnabled = true;
                SearchButton.Content = "Buscar / Filtrar";
            });
        }
        catch (System.Net.Http.HttpRequestException)
        {
            Dispatcher.UIThread.Invoke(() => {
                StatusText.Text = "Sin conexión a Internet. Verifica tu red.";
                SearchButton.IsEnabled = true;
                SearchButton.Content = "Buscar / Filtrar";
            });
        }
        catch (System.Exception ex)
        {
            Dispatcher.UIThread.Invoke(() => {
                StatusText.Text = $"Error: {ex.Message}";
                SearchButton.IsEnabled = true;
                SearchButton.Content = "Buscar / Filtrar";
            });
        }
    }

    private void OnScrollChanged(object? sender, Avalonia.Controls.ScrollChangedEventArgs e)
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
