using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Authentication.DTOs;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Employees.DTOs;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        RestaurantDbContext dbContext,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService,
        IDateTimeProvider dateTimeProvider,
        ILogger<AuthenticationService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return LoginResult.Failed("Username and password are required.");
        }

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        var employee = await _dbContext.Employees
            .FirstOrDefaultAsync(e => e.Username == normalizedUsername, cancellationToken);

        if (employee == null)
        {
            _logger.LogWarning("Login failed: Username '{Username}' not found.", normalizedUsername);
            return LoginResult.Failed("Invalid username or password.");
        }

        if (!employee.IsActive)
        {
            _logger.LogWarning("Login rejected: Employee '{Username}' is deactivated.", normalizedUsername);
            return LoginResult.Failed("This account is deactivated. Please contact an administrator.");
        }

        var isPasswordValid = _passwordHasher.VerifyPassword(request.Password, employee.PasswordHash);
        if (!isPasswordValid)
        {
            _logger.LogWarning("Login failed: Incorrect password for user '{Username}'.", normalizedUsername);
            return LoginResult.Failed("Invalid username or password.");
        }

        var now = _dateTimeProvider.UtcNow;
        employee.RecordLogin(now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _currentUserService.SetUser(employee.Id, employee.Username, employee.DisplayName, employee.Role);

        _logger.LogInformation("Employee '{Username}' logged in successfully as {Role}.", employee.Username, employee.Role);

        var employeeDto = new EmployeeDto(
            employee.Id,
            employee.Username,
            employee.DisplayName,
            employee.Role,
            employee.IsActive,
            employee.CreatedAt,
            employee.UpdatedAt,
            employee.LastLoginAt);

        return LoginResult.Success(employeeDto);
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var username = _currentUserService.Username;
        _currentUserService.ClearUser();
        _logger.LogInformation("Employee '{Username}' logged out.", username ?? "Unknown");
        return Task.CompletedTask;
    }

    public async Task<bool> HasAnyEmployeesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Employees.AnyAsync(cancellationToken);
    }

    public async Task<LoginResult> InitializeFirstAdminAsync(CreateFirstAdminRequest request, CancellationToken cancellationToken = default)
    {
        if (await _dbContext.Employees.AnyAsync(cancellationToken))
        {
            throw new ValidationException("An administrator account already exists. Please log in.");
        }

        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Trim().Length < 3)
        {
            throw new ValidationException(nameof(request.Username), "Username must be at least 3 characters long.");
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ValidationException(nameof(request.DisplayName), "Display name is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 4)
        {
            throw new ValidationException(nameof(request.Password), "Password must be at least 4 characters long.");
        }

        var now = _dateTimeProvider.UtcNow;
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var admin = new Employee(
            Guid.NewGuid(),
            request.Username.Trim(),
            request.DisplayName.Trim(),
            passwordHash,
            EmployeeRole.Administrator,
            now,
            isActive: true);

        _dbContext.Employees.Add(admin);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Initial administrator account '{Username}' created successfully.", admin.Username);

        _currentUserService.SetUser(admin.Id, admin.Username, admin.DisplayName, admin.Role);

        var dto = new EmployeeDto(
            admin.Id,
            admin.Username,
            admin.DisplayName,
            admin.Role,
            admin.IsActive,
            admin.CreatedAt,
            admin.UpdatedAt,
            admin.LastLoginAt);

        return LoginResult.Success(dto);
    }
}
