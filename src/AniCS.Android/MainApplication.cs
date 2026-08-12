using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using AniCS.Desktop;

namespace AniCS.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(IntPtr javaReference, JniHandleOwnership transfer) 
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // ── 1. Set the cross-platform data path before ANY AniCS code runs ──
        // Android sandboxes each app to its own private directory; we use
        // FilesDir (internal storage, always available, no permissions needed).
        var dataDir = ApplicationContext?.FilesDir?.AbsolutePath
                      ?? System.IO.Path.Combine(
                             System.Environment.GetFolderPath(
                                 System.Environment.SpecialFolder.Personal),
                             "AniCS");

        AniCS.ConfigManager.BaseDataPath = System.IO.Path.Combine(dataDir, "AniCS");

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
        catch
        {
            // LibVLC initialization can fail if binaries are missing
            // We ignore it so the UI still loads (it'll fallback/show error when playing).
        }

        return base.CustomizeAppBuilder(builder);
    }
}
