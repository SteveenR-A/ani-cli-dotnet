using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AniCS.Extractors;
using AniCS.Models;
using System.Collections.Generic;

namespace AniCS.Desktop.Views;

public partial class TopAnimesView : UserControl
{

    public TopAnimesView()
    {
        InitializeComponent();
        ReloadConfig();
    }

    public void ReloadConfig()
    {
        LoadTopAnimes();
    }

    private void OnFilterChanged(object? sender, Avalonia.Controls.SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            LoadTopAnimes();
        }
    }

    private async void LoadTopAnimes()
    {
        StatusText.IsVisible = true;
        StatusText.Text = "Cargando top...";
        TopList.ItemsSource = null;
        


        var topType = (TopTypeComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

        var extractor = ExtractorFactory.GetExtractor();
        
        try
        {
            var cacheKey = $"Top_{extractor.Domain}_{topType}_1";
            var results = await DataCache.GetOrFetchDataAsync(cacheKey, System.TimeSpan.FromMinutes(30), 
                async () => await extractor.GetTopAnimesAsync(topType, "", 1));
            
            Dispatcher.UIThread.Invoke(() => {
                if (results.Count > 0)
                {
                    StatusText.IsVisible = false;
                    TopList.ItemsSource = results;
                }
                else
                {
                    StatusText.IsVisible = true;
                    StatusText.Text = "No hay resultados.";
                }
            });
        }
        catch (System.Net.Http.HttpRequestException)
        {
            Dispatcher.UIThread.Invoke(() => {
                StatusText.IsVisible = true;
                StatusText.Text = "Sin conexión a Internet. Verifica tu red.";
            });
        }
        catch (System.Exception ex)
        {
            Dispatcher.UIThread.Invoke(() => {
                StatusText.IsVisible = true;
                StatusText.Text = $"Error: {ex.Message}";
            });
        }
    }
}
