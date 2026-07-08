using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace AniCS.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Load initial config and theme
        var config = AniCS.ConfigManager.Current;
        ThemeManager.ApplyTheme(config.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (s, e) => DataCache.CleanupImageCache(ConfigManager.Current.MaxImageCacheCount);
        }

        base.OnFrameworkInitializationCompleted();
    }
}