using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Employees.DTOs;
using RestaurantManagement.Application.Employees.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;

namespace RestaurantManagement.Infrastructure.Services;

public class EmployeeService : IEmployeeService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<EmployeeService> _logger;

    public EmployeeService(
        RestaurantDbContext dbContext,
        IPasswordHasher passwordHasher,
        IDateTimeProvider dateTimeProvider,
        ILogger<EmployeeService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetEmployeesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Employees.AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }

        var employees = await query
            .OrderBy(e => e.Role)
            .ThenBy(e => e.DisplayName)
            .Select(e => new EmployeeDto(
                e.Id,
                e.Username,
                e.DisplayName,
                e.Role,
                e.IsActive,
                e.CreatedAt,
                e.UpdatedAt,
                e.LastLoginAt))
            .ToListAsync(cancellationToken);

        return employees;
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDto(
                e.Id,
                e.Username,
                e.DisplayName,
                e.Role,
                e.IsActive,
                e.CreatedAt,
                e.UpdatedAt,
                e.LastLoginAt))
            .FirstOrDefaultAsync(cancellationToken);

        return employee;
    }

    public async Task<EmployeeDto?> GetEmployeeByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Where(e => e.Username == normalized)
            .Select(e => new EmployeeDto(
                e.Id,
                e.Username,
                e.DisplayName,
                e.Role,
                e.IsActive,
                e.CreatedAt,
                e.UpdatedAt,
                e.LastLoginAt))
            .FirstOrDefaultAsync(cancellationToken);

        return employee;
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
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

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        var usernameExists = await _dbContext.Employees
            .AnyAsync(e => e.Username == normalizedUsername, cancellationToken);

        if (usernameExists)
        {
            throw new ValidationException(nameof(request.Username), $"An employee with username '{normalizedUsername}' already exists.");
        }

        var now = _dateTimeProvider.UtcNow;
        var passwordHash = _passwordHasher.HashPassword(request.Password);

        var employee = new Employee(
            Guid.NewGuid(),
            normalizedUsername,
            request.DisplayName.Trim(),
            passwordHash,
            request.Role,
            now,
            request.IsActive);

        _dbContext.Employees.Add(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created employee '{Username}' with role {Role} (ID: {EmployeeId})", employee.Username, employee.Role, employee.Id);

        return new EmployeeDto(
            employee.Id,
            employee.Username,
            employee.DisplayName,
            employee.Role,
            employee.IsActive,
            employee.CreatedAt,
            employee.UpdatedAt,
            employee.LastLoginAt);
    }

    public async Task<EmployeeDto> UpdateEmployeeAsync(UpdateEmployeeRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ValidationException(nameof(request.DisplayName), "Display name is required.");
        }

        var employee = await _dbContext.Employees.FindAsync(new object[] { request.Id }, cancellationToken);
        if (employee == null)
        {
            throw new NotFoundException(nameof(Employee), request.Id);
        }

        var now = _dateTimeProvider.UtcNow;

        if (employee.Role == EmployeeRole.Administrator && (!request.IsActive || request.Role != EmployeeRole.Administrator))
        {
            var activeAdminCount = await _dbContext.Employees
                .CountAsync(e => e.Role == EmployeeRole.Administrator && e.IsActive && e.Id != request.Id, cancellationToken);
            if (activeAdminCount == 0)
            {
                throw new ValidationException("Cannot modify or deactivate the only active Administrator account.");
            }
        }

        employee.UpdateDetails(request.DisplayName.Trim(), request.Role, now);

        if (request.IsActive && !employee.IsActive)
        {
            employee.Activate(now);
        }
        else if (!request.IsActive && employee.IsActive)
        {
            employee.Deactivate(now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated employee '{Username}' (ID: {EmployeeId})", employee.Username, employee.Id);

        return new EmployeeDto(
            employee.Id,
            employee.Username,
            employee.DisplayName,
            employee.Role,
            employee.IsActive,
            employee.CreatedAt,
            employee.UpdatedAt,
            employee.LastLoginAt);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FindAsync(new object[] { request.EmployeeId }, cancellationToken);
        if (employee == null)
        {
            throw new NotFoundException(nameof(Employee), request.EmployeeId);
        }

        var isCurrentPasswordValid = _passwordHasher.VerifyPassword(request.CurrentPassword, employee.PasswordHash);
        if (!isCurrentPasswordValid)
        {
            throw new ValidationException(nameof(request.CurrentPassword), "Current password is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
        {
            throw new ValidationException(nameof(request.NewPassword), "New password must be at least 4 characters long.");
        }

        var now = _dateTimeProvider.UtcNow;
        var newHash = _passwordHasher.HashPassword(request.NewPassword);
        employee.SetPasswordHash(newHash, now);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Password changed for employee '{Username}' (ID: {EmployeeId})", employee.Username, employee.Id);
    }

    public async Task AdminResetPasswordAsync(AdminResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FindAsync(new object[] { request.EmployeeId }, cancellationToken);
        if (employee == null)
        {
            throw new NotFoundException(nameof(Employee), request.EmployeeId);
        }

        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
        {
            throw new ValidationException(nameof(request.NewPassword), "New password must be at least 4 characters long.");
        }

        var now = _dateTimeProvider.UtcNow;
        var newHash = _passwordHasher.HashPassword(request.NewPassword);
        employee.SetPasswordHash(newHash, now);

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Password reset by admin for employee '{Username}' (ID: {EmployeeId})", employee.Username, employee.Id);
    }

    public async Task ChangeRoleAsync(Guid employeeId, EmployeeRole newRole, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FindAsync(new object[] { employeeId }, cancellationToken);
        if (employee == null)
        {
            throw new NotFoundException(nameof(Employee), employeeId);
        }

        employee.ChangeRole(newRole, _dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Changed role for employee '{Username}' to {Role}", employee.Username, newRole);
    }

    public async Task DeactivateEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FindAsync(new object[] { id }, cancellationToken);
        if (employee == null)
        {
            throw new NotFoundException(nameof(Employee), id);
        }

        if (employee.Role == EmployeeRole.Administrator)
        {
            var activeAdminCount = await _dbContext.Employees
                .CountAsync(e => e.Role == EmployeeRole.Administrator && e.IsActive && e.Id != id, cancellationToken);
            if (activeAdminCount == 0)
            {
                throw new ValidationException("Cannot deactivate the only active Administrator account.");
            }
        }

        employee.Deactivate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deactivated employee account '{Username}' (ID: {EmployeeId})", employee.Username, employee.Id);
    }

    public async Task ActivateEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FindAsync(new object[] { id }, cancellationToken);
        if (employee == null)
        {
            throw new NotFoundException(nameof(Employee), id);
        }

        employee.Activate(_dateTimeProvider.UtcNow);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Activated employee account '{Username}' (ID: {EmployeeId})", employee.Username, employee.Id);
    }

    public async Task DeleteEmployeeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees.FindAsync(new object[] { id }, cancellationToken);
        if (employee == null)
        {
            throw new NotFoundException(nameof(Employee), id);
        }

        if (employee.Role == EmployeeRole.Administrator)
        {
            var adminCount = await _dbContext.Employees
                .CountAsync(e => e.Role == EmployeeRole.Administrator && e.Id != id, cancellationToken);
            if (adminCount == 0)
            {
                throw new ValidationException("Cannot delete the only Administrator account in the system.");
            }
        }

        _dbContext.Employees.Remove(employee);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Deleted employee account '{Username}' (ID: {EmployeeId})", employee.Username, id);
    }
}
