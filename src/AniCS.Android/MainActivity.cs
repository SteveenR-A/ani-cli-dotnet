using Android.App;
using Android.OS;
using AndroidX.Activity;
using Avalonia;
using Avalonia.Android;
using AniCS.Android.Services;

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

        // 1. AndroidX OnBackPressedDispatcher callback
        try
        {
            OnBackPressedDispatcher.AddCallback(this, new BackPressHandler(this));
        }
        catch (System.Exception ex)
        {
            global::Android.Util.Log.Error("AniCS_Back", $"Error registering OnBackPressedDispatcher callback: {ex}");
        }

        // 2. AvaloniaActivity BackRequested event (si está soportado por Avalonia)
        try
        {
            this.BackRequested += (s, e) =>
            {
                global::Android.Util.Log.Debug("AniCS_Back", "AvaloniaActivity.BackRequested event triggered!");
                bool handled = MobileNavigationService.HandleBackPress();
                if (handled)
                {
                    e.Handled = true;
                }
            };
        }
        catch { }

        // 3. Conectar DownloadManager con Foreground Service para descargas continuas en segundo plano
        try
        {
            AniCS.Desktop.Services.DownloadManager.OnDownloadsStarted = () =>
            {
                AndroidDownloadForegroundService.Start(this);
            };

            AniCS.Desktop.Services.DownloadManager.OnDownloadsFinished = () =>
            {
                AndroidDownloadForegroundService.Stop(this);
            };

            AniCS.Desktop.Services.DownloadManager.OnDownloadProgressNotify = (title, content, progress) =>
            {
                AndroidDownloadForegroundService.UpdateNotification(title, content, progress);
            };
        }
        catch { }
    }

    public override void OnBackPressed()
    {
        global::Android.Util.Log.Debug("AniCS_Back", "MainActivity.OnBackPressed() invoked!");
        bool handled = MobileNavigationService.HandleBackPress();
        if (!handled)
        {
            base.OnBackPressed();
        }
    }

    public override bool DispatchKeyEvent(global::Android.Views.KeyEvent? e)
    {
        if (e != null && e.KeyCode == global::Android.Views.Keycode.Back)
        {
            global::Android.Util.Log.Debug("AniCS_Back", $"DispatchKeyEvent KEYCODE_BACK Action={e.Action}");
            if (e.Action == global::Android.Views.KeyEventActions.Up)
            {
                bool handled = MobileNavigationService.HandleBackPress();
                if (handled)
                {
                    return true;
                }
            }
        }
        return base.DispatchKeyEvent(e);
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

    private class BackPressHandler : OnBackPressedCallback
    {
        private readonly MainActivity _activity;

        public BackPressHandler(MainActivity activity) : base(true)
        {
            _activity = activity;
        }

        public override void HandleOnBackPressed()
        {
            try
            {
                global::Android.Util.Log.Debug("AniCS_Back", "BackPressHandler.HandleOnBackPressed triggered!");

                // Llama al servicio de navegación desacoplado de Avalonia
                bool handled = MobileNavigationService.HandleBackPress();

                global::Android.Util.Log.Debug("AniCS_Back", $"HandleBackPress result: {handled}");

                if (!handled)
                {
                    // Si Avalonia no consumió el evento (ej. estamos en Inicio y pila vacía),
                    // desactivamos temporalmente el callback y permitimos que el sistema Android
                    // ejecute el comportamiento nativo (minimizar/cerrar Activity).
                    Enabled = false;
                    _activity.OnBackPressedDispatcher.OnBackPressed();
                    Enabled = true;
                }
            }
            catch (System.Exception ex)
            {
                global::Android.Util.Log.Error("AniCS_Back", $"Error in BackPressHandler.HandleOnBackPressed: {ex}");
                AniCS.AppLogger.Error("BackPressHandler.HandleOnBackPressed", ex);
            }
        }
    }
}
