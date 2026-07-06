using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using AniCS;
using System.IO;
using System.Net.Http;

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

    static AsyncImageLoader()
    {
        SourceUrlProperty.Changed.AddClassHandler<Image>(OnSourceUrlChanged);
    }

    private static async void OnSourceUrlChanged(Image sender, AvaloniaPropertyChangedEventArgs e)
    {
        var url = e.NewValue as string;
        sender.Source = null;

        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            var bytes = await DataCache.GetImageAsync(_httpClient, url);
            if (bytes != null && bytes.Length > 0)
            {
                using var ms = new MemoryStream(bytes);
                var bitmap = new Bitmap(ms);
                sender.Source = bitmap;
            }
        }
        catch
        {
            // Fallback or ignore
        }
    }
}
