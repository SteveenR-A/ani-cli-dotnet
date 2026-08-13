using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Models;
using AniCS.Desktop.Views;

namespace AniCS.Desktop.Controls;

public partial class AnimeBlockControl : UserControl
{
    public AnimeBlockControl()
    {
        InitializeComponent();
    }

    private void OnBlockClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnimeResult anime)
        {
            Services.NavigationHelper.NavigateToAnimeDetails(this, anime);
        }
    }
}
