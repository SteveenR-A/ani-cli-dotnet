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
}
