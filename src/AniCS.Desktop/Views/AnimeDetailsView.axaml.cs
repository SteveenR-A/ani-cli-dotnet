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
        
        if (string.IsNullOrEmpty(_anime.ThumbnailUrl))
        {
            try
            {
                var thumb = await extractor.GetThumbnailAsync(_anime.Url);
                if (!string.IsNullOrEmpty(thumb))
                {
                    Dispatcher.UIThread.Invoke(() => {
                        _anime.ThumbnailUrl = thumb;
                        AniCS.Desktop.Converters.AsyncImageLoader.SetSourceUrl(CoverImage, thumb);
                    });
                }
            }
            catch { }
        }

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
    private async void OnDownloadEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is Episode episode)
        {
            if (AniCS.Desktop.Services.DownloadManager.IsEpisodeDownloaded(_anime.Url, episode.EpisodeNumber))
            {
                StatusText.Text = "Este episodio ya está descargado.";
                StatusText.IsVisible = true;
                return;
            }

            StatusText.Text = $"Preparando descarga: {episode.Title}...";
            StatusText.IsVisible = true;
            btn.IsEnabled = false;
            btn.Content = "⏳ Descargando...";

            try
            {
                var extractor = new JKAnimeExtractor(_httpClient);
                var servers = await extractor.GetVideoServersAsync(episode.Url);
                
                if (servers.Count > 0)
                {
                    var server = servers.Find(s => s.IsDirectPlaySupported) ?? servers[0];
                    var videoUrl = await extractor.ResolveVideoUrlAsync(server.Url);

                    if (!string.IsNullOrEmpty(videoUrl))
                    {
                        StatusText.IsVisible = false;
                        var defaultDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "AniCS");
                        
                        // Run in background without awaiting the UI thread
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            await AniCS.Desktop.Services.DesktopPlayer.DownloadAsync(videoUrl, _anime, episode, defaultDir, server.Url);
                            Dispatcher.UIThread.Invoke(() => {
                                btn.Content = "✅ Descargado";
                            });
                        });
                    }
                    else
                    {
                        StatusText.Text = "Error: No se pudo extraer el video para descargar.";
                        btn.IsEnabled = true;
                        btn.Content = "📥 Descargar";
                    }
                }
                else
                {
                    StatusText.Text = "No se encontraron servidores de video.";
                    btn.IsEnabled = true;
                    btn.Content = "📥 Descargar";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error: {ex.Message}";
                btn.IsEnabled = true;
                btn.Content = "📥 Descargar";
            }
        }
    }
}
