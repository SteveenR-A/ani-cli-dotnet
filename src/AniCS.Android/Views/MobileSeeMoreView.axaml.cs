using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using AniCS.Models;
using AniCS.Desktop.Services;

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

    private void OnAnimeCardTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control ctrl && ctrl.DataContext is AnimeResult anime)
        {
            AndroidMainView.Current?.NavigateToAnimeDetails(anime);
        }
    }
}
