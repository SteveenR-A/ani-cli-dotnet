using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Microsoft.Extensions.DependencyInjection;
using System;
using AniCS.Player;
using AniCS.Resolver;

namespace AniCS.Desktop;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static Func<IServiceProvider, Avalonia.Controls.Control>? SingleViewFactory { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            AppLogger.Error("UnhandledException", e.ExceptionObject as Exception);
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            AppLogger.Error("UnobservedTaskException", e.Exception);
        };

        try { LibVLCSharp.Shared.Core.Initialize(); } catch { }

        var services = new ServiceCollection();
        
        // Add Core
        services.AddAniCSCore();

        // ── Backends de reproducción y descarga ──────────────────────────────
        // Se crean como singletons: un solo backend vivo durante toda la sesión.
        // La selección respeta AppConfig.PlayerBackend / AppConfig.ResolverBackend.
        services.AddSingleton<IPlayerBackend>(_ => PlayerFactory.CreateFromConfig());
        services.AddSingleton<IResolverBackend>(_ => ResolverFactory.CreateFromConfig());
        
        // Add ViewModels
        services.AddSingleton<ViewModels.HomeViewModel>();

        // AppUpdate service
        services.AddSingleton<Services.AppUpdateService>();
        
        Services = services.BuildServiceProvider();

        // Load initial config and theme
        var config = Services.GetRequiredService<AniCS.Models.AppConfig>();
        ThemeManager.ApplyTheme(config.Theme);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(Services.GetRequiredService<ViewModels.HomeViewModel>());
            desktop.Exit += (s, e) =>
            {
                DataCache.CleanupImageCache(config.MaxImageCacheCount);
                // Liberar backends al cerrar
                (Services.GetService<IPlayerBackend>() as IDisposable)?.Dispose();
                (Services.GetService<IResolverBackend>() as IDisposable)?.Dispose();
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            if (SingleViewFactory != null)
            {
                singleView.MainView = SingleViewFactory(Services);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}