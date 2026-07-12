using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Desktop.Views;

namespace AniCS.Desktop;

public partial class MainWindow : Window
{
    private ViewModels.HomeViewModel _sharedHomeViewModel = new ViewModels.HomeViewModel();
    private HomeView _homeView = new HomeView();
    private SearchView _searchView = new SearchView();

    private CalendarView _calendarView = new CalendarView();
    private DownloadsView _downloadsView = new DownloadsView();
    private HistoryView _historyView = new HistoryView();
    private SettingsView _settingsView = new SettingsView();
    private UserControl? _previousView;

    public MainWindow()
    {
        InitializeComponent();
        ApplyWindowConfig();
        LoadHomeParadigm();
    }

    private void LoadHomeParadigm()
    {
        var config = ConfigManager.Current;
        UserControl targetView;
        
        switch (config.UiParadigm)
        {
            case "Spatial": targetView = new Views.Paradigms.Spatial.SpatialView(); break;
            case "Node": targetView = new Views.Paradigms.Node.NodeView(); break;
            case "Kinetic": targetView = new Views.Paradigms.Kinetic.KineticView(); break;
            case "ASCII": targetView = new Views.Paradigms.ASCII.ASCIIView(); break;
            case "AndroidApp": targetView = new Views.Paradigms.AndroidApp.AndroidAppView(); break;
            default: targetView = _homeView; break;
        }

        // Inyectamos el ViewModel compartido para que no re-carguen todo al cambiar de vista
        targetView.DataContext = _sharedHomeViewModel;
        MainContent.Content = targetView;

        // Si es la primera vez que se carga la aplicación y la lista está vacía, forzar la carga
        if (_sharedHomeViewModel.AnimeList.Count == 0 && !_sharedHomeViewModel.IsReloading)
        {
            _ = _sharedHomeViewModel.LoadDataAsync();
        }
    }

    private void ApplyWindowConfig()
    {
        var config = ConfigManager.Current;
        if (config.WindowState == "Maximized")
        {
            this.WindowState = WindowState.Maximized;
        }
        else
        {
            this.WindowState = WindowState.Normal;
            this.Width = config.WindowWidth > 0 ? config.WindowWidth : 1000;
            this.Height = config.WindowHeight > 0 ? config.WindowHeight : 700;
        }
    }

    protected override void OnClosing(Avalonia.Controls.WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        var config = ConfigManager.Current;
        config.WindowState = this.WindowState == WindowState.Maximized ? "Maximized" : "Normal";
        if (this.WindowState == WindowState.Normal)
        {
            config.WindowWidth = this.Bounds.Width;
            config.WindowHeight = this.Bounds.Height;
        }
        ConfigManager.Save(config);
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

    public void NavigateToSeeMore(string title, System.Collections.Generic.IEnumerable<AniCS.Models.AnimeResult> items)
    {
        _previousView = MainContent.Content as UserControl;
        var seeMoreView = new SeeMoreView(title, items);
        MainContent.Content = seeMoreView;
        PageTitleText.Text = title;
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
            LoadHomeParadigm();
            PageTitleText.Text = "Inicio";
        }
    }
    
    private void SetTitleForView(UserControl view)
    {
        if (view is SearchView) PageTitleText.Text = "Buscar Anime";
        else if (view is CalendarView) PageTitleText.Text = "Calendario";
        else if (view is DownloadsView) PageTitleText.Text = "Descargas";
        else if (view is HistoryView) PageTitleText.Text = "Historial";
        else if (view is SettingsView) PageTitleText.Text = "Configuración";
        else PageTitleText.Text = "Inicio"; // Todos los demás (HomeView, ASCIIView, etc.) son el Inicio
    }

    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        LoadHomeParadigm();
        PageTitleText.Text = "Inicio";
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
        MainContent.Content = _downloadsView;
        PageTitleText.Text = "Descargas";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnHistoryClicked(object? sender, RoutedEventArgs e)
    {
        _historyView.Reload(); // Refrescar el historial al abrirlo
        MainContent.Content = _historyView;
        PageTitleText.Text = "Historial";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        _settingsView.LoadConfig(); // Refrescar por si se cambió desde otro lado
        MainContent.Content = _settingsView;
        PageTitleText.Text = "Configuración";
        MainSplitView.IsPaneOpen = false;
    }
}