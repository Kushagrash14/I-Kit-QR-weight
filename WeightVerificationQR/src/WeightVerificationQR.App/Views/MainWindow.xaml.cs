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

        // MainViewModel is a singleton but a fresh MainWindow is created per login -
        // re-read the session so user name/role/permissions reflect the new login.
        _viewModel.RefreshSession();
    }

    private void OnLoggedOut(object? sender, EventArgs e)
    {
        // Unsubscribe so previous (closed) windows don't also react to future logouts.
        _viewModel.LoggedOut -= OnLoggedOut;
        var loginWindow = App.Services.GetRequiredService<LoginView>();
        loginWindow.Show();
        Close();
    }
}
