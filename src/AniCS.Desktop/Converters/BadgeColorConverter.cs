using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AniCS.Desktop.Converters;

public class BadgeColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string typeStr)
        {
            var type = typeStr.ToUpper();
            
            if (type.Contains("DONGHUA"))
                return Brush.Parse("#F47521"); // Naranja
            if (type.Contains("ESPECIAL") || type.Contains("PELICULA") || type.Contains("PELÍCULA"))
                return Brush.Parse("#4CAF50"); // Verde
            if (type.Contains("OVA") || type.Contains("ONA"))
                return Brush.Parse("#2196F3"); // Azul
                
            return Brush.Parse("#F47521"); // Fallback
        }

        return Brush.Parse("#F47521");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
