using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Domain.Enums;

namespace RestaurantManagement.Infrastructure.Services;

public class AuthorizationService : IAuthorizationService
{
    public bool CanAccessFeature(EmployeeRole? role, string featureTag)
    {
        if (role == null) return false;

        var tag = featureTag.ToLowerInvariant();

        switch (role.Value)
        {
            case EmployeeRole.Administrator:
                return true; // Full access

            case EmployeeRole.Manager:
                // All operational features except employee account administration & hardware settings
                return tag switch
                {
                    "dashboard" => true,
                    "orders" => true,
                    "kitchen" => true,
                    "menu" => true,
                    "tables" => true,
                    "inventory" => true,
                    "billing" => true,
                    "reports" => true,
                    "employees" => false,
                    "settings" => false,
                    _ => false
                };

            case EmployeeRole.Cashier:
                // Front-of-house sales, orders, kitchen view, and tables
                return tag switch
                {
                    "dashboard" => true,
                    "orders" => true,
                    "kitchen" => true,
                    "tables" => true,
                    "billing" => true,
                    "menu" => false,
                    "inventory" => false,
                    "reports" => false,
                    "employees" => false,
                    "settings" => false,
                    _ => false
                };

            case EmployeeRole.KitchenStaff:
                // Kitchen Display System and Dashboard only
                return tag switch
                {
                    "dashboard" => true,
                    "kitchen" => true,
                    "orders" => false,
                    "menu" => false,
                    "tables" => false,
                    "inventory" => false,
                    "billing" => false,
                    "employees" => false,
                    "reports" => false,
                    "settings" => false,
                    _ => false
                };

            default:
                return false;
        }
    }
}
