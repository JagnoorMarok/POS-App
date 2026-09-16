using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Employees.DTOs;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Security;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class EmployeeServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPasswordHasher _passwordHasher;

    public EmployeeServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantEmployeeTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "employee_test.db");
        _connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        _dateTimeProvider = new DateTimeProvider();
        _passwordHasher = new PasswordHasher();

        using var context = new RestaurantDbContext(_dbContextOptions);
        context.Database.Migrate();
    }

    public void Dispose()
    {
        try
        {
            var directory = Path.GetDirectoryName(_testDbPath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public async Task CreateEmployeeAsync_ShouldCreateEmployee_WhenValid()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new EmployeeService(context, _passwordHasher, _dateTimeProvider, NullLogger<EmployeeService>.Instance);

        var request = new CreateEmployeeRequest("alice", "Alice Smith", "Password123!", EmployeeRole.Manager, true);

        // Act
        var result = await service.CreateEmployeeAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("alice");
        result.DisplayName.Should().Be("Alice Smith");
        result.Role.Should().Be(EmployeeRole.Manager);
        result.IsActive.Should().BeTrue();

        var dbEmployee = await context.Employees.FirstOrDefaultAsync(e => e.Id == result.Id);
        dbEmployee.Should().NotBeNull();
        _passwordHasher.VerifyPassword("Password123!", dbEmployee!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task CreateEmployeeAsync_ShouldThrowValidationException_WhenUsernameAlreadyExists()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new EmployeeService(context, _passwordHasher, _dateTimeProvider, NullLogger<EmployeeService>.Instance);

        await service.CreateEmployeeAsync(new CreateEmployeeRequest("alice", "Alice Smith", "Password123!", EmployeeRole.Manager, true));

        // Act
        var act = () => service.CreateEmployeeAsync(new CreateEmployeeRequest("ALICE", "Alice Second", "Password456!", EmployeeRole.Cashier, true));

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateEmployeeAsync_ShouldUpdateFields()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new EmployeeService(context, _passwordHasher, _dateTimeProvider, NullLogger<EmployeeService>.Instance);

        var created = await service.CreateEmployeeAsync(new CreateEmployeeRequest("bob", "Bob Jones", "Password123!", EmployeeRole.Cashier, true));

        // Act
        var updated = await service.UpdateEmployeeAsync(new UpdateEmployeeRequest(created.Id, "Robert Jones", EmployeeRole.Manager, false));

        // Assert
        updated.DisplayName.Should().Be("Robert Jones");
        updated.Role.Should().Be(EmployeeRole.Manager);
        updated.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task AdminResetPasswordAsync_ShouldChangePassword()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new EmployeeService(context, _passwordHasher, _dateTimeProvider, NullLogger<EmployeeService>.Instance);

        var created = await service.CreateEmployeeAsync(new CreateEmployeeRequest("charlie", "Charlie Brown", "OldPass123!", EmployeeRole.KitchenStaff, true));

        // Act
        await service.AdminResetPasswordAsync(new AdminResetPasswordRequest(created.Id, "NewPass456!"));

        // Assert
        var dbEmployee = await context.Employees.FirstAsync(e => e.Id == created.Id);
        _passwordHasher.VerifyPassword("OldPass123!", dbEmployee.PasswordHash).Should().BeFalse();
        _passwordHasher.VerifyPassword("NewPass456!", dbEmployee.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task DeactivateEmployeeAsync_ShouldPreventLastActiveAdminFromBeingDeactivated()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new EmployeeService(context, _passwordHasher, _dateTimeProvider, NullLogger<EmployeeService>.Instance);

        var admin = await service.CreateEmployeeAsync(new CreateEmployeeRequest("admin", "Admin", "AdminPass123!", EmployeeRole.Administrator, true));

        // Act
        var act = () => service.DeactivateEmployeeAsync(admin.Id);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Cannot deactivate the only active Administrator*");
    }

    [Fact]
    public async Task DeleteEmployeeAsync_ShouldDeleteEmployee_WhenNotSoleAdmin()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new EmployeeService(context, _passwordHasher, _dateTimeProvider, NullLogger<EmployeeService>.Instance);

        var staff = await service.CreateEmployeeAsync(new CreateEmployeeRequest("staff1", "Staff One", "Pass1234!", EmployeeRole.Cashier, true));

        // Act
        await service.DeleteEmployeeAsync(staff.Id);

        // Assert
        var deleted = await service.GetEmployeeByIdAsync(staff.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteEmployeeAsync_ShouldPreventSoleAdminFromBeingDeleted()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var service = new EmployeeService(context, _passwordHasher, _dateTimeProvider, NullLogger<EmployeeService>.Instance);

        var admin = await service.CreateEmployeeAsync(new CreateEmployeeRequest("soleadmin", "Sole Admin", "AdminPass123!", EmployeeRole.Administrator, true));

        // Act
        var act = () => service.DeleteEmployeeAsync(admin.Id);

        // Assert
        await act.Should().ThrowAsync<ValidationException>().WithMessage("*Cannot delete the only Administrator account*");
    }
}
