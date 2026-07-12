using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Desktop.ViewModels;
using System.Collections.Generic;
using AniCS.Models;

namespace AniCS.Desktop.Views.Paradigms.AndroidApp;

public partial class AndroidAppView : UserControl
{
    public AndroidAppView()
    {
        InitializeComponent();
    }

    private void OnSeeMoreLatestClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            NavigateToSeeMore("Emisión Reciente", vm.LatestList);
        }
    }

    private void OnSeeMorePremieresClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            NavigateToSeeMore("Estrenos / Destacados", vm.PremieresList);
        }
    }

    private void OnSeeMoreHistoryClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HomeViewModel vm)
        {
            NavigateToSeeMore("Seguir Viendo", vm.HistoryList);
        }
    }

    private void NavigateToSeeMore(string title, IEnumerable<AnimeResult> items)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window is MainWindow mainWindow)
        {
            mainWindow.NavigateToSeeMore(title, items);
        }
    }
}
