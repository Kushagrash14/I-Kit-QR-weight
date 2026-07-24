using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SessionContext _session;

    public MainViewModel(IServiceProvider serviceProvider, SessionContext session)
    {
        _serviceProvider = serviceProvider;
        _session = session;

        NavigateCommand = new RelayCommand<string>(Navigate);
        LogoutCommand = new RelayCommand(Logout);

        Navigate("Dashboard");
    }

    private ViewModelBase? _currentViewModel;
    public ViewModelBase? CurrentViewModel { get => _currentViewModel; set => SetProperty(ref _currentViewModel, value); }

    private string _currentPage = "Dashboard";
    public string CurrentPage { get => _currentPage; set => SetProperty(ref _currentPage, value); }

    public string LoggedInUserName => _session.CurrentUser?.FullName ?? "Unknown";
    public string LoggedInUserRole => _session.CurrentUser?.Role.ToString() ?? "";

    public bool IsAdmin => _session.IsAdmin;
    public bool IsSupervisorOrAbove => _session.IsSupervisorOrAbove;

    public ICommand LogoutCommand { get; }
    public ICommand NavigateCommand { get; }

    public event EventHandler? LoggedOut;

    private void Navigate(string? page)
    {
        if (string.IsNullOrEmpty(page)) return;

        CurrentPage = page;
        CurrentViewModel = page switch
        {
            "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            "ProductMaster" => RequiresRole(UserRole.Admin) ? _serviceProvider.GetRequiredService<ProductMasterViewModel>() : CurrentViewModel,
            "UserManagement" => RequiresRole(UserRole.Admin) ? _serviceProvider.GetRequiredService<UserManagementViewModel>() : CurrentViewModel,
            "Reports" => RequiresRole(UserRole.Supervisor) ? _serviceProvider.GetRequiredService<ReportsViewModel>() : CurrentViewModel,
            "MachineSettings" => RequiresRole(UserRole.Admin) ? _serviceProvider.GetRequiredService<MachineSettingsViewModel>() : CurrentViewModel,
            "PrinterSettings" => RequiresRole(UserRole.Admin) ? _serviceProvider.GetRequiredService<PrinterSettingsViewModel>() : CurrentViewModel,
            "QrReprint" => RequiresRole(UserRole.Supervisor) ? _serviceProvider.GetRequiredService<QrReprintViewModel>() : CurrentViewModel,
            _ => CurrentViewModel
        };
    }

    /// <summary>Admin implicitly satisfies Supervisor-level checks; Supervisor satisfies Supervisor-level only.</summary>
    private bool RequiresRole(UserRole minimumRole)
    {
        if (_session.CurrentUser is null) return false;
        return minimumRole switch
        {
            UserRole.Admin => _session.IsAdmin,
            UserRole.Supervisor => _session.IsSupervisorOrAbove,
            _ => true
        };
    }

    private void Logout() => LoggedOut?.Invoke(this, EventArgs.Empty);
}
