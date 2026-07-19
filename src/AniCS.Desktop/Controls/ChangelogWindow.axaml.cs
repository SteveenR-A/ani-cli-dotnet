using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AniCS.Desktop.Controls;

public partial class ChangelogWindow : Window
{
    public ChangelogWindow()
    {
        InitializeComponent();
    }

    public ChangelogWindow(string version, string changelogText) : this()
    {
        var versionText = this.FindControl<TextBlock>("VersionText");
        var textBlock = this.FindControl<TextBlock>("ChangelogText");
        
        if (versionText != null) versionText.Text = $"Versión {version}";
        if (textBlock != null) textBlock.Text = changelogText;
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }
}
