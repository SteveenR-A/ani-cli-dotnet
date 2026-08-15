using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AniCS.Extractors;
using AniCS.Models;
using AniCS.Desktop.Converters;
using AniCS.Desktop.Services;
using AniCS.Desktop.Views;
using AniCS.Player;
using AniCS.Resolver;

using App = AniCS.Desktop.App;
using Button = Avalonia.Controls.Button;
using DownloadManager = AniCS.Desktop.Services.DownloadManager;

namespace AniCS.Android.Views;

public partial class MobileAnimeDetailsView : UserControl
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private AnimeResult _anime;
    private readonly IPlayerBackend _playerBackend;
    private readonly IResolverBackend _resolverBackend;
    private EpisodeViewModel? _nowPlayingVm;
    private List<EpisodeViewModel>? _episodeViewModels;

    public MobileAnimeDetailsView()
    {
        InitializeComponent();
        _anime = new AnimeResult();
        _playerBackend = App.Services.GetService(typeof(IPlayerBackend)) as IPlayerBackend ?? PlayerFactory.CreateFromConfig();
        _resolverBackend = App.Services.GetService(typeof(IResolverBackend)) as IResolverBackend ?? ResolverFactory.CreateFromConfig();
    }

    public MobileAnimeDetailsView(AnimeResult anime)
    {
        InitializeComponent();
        _anime = anime ?? new AnimeResult();
        DataContext = _anime;
        TitleText.Text = string.IsNullOrEmpty(_anime.Title) ? "Detalles del Anime" : _anime.Title;
        HeaderTitleText.Text = TitleText.Text;

        _playerBackend = App.Services.GetService(typeof(IPlayerBackend)) as IPlayerBackend ?? PlayerFactory.CreateFromConfig();
        _resolverBackend = App.Services.GetService(typeof(IResolverBackend)) as IResolverBackend ?? ResolverFactory.CreateFromConfig();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnBackClicked(object? sender, RoutedEventArgs e)
    {
        NavigationHelper.GoBack(this);
    }

    private void OnCoverImageClicked(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_anime.ThumbnailUrl))
        {
            AndroidMainView.Current?.ShowImageModal(_anime.ThumbnailUrl, _anime.Title);
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged -= OnDownloadsChanged;
    }

    private void OnDownloadsChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_episodeViewModels != null)
            {
                foreach (var vm in _episodeViewModels)
                {
                    UpdateEpisodeViewModelState(vm);
                }
            }
        });
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        DownloadManager.DownloadsChanged -= OnDownloadsChanged;
        DownloadManager.DownloadsChanged += OnDownloadsChanged;
        await LoadAnimeInfoAsync();
    }

    private async void OnRetryClicked(object? sender, RoutedEventArgs e)
    {
        RetryBtn.IsVisible = false;
        await LoadAnimeInfoAsync();
    }

    private async Task LoadAnimeInfoAsync()
    {
        if (_anime == null)
        {
            StatusText.Text = "Información de anime no disponible.";
            RetryBtn.IsVisible = true;
            return;
        }

        // Si la URL está vacía (ej. importado desde disco o scan manual), buscar online por título
        if (string.IsNullOrWhiteSpace(_anime.Url) && !string.IsNullOrWhiteSpace(_anime.Title))
        {
            Dispatcher.UIThread.Invoke(() =>
            {
                StatusText.Text = $"Buscando '{_anime.Title}' en línea...";
                StatusText.IsVisible = true;
                RetryBtn.IsVisible = false;
            });

            try
            {
                var searchExtractor = ExtractorFactory.GetExtractor();
                var searchResults = await searchExtractor.SearchAsync(_anime.Title);
                if (searchResults.Count == 0)
                {
                    IAnimeExtractor altExtractor = searchExtractor is JKAnimeExtractor
                        ? new MundoDonghuaExtractor(_httpClient)
                        : new JKAnimeExtractor(_httpClient);
                    searchResults = await altExtractor.SearchAsync(_anime.Title);
                }

                if (searchResults.Count > 0)
                {
                    var match = searchResults.FirstOrDefault(r => 
                        string.Equals(r.Title, _anime.Title, StringComparison.OrdinalIgnoreCase)) ?? searchResults[0];

                    _anime.Url = match.Url;
                    if (string.IsNullOrEmpty(_anime.ThumbnailUrl)) _anime.ThumbnailUrl = match.ThumbnailUrl;

                    DownloadManager.LinkAnimeUrl(_anime.Title, _anime.Url, _anime.ThumbnailUrl);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("MobileAnimeDetailsView.SearchOnlineForUrl", ex);
            }
        }

        if (string.IsNullOrWhiteSpace(_anime.Url))
        {
            var downloadedAnime = DownloadManager.GetAll()
                .FirstOrDefault(a => string.Equals(a.Title, _anime.Title, StringComparison.OrdinalIgnoreCase));

            Dispatcher.UIThread.Invoke(() =>
            {
                if (downloadedAnime != null && downloadedAnime.Episodes.Count > 0)
                {
                    StatusText.IsVisible = false;
                    RetryBtn.IsVisible = false;
                    var viewModels = new List<EpisodeViewModel>();
                    foreach (var ep in downloadedAnime.Episodes)
                    {
                        var episodeModel = new Episode
                        {
                            EpisodeNumber = ep.EpisodeNumber,
                            Title = ep.EpisodeTitle,
                            Url = ep.FilePath
                        };
                        var vm = new EpisodeViewModel(episodeModel)
                        {
                            IsDownloaded = true,
                            CanDownload = false,
                            DownloadText = "Descargado",
                            DownloadIcon = "Check"
                        };
                        viewModels.Add(vm);
                    }
                    EpisodesList.ItemsSource = viewModels;
                    SynopsisText.Text = "Anime importado desde archivos locales en el dispositivo.";
                }
                else
                {
                    StatusText.Text = "No se pudo encontrar información en línea para este anime.";
                    RetryBtn.IsVisible = true;
                }
            });
            return;
        }

        StatusText.Text = "Cargando información...";
        StatusText.IsVisible = true;
        RetryBtn.IsVisible = false;

        try
        {
            var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);
            var detailsTask = extractor.GetDetailsAsync(_anime.Url);
            var episodesTask = extractor.GetEpisodesAsync(_anime.Url);

            try
            {
                await Task.WhenAll(detailsTask, episodesTask);
            }
            catch (Exception ex)
            {
                AppLogger.Error("MobileAnimeDetailsView.WhenAll", ex);
            }

            // Update details
            try
            {
                var details = await detailsTask;
                if (details != null)
                {
                    Dispatcher.UIThread.Invoke(() =>
                    {
                        if (string.IsNullOrWhiteSpace(details.Title)) details.Title = _anime.Title;
                        if (string.IsNullOrEmpty(details.ThumbnailUrl)) details.ThumbnailUrl = _anime.ThumbnailUrl;

                        _anime = details;
                        DataContext = _anime;
                        TitleText.Text = _anime.Title;
                        HeaderTitleText.Text = _anime.Title;

                        if (!string.IsNullOrEmpty(_anime.ThumbnailUrl))
                        {
                            AsyncImageLoader.SetSourceUrl(CoverImage, _anime.ThumbnailUrl);
                        }

                        SynopsisText.Text = string.IsNullOrWhiteSpace(_anime.Synopsis) ? "Sinopsis no disponible." : _anime.Synopsis;
                    });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("MobileAnimeDetailsView.Details", ex);
                Dispatcher.UIThread.Invoke(() => SynopsisText.Text = "Error cargando sinopsis.");
            }

            // Update episodes
            try
            {
                var episodes = await episodesTask;
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (episodes != null && episodes.Count > 0)
                    {
                        StatusText.IsVisible = false;
                        var viewModels = new List<EpisodeViewModel>();
                        foreach (var ep in episodes)
                        {
                            var vm = new EpisodeViewModel(ep);
                            UpdateEpisodeViewModelState(vm);
                            viewModels.Add(vm);
                        }
                        _episodeViewModels = viewModels;
                        EpisodesList.ItemsSource = _episodeViewModels;
                    }
                    else
                    {
                        StatusText.Text = "No se encontraron episodios.";
                    }
                });
            }
            catch (Exception ex)
            {
                AppLogger.Error("MobileAnimeDetailsView.Episodes", ex);
                Dispatcher.UIThread.Invoke(() =>
                {
                    StatusText.Text = $"Error al obtener episodios: {ex.Message}";
                    RetryBtn.IsVisible = true;
                });
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("MobileAnimeDetailsView.LoadAnimeInfoAsync", ex);
            StatusText.Text = $"Error inesperado: {ex.Message}";
            RetryBtn.IsVisible = true;
        }
    }

    private void UpdateEpisodeViewModelState(EpisodeViewModel vm)
    {
        try
        {
            var history = new History.WatchHistory();
            var historyEntry = history.GetAll().FirstOrDefault(h => h.AnimeUrl.Equals(_anime.Url, StringComparison.OrdinalIgnoreCase));
            if (historyEntry != null && historyEntry.LastEpisodeNumber == vm.EpisodeNumber)
            {
                vm.WatchStatus = historyEntry.IsCompleted ? EpisodeWatchStatus.Completed : EpisodeWatchStatus.InProgress;
            }
            else
            {
                var downloadedEp = DownloadManager.GetDownloadedEpisode(_anime.Url, vm.EpisodeNumber);
                if (downloadedEp != null)
                {
                    vm.WatchStatus = downloadedEp.Status;
                }
                else
                {
                    vm.WatchStatus = EpisodeWatchStatus.Unwatched;
                }
            }

            if (DownloadManager.IsEpisodeDownloaded(_anime.Url, vm.EpisodeNumber))
            {
                vm.DownloadText = "Descargado";
                vm.DownloadIcon = "Check";
                vm.CanDownload = false;
                vm.IsDownloading = false;
                vm.IsDownloaded = true;
            }
            else
            {
                vm.IsDownloaded = false;
                var active = DownloadManager.GetActiveDownload(_anime.Url, vm.EpisodeNumber);
                if (active != null)
                {
                    vm.DownloadText = active.StatusText;
                    vm.DownloadIcon = active.StatusIcon;
                    vm.CanDownload = false;
                    vm.IsDownloading = active.State == DownloadState.Downloading;
                }
                else
                {
                    vm.DownloadText = "Descargar";
                    vm.DownloadIcon = "Download";
                    vm.CanDownload = true;
                    vm.IsDownloading = false;
                }
            }
        }
        catch { }
    }

    private async void OnEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            btn.IsEnabled = false;
            try
            {
                await ProceedWithPlay(vm);
            }
            catch (Exception ex)
            {
                AppLogger.Error("MobileAnimeDetailsView.OnEpisodeClicked", ex);
                StatusText.Text = $"Error: {ex.Message}";
                StatusText.IsVisible = true;
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }

    private async Task ProceedWithPlay(EpisodeViewModel vm)
    {
        StatusText.Text = $"Cargando servidores: {vm.Title}...";
        StatusText.IsVisible = true;

        var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);
        var servers = await extractor.GetVideoServersAsync(vm.Url);

        if (servers == null || servers.Count == 0)
        {
            StatusText.Text = "No se encontraron servidores de video.";
            return;
        }

        VideoServer? chosenServer = servers[0];
        string chosenQuality = ConfigManager.Current.PreferredQuality;

        if (servers.Count > 1)
        {
            chosenServer = await ShowServerPickerModalAsync(servers, $"{_anime.Title} — {vm.Title}");
        }

        if (chosenServer == null)
        {
            StatusText.IsVisible = false;
            return;
        }

        Func<Task<string>> urlResolver = async () =>
        {
            var freshUrl = await extractor.ResolveVideoUrlAsync(chosenServer.Url);
            if (string.IsNullOrEmpty(freshUrl))
            {
                var resolved = await _resolverBackend.ResolveAsync(chosenServer.Url, new ResolveOptions { Referer = chosenServer.Url });
                if (resolved.Type != MediaType.Unknown)
                    freshUrl = resolved.DirectUrl;
            }
            return freshUrl ?? string.Empty;
        };

        var history = new History.WatchHistory();
        history.Record(_anime.Title, _anime.Url, _anime.ThumbnailUrl, vm.EpisodeNumber, chosenServer.Url);
        _nowPlayingVm = vm;

        if (AndroidMainView.Current != null)
        {
            StatusText.IsVisible = false;
            var playerView = new MobileVideoPlayerView(
                _playerBackend,
                urlResolver,
                $"{_anime.Title} — {vm.Title}",
                chosenServer.Url,
                chosenQuality,
                animeTitle: _anime.Title,
                animeUrl: _anime.Url,
                thumbnailUrl: _anime.ThumbnailUrl,
                episodeNumber: vm.EpisodeNumber,
                episodeUrl: vm.Url);

            AndroidMainView.Current.PushPlayerView(playerView);
        }
    }

    private async Task<VideoServer?> ShowServerPickerModalAsync(List<VideoServer> servers, string title)
    {
        var tcs = new TaskCompletionSource<VideoServer?>();
        var container = new StackPanel { Spacing = 10, Margin = new Thickness(10) };

        var primaryBrush = this.TryFindResource("AppPrimaryColor", out var pRes) && pRes is IBrush pb ? pb : Brushes.MediumPurple;
        var surfaceBrush = this.TryFindResource("AppSurfaceColor", out var sRes) && sRes is IBrush sb ? sb : Brushes.DarkSlateGray;
        var titleBrush = this.TryFindResource("AppTitleColor", out var tRes) && tRes is IBrush tb ? tb : Brushes.White;
        var subtextBrush = this.TryFindResource("AppSubtextColor", out var subRes) && subRes is IBrush stb ? stb : Brushes.Gray;

        foreach (var srv in servers)
        {
            var stack = new StackPanel { Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center };
            var serverTitle = new TextBlock
            {
                Text = srv.Name,
                FontSize = 15,
                FontWeight = FontWeight.Bold,
                Foreground = titleBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            stack.Children.Add(serverTitle);

            var badgeColor = srv.IsDirectPlaySupported ? primaryBrush : subtextBrush;

            var badgeIcon = new Material.Icons.Avalonia.MaterialIcon
            {
                Kind = srv.IsDirectPlaySupported
                    ? Material.Icons.MaterialIconKind.CheckCircleOutline
                    : Material.Icons.MaterialIconKind.Server,
                Width = 14,
                Height = 14,
                Foreground = badgeColor,
                VerticalAlignment = VerticalAlignment.Center
            };

            var badgeBlock = new TextBlock
            {
                Text = srv.IsDirectPlaySupported ? "Reproducción Directa" : "Servidor Externo",
                FontSize = 11,
                FontWeight = FontWeight.SemiBold,
                Foreground = badgeColor,
                VerticalAlignment = VerticalAlignment.Center
            };

            var badgeStack = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 4,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            badgeStack.Children.Add(badgeIcon);
            badgeStack.Children.Add(badgeBlock);

            stack.Children.Add(badgeStack);

            var btn = new Button
            {
                Content = stack,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(14, 10),
                Background = surfaceBrush,
                BorderBrush = srv.IsDirectPlaySupported ? primaryBrush : subtextBrush,
                BorderThickness = new Thickness(srv.IsDirectPlaySupported ? 1.5 : 0.5),
                CornerRadius = new CornerRadius(8)
            };

            var capturedServer = srv;
            btn.Click += (s, e) =>
            {
                CloseAndroidModal();
                tcs.TrySetResult(capturedServer);
            };
            container.Children.Add(btn);
        }

        if (!ShowAndroidModal(title, container))
        {
            return servers.FirstOrDefault();
        }

        return await tcs.Task;
    }

    private bool ShowAndroidModal(string title, Control content)
    {
        Visual? current = this;
        while (current != null)
        {
            if (current is AndroidMainView mainView)
            {
                mainView.ShowModal(title, content);
                return true;
            }
            current = current.GetVisualParent();
        }
        return false;
    }

    private void CloseAndroidModal()
    {
        Visual? current = this;
        while (current != null)
        {
            if (current is AndroidMainView mainView)
            {
                mainView.CloseModal();
                return;
            }
            current = current.GetVisualParent();
        }
    }

    private async void OnDownloadEpisodeClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is EpisodeViewModel vm)
        {
            btn.IsEnabled = false;
            try
            {
                if (DownloadManager.IsEpisodeDownloaded(_anime.Url, vm.EpisodeNumber))
                {
                    StatusText.Text = "Este episodio ya está descargado.";
                    StatusText.IsVisible = true;
                    return;
                }

                StatusText.Text = $"Preparando descarga de {vm.Title}...";
                StatusText.IsVisible = true;

                var extractor = ExtractorFactory.GetExtractorForUrl(_anime.Url);
                var servers = await extractor.GetVideoServersAsync(vm.Url);

                if (servers == null || servers.Count == 0)
                {
                    StatusText.Text = "No se encontraron servidores.";
                    return;
                }

                VideoServer? chosenServer = servers[0];
                if (servers.Count > 1)
                {
                    chosenServer = await ShowServerPickerModalAsync(servers, $"Descargar {vm.Title}");
                }

                if (chosenServer == null) return;

                var videoUrl = await extractor.ResolveVideoUrlAsync(chosenServer.Url);
                if (string.IsNullOrEmpty(videoUrl))
                {
                    var resolved = await _resolverBackend.ResolveAsync(chosenServer.Url, new ResolveOptions { Referer = chosenServer.Url });
                    if (resolved.Type != MediaType.Unknown) videoUrl = resolved.DirectUrl;
                }

                if (!string.IsNullOrEmpty(videoUrl))
                {
                    StatusText.Text = $"Descarga iniciada para {vm.Title}";
                    var defaultDir = System.IO.Path.Combine(ConfigManager.BaseDataPath, "Downloads");
                    var safeTitle = string.Join("_", _anime.Title.Split(System.IO.Path.GetInvalidFileNameChars())).Trim();
                    if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime";
                    var animeDir = System.IO.Path.Combine(defaultDir, safeTitle);
                    if (!System.IO.Directory.Exists(animeDir)) System.IO.Directory.CreateDirectory(animeDir);

                    var episodeNumStr = string.IsNullOrWhiteSpace(vm.EpisodeNumber) ? "Desconocido" : vm.EpisodeNumber;
                    var outputPath = System.IO.Path.Combine(animeDir, $"Episodio {episodeNumStr}.mp4");

                    var mediaType = videoUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ? MediaType.Hls : MediaType.Mp4;
                    var resolvedMedia = new ResolvedMedia(
                        chosenServer.Url,
                        videoUrl,
                        mediaType,
                        chosenServer.Url,
                        ConfigManager.Current.RandomUserAgent);

                    var activeDownload = new ActiveDownload
                    {
                        AnimeTitle = _anime.Title,
                        AnimeUrl = _anime.Url,
                        ThumbnailUrl = _anime.ThumbnailUrl,
                        EpisodeUrl = vm.Url,
                        EpisodeNumber = vm.EpisodeNumber,
                        EpisodeTitle = vm.Title,
                        ServerUrl = chosenServer.Url,
                        DirectVideoUrl = videoUrl,
                        OutputPath = outputPath,
                        Progress = 0
                    };

                    DownloadManager.StartOrResumeDownloadAsync(activeDownload);
                    UpdateEpisodeViewModelState(vm);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("MobileAnimeDetailsView.OnDownloadEpisodeClicked", ex);
                StatusText.Text = $"Error al descargar: {ex.Message}";
            }
            finally
            {
                btn.IsEnabled = true;
            }
        }
    }
}
