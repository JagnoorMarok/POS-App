namespace RestaurantManagement.Application.Common.Interfaces;

/// <summary>
/// Service contract to initialize database storage and apply pending EF Core migrations at startup.
/// </summary>
public interface IDatabaseInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
