using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Net.Http;
using AniCS.Extractors;
using AniCS.Models;
using AniCS.Desktop.Converters;
using System;

namespace AniCS.Desktop.Views;

public partial class AnimeDetailsView : UserControl
{
    private readonly AnimeResult _anime;
    private static readonly HttpClient _httpClient = new HttpClient();

    public AnimeDetailsView()
    {
        InitializeComponent();
    }

    public AnimeDetailsView(AnimeResult anime)
    {
        InitializeComponent();
        _anime = anime;
        DataContext = anime;
        TitleText.Text = anime.Title;

        Loaded += OnLoaded;
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window is MainWindow mainWindow)
        {
            mainWindow.GoBack();
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var extractor = new JKAnimeExtractor(_httpClient);
        
        try
        {
            var syn = await extractor.GetSynopsisAsync(_anime.Url);
            Dispatcher.UIThread.Invoke(() => {
                SynopsisText.Text = string.IsNullOrEmpty(syn) ? "Sinopsis no disponible." : syn;
            });
        }
        catch
        {
            Dispatcher.UIThread.Invoke(() => SynopsisText.Text = "Error cargando sinopsis.");
        }

        try
        {
            var episodes = await extractor.GetEpisodesAsync(_anime.Url);
            Dispatcher.UIThread.Invoke(() => {
                if (episodes.Count > 0)
                {
                    StatusText.IsVisible = false;
                    EpisodesList.ItemsSource = episodes;
                }
                else
                {
                    StatusText.Text = "No se encontraron episodios.";
                }
            });
        }
        catch (Exception ex)
        {
            Dispatcher.UIThread.Invoke(() => StatusText.Text = $"Error: {ex.Message}");
        }
    }

    private async void OnEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Episode episode)
        {
            StatusText.Text = $"Resolviendo video: {episode.Title}...";
            StatusText.IsVisible = true;
            btn.IsEnabled = false;

            try
            {
                var extractor = new JKAnimeExtractor(_httpClient);
                var servers = await extractor.GetVideoServersAsync(episode.Url);
                
                if (servers.Count > 0)
                {
                    // Pick best server or fallback
                    var server = servers.Find(s => s.IsDirectPlaySupported) ?? servers[0];
                    var videoUrl = await extractor.ResolveVideoUrlAsync(server.Url);

                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        StatusText.IsVisible = false;
                        AniCS.Desktop.Services.DesktopPlayer.Play(videoUrl, $"AniCS - {_anime.Title} - {episode.Title}", server.Url);
                    }
                    else
                    {
                        StatusText.Text = "Error: No se pudo extraer el video directo.";
                    }
                }
                else
                {
                    StatusText.Text = "No se encontraron servidores de video.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}
