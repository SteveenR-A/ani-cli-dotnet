using Avalonia.Controls;

namespace AniCS.Desktop.Views.Paradigms.ASCII;

public partial class ASCIIView : UserControl
{
    public ASCIIView()
    {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        
        // El evento Loaded asegura que el control ya está en pantalla y puede recibir foco real
        this.Loaded += (s, e) => {
            if (DataContext is ViewModels.HomeViewModel vm && !vm.IsReloading && vm.AnimeList.Count > 0)
            {
                AnimeListBox.SelectedIndex = 0;
                AnimeListBox.Focus();
            }
        };
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is ViewModels.HomeViewModel vm)
        {
            vm.PropertyChanged += (s, args) => 
            {
                if (args.PropertyName == nameof(vm.IsReloading) && !vm.IsReloading)
                {
                    Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                        if (AnimeListBox.ItemCount > 0)
                        {
                            // Seleccionar y forzar el foco del teclado al primer elemento
                            AnimeListBox.SelectedIndex = 0;
                            AnimeListBox.Focus();
                        }
                    }, Avalonia.Threading.DispatcherPriority.Loaded); // Usamos prioridad Loaded
                }
            };
        }
    }

    private void OnListBoxKeyUp(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter || e.Key == Avalonia.Input.Key.Space)
        {
            HandleSelection();
            e.Handled = true;
        }
    }

    private void OnListBoxDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        HandleSelection();
    }

    private void HandleSelection()
    {
        var selectedAnime = AnimeListBox.SelectedItem as AniCS.Models.AnimeResult;
        if (selectedAnime != null && Avalonia.Controls.TopLevel.GetTopLevel(this) is Avalonia.Controls.Window window)
        {
            if (window is MainWindow mainWindow)
            {
                mainWindow.NavigateToAnimeDetails(selectedAnime);
            }
        }
    }
}
