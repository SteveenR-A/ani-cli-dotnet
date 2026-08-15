using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using AniCS.Desktop.ViewModels;
using AniCS.Desktop.Views.Paradigms.AndroidApp;
using AniCS.Models;
using Microsoft.Extensions.DependencyInjection;
using DesktopViews = AniCS.Desktop.Views;
using Button = Avalonia.Controls.Button;

namespace AniCS.Android.Views;

public partial class AndroidMainView : UserControl, INavigableHost
{
    private record NavigationEntry(UserControl View, string Title);

    private readonly HomeViewModel _sharedHomeViewModel;
    private readonly AndroidAppView _androidHomeView;
    private readonly MobileSearchView _searchView;
    private readonly MobileCalendarView _calendarView;
    private readonly MobileTopAnimesView _topAnimesView;
    private readonly MobileDownloadsView _downloadsView;
    private readonly MobileHistoryView _historyView;
    private readonly MobileSettingsView _settingsView;
    private readonly Stack<NavigationEntry> _navigationStack = new();

    public static AndroidMainView? Current { get; private set; }

    public bool CanGoBack =>
        ImageModalOverlay.IsVisible ||
        MainContent.Content is MobileVideoPlayerView ||
        ModalOverlay.IsVisible ||
        MainSplitView.IsPaneOpen ||
        _navigationStack.Count > 0 ||
        MainContent.Content != _androidHomeView;

    public AndroidMainView()
        : this(AniCS.Desktop.App.Services?.GetService<HomeViewModel>() ?? new HomeViewModel())
    {
    }

