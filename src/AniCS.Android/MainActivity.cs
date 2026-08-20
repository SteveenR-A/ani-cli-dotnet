using Android.App;
using Android.OS;
using AndroidX.Activity;
using AndroidX.Core.View;
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
    private bool _isImmersiveModeActive = false;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Instance = this;
        base.OnCreate(savedInstanceState);

        // Limpieza en segundo plano de APKs residuales de actualizaciones anteriores
        System.Threading.Tasks.Task.Run(() => AndroidUpdateService.CleanOldUpdates(this));

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

            AniCS.Desktop.Services.DownloadManager.OnFileDownloaded = (filePath) =>
            {
                try
                {
                    if (System.IO.File.Exists(filePath))
                    {
                        global::Android.Media.MediaScannerConnection.ScanFile(
                            this,
                            new[] { filePath },
                            new[] { "video/mp4", "video/mp2t", "video/*" },
                            null);
                    }
                }
                catch (System.Exception ex)
                {
                    global::Android.Util.Log.Error("AniCS_Scanner", $"Error scanning file {filePath}: {ex}");
                }
            };
        }
        catch { }

        // 4. Configurar la carpeta oficial de descargas en DCIM/AniCS
        try
        {
            var dcimDir = global::Android.OS.Environment.GetExternalStoragePublicDirectory(global::Android.OS.Environment.DirectoryDcim);
            if (dcimDir != null && !string.IsNullOrEmpty(dcimDir.AbsolutePath))
            {
                AniCS.Desktop.Services.DownloadManager.PlatformDefaultDownloadDirectory = System.IO.Path.Combine(dcimDir.AbsolutePath, "AniCS");
            }
            else
            {
                AniCS.Desktop.Services.DownloadManager.PlatformDefaultDownloadDirectory = System.IO.Path.Combine("/storage/emulated/0", "DCIM", "AniCS");
            }
        }
        catch
        {
            AniCS.Desktop.Services.DownloadManager.PlatformDefaultDownloadDirectory = System.IO.Path.Combine("/storage/emulated/0", "DCIM", "AniCS");
        }

        // 5. Solicitar permisos de almacenamiento si no han sido concedidos
        RequestStoragePermissions();
    }

    private void RequestStoragePermissions()
    {
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R) // Android 11+ (API 30+)
            {
                if (!global::Android.OS.Environment.IsExternalStorageManager)
                {
                    try
                    {
                        var uri = global::Android.Net.Uri.Parse($"package:{PackageName}");
                        var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAppAllFilesAccessPermission, uri);
                        StartActivity(intent);
                    }
                    catch
                    {
                        var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionManageAllFilesAccessPermission);
                        StartActivity(intent);
                    }
                }
            }

            var permissionsToRequest = new System.Collections.Generic.List<string>();

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu) // API 33+ (Android 13+)
            {
                if (CheckSelfPermission(global::Android.Manifest.Permission.ReadMediaVideo) != global::Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(global::Android.Manifest.Permission.ReadMediaVideo);
                }
                if (CheckSelfPermission(global::Android.Manifest.Permission.ReadMediaImages) != global::Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(global::Android.Manifest.Permission.ReadMediaImages);
                }
                if (CheckSelfPermission(global::Android.Manifest.Permission.PostNotifications) != global::Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(global::Android.Manifest.Permission.PostNotifications);
                }
            }
            else
            {
                if (CheckSelfPermission(global::Android.Manifest.Permission.WriteExternalStorage) != global::Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(global::Android.Manifest.Permission.WriteExternalStorage);
                }
                if (CheckSelfPermission(global::Android.Manifest.Permission.ReadExternalStorage) != global::Android.Content.PM.Permission.Granted)
                {
                    permissionsToRequest.Add(global::Android.Manifest.Permission.ReadExternalStorage);
                }
            }

            if (permissionsToRequest.Count > 0)
            {
                RequestPermissions(permissionsToRequest.ToArray(), 1001);
            }
        }
        catch (System.Exception ex)
        {
            global::Android.Util.Log.Error("AniCS_Storage", $"Error requesting storage permissions: {ex}");
        }
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus && _isImmersiveModeActive)
        {
            ApplyImmersiveMode();
        }
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

    protected override void OnPause()
    {
        base.OnPause();
        try
        {
            if (Views.AndroidMainView.Current?.MainContent.Content is Views.MobileVideoPlayerView player && player.IsPlaying)
            {
                player.Pause();
            }
        }
        catch { }
        DisableKeepScreenOn();
    }

    protected override void OnStop()
    {
        base.OnStop();
        DisableKeepScreenOn();
    }

    public void EnableKeepScreenOn()
    {
        RunOnUiThread(() =>
        {
            try
            {
                Window?.AddFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn);
            }
            catch { }
        });
    }

    public void DisableKeepScreenOn()
    {
        RunOnUiThread(() =>
        {
            try
            {
                Window?.ClearFlags(global::Android.Views.WindowManagerFlags.KeepScreenOn);
                if (Window?.DecorView != null)
                {
                    Window.DecorView.KeepScreenOn = false;
                }
            }
            catch { }
        });
    }

    public void EnableImmersiveMode()
    {
        _isImmersiveModeActive = true;
        RunOnUiThread(ApplyImmersiveMode);
    }

    private void ApplyImmersiveMode()
    {
        try
        {
            if (Window == null) return;

            // 1. AndroidX WindowCompat (Moderno, Android 11+ / API 30+)
            WindowCompat.SetDecorFitsSystemWindows(Window, false);
            var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
            if (insetsController != null)
            {
                insetsController.Hide(WindowInsetsCompat.Type.SystemBars());
                insetsController.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            }

            // 2. Flags clásicos como respaldo para versiones anteriores de Android
#pragma warning disable CS0618
            if (Window.DecorView != null)
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
        _isImmersiveModeActive = false;
        RunOnUiThread(() =>
        {
            try
            {
                if (Window == null) return;

                // 1. AndroidX WindowCompat
                WindowCompat.SetDecorFitsSystemWindows(Window, true);
                var insetsController = WindowCompat.GetInsetsController(Window, Window.DecorView);
                if (insetsController != null)
                {
                    insetsController.Show(WindowInsetsCompat.Type.SystemBars());
                }

                // 2. Flags clásicos
#pragma warning disable CS0618
                if (Window.DecorView != null)
                {
                    Window.DecorView.SystemUiVisibility = (global::Android.Views.StatusBarVisibility)global::Android.Views.SystemUiFlags.Visible;
                }
#pragma warning restore CS0618
            }
            catch { }
        });
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
