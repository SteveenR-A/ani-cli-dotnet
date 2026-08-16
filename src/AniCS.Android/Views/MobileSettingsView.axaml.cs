using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AniCS.Android.Services;
using AniCS.Desktop;
using AniCS.Models;
using Button = Avalonia.Controls.Button;

namespace AniCS.Android.Views;

public partial class MobileSettingsView : UserControl
{
    private AndroidUpdateService.UpdateInfo? _availableUpdate;

    public MobileSettingsView()
    {
        InitializeComponent();
        LoadConfig();
    }

    public void LoadConfig()
    {
        var cfg = ConfigManager.Current;

        // Tema
        SelectComboByTag(ThemeComboBox, cfg.Theme);

        // Calidad
        SelectComboByTag(QualityComboBox, cfg.PreferredQuality);

        // JKAnime URL
        CustomJkAnimeUrlInput.Text = cfg.CustomJkAnimeBaseUrl;

        // Cache limit
        CacheLimitInput.Value = cfg.MaxImageCacheCount;

        // Ubicación de descargas
        if (DownloadDirectoryInput != null)
        {
            DownloadDirectoryInput.Text = !string.IsNullOrWhiteSpace(cfg.CustomDownloadDirectory)
                ? cfg.CustomDownloadDirectory
                : Desktop.Services.DownloadManager.SystemDefaultDownloadDirectory;
        }

        // Versión
        AppVersionText.Text = $"Versión: {AppInfo.CurrentVersion}";
    }

    private void OnResetDownloadDirClicked(object? sender, RoutedEventArgs e)
    {
        if (DownloadDirectoryInput != null)
        {
            DownloadDirectoryInput.Text = Desktop.Services.DownloadManager.SystemDefaultDownloadDirectory;
        }
    }

