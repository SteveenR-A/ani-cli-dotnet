using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Models;

namespace AniCS.Desktop.Controls;

public partial class AnimeCardControl : UserControl
{
    public AnimeCardControl()
    {
        InitializeComponent();
    }

    private void OnCardClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnimeResult anime)
        {
            Services.NavigationHelper.NavigateToAnimeDetails(this, anime);
        }
    }
}
