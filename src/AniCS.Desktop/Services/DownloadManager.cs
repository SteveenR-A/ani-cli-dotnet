using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AniCS.Models;
using AniCS.Resolver;
using AniCS.Extractors;

namespace AniCS.Desktop.Services;

public enum DownloadState
{
    Pending,
    Downloading,
    Completed,
    Error,
    Cancelled,
    Paused
}

public class ActiveDownload : INotifyPropertyChanged
{
    public string AnimeTitle { get; set; } = string.Empty;
    public string AnimeUrl { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public string EpisodeUrl { get; set; } = string.Empty;
    public string EpisodeNumber { get; set; } = string.Empty;
    public string EpisodeTitle { get; set; } = string.Empty;

    public string ServerUrl { get; set; } = string.Empty;
    public string DirectVideoUrl { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int RetryAttempt { get; set; } = 0;
    public const int MaxRetries = 3;

    private double _progress;
    public double Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private string _sizeText = string.Empty;
    public string SizeText
    {
        get => _sizeText;
        set { _sizeText = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    private DownloadState _state = DownloadState.Pending;
    public DownloadState State
    {
        get => _state;
        set
        {
            _state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusIcon));
            OnPropertyChanged(nameof(PauseResumeText));
            OnPropertyChanged(nameof(PauseResumeIcon));
            OnPropertyChanged(nameof(CanPauseOrCancel));
        }
    }

    public bool CanPauseOrCancel => State != DownloadState.Completed && State != DownloadState.Cancelled;

    public string PauseResumeText => State switch
    {
        DownloadState.Paused => "Reanudar",
        DownloadState.Error => "Reintentar",
        _ => "Pausar"
    };

    public string PauseResumeIcon => State switch
    {
        DownloadState.Paused => "Play",
        DownloadState.Error => "Refresh",
        _ => "Pause"
    };

    public string StatusText => State switch
    {
        DownloadState.Pending => "Iniciando descarga...",
        DownloadState.Downloading => string.IsNullOrWhiteSpace(SizeText)
            ? $"Descargando... {Progress:F1}%"
            : $"Descargando... {SizeText} ({Progress:F1}%)",
        DownloadState.Completed => "Descargado",
        DownloadState.Error => "Error",
        DownloadState.Cancelled => "Cancelado",
        DownloadState.Paused => "Pausado",
        _ => State.ToString()
    };


    public string StatusIcon => State switch
    {
        DownloadState.Downloading => "Download",
        DownloadState.Completed => "Check",
        DownloadState.Error => "Close",
        DownloadState.Cancelled => "Cancel",
        DownloadState.Paused => "Pause",
        _ => "Information"
    };

    public CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();

    public void Pause()
    {
        if (State == DownloadState.Downloading)
        {
            State = DownloadState.Paused;
            CancellationTokenSource.Cancel();
        }
    }

    public void Resume()
    {
        if (State == DownloadState.Paused)
        {
            State = DownloadState.Downloading;
            CancellationTokenSource = new CancellationTokenSource();
        }
    }

    public void Cancel()
    {
        if (State == DownloadState.Downloading || State == DownloadState.Paused)
        {
            State = DownloadState.Cancelled;
            CancellationTokenSource.Cancel();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum EpisodeWatchStatus
{
    Unwatched,
    InProgress,
    Completed
}

public class DownloadedEpisode : INotifyPropertyChanged
{
    public string EpisodeNumber { get; set; } = string.Empty;
    public string EpisodeTitle { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime DownloadedAt { get; set; } = DateTime.Now;

    private EpisodeWatchStatus _status = EpisodeWatchStatus.Unwatched;
    public EpisodeWatchStatus Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusColorResource));
                OnPropertyChanged(nameof(NextStatusTooltip));
            }
        }
    }

    [JsonIgnore]
    public string StatusText => Status switch
    {
        EpisodeWatchStatus.Completed => "Terminado",
        EpisodeWatchStatus.InProgress => "En progreso",
        _ => "Sin ver"
    };

    [JsonIgnore]
    public string StatusIcon => Status switch
    {
        EpisodeWatchStatus.Completed => "CheckCircleOutline",
        EpisodeWatchStatus.InProgress => "PlayCircleOutline",
        _ => "EyeOffOutline"
    };

    [JsonIgnore]
    public string StatusColorResource => Status switch
    {
        EpisodeWatchStatus.Completed => "AppStatusCompletedColor",
        EpisodeWatchStatus.InProgress => "AppStatusInProgressColor",
        _ => "AppStatusUnwatchedColor"
    };

    [JsonIgnore]
    public string NextStatusTooltip => Status switch
    {
        EpisodeWatchStatus.Unwatched => "Marcar como En progreso",
        EpisodeWatchStatus.InProgress => "Marcar como Terminado",
        EpisodeWatchStatus.Completed => "Marcar como Sin ver",
        _ => "Cambiar estado"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DownloadedAnime : INotifyPropertyChanged
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public List<DownloadedEpisode> Episodes { get; set; } = new();

    [JsonIgnore]
    public List<DownloadedEpisode> RegularEpisodes => Episodes
        .Where(e => e.EpisodeNumber != "Opening" && !e.EpisodeNumber.Equals("Trailer", StringComparison.OrdinalIgnoreCase))
        .ToList();

    [JsonIgnore]
    public List<DownloadedEpisode> SpecialEpisodes => Episodes
        .Where(e => e.EpisodeNumber == "Opening" || e.EpisodeNumber.Equals("Trailer", StringComparison.OrdinalIgnoreCase))
        .ToList();

    [JsonIgnore]
    public bool HasSpecialEpisodes => SpecialEpisodes.Count > 0;

    [JsonIgnore]
    public bool HasRegularEpisodes => RegularEpisodes.Count > 0;

    private bool _isExpanded;
    [JsonIgnore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}


public static class DownloadManager
{
    private static string ConfigDir => ConfigManager.BaseDataPath;
    private static string DownloadsFile => Path.Combine(ConfigManager.BaseDataPath, "downloads.json");

    public static string DefaultDownloadDirectory
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                // En Windows la ruta oficial de descargas es siempre Videos\AniCS (C:\Users\<usuario>\Videos\AniCS)
                var userVideos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Videos", "AniCS");
                if (Directory.Exists(userVideos))
                    return userVideos;

                var specialVideos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                if (!string.IsNullOrEmpty(specialVideos))
                    return Path.Combine(specialVideos, "AniCS");

                return userVideos;
            }

            return Path.Combine(ConfigManager.BaseDataPath, "Downloads");
        }
    }

