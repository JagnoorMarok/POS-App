using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using RestaurantManagement.Domain.Entities;
using RestaurantManagement.Infrastructure.Persistence;
using RestaurantManagement.Infrastructure.Services;
using Xunit;

namespace RestaurantManagement.Infrastructure.Tests.Persistence;

public class DatabaseInfrastructureTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly string _connectionString;
    private readonly DbContextOptions<RestaurantDbContext> _dbContextOptions;

    public DatabaseInfrastructureTests()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "RestaurantManagementTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempFolder);
        _testDbPath = Path.Combine(tempFolder, "test_restaurant.db");
        _connectionString = $"Data Source={_testDbPath}";

        _dbContextOptions = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlite(_connectionString)
            .Options;
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
            // Best effort cleanup for temporary test files
        }
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabaseAndApplyMigrations()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "ConnectionStrings:DefaultConnection", _connectionString }
            })
            .Build();

        await using var context = new RestaurantDbContext(_dbContextOptions);
        var initializer = new DatabaseInitializer(context, configuration, NullLogger<DatabaseInitializer>.Instance);

        // Act
        await initializer.InitializeAsync();

        // Assert
        File.Exists(_testDbPath).Should().BeTrue();
        var canConnect = await context.Database.CanConnectAsync();
        canConnect.Should().BeTrue();

        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        pendingMigrations.Should().BeEmpty();
    }

    [Fact]
    public async Task Persistence_ShouldInsertAndRetrieveApplicationSettingAcrossDbContextLifetimes()
    {
        // Arrange: Migrate test database
        await using (var migrateContext = new RestaurantDbContext(_dbContextOptions))
        {
            await migrateContext.Database.MigrateAsync();
        }

        var settingId = Guid.NewGuid();
        var settingKey = "App.Theme";
        var settingValue = "Dark";
        var createdAt = DateTime.UtcNow;

        // Act 1: Insert entity and save
        await using (var context1 = new RestaurantDbContext(_dbContextOptions))
        {
            var setting = new ApplicationSetting(settingId, settingKey, settingValue, createdAt);
            context1.ApplicationSettings.Add(setting);
            await context1.SaveChangesAsync();
        }

        // Act 2: Read back in fresh DbContext
        await using (var context2 = new RestaurantDbContext(_dbContextOptions))
        {
            var retrieved = await context2.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == settingKey);

            // Assert 1
            retrieved.Should().NotBeNull();
            retrieved!.Id.Should().Be(settingId);
            retrieved.Key.Should().Be(settingKey);
            retrieved.Value.Should().Be(settingValue);

            // Act 3: Update value
            var updatedAt = DateTime.UtcNow;
            retrieved.UpdateValue("Light", updatedAt);
            await context2.SaveChangesAsync();
        }

        // Assert 2: Verify update in another fresh DbContext
        await using (var context3 = new RestaurantDbContext(_dbContextOptions))
        {
            var updated = await context3.ApplicationSettings.FirstOrDefaultAsync(s => s.Key == settingKey);
            updated.Should().NotBeNull();
            updated!.Value.Should().Be("Light");
            updated.UpdatedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task DatabaseHealthService_ShouldReportHealthy_WhenDatabaseIsOperational()
    {
        // Arrange
        await using var context = new RestaurantDbContext(_dbContextOptions);
        await context.Database.MigrateAsync();

        var dateTimeProvider = new DateTimeProvider();
        var healthService = new DatabaseHealthService(context, dateTimeProvider, NullLogger<DatabaseHealthService>.Instance);

        // Act
        var result = await healthService.CheckHealthAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeTrue();
        result.StatusMessage.Should().Be("Healthy");
        result.ResponseTime.Should().NotBeNull();
    }
}
