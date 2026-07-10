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
    
    private ObservableCollection<AnimeResult> _animeList = new();
    public ObservableCollection<AnimeResult> AnimeList
    {
        get => _animeList;
        set => SetProperty(ref _animeList, value);
    }

    private string _statusText = "Cargando animes recientes...";
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
        StatusText = "Cargando animes recientes...";
        IsStatusVisible = true;
        AnimeList.Clear();

        var extractor = new JKAnimeExtractor(_httpClient);
        
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
                    Url = ep.Url
                });
            }
            
            if (results.Count > 0)
            {
                IsStatusVisible = false;
                AnimeList = results;
                SelectedIndex = 0;
            }
            else
            {
                StatusText = "No se encontraron animes. Intenta recargar.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsReloading = false;
        }
    }
}
