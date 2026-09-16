using RestaurantManagement.Application.Employees.DTOs;

namespace RestaurantManagement.Application.Authentication.DTOs;

public record LoginRequest(
    string Username,
    string Password);

public record LoginResult(
    bool Succeeded,
    string? ErrorMessage = null,
    EmployeeDto? Employee = null)
{
    public static LoginResult Success(EmployeeDto employee) => new(true, null, employee);
    public static LoginResult Failed(string errorMessage) => new(false, errorMessage, null);
}

public record CreateFirstAdminRequest(
    string Username,
    string DisplayName,
    string Password);
