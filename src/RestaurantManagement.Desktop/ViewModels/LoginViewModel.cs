using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.DTOs;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Desktop.ViewModels;

public record DemoAccountItem(string Label, string Username, string Password, string RoleDescription, string BadgeColor);

public class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<LoginViewModel> _logger;

    private string _username = string.Empty;
    private string _displayName = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string? _errorMessage;
    private bool _isLoading;
    private bool _isSetupMode;

    public event Action? LoginSucceeded;

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public bool IsSetupMode
    {
        get => _isSetupMode;
        set => SetProperty(ref _isSetupMode, value);
    }

    public ObservableCollection<DemoAccountItem> DemoAccounts { get; }

    public ICommand LoginCommand { get; }
    public ICommand InitializeAdminCommand { get; }
    public ICommand SelectDemoAccountCommand { get; }

    public LoginViewModel(
        IAuthenticationService authenticationService,
        ICurrentUserService currentUserService,
        ILogger<LoginViewModel> logger)
    {
        _authenticationService = authenticationService;
        _currentUserService = currentUserService;
        _logger = logger;

        DemoAccounts = new ObservableCollection<DemoAccountItem>
        {
            new("Administrator", "admin", "Admin@123", "Full system access & employee management", "#818CF8"),
            new("Manager", "manager", "Manager@123", "Operations, menu, tables & billing", "#60A5FA"),
            new("Cashier", "cashier", "Cashier@123", "POS ordering & tables", "#34D399"),
            new("Kitchen Staff", "chef", "Chef@123", "Kitchen queue & order status", "#FBBF24")
        };

        LoginCommand = new RelayCommand(async _ => await ExecuteLoginAsync(), _ => !IsLoading);
        InitializeAdminCommand = new RelayCommand(async _ => await ExecuteInitializeAdminAsync(), _ => !IsLoading);
        SelectDemoAccountCommand = new RelayCommand<DemoAccountItem>(account =>
        {
            if (account != null)
            {
                Username = account.Username;
                Password = account.Password;
                ErrorMessage = null;
            }
        });
    }

    public async Task CheckInitialSetupAsync()
    {
        try
        {
            IsLoading = true;
            ErrorMessage = null;
            var hasEmployees = await _authenticationService.HasAnyEmployeesAsync();
            IsSetupMode = !hasEmployees;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if employees exist.");
            ErrorMessage = "Failed to connect to local database for authentication.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ExecuteLoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Please enter your username.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter your password.";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var result = await _authenticationService.LoginAsync(new LoginRequest(Username.Trim(), Password));
            if (result.Succeeded)
            {
                _logger.LogInformation("Login succeeded for '{Username}' ({Role})", result.Employee!.Username, result.Employee.Role);
                Password = string.Empty;
                ErrorMessage = null;
                LoginSucceeded?.Invoke();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Invalid username or password.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during login.");
            ErrorMessage = "An unexpected authentication error occurred.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task ExecuteInitializeAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || Username.Trim().Length < 3)
        {
            ErrorMessage = "Admin username must be at least 3 characters long.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Display name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Password) || Password.Length < 4)
        {
            ErrorMessage = "Password must be at least 4 characters long.";
            return;
        }

        if (Password != ConfirmPassword)
        {
            ErrorMessage = "Passwords do not match.";
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var request = new CreateFirstAdminRequest(Username.Trim(), DisplayName.Trim(), Password);
            var result = await _authenticationService.InitializeFirstAdminAsync(request);

            if (result.Succeeded)
            {
                _logger.LogInformation("Initial administrator initialized: '{Username}'", result.Employee!.Username);
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                IsSetupMode = false;
                LoginSucceeded?.Invoke();
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Failed to initialize administrator account.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize first administrator.");
            ErrorMessage = "An error occurred while creating the initial administrator account.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void ResetForm()
    {
        Username = string.Empty;
        DisplayName = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
        IsLoading = false;
    }
}
