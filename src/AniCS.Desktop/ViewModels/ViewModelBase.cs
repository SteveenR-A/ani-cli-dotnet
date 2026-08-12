using CommunityToolkit.Mvvm.ComponentModel;

namespace AniCS.Desktop.ViewModels;

/// <summary>
/// Base for all view models. Thin wrapper over CommunityToolkit.Mvvm's
/// ObservableObject so the rest of the code keeps using SetProperty/OnPropertyChanged.
/// </summary>
public class ViewModelBase : ObservableObject
{
}