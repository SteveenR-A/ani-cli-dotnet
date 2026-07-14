using System;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Threading.Tasks;
using AniCS.Extractors;
using AniCS.Models;
using System.Windows.Input;

namespace AniCS.Desktop.ViewModels;

// A simple ICommand implementation to bind buttons if needed, though we can still use code-behind clicks if preferred.
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();
    public void Execute(object? parameter) => _execute();
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public class HomeViewModel : ViewModelBase
{
    private static readonly HttpClient _httpClient = new HttpClient();
    
    // Lista general (Emisión - Modo Clásico)
    private ObservableCollection<AnimeResult> _animeList = new();
    public ObservableCollection<AnimeResult> AnimeList
    {
        get => _animeList;
        set => SetProperty(ref _animeList, value);
    }

    // Listas para el modo Android App
    private ObservableCollection<AnimeResult> _latestList = new();
    public ObservableCollection<AnimeResult> LatestList
    {
        get => _latestList;
        set => SetProperty(ref _latestList, value);
    }

    private ObservableCollection<AnimeResult> _premieresList = new();
    public ObservableCollection<AnimeResult> PremieresList
    {
        get => _premieresList;
        set => SetProperty(ref _premieresList, value);
    }

    private ObservableCollection<AnimeResult> _historyList = new();
    public ObservableCollection<AnimeResult> HistoryList
    {
        get => _historyList;
        set => SetProperty(ref _historyList, value);
    }

    private string _statusText = "Cargando...";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private bool _isStatusVisible = true;
    public bool IsStatusVisible
    {
        get => _isStatusVisible;
        set => SetProperty(ref _isStatusVisible, value);
    }

    private bool _isReloading = false;
    public bool IsReloading
    {
        get => _isReloading;
        set
        {
            if (SetProperty(ref _isReloading, value))
            {
                ReloadCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    private int _selectedIndex = 0;
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetProperty(ref _selectedIndex, value);
    }

    public RelayCommand ReloadCommand { get; }

    public HomeViewModel()
    {
        ReloadCommand = new RelayCommand(async () => await LoadDataAsync(), () => !IsReloading);
    }

    public async Task LoadDataAsync()
    {
        if (IsReloading) return;
        
        IsReloading = true;
        StatusText = "Cargando contenido...";
        IsStatusVisible = true;
        
        AnimeList.Clear();
        LatestList.Clear();
        PremieresList.Clear();
        HistoryList.Clear();

        var extractor = new JKAnimeExtractor(_httpClient);
        var watchHistory = new AniCS.History.WatchHistory();

        // Lanzar las tres tareas simultáneamente para que vayan renderizando apenas salgan
        var latestTask = LoadLatestAsync(extractor);
        var premieresTask = LoadPremieresAsync(extractor);
        var historyTask = LoadHistoryAsync(watchHistory);

        try
        {
            await Task.WhenAll(latestTask, premieresTask, historyTask);
        }
        catch (Exception ex)
        {
            StatusText = $"Error parcial: {ex.Message}";
        }
        finally
        {
            IsStatusVisible = false;
            IsReloading = false;
        }
    }

    private async Task LoadLatestAsync(JKAnimeExtractor extractor)
    {
        try
        {
            var episodes = await extractor.GetLatestReleasesAsync();
            var results = new ObservableCollection<AnimeResult>();
            
            foreach (var ep in episodes)
            {
                results.Add(new AnimeResult
                {
                    Title = ep.Title,
                    Description = $"Episodio {ep.EpisodeNumber}",
                    ThumbnailUrl = ep.ThumbnailUrl,
                    Url = extractor.NormalizeSeriesUrl(ep.Url)
                });
            }
            
            // Asignar a ambas listas (AnimeList para el modo clásico y LatestList para el modo App)
            if (results.Count > 0)
            {
                // Dispatching al UI thread no es estrictamente necesario en Avalonia si solo se reasigna la prop,
                // pero si la vista ya está ligada, Avalonia lo maneja.
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    AnimeList = new ObservableCollection<AnimeResult>(results);
                    LatestList = new ObservableCollection<AnimeResult>(results);
                    SelectedIndex = 0;
                });
            }
        }
        catch { /* Ignore */ }
    }

    private async Task LoadPremieresAsync(JKAnimeExtractor extractor)
    {
        try
        {
            var premieres = await extractor.GetPremieresAsync();
            if (premieres != null && premieres.Count > 0)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                {
                    PremieresList = new ObservableCollection<AnimeResult>(premieres);
                });
            }
        }
        catch { /* Ignore */ }
    }

    private async Task LoadHistoryAsync(AniCS.History.WatchHistory history)
    {
        try
        {
            // El historial local es rápido, pero usamos Task.Run para no bloquear
            await Task.Run(() =>
            {
                var entries = history.GetAll();
                var results = new ObservableCollection<AnimeResult>();
                
                foreach (var entry in entries)
                {
                    results.Add(new AnimeResult
                    {
                        Title = entry.AnimeTitle,
                        Description = $"Visto: Ep {entry.LastEpisodeNumber}",
                        ThumbnailUrl = entry.AnimeThumbnailUrl, 
                        Url = entry.AnimeUrl
                    });
                }

                if (results.Count > 0)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => 
                    {
                        HistoryList = results;
                    });
                }
            });
        }
        catch { /* Ignore */ }
    }
}
