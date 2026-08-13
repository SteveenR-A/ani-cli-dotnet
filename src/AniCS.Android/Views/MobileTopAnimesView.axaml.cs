using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AniCS.Extractors;
using AniCS.Models;
using AniCS.Desktop.Services;
using Button = Avalonia.Controls.Button;

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
        try
        {
            var extractor = ExtractorFactory.GetExtractor();
            var topList = await extractor.GetTopAnimesAsync("most-popular", "", 1);
            TopAnimesItemsControl.ItemsSource = topList;
        }
        catch (Exception ex)
        {
            AppLogger.Error("MobileTopAnimesView.LoadTopAnimesAsync", ex);
        }
    }

    private void OnAnimeCardPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is AnimeResult anime)
        {
            AndroidMainView.Current?.NavigateToAnimeDetails(anime);
        }
    }

    private void OnSeeAnimeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AnimeResult anime)
        {
            AndroidMainView.Current?.NavigateToAnimeDetails(anime);
        }
    }
}
