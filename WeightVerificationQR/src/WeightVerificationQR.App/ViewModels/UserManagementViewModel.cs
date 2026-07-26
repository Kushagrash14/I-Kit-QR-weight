using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using WeightVerificationQR.Core.Interfaces;
using WeightVerificationQR.Core.Models;

namespace WeightVerificationQR.App.ViewModels;

public class UserManagementViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SessionContext _session;

    public UserManagementViewModel(IUserRepository userRepository, IPasswordHasher passwordHasher, SessionContext session)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _session = session;

        Users = new ObservableCollection<User>();

        // Role hierarchy: you can only assign roles strictly BELOW your own rank.
        // SuperAdmin -> can create Admin/Supervisor/Operator; Admin -> Supervisor/Operator.
        Roles = Enum.GetValues<UserRole>()
            .Where(r => SessionContext.RankOf(r) < _session.CurrentRank)
            .OrderByDescending(SessionContext.RankOf)
            .ToList();

        NewCommand = new RelayCommand(StartNew);
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        DeleteCommand = new AsyncRelayCommand<User>(DeleteAsync);
        EditCommand = new RelayCommand<User>(Edit);

        _ = LoadAsync();
    }

    public ObservableCollection<User> Users { get; }
    public IEnumerable<UserRole> Roles { get; }

    private User? _editingUser;
    public User? EditingUser { get => _editingUser; set => SetProperty(ref _editingUser, value); }

    private string _newPassword = string.Empty;
    public string NewPassword { get => _newPassword; set => SetProperty(ref _newPassword, value); }

    private bool _isEditing;
    public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

    private string _statusMessage = string.Empty;
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public ICommand NewCommand { get; }
    public IAsyncRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand<User> DeleteCommand { get; }
    public ICommand EditCommand { get; }

    /// <summary>True when the current user outranks the given user and may modify them.</summary>
    private bool CanManage(User user) =>
        user.Id != _session.CurrentUser?.Id && SessionContext.RankOf(user.Role) < _session.CurrentRank;

    private async Task LoadAsync()
    {
        var users = await _userRepository.GetAllAsync();
        Users.Clear();
        foreach (var u in users) Users.Add(u);
    }

    private void StartNew()
    {
        EditingUser = new User { Role = UserRole.Operator, IsActive = true };
        NewPassword = string.Empty;
        StatusMessage = string.Empty;
        IsEditing = true;
    }

    private void Edit(User? user)
    {
        if (user is null) return;

        if (!CanManage(user))
        {
            StatusMessage = user.Id == _session.CurrentUser?.Id
                ? "You cannot edit your own account from here."
                : $"Only a higher role can modify a {user.Role} account.";
            return;
        }

        EditingUser = new User
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Role = user.Role,
            IsActive = user.IsActive,
            PasswordHash = user.PasswordHash,
            PasswordSalt = user.PasswordSalt
        };
        NewPassword = string.Empty;
        StatusMessage = string.Empty;
        IsEditing = true;
    }

    private async Task SaveAsync()
    {
        if (EditingUser is null) return;

        if (string.IsNullOrWhiteSpace(EditingUser.FullName) || string.IsNullOrWhiteSpace(EditingUser.Username))
        {
            StatusMessage = "Full name and username are required.";
            return;
        }

        // Never allow saving a user at or above the current user's own rank.
        if (SessionContext.RankOf(EditingUser.Role) >= _session.CurrentRank)
        {
            StatusMessage = $"You are not allowed to assign the {EditingUser.Role} role.";
            return;
        }

        if (EditingUser.Id == 0)
        {
            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
            {
                StatusMessage = "A password of at least 6 characters is required for new users.";
                return;
            }
            var (hash, salt) = _passwordHasher.HashPassword(NewPassword);
            EditingUser.PasswordHash = hash;
            EditingUser.PasswordSalt = salt;
            await _userRepository.AddAsync(EditingUser);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                if (NewPassword.Length < 6)
                {
                    StatusMessage = "Password must be at least 6 characters.";
                    return;
                }
                var (hash, salt) = _passwordHasher.HashPassword(NewPassword);
                EditingUser.PasswordHash = hash;
                EditingUser.PasswordSalt = salt;
            }
            await _userRepository.UpdateAsync(EditingUser);
        }

        StatusMessage = $"User '{EditingUser.FullName}' saved.";
        IsEditing = false;
        EditingUser = null;
        await LoadAsync();
    }

    private async Task DeleteAsync(User? user)
    {
        if (user is null) return;

        if (!CanManage(user))
        {
            StatusMessage = user.Id == _session.CurrentUser?.Id
                ? "You cannot deactivate your own account."
                : $"Only a higher role can deactivate a {user.Role} account.";
            return;
        }

        await _userRepository.DeleteAsync(user.Id);
        StatusMessage = $"User '{user.FullName}' deactivated.";
        await LoadAsync();
    }
}
