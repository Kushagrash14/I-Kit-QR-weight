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

        if (CurrentViewModel is not null &&
            string.Equals(CurrentPage, page, StringComparison.Ordinal))
        {
            return;
        }

        ViewModelBase? nextViewModel = page switch
        {
            "Dashboard" => _serviceProvider.GetRequiredService<DashboardViewModel>(),
            "ProductMaster" when RequiresRole(UserRole.Admin) => _serviceProvider.GetRequiredService<ProductMasterViewModel>(),
            "UserManagement" when RequiresRole(UserRole.Admin) => _serviceProvider.GetRequiredService<UserManagementViewModel>(),
            "Reports" when RequiresRole(UserRole.Supervisor) => _serviceProvider.GetRequiredService<ReportsViewModel>(),
            "MachineSettings" when RequiresRole(UserRole.Admin) => _serviceProvider.GetRequiredService<MachineSettingsViewModel>(),
            "PrinterSettings" when RequiresRole(UserRole.Admin) => _serviceProvider.GetRequiredService<PrinterSettingsViewModel>(),
            "QrReprint" when RequiresRole(UserRole.Supervisor) => _serviceProvider.GetRequiredService<QrReprintViewModel>(),
            _ => null
        };

        if (nextViewModel is null)
            return;

        ReleaseCurrentViewModel();
        CurrentViewModel = nextViewModel;
        CurrentPage = page;
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

    private void Logout()
    {
        ReleaseCurrentViewModel();
        CurrentPage = string.Empty;
        _session.CurrentUser = null;
        LoggedOut?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Refreshes user-dependent bindings after a new login (singleton VM, new session).</summary>
    public void RefreshSession()
    {
        OnPropertyChanged(nameof(LoggedInUserName));
        OnPropertyChanged(nameof(LoggedInUserRole));
        OnPropertyChanged(nameof(IsAdmin));
        OnPropertyChanged(nameof(IsSupervisorOrAbove));
        Navigate("Dashboard");
    }

    private void ReleaseCurrentViewModel()
    {
        if (CurrentViewModel is IDisposable disposable)
            disposable.Dispose();

        CurrentViewModel = null;
    }
}
