using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestaurantManagement.Application.Common.Interfaces;
using RestaurantManagement.Application.Common.Models;

namespace RestaurantManagement.Infrastructure.Persistence;

public class DatabaseHealthService : IDatabaseHealthService
{
    private readonly RestaurantDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DatabaseHealthService> _logger;

    public DatabaseHealthService(
        RestaurantDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        ILogger<DatabaseHealthService> logger)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<DatabaseHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var checkedAt = _dateTimeProvider.UtcNow;

        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                stopwatch.Stop();
                _logger.LogWarning("Database health check failed: Unable to connect to SQLite database.");
                return new DatabaseHealthResult(
                    IsHealthy: false,
                    StatusMessage: "Unhealthy: Cannot connect to database file.",
                    CheckedAtUtc: checkedAt,
                    ResponseTime: stopwatch.Elapsed
                );
            }

            // Perform verification query
            await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

            stopwatch.Stop();
            _logger.LogDebug("Database health check succeeded in {ElapsedMilliseconds} ms.", stopwatch.ElapsedMilliseconds);

            return new DatabaseHealthResult(
                IsHealthy: true,
                StatusMessage: "Healthy",
                CheckedAtUtc: checkedAt,
                ResponseTime: stopwatch.Elapsed
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database health check encountered an error.");

            return new DatabaseHealthResult(
                IsHealthy: false,
                StatusMessage: $"Unhealthy: Database query execution error ({ex.GetType().Name}).",
                CheckedAtUtc: checkedAt,
                ResponseTime: stopwatch.Elapsed
            );
        }
    }
}
