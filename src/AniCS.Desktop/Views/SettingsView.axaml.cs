using Avalonia.Controls;
using Avalonia.Interactivity;
using AniCS.Models;
using System.Threading.Tasks;

namespace AniCS.Desktop.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        LoadConfig();
    }

    public void LoadConfig()
    {
        var config = ConfigManager.Current;

        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
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
        
        RefreshThemeList(config);
        RefreshParadigmList(config);
        
        UseSpatialHudToggle.IsChecked = config.UseSpatialHud;
        

        
        StatusMessage.IsVisible = false;
    }



    private void RefreshParadigmList(AppConfig config)
    {
        if (ParadigmComboBox == null) return;
        for (int i = 0; i < ParadigmComboBox.Items.Count; i++)
        {
            if (ParadigmComboBox.Items[i] is Avalonia.Controls.ComboBoxItem item && item.Tag?.ToString() == config.UiParadigm)
            {
                ParadigmComboBox.SelectedIndex = i;
                break;
            }
        }
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
        
        if (ParadigmComboBox != null && ParadigmComboBox.SelectedItem is Avalonia.Controls.ComboBoxItem paradigmItem && paradigmItem.Tag != null)
        {
            config.UiParadigm = paradigmItem.Tag.ToString()!;
        }
        
        config.UseSpatialHud = UseSpatialHudToggle.IsChecked == true;
        


        ConfigManager.Save(config);

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

    private void OnViewChangelogClicked(object? sender, RoutedEventArgs e)
    {
        var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
        string changelog = "¡Hola! Estas son las novedades de la versión 1.5.1 (Parche de Correcciones):\n\n" +
                           "• Gran mejora en la velocidad de inicio de reproducción (inicio instantáneo de streams directos).\n" +
                           "• Optimización de flujo: El reproductor mpv ya no se abre con ventana negra a esperar, la app resuelve el enlace antes de lanzarlo.\n" +
                           "• En Modo Donghua, el servidor nativo principal y funcional es MundoDonghua HLS.\n" +
                           "• Solución al problema de bloqueo/congelamiento completo de la interfaz al dar clic en Reproducir.\n" +
                           "• Corregido el botón 'Ver Notas del Parche' en Ajustes que no abría la ventana emergente.\n" +
                           "• Servidores externos (VidHide / Embedwish) marcados adecuadamente y procesados vía yt-dlp en segundo plano.\n\n" +
                           "¡Gracias por usar AniCS!";
        
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
