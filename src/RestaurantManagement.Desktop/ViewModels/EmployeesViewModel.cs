using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Employees.DTOs;
using RestaurantManagement.Application.Employees.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Desktop.ViewModels;

public class EmployeesViewModel : ViewModelBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ILogger<EmployeesViewModel> _logger;

    private readonly List<EmployeeDto> _allEmployees = new();
    private ObservableCollection<EmployeeDto> _filteredEmployees = new();
    private EmployeeDto? _selectedEmployee;

    private string _searchText = string.Empty;
    private EmployeeRole? _selectedRoleFilter;
    private bool _isLoading;
    private string? _statusMessage;
    private bool _isStatusError;

    // Modal dialog controls
    private bool _isAddDialogOpen;
    private bool _isEditDialogOpen;
    private bool _isResetPasswordDialogOpen;

    // Add Form Fields
    private string _newUsername = string.Empty;
    private string _newDisplayName = string.Empty;
    private string _newPassword = string.Empty;
    private EmployeeRole _newRole = EmployeeRole.Cashier;
    private bool _newIsActive = true;
    private string? _dialogErrorMessage;

    // Edit Form Fields
    private Guid _editEmployeeId;
    private string _editUsername = string.Empty;
    private string _editDisplayName = string.Empty;
    private EmployeeRole _editRole = EmployeeRole.Cashier;
    private bool _editIsActive = true;

    // Reset Password Form Fields
    private Guid _resetEmployeeId;
    private string _resetUsername = string.Empty;
    private string _resetPassword = string.Empty;
    private string _resetConfirmPassword = string.Empty;

    public ObservableCollection<EmployeeDto> FilteredEmployees
    {
        get => _filteredEmployees;
        set => SetProperty(ref _filteredEmployees, value);
    }

    public EmployeeDto? SelectedEmployee
    {
        get => _selectedEmployee;
        set => SetProperty(ref _selectedEmployee, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilter();
            }
        }
    }

    public EmployeeRole? SelectedRoleFilter
    {
        get => _selectedRoleFilter;
        set
        {
            if (SetProperty(ref _selectedRoleFilter, value))
            {
                ApplyFilter();
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (SetProperty(ref _statusMessage, value))
            {
                OnPropertyChanged(nameof(HasStatusMessage));
            }
        }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    public bool IsStatusError
    {
        get => _isStatusError;
        set => SetProperty(ref _isStatusError, value);
    }

    public bool IsAddDialogOpen
    {
        get => _isAddDialogOpen;
        set => SetProperty(ref _isAddDialogOpen, value);
    }

    public bool IsEditDialogOpen
    {
        get => _isEditDialogOpen;
        set => SetProperty(ref _isEditDialogOpen, value);
    }

    public bool IsResetPasswordDialogOpen
    {
        get => _isResetPasswordDialogOpen;
        set => SetProperty(ref _isResetPasswordDialogOpen, value);
    }

    public string? DialogErrorMessage
    {
        get => _dialogErrorMessage;
        set => SetProperty(ref _dialogErrorMessage, value);
    }

    // Add Form Properties
    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    public string NewDisplayName
    {
        get => _newDisplayName;
        set => SetProperty(ref _newDisplayName, value);
    }

    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    public EmployeeRole NewRole
    {
        get => _newRole;
        set => SetProperty(ref _newRole, value);
    }

    public bool NewIsActive
    {
        get => _newIsActive;
        set => SetProperty(ref _newIsActive, value);
    }

    // Edit Form Properties
    public string EditUsername
    {
        get => _editUsername;
        set => SetProperty(ref _editUsername, value);
    }

    public string EditDisplayName
    {
        get => _editDisplayName;
        set => SetProperty(ref _editDisplayName, value);
    }

    public EmployeeRole EditRole
    {
        get => _editRole;
        set => SetProperty(ref _editRole, value);
    }

    public bool EditIsActive
    {
        get => _editIsActive;
        set => SetProperty(ref _editIsActive, value);
    }

    // Reset Password Properties
    public string ResetUsername
    {
        get => _resetUsername;
        set => SetProperty(ref _resetUsername, value);
    }

    public string ResetPassword
    {
        get => _resetPassword;
        set => SetProperty(ref _resetPassword, value);
    }

    public string ResetConfirmPassword
    {
        get => _resetConfirmPassword;
        set => SetProperty(ref _resetConfirmPassword, value);
    }

    public IReadOnlyList<EmployeeRole> AvailableRoles { get; } = Enum.GetValues<EmployeeRole>();

    // Commands
    public ICommand LoadEmployeesCommand { get; }
    public ICommand OpenAddDialogCommand { get; }
    public ICommand SaveNewEmployeeCommand { get; }
    public ICommand OpenEditDialogCommand { get; }
    public ICommand SaveEditEmployeeCommand { get; }
    public ICommand OpenResetPasswordDialogCommand { get; }
    public ICommand SaveResetPasswordCommand { get; }
    public ICommand ToggleActiveStatusCommand { get; }
    public ICommand DeleteEmployeeCommand { get; }
    public ICommand CloseDialogCommand { get; }

    public EmployeesViewModel(
        IEmployeeService employeeService,
        ILogger<EmployeesViewModel> logger)
    {
        _employeeService = employeeService;
        _logger = logger;

        LoadEmployeesCommand = new RelayCommand(async _ => await LoadEmployeesAsync());
        OpenAddDialogCommand = new RelayCommand(_ => OpenAddDialog());
        SaveNewEmployeeCommand = new RelayCommand(async _ => await SaveNewEmployeeAsync());
        OpenEditDialogCommand = new RelayCommand<EmployeeDto>(emp => OpenEditDialog(emp));
        SaveEditEmployeeCommand = new RelayCommand(async _ => await SaveEditEmployeeAsync());
        OpenResetPasswordDialogCommand = new RelayCommand<EmployeeDto>(emp => OpenResetPasswordDialog(emp));
        SaveResetPasswordCommand = new RelayCommand(async _ => await SaveResetPasswordAsync());
        ToggleActiveStatusCommand = new RelayCommand<EmployeeDto>(async emp => await ToggleActiveStatusAsync(emp));
        DeleteEmployeeCommand = new RelayCommand<EmployeeDto>(async emp => await DeleteEmployeeAsync(emp));
        CloseDialogCommand = new RelayCommand(_ => CloseDialogs());
    }

    public async Task LoadEmployeesAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = null;

            var employees = await _employeeService.GetEmployeesAsync(includeInactive: true);
            _allEmployees.Clear();
            _allEmployees.AddRange(employees);

            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load employee list.");
            SetStatus("Failed to load employee records from database.", isError: true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFilter()
    {
        var query = _allEmployees.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim().ToLowerInvariant();
            query = query.Where(e => e.Username.ToLowerInvariant().Contains(search) ||
                                     e.DisplayName.ToLowerInvariant().Contains(search));
        }

        if (SelectedRoleFilter.HasValue)
        {
            query = query.Where(e => e.Role == SelectedRoleFilter.Value);
        }

        FilteredEmployees = new ObservableCollection<EmployeeDto>(query);
    }

    private void OpenAddDialog()
    {
        NewUsername = string.Empty;
        NewDisplayName = string.Empty;
        NewPassword = string.Empty;
        NewRole = EmployeeRole.Cashier;
        NewIsActive = true;
        DialogErrorMessage = null;
        IsAddDialogOpen = true;
    }

    private async Task SaveNewEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(NewUsername) || NewUsername.Trim().Length < 3)
        {
            DialogErrorMessage = "Username must be at least 3 characters long.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewDisplayName))
        {
            DialogErrorMessage = "Display name is required.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 4)
        {
            DialogErrorMessage = "Password must be at least 4 characters long.";
            return;
        }

        try
        {
            IsLoading = true;
            DialogErrorMessage = null;

            var request = new CreateEmployeeRequest(
                NewUsername.Trim(),
                NewDisplayName.Trim(),
                NewPassword,
                NewRole,
                NewIsActive);

            var created = await _employeeService.CreateEmployeeAsync(request);
            CloseDialogs();
            await LoadEmployeesAsync();
            SetStatus($"Employee '{created.Username}' created successfully.", isError: false);
        }
        catch (ValidationException vex)
        {
            DialogErrorMessage = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating employee.");
            DialogErrorMessage = "Failed to create employee account. Please verify input.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenEditDialog(EmployeeDto? emp)
    {
        var target = emp ?? SelectedEmployee;
        if (target == null) return;

        _editEmployeeId = target.Id;
        EditUsername = target.Username;
        EditDisplayName = target.DisplayName;
        EditRole = target.Role;
        EditIsActive = target.IsActive;
        DialogErrorMessage = null;
        IsEditDialogOpen = true;
    }

    private async Task SaveEditEmployeeAsync()
    {
        if (string.IsNullOrWhiteSpace(EditDisplayName))
        {
            DialogErrorMessage = "Display name cannot be empty.";
            return;
        }

        try
        {
            IsLoading = true;
            DialogErrorMessage = null;

            var request = new UpdateEmployeeRequest(_editEmployeeId, EditDisplayName.Trim(), EditRole, EditIsActive);
            var updated = await _employeeService.UpdateEmployeeAsync(request);
            CloseDialogs();
            await LoadEmployeesAsync();
            SetStatus($"Employee '{updated.Username}' updated successfully.", isError: false);
        }
        catch (ValidationException vex)
        {
            DialogErrorMessage = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating employee.");
            DialogErrorMessage = "Failed to update employee account.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void OpenResetPasswordDialog(EmployeeDto? emp)
    {
        var target = emp ?? SelectedEmployee;
        if (target == null) return;

        _resetEmployeeId = target.Id;
        ResetUsername = target.Username;
        ResetPassword = string.Empty;
        ResetConfirmPassword = string.Empty;
        DialogErrorMessage = null;
        IsResetPasswordDialogOpen = true;
    }

    private async Task SaveResetPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(ResetPassword) || ResetPassword.Length < 4)
        {
            DialogErrorMessage = "New password must be at least 4 characters long.";
            return;
        }

        if (ResetPassword != ResetConfirmPassword)
        {
            DialogErrorMessage = "Passwords do not match.";
            return;
        }

        try
        {
            IsLoading = true;
            DialogErrorMessage = null;

            var request = new AdminResetPasswordRequest(_resetEmployeeId, ResetPassword);
            await _employeeService.AdminResetPasswordAsync(request);
            CloseDialogs();
            SetStatus($"Password for '{ResetUsername}' was reset successfully.", isError: false);
        }
        catch (ValidationException vex)
        {
            DialogErrorMessage = vex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting employee password.");
            DialogErrorMessage = "Failed to reset password.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ToggleActiveStatusAsync(EmployeeDto? emp)
    {
        if (emp == null) return;

        try
        {
            IsLoading = true;
            StatusMessage = null;

            if (emp.IsActive)
            {
                await _employeeService.DeactivateEmployeeAsync(emp.Id);
                SetStatus($"Employee '{emp.Username}' deactivated.", isError: false);
            }
            else
            {
                await _employeeService.ActivateEmployeeAsync(emp.Id);
                SetStatus($"Employee '{emp.Username}' activated.", isError: false);
            }

            await LoadEmployeesAsync();
        }
        catch (ValidationException vex)
        {
            SetStatus(vex.Message, isError: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling employee active status.");
            SetStatus("Failed to update employee status.", isError: true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteEmployeeAsync(EmployeeDto? emp)
    {
        var target = emp ?? SelectedEmployee;
        if (target == null && _isEditDialogOpen)
        {
            target = _allEmployees.FirstOrDefault(e => e.Id == _editEmployeeId);
        }
        if (target == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to permanently delete employee account '{target.Username}' ({target.DisplayName})?",
            "Confirm Delete Employee",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsLoading = true;
            StatusMessage = null;
            await _employeeService.DeleteEmployeeAsync(target.Id);
            CloseDialogs();
            await LoadEmployeesAsync();
            SetStatus($"Employee account '{target.Username}' was deleted successfully.", isError: false);
        }
        catch (ValidationException vex)
        {
            SetStatus(vex.Message, isError: true);
            if (IsEditDialogOpen)
            {
                DialogErrorMessage = vex.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting employee '{Username}'.", target.Username);
            SetStatus("Failed to delete employee account. Please check system logs.", isError: true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void CloseDialogs()
    {
        IsAddDialogOpen = false;
        IsEditDialogOpen = false;
        IsResetPasswordDialogOpen = false;
        DialogErrorMessage = null;
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsStatusError = isError;
    }
}
