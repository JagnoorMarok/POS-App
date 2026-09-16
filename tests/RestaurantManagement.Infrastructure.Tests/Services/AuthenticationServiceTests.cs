using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Authentication.DTOs;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Security;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class AuthenticationServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthorizationService _authorizationService;
    private readonly CurrentUserService _currentUserService;

    public AuthenticationServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantAuthTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "auth_test.db");
        _connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(_connectionString)
            .Options;

        _dateTimeProvider = new DateTimeProvider();
        _passwordHasher = new PasswordHasher();
        _authorizationService = new AuthorizationService();
        _currentUserService = new CurrentUserService(_authorizationService);

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
    public async Task HasAnyEmployeesAsync_ShouldReturnFalse_WhenDbIsEmpty()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var authService = new AuthenticationService(context, _passwordHasher, _currentUserService, _dateTimeProvider, NullLogger<AuthenticationService>.Instance);

        // Act
        var hasEmployees = await authService.HasAnyEmployeesAsync();

        // Assert
        hasEmployees.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeFirstAdminAsync_ShouldCreateFirstAdminAndSetCurrentSession()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var authService = new AuthenticationService(context, _passwordHasher, _currentUserService, _dateTimeProvider, NullLogger<AuthenticationService>.Instance);

        var request = new CreateFirstAdminRequest("admin", "System Administrator", "AdminPass123!");

        // Act
        var result = await authService.InitializeFirstAdminAsync(request);

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Employee.Should().NotBeNull();
        result.Employee!.Username.Should().Be("admin");
        result.Employee.Role.Should().Be(EmployeeRole.Administrator);

        _currentUserService.IsAuthenticated.Should().BeTrue();
        _currentUserService.Username.Should().Be("admin");

        var hasEmployees = await authService.HasAnyEmployeesAsync();
        hasEmployees.Should().BeTrue();
    }

    [Fact]
    public async Task LoginAsync_ShouldSucceed_WithCorrectCredentials()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var hash = _passwordHasher.HashPassword("SecretPassword123!");
        var employee = new Employee(Guid.NewGuid(), "jdoe", "John Doe", hash, EmployeeRole.Cashier, _dateTimeProvider.UtcNow);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var authService = new AuthenticationService(context, _passwordHasher, _currentUserService, _dateTimeProvider, NullLogger<AuthenticationService>.Instance);

        // Act
        var result = await authService.LoginAsync(new LoginRequest("JDOE", "SecretPassword123!"));

        // Assert
        result.Succeeded.Should().BeTrue();
        result.Employee.Should().NotBeNull();
        result.Employee!.Username.Should().Be("jdoe");
        result.Employee.Role.Should().Be(EmployeeRole.Cashier);

        _currentUserService.IsAuthenticated.Should().BeTrue();
        _currentUserService.EmployeeId.Should().Be(employee.Id);
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WithIncorrectPassword()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var hash = _passwordHasher.HashPassword("SecretPassword123!");
        var employee = new Employee(Guid.NewGuid(), "jdoe", "John Doe", hash, EmployeeRole.Cashier, _dateTimeProvider.UtcNow);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var authService = new AuthenticationService(context, _passwordHasher, _currentUserService, _dateTimeProvider, NullLogger<AuthenticationService>.Instance);

        // Act
        var result = await authService.LoginAsync(new LoginRequest("jdoe", "WrongPassword"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid username or password");
        _currentUserService.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_ShouldFail_WhenAccountIsInactive()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var hash = _passwordHasher.HashPassword("SecretPassword123!");
        var employee = new Employee(Guid.NewGuid(), "inactive_user", "Inactive User", hash, EmployeeRole.Cashier, _dateTimeProvider.UtcNow);
        employee.Deactivate(_dateTimeProvider.UtcNow);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var authService = new AuthenticationService(context, _passwordHasher, _currentUserService, _dateTimeProvider, NullLogger<AuthenticationService>.Instance);

        // Act
        var result = await authService.LoginAsync(new LoginRequest("inactive_user", "SecretPassword123!"));

        // Assert
        result.Succeeded.Should().BeFalse();
        result.ErrorMessage.Should().Contain("deactivated");
        _currentUserService.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearSession()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        var hash = _passwordHasher.HashPassword("SecretPassword123!");
        var employee = new Employee(Guid.NewGuid(), "jdoe", "John Doe", hash, EmployeeRole.Cashier, _dateTimeProvider.UtcNow);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        var authService = new AuthenticationService(context, _passwordHasher, _currentUserService, _dateTimeProvider, NullLogger<AuthenticationService>.Instance);
        await authService.LoginAsync(new LoginRequest("jdoe", "SecretPassword123!"));
        _currentUserService.IsAuthenticated.Should().BeTrue();

        // Act
        await authService.LogoutAsync();

        // Assert
        _currentUserService.IsAuthenticated.Should().BeFalse();
        _currentUserService.EmployeeId.Should().BeNull();
        _currentUserService.Username.Should().BeNull();
    }
}
