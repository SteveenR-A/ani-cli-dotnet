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
        if (DataContext is AnimeResult anime && TopLevel.GetTopLevel(this) is Window window)
        {
            if (window is MainWindow mainWindow)
            {
                mainWindow.NavigateToAnimeDetails(anime);
            }
        }
    }
}
