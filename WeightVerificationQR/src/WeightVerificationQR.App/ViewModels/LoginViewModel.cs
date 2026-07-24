using CommunityToolkit.Mvvm.Input;
using WeightVerificationQR.Core.Interfaces;

namespace WeightVerificationQR.App.ViewModels;

public class LoginViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SessionContext _session;

    public LoginViewModel(IUserRepository userRepository, IPasswordHasher passwordHasher, SessionContext session)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _session = session;
        LoginCommand = new AsyncRelayCommand<string>(LoginAsync, _ => !IsBusy);
    }

    private string _username = string.Empty;
    public string Username { get => _username; set => SetProperty(ref _username, value); }

    private string _errorMessage = string.Empty;
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

    public IAsyncRelayCommand<string> LoginCommand { get; }

    /// <summary>Raised when login succeeds so the code-behind can open MainWindow and close itself.</summary>
    public event EventHandler? LoginSucceeded;

    // PasswordBox.Password cannot be data-bound directly (by design, for security),
    // so the plain-text password is passed in as the command parameter from the view's code-behind.
    private async Task LoginAsync(string? password)
    {
        ErrorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorMessage = "Please enter both username and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var user = await _userRepository.GetByUsernameAsync(Username.Trim());
            if (user is null || !_passwordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            {
                ErrorMessage = "Invalid username or password.";
                return;
            }

            user.LastLoginAt = DateTime.Now;
            await _userRepository.UpdateAsync(user);

            _session.CurrentUser = user;
            LoginSucceeded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Login failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
