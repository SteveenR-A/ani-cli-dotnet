using System.Net.Http;

namespace AniCS.Terminal;

public static class KittyGraphics
{
    public static async Task DisplayImageAsync(HttpClient client, string imageUrl)
    {
        if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            return; // Kitty Graphics Protocol no está soportado en terminales de Windows de forma nativa.

        if (string.IsNullOrWhiteSpace(imageUrl)) return;
        
        try
        {
            var imageBytes = await client.GetByteArrayAsync(imageUrl);
            var base64 = Convert.ToBase64String(imageBytes);

            // Kitty Graphics Protocol escape sequence
            // a=T means action=Transmit
            // f=100 means format=PNG/JPEG/etc (100 is generic/auto detect)
            // m=0 means no more chunks
            Console.WriteLine();
            Console.Write($"\x1b_Gf=100,a=T,m=0;{base64}\x1b\\");
            Console.WriteLine();
        }
        catch 
        {
            // Fallback if image download fails, just don't display anything
        }
    }
}
