using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Employees.DTOs;

public record EmployeeDto(
    Guid Id,
    string Username,
    string DisplayName,
    EmployeeRole Role,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastLoginAt);

public record CreateEmployeeRequest(
    string Username,
    string DisplayName,
    string Password,
    EmployeeRole Role,
    bool IsActive = true);

public record UpdateEmployeeRequest(
    Guid Id,
    string DisplayName,
    EmployeeRole Role,
    bool IsActive = true);

public record ChangePasswordRequest(
    Guid EmployeeId,
    string CurrentPassword,
    string NewPassword);

public record AdminResetPasswordRequest(
    Guid EmployeeId,
    string NewPassword);
