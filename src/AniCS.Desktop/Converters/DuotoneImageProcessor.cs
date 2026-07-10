using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Media;
using Avalonia.Platform;

namespace AniCS.Desktop.Converters;

public static class DuotoneImageProcessor
{
    public static Bitmap Process(byte[] imageBytes, Color bgColor, Color fgColor)
    {
        try
        {
            using var ms = new MemoryStream(imageBytes);
            // Decode directly into a WriteableBitmap to modify pixels
            var writeableBitmap = WriteableBitmap.Decode(ms);
            
            using (var frameBuffer = writeableBitmap.Lock())
            {
                unsafe
                {
                    byte* ptr = (byte*)frameBuffer.Address;
                    int width = frameBuffer.Size.Width;
                    int height = frameBuffer.Size.Height;
                    int stride = frameBuffer.RowBytes;
                    var format = frameBuffer.Format;

                    for (int y = 0; y < height; y++)
                    {
                        byte* row = ptr + (y * stride);
                        for (int x = 0; x < width; x++)
                        {
                            int p = x * 4;
                            
                            // Avalonia generally uses BGRA8888 or RGBA8888
                            byte b = row[p];
                            byte g = row[p + 1];
                            byte r = row[p + 2];
                            byte a = row[p + 3];

                            // Skip fully transparent pixels
                            if (a == 0) continue;

                            // Calculate Luminance (standard weights)
                            double l = (0.299 * r + 0.587 * g + 0.114 * b) / 255.0;

                            // Interpolate between bgColor (dark) and fgColor (light)
                            // We can use a linear interpolation (Lerp)
                            byte newR = (byte)(bgColor.R + (fgColor.R - bgColor.R) * l);
                            byte newG = (byte)(bgColor.G + (fgColor.G - bgColor.G) * l);
                            byte newB = (byte)(bgColor.B + (fgColor.B - bgColor.B) * l);

                            row[p] = newB;     // B
                            row[p + 1] = newG; // G
                            row[p + 2] = newR; // R
                        }
                    }
                }
            }
            
            return writeableBitmap;
        }
        catch
        {
            // Fallback to standard bitmap if unsafe manipulation fails
            return new Bitmap(new MemoryStream(imageBytes));
        }
    }
}
