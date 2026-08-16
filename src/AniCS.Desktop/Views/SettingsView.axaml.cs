using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Models;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Linq;

namespace AniCS.Desktop.Views;

public partial class SettingsView : UserControl
{
    private readonly Services.AppUpdateService _updater;

    public SettingsView()
    {
        InitializeComponent();
        _updater = App.Services?.GetService<Services.AppUpdateService>() ?? new Services.AppUpdateService();
        LoadConfig();
    }

    public void LoadConfig()
    {
        var config = ConfigManager.Current;

        var currentVersion = AniCS.Desktop.AppInfo.CurrentVersion;
        if (AppVersionText != null)
        {
            AppVersionText.Text = $"Versión: {currentVersion}";
        }

        CacheLimitInput.Value = config.MaxImageCacheCount;

        switch (config.DefaultPlayer.ToLower())
        {
            case "mpv": PlayerComboBox.SelectedIndex = 1; break;
            case "vlc": PlayerComboBox.SelectedIndex = 2; break;
            default: PlayerComboBox.SelectedIndex = 0; break;
        }

        CustomPlayerPathInput.Text = config.CustomPlayerExePath;

        if (DownloadDirectoryInput != null)
        {
            DownloadDirectoryInput.Text = !string.IsNullOrWhiteSpace(config.CustomDownloadDirectory)
                ? config.CustomDownloadDirectory
                : Services.DownloadManager.SystemDefaultDownloadDirectory;
        }

        RefreshThemeList(config);
        RefreshParadigmList(config);

        UseSpatialHudToggle.IsChecked = config.UseSpatialHud;
        CustomJkAnimeUrlInput.Text = config.CustomJkAnimeBaseUrl;

        // Backends de reproducción y resolución
        SelectComboByTag(PlayerBackendComboBox, config.PlayerBackend.ToString());
        SelectComboByTag(ResolverBackendComboBox, config.ResolverBackend.ToString());

        StatusMessage.IsVisible = false;

        ResetUpdateSection();
    }

    private async void OnBrowseDownloadDirClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
        {
            Title = "Seleccionar carpeta de descargas de AniCS",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var localPath = folders[0].Path.LocalPath;
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                DownloadDirectoryInput.Text = localPath;
            }
        }
    }

    private void OnResetDownloadDirClicked(object? sender, RoutedEventArgs e)
    {
        DownloadDirectoryInput.Text = Services.DownloadManager.SystemDefaultDownloadDirectory;
    }

    private void ResetUpdateSection()
    {
        if (UpdateStatusText == null) return;
        UpdateStatusText.Text = "Listo.";
        UpdateProgress.IsVisible = false;
        UpdateProgressText.IsVisible = false;
        if (CheckForUpdatesBtn != null) CheckForUpdatesBtn.IsEnabled = true;
        if (DownloadUpdateBtn != null)
        {
            DownloadUpdateBtn.IsVisible = false;
            DownloadUpdateBtn.IsEnabled = true;
        }
    }

    private void RefreshParadigmList(AppConfig config)
    {
        if (ParadigmComboBox == null) return;
        SelectComboByTag(ParadigmComboBox, config.UiParadigm);
    }

    /// <summary>Selecciona el item del ComboBox cuyo Tag coincide con el valor dado.</summary>
    private static void SelectComboByTag(ComboBox? combo, string tag)
    {
        if (combo == null) return;
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
        if (combo.Items.Count > 0) combo.SelectedIndex = 0;
    }

    private void RefreshThemeList(AppConfig config)
    {
        for (int i = 0; i < ThemeComboBox.Items.Count; i++)
        {
            if (ThemeComboBox.Items[i] is Avalonia.Controls.ComboBoxItem item && item.Tag?.ToString() == config.Theme)
            {
                ThemeComboBox.SelectedIndex = i;
                break;
            }
        }
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var config = ConfigManager.Current;

        config.MaxImageCacheCount = (int)(CacheLimitInput.Value ?? 30);

        config.DefaultPlayer = PlayerComboBox.SelectedIndex switch
        {
            1 => "Mpv",
            2 => "Vlc",
            _ => "Auto"
        };

        config.CustomPlayerExePath = CustomPlayerPathInput.Text ?? string.Empty;

        if (DownloadDirectoryInput != null)
        {
            var customDir = DownloadDirectoryInput.Text?.Trim() ?? string.Empty;
            if (customDir.Equals(Services.DownloadManager.SystemDefaultDownloadDirectory, System.StringComparison.OrdinalIgnoreCase))
            {
                customDir = string.Empty;
            }
            Services.DownloadManager.SetCustomDownloadDirectory(customDir);
        }

        if (ParadigmComboBox != null && ParadigmComboBox.SelectedItem is Avalonia.Controls.ComboBoxItem paradigmItem && paradigmItem.Tag != null)
        {
            config.UiParadigm = paradigmItem.Tag.ToString()!;
        }

        config.UseSpatialHud = UseSpatialHudToggle.IsChecked == true;
        if (CustomJkAnimeUrlInput != null)
            config.CustomJkAnimeBaseUrl = CustomJkAnimeUrlInput.Text?.Trim() ?? "https://jkanime.net";

        // Backends de reproducción y resolución
        if (PlayerBackendComboBox?.SelectedItem is ComboBoxItem playerItem && playerItem.Tag is string playerTag)
        {
            if (System.Enum.TryParse<PlayerBackendMode>(playerTag, out var playerMode))
                config.PlayerBackend = playerMode;
        }
        if (ResolverBackendComboBox?.SelectedItem is ComboBoxItem resolverItem && resolverItem.Tag is string resolverTag)
        {
            if (System.Enum.TryParse<ResolverBackendMode>(resolverTag, out var resolverMode))
                config.ResolverBackend = resolverMode;
        }

        ConfigManager.Save(config);
        DataCache.ClearRamCache();

        StatusMessage.Text = "¡Configuración guardada exitosamente!";
        StatusMessage.IsVisible = true;

        await Task.Delay(3000);
        StatusMessage.IsVisible = false;
    }

    private void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox != null && ThemeComboBox.SelectedItem is Avalonia.Controls.ComboBoxItem item && item.Tag != null)
        {
            string newTheme = item.Tag.ToString()!;
            var config = ConfigManager.Current;
            if (config.Theme != newTheme)
            {
                config.Theme = newTheme;
                ConfigManager.Save(config);
                ThemeManager.ApplyTheme(newTheme);
            }
        }
    }

    private async void OnCheckForUpdatesClicked(object? sender, RoutedEventArgs e)
    {
        ResetUpdateSection();
        UpdateStatusText.Text = "Consultando GitHub...";
        CheckForUpdatesBtn.IsEnabled = false;

        var release = await _updater.FetchLatestReleaseAsync();

        if (release == null)
        {
            UpdateStatusText.Text = "No se pudo contactar con GitHub. Revisa tu conexión.";
            CheckForUpdatesBtn.IsEnabled = true;
            return;
        }

        if (_updater.IsNewerAvailable(release, out _))
        {
            var msi = _updater.FindMsi(release);
            if (msi == null)
            {
                UpdateStatusText.Text = $"Nueva versión {release.TagName} disponible, pero no se encontró el instalador (.msi) en la Release.";
                CheckForUpdatesBtn.IsEnabled = true;
                return;
            }

            var notes = string.IsNullOrWhiteSpace(release.Body) ? AniCS.Desktop.AppInfo.LatestChangelog : release.Body;
            var changelogWindow = new Controls.ChangelogWindow(release.TagName, notes);
            var window = TopLevel.GetTopLevel(this) as Window;
            if (window != null) await changelogWindow.ShowDialog(window);
            else changelogWindow.Show();

            UpdateStatusText.Text = $"Nueva versión {release.TagName} lista para instalar.";
            DownloadUpdateBtn.IsVisible = true;
        }
        else
        {
            UpdateStatusText.Text = "Estás usando la última versión disponible. ¡Genial!";
        }

        CheckForUpdatesBtn.IsEnabled = true;
    }

    private async void OnDownloadUpdateClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            CheckForUpdatesBtn.IsEnabled = false;
            DownloadUpdateBtn.IsEnabled = false;
            UpdateStatusText.Text = "Descargando instalador...";
            UpdateProgress.IsVisible = true;
            UpdateProgressText.IsVisible = true;
            UpdateProgress.Value = 0;

            var release = await _updater.FetchLatestReleaseAsync();
            var msi = release == null ? null : _updater.FindMsi(release);
            if (msi == null)
            {
                UpdateStatusText.Text = "No se encontró el instalador. Reintenta en un momento.";
                ResetUpdateSection();
                return;
            }

            var progress = new System.Progress<double>(p =>
            {
                UpdateProgress.Value = p;
                UpdateProgressText.Text = $"Descargando... {p:F0}%";
            });

            var msiPath = await _updater.DownloadMsiAsync(msi, progress);
            if (string.IsNullOrEmpty(msiPath))
            {
                UpdateStatusText.Text = "Falló la descarga del instalador.";
                ResetUpdateSection();
                return;
            }

            UpdateStatusText.Text = "Instalando actualización... La app se cerrará y se reabrirá automáticamente.";
            UpdateProgress.IsVisible = false;
            _updater.ApplyAndRelaunch(msiPath);
        }
        catch
        {
            UpdateStatusText.Text = "Ocurrió un error durante la actualización.";
            ResetUpdateSection();
        }
    }

    private void OnViewChangelogClicked(object? sender, RoutedEventArgs e)
    {
        var currentVersion = AniCS.Desktop.AppInfo.CurrentVersion;
        string changelog = AniCS.Desktop.AppInfo.LatestChangelog;
        var window = TopLevel.GetTopLevel(this) as Window;
        var changelogWindow = new Controls.ChangelogWindow(currentVersion, changelog);
        if (window != null)
        {
            changelogWindow.ShowDialog(window);
        }
        else
        {
            changelogWindow.Show();
        }
    }
}