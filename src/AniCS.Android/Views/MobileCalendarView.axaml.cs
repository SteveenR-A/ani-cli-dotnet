using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using AniCS.Extractors;
using AniCS.Models;
using Button = Avalonia.Controls.Button;

namespace AniCS.Android.Views;

public partial class MobileCalendarView : UserControl
{
    private readonly Dictionary<string, List<AnimeResult>> _groupedAnimes = new();
    private string _selectedDay = "";
    private readonly string[] _daysOfWeek = { "Lunes", "Martes", "Miércoles", "Jueves", "Viernes", "Sábado", "Domingo" };

    public MobileCalendarView()
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

            Dispatcher.UIThread.Invoke(() =>
            {
                BuildDaySelector();
                StatusText.IsVisible = false;
                ReloadButton.IsEnabled = true;
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("MobileCalendarView.LoadData", ex);
            Dispatcher.UIThread.Invoke(() =>
            {
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
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            if (isToday)
                _selectedDay = day;

            UpdateDayButtonStyle(btn, day == _selectedDay);

            var sp = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
            sp.Children.Add(new TextBlock { Text = day, VerticalAlignment = VerticalAlignment.Center });

            if (isToday)
            {
                var badge = new Border
                {
                    Background = SolidColorBrush.Parse("#FF9800"),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1),
                    Child = new TextBlock { Text = "HOY", FontSize = 9, FontWeight = FontWeight.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }
                };
                sp.Children.Add(badge);
            }

            btn.Content = sp;
            btn.Click += (s, e) =>
            {
                _selectedDay = day;
                foreach (Control child in DaysPanel.Children)
                {
                    if (child is Button b) UpdateDayButtonStyle(b, false);
                }
                UpdateDayButtonStyle(btn, true);
                DisplayDayAnimes();
            };

            DaysPanel.Children.Add(btn);
        }

        DisplayDayAnimes();
    }

    private void UpdateDayButtonStyle(Button btn, bool isSelected)
    {
        var primaryBrush = this.TryFindResource("AppPrimaryColor", out var pRes) && pRes is IBrush pb ? pb : SolidColorBrush.Parse("#4A90E2");
        var surfaceBrush = this.TryFindResource("AppSurfaceColor", out var sRes) && sRes is IBrush sb ? sb : SolidColorBrush.Parse("#2A2A40");
        var subtextBrush = this.TryFindResource("AppSubtextColor", out var subRes) && subRes is IBrush stb ? stb : SolidColorBrush.Parse("#A0A0B0");

        if (isSelected)
        {
            btn.Background = primaryBrush;
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = surfaceBrush;
            btn.Foreground = subtextBrush;
        }
    }

    private void DisplayDayAnimes()
    {
        if (string.IsNullOrEmpty(_selectedDay)) return;

        CurrentDayTitleText.Text = _selectedDay;

        if (!_groupedAnimes.TryGetValue(_selectedDay, out var animes))
        {
            animes = new List<AnimeResult>();
        }

        DayItemsList.ItemsSource = animes;

        if (animes.Count == 0)
        {
            StatusText.Text = $"No hay animes para el {_selectedDay}.";
            StatusText.IsVisible = true;
        }
        else
        {
            StatusText.IsVisible = false;
        }
    }
}
