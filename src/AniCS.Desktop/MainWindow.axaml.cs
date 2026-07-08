using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Desktop.Views;

namespace AniCS.Desktop;

public partial class MainWindow : Window
{
    private HomeView _homeView = new HomeView();
    private SearchView _searchView = new SearchView();

    private CalendarView _calendarView = new CalendarView();
    private DownloadsView _downloadsView = new DownloadsView();
    private HistoryView _historyView = new HistoryView();
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
        PageTitleText.Text = anime.Title;
    }

    public void GoBack()
    {
        if (_previousView != null)
        {
            MainContent.Content = _previousView;
            SetTitleForView(_previousView);
            _previousView = null;
        }
        else
        {
            MainContent.Content = _homeView;
            PageTitleText.Text = "Inicio (Recientes)";
        }
    }
    
    private void SetTitleForView(UserControl view)
    {
        if (view is HomeView) PageTitleText.Text = "Inicio (Recientes)";
        else if (view is SearchView) PageTitleText.Text = "Buscar Anime";
        else if (view is CalendarView) PageTitleText.Text = "Calendario";
        else if (view is DownloadsView) PageTitleText.Text = "Descargas";
        else if (view is HistoryView) PageTitleText.Text = "Historial";
    }

    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = _homeView;
        PageTitleText.Text = "Inicio (Recientes)";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = _searchView;
        PageTitleText.Text = "Buscar Anime";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnCalendarClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = _calendarView;
        PageTitleText.Text = "Calendario";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = new DownloadsView();
        PageTitleText.Text = "Descargas";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnHistoryClicked(object? sender, RoutedEventArgs e)
    {
        MainContent.Content = new HistoryView();
        PageTitleText.Text = "Historial";
        MainSplitView.IsPaneOpen = false;
    }
}