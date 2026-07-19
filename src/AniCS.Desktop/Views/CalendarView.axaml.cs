using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.Layout;
using System.Net.Http;
using AniCS.Extractors;
using AniCS.Models;
using System.Collections.Generic;
using System.Linq;
using System;

namespace AniCS.Desktop.Views;

public partial class CalendarView : UserControl
{
    private Dictionary<string, List<AnimeResult>> _groupedAnimes = new();
    private string _selectedDay = "";
    private readonly string[] _daysOfWeek = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };

    public CalendarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_groupedAnimes.Count == 0)
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
        StatusText.Text = "Cargando horarios...";
        StatusText.IsVisible = true;
        _groupedAnimes.Clear();
        DayItemsList.ItemsSource = null;
        DaysPanel.Children.Clear();

        var extractor = ExtractorFactory.GetExtractor();
        
        try
        {
            var scoop = await extractor.GetWeeklyScoopAsync();
            
            foreach (var item in scoop)
            {
                if (!_groupedAnimes.ContainsKey(item.Day))
                    _groupedAnimes[item.Day] = new List<AnimeResult>();
                
                _groupedAnimes[item.Day].Add(new AnimeResult
                {
                    Title = item.Title,
                    Description = item.Day,
                    ThumbnailUrl = item.ThumbnailUrl,
                    Url = item.Url
                });
            }
            
            Dispatcher.UIThread.Invoke(() => {
                BuildDaySelector();
                StatusText.IsVisible = false;
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
    
    private void BuildDaySelector()
    {
        DaysPanel.Children.Clear();
        var today = DateTime.Now.DayOfWeek;
        int todayIndex = today == DayOfWeek.Sunday ? 6 : (int)today - 1;

        for (int i = 0; i < _daysOfWeek.Length; i++)
        {
            string day = _daysOfWeek[i];
            bool isToday = (i == todayIndex);

            var btn = new Button
            {
                CornerRadius = new Avalonia.CornerRadius(4),
                Margin = new Avalonia.Thickness(0, 0, 0, 0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };
            
            if (isToday)
                _selectedDay = day; // Select today by default

            UpdateDayButtonStyle(btn, day == _selectedDay);

            var sp = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
            sp.Children.Add(new TextBlock { Text = day, VerticalAlignment = VerticalAlignment.Center });
            
            if (isToday)
            {
                var badge = new Border
                {
                    Background = SolidColorBrush.Parse("#FF9800"), // Orange badge
                    CornerRadius = new Avalonia.CornerRadius(2),
                    Padding = new Avalonia.Thickness(4, 1),
                    Child = new TextBlock { Text = "HOY", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }
                };
                sp.Children.Add(badge);
            }
            
            btn.Content = sp;
            btn.Click += (s, e) => {
                _selectedDay = day;
                foreach(Control child in DaysPanel.Children)
                {
                    if (child is Button b) UpdateDayButtonStyle(b, false);
                }
                UpdateDayButtonStyle(btn, true);
                FilterAndDisplay();
            };
            
            DaysPanel.Children.Add(btn);
        }
        
        FilterAndDisplay();
    }
    
    private void UpdateDayButtonStyle(Button btn, bool isSelected)
    {
        if (isSelected)
        {
            btn.Background = SolidColorBrush.Parse("#4A90E2"); // Active color
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = SolidColorBrush.Parse("#2A2A40"); // Inactive color
            btn.Foreground = SolidColorBrush.Parse("#A0A0B0");
        }
    }
    
    private void OnFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        FilterAndDisplay();
    }
    
    private void FilterAndDisplay()
    {
        if (string.IsNullOrEmpty(_selectedDay)) return;
        
        CurrentDayTitleText.Text = _selectedDay;
        
        if (!_groupedAnimes.TryGetValue(_selectedDay, out var animes))
        {
            animes = new List<AnimeResult>(); // Empty day
        }
        
        var query = FilterTextBox.Text?.ToLowerInvariant() ?? "";
        
        if (string.IsNullOrWhiteSpace(query))
        {
            DayItemsList.ItemsSource = animes;
        }
        else
        {
            DayItemsList.ItemsSource = animes.Where(a => a.Title.ToLowerInvariant().Contains(query)).ToList();
        }
        
        if (animes.Count == 0 && string.IsNullOrWhiteSpace(query))
        {
            StatusText.Text = $"No hay animes para el {_selectedDay}.";
            StatusText.IsVisible = true;
        }
        else if (DayItemsList.ItemsSource is List<AnimeResult> filtered && filtered.Count == 0)
        {
            StatusText.Text = "No se encontraron resultados.";
            StatusText.IsVisible = true;
        }
        else
        {
            StatusText.IsVisible = false;
        }
    }
}
