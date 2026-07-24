using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using AniCS.Desktop.Services;

namespace AniCS.Desktop.Converters;

public class EpisodeStatusBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EpisodeWatchStatus status && Application.Current != null)
        {
            string key = status switch
            {
                EpisodeWatchStatus.Completed => "AppStatusCompletedColor",
                EpisodeWatchStatus.InProgress => "AppStatusInProgressColor",
                _ => "AppStatusUnwatchedColor"
            };

            if (Application.Current.Resources.TryGetResource(key, Application.Current.ActualThemeVariant, out var res) && res is IBrush brush)
            {
                return brush;
            }
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