    private static List<DownloadedAnime> _downloads = new();
    
    public static ObservableCollection<ActiveDownload> ActiveDownloads { get; } = new();
    
    public static event EventHandler? DownloadsChanged;

    public static Action<string, string, int>? OnDownloadProgressNotify { get; set; }
    public static Action? OnDownloadsStarted { get; set; }
    public static Action? OnDownloadsFinished { get; set; }

    private static readonly object _runningLock = new();
    private static readonly HashSet<ActiveDownload> _runningDownloads = new();

    public static void StartOrResumeDownloadAsync(ActiveDownload active)
    {
        lock (_runningLock)
        {
            if (_runningDownloads.Contains(active))
                return;
            _runningDownloads.Add(active);
        }

        active.State = DownloadState.Downloading;
        active.CancellationTokenSource?.Dispose();
        active.CancellationTokenSource = new CancellationTokenSource();
        var token = active.CancellationTokenSource.Token;

        AddActiveDownload(active);
        OnDownloadsStarted?.Invoke();

        var baseDir = DefaultDownloadDirectory;
        var safeTitle = string.Join("_", active.AnimeTitle.Split(Path.GetInvalidFileNameChars())).Trim();
        if (string.IsNullOrWhiteSpace(safeTitle)) safeTitle = "Anime";
        var animeDir = Path.Combine(baseDir, safeTitle);
        if (!Directory.Exists(animeDir)) Directory.CreateDirectory(animeDir);

        var episodeNumStr = string.IsNullOrWhiteSpace(active.EpisodeNumber) ? "Desconocido" : active.EpisodeNumber;
        if (string.IsNullOrEmpty(active.OutputPath))
        {
            active.OutputPath = Path.Combine(animeDir, $"Episodio {episodeNumStr}.mp4");
        }

        var rng = new Random();

        _ = Task.Run(async () =>
        {
            try
            {
                var resolverBackend = ResolverFactory.CreateFromConfig();

                while (active.RetryAttempt <= ActiveDownload.MaxRetries && !token.IsCancellationRequested)
                {
                    try
                    {
                        // Si la URL directa del video está vacía o falló en reintentos, resolverla de nuevo
                        if (string.IsNullOrEmpty(active.DirectVideoUrl) && !string.IsNullOrEmpty(active.ServerUrl))
                        {
                            active.SizeText = "Resolviendo enlace del servidor...";
                            var extractor = ExtractorFactory.GetExtractor();
                            active.DirectVideoUrl = await extractor.ResolveVideoUrlAsync(active.ServerUrl);
                            if (string.IsNullOrEmpty(active.DirectVideoUrl))
                            {
                                var res = await resolverBackend.ResolveAsync(active.ServerUrl, new ResolveOptions { Referer = active.ServerUrl });
                                if (res.Type != MediaType.Unknown) active.DirectVideoUrl = res.DirectUrl;
                            }
                        }
                        else if (string.IsNullOrEmpty(active.DirectVideoUrl) && !string.IsNullOrEmpty(active.EpisodeUrl))
                        {
                            active.SizeText = "Obteniendo enlaces de video...";
                            var extractor = ExtractorFactory.GetExtractor();
                            var servers = await extractor.GetVideoServersAsync(active.EpisodeUrl);
                            if (servers.Count > 0)
                            {
                                var chosen = servers.FirstOrDefault(s => s.IsDirectPlaySupported) ?? servers.First();
                                active.ServerUrl = chosen.Url;
                                active.DirectVideoUrl = await extractor.ResolveVideoUrlAsync(chosen.Url);
                                if (string.IsNullOrEmpty(active.DirectVideoUrl))
                                {
                                    var res = await resolverBackend.ResolveAsync(chosen.Url, new ResolveOptions { Referer = chosen.Url });
                                    if (res.Type != MediaType.Unknown) active.DirectVideoUrl = res.DirectUrl;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(active.DirectVideoUrl))
                        {
                            throw new InvalidOperationException("No se pudo obtener el enlace directo del video.");
                        }

                        var mediaType = active.DirectVideoUrl.Contains(".m3u8", StringComparison.OrdinalIgnoreCase)
                            ? MediaType.Hls
                            : MediaType.Mp4;

                        var resolvedMedia = new ResolvedMedia(
                            active.ServerUrl,
                            active.DirectVideoUrl,
                            mediaType,
                            active.ServerUrl,
                            ConfigManager.Current.RandomUserAgent);

                        var progress = new Progress<DownloadProgress>(p =>
                        {
                            active.Progress = p.Percent;
                            if (!string.IsNullOrEmpty(p.SizeInfo))
                            {
                                active.SizeText = p.SizeInfo;
                            }
                            OnDownloadProgressNotify?.Invoke(
                                active.AnimeTitle,
                                $"{active.EpisodeTitle}: {p.Percent:F0}% ({p.SizeInfo})",
                                (int)p.Percent);
                        });

                        var result = await resolverBackend.DownloadAsync(resolvedMedia, active.OutputPath, progress, token);

                        if (result.Code == DownloadResultCode.Success && result.OutputPath != null)
                        {
                            active.State = DownloadState.Completed;
                            active.SizeText = "Descargado";
                            RecordDownload(
                                active.AnimeTitle,
                                active.AnimeUrl,
                                active.ThumbnailUrl,
                                active.EpisodeNumber,
                                active.EpisodeTitle,
                                result.OutputPath);

                            OnDownloadProgressNotify?.Invoke(
                                active.AnimeTitle,
                                $"{active.EpisodeTitle} descargado con éxito",
                                100);

                            // Ocultar de descargas activas tras 1.2 segundos para transición limpia
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(1200);
                                RemoveActiveDownload(active);
                            });
                            break;
                        }
                        else if (result.Code == DownloadResultCode.Cancelled)
                        {
                            if (active.State == DownloadState.Paused)
                            {
                                active.SizeText = "Pausado";
                            }
                            else
                            {
                                active.State = DownloadState.Cancelled;
                                active.SizeText = "Cancelado";
                                CleanupPartialFiles(baseDir, safeTitle, episodeNumStr);
                            }
                            break;
                        }
                        else
                        {
                            throw new Exception(result.ErrorMessage ?? "Error durante la descarga");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        if (active.State == DownloadState.Paused)
                        {
                            active.SizeText = "Pausado";
                        }
                        else
                        {
                            active.State = DownloadState.Cancelled;
                            active.SizeText = "Cancelado";
                            CleanupPartialFiles(baseDir, safeTitle, episodeNumStr);
                        }
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested)
                        {
                            if (active.State == DownloadState.Paused)
                            {
                                active.SizeText = "Pausado";
                            }
                            else
                            {
                                active.State = DownloadState.Cancelled;
                                active.SizeText = "Cancelado";
                                CleanupPartialFiles(baseDir, safeTitle, episodeNumStr);
                            }
                            break;
                        }

                        active.RetryAttempt++;
                        if (active.RetryAttempt <= ActiveDownload.MaxRetries)
                        {
                            // Backoff aleatorio entre 1000ms y 3000ms con jitter
                            int minDelay = 1000 + (active.RetryAttempt - 1) * 800;
                            int maxDelay = minDelay + 1000;
                            int jitterMs = rng.Next(minDelay, maxDelay);

                            active.SizeText = $"Reconectando ({active.RetryAttempt}/{ActiveDownload.MaxRetries}) en {(jitterMs / 1000.0):0.0}s...";
                            AppLogger.Warn("DownloadManager", $"Download retry {active.RetryAttempt}/{ActiveDownload.MaxRetries} for {active.EpisodeTitle} in {jitterMs}ms: {ex.Message}");

                            // Si el error fue por enlace expirado/403, limpiar para forzar re-resolución
                            active.DirectVideoUrl = string.Empty;

                            await Task.Delay(jitterMs, token);
                        }
                        else
                        {
                            active.State = DownloadState.Error;
                            active.SizeText = "Error de conexión tras 3 intentos";
                            AppLogger.Error("DownloadManager.StartOrResumeDownloadAsync", ex);
                        }
                    }
                }
            }
            finally
            {
                lock (_runningLock)
                {
                    _runningDownloads.Remove(active);
                }

                if (ActiveDownloads.All(d => d.State != DownloadState.Downloading))
                {
                    OnDownloadsFinished?.Invoke();
                }
            }
        });
    }

