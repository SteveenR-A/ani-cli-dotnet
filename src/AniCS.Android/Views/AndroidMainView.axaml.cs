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
    private readonly HomeViewModel _sharedHomeViewModel;
    private readonly AndroidAppView _androidHomeView;
    private readonly MobileSearchView _searchView;
    private readonly MobileCalendarView _calendarView;
    private readonly MobileTopAnimesView _topAnimesView;
    private readonly DesktopViews.DownloadsView _downloadsView;
    private readonly MobileHistoryView _historyView;
    private readonly DesktopViews.SettingsView _settingsView;
    private UserControl? _previousView;

    public static AndroidMainView? Current { get; private set; }

    public bool CanGoBack => MainContent.Content != null && !(MainContent.Content is AndroidAppView) || _previousView != null;

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

        _androidHomeView = new AndroidAppView { DataContext = _sharedHomeViewModel };
        _searchView = new MobileSearchView();
        _calendarView = new MobileCalendarView();
        _topAnimesView = new MobileTopAnimesView();
        _downloadsView = new DesktopViews.DownloadsView();
        _historyView = new MobileHistoryView();
        _settingsView = new DesktopViews.SettingsView();

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
            _previousView = MainContent.Content as UserControl;
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
        _previousView = MainContent.Content as UserControl;
        var seeMoreView = new DesktopViews.SeeMoreView(title, items);
        SetMainContent(seeMoreView);
        PageTitleText.Text = title;
    }

    public void GoBack()
    {
        if (MainContent.Content is MobileVideoPlayerView playerView)
        {
            playerView.ClosePlayer();
            return;
        }

        if (_previousView != null)
        {
            var targetView = _previousView;
            _previousView = null;
            SetMainContent(targetView);
            SetTitleForView(targetView);
        }
        else
        {
            SetMainContent(_androidHomeView);
            PageTitleText.Text = "Inicio";
            HighlightTab(IconHome, TextHome);
        }
    }

    public void FinishPlayerClose(MobileVideoPlayerView playerView)
    {
        if (_previousView != null)
        {
            var targetView = _previousView;
            _previousView = null;
            SetMainContent(targetView);
            SetTitleForView(targetView);
        }
        else
        {
            SetMainContent(_androidHomeView);
            PageTitleText.Text = "Inicio";
            HighlightTab(IconHome, TextHome);
        }
    }

    private void SetTitleForView(UserControl view)
    {
        if (view is DesktopViews.SearchView || view is MobileSearchView) PageTitleText.Text = "Buscar Anime";
        else if (view is DesktopViews.CalendarView || view is MobileCalendarView) PageTitleText.Text = "Horarios";
        else if (view is DesktopViews.TopAnimesView) PageTitleText.Text = "Top Animes";
        else if (view is DesktopViews.DownloadsView) PageTitleText.Text = "Descargas";
        else if (view is DesktopViews.HistoryView) PageTitleText.Text = "Historial";
        else if (view is DesktopViews.SettingsView) PageTitleText.Text = "Configuración";
        else PageTitleText.Text = "Inicio";
    }

    // ── Sidebar & Navigation Handlers (Los 7 apartados de PC) ────────────

    private void OnHomeClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_androidHomeView);
        PageTitleText.Text = "Inicio";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconHome, TextHome);
    }

    private void OnSearchClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_searchView);
        PageTitleText.Text = "Buscar Anime";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconSearch, TextSearch);
    }

    private void OnCalendarClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_calendarView);
        PageTitleText.Text = "Horarios";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconCalendar, TextCalendar);
    }

    private void OnTopAnimesClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_topAnimesView);
        PageTitleText.Text = "Top Animes";
        MainSplitView.IsPaneOpen = false;
        ClearTabHighlights();
    }

    private void OnDownloadsClicked(object? sender, RoutedEventArgs e)
    {
        SetMainContent(_downloadsView);
        PageTitleText.Text = "Descargas";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconDownloads, TextDownloads);
    }

    private void OnHistoryClicked(object? sender, RoutedEventArgs e)
    {
        _historyView.LoadHistory();
        SetMainContent(_historyView);
        PageTitleText.Text = "Historial";
        MainSplitView.IsPaneOpen = false;
        HighlightTab(IconHistory, TextHistory);
    }

    private void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
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
}
