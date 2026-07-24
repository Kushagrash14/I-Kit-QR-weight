using CommunityToolkit.Mvvm.ComponentModel;

namespace WeightVerificationQR.App.ViewModels;

/// <summary>
/// Base class for all ViewModels. CommunityToolkit.Mvvm's ObservableObject already
/// implements INotifyPropertyChanged with SetProperty helpers, so every ViewModel
/// in this app inherits from here instead of hand-rolling INotifyPropertyChanged.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
}
