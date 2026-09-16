using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IAuthorizationService _authorizationService;

    public bool IsAuthenticated => EmployeeId.HasValue;
    public Guid? EmployeeId { get; private set; }
    public string? Username { get; private set; }
    public string? DisplayName { get; private set; }
    public EmployeeRole? Role { get; private set; }

    public event Action? CurrentUserChanged;

    public CurrentUserService(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public bool IsInRole(EmployeeRole role)
    {
        return Role.HasValue && Role.Value == role;
    }

    public bool CanAccess(string featureTag)
    {
        return _authorizationService.CanAccessFeature(Role, featureTag);
    }

    public void SetUser(Guid employeeId, string username, string displayName, EmployeeRole role)
    {
        EmployeeId = employeeId;
        Username = username;
        DisplayName = displayName;
        Role = role;
        CurrentUserChanged?.Invoke();
    }

    public void ClearUser()
    {
        EmployeeId = null;
        Username = null;
        DisplayName = null;
        Role = null;
        CurrentUserChanged?.Invoke();
    }
}
