using FluentAssertions;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class AuthorizationServiceTests
{
    private readonly AuthorizationService _authzService = new();

    [Theory]
    [InlineData("Employees", EmployeeRole.Administrator, true)]
    [InlineData("Employees", EmployeeRole.Manager, false)]
    [InlineData("Employees", EmployeeRole.Cashier, false)]
    [InlineData("Employees", EmployeeRole.KitchenStaff, false)]
    [InlineData("Menu", EmployeeRole.Administrator, true)]
    [InlineData("Menu", EmployeeRole.Manager, true)]
    [InlineData("Menu", EmployeeRole.Cashier, false)]
    [InlineData("Menu", EmployeeRole.KitchenStaff, false)]
    [InlineData("Tables", EmployeeRole.Administrator, true)]
    [InlineData("Tables", EmployeeRole.Manager, true)]
    [InlineData("Tables", EmployeeRole.Cashier, true)]
    [InlineData("Tables", EmployeeRole.KitchenStaff, false)]
    [InlineData("Orders", EmployeeRole.Administrator, true)]
    [InlineData("Orders", EmployeeRole.Manager, true)]
    [InlineData("Orders", EmployeeRole.Cashier, true)]
    [InlineData("Orders", EmployeeRole.KitchenStaff, false)]
    [InlineData("Kitchen", EmployeeRole.Administrator, true)]
    [InlineData("Kitchen", EmployeeRole.Manager, true)]
    [InlineData("Kitchen", EmployeeRole.Cashier, true)]
    [InlineData("Kitchen", EmployeeRole.KitchenStaff, true)]
    [InlineData("Dashboard", EmployeeRole.Administrator, true)]
    [InlineData("Dashboard", EmployeeRole.Manager, true)]
    [InlineData("Dashboard", EmployeeRole.Cashier, true)]
    [InlineData("Dashboard", EmployeeRole.KitchenStaff, true)]
    public void CanAccessFeature_ShouldReturnExpectedPermission_ForRole(string featureTag, EmployeeRole role, bool expectedResult)
    {
        // Act
        var result = _authzService.CanAccessFeature(role, featureTag);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void CanAccessFeature_ShouldReturnFalse_WhenRoleIsNull()
    {
        _authzService.CanAccessFeature(null, "Dashboard").Should().BeFalse();
        _authzService.CanAccessFeature(null, "Kitchen").Should().BeFalse();
        _authzService.CanAccessFeature(null, "Employees").Should().BeFalse();
    }
}
