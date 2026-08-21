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
    public const int MaxRetries = 5;

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
        DownloadState.Pending => "En cola para descargar...",
        DownloadState.Downloading => string.IsNullOrWhiteSpace(SizeText)
            ? $"Descargando... {Progress:F1}%"
            : $"Descargando... {SizeText} ({Progress:F1}%)",
        DownloadState.Completed => "Descargado",
        DownloadState.Error => string.IsNullOrWhiteSpace(SizeText) ? "Error" : SizeText,
        DownloadState.Cancelled => "Cancelado",
        DownloadState.Paused => "Pausado",
        _ => State.ToString()
    };

    public string StatusIcon => State switch
    {
        DownloadState.Pending => "ClockOutline",
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
        if (State == DownloadState.Downloading || State == DownloadState.Pending)
        {
            State = DownloadState.Paused;
            CancellationTokenSource.Cancel();
            DownloadManager.ProcessQueue();
        }
    }

    public void Resume()
    {
        if (State == DownloadState.Paused || State == DownloadState.Error)
        {
            State = DownloadState.Pending;
            RetryAttempt = 0;
            CancellationTokenSource?.Dispose();
            CancellationTokenSource = new CancellationTokenSource();
            DownloadManager.ProcessQueue();
        }
    }

    public void Cancel()
    {
        if (State == DownloadState.Downloading || State == DownloadState.Paused || State == DownloadState.Pending)
        {
            State = DownloadState.Cancelled;
            CancellationTokenSource.Cancel();
            DownloadManager.ProcessQueue();
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

    /// <summary>
    /// Ruta de descargas predeterminada provista por la plataforma en tiempo de ejecución (ej. DCIM/AniCS en Android).
    /// </summary>
    public static string? PlatformDefaultDownloadDirectory { get; set; }

    public static string SystemDefaultDownloadDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(PlatformDefaultDownloadDirectory))
                return PlatformDefaultDownloadDirectory;

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

            if (OperatingSystem.IsAndroid())
            {
                return Path.Combine("/storage/emulated/0", "DCIM", "AniCS");
            }

            return Path.Combine(ConfigManager.BaseDataPath, "Downloads");
        }
    }

    public static string DefaultDownloadDirectory
    {
        get
        {
            var custom = ConfigManager.Current.CustomDownloadDirectory;
            if (!string.IsNullOrWhiteSpace(custom))
            {
                try
                {
                    if (!Directory.Exists(custom))
                    {
                        Directory.CreateDirectory(custom);
                    }
                    return custom;
                }
                catch
                {
                    // Si falla el acceso o creación de la ruta personalizada, fallback a la ruta por defecto
                }
            }

            return SystemDefaultDownloadDirectory;
        }
    }

    public static void SetCustomDownloadDirectory(string? newPath)
    {
        var cfg = ConfigManager.Current;
        var cleanPath = newPath?.Trim() ?? string.Empty;

        if (!string.IsNullOrEmpty(cleanPath))
        {
            try
            {
                if (!Directory.Exists(cleanPath))
                {
                    Directory.CreateDirectory(cleanPath);
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error("DownloadManager.SetCustomDownloadDirectory", ex);
            }
        }

        cfg.CustomDownloadDirectory = cleanPath;
        ConfigManager.Save(cfg);

        // Re-escanear descargas en disco con la nueva ubicación
        ScanDiskDownloads();
    }

    private static List<DownloadedAnime> _downloads = new();
    
    public static ObservableCollection<ActiveDownload> ActiveDownloads { get; } = new();
    
    public static event EventHandler? DownloadsChanged;

    public static Action<string, string, int>? OnDownloadProgressNotify { get; set; }
    public static Action? OnDownloadsStarted { get; set; }
    public static Action? OnDownloadsFinished { get; set; }
    public static Action<string>? OnFileDownloaded { get; set; }

    private static readonly object _runningLock = new();
    private static readonly HashSet<ActiveDownload> _runningDownloads = new();

    public static void StartOrResumeDownloadAsync(ActiveDownload active)
    {
        active.RetryAttempt = 0;
        active.State = DownloadState.Pending;
        active.CancellationTokenSource?.Dispose();
        active.CancellationTokenSource = new CancellationTokenSource();

        AddActiveDownload(active);
        ProcessQueue();
    }

    public static void ProcessQueue()
    {
        lock (_runningLock)
        {
            int maxConcurrent = Math.Max(1, ConfigManager.Current.MaxConcurrentDownloads);

            int runningCount = _runningDownloads.Count;
            if (runningCount >= maxConcurrent)
                return;

            var pendingItems = ActiveDownloads
                .Where(d => d.State == DownloadState.Pending && !_runningDownloads.Contains(d))
                .ToList();

            foreach (var item in pendingItems)
            {
                if (_runningDownloads.Count >= maxConcurrent)
                    break;

                _runningDownloads.Add(item);
                item.State = DownloadState.Downloading;
                StartDownloadWorker(item);
            }

            if (_runningDownloads.Count > 0)
            {
                OnDownloadsStarted?.Invoke();
            }
        }
    }

    private static void StartDownloadWorker(ActiveDownload active)
    {
        var token = active.CancellationTokenSource.Token;

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

                while (active.RetryAttempt <= ActiveDownload.MaxRetries && !token.IsCancellationRequested && active.State == DownloadState.Downloading)
                {
                    try
                    {
                        var targetUrl = !string.IsNullOrEmpty(active.AnimeUrl) ? active.AnimeUrl : (!string.IsNullOrEmpty(active.EpisodeUrl) ? active.EpisodeUrl : active.ServerUrl);
                        var extractor = ExtractorFactory.GetExtractorForUrl(targetUrl);

                        // 1. Intentar resolver el ServerUrl asignado
                        if (string.IsNullOrEmpty(active.DirectVideoUrl) && !string.IsNullOrEmpty(active.ServerUrl))
                        {
                            active.SizeText = "Resolviendo enlace del servidor...";
                            active.DirectVideoUrl = await extractor.ResolveVideoUrlAsync(active.ServerUrl);
                            if (string.IsNullOrEmpty(active.DirectVideoUrl) || (!active.DirectVideoUrl.Contains(".m3u8") && !active.DirectVideoUrl.Contains(".mp4")))
                            {
                                var res = await resolverBackend.ResolveAsync(active.ServerUrl, new ResolveOptions { Referer = active.ServerUrl });
                                if (res.Type != MediaType.Unknown && !string.IsNullOrEmpty(res.DirectUrl))
                                {
                                    active.DirectVideoUrl = res.DirectUrl;
                                }
                            }
                        }

                        // 2. Si no se resolvió a un stream válido y tenemos EpisodeUrl, probar los demás servidores disponibles
                        if ((string.IsNullOrEmpty(active.DirectVideoUrl) || (!active.DirectVideoUrl.Contains(".m3u8") && !active.DirectVideoUrl.Contains(".mp4"))) && !string.IsNullOrEmpty(active.EpisodeUrl))
                        {
                            active.SizeText = "Buscando servidor disponible...";
                            var servers = await extractor.GetVideoServersAsync(active.EpisodeUrl);
                            foreach (var s in servers)
                            {
                                if (s.Url == active.ServerUrl) continue; // ya probado

                                var resolved = await extractor.ResolveVideoUrlAsync(s.Url);
                                if (string.IsNullOrEmpty(resolved) || (!resolved.Contains(".m3u8") && !resolved.Contains(".mp4")))
                                {
                                    var res = await resolverBackend.ResolveAsync(s.Url, new ResolveOptions { Referer = s.Url });
                                    if (res.Type != MediaType.Unknown && !string.IsNullOrEmpty(res.DirectUrl))
                                    {
                                        resolved = res.DirectUrl;
                                    }
                                }

                                if (!string.IsNullOrEmpty(resolved) && (resolved.Contains(".m3u8") || resolved.Contains(".mp4")))
                                {
                                    active.ServerUrl = s.Url;
                                    active.DirectVideoUrl = resolved;
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(active.DirectVideoUrl))
                        {
                            active.DirectVideoUrl = active.DirectVideoUrl.Replace("\\", "");
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

                            try
                            {
                                OnFileDownloaded?.Invoke(result.OutputPath);
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Debug("DownloadManager", $"OnFileDownloaded handler failed: {ex.Message}");
                            }

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
                    catch (InvalidOperationException ex)
                    {
                        // Error de resolución irrecuperable (servidor o enlace no disponible)
                        active.State = DownloadState.Error;
                        active.SizeText = "Servidor no disponible para descarga";
                        AppLogger.Warn("DownloadManager", $"Resolution failed for {active.EpisodeTitle}: {ex.Message}");
                        break;
                    }
                    catch (NotSupportedException ex)
                    {
                        // Servidor no compatible con descarga directa
                        active.State = DownloadState.Error;
                        active.SizeText = "Servidor no compatible";
                        AppLogger.Warn("DownloadManager", $"Unsupported server for {active.EpisodeTitle}: {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        if (token.IsCancellationRequested || active.State == DownloadState.Paused)
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
                            (int minDelay, int maxDelay) = active.RetryAttempt switch
                            {
                                1 => (3000, 5000),
                                2 => (10000, 15000),
                                3 => (25000, 35000),
                                4 => (50000, 65000),
                                _ => (70000, 85000)
                            };
                            int jitterMs = rng.Next(minDelay, maxDelay);

                            AppLogger.Warn("DownloadManager", $"Download retry {active.RetryAttempt}/{ActiveDownload.MaxRetries} for {active.EpisodeTitle} in {jitterMs}ms: {ex.Message}");

                            // Si el error fue por enlace expirado/403, limpiar para forzar re-resolución en el siguiente intento
                            active.DirectVideoUrl = string.Empty;

                            int totalSeconds = (int)Math.Ceiling(jitterMs / 1000.0);
                            for (int sec = totalSeconds; sec > 0; sec--)
                            {
                                if (token.IsCancellationRequested || active.State == DownloadState.Paused) break;
                                active.SizeText = $"Reintentando ({active.RetryAttempt}/{ActiveDownload.MaxRetries}) en {sec}s...";
                                await Task.Delay(1000, token);
                            }
                        }
                        else
                        {
                            active.State = DownloadState.Error;
                            active.SizeText = "Error de conexión tras 3 minutos";
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

                ProcessQueue();

                if (ActiveDownloads.All(d => d.State != DownloadState.Downloading && d.State != DownloadState.Pending))
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
                    catch (Exception ex)
                    {
                        AppLogger.Debug("DownloadManager", $"Failed to migrate legacy downloads file: {ex.Message}");
                    }
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
        catch (Exception ex)
        {
            AppLogger.Warn("DownloadManager", $"Failed to load downloads: {ex.Message}");
            _downloads = new();
        }
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
        catch (Exception ex)
        {
            AppLogger.Error("DownloadManager.Save", ex);
        }
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
            
            ActiveDownloads.Add(download);
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
                    catch (Exception ex)
                    {
                        AppLogger.Debug("DownloadManager", $"Failed to delete episode file '{ep.FilePath}': {ex.Message}");
                    }
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
                    try { File.Delete(ep.FilePath); } catch (Exception ex) { AppLogger.Debug("DownloadManager", $"Failed to delete ep file '{ep.FilePath}': {ex.Message}"); }
                }
            }
            if (dir != null && Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                try { Directory.Delete(dir); } catch (Exception ex) { AppLogger.Debug("DownloadManager", $"Failed to delete anime dir '{dir}': {ex.Message}"); }
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

    public static DownloadedEpisode? GetNextEpisode(string? animeUrl, string currentEpisodeNumber, string? animeTitle = null)
    {
        EnsureLoaded();
        var anime = _downloads.FirstOrDefault(a => 
            (!string.IsNullOrEmpty(animeUrl) && a.Url == animeUrl) ||
            (!string.IsNullOrEmpty(animeTitle) && string.Equals(a.Title, animeTitle, StringComparison.OrdinalIgnoreCase)));
        return anime != null ? GetNextEpisode(anime, currentEpisodeNumber) : null;
    }

    public static DownloadedEpisode? GetPreviousEpisode(string? animeUrl, string currentEpisodeNumber, string? animeTitle = null)
    {
        EnsureLoaded();
        var anime = _downloads.FirstOrDefault(a => 
            (!string.IsNullOrEmpty(animeUrl) && a.Url == animeUrl) ||
            (!string.IsNullOrEmpty(animeTitle) && string.Equals(a.Title, animeTitle, StringComparison.OrdinalIgnoreCase)));
        return anime != null ? GetPreviousEpisode(anime, currentEpisodeNumber) : null;
    }

    public static DownloadedEpisode? GetNextEpisode(DownloadedAnime anime, DownloadedEpisode currentEpisode)
    {
        return GetNextEpisode(anime, currentEpisode.EpisodeNumber, currentEpisode.FilePath);
    }

    public static DownloadedEpisode? GetPreviousEpisode(DownloadedAnime anime, DownloadedEpisode currentEpisode)
    {
        return GetPreviousEpisode(anime, currentEpisode.EpisodeNumber, currentEpisode.FilePath);
    }

    public static DownloadedEpisode? GetNextEpisode(DownloadedAnime anime, string currentEpisodeNumber, string? currentFilePath = null)
    {
        if (anime?.Episodes == null || anime.Episodes.Count == 0) return null;
        SortEpisodes(anime);

        var list = anime.RegularEpisodes.Where(e => File.Exists(e.FilePath)).ToList();
        if (list.Count == 0) list = anime.Episodes.Where(e => File.Exists(e.FilePath)).ToList();
        if (list.Count == 0) return null;

        int idx = -1;
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            idx = list.FindIndex(e => string.Equals(e.FilePath, currentFilePath, StringComparison.OrdinalIgnoreCase));
        }
        if (idx < 0)
        {
            idx = list.FindIndex(e => string.Equals(e.EpisodeNumber, currentEpisodeNumber, StringComparison.OrdinalIgnoreCase));
        }
        if (idx < 0)
        {
            double curNum = ParseEpisodeNumber(currentEpisodeNumber);
            if (curNum < double.MaxValue)
            {
                idx = list.FindIndex(e => ParseEpisodeNumber(e.EpisodeNumber) == curNum);
            }
        }

        if (idx >= 0 && idx + 1 < list.Count)
        {
            return list[idx + 1];
        }
        else if (idx < 0)
        {
            double curNum = ParseEpisodeNumber(currentEpisodeNumber);
            if (curNum < double.MaxValue)
            {
                return list.FirstOrDefault(e => ParseEpisodeNumber(e.EpisodeNumber) > curNum);
            }
        }

        return null;
    }

    public static DownloadedEpisode? GetPreviousEpisode(DownloadedAnime anime, string currentEpisodeNumber, string? currentFilePath = null)
    {
        if (anime?.Episodes == null || anime.Episodes.Count == 0) return null;
        SortEpisodes(anime);

        var list = anime.RegularEpisodes.Where(e => File.Exists(e.FilePath)).ToList();
        if (list.Count == 0) list = anime.Episodes.Where(e => File.Exists(e.FilePath)).ToList();
        if (list.Count == 0) return null;

        int idx = -1;
        if (!string.IsNullOrEmpty(currentFilePath))
        {
            idx = list.FindIndex(e => string.Equals(e.FilePath, currentFilePath, StringComparison.OrdinalIgnoreCase));
        }
        if (idx < 0)
        {
            idx = list.FindIndex(e => string.Equals(e.EpisodeNumber, currentEpisodeNumber, StringComparison.OrdinalIgnoreCase));
        }
        if (idx < 0)
        {
            double curNum = ParseEpisodeNumber(currentEpisodeNumber);
            if (curNum < double.MaxValue)
            {
                idx = list.FindIndex(e => ParseEpisodeNumber(e.EpisodeNumber) == curNum);
            }
        }

        if (idx > 0)
        {
            return list[idx - 1];
        }
        else if (idx < 0)
        {
            double curNum = ParseEpisodeNumber(currentEpisodeNumber);
            if (curNum < double.MaxValue)
            {
                return list.LastOrDefault(e => ParseEpisodeNumber(e.EpisodeNumber) < curNum);
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
            catch (Exception ex)
            {
                AppLogger.Debug("DownloadManager", $"CleanupPartialFiles attempt {i + 1} failed: {ex.Message}");
            }
            System.Threading.Thread.Sleep(500); // Wait and retry
        }
    }
}
