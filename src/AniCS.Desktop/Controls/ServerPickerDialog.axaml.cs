using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System.Linq;
using AniCS.Models;

namespace AniCS.Desktop.Controls;

/// <summary>
/// Diálogo modal que presenta al usuario los servidores de video disponibles
/// para un episodio, equivalente al SelectionPrompt del CLI.
/// Uso: var server = await ServerPickerDialog.ShowAsync(owner, servers, episodeTitle);
/// </summary>
public partial class ServerPickerDialog : Window
{
    private VideoServer? _selectedServer;
    private string _selectedQuality = "Mejor";

    public ServerPickerDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Muestra el diálogo y retorna el servidor elegido, o null si el usuario canceló.
    /// </summary>
    public static async System.Threading.Tasks.Task<(VideoServer? Server, string Quality)> ShowAsync(
        Window owner,
        List<VideoServer> servers,
        string episodeTitle,
        bool showQualitySelector = true) // Parameter kept for interface compatibility but ignored
    {
        var dialog = new ServerPickerDialog();
        dialog.EpisodeTitleText.Text = episodeTitle;
        dialog.BuildServerButtons(servers);

        // Hereda el tema de la ventana padre aplicando sus recursos
        dialog.RequestedThemeVariant = owner.RequestedThemeVariant;

        var result = await dialog.ShowDialog<VideoServer?>(owner);
        return (result, dialog._selectedQuality);
    }

    private void BuildServerButtons(List<VideoServer> servers)
    {
        ServersPanel.Children.Clear();

        foreach (var server in servers)
        {
            var btn = new Button
            {
                Tag = server,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                Padding = new Thickness(14, 11),
                CornerRadius = new CornerRadius(6),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                BorderThickness = new Thickness(1),
            };

            // Visual: nombre del servidor + indicador de soporte
            var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };

            var nameText = new TextBlock
            {
                Text = server.Name,
                FontSize = 15,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 180
            };

            var badge = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = server.IsDirectPlaySupported ? "Nativo" : "yt-dlp",
                    FontSize = 11,
                    Foreground = Brushes.White,
                }
            };

            if (server.IsDirectPlaySupported)
            {
                badge.Background = new SolidColorBrush(Color.Parse("#22C55E")); // Verde
                btn.SetValue(BorderBrushProperty, new SolidColorBrush(Color.Parse("#22C55E80")));
            }
            else
            {
                badge.Background = new SolidColorBrush(Color.Parse("#F59E0B")); // Ámbar
                btn.SetValue(BorderBrushProperty, new SolidColorBrush(Color.Parse("#F59E0B50")));
            }

            panel.Children.Add(nameText);
            panel.Children.Add(badge);
            btn.Content = panel;

            // Heredar colores de fondo del tema dinámico
            btn.SetValue(BackgroundProperty, Application.Current?.Resources["AppSurfaceColor"] as ISolidColorBrush
                ?? new SolidColorBrush(Color.Parse("#44475A")));

            // Foreground en el texto del nombre del servidor
            if (Application.Current?.Resources["AppTitleColor"] is ISolidColorBrush fgBrush)
                nameText.Foreground = fgBrush;

            btn.Click += OnServerButtonClicked;
            ServersPanel.Children.Add(btn);
        }
    }

    private void OnServerButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VideoServer server)
        {
            _selectedServer = server;
            _selectedQuality = "Mejor";
            
            Close(_selectedServer);
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
