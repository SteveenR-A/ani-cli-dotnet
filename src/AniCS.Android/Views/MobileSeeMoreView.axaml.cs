using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Models;
using AniCS.Desktop.Services;
using Button = Avalonia.Controls.Button;

namespace AniCS.Android.Views;

public partial class MobileSeeMoreView : UserControl
{
    public MobileSeeMoreView()
    {
        InitializeComponent();
    }

    public MobileSeeMoreView(string title, IEnumerable<AnimeResult> items)
    {
        InitializeComponent();
        CategoryTitle.Text = title;
        AnimeItemsControl.ItemsSource = items;
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        NavigationHelper.GoBack(this);
    }

    private void OnAnimeCardClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is AnimeResult anime)
        {
            AndroidMainView.Current?.NavigateToAnimeDetails(anime);
        }
    }
}
