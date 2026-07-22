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
    private TopAnimesView _topAnimesView = new TopAnimesView();
    private DownloadsView _downloadsView = new DownloadsView();
    private HistoryView _historyView = new HistoryView();
    private SettingsView _settingsView = new SettingsView();
    private UserControl? _previousView;

    public MainWindow()
    {
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

    protected override void OnOpened(System.EventArgs e)
    {
        base.OnOpened(e);
        CheckForUpdates();
    }

    private void CheckForUpdates()
    {
        var config = ConfigManager.Current;
        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";

        if (config.LastSeenVersion != currentVersion)
        {
            string changelog = "¡Hola! Novedades de la versión 1.5.2 (Parche \"Arreglando lo que rompimos al arreglar\"):\n\n" +
                               "• 🐛 Se agregaron nuevos bugs misteriosos creados con cariño al intentar corregir el bug anterior de las descargas.\n" +
                               "• 📁 Corregido el despiste espacial: Los animes sin título ya no se descargan revueltos en la misma carpeta raíz matándose entre sí.\n" +
                               "• 🗑️ Arreglado el efecto dominó: Al borrar el capítulo de un anime ya no se borra mágicamente el otro anime que compartía archivo.\n" +
                               "• 🏷️ Reparado JKAnime: Ahora la app sí lee el título del anime en lugar de dejar un vacío existencial.\n" +
                               "• 🌸 Géneros rescatados: JKAnime devolvió los géneros que habían desaparecido por cambios en su web.\n" +
                               "• 🔍 MundoDonghua revivido: La búsqueda volvió a la vida tras actualizar el formato de URLs de consulta.\n" +
                               "• 💻 Navegación CLI renovada: Se agregaron los comandos 'top' (ranking) y 'directorio' (filtros por género/estado/tipo) en la consola.\n" +
                               "• 📂 Carpeta tests limpia: Se organizaron todos los archivos de prueba que estaban flotando en el proyecto.\n\n" +
                               "¡Gracias por usar AniCS y sobrevivir a nuestros parches!";


            var changelogWindow = new Controls.ChangelogWindow(currentVersion, changelog);
            changelogWindow.ShowDialog(this);

            config.LastSeenVersion = currentVersion;
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
        _previousView = MainContent.Content as UserControl;
        var detailsView = new AnimeDetailsView(anime);
        SetMainContent(detailsView);
        PageTitleText.Text = anime.Title;
    }

    public void NavigateToSeeMore(string title, System.Collections.Generic.IEnumerable<AniCS.Models.AnimeResult> items)
    {
        _previousView = MainContent.Content as UserControl;
        var seeMoreView = new SeeMoreView(title, items);
        SetMainContent(seeMoreView);
        PageTitleText.Text = title;
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