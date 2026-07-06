using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Net.Http;
using AniCS.Extractors;
using AniCS.Models;
using System.Collections.Generic;
using System.Linq;

namespace AniCS.Desktop.Views;

public partial class CalendarView : UserControl
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public CalendarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DayList.ItemsSource == null)
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
        StatusText.Text = "Cargando calendario...";
        StatusText.IsVisible = true;
        DayList.ItemsSource = null;

        var extractor = new JKAnimeExtractor(_httpClient);
        
        try
        {
            var scoop = await extractor.GetWeeklyScoopAsync();
            
            var grouped = new Dictionary<string, List<AnimeResult>>();
            
            foreach (var item in scoop)
            {
                if (!grouped.ContainsKey(item.Day))
                    grouped[item.Day] = new List<AnimeResult>();
                
                grouped[item.Day].Add(new AnimeResult
                {
                    Title = item.Title,
                    Description = item.Day,
                    ThumbnailUrl = item.ThumbnailUrl,
                    Url = item.Url
                });
            }
            
            var orderedDays = new[] { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };
            var sortedList = grouped.OrderBy(x => 
            {
                var idx = System.Array.IndexOf(orderedDays, x.Key);
                return idx == -1 ? 99 : idx;
            }).Select(x => new CalendarDayGroup { DayName = x.Key, Animes = x.Value }).ToList();

            Dispatcher.UIThread.Invoke(() => {
                if (sortedList.Count > 0)
                {
                    StatusText.IsVisible = false;
                    DayList.ItemsSource = sortedList;
                }
                else
                {
                    StatusText.Text = "No se encontró información del calendario.";
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

public class CalendarDayGroup
{
    public string DayName { get; set; } = "";
    public List<AnimeResult> Animes { get; set; } = new();
}
