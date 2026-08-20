using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AniCS.Extractors;
using AniCS.Models;

namespace AniCS.Android.Views;

public partial class MobileTopAnimesView : UserControl
{
    public MobileTopAnimesView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _ = LoadTopAnimesAsync();
    }

    public void ReloadConfig()
    {
        _ = LoadTopAnimesAsync();
    }

    private async Task LoadTopAnimesAsync()
    {
        StatusText.IsVisible = true;
        StatusText.Text = "Cargando top animes...";
        TopAnimesItemsControl.ItemsSource = null;

        try
        {
            var extractor = ExtractorFactory.GetExtractor();
            var cacheKey = $"Top_{extractor.Domain}_mostpopular_1";
            var topList = await DataCache.GetOrFetchDataAsync(cacheKey, TimeSpan.FromMinutes(30),
                async () => await extractor.GetTopAnimesAsync("most-popular", "", 1));

            Dispatcher.UIThread.Invoke(() =>
            {
                if (topList != null && topList.Count > 0)
                {
                    StatusText.IsVisible = false;
                    TopAnimesItemsControl.ItemsSource = topList;
                }
                else
                {
                    StatusText.IsVisible = true;
                    StatusText.Text = "No se encontraron animes en el top.";
                }
            });
        }
        catch (System.Net.Http.HttpRequestException)
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                StatusText.IsVisible = true;
                StatusText.Text = "Sin conexión a Internet. Verifica tu red.";
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("MobileTopAnimesView.LoadTopAnimesAsync", ex);
            Dispatcher.UIThread.Invoke(() =>
            {
                StatusText.IsVisible = true;
                StatusText.Text = $"Error: {ex.Message}";
            });
        }
    }

    private void OnAnimeCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control ctrl && ctrl.DataContext is AnimeResult anime)
        {
            AndroidMainView.Current?.NavigateToAnimeDetails(anime);
        }
    }
}
