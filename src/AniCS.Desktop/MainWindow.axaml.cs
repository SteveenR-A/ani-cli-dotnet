using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

namespace AniCS.Desktop;

public partial class MainWindow : Window, INavigableHost
{
    private readonly ViewModels.HomeViewModel _sharedHomeViewModel;
    private HomeView _homeView = new HomeView();
    private SearchView _searchView = new SearchView();

    private CalendarView _calendarView = new CalendarView();
    private TopAnimesView _topAnimesView = new TopAnimesView();
    private DownloadsView _downloadsView = new DownloadsView();
    private HistoryView _historyView = new HistoryView();
    private SettingsView _settingsView = new SettingsView();
    private UserControl? _previousView;

    public MainWindow()
        : this(App.Services?.GetService<ViewModels.HomeViewModel>() ?? new ViewModels.HomeViewModel())
    {
    }

    public MainWindow(ViewModels.HomeViewModel homeViewModel)
    {
        _sharedHomeViewModel = homeViewModel;
        InitializeComponent();
        TopNavigationBar.DataContext = _sharedHomeViewModel;

        _sharedHomeViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModels.HomeViewModel.IsDonghuaMode))
            {
                bool isDonghua = _sharedHomeViewModel.IsDonghuaMode;

                var calendarBtn = this.FindControl<Button>("CalendarNavBtn");
                var topAnimesBtn = this.FindControl<Button>("TopAnimesNavBtn");

                if (calendarBtn != null) calendarBtn.IsVisible = !isDonghua;
                if (topAnimesBtn != null) topAnimesBtn.IsVisible = !isDonghua;

                if (isDonghua && (MainContent.Content is CalendarView || MainContent.Content is TopAnimesView))
                {
                    OnHomeClicked(null, new RoutedEventArgs());
                }
                else
                {
                    _searchView.ReloadConfig();
                    _topAnimesView.ReloadConfig();
                }
            }
        };

        // Estado inicial de los botones
        bool initialDonghua = _sharedHomeViewModel.IsDonghuaMode;
        var initialCalendarBtn = this.FindControl<Button>("CalendarNavBtn");
        var initialTopAnimesBtn = this.FindControl<Button>("TopAnimesNavBtn");
        if (initialCalendarBtn != null) initialCalendarBtn.IsVisible = !initialDonghua;
        if (initialTopAnimesBtn != null) initialTopAnimesBtn.IsVisible = !initialDonghua;

        ApplyWindowConfig();
        LoadHomeParadigm();
    }

    protected override async void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        try
        {
            await CheckForUpdatesAsync();
        }
        catch { }
    }

    private async Task CheckForUpdatesAsync()
    {
        var config = ConfigManager.Current;
        var currentVersion = AppInfo.CurrentVersion;

        // 1) Changelog local: se muestra una vez tras cada actualización del binario.
        if (config.LastSeenVersion != currentVersion)
        {
            var changelogWindow = new Controls.ChangelogWindow(currentVersion, AppInfo.LatestChangelog);
            await changelogWindow.ShowDialog(this);

            config.LastSeenVersion = currentVersion;
            ConfigManager.Save(config);
        }

        // 2) Chequeo contra GitHub: avisa una sola vez por release cuando hay novedad.
        var updater = App.Services.GetRequiredService<Services.AppUpdateService>();
        var release = await updater.FetchLatestReleaseAsync();
        if (updater.IsNewerAvailable(release, out _) && release != null && config.LastSeenReleaseVersion != release.TagName)
        {
            var notes = string.IsNullOrWhiteSpace(release.Body) ? AppInfo.LatestChangelog : release.Body;
            var updateWindow = new Controls.ChangelogWindow(release.TagName, notes);
            await updateWindow.ShowDialog(this);

            config.LastSeenReleaseVersion = release.TagName;
            ConfigManager.Save(config);
        }
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

        targetView.DataContext = _sharedHomeViewModel;
        SetMainContent(targetView);

        if (_sharedHomeViewModel.AnimeList.Count == 0 && !_sharedHomeViewModel.IsReloading)
        {
            _ = _sharedHomeViewModel.LoadDataAsync();
        }
    }

    private void SetMainContent(UserControl view)
    {
        MainContent.Content = view;

        var isMainView = view is HomeView || view is SearchView || view is TopAnimesView ||
                         view is Views.Paradigms.Spatial.SpatialView ||
                         view is Views.Paradigms.Node.NodeView ||
                         view is Views.Paradigms.Kinetic.KineticView ||
                         view is Views.Paradigms.ASCII.ASCIIView ||
                         view is Views.Paradigms.AndroidApp.AndroidAppView;

        SourceTogglePanel.IsVisible = isMainView;
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
        try
        {
            _previousView = MainContent.Content as UserControl;
            var detailsView = new AnimeDetailsView(anime);
            SetMainContent(detailsView);
            PageTitleText.Text = anime.Title;
        }
        catch (System.Exception ex)
        {
            AniCS.AppLogger.Error("NavigateToAnimeDetails", ex);
            PageTitleText.Text = "Crash interceptado. Revisa %LocalAppData%/AniCS/logs";
        }
    }



    public void NavigateToSeeMore(string title, System.Collections.Generic.IEnumerable<AniCS.Models.AnimeResult> items)
    {
        _previousView = MainContent.Content as UserControl;
        var seeMoreView = new SeeMoreView(title, items);
        SetMainContent(seeMoreView);
        PageTitleText.Text = title;
    }

    /// <summary>
    /// Generic navigation helper used by the mobile BottomNavigationBar.
    /// </summary>
    public void NavigateTo(string viewName)
    {
        switch (viewName)
        {
            case "Home":
                OnHomeClicked(null, new RoutedEventArgs());
                break;
            case "Search":
                OnSearchClicked(null, new RoutedEventArgs());
                break;
            case "Downloads":
                OnDownloadsClicked(null, new RoutedEventArgs());
                break;
            case "History":
                OnHistoryClicked(null, new RoutedEventArgs());
                break;
            case "Settings":
                OnSettingsClicked(null, new RoutedEventArgs());
                break;
        }
    }

    public void GoBack()
    {
        if (_previousView != null)
        {
            SetMainContent(_previousView);
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
        else if (view is CalendarView) PageTitleText.Text = "Horarios";
        else if (view is TopAnimesView) PageTitleText.Text = "Top Animes";
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
        SetMainContent(_searchView);
        PageTitleText.Text = "Buscar Anime";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnCalendarClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_calendarView);
        PageTitleText.Text = "Horarios";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnTopAnimesClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_topAnimesView);
        PageTitleText.Text = "Top Animes";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_downloadsView);
        PageTitleText.Text = "Descargas";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnHistoryClicked(object? sender, RoutedEventArgs e)
    {
        _historyView.Reload(); // Refrescar el historial al abrirlo
        SetMainContent(_historyView);
        PageTitleText.Text = "Historial";
        MainSplitView.IsPaneOpen = false;
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        _settingsView.LoadConfig(); // Refrescar por si se cambió desde otro lado
        SetMainContent(_settingsView);
        PageTitleText.Text = "Configuración";
        MainSplitView.IsPaneOpen = false;
    }
}