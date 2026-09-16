using RestaurantManagement.Application.Common.Models;

namespace RestaurantManagement.Application.Common.Interfaces;

/// <summary>
/// Service contract to evaluate database connectivity and query health.
/// </summary>
public interface IDatabaseHealthService
{
    Task<DatabaseHealthResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}
