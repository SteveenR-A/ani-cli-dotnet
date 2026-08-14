using System;
using Avalonia.Threading;
using AniCS.Android.Views;

namespace AniCS.Android.Services;

/// <summary>
/// Servicio desacoplado de navegación para la aplicación móvil Android.
/// Canaliza las peticiones de retroceso provenientes del OnBackPressedDispatcher de AndroidX hacia la vista activa de Avalonia.
/// </summary>
public static class MobileNavigationService
{
    public static Func<bool>? BackPressHandler { get; set; }

    /// <summary>
    /// Intenta procesar el evento de retroceso en el árbol de UI de Avalonia en el hilo principal de la interfaz.
    /// </summary>
    /// <returns>True si la acción de retroceso fue consumida por Avalonia (modal, lightbox, reproductor o historial); False si no queda nada que retroceder.</returns>
    public static bool HandleBackPress()
    {
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                return ExecuteBackPress();
            }

            return Dispatcher.UIThread.Invoke(ExecuteBackPress);
        }
        catch (Exception ex)
        {
            global::Android.Util.Log.Error("AniCS_Nav", $"Error en MobileNavigationService.HandleBackPress: {ex}");
            AppLogger.Error("MobileNavigationService.HandleBackPress", ex);
            return false;
        }
    }

    private static bool ExecuteBackPress()
    {
        if (BackPressHandler != null)
        {
            return BackPressHandler.Invoke();
        }

        if (AndroidMainView.Current != null)
        {
            return AndroidMainView.Current.HandleBackPress();
        }

        return false;
    }
}
