using FluentAssertions;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using Xunit;

namespace RestaurantManagement.Application.Tests;

public class EmployeeEntityTests
{
    private readonly DateTime _now = DateTime.UtcNow;

    [Fact]
    public void EmployeeConstructor_ShouldNormalizeUsernameToLowercaseAndTrim()
    {
        // Act
        var employee = new Employee(
            Guid.NewGuid(),
            "  AdminUser  ",
            "  Administrator User  ",
            "pbkdf2$100000$salt$hash",
            EmployeeRole.Administrator,
            _now);

        // Assert
        employee.Username.Should().Be("adminuser");
        employee.DisplayName.Should().Be("Administrator User");
        employee.Role.Should().Be(EmployeeRole.Administrator);
        employee.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmployeeConstructor_ShouldThrowArgumentException_WhenUsernameIsInvalid(string? username)
    {
        var act = () => new Employee(
            Guid.NewGuid(),
            username!,
            "Display Name",
            "pbkdf2$100000$salt$hash",
            EmployeeRole.Cashier,
            _now);

        act.Should().Throw<ArgumentException>().WithParameterName("username");
    }

    [Fact]
    public void UpdateDetails_ShouldUpdateFieldsAndTimestamp()
    {
        var employee = new Employee(
            Guid.NewGuid(),
            "jdoe",
            "John Doe",
            "pbkdf2$100000$salt$hash",
            EmployeeRole.Cashier,
            _now);

        var updateTime = _now.AddHours(1);
        employee.UpdateDetails("Johnathan Doe", EmployeeRole.Manager, updateTime);

        employee.DisplayName.Should().Be("Johnathan Doe");
        employee.Role.Should().Be(EmployeeRole.Manager);
        employee.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void SetPasswordHash_ShouldUpdateHashAndTimestamp()
    {
        var employee = new Employee(
            Guid.NewGuid(),
            "jdoe",
            "John Doe",
            "pbkdf2$100000$salt$hash",
            EmployeeRole.Cashier,
            _now);

        var updateTime = _now.AddHours(2);
        employee.SetPasswordHash("pbkdf2$100000$new_salt$new_hash", updateTime);

        employee.PasswordHash.Should().Be("pbkdf2$100000$new_salt$new_hash");
        employee.UpdatedAt.Should().Be(updateTime);
    }

    [Fact]
    public void DeactivateAndActivate_ShouldChangeStatusProperly()
    {
        var employee = new Employee(
            Guid.NewGuid(),
            "jdoe",
            "John Doe",
            "pbkdf2$100000$salt$hash",
            EmployeeRole.Cashier,
            _now);

        var deactivateTime = _now.AddHours(1);
        employee.Deactivate(deactivateTime);
        employee.IsActive.Should().BeFalse();
        employee.UpdatedAt.Should().Be(deactivateTime);

        var activateTime = _now.AddHours(2);
        employee.Activate(activateTime);
        employee.IsActive.Should().BeTrue();
        employee.UpdatedAt.Should().Be(activateTime);
    }

    [Fact]
    public void RecordLogin_ShouldUpdateLastLoginTimestamp()
    {
        var employee = new Employee(
            Guid.NewGuid(),
            "jdoe",
            "John Doe",
            "pbkdf2$100000$salt$hash",
            EmployeeRole.Cashier,
            _now);

        var loginTime = _now.AddMinutes(30);
        employee.RecordLogin(loginTime);

        employee.LastLoginAt.Should().Be(loginTime);
    }
}
