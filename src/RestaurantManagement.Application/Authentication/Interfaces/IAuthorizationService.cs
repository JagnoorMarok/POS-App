using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Application.Authentication.Interfaces;

/// <summary>
/// Evaluates authorization rules based on employee role and feature tags.
/// </summary>
public interface IAuthorizationService
{
    bool CanAccessFeature(EmployeeRole? role, string featureTag);
}
