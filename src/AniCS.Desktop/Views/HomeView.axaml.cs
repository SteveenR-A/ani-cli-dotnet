using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Extractors;
using System.Net.Http;
using Avalonia.Threading;

namespace AniCS.Desktop.Views;

public partial class HomeView : UserControl
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public HomeView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Solo cargar si la lista está vacía para no recargar cada vez que cambiamos de vista
        if (AnimeList.ItemsSource == null)
        {
            LoadData();
        }
    }

    private void OnReloadClicked(object? sender, RoutedEventArgs e)
    {
        LoadData();
    }

    private async void LoadData()
    {
        ReloadButton.IsEnabled = false;
        StatusText.Text = "Cargando animes recientes...";
        StatusText.IsVisible = true;
        AnimeList.ItemsSource = null;

        var extractor = new JKAnimeExtractor(_httpClient);
        
        try
        {
            var episodes = await extractor.GetLatestReleasesAsync();
            var results = new System.Collections.Generic.List<AniCS.Models.AnimeResult>();
            
            foreach (var ep in episodes)
            {
                results.Add(new AniCS.Models.AnimeResult
                {
                    Title = ep.Title,
                    Description = $"Episodio {ep.EpisodeNumber}",
                    ThumbnailUrl = ep.ThumbnailUrl,
                    Url = ep.Url
                });
            }
            
            Dispatcher.UIThread.Invoke(() => {
                if (results.Count > 0)
                {
                    StatusText.IsVisible = false;
                    AnimeList.ItemsSource = results;
                }
                else
                {
                    StatusText.Text = "No se encontraron animes. Intenta recargar.";
                }
                ReloadButton.IsEnabled = true;
            });
        }
        catch (System.Exception ex)
        {
            Dispatcher.UIThread.Invoke(() => {
                StatusText.Text = $"Error: {ex.Message}";
                ReloadButton.IsEnabled = true;
            });
        }
    }
}
