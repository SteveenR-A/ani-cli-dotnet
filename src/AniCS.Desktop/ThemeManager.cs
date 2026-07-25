using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace AniCS.Desktop
{
    public static class ThemeManager
    {
        private static readonly Dictionary<string, Dictionary<string, string>> Themes = new()
        {
            ["Dark"] = new()
            {
                { "AppBackgroundColor", "#000000" },
                { "AppSurfaceColor", "#121212" },
                { "AppPrimaryColor", "#FFFFFF" },
                { "AppPrimaryForegroundColor", "#000000" },
                { "AppTextColor", "#B3B3B3" },
                { "AppTitleColor", "#FFFFFF" },
                { "AppStatusCompletedColor", "#4CAF50" },
                { "AppStatusInProgressColor", "#FFB74D" },
                { "AppStatusUnwatchedColor", "#9E9E9E" },
                { "AppSuccessColor", "#4CAF50" },
                { "AppSuccessForegroundColor", "#FFFFFF" },
                { "AppDangerColor", "#E53935" },
                { "AppDangerForegroundColor", "#FFFFFF" },
                { "AppWarningColor", "#FFB300" },
                { "AppWarningForegroundColor", "#000000" }
            },
            ["Light"] = new()
            {
                { "AppBackgroundColor", "#FFFFFF" },
                { "AppSurfaceColor", "#F3F4F6" },
                { "AppPrimaryColor", "#005FB8" }, // Blue
                { "AppPrimaryForegroundColor", "#FFFFFF" },
                { "AppTextColor", "#1F2937" },
                { "AppTitleColor", "#111827" },
                { "AppStatusCompletedColor", "#2E7D32" },
                { "AppStatusInProgressColor", "#EF6C00" },
                { "AppStatusUnwatchedColor", "#757575" },
                { "AppSuccessColor", "#2E7D32" },
                { "AppSuccessForegroundColor", "#FFFFFF" },
                { "AppDangerColor", "#D32F2F" },
                { "AppDangerForegroundColor", "#FFFFFF" },
                { "AppWarningColor", "#ED6C02" },
                { "AppWarningForegroundColor", "#FFFFFF" }
            },
            ["Dracula"] = new()
            {
                { "AppBackgroundColor", "#282A36" },
                { "AppSurfaceColor", "#44475A" },
                { "AppPrimaryColor", "#FF79C6" }, // Pink
                { "AppPrimaryForegroundColor", "#282A36" },
                { "AppTextColor", "#F8F8F2" },
                { "AppTitleColor", "#BD93F9" }, // Purple
                { "AppStatusCompletedColor", "#50FA7B" },
                { "AppStatusInProgressColor", "#FFB86C" },
                { "AppStatusUnwatchedColor", "#8BE9FD" },
                { "AppSuccessColor", "#50FA7B" },
                { "AppSuccessForegroundColor", "#282A36" },
                { "AppDangerColor", "#FF5555" },
                { "AppDangerForegroundColor", "#F8F8F2" },
                { "AppWarningColor", "#FFB86C" },
                { "AppWarningForegroundColor", "#282A36" }
            },
            ["TokyoNight"] = new()
            {
                { "AppBackgroundColor", "#1A1B26" },
                { "AppSurfaceColor", "#24283B" },
                { "AppPrimaryColor", "#7DCFFF" }, // Cyan
                { "AppPrimaryForegroundColor", "#1A1B26" },
                { "AppTextColor", "#A9B1D6" },
                { "AppTitleColor", "#BB9AF7" }, // Purple/Violet
                { "AppStatusCompletedColor", "#9ECE6A" },
                { "AppStatusInProgressColor", "#E0AF68" },
                { "AppStatusUnwatchedColor", "#7AA2F7" },
                { "AppSuccessColor", "#9ECE6A" },
                { "AppSuccessForegroundColor", "#1A1B26" },
                { "AppDangerColor", "#F7768E" },
                { "AppDangerForegroundColor", "#1A1B26" },
                { "AppWarningColor", "#E0AF68" },
                { "AppWarningForegroundColor", "#1A1B26" }
            },
            ["Cyberpunk"] = new()
            {
                { "AppBackgroundColor", "#09090B" }, // Very dark grey/black
                { "AppSurfaceColor", "#1A1A24" }, // Dark purple/blue tint
                { "AppPrimaryColor", "#00FFFF" }, // Cyan neon
                { "AppPrimaryForegroundColor", "#000000" },
                { "AppTextColor", "#E0E0E0" },
                { "AppTitleColor", "#FF003C" }, // Red neon
                { "AppStatusCompletedColor", "#00FF66" },
                { "AppStatusInProgressColor", "#FFD700" },
                { "AppStatusUnwatchedColor", "#808090" },
                { "AppSuccessColor", "#00FF66" },
                { "AppSuccessForegroundColor", "#000000" },
                { "AppDangerColor", "#FF0055" },
                { "AppDangerForegroundColor", "#FFFFFF" },
                { "AppWarningColor", "#FFD700" },
                { "AppWarningForegroundColor", "#000000" }
            },
            ["Catppuccin"] = new()
            {
                { "AppBackgroundColor", "#1E1E2E" }, // Mocha Base
                { "AppSurfaceColor", "#313244" }, // Surface0
                { "AppPrimaryColor", "#CBA6F7" }, // Mauve
                { "AppPrimaryForegroundColor", "#11111B" }, // Crust
                { "AppTextColor", "#CDD6F4" }, // Text
                { "AppTitleColor", "#F5C2E7" }, // Pink
                { "AppStatusCompletedColor", "#A6E3A1" }, // Green
                { "AppStatusInProgressColor", "#F9E2AF" }, // Yellow
                { "AppStatusUnwatchedColor", "#89DCEB" }, // Sky
                { "AppSuccessColor", "#A6E3A1" },
                { "AppSuccessForegroundColor", "#11111B" },
                { "AppDangerColor", "#F38BA8" },
                { "AppDangerForegroundColor", "#11111B" },
                { "AppWarningColor", "#F9E2AF" },
                { "AppWarningForegroundColor", "#11111B" }
            }
        };

        public static void ApplyTheme(string themeName)
        {
            if (!Themes.ContainsKey(themeName))
                themeName = "Dark";

            var palette = Themes[themeName];
            
            // Reemplazamos los SolidColorBrushes de forma dinámica para toda la app.
            foreach (var kvp in palette)
            {
                if (Application.Current != null)
                {
                    Application.Current.Resources[kvp.Key] = new SolidColorBrush(Color.Parse(kvp.Value));
                }
            }

            // Set system theme variant so controls like ComboBox render correctly in Light mode
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = themeName == "Light" 
                    ? Avalonia.Styling.ThemeVariant.Light 
                    : Avalonia.Styling.ThemeVariant.Dark;
            }
            
            // Fix text contrast globally for cyberpunk main window background
            if (themeName == "Cyberpunk")
            {
                 // We already set TitleColor to red neon, no need for hardcoded override here
            }
        }
    }
}
