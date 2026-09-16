namespace RestaurantManagement.Application.Common.Models;

/// <summary>
/// Result of a database connectivity and query health check.
/// </summary>
public record DatabaseHealthResult(
    bool IsHealthy,
    string StatusMessage,
    DateTime CheckedAtUtc,
    TimeSpan? ResponseTime
);
