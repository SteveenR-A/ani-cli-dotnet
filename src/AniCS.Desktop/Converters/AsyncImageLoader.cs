using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AniCS;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AniCS.Desktop.Converters;

public class AsyncImageLoader
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public static readonly AttachedProperty<string> SourceUrlProperty =
        AvaloniaProperty.RegisterAttached<AsyncImageLoader, Image, string>("SourceUrl");

    public static string GetSourceUrl(Image element)
    {
        return element.GetValue(SourceUrlProperty);
    }

    public static void SetSourceUrl(Image element, string value)
    {
        element.SetValue(SourceUrlProperty, value);
    }

    public static readonly AttachedProperty<bool> ApplyDuotoneProperty =
        AvaloniaProperty.RegisterAttached<AsyncImageLoader, Image, bool>("ApplyDuotone", false);

    public static bool GetApplyDuotone(Image element)
    {
        return element.GetValue(ApplyDuotoneProperty);
    }

    public static void SetApplyDuotone(Image element, bool value)
    {
        element.SetValue(ApplyDuotoneProperty, value);
    }

    static AsyncImageLoader()
    {
        SourceUrlProperty.Changed.AddClassHandler<Image>(OnSourceUrlChanged);
        ApplyDuotoneProperty.Changed.AddClassHandler<Image>(OnSourceUrlChanged);
    }

    private static async void OnSourceUrlChanged(Image sender, AvaloniaPropertyChangedEventArgs e)
    {
        var url = sender.GetValue(SourceUrlProperty);
        sender.Source = null;

        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var bytes = await DataCache.GetImageAsync(_httpClient, url);
            if (bytes != null && bytes.Length > 0)
            {
                bool applyDuotone = sender.GetValue(ApplyDuotoneProperty);
                if (applyDuotone)
                {
                    // Buscar los colores del tema actual respetando diccionarios locales
                    if (sender.TryFindResource("AppBackgroundColor", out var bgRes) &&
                        sender.TryFindResource("AppPrimaryColor", out var fgRes) &&
                        bgRes is Avalonia.Media.SolidColorBrush bgBrush &&
                        fgRes is Avalonia.Media.SolidColorBrush fgBrush)
                    {
                        var bgColor = bgBrush.Color;
                        var fgColor = fgBrush.Color;
                        // Procesar de forma asíncrona para no bloquear UI
                        var processedBitmap = await Task.Run(() => DuotoneImageProcessor.Process(bytes, bgColor, fgColor));
                        sender.Source = processedBitmap;
                    }
                    else
                    {
                        sender.Source = new Bitmap(new MemoryStream(bytes));
                    }
                }
                else
                {
                    sender.Source = new Bitmap(new MemoryStream(bytes));
                }
            }
        }
        catch
        {
            // Fallback
        }
    }
}
