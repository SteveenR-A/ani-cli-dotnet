using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using AniCS.Desktop;
using Microsoft.Extensions.DependencyInjection;

namespace AniCS.Android;

[Application(
    Label = "AniCS",
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    AllowBackup = true)]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(IntPtr javaReference, JniHandleOwnership transfer) 
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // ── 1. Set the cross-platform data path before ANY AniCS code runs ──
        var dataDir = ApplicationContext?.FilesDir?.AbsolutePath
                      ?? System.IO.Path.Combine(
                             System.Environment.GetFolderPath(
                                 System.Environment.SpecialFolder.Personal),
                             "AniCS");

        AniCS.ConfigManager.BaseDataPath = System.IO.Path.Combine(dataDir, "AniCS");

        // ── Global Exception Handling for Android ──────────────────────────
        AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
        {
            AppLogger.Error("AndroidEnvironment.UnhandledException", args.Exception);
            global::Android.Util.Log.Error("AniCS", $"Unhandled Android Exception: {args.Exception}");
        };
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            AppLogger.Error("AppDomain.UnhandledException", ex);
            global::Android.Util.Log.Error("AniCS", $"Unhandled AppDomain Exception: {ex}");
        };
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, args) =>
        {
            AppLogger.Error("TaskScheduler.UnobservedTaskException", args.Exception);
            global::Android.Util.Log.Error("AniCS", $"Unobserved Task Exception: {args.Exception}");
        };

        // ── 2. Initialize LibVLC with the Android context ────────────────
        try
        {
            if (ApplicationContext?.FilesDir?.AbsolutePath != null)
            {
                LibVLCSharp.Shared.Core.Initialize(ApplicationContext.FilesDir.AbsolutePath);
            }
            else
            {
                LibVLCSharp.Shared.Core.Initialize();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Debug("MainApplication", $"LibVLC init failed: {ex.Message}");
        }

        // ── 3. Register Android dedicated view factory ──────────────────
        AniCS.Desktop.App.SingleViewFactory = sp => 
            new Views.AndroidMainView(sp.GetRequiredService<AniCS.Desktop.ViewModels.HomeViewModel>());

        return base.CustomizeAppBuilder(builder);
    }
}
