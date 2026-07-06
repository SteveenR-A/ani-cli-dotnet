using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Extractors;
using System.Net.Http;
using Avalonia.Threading;

namespace AniCS.Desktop.Views;

public partial class SearchView : UserControl
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public SearchView()
    {
        InitializeComponent();
    }

    private async void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        var query = SearchBox.Text;
        if (string.IsNullOrWhiteSpace(query)) return;

        SearchButton.IsEnabled = false;
        SearchButton.Content = "Buscando...";
        StatusText.Text = $"Buscando resultados para '{query}'...";
        StatusText.IsVisible = true;
        AnimeList.ItemsSource = null;

        var extractor = new JKAnimeExtractor(_httpClient);
        
        try
        {
            var results = await extractor.SearchAsync(query);
            
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
                SearchButton.Content = "Buscar";
            });
        }
        catch (System.Exception ex)
        {
            Dispatcher.UIThread.Invoke(() => {
                StatusText.Text = $"Error: {ex.Message}";
                SearchButton.IsEnabled = true;
                SearchButton.Content = "Buscar";
            });
        }
    }
}