    private void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string themeTag)
        {
            ThemeManager.ApplyTheme(themeTag);
        }
    }

    private async void OnClearCacheClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var cacheDir = Path.Combine(ConfigManager.BaseDataPath, "cache");
            if (Directory.Exists(cacheDir))
            {
                Directory.Delete(cacheDir, recursive: true);
                Directory.CreateDirectory(cacheDir);
            }

            var downloadsDir = Path.Combine(ConfigManager.BaseDataPath, "Downloads");
            if (Directory.Exists(downloadsDir))
            {
                foreach (var file in Directory.GetFiles(downloadsDir, "*.part", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (var file in Directory.GetFiles(downloadsDir, "*.tmp", SearchOption.AllDirectories))
                {
                    try { File.Delete(file); } catch { }
                }
            }

            ShowStatus("Caché y residuos temporales eliminados con éxito.", Brushes.LightGreen);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error al limpiar caché: {ex.Message}", Brushes.Salmon);
        }
    }

    private async void OnClearHistoryClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var history = new History.WatchHistory();
            history.Clear();
            ShowStatus("Historial de reproducción eliminado.", Brushes.LightGreen);
        }
        catch (Exception ex)
        {
            ShowStatus($"Error al limpiar historial: {ex.Message}", Brushes.Salmon);
        }
    }

    private async void OnCheckForUpdatesClicked(object? sender, RoutedEventArgs e)
    {
        UpdateStatusText.Text = "Comprobando actualizaciones...";
        UpdateStatusText.Foreground = (IBrush)this.FindResource("AppSubtextColor")!;
        CheckForUpdatesBtn.IsEnabled = false;
        DownloadUpdateBtn.IsVisible = false;

        try
        {
            var update = await AndroidUpdateService.CheckAsync(AppInfo.CurrentVersion);
            if (update != null)
            {
                _availableUpdate = update;
                UpdateStatusText.Text = $"¡Nueva versión disponible: v{update.Version}!\n{update.ReleaseNotes}";
                UpdateStatusText.Foreground = Brushes.LightGreen;
                DownloadUpdateBtn.IsVisible = true;
            }
            else
            {
                UpdateStatusText.Text = "Estás utilizando la versión más reciente.";
                UpdateStatusText.Foreground = (IBrush)this.FindResource("AppSubtextColor")!;
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Error al buscar: {ex.Message}";
            UpdateStatusText.Foreground = Brushes.Salmon;
        }
        finally
        {
            CheckForUpdatesBtn.IsEnabled = true;
        }
    }

    private async void OnDownloadUpdateClicked(object? sender, RoutedEventArgs e)
    {
        if (_availableUpdate == null || MainActivity.Instance == null) return;

        DownloadUpdateBtn.IsEnabled = false;
        UpdateProgress.IsVisible = true;
        UpdateProgressText.IsVisible = true;
        UpdateStatusText.Text = "Descargando e instalando APK...";

        try
        {
            await AndroidUpdateService.DownloadAndInstallAsync(
                MainActivity.Instance,
                _availableUpdate.ApkUrl,
                pct =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateProgress.Value = pct * 100;
                        UpdateProgressText.Text = $"{pct * 100:F0}%";
                    });
                });
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Error al instalar: {ex.Message}";
            UpdateStatusText.Foreground = Brushes.Salmon;
        }
        finally
        {
            DownloadUpdateBtn.IsEnabled = true;
        }
    }

    private void OnViewChangelogClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var panel = new StackPanel { Spacing = 10 };

            var primaryBrush = (IBrush)(this.TryFindResource("AppPrimaryColor", out var pBrush) ? pBrush! : Brushes.Purple);
            var cardBrush = (IBrush)(this.TryFindResource("AppCardBg", out var cBrush) ? cBrush! : (this.TryFindResource("AppSurfaceColor", out var sBrush) ? sBrush! : Brushes.DarkSlateGray));
            var textBrush = (IBrush)(this.TryFindResource("AppTextColor", out var tBrush) ? tBrush! : Brushes.White);

            var versionHeader = new Border
            {
                Background = primaryBrush,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8),
                Margin = new Thickness(0, 0, 0, 6)
            };
            versionHeader.Child = new TextBlock
            {
                Text = $"Versión {AppInfo.CurrentVersion} — Registro de Cambios",
                FontWeight = FontWeight.Bold,
                FontSize = 13,
                Foreground = Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            panel.Children.Add(versionHeader);

            var notesBorder = new Border
            {
                Background = cardBrush,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 12),
                Margin = new Thickness(0, 0, 0, 6)
            };

            var changelogText = new TextBlock
            {
                Text = AppInfo.LatestChangelog,
                FontSize = 12,
                LineHeight = 18,
                TextWrapping = TextWrapping.Wrap,
                Foreground = textBrush
            };
            notesBorder.Child = changelogText;
            panel.Children.Add(notesBorder);

            AndroidMainView.Current?.ShowModal("Notas de la Versión", panel);
        }
        catch (Exception ex)
        {
            AppLogger.Error("MobileSettingsView.OnViewChangelogClicked", ex);
            ShowStatus($"Error al abrir notas: {ex.Message}", Brushes.Salmon);
        }
    }

    private void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        var cfg = ConfigManager.Current;

        if (ThemeComboBox.SelectedItem is ComboBoxItem themeItem && themeItem.Tag is string theme)
            cfg.Theme = theme;

        if (QualityComboBox.SelectedItem is ComboBoxItem qItem && qItem.Tag is string quality)
            cfg.PreferredQuality = quality;

        cfg.CustomJkAnimeBaseUrl = CustomJkAnimeUrlInput.Text?.Trim() ?? "https://jkanime.net";
        cfg.MaxImageCacheCount = (int)(CacheLimitInput.Value ?? 100);

        if (DownloadDirectoryInput != null)
        {
            var customDir = DownloadDirectoryInput.Text?.Trim() ?? string.Empty;
            if (customDir.Equals(Desktop.Services.DownloadManager.SystemDefaultDownloadDirectory, StringComparison.OrdinalIgnoreCase))
            {
                customDir = string.Empty;
            }
            cfg.CustomDownloadDirectory = customDir;
            Desktop.Services.DownloadManager.SetCustomDownloadDirectory(customDir);
        }

        ConfigManager.Save(cfg);
        ShowStatus("¡Configuración guardada correctamente!", Brushes.LightGreen);
    }

    private void ShowStatus(string message, IBrush brush)
    {
        StatusMessage.Text = message;
        StatusMessage.Foreground = brush;

        Task.Delay(3000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (StatusMessage.Text == message) StatusMessage.Text = "";
            });
        });
    }

    private static void SelectComboByTag(ComboBox combo, string? tag)
    {
        if (string.IsNullOrEmpty(tag)) return;
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem ci && ci.Tag?.ToString() == tag)
            {
                combo.SelectedItem = ci;
                return;
            }
        }
    }
}
