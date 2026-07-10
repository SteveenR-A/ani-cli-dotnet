using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Extractors;
using System.Net.Http;
using Avalonia.Threading;

namespace AniCS.Desktop.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ViewModels.HomeViewModel vm && vm.AnimeList.Count == 0)
        {
            _ = vm.LoadDataAsync();
        }
    }
}