    public static double ParseEpisodeNumber(string epNum)
    {
        if (string.IsNullOrWhiteSpace(epNum)) return double.MaxValue;
        var match = System.Text.RegularExpressions.Regex.Match(epNum, @"\d+(?:\.\d+)?");
        if (match.Success && double.TryParse(match.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double num))
        {
            return num;
        }
        return double.MaxValue;
    }

    public static void SortEpisodes(DownloadedAnime anime)
    {
        if (anime?.Episodes == null) return;
        anime.Episodes = anime.Episodes
            .OrderBy(e => ParseEpisodeNumber(e.EpisodeNumber))
            .ThenBy(e => e.EpisodeNumber)
            .ToList();
    }

    private static bool _isLoaded = false;

    public static void EnsureLoaded()
    {
        if (!_isLoaded)
        {
            Load();
        }
    }

    public static void Load()
    {
        _isLoaded = true;
        try
        {
            if (!File.Exists(DownloadsFile))
            {
                // Migración automática de downloads.json legacy si existe en ~/.config/anics/
                var legacyPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".config", "anics", "downloads.json");
                if (File.Exists(legacyPath))
                {
                    try
                    {
                        Directory.CreateDirectory(ConfigDir);
                        File.Copy(legacyPath, DownloadsFile, true);
                    }
                    catch { }
                }
            }

            if (File.Exists(DownloadsFile))
            {
                var json = File.ReadAllText(DownloadsFile);
                _downloads = JsonSerializer.Deserialize<List<DownloadedAnime>>(json) ?? new();
                foreach (var anime in _downloads)
                {
                    SortEpisodes(anime);
                }
                CleanupMissingFiles();
            }
        }
        catch { _downloads = new(); }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_downloads, options);
            File.WriteAllText(DownloadsFile, json);
        }
        catch { }
    }

    private static void CleanupMissingFiles()
    {
        bool changed = false;
        var defaultDir = DefaultDownloadDirectory;

        foreach (var anime in _downloads.ToList())
        {
            var validEpisodes = new List<DownloadedEpisode>();

            foreach (var ep in anime.Episodes)
            {
                if (File.Exists(ep.FilePath))
                {
                    validEpisodes.Add(ep);
                }
                else
                {
                    // Intento de reubicación inteligente si la ruta base cambió (ej. OneDrive vs Carpeta Local de Videos)
                    var safeTitle = string.Join("_", anime.Title.Split(Path.GetInvalidFileNameChars())).Trim();
                    var fileName = Path.GetFileName(ep.FilePath);
                    var candidatePath = Path.Combine(defaultDir, safeTitle, fileName);

                    if (File.Exists(candidatePath))
                    {
                        ep.FilePath = candidatePath;
                        validEpisodes.Add(ep);
                        changed = true;
                    }
                }
            }

            if (validEpisodes.Count != anime.Episodes.Count)
            {
                anime.Episodes = validEpisodes;
                changed = true;
            }

            SortEpisodes(anime);

            if (anime.Episodes.Count == 0)
            {
                _downloads.Remove(anime);
                changed = true;
            }
        }
        
        if (changed)
            Save();
    }

    /// <summary>
    /// Escanea la carpeta base de descargas (<c>Videos\AniCS</c>) en busca de archivos
    /// de episodios que existen físicamente en disco pero <b>no están registrados</b> en
    /// <c>downloads.json</c>. Los importa automáticamente como entradas "huérfanas".
    /// </summary>
    /// <returns>Número de episodios nuevos importados.</returns>
    public static int ScanDiskDownloads()
    {
        EnsureLoaded();
        var baseDir = DefaultDownloadDirectory;

        if (!Directory.Exists(baseDir)) return 0;

        // Extensiones de video reconocidas
        var videoExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp4", ".ts", ".mkv", ".avi", ".webm" };

        // Patrón: "Episodio 6.mp4", "Episodio 10.ts", "Episodio 1.5.mp4", "Episodio Opening.mp4", "1.mp4", etc.
        var episodePattern = new System.Text.RegularExpressions.Regex(
            @"^(?:Episodio\s+)?([\d]+(?:[.,][\d]+)?|Opening|Trailer)\b",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        int added = 0;
        bool changed = false;

        foreach (var animeDir in Directory.EnumerateDirectories(baseDir))
        {
            // Restaurar el nombre del anime: los '_' de reemplazo no se pueden deshacer
            // sin ambigüedad, así que usamos el nombre de carpeta tal cual.
            var folderName = Path.GetFileName(animeDir);

            // Buscar una entrada existente por nombre de carpeta (comparando el título
            // con guiones o con el título original después de limpiar caracteres inválidos).
            var existing = _downloads.FirstOrDefault(a =>
            {
                var safe = string.Join("_", a.Title.Split(Path.GetInvalidFileNameChars())).Trim();
                return string.Equals(safe, folderName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(a.Title, folderName, StringComparison.OrdinalIgnoreCase);
            });

            // Si no existe ninguna entrada, crearla con los datos mínimos disponibles.
            // Url y ThumbnailUrl quedarán vacíos — el usuario podrá navegar al anime
            // manualmente para completarlos.
            bool isNew = existing == null;
            var animeEntry = existing ?? new DownloadedAnime
            {
                Title       = folderName.Replace('_', ' ').Trim(),
                Url         = string.Empty,
                ThumbnailUrl = string.Empty
            };

            foreach (var file in Directory.EnumerateFiles(animeDir))
            {
                var ext = Path.GetExtension(file);
                if (!videoExts.Contains(ext)) continue;

                var fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                var match = episodePattern.Match(fileNameNoExt);
                if (!match.Success) continue;

                var epNumber = match.Groups[1].Value.Replace(',', '.');

                // Comprobar si ya está registrado con cualquier ruta
                bool alreadyRegistered = animeEntry.Episodes.Any(ep =>
                    ep.EpisodeNumber == epNumber
                    || string.Equals(ep.FilePath, file, StringComparison.OrdinalIgnoreCase));

                if (!alreadyRegistered)
                {
                    animeEntry.Episodes.Add(new DownloadedEpisode
                    {
                        EpisodeNumber = epNumber,
                        EpisodeTitle  = $"Episodio {epNumber}",
                        FilePath      = file,
                        DownloadedAt  = File.GetLastWriteTime(file)
                    });
                    added++;
                    changed = true;
                }
            }

            if (isNew && animeEntry.Episodes.Count > 0)
            {
                SortEpisodes(animeEntry);
                _downloads.Insert(0, animeEntry);
            }
            else if (!isNew && changed)
            {
                SortEpisodes(animeEntry);
            }
        }

        if (changed)
        {
            Save();
            DownloadsChanged?.Invoke(null, EventArgs.Empty);
        }

        return added;
    }
    
    public static ActiveDownload? GetActiveDownload(string animeUrl, string episodeNumber)
    {
        return ActiveDownloads.FirstOrDefault(d => d.AnimeUrl == animeUrl && d.EpisodeNumber == episodeNumber);
    }
    
    public static void AddActiveDownload(ActiveDownload download)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            // Remove any existing active download for the same episode
            var existing = GetActiveDownload(download.AnimeUrl, download.EpisodeNumber);
            if (existing != null)
            {
                ActiveDownloads.Remove(existing);
            }
            
            ActiveDownloads.Insert(0, download);
            DownloadsChanged?.Invoke(null, EventArgs.Empty);
        });
    }
    
    public static void RemoveActiveDownload(ActiveDownload download)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (ActiveDownloads.Contains(download))
            {
                ActiveDownloads.Remove(download);
                DownloadsChanged?.Invoke(null, EventArgs.Empty);
            }
        });
    }

    public static void RecordDownload(string animeTitle, string animeUrl, string thumbnailUrl, string episodeNumber, string episodeTitle, string filePath)
    {
        Load(); // Reload to sync with possible other instances
        var anime = _downloads.FirstOrDefault(a => 
            (!string.IsNullOrEmpty(animeUrl) && a.Url == animeUrl) ||
            (!string.IsNullOrEmpty(animeTitle) && string.Equals(a.Title, animeTitle, StringComparison.OrdinalIgnoreCase)));
        if (anime == null)
        {
            anime = new DownloadedAnime
            {
                Title = animeTitle,
                Url = animeUrl,
                ThumbnailUrl = thumbnailUrl
            };
            _downloads.Insert(0, anime);
        }
        else
        {
            if (string.IsNullOrEmpty(anime.Url) && !string.IsNullOrEmpty(animeUrl))
                anime.Url = animeUrl;
            if (string.IsNullOrEmpty(anime.ThumbnailUrl) && !string.IsNullOrEmpty(thumbnailUrl))
                anime.ThumbnailUrl = thumbnailUrl;
        }

        var ep = anime.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
        if (ep != null)
        {
            ep.FilePath = filePath;
            ep.DownloadedAt = DateTime.Now;
        }
        else
        {
            anime.Episodes.Add(new DownloadedEpisode
            {
                EpisodeNumber = episodeNumber,
                EpisodeTitle = episodeTitle,
                FilePath = filePath,
                DownloadedAt = DateTime.Now
            });
        }
        SortEpisodes(anime);
        Save();
        DownloadsChanged?.Invoke(null, EventArgs.Empty);
    }

    public static void LinkAnimeUrl(string title, string url, string thumbnailUrl)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(url)) return;
        Load();
        var anime = _downloads.FirstOrDefault(a => 
            string.Equals(a.Title, title, StringComparison.OrdinalIgnoreCase) ||
            (!string.IsNullOrEmpty(a.Url) && a.Url == url));
        if (anime != null)
        {
            anime.Url = url;
            if (string.IsNullOrEmpty(anime.ThumbnailUrl) && !string.IsNullOrEmpty(thumbnailUrl))
                anime.ThumbnailUrl = thumbnailUrl;
            Save();
            DownloadsChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static IReadOnlyList<DownloadedAnime> GetAll()
    {
        EnsureLoaded();
        CleanupMissingFiles();
        foreach (var anime in _downloads)
        {
            SortEpisodes(anime);
        }
        return _downloads.AsReadOnly();
    }

    public static void DeleteEpisode(string animeUrl, string episodeNumber)
    {
        EnsureLoaded();
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime != null)
        {
            var ep = anime.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
            if (ep != null)
            {
                if (File.Exists(ep.FilePath))
                {
                    try 
                    { 
                        File.Delete(ep.FilePath); 
                        var dir = Path.GetDirectoryName(ep.FilePath);
                        if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                        {
                            Directory.Delete(dir);
                        }
                    } 
                    catch { }
                }
                anime.Episodes.Remove(ep);
                if (anime.Episodes.Count == 0)
                {
                    _downloads.Remove(anime);
                }
                Save();
                DownloadsChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static void DeleteAnime(string animeUrl)
    {
        EnsureLoaded();
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime != null)
        {
            string? dir = null;
            foreach (var ep in anime.Episodes)
            {
                if (File.Exists(ep.FilePath))
                {
                    dir ??= Path.GetDirectoryName(ep.FilePath);
                    try { File.Delete(ep.FilePath); } catch { }
                }
            }
            if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try { Directory.Delete(dir); } catch { }
            }
            _downloads.Remove(anime);
            Save();
            DownloadsChanged?.Invoke(null, EventArgs.Empty);
        }
    }

    public static bool IsEpisodeDownloaded(string animeUrl, string episodeNumber, string? animeTitle = null)
    {
        EnsureLoaded();
        CleanupMissingFiles();
        var anime = _downloads.FirstOrDefault(a => 
            (!string.IsNullOrEmpty(animeUrl) && a.Url == animeUrl) ||
            (!string.IsNullOrEmpty(animeTitle) && string.Equals(a.Title, animeTitle, StringComparison.OrdinalIgnoreCase)));
        if (anime != null)
        {
            var ep = anime.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
            return ep != null && File.Exists(ep.FilePath);
        }
        return false;
    }

    public static DownloadedEpisode? GetDownloadedEpisode(string animeUrl, string episodeNumber, string? animeTitle = null)
    {
        EnsureLoaded();
        var anime = _downloads.FirstOrDefault(a => 
            (!string.IsNullOrEmpty(animeUrl) && a.Url == animeUrl) ||
            (!string.IsNullOrEmpty(animeTitle) && string.Equals(a.Title, animeTitle, StringComparison.OrdinalIgnoreCase)));
        return anime?.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
    }

    public static void UpdateEpisodeStatus(string animeUrl, string episodeNumber, EpisodeWatchStatus status)
    {
        EnsureLoaded();
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime != null)
        {
            var ep = anime.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
            if (ep != null && ep.Status != status)
            {
                ep.Status = status;
                Save();
                DownloadsChanged?.Invoke(null, EventArgs.Empty);
            }
        }
    }

    public static DownloadedEpisode? GetNextEpisode(string animeUrl, string currentEpisodeNumber)
    {
        EnsureLoaded();
        var anime = _downloads.FirstOrDefault(a => a.Url == animeUrl);
        if (anime != null && anime.Episodes.Count > 0)
        {
            SortEpisodes(anime);
            int idx = anime.Episodes.FindIndex(e => e.EpisodeNumber == currentEpisodeNumber);
            if (idx >= 0 && idx + 1 < anime.Episodes.Count)
            {
                return anime.Episodes[idx + 1];
            }
        }
        return null;
    }


    public static void CleanupPartialFiles(string downloadDir, string safeTitle, string episodeNumStr)
    {
        // Try multiple times since yt-dlp might take a moment to release the file lock
        for (int i = 0; i < 5; i++)
        {
            try
            {
                var animeDir = Path.Combine(downloadDir, safeTitle);
                if (Directory.Exists(animeDir))
                {
                    var files = Directory.GetFiles(animeDir, $"Episodio {episodeNumStr}.*");
                    bool allDeleted = true;
                    foreach (var f in files)
                    {
                        try { File.Delete(f); } catch { allDeleted = false; }
                    }
                    if (allDeleted)
                    {
                        if (!Directory.EnumerateFileSystemEntries(animeDir).Any())
                        {
                            Directory.Delete(animeDir);
                        }
                        break; // Success
                    }
                }
                else { break; }
            }
            catch { }
            System.Threading.Thread.Sleep(500); // Wait and retry
        }
    }
}
