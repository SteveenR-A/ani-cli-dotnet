using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Desktop.Views;

namespace AniCS.Desktop;

public partial class MainWindow : Window
{
    private HomeView _homeView = new HomeView();
    private SearchView _searchView = new SearchView();

    private CalendarView _calendarView = new CalendarView();
    private UserControl? _previousView;

    public MainWindow()
    {
        InitializeComponent();
        MainContent.Content = _homeView;
    }

    private void OnHamburgerClicked(object? sender, RoutedEventArgs e)
    {
        MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
    }

    public void NavigateToAnimeDetails(AniCS.Models.AnimeResult anime)
    {
        _previousView = MainContent.Content as UserControl;
        var detailsView = new AnimeDetailsView(anime);
        MainContent.Content = detailsView;
    }

    public void GoBack()
    {
        if (_previousView != null)
        {
            MainContent.Content = _previousView;
            _previousView = null;
        }
        else
        {
            MainContent.Content = _homeView;
        }
    }

    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = _homeView;
        MainSplitView.IsPaneOpen = false;
    }

    private void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = _searchView;
        MainSplitView.IsPaneOpen = false;
    }

    private void OnCalendarClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = _calendarView;
        MainSplitView.IsPaneOpen = false;
    }
}