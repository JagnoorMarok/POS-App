using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Authentication.Interfaces;

/// <summary>
/// Maintains and exposes the in-memory authenticated session for the desktop application.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }
    Guid? EmployeeId { get; }
    string? Username { get; }
    string? DisplayName { get; }
    EmployeeRole? Role { get; }

    bool IsInRole(EmployeeRole role);
    bool CanAccess(string featureTag);

    void SetUser(Guid employeeId, string username, string displayName, EmployeeRole role);
    void ClearUser();

    event Action? CurrentUserChanged;
}
