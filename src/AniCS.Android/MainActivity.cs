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
    Icon = "@mipmap/ic_launcher",
    RoundIcon = "@mipmap/ic_launcher",
    Theme = "@style/Theme.AppCompat.NoActionBar",
    MainLauncher = true,
    Exported = true,
    LaunchMode = global::Android.Content.PM.LaunchMode.SingleTop,
    ScreenOrientation = global::Android.Content.PM.ScreenOrientation.FullUser,
    ConfigurationChanges =
        global::Android.Content.PM.ConfigChanges.Orientation |
        global::Android.Content.PM.ConfigChanges.ScreenSize |
        global::Android.Content.PM.ConfigChanges.SmallestScreenSize |
        global::Android.Content.PM.ConfigChanges.ScreenLayout |
        global::Android.Content.PM.ConfigChanges.UiMode |
        global::Android.Content.PM.ConfigChanges.Keyboard |
        global::Android.Content.PM.ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity
{
    public static MainActivity? Instance { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);
    }

    public void SetOrientationLandscape()
    {
        RequestedOrientation = global::Android.Content.PM.ScreenOrientation.SensorLandscape;
    }

    public void SetOrientationPortrait()
    {
        RequestedOrientation = global::Android.Content.PM.ScreenOrientation.SensorPortrait;
    }

    public void ResetOrientation()
    {
        RequestedOrientation = global::Android.Content.PM.ScreenOrientation.FullUser;
    }

    public void EnableImmersiveMode()
    {
        try
        {
#pragma warning disable CS0618
            if (Window?.DecorView != null)
            {
                Window.DecorView.SystemUiVisibility = (global::Android.Views.StatusBarVisibility)(
                    global::Android.Views.SystemUiFlags.Fullscreen |
                    global::Android.Views.SystemUiFlags.HideNavigation |
                    global::Android.Views.SystemUiFlags.ImmersiveSticky |
                    global::Android.Views.SystemUiFlags.LayoutFullscreen |
                    global::Android.Views.SystemUiFlags.LayoutHideNavigation |
                    global::Android.Views.SystemUiFlags.LayoutStable);
            }
#pragma warning restore CS0618
        }
        catch { }
    }

    public void DisableImmersiveMode()
    {
        try
        {
#pragma warning disable CS0618
            if (Window?.DecorView != null)
            {
                Window.DecorView.SystemUiVisibility = (global::Android.Views.StatusBarVisibility)global::Android.Views.SystemUiFlags.Visible;
            }
#pragma warning restore CS0618
        }
        catch { }
    }

    public override void OnBackPressed()
    {
        if (Views.AndroidMainView.Current != null && Views.AndroidMainView.Current.CanGoBack)
        {
            Views.AndroidMainView.Current.GoBack();
            return;
        }

        base.OnBackPressed();
    }
}
