using System;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AniCS.Desktop.Services;

namespace AniCS.Android.Services;

[Service(ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeDataSync, Exported = false)]
public class AndroidDownloadForegroundService : Service
{
    private const string ChannelId = "anics_download_channel";
    private const int NotificationId = 1001;
    private PowerManager.WakeLock? _wakeLock;
    private static AndroidDownloadForegroundService? _instance;

    public static void Start(Context context)
    {
        try
        {
            var intent = new Intent(context, typeof(AndroidDownloadForegroundService));
            if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            {
                context.StartForegroundService(intent);
            }
            else
            {
                context.StartService(intent);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("AndroidDownloadForegroundService.Start", ex);
        }
    }

    public static void Stop(Context context)
    {
        try
        {
            var intent = new Intent(context, typeof(AndroidDownloadForegroundService));
            context.StopService(intent);
        }
        catch { }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnCreate()
    {
        base.OnCreate();
        _instance = this;
        CreateNotificationChannel();
        AcquireWakeLock();

        var notification = BuildNotification("Descargas en segundo plano", "Descargando contenido...");
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Q)
        {
            StartForeground(NotificationId, notification, global::Android.Content.PM.ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(NotificationId, notification);
        }
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        ReleaseWakeLock();
        _instance = null;
        base.OnDestroy();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                "Descargas de AniCS",
                NotificationImportance.Low)
            {
                Description = "Notificaciones de progreso para descargas de anime"
            };
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.CreateNotificationChannel(channel);
        }
    }

    private void AcquireWakeLock()
    {
        try
        {
            var pm = (PowerManager?)GetSystemService(PowerService);
            if (pm != null && _wakeLock == null)
            {
                _wakeLock = pm.NewWakeLock(WakeLockFlags.Partial, "AniCS:DownloadWakeLock");
                _wakeLock?.Acquire();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("AndroidDownloadForegroundService.AcquireWakeLock", ex);
        }
    }

    private void ReleaseWakeLock()
    {
        try
        {
            if (_wakeLock != null && _wakeLock.IsHeld)
            {
                _wakeLock.Release();
                _wakeLock = null;
            }
        }
        catch { }
    }

    private Notification BuildNotification(string title, string content, int progress = -1)
    {
        var pkg = PackageName ?? "com.anics.android";
        var launchIntent = PackageManager?.GetLaunchIntentForPackage(pkg);
        var pendingIntent = launchIntent != null
            ? PendingIntent.GetActivity(this, 0, launchIntent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)
            : null;

        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle(title);
        builder.SetContentText(content);
        builder.SetSmallIcon(global::Android.Resource.Drawable.StatSysDownload);
        builder.SetOngoing(true);
        builder.SetOnlyAlertOnce(true);
        if (pendingIntent != null)
        {
            builder.SetContentIntent(pendingIntent);
        }

        if (progress >= 0)
        {
            builder.SetProgress(100, Math.Clamp(progress, 0, 100), false);
        }

        return builder.Build()!;
    }

    public static void UpdateNotification(string title, string content, int progress = -1)
    {
        if (_instance == null) return;
        try
        {
            var notification = _instance.BuildNotification(title, content, progress);
            var manager = (NotificationManager?)_instance.GetSystemService(NotificationService);
            manager?.Notify(NotificationId, notification);
        }
        catch { }
    }
}
