using Android.App;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using AniCS.Desktop;

namespace AniCS.Android;

/// <summary>
/// Android entry point for AniCS.
/// Boots the shared Avalonia/Desktop app inside the Android runtime.
/// </summary>
[Activity(
    Label = "AniCS",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    MainLauncher = true,
    ConfigurationChanges =
        global::Android.Content.PM.ConfigChanges.Orientation |
        global::Android.Content.PM.ConfigChanges.ScreenSize |
        global::Android.Content.PM.ConfigChanges.UiMode |
        global::Android.Content.PM.ConfigChanges.Keyboard |
        global::Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        // ── 1. Set the cross-platform data path before ANY AniCS code runs ──
        // Android sandboxes each app to its own private directory; we use
        // FilesDir (internal storage, always available, no permissions needed).
        var dataDir = FilesDir?.AbsolutePath
                      ?? System.IO.Path.Combine(
                             System.Environment.GetFolderPath(
                                 System.Environment.SpecialFolder.Personal),
                             "AniCS");

        AniCS.ConfigManager.BaseDataPath = System.IO.Path.Combine(dataDir, "AniCS");

        // ── 2. Initialize LibVLC with the Android context ────────────────
        // Core.Initialize() with the Android application context is required
        // on Android; passing null would throw at runtime.
        try
        {
            LibVLCSharp.Shared.Core.Initialize(ApplicationContext!.FilesDir!.AbsolutePath);
        }
        catch
        {
            // Non-fatal: audio-only features still work; video falls back to ExoPlayer.
        }

        // ── 3. Configure Avalonia ─────────────────────────────────────────
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .UseAndroid();
    }
}
