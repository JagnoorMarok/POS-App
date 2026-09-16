using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Interfaces;

namespace RestaurantManagement.Infrastructure.Persistence;

public class DatabaseInitializer : IDatabaseInitializer
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        RestaurantDbContext dbContext,
        IConfiguration configuration,
        ILogger<DatabaseInitializer> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var rawConnectionString = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        var resolvedConnectionString = DatabasePathResolver.ResolveConnectionString(rawConnectionString);

        _logger.LogInformation("Initializing local SQLite database storage...");

        // Ensure database directory exists
        DatabasePathResolver.EnsureDirectoryExists(resolvedConnectionString);

        try
        {
            var pendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pendingMigrations.Count != 0)
            {
                _logger.LogInformation("Applying {Count} pending EF Core migration(s): {Migrations}",
                    pendingMigrations.Count, string.Join(", ", pendingMigrations));

                await _dbContext.Database.MigrateAsync(cancellationToken);

                _logger.LogInformation("EF Core migrations applied successfully.");
            }
            else
            {
                _logger.LogInformation("Database is up to date. No pending migrations found.");
            }

            // Seed development data if enabled / in Development mode
            var environment = _configuration["Environment"] ?? _configuration["Application:Environment"] ?? "Development";
            if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                await DevelopmentDataSeeder.SeedAsync(_dbContext, _logger, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Critical failure: Database migration or initialization failed.");
            throw;
        }
    }
}
