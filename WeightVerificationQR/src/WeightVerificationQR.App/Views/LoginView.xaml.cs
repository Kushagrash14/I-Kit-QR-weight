using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using WeightVerificationQR.App.ViewModels;

namespace WeightVerificationQR.App.Views;

public partial class LoginView : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginView(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.LoginSucceeded += OnLoginSucceeded;
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.LoginCommand.Execute(PasswordBox.Password);

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _viewModel.LoginCommand.Execute(PasswordBox.Password);
    }

    private void OnLoginSucceeded(object? sender, EventArgs e)
    {
        var mainWindow = App.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        Close();
    }
}
