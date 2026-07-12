using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using AniCS.Models;

namespace AniCS.Desktop.Views;

public partial class SeeMoreView : UserControl
{
    public SeeMoreView()
    {
        InitializeComponent();
    }

    public SeeMoreView(string title, IEnumerable<AnimeResult> items)
    {
        InitializeComponent();
        CategoryTitle.Text = title;
        AnimeItemsControl.ItemsSource = items;
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window && window is MainWindow mainWindow)
        {
            mainWindow.GoBack();
        }
    }
}
