using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;
using System;
using AniCS.Core; // Assuming CoreServiceCollectionExtensions is here or in AniCS namespace

namespace AniCS.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        
        // Add Core
        services.AddAniCSCore();
        
        // Add ViewModels
        // services.AddTransient<AnimeDetailsViewModel>(); (We will add this once we create it)
        
        Services = services.BuildServiceProvider();

        // Load initial config and theme
        var config = Services.GetRequiredService<AniCS.Models.AppConfig>();
        ThemeManager.ApplyTheme(config.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            desktop.Exit += (s, e) => DataCache.CleanupImageCache(config.MaxImageCacheCount);
        }

        base.OnFrameworkInitializationCompleted();
    }
}