    public AndroidMainView(HomeViewModel homeViewModel)
    {
        Current = this;
        _sharedHomeViewModel = homeViewModel;
        InitializeComponent();
        DataContext = _sharedHomeViewModel;

        // Registrar manejador en el servicio desacoplado de navegación móvil
        Services.MobileNavigationService.BackPressHandler = HandleBackPress;

        Loaded += (s, e) =>
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel != null)
            {
                topLevel.BackRequested -= OnTopLevelBackRequested;
                topLevel.BackRequested += OnTopLevelBackRequested;
            }
        };

        _androidHomeView = new AndroidAppView { DataContext = _sharedHomeViewModel };
        _searchView = new MobileSearchView();
        _calendarView = new MobileCalendarView();
        _topAnimesView = new MobileTopAnimesView();
        _downloadsView = new MobileDownloadsView();
        _historyView = new MobileHistoryView();
        _settingsView = new MobileSettingsView();

        // Monitorear modo Donghua para ocultar/mostrar Horarios y Top Animes
        _sharedHomeViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(HomeViewModel.IsDonghuaMode))
            {
                bool isDonghua = _sharedHomeViewModel.IsDonghuaMode;

                var calendarBtn = this.FindControl<Button>("CalendarNavBtn");
                var topAnimesBtn = this.FindControl<Button>("TopAnimesNavBtn");
                var tabCalendar = this.FindControl<Button>("TabCalendar");

                if (calendarBtn != null) calendarBtn.IsVisible = !isDonghua;
                if (topAnimesBtn != null) topAnimesBtn.IsVisible = !isDonghua;
                if (tabCalendar != null) tabCalendar.IsVisible = !isDonghua;

                if (isDonghua && (MainContent.Content is MobileCalendarView || MainContent.Content is MobileTopAnimesView))
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

        // Estado inicial de los botones según modo Donghua
        bool initialDonghua = _sharedHomeViewModel.IsDonghuaMode;
        var initialCalendarBtn = this.FindControl<Button>("CalendarNavBtn");
        var initialTopAnimesBtn = this.FindControl<Button>("TopAnimesNavBtn");
        var initialTabCalendar = this.FindControl<Button>("TabCalendar");
        if (initialCalendarBtn != null) initialCalendarBtn.IsVisible = !initialDonghua;
        if (initialTopAnimesBtn != null) initialTopAnimesBtn.IsVisible = !initialDonghua;
        if (initialTabCalendar != null) initialTabCalendar.IsVisible = !initialDonghua;

        // Cargar vista de inicio por defecto
        SetMainContent(_androidHomeView);
        PageTitleText.Text = "Inicio";
        HighlightTab(IconHome, TextHome);

        if (_sharedHomeViewModel.AnimeList.Count == 0 && !_sharedHomeViewModel.IsReloading)
        {
            _ = _sharedHomeViewModel.LoadDataAsync();
        }

        AniCS.Core.Services.NetworkService.StartMonitoring();
        AniCS.Core.Services.NetworkService.ConnectivityChanged += OnConnectivityChanged;
        UpdateOfflineBanner(AniCS.Core.Services.NetworkService.IsConnected);
    }

    private void OnConnectivityChanged(bool isConnected)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() => UpdateOfflineBanner(isConnected));
    }

    private void UpdateOfflineBanner(bool isConnected)
    {
        var banner = this.FindControl<Border>("OfflineBanner");
        var text = this.FindControl<TextBlock>("OfflineBannerText");
        var icon = this.FindControl<Material.Icons.Avalonia.MaterialIcon>("OfflineBannerIcon");

        if (banner == null || text == null || icon == null) return;

        if (!isConnected)
        {
            banner.Background = Avalonia.Media.Brush.Parse("#C62828");
            icon.Kind = Material.Icons.MaterialIconKind.WifiOff;
            text.Text = "Sin conexión a internet. Tus descargas están disponibles.";
            banner.IsVisible = true;
        }
        else if (banner.IsVisible)
        {
            banner.Background = Avalonia.Media.Brush.Parse("#2E7D32");
            icon.Kind = Material.Icons.MaterialIconKind.WifiCheck;
            text.Text = "Conexión a internet restablecida.";
            Avalonia.Threading.DispatcherTimer.RunOnce(() =>
            {
                if (AniCS.Core.Services.NetworkService.IsConnected)
                {
                    banner.IsVisible = false;
                }
            }, TimeSpan.FromSeconds(3));
        }
    }

    public void SetMainContent(UserControl view)
    {
        MainContent.Content = view;

        bool isPlayer = view is MobileVideoPlayerView;
        if (isPlayer)
        {
            TopHeaderBorder.IsVisible = false;
            BottomNavBar.IsVisible = false;
            MainActivity.Instance?.EnableImmersiveMode();
        }
        else
        {
            TopHeaderBorder.IsVisible = true;
            BottomNavBar.IsVisible = true;
            MainActivity.Instance?.DisableImmersiveMode();
        }

        var isMainView = view is DesktopViews.HomeView || view is DesktopViews.SearchView || view is DesktopViews.TopAnimesView ||
                         view is DesktopViews.Paradigms.Spatial.SpatialView ||
                         view is DesktopViews.Paradigms.Node.NodeView ||
                         view is DesktopViews.Paradigms.Kinetic.KineticView ||
                         view is DesktopViews.Paradigms.ASCII.ASCIIView ||
                         view is AndroidAppView;

        SourceTogglePanel.IsVisible = isMainView;
    }

    private void OnHamburgerClicked(object? sender, RoutedEventArgs e)
    {
        MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
    }

    public void NavigateToAnimeDetails(AnimeResult anime)
    {
        try
        {
            if (MainContent.Content is UserControl current && !(current is MobileVideoPlayerView))
            {
                _navigationStack.Push(new NavigationEntry(current, PageTitleText.Text ?? "Inicio"));
            }

            var detailsView = new MobileAnimeDetailsView(anime);
            SetMainContent(detailsView);
            PageTitleText.Text = anime.Title;
        }
        catch (Exception ex)
        {
            AppLogger.Error("AndroidMainView.NavigateToAnimeDetails", ex);
        }
    }

    public void NavigateToSeeMore(string title, IEnumerable<AnimeResult> items)
    {
        if (MainContent.Content is UserControl current && !(current is MobileVideoPlayerView))
        {
            _navigationStack.Push(new NavigationEntry(current, PageTitleText.Text ?? "Inicio"));
        }

        var seeMoreView = new MobileSeeMoreView(title, items);
        SetMainContent(seeMoreView);
        PageTitleText.Text = title;
    }

    public void PushPlayerView(MobileVideoPlayerView playerView)
    {
        if (MainContent.Content is UserControl current && !(current is MobileVideoPlayerView))
        {
            _navigationStack.Push(new NavigationEntry(current, PageTitleText.Text ?? "Inicio"));
        }

        SetMainContent(playerView);
    }

    public bool HandleBackPress()
    {
        // 0. Si el visor de imágenes en grande está abierto -> cerrarlo
        if (ImageModalOverlay.IsVisible)
        {
            CloseImageModal();
            return true;
        }

        // 1. Si el reproductor de video está activo -> cerrarlo
        if (MainContent.Content is MobileVideoPlayerView playerView)
        {
            playerView.ClosePlayer();
            return true;
        }

        // 2. Si hay un modal abierto -> cerrarlo
        if (ModalOverlay.IsVisible)
        {
            CloseModal();
            return true;
        }

        // 3. Si el menú lateral está abierto -> cerrarlo
        if (MainSplitView.IsPaneOpen)
        {
            MainSplitView.IsPaneOpen = false;
            return true;
        }

        // 4. Si hay vistas en la pila de navegación -> volver a la anterior
        if (_navigationStack.Count > 0)
        {
            var prev = _navigationStack.Pop();
            SetMainContent(prev.View);
            PageTitleText.Text = prev.Title;
            UpdateTabHighlightsForView(prev.View);
            return true;
        }

        // 5. Si no estamos en la pestaña Inicio -> volver a Inicio
        if (MainContent.Content != _androidHomeView)
        {
            OnHomeClicked(null, new RoutedEventArgs());
            return true;
        }

        // 6. Estamos en Inicio y pila vacía -> permitir salir de la app
        return false;
    }

    private void OnTopLevelBackRequested(object? sender, RoutedEventArgs e)
    {
        global::Android.Util.Log.Debug("AniCS_Back", "AndroidMainView: TopLevel.BackRequested routed event received!");
        bool handled = HandleBackPress();
        if (handled)
        {
            e.Handled = true;
        }
    }

    private void UpdateTabHighlightsForView(Control view)
    {
        if (view == _androidHomeView) HighlightTab(IconHome, TextHome);
        else if (view == _searchView) HighlightTab(IconSearch, TextSearch);
        else if (view == _calendarView) HighlightTab(IconCalendar, TextCalendar);
        else if (view == _downloadsView) HighlightTab(IconDownloads, TextDownloads);
        else if (view == _historyView) HighlightTab(IconHistory, TextHistory);
        else ClearTabHighlights();
    }

    public void GoBack()
    {
        HandleBackPress();
    }

    public void FinishPlayerClose(MobileVideoPlayerView playerView)
    {
        if (_navigationStack.Count > 0)
        {
            var prev = _navigationStack.Pop();
            SetMainContent(prev.View);
            PageTitleText.Text = prev.Title;
        }
        else
        {
            SetMainContent(_androidHomeView);
            PageTitleText.Text = "Inicio";
            HighlightTab(IconHome, TextHome);
        }
    }

    // ── Sidebar & Navigation Handlers (Los 7 apartados de PC) ────────────

    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        _navigationStack.Clear();
        SetMainContent(_androidHomeView);
        PageTitleText.Text = "Inicio";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconHome, TextHome);
    }

    private void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        _navigationStack.Clear();
        SetMainContent(_searchView);
        PageTitleText.Text = "Buscar Anime";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconSearch, TextSearch);
    }

    private void OnCalendarClicked(object? sender, RoutedEventArgs e)
    {
        _navigationStack.Clear();
        SetMainContent(_calendarView);
        PageTitleText.Text = "Horarios";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconCalendar, TextCalendar);
    }

    private void OnTopAnimesClicked(object? sender, RoutedEventArgs e)
    {
        _navigationStack.Clear();
        SetMainContent(_topAnimesView);
        PageTitleText.Text = "Top Animes";
        MainSplitView.IsPaneOpen = false;
        ClearTabHighlights();
    }

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        _navigationStack.Clear();
        _downloadsView.LoadDownloads();
        SetMainContent(_downloadsView);
        PageTitleText.Text = "Descargas";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconDownloads, TextDownloads);
    }

    private void OnHistoryClicked(object? sender, RoutedEventArgs e)
    {
        _navigationStack.Clear();
        _historyView.LoadHistory();
        SetMainContent(_historyView);
        PageTitleText.Text = "Historial";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconHistory, TextHistory);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        _navigationStack.Clear();
        _settingsView.LoadConfig();
        SetMainContent(_settingsView);
        PageTitleText.Text = "Configuración";
        MainSplitView.IsPaneOpen = false;
        ClearTabHighlights();
    }

    // ── Bottom Bar Quick Tabs Handlers ─────────────────────────────────

    private void OnTabHomeClicked(object? sender, RoutedEventArgs e) => OnHomeClicked(sender, e);
    private void OnTabSearchClicked(object? sender, RoutedEventArgs e) => OnSearchClicked(sender, e);
    private void OnTabCalendarClicked(object? sender, RoutedEventArgs e) => OnCalendarClicked(sender, e);
    private void OnTabDownloadsClicked(object? sender, RoutedEventArgs e) => OnDownloadsClicked(sender, e);
    private void OnTabHistoryClicked(object? sender, RoutedEventArgs e) => OnHistoryClicked(sender, e);

    private void HighlightTab(Material.Icons.Avalonia.MaterialIcon activeIcon, TextBlock activeText)
    {
        ClearTabHighlights();
        var primaryBrush = Avalonia.Application.Current?.Resources["AppPrimaryColor"] as IBrush ?? Brushes.Purple;

        activeIcon.Foreground = primaryBrush;
        activeText.Foreground = primaryBrush;
    }

    private void ClearTabHighlights()
    {
        var subtextBrush = Avalonia.Application.Current?.Resources["AppSubtextColor"] as IBrush ?? Brushes.Gray;

        IconHome.Foreground = subtextBrush;
        TextHome.Foreground = subtextBrush;
        IconSearch.Foreground = subtextBrush;
        TextSearch.Foreground = subtextBrush;
        IconCalendar.Foreground = subtextBrush;
        TextCalendar.Foreground = subtextBrush;
        IconDownloads.Foreground = subtextBrush;
        TextDownloads.Foreground = subtextBrush;
        IconHistory.Foreground = subtextBrush;
        TextHistory.Foreground = subtextBrush;
    }

    // ── Modal Overlay API para Android ─────────────────────────────────

    public void ShowModal(string title, Control content)
    {
        ModalTitle.Text = title;
        ModalContentContainer.Children.Clear();
        ModalContentContainer.Children.Add(content);
        ModalOverlay.IsVisible = true;
    }

    public void CloseModal()
    {
        ModalOverlay.IsVisible = false;
        ModalContentContainer.Children.Clear();
    }

    private void OnCloseModalClicked(object? sender, RoutedEventArgs e)
    {
        CloseModal();
    }

    // ── Visor de Portadas en Grande (Lightbox) ─────────────────────────

    public void ShowImageModal(string imageUrl, string title)
    {
        ImageModalTitle.Text = title;
        AniCS.Desktop.Converters.AsyncImageLoader.SetSourceUrl(ImageModalPicture, imageUrl);
        ImageModalOverlay.IsVisible = true;
    }

    public void CloseImageModal()
    {
        ImageModalOverlay.IsVisible = false;
        AniCS.Desktop.Converters.AsyncImageLoader.SetSourceUrl(ImageModalPicture, string.Empty);
    }

    private void OnCloseImageModalClicked(object? sender, RoutedEventArgs e)
    {
        CloseImageModal();
    }
}
