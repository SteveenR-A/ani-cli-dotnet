using Avalonia.Controls;
using Avalonia.Input;
using AniCS.Models;

namespace AniCS.Desktop.Controls;

public partial class AnimeCardControl : UserControl
{
    public AnimeCardControl()
    {
        InitializeComponent();
    }

    private void OnCardTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is AnimeResult anime)
        {
            Services.NavigationHelper.NavigateToAnimeDetails(this, anime);
        }
    }
}
