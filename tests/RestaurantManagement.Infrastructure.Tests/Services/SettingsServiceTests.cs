using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Application.Authentication.Interfaces;
using RestaurantManagement.Application.Common.Exceptions;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Settings.DTOs;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Domain.Enums;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Services;

public class SettingsServiceTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;
    private readonly RestaurantDbContext _dbContext;
    private readonly FakeDateTimeProvider _dateTimeProvider = new();
    private readonly FakeCurrentUserService _currentUserService = new();
    private readonly IAuthorizationService _authorizationService = new AuthorizationService();
    private readonly IAppInfoService _appInfoService;
    private readonly SettingsService _settingsService;

    private readonly DateTime _utcNow = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);

    public SettingsServiceTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantSettingsTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "settings_test.db");
        var connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(connectionString)
            .Options;

        _dbContext = new RestaurantDbContext(_dbContextOptions);
        _dbContext.Database.Migrate();

        _dateTimeProvider.UtcNow = _utcNow;
        _currentUserService.SetUser(Guid.NewGuid(), "admin_sys", "System Admin", EmployeeRole.Administrator);

        var config = new ConfigurationBuilder().Build();
        _appInfoService = new AppInfoService(config, _dateTimeProvider);

        _settingsService = new SettingsService(
            _dbContext,
            _dateTimeProvider,
            _appInfoService,
            _currentUserService,
            _authorizationService,
            NullLogger<SettingsService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        try
        {
            if (File.Exists(_testDbPath))
            {
                File.Delete(_testDbPath);
            }
        }
        catch
        {
            // Ignore temp file cleanup lock
        }
    }

    [Fact]
    public async Task GetRestaurantProfileAsync_WhenNoneExists_ShouldCreateAndReturnDefaultProfile()
    {
        // Act
        var profile = await _settingsService.GetRestaurantProfileAsync();

        // Assert
        profile.Should().NotBeNull();
        profile.RestaurantName.Should().Be("The Grand Bistro");
        profile.CurrencyCode.Should().Be("INR");
        profile.DefaultTaxRatePercent.Should().Be(5.0m);
    }

    [Fact]
    public async Task UpdateRestaurantProfileAsync_ShouldPersistChangesAndTaxRate()
    {
        // Arrange
        await _settingsService.GetRestaurantProfileAsync();

        var request = new UpdateRestaurantProfileRequest(
            RestaurantName: "Royal Spice Palace",
            Address: "456 Royal Blvd",
            PhoneNumber: "+91 11223 34455",
            CurrencyCode: "INR",
            CurrencySymbol: "₹",
            GstOrTaxNumber: "GSTIN99ZZZZZ9999Z9Z9",
            DefaultTaxRatePercent: 8.5m);

        // Act
        var updated = await _settingsService.UpdateRestaurantProfileAsync(request);

        // Assert
        updated.RestaurantName.Should().Be("Royal Spice Palace");
        updated.Address.Should().Be("456 Royal Blvd");
        updated.DefaultTaxRatePercent.Should().Be(8.5m);

        // Verify retrieval matches
        var retrieved = await _settingsService.GetRestaurantProfileAsync();
        retrieved.RestaurantName.Should().Be("Royal Spice Palace");
        retrieved.DefaultTaxRatePercent.Should().Be(8.5m);
    }

    [Fact]
    public async Task GetSystemDiagnosticsAsync_ShouldReturnValidMetrics()
    {
        // Act
        var diag = await _settingsService.GetSystemDiagnosticsAsync();

        // Assert
        diag.Should().NotBeNull();
        diag.IsDatabaseHealthy.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateRestaurantProfileAsync_InvalidTaxRate_ShouldThrowValidationException()
    {
        // Arrange
        var request = new UpdateRestaurantProfileRequest(
            RestaurantName: "Invalid Cafe",
            Address: null,
            PhoneNumber: null,
            CurrencyCode: "INR",
            CurrencySymbol: "₹",
            GstOrTaxNumber: null,
            DefaultTaxRatePercent: 150m); // > 100%

        // Act & Assert
        var act = async () => await _settingsService.UpdateRestaurantProfileAsync(request);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateRestaurantProfileAsync_AsCashier_ShouldThrowUnauthorizedAccessException()
    {
        // Arrange
        _currentUserService.SetUser(Guid.NewGuid(), "cashier_dave", "Dave Cashier", EmployeeRole.Cashier);

        var request = new UpdateRestaurantProfileRequest(
            RestaurantName: "Hacked Cafe",
            Address: null,
            PhoneNumber: null,
            CurrencyCode: "INR",
            CurrencySymbol: "₹",
            GstOrTaxNumber: null,
            DefaultTaxRatePercent: 5.0m);

        // Act & Assert
        var act = async () => await _settingsService.UpdateRestaurantProfileAsync(request);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
