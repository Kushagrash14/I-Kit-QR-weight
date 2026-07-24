using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WeightVerificationQR.App.ViewModels;

namespace WeightVerificationQR.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.LoggedOut += OnLoggedOut;
    }

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        var loginWindow = App.Services.GetRequiredService<LoginView>();
        loginWindow.Show();
        Close();
    }
}
