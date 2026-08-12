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
public class MainActivity : AvaloniaMainActivity
}
