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

        // Versión
        AppVersionText.Text = $"Versión: {AppInfo.CurrentVersion}";
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
        CheckForUpdatesBtn.IsEnabled = false;
        UpdateStatusText.Text = "Buscando actualizaciones en GitHub...";
        UpdateStatusText.Foreground = (IBrush)this.FindResource("AppSubtextColor")!;

        try
        {
            var currentVersion = AppInfo.CurrentVersion.Split('+')[0].Trim();
            _availableUpdate = await AndroidUpdateService.CheckAsync(currentVersion);

            if (_availableUpdate != null)
            {
                UpdateStatusText.Text = $"¡Nueva versión {_availableUpdate.Version} disponible!";
                UpdateStatusText.Foreground = Brushes.LightGreen;
                DownloadUpdateBtn.IsVisible = true;
            }
            else
            {
                UpdateStatusText.Text = "Tienes la versión más reciente.";
                DownloadUpdateBtn.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Error al buscar actualización: {ex.Message}";
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
        UpdateStatusText.Text = "Descargando paquete APK...";

        try
        {
            await AndroidUpdateService.DownloadAndInstallAsync(
                MainActivity.Instance,
                _availableUpdate.ApkUrl,
                progress =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateProgress.Value = progress * 100;
                        UpdateProgressText.Text = $"{progress * 100:F0}%";
                    });
                });

            UpdateStatusText.Text = "Instalador iniciado. Sigue las instrucciones del sistema.";
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
        var panel = new StackPanel { Spacing = 10 };

        var versionHeader = new Border
        {
            Background = (IBrush)this.FindResource("AppPrimaryColor")!,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12, 6),
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

        var items = new (Material.Icons.MaterialIconKind icon, string title, string description)[]
        {
            (Material.Icons.MaterialIconKind.VolumeHigh, "[Audio & Sincronía]", "Corrección del problema de desincronización de audio y mixer en el reproductor."),
            (Material.Icons.MaterialIconKind.PlayBoxMultipleOutline, "[Reproductor & OSD]", "Migración a TextureView nativo con aspecto centrado y controles al frente."),
            (Material.Icons.MaterialIconKind.DownloadNetworkOutline, "[Descargas]", "Sincronización en tiempo real de episodios descargados y soporte Mediafire/HLS."),
            (Material.Icons.MaterialIconKind.Cellphone, "[Navegación]", "Navegación con historial y confirmación de doble toque para salir."),
            (Material.Icons.MaterialIconKind.History, "[Historial]", "Visualización de portadas y reanudación de animes vistos.")
        };

        var primaryBrush = (IBrush)this.FindResource("AppPrimaryColor")!;
        var titleBrush = (IBrush)this.FindResource("AppTitleColor")!;
        var subtextBrush = (IBrush)this.FindResource("AppSubtextColor")!;

        foreach (var (icon, title, desc) in items)
        {
            var itemBorder = new Border
            {
                Background = (IBrush)this.FindResource("AppCardBg")!,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("36, *")
            };

            var mIcon = new Material.Icons.Avalonia.MaterialIcon
            {
                Kind = icon,
                Width = 22,
                Height = 22,
                Foreground = primaryBrush,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Grid.SetColumn(mIcon, 0);

            var textStack = new StackPanel { Spacing = 2 };
            textStack.Children.Add(new TextBlock
            {
                Text = title,
                FontWeight = FontWeight.Bold,
                FontSize = 13,
                Foreground = titleBrush
            });
            textStack.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Foreground = subtextBrush
            });
            Grid.SetColumn(textStack, 1);

            grid.Children.Add(mIcon);
            grid.Children.Add(textStack);
            itemBorder.Child = grid;
            panel.Children.Add(itemBorder);
        }

        AndroidMainView.Current?.ShowModal("Notas de la Versión", panel);
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
