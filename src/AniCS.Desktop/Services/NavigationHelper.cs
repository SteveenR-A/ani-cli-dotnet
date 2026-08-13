using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using AniCS.Models;
using System.Collections.Generic;

namespace AniCS.Desktop.Services;

public static class NavigationHelper
{
    public static void NavigateToAnimeDetails(Visual source, AnimeResult anime)
    {
        var host = FindHost(source);
        if (host != null)
        {
            host.NavigateToAnimeDetails(anime);
            return;
        }

        AppLogger.Error("NavigationHelper", "No se encontró un INavigableHost válido en el árbol visual.");
    }

    public static void NavigateToSeeMore(Visual source, string title, IEnumerable<AnimeResult> items)
    {
        var host = FindHost(source);
        if (host != null)
        {
            host.NavigateToSeeMore(title, items);
            return;
        }

        AppLogger.Error("NavigationHelper", "No se encontró un INavigableHost válido en el árbol visual.");
    }

    public static void GoBack(Visual source)
    {
        var host = FindHost(source);
        if (host != null)
        {
            host.GoBack();
            return;
        }

        AppLogger.Error("NavigationHelper", "No se encontró un INavigableHost válido en el árbol visual.");
    }

    private static INavigableHost? FindHost(Visual source)
    {
        var topLevel = TopLevel.GetTopLevel(source);
        if (topLevel is INavigableHost topHost)
            return topHost;

        Visual? current = source;
        while (current != null)
        {
            if (current is INavigableHost host)
                return host;

            current = current.GetVisualParent();
        }

        return null;
    }
}
