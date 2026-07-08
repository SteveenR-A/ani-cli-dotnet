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
                { "AppTitleColor", "#FFFFFF" }
            },
            ["Light"] = new()
            {
                { "AppBackgroundColor", "#FFFFFF" },
                { "AppSurfaceColor", "#F3F4F6" },
                { "AppPrimaryColor", "#005FB8" }, // Blue
                { "AppPrimaryForegroundColor", "#FFFFFF" },
                { "AppTextColor", "#000000" },
                { "AppTitleColor", "#111827" }
            },
            ["Dracula"] = new()
            {
                { "AppBackgroundColor", "#282A36" },
                { "AppSurfaceColor", "#44475A" },
                { "AppPrimaryColor", "#FF79C6" }, // Pink
                { "AppPrimaryForegroundColor", "#282A36" },
                { "AppTextColor", "#F8F8F2" },
                { "AppTitleColor", "#BD93F9" } // Purple
            },
            ["TokyoNight"] = new()
            {
                { "AppBackgroundColor", "#1A1B26" },
                { "AppSurfaceColor", "#24283B" },
                { "AppPrimaryColor", "#7DCFFF" }, // Cyan
                { "AppPrimaryForegroundColor", "#1A1B26" },
                { "AppTextColor", "#A9B1D6" },
                { "AppTitleColor", "#BB9AF7" } // Purple/Violet
            },
            ["Cyberpunk"] = new()
            {
                { "AppBackgroundColor", "#09090B" }, // Very dark grey/black
                { "AppSurfaceColor", "#1A1A24" }, // Dark purple/blue tint
                { "AppPrimaryColor", "#00FFFF" }, // Cyan neon
                { "AppPrimaryForegroundColor", "#000000" },
                { "AppTextColor", "#E0E0E0" },
                { "AppTitleColor", "#FF003C" } // Red neon
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
