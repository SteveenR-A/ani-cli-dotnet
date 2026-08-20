using Avalonia.Controls;
using Avalonia.Input;
using AniCS.Models;

namespace AniCS.Desktop.Controls;

public partial class AnimeBlockControl : UserControl
{
    public AnimeBlockControl()
    {
        InitializeComponent();
    }

    private void OnBlockTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is AnimeResult anime)
        {
            Services.NavigationHelper.NavigateToAnimeDetails(this, anime);
        }
    }
}
